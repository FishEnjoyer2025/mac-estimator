using System.Text.Json.Serialization;

namespace MacEstimator.App.Models;

public enum BidStatus
{
    Draft,
    Submitted,
    FollowedUp,
    Won,
    Lost,
    Declined
}

public class JobIndexEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string JobName { get; set; } = string.Empty;
    public string JobNumber { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string ClientCompany { get; set; } = string.Empty;
    public string SubmittedBy { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime ModifiedAt { get; set; } = DateTime.Now;
    public string FilePath { get; set; } = string.Empty;

    // War Room fields
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public BidStatus Status { get; set; } = BidStatus.Draft;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? FollowedUpAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string Notes { get; set; } = string.Empty;

    // Lost reason tracking
    public string? LostTo { get; set; }
    public decimal? CompetitorPrice { get; set; }
    public string? LostReason { get; set; }
}
