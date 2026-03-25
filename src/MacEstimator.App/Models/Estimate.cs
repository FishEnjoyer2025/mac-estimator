namespace MacEstimator.App.Models;

public class Estimate
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime CreatedAt { get; init; } = DateTime.Now;
    public DateTime ModifiedAt { get; set; } = DateTime.Now;

    // Job info
    public string JobName { get; set; } = string.Empty;
    public string JobNumber { get; set; } = string.Empty;

    // Client info
    public string ClientName { get; set; } = string.Empty;
    public string ClientCompany { get; set; } = string.Empty;
    public string ClientEmail { get; set; } = string.Empty;
    public string ClientPhone { get; set; } = string.Empty;

    // Content
    public List<Room> Rooms { get; set; } = [];

    // Adjustments
    public decimal AdjustmentPercent { get; set; }
    public string AdjustmentLabel { get; set; } = string.Empty;

    // Footer
    public string Exclusions { get; set; } = DefaultExclusions;
    public string Notes { get; set; } = string.Empty;
    public string SubmittedBy { get; set; } = "Rusty Philbrick";

    public const string DefaultExclusions =
        "Excludes any other elevations other than the ones listed above.";
}
