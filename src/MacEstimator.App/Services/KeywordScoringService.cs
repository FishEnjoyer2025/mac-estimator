using MacEstimator.App.Models;

namespace MacEstimator.App.Services;

public class KeywordScoringService
{
    public PdfAnalysisResult Analyze(Dictionary<int, string> pageTexts, KeywordConfig config)
    {
        var result = new PdfAnalysisResult
        {
            TotalPages = pageTexts.Count,
            ExtractedText = string.Join("\n\n--- Page Break ---\n\n",
                pageTexts.OrderBy(p => p.Key).Select(p => $"[Page {p.Key}]\n{p.Value}"))
        };

        // Search good keywords
        foreach (var entry in config.GoodKeywords)
        {
            var found = FindKeyword(entry.Keyword, pageTexts);
            if (found is not null)
                result.FoundGood.Add(found);
            else
                result.MissingGood.Add(entry.Keyword);
        }

        // Search bad keywords
        foreach (var entry in config.BadKeywords)
        {
            var found = FindKeyword(entry.Keyword, pageTexts);
            if (found is not null)
                result.FoundBad.Add(found);
            else
                result.MissingBad.Add(entry.Keyword);
        }

        // Calculate fitness score
        result.FitnessScore = CalculateScore(result, config);

        return result;
    }

    private static FoundKeyword? FindKeyword(string keyword, Dictionary<int, string> pageTexts)
    {
        var pages = new List<int>();
        int totalCount = 0;

        foreach (var (pageNum, text) in pageTexts)
        {
            // Try exact match first, then normalized (collapsed whitespace) match
            int count = CountOccurrences(text, keyword);
            if (count == 0)
                count = CountOccurrences(NormalizeText(text), NormalizeText(keyword));

            if (count > 0)
            {
                pages.Add(pageNum);
                totalCount += count;
            }
        }

        if (pages.Count == 0)
            return null;

        return new FoundKeyword
        {
            Keyword = keyword,
            Pages = pages,
            OccurrenceCount = totalCount
        };
    }

    private static int CountOccurrences(string text, string keyword)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(keyword, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += keyword.Length;
        }
        return count;
    }

    /// <summary>
    /// Collapses all whitespace runs into single spaces and trims.
    /// Handles architectural PDFs where text gets extracted with extra spaces
    /// (e.g. "M I L L W O R K" -> "MILLWORK", "BASE  CABINETS" -> "BASE CABINETS").
    /// </summary>
    private static string NormalizeText(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        bool lastWasSpace = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!lastWasSpace)
                    sb.Append(' ');
                lastWasSpace = true;
            }
            else
            {
                sb.Append(c);
                lastWasSpace = false;
            }
        }
        return sb.ToString().Trim();
    }

    private static int CalculateScore(PdfAnalysisResult result, KeywordConfig config)
    {
        double score = 50.0;

        score += result.FoundGood.Count * 20.0;
        score -= result.FoundBad.Count * 20.0;

        return (int)Math.Clamp(Math.Round(score), 0, 100);
    }
}
