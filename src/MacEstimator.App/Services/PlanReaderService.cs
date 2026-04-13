using System.Text.RegularExpressions;
using MacEstimator.App.Models;

namespace MacEstimator.App.Services;

public class PlanReaderService
{
    // Room name patterns commonly found in commercial architectural plans
    private static readonly string[] RoomPatterns =
    [
        "STANDARD SINGLE ROOM", "STANDARD FAMILY ROOM", "STANDARD ACCESSIBLE ROOM",
        "STANDARD DOUBLE ROOM", "STANDARD SUITE",
        "BREAK ROOM", "BREAKROOM",
        "MAIL ROOM", "MAILROOM",
        "NURSE STATION", "NURSES STATION", "NURSING STATION",
        "RECEPTION", "RECEPTION AREA",
        "EXAM ROOM", "EXAM",
        "COPY ROOM", "COPY AREA",
        "KITCHEN", "KITCHENETTE",
        "RESTROOM", "BATHROOM", "WASHROOM", "TOILET",
        "LOBBY",
        "CONFERENCE", "CONFERENCE ROOM",
        "OFFICE", "PRIVATE OFFICE",
        "LOUNGE", "BREAK AREA",
        "STORAGE", "STORAGE ROOM",
        "WORKROOM", "WORK ROOM",
        "CLEAN ROOM",
        "MEDICATION ROOM", "MED ROOM",
        "LAB", "LABORATORY",
        "PATIENT ROOM",
        "CONSULTATION", "CONSULT ROOM",
        "TREATMENT ROOM", "TREATMENT",
        "JANITOR", "JANITOR CLOSET",
        "STAFF LOUNGE",
        "WAITING", "WAITING ROOM", "WAITING AREA",
        "CHECK IN", "CHECK-IN",
        "LAUNDRY", "LAUNDRY ROOM",
        "PANTRY",
        "VESTIBULE",
        "CORRIDOR",
        "CLASSROOM",
        "CAFE", "CAFETERIA",
        "WINDOW ALCOVE", "STORAGE ELEV", "STORAGE DETAIL",
        "BATHROOM ELEVATION", "FLOOR PLAN",
        "FINISHES", "CASEWORK",
    ];

    // Cabinet/item keyword patterns and the template names they map to
    private static readonly (string Pattern, string TemplateName)[] ItemPatterns =
    [
        (@"upper\s*cabinet", "PLAM Upper Cabinets"),
        (@"wall\s*cabinet", "PLAM Upper Cabinets"),
        (@"base\s*cabinet", "PLAM Base Cabinets"),
        (@"lower\s*cabinet", "PLAM Base Cabinets"),
        (@"tall\s*cabinet", "PLAM Tall Cabinets"),
        (@"full[\s-]*height\s*cabinet", "PLAM Tall Cabinets"),
        (@"pantry\s*cabinet", "PLAM Tall Cabinets"),
        (@"wardrobe\s*cabinet", "PLAM Tall Cabinets"),
        (@"storage\s*cabinet", "PLAM Tall Cabinets"),
        (@"solid\s*surface\s*counter", "Solid Surface Countertops"),
        (@"solid\s*surface\s*top", "Solid Surface Countertops"),
        (@"solid\s*surface\s*countertop", "Solid Surface Countertops"),
        (@"corian", "Solid Surface Countertops"),
        (@"quartz\s*counter", "Quartz Countertops"),
        (@"quartz\s*top", "Quartz Countertops"),
        (@"ada\s*vanit", "PLAM ADA Vanity"),
        (@"ada\s*sink\s*apron", "PLAM ADA Vanity"),
        (@"vanit", "PLAM ADA Vanity"),
        (@"shelves?\s*w/?\s*brackets?", "PLAM Shelves w/ Brackets"),
        (@"shelf\s*w/?\s*brackets?", "PLAM Shelves w/ Brackets"),
        (@"floating\s*shelf", "PLAM Floating Shelf"),
        (@"float\s*shelf", "PLAM Floating Shelf"),
        (@"open\s*shelf", "PLAM Floating Shelf"),
        (@"wall\s*cap", "PLAM Wall Caps"),
        (@"end\s*panel", "PLAM End Panels"),
        (@"filler\s*panel", "PLAM End Panels"),
        (@"support\s*panel", "PLAM End Panels"),
        (@"plastic\s*laminate\s*counter", "PLAM Countertops"),
        (@"plam\s*counter", "PLAM Countertops"),
        (@"laminate\s*counter", "PLAM Countertops"),
        (@"p[\.\s]*?lam\s*top", "PLAM Countertops"),
        (@"laminate\s*bench\s*top", "PLAM Countertops"),
        (@"laminate\s*desk", "PLAM Countertops"),
        (@"countertop\s*w/?\s*support\s*panel", "PLAM Countertop w/ Support Panel"),
        (@"flip[\s-]*up\s*counter", "PLAM Flip-Up Countertop"),
        (@"piano\s*hinge\s*counter", "PLAM Flip-Up Countertop"),
        (@"countertop", "PLAM Countertops"),
        (@"bracket", "Brackets"),
        (@"support\s*bracket", "Brackets"),
        (@"stainless\s*steel\s*leg", "Stainless Steel Legs"),
        (@"ss\s*leg", "Stainless Steel Legs"),
        (@"stainless\s*steel\s*pull", "Brackets"),  // hardware
        (@"plastic\s*laminate\s*desk", "PLAM Base Cabinets"),
        (@"drawer\s*cabinet", "PLAM Base Cabinets"),
        (@"3[\s-]*drawer\s*cabinet", "PLAM Base Cabinets"),
        (@"2[\s-]*drawer\s*cabinet", "PLAM Base Cabinets"),
    ];

    // Architectural dimension patterns: 2'-4", 6'-11", 3'-0" VIF, etc.
    private static readonly Regex ArchDimensionRegex = new(
        @"(\d{1,2})'\s*-?\s*(\d{1,2})(?:""|\u201D|″)?\s*(?:VIF|V\.I\.F\.)?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Simple quantity patterns: "12 LF", "15 SF", "24 linear feet", "8 each"
    private static readonly Regex SimpleQtyRegex = new(
        @"(\d+(?:\.\d+)?)\s*(?:" +
            @"(?:LF|L\.?F\.?)" +
            @"|(?:SF|S\.?F\.?)" +
            @"|linear\s*(?:feet|foot|ft)" +
            @"|(?:sq|square)\s*(?:feet|foot|ft)" +
            @"|(?:each|ea\.?)" +
        @")",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Takes raw OCR text from a PDF, returns extracted rooms with line items.
    /// </summary>
    public List<ExtractedRoom> ExtractRooms(string pdfText)
    {
        if (string.IsNullOrWhiteSpace(pdfText))
            return [];

        var rooms = new List<ExtractedRoom>();
        var sections = SplitIntoSections(pdfText);

        foreach (var (roomName, sectionText) in sections)
        {
            var items = ExtractLineItems(sectionText);
            if (items.Count > 0)
            {
                rooms.Add(new ExtractedRoom
                {
                    Name = FormatRoomName(roomName),
                    Items = items
                });
            }
        }

        // If no rooms found by headers, try to extract from the full text as one room
        if (rooms.Count == 0)
        {
            var items = ExtractLineItems(pdfText);
            if (items.Count > 0)
            {
                rooms.Add(new ExtractedRoom
                {
                    Name = "Plan Import",
                    Items = items
                });
            }
        }

        return rooms;
    }

    /// <summary>
    /// Returns all keyword matches with their locations for PDF highlighting.
    /// </summary>
    public List<HighlightMatch> FindHighlightMatches(string pdfText)
    {
        if (string.IsNullOrWhiteSpace(pdfText))
            return [];

        var matches = new List<HighlightMatch>();
        var normalized = Regex.Replace(pdfText, @"\s+", " ");

        foreach (var (pattern, templateName) in ItemPatterns)
        {
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            foreach (Match match in regex.Matches(normalized))
            {
                matches.Add(new HighlightMatch
                {
                    MatchedText = match.Value,
                    TemplateName = templateName,
                    StartIndex = match.Index,
                    Length = match.Length
                });
            }
        }

        // Also match room names
        var sortedPatterns = RoomPatterns
            .OrderByDescending(p => p.Length)
            .Select(p => Regex.Escape(p))
            .ToArray();
        var roomRegex = new Regex(
            @"\b(" + string.Join("|", sortedPatterns) + @")\b",
            RegexOptions.IgnoreCase);
        foreach (Match match in roomRegex.Matches(normalized))
        {
            matches.Add(new HighlightMatch
            {
                MatchedText = match.Value,
                TemplateName = "ROOM",
                StartIndex = match.Index,
                Length = match.Length
            });
        }

        return matches;
    }

    private List<(string RoomName, string Text)> SplitIntoSections(string text)
    {
        var sections = new List<(string RoomName, string Text)>();

        var sortedPatterns = RoomPatterns
            .OrderByDescending(p => p.Length)
            .Select(p => Regex.Escape(p))
            .ToArray();
        var roomRegex = new Regex(
            @"(?:^|\s|[:\-\|/])(" + string.Join("|", sortedPatterns) + @")(?:\s*(?:\d+|[A-Z]))?(?:\s|[:\-\|/]|$)",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);

        var matches = roomRegex.Matches(text);
        if (matches.Count == 0)
            return sections;

        for (int i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var roomName = match.Groups[1].Value.Trim();
            var startIndex = match.Index + match.Length;
            var endIndex = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;

            if (endIndex > startIndex)
            {
                var sectionText = text[startIndex..endIndex];
                sections.Add((roomName, sectionText));
            }
        }

        // Deduplicate rooms with same name — merge their text
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, sectionText) in sections)
        {
            var key = name.ToUpperInvariant().Trim();
            if (merged.ContainsKey(key))
                merged[key] += "\n" + sectionText;
            else
                merged[key] = sectionText;
        }

        return merged.Select(kv => (kv.Key, kv.Value)).ToList();
    }

    private List<ExtractedLineItem> ExtractLineItems(string sectionText)
    {
        var items = new List<ExtractedLineItem>();
        var matched = new HashSet<string>();

        var normalized = Regex.Replace(sectionText, @"\s+", " ");

        foreach (var (pattern, templateName) in ItemPatterns)
        {
            if (matched.Contains(templateName))
                continue;

            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            var match = regex.Match(normalized);
            if (!match.Success)
                continue;

            matched.Add(templateName);

            // Try to extract a quantity near the match
            decimal? quantity = ExtractQuantityNear(normalized, match.Index);

            // Get a snippet of raw text around the match for context
            var rawStart = Math.Max(0, match.Index - 30);
            var rawEnd = Math.Min(normalized.Length, match.Index + match.Length + 50);
            var rawText = normalized[rawStart..rawEnd].Trim();

            items.Add(new ExtractedLineItem
            {
                MatchedTemplate = templateName,
                Quantity = quantity,
                RawText = rawText
            });
        }

        return items;
    }

    private decimal? ExtractQuantityNear(string text, int matchIndex)
    {
        // Search in a window around the match
        var start = Math.Max(0, matchIndex - 150);
        var end = Math.Min(text.Length, matchIndex + 100);
        var window = text[start..end];

        // Try simple LF/SF/EA patterns first
        var simpleMatch = SimpleQtyRegex.Match(window);
        if (simpleMatch.Success && decimal.TryParse(simpleMatch.Groups[1].Value, out var simpleQty) && simpleQty > 0 && simpleQty < 10000)
            return simpleQty;

        // Try architectural dimensions: sum all feet-inches in the window
        // These are like "2'-4" VIF", "6'-11" VIF" — individual segment widths
        var archMatches = ArchDimensionRegex.Matches(window);
        if (archMatches.Count > 0)
        {
            decimal totalFeet = 0;
            foreach (Match m in archMatches)
            {
                if (int.TryParse(m.Groups[1].Value, out var feet) && int.TryParse(m.Groups[2].Value, out var inches))
                {
                    totalFeet += feet + (inches / 12m);
                }
            }
            if (totalFeet > 0 && totalFeet < 200)
                return Math.Round(totalFeet, 1);
        }

        // Try standalone numbers that might be feet
        var standaloneNum = Regex.Match(window, @"(\d{1,3})(?:\s*(?:feet|foot|ft|'))", RegexOptions.IgnoreCase);
        if (standaloneNum.Success && decimal.TryParse(standaloneNum.Groups[1].Value, out var ftQty) && ftQty > 0 && ftQty < 200)
            return ftQty;

        return null;
    }

    private static string FormatRoomName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;
        var words = raw.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", words.Select(w =>
            char.ToUpper(w[0]) + (w.Length > 1 ? w[1..].ToLower() : "")));
    }

    public class ExtractedRoom
    {
        public string Name { get; set; } = string.Empty;
        public List<ExtractedLineItem> Items { get; set; } = [];
    }

    public class ExtractedLineItem
    {
        public string MatchedTemplate { get; set; } = string.Empty;
        public decimal? Quantity { get; set; }
        public string RawText { get; set; } = string.Empty;
    }

    public class HighlightMatch
    {
        public string MatchedText { get; set; } = string.Empty;
        public string TemplateName { get; set; } = string.Empty;
        public int StartIndex { get; set; }
        public int Length { get; set; }
    }
}
