using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MacEstimator.App.Services;

/// <summary>
/// Hybrid AI: fast OCR regex matching finds rooms/items, Claude extracts quantities + client info from matched snippets.
/// </summary>
public class GeminiService
{
    private readonly PdfTextExtractor _pdfExtractor;
    private readonly PlanReaderService _planReader;

    public bool IsAvailable => true;

    public GeminiService(PdfTextExtractor pdfExtractor, PlanReaderService planReader)
    {
        _pdfExtractor = pdfExtractor;
        _planReader = planReader;
    }

    public async Task<AiBidResult> AnalyzePlans(string pdfPath, IProgress<string>? progress = null)
    {
        // Step 1: OCR extract text (handles scanned plans)
        progress?.Report("Extracting text from plans...");
        var pages = await Task.Run(() => _pdfExtractor.ExtractText(pdfPath));
        var allText = string.Join("\n", pages.OrderBy(p => p.Key).Select(p => p.Value));

        if (string.IsNullOrWhiteSpace(allText))
            throw new InvalidOperationException("Could not extract text from PDF.");

        // Step 2: Fast regex matching for rooms and items
        progress?.Report("Identifying rooms and items...");
        var extractedRooms = _planReader.ExtractRooms(allText);

        // Step 3: Build a compact prompt with ONLY the matched snippets + first page (title block)
        // Send first page (cover/title block) and last portion of each page (title blocks are bottom-right)
        var firstPageText = pages.ContainsKey(1) ? pages[1] : "";
        // Also grab the tail of page 1 and page 2 where title blocks often appear
        var titleBlockText = firstPageText;
        if (pages.ContainsKey(2))
            titleBlockText += "\n" + pages[2];
        if (titleBlockText.Length > 4000) titleBlockText = titleBlockText[..4000];

        var snippets = new System.Text.StringBuilder();
        snippets.AppendLine("TITLE BLOCK / COVER PAGES:");
        snippets.AppendLine(titleBlockText);
        snippets.AppendLine();

        foreach (var room in extractedRooms)
        {
            snippets.AppendLine($"ROOM: {room.Name}");
            foreach (var item in room.Items)
            {
                snippets.AppendLine($"  ITEM: {item.MatchedTemplate} | CONTEXT: {item.RawText}");
            }
            snippets.AppendLine();
        }

        // If no rooms found by regex, send first few pages for AI to try
        if (extractedRooms.Count == 0)
        {
            snippets.Clear();
            snippets.AppendLine("No rooms detected by pattern matching. Full OCR text (first 5000 chars):");
            snippets.AppendLine(allText.Length > 5000 ? allText[..5000] : allText);
        }

        // Step 4: Send compact prompt to Claude for quantities + client info
        progress?.Report("AI extracting quantities (~10 sec)...");
        var aiResponse = await RunClaude(snippets.ToString());

        // Merge AI quantities back into regex-detected rooms
        if (extractedRooms.Count > 0 && aiResponse.Rooms.Count > 0)
        {
            foreach (var aiRoom in aiResponse.Rooms)
            {
                var matchedRoom = extractedRooms.FirstOrDefault(r =>
                    r.Name.Contains(aiRoom.Name, StringComparison.OrdinalIgnoreCase) ||
                    aiRoom.Name.Contains(r.Name, StringComparison.OrdinalIgnoreCase));
                if (matchedRoom == null) continue;

                foreach (var aiItem in aiRoom.Items.Where(i => i.Quantity > 0))
                {
                    var matchedItem = matchedRoom.Items.FirstOrDefault(i =>
                        i.MatchedTemplate.Contains(aiItem.Item, StringComparison.OrdinalIgnoreCase) ||
                        aiItem.Item.Contains(i.MatchedTemplate.Replace("PLAM ", ""), StringComparison.OrdinalIgnoreCase));
                    if (matchedItem != null)
                        matchedItem.Quantity = aiItem.Quantity;
                }
            }

            // Convert to AiBidResult format
            aiResponse.Rooms = extractedRooms.Select(r => new AiBidRoom
            {
                Name = r.Name,
                Items = r.Items.Select(i => new AiBidItem
                {
                    Item = i.MatchedTemplate.Replace("PLAM ", ""),
                    Quantity = i.Quantity ?? 0,
                    Unit = GetUnit(i.MatchedTemplate),
                    Notes = i.RawText
                }).ToList()
            }).ToList();
        }

        return aiResponse;
    }

    private static async Task<AiBidResult> RunClaude(string contextText)
    {
        var prompt = "From these architectural plan excerpts for a commercial cabinet shop bid, extract: " +
            "1) CLIENT: Look for the GENERAL CONTRACTOR (GC) or OWNER in the title block — NOT the architect. " +
            "client_company = the GC or building owner (e.g. 'Harmon', 'JE Dunn', 'McCownGordon'). " +
            "client_name = GC contact person. client_address = project address. " +
            "2) GRADE: PLAM, Paint Grade, or Stain Grade from finish schedules. " +
            "3) ROOMS: For hotel/multifamily projects, rooms often repeat. " +
            "Add a 'multiplier' field for how many of that room type exist (e.g. 10 for 'Standard Single Room'). " +
            "Room types might have sub-areas (Restroom, Storage, Window Alcove) — group items under the parent room type. " +
            "4) QUANTITIES: Sum dimensions from the context if visible. Use 0 if unknown. " +
            "Available items: Upper Cabinets(LF), Base Cabinets(LF), Tall Cabinets(LF), " +
            "Solid Surface Countertops(SF), PLAM Countertops(SF), Quartz Countertops(SF), " +
            "ADA Vanity(LF), Shelves w/ Brackets(LF), Floating Shelf(LF), Wall Caps(LF), " +
            "Countertop w/ Support Panel(LF), Flip-Up Countertop(LF), " +
            "End Panels(Each), Brackets(Each), Stainless Steel Legs(Each). " +
            "Use 0 for unknown quantities, empty string for unknown fields. " +
            "Reply ONLY valid JSON. Schema: " +
            "{project_name:str, client_company:str, client_name:str, client_address:str, grade:str, " +
            "rooms:[{name:str, multiplier:int, items:[{item:str, quantity:number, unit:str, notes:str}]}], notes:str}\n\n" +
            contextText;

        // Pipe prompt via stdin to avoid command-line length limits
        var psi = new ProcessStartInfo
        {
            FileName = "claude",
            Arguments = "-p --model haiku",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = psi };
        var output = new System.Text.StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data != null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Write prompt to stdin then close it
        await process.StandardInput.WriteAsync(prompt);
        process.StandardInput.Close();

        var completed = await Task.Run(() => process.WaitForExit(45_000));
        if (!completed)
        {
            process.Kill();
            throw new TimeoutException("AI timed out after 45 seconds.");
        }

        var text = output.ToString().Trim();
        if (string.IsNullOrEmpty(text))
            throw new InvalidOperationException("AI returned no output.");

        var json = ExtractJson(text);
        if (json == null)
            throw new InvalidOperationException($"No JSON in AI response:\n{text[..Math.Min(300, text.Length)]}");

        return JsonSerializer.Deserialize<AiBidResult>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        }) ?? new AiBidResult();
    }

    private static string GetUnit(string templateName)
    {
        if (templateName.Contains("Countertop", StringComparison.OrdinalIgnoreCase)) return "SF";
        if (templateName.Contains("Panel", StringComparison.OrdinalIgnoreCase)) return "Each";
        if (templateName.Contains("Bracket", StringComparison.OrdinalIgnoreCase)) return "Each";
        if (templateName.Contains("Leg", StringComparison.OrdinalIgnoreCase)) return "Each";
        return "LF";
    }

    private static string? ExtractJson(string text)
    {
        var start = text.IndexOf('{');
        if (start < 0) return null;
        int depth = 0;
        for (int i = start; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}') depth--;
            if (depth == 0) return text[start..(i + 1)];
        }
        return null;
    }

    private static string EscapeArg(string arg) =>
        arg.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", "");
}

public class AiBidResult
{
    [JsonPropertyName("project_name")]
    public string ProjectName { get; set; } = "";

    [JsonPropertyName("client_company")]
    public string ClientCompany { get; set; } = "";

    [JsonPropertyName("client_name")]
    public string ClientName { get; set; } = "";

    [JsonPropertyName("client_address")]
    public string ClientAddress { get; set; } = "";

    [JsonPropertyName("grade")]
    public string Grade { get; set; } = "PLAM";

    [JsonPropertyName("rooms")]
    public List<AiBidRoom> Rooms { get; set; } = [];

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = "";
}

public class AiBidRoom
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("multiplier")]
    public int Multiplier { get; set; } = 1;

    [JsonPropertyName("items")]
    public List<AiBidItem> Items { get; set; } = [];
}

public class AiBidItem
{
    [JsonPropertyName("item")]
    public string Item { get; set; } = "";

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }

    [JsonPropertyName("unit")]
    public string Unit { get; set; } = "";

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = "";
}
