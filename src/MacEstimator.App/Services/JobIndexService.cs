using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MacEstimator.App.Models;

namespace MacEstimator.App.Services;

public class JobIndexService
{
    public const string SharedFolder = @"G:\My Drive\MAC\Estimator";
    private static readonly string IndexPath = Path.Combine(SharedFolder, "jobs.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Register or update a job in the shared index when saving an estimate.
    /// </summary>
    public async Task RegisterJobAsync(Estimate estimate, string filePath, decimal total)
    {
        try
        {
            var entries = await LoadEntriesAsync();

            // Find existing entry by file path or estimate ID
            var existing = entries.FirstOrDefault(e => e.FilePath == filePath)
                        ?? entries.FirstOrDefault(e => e.Id == estimate.Id);

            if (existing is not null)
            {
                existing.JobName = estimate.JobName;
                existing.JobNumber = estimate.JobNumber;
                existing.ClientName = estimate.ClientName;
                existing.ClientCompany = estimate.ClientCompany;
                existing.SubmittedBy = estimate.SubmittedBy;
                existing.Total = total;
                existing.ModifiedAt = DateTime.Now;
                existing.FilePath = filePath;
            }
            else
            {
                entries.Add(new JobIndexEntry
                {
                    Id = estimate.Id,
                    JobName = estimate.JobName,
                    JobNumber = estimate.JobNumber,
                    ClientName = estimate.ClientName,
                    ClientCompany = estimate.ClientCompany,
                    SubmittedBy = estimate.SubmittedBy,
                    Total = total,
                    CreatedAt = estimate.CreatedAt,
                    ModifiedAt = DateTime.Now,
                    FilePath = filePath
                });
            }

            await SaveEntriesAsync(entries);
        }
        catch
        {
            // Silently fail — don't block saving if shared drive is unavailable
        }
    }

    /// <summary>
    /// Load all job entries from the shared index.
    /// </summary>
    public async Task<List<JobIndexEntry>> LoadEntriesAsync()
    {
        try
        {
            if (!File.Exists(IndexPath))
                return [];

            await using var stream = File.OpenRead(IndexPath);
            return await JsonSerializer.DeserializeAsync<List<JobIndexEntry>>(stream, Options) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private async Task SaveEntriesAsync(List<JobIndexEntry> entries)
    {
        Directory.CreateDirectory(SharedFolder);
        var tempPath = IndexPath + ".tmp";
        try
        {
            await using var stream = File.Open(tempPath, FileMode.Create);
            await JsonSerializer.SerializeAsync(stream, entries, Options);
            await stream.FlushAsync();
            stream.Close();
            File.Move(tempPath, IndexPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
                try { File.Delete(tempPath); } catch { }
        }
    }
}
