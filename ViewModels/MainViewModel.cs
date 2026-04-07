using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using WaybridgeApp.Models;
using WaybridgeApp.Services;

namespace WaybridgeApp.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly SerialService _serialService;
    private readonly CameraService _cameraService;
    private readonly DatabaseService _databaseService;
    private readonly Queue<(double Weight, DateTime Time)> _recentReadings = new();
    private const int StabilityReadingsCount = 5;
    private const double StabilityTolerance = 2.0;

    private string _selectedComPort = string.Empty;
    private string _connectionStatus = "Disconnected";
    private double _currentWeight;
    private bool _isWeightStable;
    private string _applicationNo = string.Empty;
    private string _vehicleNo = string.Empty;
    private string _itemNo = string.Empty;
    private BitmapSource? _cameraFrame;
    private string? _capturedImagePath;
    private string _selectedCamera = string.Empty;
    private string _cameraStatus = "Idle";
    private bool _isBusy;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> ComPorts { get; } = new();
    public ObservableCollection<string> Cameras { get; } = new();

    public ICommand ConnectCommand { get; }
    public ICommand RefreshCamerasCommand { get; }
    public ICommand StartCameraCommand { get; }
    public ICommand CaptureCommand { get; }
    public ICommand SubmitCommand { get; }
    public ICommand ResetCommand { get; }

    public MainViewModel()
    {
        var connectionString = "Server=.;Database=WeighbridgeDB;Trusted_Connection=True;TrustServerCertificate=True;";

        _serialService = new SerialService();
        _cameraService = new CameraService();
        _databaseService = new DatabaseService(connectionString);

        _serialService.WeightReceived += OnWeightReceived;
        _serialService.StatusChanged += status => Application.Current.Dispatcher.InvokeAsync(() => ConnectionStatus = status);
        _serialService.ErrorOccurred += ShowError;

        _cameraService.FrameReady += frame =>
            Application.Current.Dispatcher.InvokeAsync(() => CameraFrame = frame);
        _cameraService.ErrorOccurred += ShowError;

        ConnectCommand = new RelayCommand(_ => ConnectToSerial(), _ => !string.IsNullOrWhiteSpace(SelectedComPort));
        RefreshCamerasCommand = new RelayCommand(async _ => await RefreshCamerasAsync(), _ => !IsBusy);
        StartCameraCommand = new RelayCommand(async _ => await StartCameraAsync(), _ => !IsBusy && !string.IsNullOrWhiteSpace(SelectedCamera));
        CaptureCommand = new RelayCommand(async _ => await CaptureImageAsync(), _ => !IsBusy && !string.IsNullOrWhiteSpace(VehicleNo));
        SubmitCommand = new RelayCommand(async _ => await SubmitAsync(), _ => !IsBusy && CanSubmit());
        ResetCommand = new RelayCommand(_ => ResetForm(), _ => !IsBusy);

        LoadComPorts();
        _ = RefreshCamerasAsync();
    }

    public string SelectedComPort
    {
        get => _selectedComPort;
        set
        {
            if (SetProperty(ref _selectedComPort, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public string SelectedCamera
    {
        get => _selectedCamera;
        set
        {
            if (SetProperty(ref _selectedCamera, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public string ConnectionStatus
    {
        get => _connectionStatus;
        set => SetProperty(ref _connectionStatus, value);
    }

    public string CameraStatus
    {
        get => _cameraStatus;
        set => SetProperty(ref _cameraStatus, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public double CurrentWeight
    {
        get => _currentWeight;
        set => SetProperty(ref _currentWeight, value);
    }

    public string WeightDisplay => $"{CurrentWeight:0} kg";

    public bool IsWeightStable
    {
        get => _isWeightStable;
        set
        {
            if (SetProperty(ref _isWeightStable, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public string ApplicationNo
    {
        get => _applicationNo;
        set
        {
            if (SetProperty(ref _applicationNo, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public string VehicleNo
    {
        get => _vehicleNo;
        set
        {
            if (SetProperty(ref _vehicleNo, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public string ItemNo
    {
        get => _itemNo;
        set
        {
            if (SetProperty(ref _itemNo, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public BitmapSource? CameraFrame
    {
        get => _cameraFrame;
        set => SetProperty(ref _cameraFrame, value);
    }

    public void Cleanup()
    {
        _serialService.Disconnect();
        _cameraService.StopAsync().GetAwaiter().GetResult();
    }

    private void LoadComPorts()
    {
        ComPorts.Clear();
        foreach (var port in _serialService.GetAvailablePorts())
        {
            ComPorts.Add(port);
        }

        if (ComPorts.Count > 0)
        {
            SelectedComPort = ComPorts[0];
        }
    }

    private async Task RefreshCamerasAsync()
    {
        try
        {
            IsBusy = true;
            Cameras.Clear();
            var cameraNames = await _cameraService.GetCameraNamesAsync();
            foreach (var camera in cameraNames)
            {
                Cameras.Add(camera);
            }

            if (Cameras.Count == 0)
            {
                SelectedCamera = string.Empty;
                CameraStatus = "No camera detected";
                ShowError("No USB camera detected. Check the device connection and try again.");
                return;
            }

            SelectedCamera = Cameras[0];
            CameraStatus = "Camera detected";
            await StartCameraAsync();
        }
        catch (Exception ex)
        {
            CameraStatus = "Error";
            ShowError($"Failed to refresh cameras: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ConnectToSerial() => _serialService.Connect(SelectedComPort);

    private async Task StartCameraAsync()
    {
        var index = Cameras.IndexOf(SelectedCamera);
        if (index < 0)
        {
            CameraStatus = "No camera selected";
            return;
        }

        try
        {
            IsBusy = true;
            await _cameraService.StartAsync(index);
            CameraStatus = $"Running ({SelectedCamera})";
        }
        catch (Exception ex)
        {
            CameraStatus = "Error";
            ShowError($"Camera start failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnWeightReceived(double weight)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            CurrentWeight = weight;
            OnPropertyChanged(nameof(WeightDisplay));
            UpdateWeightStability(weight);
        });
    }

    private void UpdateWeightStability(double weight)
    {
        var now = DateTime.UtcNow;
        _recentReadings.Enqueue((weight, now));
        while (_recentReadings.Count > StabilityReadingsCount)
        {
            _recentReadings.Dequeue();
        }

        if (_recentReadings.Count < StabilityReadingsCount)
        {
            IsWeightStable = false;
            return;
        }

        var values = _recentReadings.Select(r => r.Weight).ToArray();
        var timeSpan = _recentReadings.Last().Time - _recentReadings.First().Time;
        var varianceOk = values.Max() - values.Min() <= StabilityTolerance;
        var durationOk = timeSpan.TotalSeconds >= 2 && timeSpan.TotalSeconds <= 3.5;
        IsWeightStable = varianceOk && durationOk;
    }

    private async Task CaptureImageAsync()
    {
        try
        {
            IsBusy = true;
            var imageFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images");
            _capturedImagePath = await _cameraService.CaptureFrameAsync(imageFolder, VehicleNo.Trim());
            MessageBox.Show("Image captured successfully.", "Capture", MessageBoxButton.OK, MessageBoxImage.Information);
            RaiseCommandStates();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSubmit() =>
        !string.IsNullOrWhiteSpace(ApplicationNo)
        && !string.IsNullOrWhiteSpace(VehicleNo)
        && !string.IsNullOrWhiteSpace(ItemNo)
        && IsWeightStable
        && !string.IsNullOrWhiteSpace(_capturedImagePath);

    private async Task SubmitAsync()
    {
        if (!CanSubmit())
        {
            ValidateAndShowErrors();
            return;
        }

        try
        {
            IsBusy = true;
            var record = new WeightRecord
            {
                ApplicationNo = ApplicationNo.Trim(),
                VehicleNo = VehicleNo.Trim(),
                ItemNo = ItemNo.Trim(),
                Weight = CurrentWeight,
                ImagePath = _capturedImagePath!
            };

            await _databaseService.InsertWeightRecordAsync(record);
            MessageBox.Show("Record saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            ResetForm();
        }
        catch (Exception ex)
        {
            ShowError($"Database error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ValidateAndShowErrors()
    {
        if (string.IsNullOrWhiteSpace(ApplicationNo) || string.IsNullOrWhiteSpace(VehicleNo) || string.IsNullOrWhiteSpace(ItemNo))
        {
            ShowError("Application No, Vehicle No, and Item No are required.");
            return;
        }

        if (!IsWeightStable)
        {
            ShowError("Weight is not stable. Wait 2-3 seconds for stable reading.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_capturedImagePath))
        {
            ShowError("Capture vehicle image before submit.");
        }
    }

    private void ResetForm()
    {
        ApplicationNo = string.Empty;
        VehicleNo = string.Empty;
        ItemNo = string.Empty;
        _capturedImagePath = null;
        RaiseCommandStates();
    }

    private void ShowError(string message)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        });
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = "") =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void RaiseCommandStates()
    {
        ((RelayCommand)ConnectCommand).RaiseCanExecuteChanged();
        ((RelayCommand)RefreshCamerasCommand).RaiseCanExecuteChanged();
        ((RelayCommand)StartCameraCommand).RaiseCanExecuteChanged();
        ((RelayCommand)CaptureCommand).RaiseCanExecuteChanged();
        ((RelayCommand)SubmitCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ResetCommand).RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        Cleanup();
        _serialService.Dispose();
        _cameraService.Dispose();
    }
}
