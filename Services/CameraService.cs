using OpenCvSharp.WpfExtensions;
using OpenCvSharp;
using System.Windows.Media.Imaging;
using System.IO;
namespace WaybridgeApp.Services;

public sealed class CameraService : IDisposable
{
    private readonly object _sync = new();
    private VideoCapture? _capture;
    private CancellationTokenSource? _previewCts;
    private Task? _previewTask;
    private Mat? _lastFrame;

    public event Action<BitmapSource>? FrameReady;
    public event Action<string>? ErrorOccurred;

    public bool IsRunning { get; private set; }

    public async Task<IReadOnlyList<string>> GetCameraNamesAsync(int maxDevices = 10)
    {
        return await Task.Run(() =>
        {
            var cameras = new List<string>();
            for (var i = 0; i < maxDevices; i++)
            {
                using var probe = new VideoCapture(i, VideoCaptureAPIs.DSHOW);
                if (!probe.IsOpened())
                {
                    continue;
                }

                cameras.Add($"Camera {i}");
                probe.Release();
            }

            return (IReadOnlyList<string>)cameras;
        });
    }

    public async Task StartAsync(int deviceIndex, CancellationToken cancellationToken = default)
    {
        await StopAsync();

        var capture = new VideoCapture(deviceIndex, VideoCaptureAPIs.DSHOW);
        if (!capture.IsOpened())
        {
            capture.Dispose();
            throw new InvalidOperationException($"Unable to open camera device index {deviceIndex}.");
        }

        capture.FrameWidth = 1280;
        capture.FrameHeight = 720;
        capture.Fps = 30;

        lock (_sync)
        {
            _capture = capture;
            _lastFrame?.Dispose();
            _lastFrame = null;
        }

        _previewCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _previewTask = Task.Run(() => PreviewLoop(_previewCts.Token), _previewCts.Token);
        IsRunning = true;

        await Task.CompletedTask;
    }

    public async Task<string> CaptureFrameAsync(string imagesFolder, string vehicleNo, CancellationToken cancellationToken = default)
    {
        var frame = await Task.Run(() =>
        {
            lock (_sync)
            {
                if (_lastFrame is null || _lastFrame.Empty())
                {
                    throw new InvalidOperationException("No frame is available to capture.");
                }

                return _lastFrame.Clone();
            }
        }, cancellationToken);

        try
        {
            Directory.CreateDirectory(imagesFolder);

            var safeVehicle = string.Concat(vehicleNo.Where(ch => !Path.GetInvalidFileNameChars().Contains(ch)));
            if (string.IsNullOrWhiteSpace(safeVehicle))
            {
                safeVehicle = "vehicle";
            }

            var fileName = $"{safeVehicle}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
            var filePath = Path.Combine(imagesFolder, fileName);

            await Task.Run(() => Cv2.ImWrite(filePath, frame), cancellationToken);
            return filePath;
        }
        finally
        {
            frame.Dispose();
        }
    }

    public async Task StopAsync()
    {
        IsRunning = false;

        var cts = _previewCts;
        _previewCts = null;

        if (cts is not null)
        {
            cts.Cancel();
            cts.Dispose();
        }

        var previewTask = _previewTask;
        _previewTask = null;
        if (previewTask is not null)
        {
            try
            {
                await previewTask;
            }
            catch (OperationCanceledException)
            {
                // expected on stop
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"Camera preview loop stopped with error: {ex.Message}");
            }
        }

        lock (_sync)
        {
            _capture?.Release();
            _capture?.Dispose();
            _capture = null;

            _lastFrame?.Dispose();
            _lastFrame = null;
        }
    }

    private void PreviewLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                Mat frame = new();
                try
                {
                    VideoCapture? capture;
                    lock (_sync)
                    {
                        capture = _capture;
                    }

                    if (capture is null || !capture.IsOpened())
                    {
                        frame.Dispose();
                        break;
                    }

                    if (!capture.Read(frame) || frame.Empty())
                    {
                        frame.Dispose();
                        continue;
                    }

                    lock (_sync)
                    {
                        _lastFrame?.Dispose();
                        _lastFrame = frame.Clone();
                    }

                    var preview = BitmapSourceConverter.ToBitmapSource(frame);
                    preview.Freeze();
                    FrameReady?.Invoke(preview);
                }
                finally
                {
                    frame.Dispose();
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"Camera frame error: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
    }
}
