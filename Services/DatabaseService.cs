using Microsoft.Data.SqlClient;
using WaybridgeApp.Models;
using System.IO;
namespace WaybridgeApp.Services;

public sealed class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(string connectionString)
    {
        _connectionString = connectionString;
    }

    // Database insert uses parameterized SQL via ADO.NET to avoid SQL injection.
    public async Task InsertWeightRecordAsync(WeightRecord record, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO WeightRecords (ApplicationNo, VehicleNo, ItemNo, Weight, ImagePath)
VALUES (@ApplicationNo, @VehicleNo, @ItemNo, @Weight, @ImagePath);";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ApplicationNo", record.ApplicationNo);
        command.Parameters.AddWithValue("@VehicleNo", record.VehicleNo);
        command.Parameters.AddWithValue("@ItemNo", record.ItemNo);
        command.Parameters.AddWithValue("@Weight", record.Weight);
        command.Parameters.AddWithValue("@ImagePath", record.ImagePath);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
