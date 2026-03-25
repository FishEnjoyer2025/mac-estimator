using System.IO;
using System.Reflection;
using MacEstimator.App.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MacEstimator.App.Services;

public class PdfGenerator
{
    private static byte[] LoadLogo()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("MacEstimator.App.Assets.mac-logo.jpg")
            ?? throw new InvalidOperationException("Logo resource not found");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    public void Generate(Estimate estimate, string outputPath)
    {
        var logoBytes = LoadLogo();

        var grandTotal = estimate.Rooms
            .SelectMany(r => r.LineItems)
            .Where(li => li.IsEnabled)
            .Sum(li => li.LineTotal);

        var adjustmentAmount = grandTotal * estimate.AdjustmentPercent / 100m;
        var adjustedTotal = grandTotal + adjustmentAmount;

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.MarginHorizontal(0.75f, Unit.Inch);
                page.MarginVertical(0.5f, Unit.Inch);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Segoe UI"));

                page.Content().Column(col =>
                {
                    col.Spacing(8);

                    // === HEADER: Logo + Contact Info ===
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            left.Item().Text($"Ph# {CompanyInfo.Phone}").FontSize(10).Italic();
                            left.Item().Text($"Fax# {CompanyInfo.Fax}").FontSize(10).Italic();
                            var bidderEmail = estimate.SubmittedBy switch
                            {
                                "Rusty Philbrick" => "Rusty@MACBuildsKC.com",
                                "Josh Irsik" => "Josh@MACBuildsKC.com",
                                _ => CompanyInfo.Email
                            };
                            left.Item().Text(bidderEmail)
                                .FontSize(10).Italic().FontColor(Colors.Blue.Medium);
                        });

                        row.ConstantItem(100).AlignCenter()
                            .Image(logoBytes).FitWidth();

                        row.RelativeItem().AlignRight().Column(right =>
                        {
                            right.Item().AlignRight().Text(CompanyInfo.Address).FontSize(10);
                            right.Item().AlignRight().Text(CompanyInfo.City).FontSize(10);
                            right.Item().AlignRight().Text(CompanyInfo.StateZip).FontSize(10);
                        });
                    });

                    // === DATE ===
                    col.Item().AlignRight().Text(DateTime.Now.ToString("M/d/yyyy")).FontSize(10);

                    col.Item().PaddingTop(10);

                    // === CLIENT INFO ===
                    col.Item().Column(client =>
                    {
                        if (!string.IsNullOrWhiteSpace(estimate.ClientName))
                            client.Item().Text(estimate.ClientName);
                        if (!string.IsNullOrWhiteSpace(estimate.ClientCompany))
                            client.Item().Text(estimate.ClientCompany);
                        if (!string.IsNullOrWhiteSpace(estimate.ClientEmail))
                            client.Item().Text(estimate.ClientEmail).FontColor(Colors.Blue.Medium);
                        if (!string.IsNullOrWhiteSpace(estimate.ClientPhone))
                            client.Item().Text(estimate.ClientPhone);
                    });

                    col.Item().PaddingTop(10);

                    // === RE: LINE ===
                    var reLine = !string.IsNullOrWhiteSpace(estimate.JobNumber)
                        ? $"RE: {estimate.JobName}  (Job #{estimate.JobNumber})"
                        : $"RE: {estimate.JobName}";
                    col.Item().Text(reLine).Bold();

                    col.Item().PaddingTop(8);

                    // === ROOMS & LINE ITEMS (customer-facing: names + totals only) ===
                    foreach (var room in estimate.Rooms)
                    {
                        var enabledItems = room.LineItems.Where(li => li.IsEnabled).ToList();
                        if (enabledItems.Count == 0)
                            continue;

                        var roomTotal = enabledItems.Sum(li => li.LineTotal);

                        // Room name
                        col.Item().Text(room.Name).Bold().FontSize(11);

                        // Line items — names only, no pricing
                        foreach (var item in enabledItems)
                        {
                            var displayName = item.Name;
                            if (!string.IsNullOrWhiteSpace(item.Note))
                                displayName += $" - {item.Note}";

                            col.Item().PaddingLeft(16).Text(displayName).FontSize(10);
                        }

                        // Room total
                        col.Item().PaddingTop(2).Row(row =>
                        {
                            row.RelativeItem();
                            row.ConstantItem(200).AlignRight()
                                .Text($"{room.Name} Total:  {roomTotal:C2}")
                                .FontSize(11).Bold();
                        });

                        col.Item().PaddingTop(6);
                    }

                    // === SEPARATOR ===
                    col.Item().PaddingTop(8).LineHorizontal(0.5f).LineColor("#808080");

                    // === TOTALS ===
                    col.Item().PaddingTop(10);

                    if (estimate.AdjustmentPercent != 0 && !string.IsNullOrWhiteSpace(estimate.AdjustmentLabel))
                    {
                        // Subtotal
                        col.Item().Row(row =>
                        {
                            row.RelativeItem();
                            row.ConstantItem(250).AlignRight().Text(text =>
                            {
                                text.Span("Subtotal:  ").FontSize(11);
                                text.Span(grandTotal.ToString("C2")).FontSize(11).Bold();
                            });
                        });

                        // Adjustment
                        col.Item().Row(row =>
                        {
                            row.RelativeItem();
                            row.ConstantItem(250).AlignRight().Text(text =>
                            {
                                text.Span($"{estimate.AdjustmentLabel} ({estimate.AdjustmentPercent:0.##}%):  ").FontSize(11);
                                text.Span(adjustmentAmount.ToString("C2")).FontSize(11).Bold();
                            });
                        });

                        col.Item().PaddingTop(4);
                    }

                    // Grand total
                    col.Item().Row(row =>
                    {
                        row.RelativeItem();
                        row.ConstantItem(300).AlignCenter().Text(text =>
                        {
                            text.Span("Total Casework Elevations Noted Above:  ").FontSize(11);
                            text.Span(adjustedTotal.ToString("C2")).FontSize(12).Bold();
                        });
                    });

                    // === EXCLUSIONS ===
                    if (!string.IsNullOrWhiteSpace(estimate.Exclusions))
                    {
                        col.Item().PaddingTop(20);
                        col.Item().AlignCenter().Text(estimate.Exclusions)
                            .FontSize(10).FontColor(Colors.Red.Medium);
                    }

                    // === NOTES ===
                    if (!string.IsNullOrWhiteSpace(estimate.Notes))
                    {
                        col.Item().PaddingTop(10);
                        col.Item().Text("Notes:").FontSize(10).Bold();
                        col.Item().Text(estimate.Notes).FontSize(10);
                    }

                    // === BOILERPLATE ===
                    col.Item().PaddingTop(16);
                    col.Item().Text(CompanyInfo.Boilerplate).FontSize(10);

                    // === SUBMITTED BY ===
                    col.Item().PaddingTop(30);
                    col.Item().Text("Submitted By: ________________");
                    col.Item().Text(estimate.SubmittedBy);
                });
            });
        })
        .GeneratePdf(outputPath);
    }
}
