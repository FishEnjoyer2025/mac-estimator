using System.IO;
using ClosedXML.Excel;
using MacEstimator.App.Models;

namespace MacEstimator.App.Services;

public class KeywordConfigService
{
    private static readonly string SharedFolder = @"G:\My Drive\MAC\Estimator";
    private static readonly string ExcelPath = Path.Combine(SharedFolder, "keyword_config.xlsx");

    public async Task<KeywordConfig> LoadAsync()
    {
        if (!File.Exists(ExcelPath))
        {
            var defaults = CreateDefaults();
            await SaveAsync(defaults);
            return defaults;
        }

        try
        {
            return await Task.Run(LoadFromExcel);
        }
        catch
        {
            return CreateDefaults();
        }
    }

    public async Task SaveAsync(KeywordConfig config)
    {
        Directory.CreateDirectory(SharedFolder);

        // Backup existing file before overwriting
        if (File.Exists(ExcelPath))
        {
            var backupDir = Path.Combine(SharedFolder, "keyword_backups");
            Directory.CreateDirectory(backupDir);
            var backupName = $"keyword_config_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            File.Copy(ExcelPath, Path.Combine(backupDir, backupName), true);

            // Keep only last 20 backups
            var backups = new DirectoryInfo(backupDir)
                .GetFiles("keyword_config_*.xlsx")
                .OrderByDescending(f => f.CreationTime)
                .Skip(20);
            foreach (var old in backups)
                old.Delete();
        }

        await Task.Run(() => SaveToExcel(config));
    }

    private static KeywordConfig LoadFromExcel()
    {
        var config = new KeywordConfig();

        using var workbook = new XLWorkbook(ExcelPath);
        var sheet = workbook.Worksheets.First();

        // Read good keywords from column A (starting row 2)
        int row = 2;
        while (!sheet.Cell(row, 1).IsEmpty())
        {
            var keyword = sheet.Cell(row, 1).GetString().Trim();
            var dateStr = sheet.Cell(row, 2).GetString().Trim();
            DateTime.TryParse(dateStr, out var date);

            if (!string.IsNullOrEmpty(keyword))
                config.GoodKeywords.Add(new KeywordEntry { Keyword = keyword, DateAdded = date == default ? DateTime.Now : date });

            row++;
        }

        // Read bad keywords from column D (starting row 2)
        row = 2;
        while (!sheet.Cell(row, 4).IsEmpty())
        {
            var keyword = sheet.Cell(row, 4).GetString().Trim();
            var dateStr = sheet.Cell(row, 5).GetString().Trim();
            DateTime.TryParse(dateStr, out var date);

            if (!string.IsNullOrEmpty(keyword))
                config.BadKeywords.Add(new KeywordEntry { Keyword = keyword, DateAdded = date == default ? DateTime.Now : date });

            row++;
        }

        return config;
    }

    private static void SaveToExcel(KeywordConfig config)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Keywords");

        // Good keywords column (A)
        sheet.Cell(1, 1).Value = "Good Keywords";
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.DarkGreen;
        sheet.Cell(1, 1).Style.Font.FontColor = XLColor.White;

        sheet.Cell(1, 2).Value = "Date Added";
        sheet.Cell(1, 2).Style.Font.Bold = true;
        sheet.Cell(1, 2).Style.Fill.BackgroundColor = XLColor.DarkGreen;
        sheet.Cell(1, 2).Style.Font.FontColor = XLColor.White;

        // Bad keywords column (D) -- gap column C for spacing
        sheet.Cell(1, 4).Value = "Bad Keywords";
        sheet.Cell(1, 4).Style.Font.Bold = true;
        sheet.Cell(1, 4).Style.Fill.BackgroundColor = XLColor.DarkRed;
        sheet.Cell(1, 4).Style.Font.FontColor = XLColor.White;

        sheet.Cell(1, 5).Value = "Date Added";
        sheet.Cell(1, 5).Style.Font.Bold = true;
        sheet.Cell(1, 5).Style.Fill.BackgroundColor = XLColor.DarkRed;
        sheet.Cell(1, 5).Style.Font.FontColor = XLColor.White;

        int row = 2;
        foreach (var entry in config.GoodKeywords.OrderBy(k => k.Keyword))
        {
            sheet.Cell(row, 1).Value = entry.Keyword;
            sheet.Cell(row, 1).Style.Font.FontColor = XLColor.Green;
            sheet.Cell(row, 2).Value = entry.DateAdded.ToString("M/d/yyyy");
            row++;
        }

        row = 2;
        foreach (var entry in config.BadKeywords.OrderBy(k => k.Keyword))
        {
            sheet.Cell(row, 4).Value = entry.Keyword;
            sheet.Cell(row, 4).Style.Font.FontColor = XLColor.Red;
            sheet.Cell(row, 5).Value = entry.DateAdded.ToString("M/d/yyyy");
            row++;
        }

        sheet.Column(3).Width = 3; // spacer
        sheet.Columns(1, 2).AdjustToContents();
        sheet.Columns(4, 5).AdjustToContents();
        workbook.SaveAs(ExcelPath);
    }

    private static KeywordConfig CreateDefaults()
    {
        return new KeywordConfig
        {
            GoodKeywords =
            [
                new() { Keyword = "Solid Surface" },
                new() { Keyword = "PLAM" },
                new() { Keyword = "Plastic Laminate" },
                new() { Keyword = "Upper Cabinets" },
                new() { Keyword = "Base Cabinets" },
                new() { Keyword = "Casework" },
                new() { Keyword = "Millwork" },
                new() { Keyword = "Reception Desk" },
                new() { Keyword = "Nurse Station" },
                new() { Keyword = "Break Room" },
                new() { Keyword = "Mail Room" },
                new() { Keyword = "Copy Room" },
                new() { Keyword = "File Cabinets" },
                new() { Keyword = "Bookshelves" },
                new() { Keyword = "Countertop" },
                new() { Keyword = "P-Lam" },
                new() { Keyword = "HPL" },
                new() { Keyword = "Tall Cabinets" },
                new() { Keyword = "Pantry" },
            ],
            BadKeywords =
            [
                new() { Keyword = "Radius" },
                new() { Keyword = "Curved" },
                new() { Keyword = "Metal Fabrication" },
                new() { Keyword = "Stainless Steel" },
                new() { Keyword = "Glass Doors" },
                new() { Keyword = "Etched Glass" },
                new() { Keyword = "Custom Hardware" },
                new() { Keyword = "Stone Countertop" },
                new() { Keyword = "Granite" },
                new() { Keyword = "Quartz" },
                new() { Keyword = "Corian" },
            ]
        };
    }
}
