using System.IO.Ports;
using System.Text.RegularExpressions;

namespace WaybridgeApp.Services;

public sealed class SerialService : IDisposable
{
    private static readonly Regex WeightRegex = new(@"([-+]?\d+(?:\.\d+)?)\s*kg", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private SerialPort? _serialPort;

    public event Action<double>? WeightReceived;
    public event Action<string>? StatusChanged;
    public event Action<string>? ErrorOccurred;

    public IReadOnlyList<string> GetAvailablePorts() => SerialPort.GetPortNames().OrderBy(p => p).ToList();

    public void Connect(string portName)
    {
        try
        {
            Disconnect();

            _serialPort = new SerialPort(portName, 9600)
            {
                NewLine = "\n",
                ReadTimeout = 1000
            };

            // Serial reading is event-driven and continuous while port is open.
            _serialPort.DataReceived += OnDataReceived;
            _serialPort.Open();
            StatusChanged?.Invoke("Connected");
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Serial connection failed: {ex.Message}");
            StatusChanged?.Invoke("Disconnected");
        }
    }

    public void Disconnect()
    {
        if (_serialPort is null)
        {
            return;
        }

        try
        {
            _serialPort.DataReceived -= OnDataReceived;
            if (_serialPort.IsOpen)
            {
                _serialPort.Close();
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Serial disconnect error: {ex.Message}");
        }
        finally
        {
            _serialPort.Dispose();
            _serialPort = null;
            StatusChanged?.Invoke("Disconnected");
        }
    }

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        if (_serialPort is null || !_serialPort.IsOpen)
        {
            return;
        }

        try
        {
            var raw = _serialPort.ReadLine();
            var parsedWeight = ParseWeight(raw);
            if (parsedWeight.HasValue)
            {
                StatusChanged?.Invoke("Reading");
                WeightReceived?.Invoke(parsedWeight.Value);
            }
        }
        catch (TimeoutException)
        {
            // Read timeout can happen in continuous streams; ignore.
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Serial read error: {ex.Message}");
            StatusChanged?.Invoke("Connected");
        }
    }

    private static double? ParseWeight(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var match = WeightRegex.Match(input);
        if (match.Success && double.TryParse(match.Groups[1].Value, out var result))
        {
            return result;
        }

        var digitsOnly = new string(input.Where(c => char.IsDigit(c) || c == '.').ToArray());
        return double.TryParse(digitsOnly, out result) ? result : null;
    }

    public void Dispose() => Disconnect();
}
