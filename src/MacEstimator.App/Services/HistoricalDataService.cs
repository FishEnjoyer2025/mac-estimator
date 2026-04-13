using System.IO;
using System.Text.Json;
using MacEstimator.App.Models;

namespace MacEstimator.App.Services;

public class HistoricalDataService
{
    private static readonly string DataPath = Path.Combine(JobIndexService.SharedFolder, "historical_data.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private HistoricalData? _cached;

    public async Task<HistoricalData?> LoadAsync()
    {
        if (_cached is not null)
            return _cached;

        try
        {
            if (!File.Exists(DataPath))
                return null;

            await using var stream = File.OpenRead(DataPath);
            _cached = await JsonSerializer.DeserializeAsync<HistoricalData>(stream, Options);
            return _cached;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Clear cache so next LoadAsync re-reads from disk.</summary>
    public void ClearCache() => _cached = null;

    /// <summary>
    /// Get pricing stats for a line item name. Tries exact match first,
    /// then strips grade prefix (PLAM/Paint Grade/Stain Grade) for a base match.
    /// </summary>
    public PricingStats? GetPricing(string itemName)
    {
        if (_cached is null) return null;

        // Exact match
        if (_cached.Pricing.TryGetValue(itemName, out var exact))
            return exact;

        // Strip grade prefix
        var baseName = StripGradePrefix(itemName);
        if (baseName != itemName && _cached.Pricing.TryGetValue(baseName, out var baseMatch))
            return baseMatch;

        // Try common variations
        foreach (var suffix in new[] { "s", "" })
        {
            var tryName = baseName.EndsWith('s') ? baseName[..^1] : baseName + "s";
            if (_cached.Pricing.TryGetValue(tryName, out var fuzzy))
                return fuzzy;
        }

        return null;
    }

    /// <summary>
    /// Get contact info for a contractor/company name. Case-insensitive partial match.
    /// </summary>
    public ContactEntry? GetContact(string company)
    {
        if (_cached is null || string.IsNullOrWhiteSpace(company)) return null;

        var lower = company.Trim().ToLowerInvariant();

        // Exact match first
        foreach (var (key, contact) in _cached.Contacts)
        {
            if (key.Equals(company.Trim(), StringComparison.OrdinalIgnoreCase))
                return contact;
        }

        // Partial match
        foreach (var (key, contact) in _cached.Contacts)
        {
            if (key.ToLowerInvariant().Contains(lower) || lower.Contains(key.ToLowerInvariant()))
                return contact;
        }

        return null;
    }

    /// <summary>
    /// Get all known contractor names for auto-complete.
    /// </summary>
    public IReadOnlyList<string> GetContractorNames()
    {
        if (_cached is null) return [];
        return _cached.Contacts.Keys.OrderBy(k => k).ToList();
    }

    /// <summary>
    /// Predict win probability using multi-factor model: amount + year + estimator.
    /// </summary>
    public decimal GetWinProbability(decimal bidAmount, string? estimator = null)
    {
        if (_cached?.WinProbability is null) return 0.42m;

        var model = _cached.WinProbability;

        // Start with amount-based probability
        decimal prob = model.BaseRate;
        foreach (var bucket in model.ByAmount)
        {
            if (bidAmount >= bucket.MinAmount && bidAmount < bucket.MaxAmount && bucket.SampleSize >= 5)
            {
                prob = bucket.WinRate;
                break;
            }
        }

        // Apply year adjustment (current year)
        var yearKey = DateTime.Now.Year.ToString();
        if (model.YearAdjustments.TryGetValue(yearKey, out var yearAdj))
            prob *= yearAdj;

        // Apply estimator adjustment
        if (!string.IsNullOrEmpty(estimator))
        {
            // Extract first name for matching (e.g. "Rusty Philbrick" -> "Rusty")
            var firstName = estimator.Split(' ')[0];
            if (model.EstimatorAdjustments.TryGetValue(firstName, out var estAdj))
                prob *= estAdj;
        }

        return Math.Clamp(prob, 0.02m, 0.95m);
    }

    /// <summary>
    /// Get room template data for a room name. Fuzzy matches by normalizing
    /// room names: strips leading numbers, normalizes common variations.
    /// "Break Room" matches "Breakroom", "112 Break Room", etc.
    /// </summary>
    public RoomTemplateData? GetRoomTemplate(string roomName)
    {
        if (_cached?.RoomTemplates is null || _cached.RoomTemplates.Count == 0
            || string.IsNullOrWhiteSpace(roomName))
            return null;

        var normalized = NormalizeRoomName(roomName);

        // Exact match on normalized name
        foreach (var (key, data) in _cached.RoomTemplates)
        {
            if (key.Equals(normalized, StringComparison.OrdinalIgnoreCase))
                return data;
        }

        // Partial/contains match: room name contains a template key or vice versa
        foreach (var (key, data) in _cached.RoomTemplates)
        {
            if (normalized.Contains(key, StringComparison.OrdinalIgnoreCase)
                || key.Contains(normalized, StringComparison.OrdinalIgnoreCase))
                return data;
        }

        return null;
    }

    /// <summary>
    /// Get all known room template names for display.
    /// </summary>
    public IReadOnlyList<string> GetRoomTemplateNames()
    {
        if (_cached?.RoomTemplates is null) return [];
        return _cached.RoomTemplates.Keys.OrderBy(k => k).ToList();
    }

    private static string NormalizeRoomName(string name)
    {
        var n = name.Trim();

        // Strip leading room/suite numbers: "112 ", "A-101 ", "Suite 200 "
        n = System.Text.RegularExpressions.Regex.Replace(n, @"^(?:Suite\s+|Room\s+|Rm\s+)?[A-Z]?\d{1,4}[A-Z]?\s+[-–]\s*", "");
        n = System.Text.RegularExpressions.Regex.Replace(n, @"^(?:Suite\s+|Room\s+|Rm\s+)?[A-Z]?\d{1,4}[A-Z]?\s+", "");

        // Strip multiplier text: "(QTY: 6)", "(x4)"
        n = System.Text.RegularExpressions.Regex.Replace(n, @"\s*\(QTY:\s*\d+\)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        n = System.Text.RegularExpressions.Regex.Replace(n, @"\s*\(x\d+\)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Normalize common room name variations
        var lower = n.Trim().ToLowerInvariant();
        n = lower switch
        {
            "breakroom" or "break room" => "Break Room",
            "restrooms" or "rest room" or "mens restroom" or "womens restroom"
                or "men's restroom" or "women's restroom" => "Restroom",
            "kitchenette" => "Kitchen",
            _ => n.Trim()
        };

        return n;
    }

    private static string StripGradePrefix(string name)
    {
        foreach (var prefix in new[] { "PLAM ", "Paint Grade ", "Stain Grade " })
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return name[prefix.Length..];
        }
        return name;
    }
}
