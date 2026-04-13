using System.Text.Json.Serialization;

namespace MacEstimator.App.Models;

public class HistoricalData
{
    [JsonPropertyName("generated")]
    public string Generated { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public HistoricalSummary Summary { get; set; } = new();

    [JsonPropertyName("pricing")]
    public Dictionary<string, PricingStats> Pricing { get; set; } = [];

    [JsonPropertyName("win_rates")]
    public WinRateData WinRates { get; set; } = new();

    [JsonPropertyName("contractors")]
    public List<ContractorEntry> Contractors { get; set; } = [];

    [JsonPropertyName("profitability")]
    public ProfitabilityData Profitability { get; set; } = new();

    [JsonPropertyName("seasonal")]
    public Dictionary<string, SeasonalEntry> Seasonal { get; set; } = [];

    [JsonPropertyName("contacts")]
    public Dictionary<string, ContactEntry> Contacts { get; set; } = [];

    [JsonPropertyName("win_probability")]
    public WinProbabilityData? WinProbability { get; set; }

    [JsonPropertyName("room_templates")]
    public Dictionary<string, RoomTemplateData> RoomTemplates { get; set; } = [];
}

public class WinProbabilityData
{
    [JsonPropertyName("base_rate")]
    public decimal BaseRate { get; set; } = 0.47m;

    [JsonPropertyName("by_amount")]
    public List<WinProbBucket> ByAmount { get; set; } = [];

    [JsonPropertyName("year_adjustments")]
    public Dictionary<string, decimal> YearAdjustments { get; set; } = [];

    [JsonPropertyName("estimator_adjustments")]
    public Dictionary<string, decimal> EstimatorAdjustments { get; set; } = [];

    [JsonPropertyName("type_adjustments")]
    public Dictionary<string, decimal> TypeAdjustments { get; set; } = [];
}

public class WinProbBucket
{
    [JsonPropertyName("min_amount")]
    public decimal MinAmount { get; set; }

    [JsonPropertyName("max_amount")]
    public decimal MaxAmount { get; set; }

    [JsonPropertyName("win_rate")]
    public decimal WinRate { get; set; }

    [JsonPropertyName("sample_size")]
    public int SampleSize { get; set; }
}

public class HistoricalSummary
{
    [JsonPropertyName("total_bids_tracked")]
    public int TotalBidsTracked { get; set; }

    [JsonPropertyName("total_jobs_costed")]
    public int TotalJobsCosted { get; set; }

    [JsonPropertyName("total_detailed_bids")]
    public int TotalDetailedBids { get; set; }

    [JsonPropertyName("overall_win_rate")]
    public decimal OverallWinRate { get; set; }

    [JsonPropertyName("avg_profit_margin")]
    public decimal? AvgProfitMargin { get; set; }
}

public class PricingStats
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("avg_unit_price")]
    public decimal AvgUnitPrice { get; set; }

    [JsonPropertyName("min_unit_price")]
    public decimal MinUnitPrice { get; set; }

    [JsonPropertyName("max_unit_price")]
    public decimal MaxUnitPrice { get; set; }

    [JsonPropertyName("median_unit_price")]
    public decimal MedianUnitPrice { get; set; }

    [JsonPropertyName("total_revenue")]
    public decimal TotalRevenue { get; set; }
}

public class WinRateData
{
    [JsonPropertyName("overall")]
    public decimal Overall { get; set; }

    [JsonPropertyName("by_year")]
    public Dictionary<string, YearWinRate> ByYear { get; set; } = [];
}

public class YearWinRate
{
    [JsonPropertyName("won")]
    public int Won { get; set; }

    [JsonPropertyName("lost")]
    public int Lost { get; set; }

    [JsonPropertyName("rate")]
    public decimal Rate { get; set; }

    [JsonPropertyName("revenue")]
    public decimal Revenue { get; set; }
}

public class ContractorEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("jobs")]
    public int Jobs { get; set; }

    [JsonPropertyName("revenue")]
    public decimal Revenue { get; set; }

    [JsonPropertyName("recent_jobs")]
    public List<string> RecentJobs { get; set; } = [];
}

public class ProfitabilityData
{
    [JsonPropertyName("avg_margin")]
    public decimal? AvgMargin { get; set; }

    [JsonPropertyName("by_estimator")]
    public Dictionary<string, EstimatorMargin> ByEstimator { get; set; } = [];

    [JsonPropertyName("bid_estimators")]
    public Dictionary<string, BidEstimatorStats> BidEstimators { get; set; } = [];

    [JsonPropertyName("top_jobs")]
    public List<JobMarginEntry> TopJobs { get; set; } = [];

    [JsonPropertyName("worst_jobs")]
    public List<JobMarginEntry> WorstJobs { get; set; } = [];
}

public class EstimatorMargin
{
    [JsonPropertyName("avg")]
    public decimal Avg { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }
}

public class BidEstimatorStats
{
    [JsonPropertyName("bids")]
    public int Bids { get; set; }

    [JsonPropertyName("won")]
    public int Won { get; set; }

    [JsonPropertyName("revenue_won")]
    public decimal RevenueWon { get; set; }

    [JsonPropertyName("win_rate")]
    public decimal WinRate { get; set; }
}

public class JobMarginEntry
{
    [JsonPropertyName("job")]
    public string Job { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("margin")]
    public decimal? Margin { get; set; }

    [JsonPropertyName("revenue")]
    public decimal? Revenue { get; set; }

    [JsonPropertyName("profit")]
    public decimal? Profit { get; set; }
}

public class SeasonalEntry
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("total_value")]
    public decimal TotalValue { get; set; }
}

public class ContactEntry
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }
}

public class RoomTemplateData
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("items")]
    public Dictionary<string, RoomTemplateItemData> Items { get; set; } = [];
}

public class RoomTemplateItemData
{
    [JsonPropertyName("avg_qty")]
    public decimal AvgQty { get; set; }

    [JsonPropertyName("avg_price")]
    public decimal AvgPrice { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }
}
