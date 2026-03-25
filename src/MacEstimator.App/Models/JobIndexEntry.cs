namespace MacEstimator.App.Models;

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
}
