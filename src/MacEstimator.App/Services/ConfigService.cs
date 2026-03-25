using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MacEstimator.App.Models;

namespace MacEstimator.App.Services;

public class ConfigService
{
    private static readonly string ConfigPath = Path.Combine(JobIndexService.SharedFolder, "config.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private AppConfig? _cached;

    /// <summary>
    /// Load config from shared drive. Falls back to built-in defaults if unavailable.
    /// </summary>
    public async Task<AppConfig> LoadAsync()
    {
        if (_cached is not null)
            return _cached;

        try
        {
            if (File.Exists(ConfigPath))
            {
                await using var stream = File.OpenRead(ConfigPath);
                _cached = await JsonSerializer.DeserializeAsync<AppConfig>(stream, Options);
            }
        }
        catch
        {
            // Fall back to defaults
        }

        _cached ??= BuildDefaultConfig();
        return _cached;
    }

    /// <summary>
    /// Save config to shared drive.
    /// </summary>
    public async Task SaveAsync(AppConfig config)
    {
        Directory.CreateDirectory(JobIndexService.SharedFolder);
        var tempPath = ConfigPath + ".tmp";
        try
        {
            await using var stream = File.Open(tempPath, FileMode.Create);
            await JsonSerializer.SerializeAsync(stream, config, Options);
            await stream.FlushAsync();
            stream.Close();
            File.Move(tempPath, ConfigPath, overwrite: true);
            _cached = config;
        }
        finally
        {
            if (File.Exists(tempPath))
                try { File.Delete(tempPath); } catch { }
        }
    }

    /// <summary>
    /// Force reload from disk on next access.
    /// </summary>
    public void InvalidateCache() => _cached = null;

    /// <summary>
    /// Convert config line items to templates used by the app.
    /// </summary>
    public static LineItemTemplate[] ToTemplates(AppConfig config)
    {
        return config.DefaultLineItems.Select(li => new LineItemTemplate(
            li.Name,
            li.DefaultRate,
            Enum.TryParse<UnitType>(li.Unit, out var u) ? u : UnitType.LinearFoot,
            Enum.TryParse<PricingMode>(li.Mode, out var m) ? m : PricingMode.PerUnit,
            li.NameOptions?.ToArray()
        )).ToArray();
    }

    private static AppConfig BuildDefaultConfig()
    {
        var config = new AppConfig();
        foreach (var t in DefaultLineItems.All)
        {
            config.DefaultLineItems.Add(new LineItemConfig
            {
                Name = t.Name,
                DefaultRate = t.DefaultRate,
                Unit = t.Unit.ToString(),
                Mode = t.Mode.ToString(),
                NameOptions = t.NameOptions?.ToList()
            });
        }
        return config;
    }
}
