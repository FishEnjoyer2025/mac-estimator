namespace MacEstimator.App.Models;

public class KeywordConfig
{
    public List<KeywordEntry> GoodKeywords { get; set; } = [];
    public List<KeywordEntry> BadKeywords { get; set; } = [];
}

public class KeywordEntry
{
    public string Keyword { get; set; } = string.Empty;
    public DateTime DateAdded { get; set; } = DateTime.Now;
}
