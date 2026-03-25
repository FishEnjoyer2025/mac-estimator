using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MacEstimator.App.Models;

namespace MacEstimator.App.Services;

public class EstimateFileService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task SaveAsync(Estimate estimate, string filePath)
    {
        estimate.ModifiedAt = DateTime.Now;
        var tempPath = filePath + ".tmp";
        try
        {
            await using var stream = File.Open(tempPath, FileMode.Create);
            await JsonSerializer.SerializeAsync(stream, estimate, Options);
            await stream.FlushAsync();
            stream.Close();
            File.Move(tempPath, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
                try { File.Delete(tempPath); } catch { }
        }
    }

    public async Task<Estimate> LoadAsync(string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<Estimate>(stream, Options)
            ?? throw new InvalidOperationException("Failed to deserialize estimate file.");
    }
}
