using System.IO;
using ClosedXML.Excel;
using MacEstimator.App.Models;

namespace MacEstimator.App.Services;

public class PricingConfigService
{
    private static readonly string SharedFolder = @"G:\My Drive\MAC\Estimator";
    private static readonly string ExcelPath = Path.Combine(SharedFolder, "pricing_config.xlsx");

    private LineItemTemplate[]? _cached;

    public async Task<LineItemTemplate[]> LoadAsync()
    {
        if (_cached is not null) return _cached;

        if (!File.Exists(ExcelPath))
        {
            _cached = DefaultLineItems.All;
            await SaveAsync(_cached);
            return _cached;
        }

        try
        {
            _cached = await Task.Run(LoadFromExcel);
            return _cached;
        }
        catch
        {
            _cached = DefaultLineItems.All;
            return _cached;
        }
    }

    public async Task SaveAsync(LineItemTemplate[] templates)
    {
        Directory.CreateDirectory(SharedFolder);

        if (File.Exists(ExcelPath))
        {
            var backupDir = Path.Combine(SharedFolder, "pricing_backups");
            Directory.CreateDirectory(backupDir);
            var backupName = $"pricing_config_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            File.Copy(ExcelPath, Path.Combine(backupDir, backupName), true);

            var backups = new DirectoryInfo(backupDir)
                .GetFiles("pricing_config_*.xlsx")
                .OrderByDescending(f => f.CreationTime)
                .Skip(20);
            foreach (var old in backups)
                old.Delete();
        }

        await Task.Run(() => SaveToExcel(templates));
    }

    private static LineItemTemplate[] LoadFromExcel()
    {
        using var workbook = new XLWorkbook(ExcelPath);
        var sheet = workbook.Worksheets.First();
        var templates = new List<LineItemTemplate>();

        int row = 2;
        while (!sheet.Cell(row, 1).IsEmpty())
        {
            var name = sheet.Cell(row, 1).GetString().Trim();
            var plamRate = GetDecimal(sheet, row, 2);
            var paintRate = GetNullableDecimal(sheet, row, 3);
            var stainRate = GetNullableDecimal(sheet, row, 4);
            var unitStr = sheet.Cell(row, 5).GetString().Trim();
            var modeStr = sheet.Cell(row, 6).GetString().Trim();
            var optionsStr = sheet.Cell(row, 7).GetString().Trim();

            var unit = unitStr switch
            {
                "SF" => UnitType.SquareFoot,
                "EA" => UnitType.Each,
                _ => UnitType.LinearFoot
            };
            var mode = modeStr == "VendorQuote" ? PricingMode.VendorQuoteMarkup : PricingMode.PerUnit;
            string[]? options = string.IsNullOrEmpty(optionsStr) ? null : optionsStr.Split('|');

            if (!string.IsNullOrEmpty(name))
            {
                templates.Add(new LineItemTemplate(
                    name, plamRate, unit, mode, options, paintRate, stainRate));
            }

            row++;
        }

        return templates.Count > 0 ? templates.ToArray() : DefaultLineItems.All;
    }

    private static void SaveToExcel(LineItemTemplate[] templates)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Pricing");

        // Headers
        var headers = new[] { "Item Name", "PLAM Rate", "Paint Grade Rate", "Stain Grade Rate", "Unit", "Mode", "Name Options (pipe-separated)" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.DarkBlue;
            cell.Style.Font.FontColor = XLColor.White;
        }

        // Color-code rate columns
        sheet.Cell(1, 2).Style.Fill.BackgroundColor = XLColor.DarkCyan;
        sheet.Cell(1, 3).Style.Fill.BackgroundColor = XLColor.DarkOrange;
        sheet.Cell(1, 4).Style.Fill.BackgroundColor = XLColor.Brown;

        int row = 2;
        foreach (var t in templates)
        {
            sheet.Cell(row, 1).Value = t.Name;
            sheet.Cell(row, 2).Value = (double)t.DefaultRate;
            sheet.Cell(row, 2).Style.NumberFormat.Format = t.DefaultRate < 10 ? "#,##0.00" : "$#,##0";

            if (t.PaintGradeRate.HasValue)
            {
                sheet.Cell(row, 3).Value = (double)t.PaintGradeRate.Value;
                sheet.Cell(row, 3).Style.NumberFormat.Format = t.PaintGradeRate.Value < 10 ? "#,##0.00" : "$#,##0";
            }

            if (t.StainGradeRate.HasValue)
            {
                sheet.Cell(row, 4).Value = (double)t.StainGradeRate.Value;
                sheet.Cell(row, 4).Style.NumberFormat.Format = t.StainGradeRate.Value < 10 ? "#,##0.00" : "$#,##0";
            }

            sheet.Cell(row, 5).Value = t.Unit switch
            {
                UnitType.SquareFoot => "SF",
                UnitType.Each => "EA",
                _ => "LF"
            };

            sheet.Cell(row, 6).Value = t.Mode == PricingMode.VendorQuoteMarkup ? "VendorQuote" : "PerUnit";

            if (t.NameOptions is { Length: > 0 })
                sheet.Cell(row, 7).Value = string.Join("|", t.NameOptions);

            // Alternate row shading
            if (row % 2 == 0)
            {
                for (int c = 1; c <= 7; c++)
                    sheet.Cell(row, c).Style.Fill.BackgroundColor = XLColor.FromArgb(240, 240, 240);
            }

            row++;
        }

        // Auto-fit and freeze header
        sheet.Columns().AdjustToContents();
        sheet.SheetView.FreezeRows(1);

        workbook.SaveAs(ExcelPath);
    }

    private static decimal GetDecimal(IXLWorksheet sheet, int row, int col)
    {
        var cell = sheet.Cell(row, col);
        if (cell.IsEmpty()) return 0m;
        try { return (decimal)cell.GetDouble(); }
        catch { return 0m; }
    }

    private static decimal? GetNullableDecimal(IXLWorksheet sheet, int row, int col)
    {
        var cell = sheet.Cell(row, col);
        if (cell.IsEmpty()) return null;
        try { return (decimal)cell.GetDouble(); }
        catch { return null; }
    }
}
