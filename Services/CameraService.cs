using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;
using AForge.Video;
using AForge.Video.DirectShow;

namespace WaybridgeApp.Services;

public sealed class CameraService : IDisposable
{
    private readonly object _sync = new();
    private FilterInfoCollection? _videoDevices;
    private VideoCaptureDevice? _videoSource;
    private Bitmap? _lastFrame;

    public event Action<BitmapImage>? FrameReady;
    public event Action<string>? ErrorOccurred;

    public IReadOnlyList<string> GetCameraNames()
    {
        _videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
        return _videoDevices.Cast<FilterInfo>().Select(d => d.Name).ToList();
    }

    public void Start(int deviceIndex)
    {
        try
        {
            Stop();

            if (_videoDevices is null)
            {
                _videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            }

            if (_videoDevices.Count == 0 || deviceIndex < 0 || deviceIndex >= _videoDevices.Count)
            {
                throw new InvalidOperationException("No valid camera device selected.");
            }

            _videoSource = new VideoCaptureDevice(_videoDevices[deviceIndex].MonikerString);
            _videoSource.NewFrame += OnNewFrame;
            _videoSource.Start();
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Camera start failed: {ex.Message}");
        }
    }

    private void OnNewFrame(object sender, NewFrameEventArgs eventArgs)
    {
        try
        {
            using var frame = (Bitmap)eventArgs.Frame.Clone();

            lock (_sync)
            {
                _lastFrame?.Dispose();
                _lastFrame = (Bitmap)frame.Clone();
            }

            using var ms = new MemoryStream();
            frame.Save(ms, ImageFormat.Bmp);
            ms.Position = 0;

            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = ms;
            bitmapImage.EndInit();
            bitmapImage.Freeze();

            FrameReady?.Invoke(bitmapImage);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Camera frame error: {ex.Message}");
        }
    }

    // Camera capture stores the current frame as JPEG for traceability.
    public string CaptureToJpeg(string imagesFolder, string vehicleNo)
    {
        lock (_sync)
        {
            if (_lastFrame is null)
            {
                throw new InvalidOperationException("No frame is available to capture.");
            }

            Directory.CreateDirectory(imagesFolder);
            var safeVehicle = string.Concat(vehicleNo.Where(ch => !Path.GetInvalidFileNameChars().Contains(ch)));
            var fileName = $"{safeVehicle}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
            var filePath = Path.Combine(imagesFolder, fileName);
            _lastFrame.Save(filePath, ImageFormat.Jpeg);
            return filePath;
        }
    }

    public void Stop()
    {
        if (_videoSource is null)
        {
            return;
        }

        try
        {
            _videoSource.NewFrame -= OnNewFrame;
            if (_videoSource.IsRunning)
            {
                _videoSource.SignalToStop();
                _videoSource.WaitForStop();
            }

            _videoSource = null;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Camera stop failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        Stop();
        lock (_sync)
        {
            _lastFrame?.Dispose();
            _lastFrame = null;
        }
    }
}
