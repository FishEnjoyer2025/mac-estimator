namespace MacEstimator.App.Models;

public class PdfAnalysisResult
{
    public int FitnessScore { get; set; }
    public List<FoundKeyword> FoundGood { get; set; } = [];
    public List<FoundKeyword> FoundBad { get; set; } = [];
    public List<string> MissingGood { get; set; } = [];
    public List<string> MissingBad { get; set; } = [];
    public int TotalPages { get; set; }
    public string ExtractedText { get; set; } = string.Empty;
}

public class FoundKeyword
{
    public string Keyword { get; set; } = string.Empty;
    public List<int> Pages { get; set; } = [];
    public int OccurrenceCount { get; set; }
    public string PagesDisplay => string.Join(", ", Pages);
}
