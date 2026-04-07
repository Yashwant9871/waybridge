namespace WaybridgeApp.Models;

public sealed class WeightRecord
{
    public int Id { get; set; }
    public string ApplicationNo { get; set; } = string.Empty;
    public string VehicleNo { get; set; } = string.Empty;
    public string ItemNo { get; set; } = string.Empty;
    public double Weight { get; set; }
    public string ImagePath { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
