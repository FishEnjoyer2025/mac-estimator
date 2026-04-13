namespace MacEstimator.App.Models;

public class AppConfig
{
    public List<LineItemConfig> DefaultLineItems { get; set; } = [];
    public List<string> SubmittedByOptions { get; set; } = ["Rusty Philbrick", "Josh Irsik"];
    public string DefaultSubmittedBy { get; set; } = "Rusty Philbrick";
    public string DefaultGrade { get; set; } = "PLAM";
    public string DefaultExclusions { get; set; } = Estimate.DefaultExclusions;
}

public class LineItemConfig
{
    public string Name { get; set; } = string.Empty;
    public decimal DefaultRate { get; set; }
    public string Unit { get; set; } = "LinearFoot";
    public string Mode { get; set; } = "PerUnit";
    public List<string>? NameOptions { get; set; }
    public decimal? PaintGradeRate { get; set; }
    public decimal? StainGradeRate { get; set; }
}
