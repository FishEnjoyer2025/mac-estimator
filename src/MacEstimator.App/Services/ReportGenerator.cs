using System.IO;
using MacEstimator.App.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MacEstimator.App.Services;

public class ReportGenerator
{
    private const string AccentColor = "#0078d4";
    private const string AccentLight = "#3a96dd";
    private const string HeaderBg = "#1b1b1b";
    private const string CardBg = "#f4f4f4";
    private const string TableHeaderBg = "#e6f0fa";
    private const string LightBorder = "#d0d0d0";
    private const string GreenColor = "#13a10e";
    private const string RedColor = "#d13438";
    private const string YellowColor = "#c19c00";
    private const string MutedText = "#666666";
    private const string ReportsFolder = @"G:\My Drive\MAC\Estimator\Reports";

    public static string GetReportsFolder()
    {
        Directory.CreateDirectory(ReportsFolder);
        return ReportsFolder;
    }

    // ================================================================
    // A. WEEKLY BID PIPELINE REPORT
    // ================================================================

    public void GenerateWeeklyPipeline(string outputPath, List<JobIndexEntry> jobs)
    {
        var now = DateTime.Now;
        var weekStart = now.AddDays(-7);
        var dateRange = $"{weekStart:MMM d} - {now:MMM d, yyyy}";

        // Compute metrics
        var activeBids = jobs.Where(j => j.Status is BidStatus.Draft or BidStatus.Submitted or BidStatus.FollowedUp).ToList();
        var pipelineValue = activeBids.Sum(j => j.Total);
        var submittedThisWeek = jobs.Count(j => j.SubmittedAt >= weekStart);
        var wonThisWeek = jobs.Where(j => j.Status == BidStatus.Won && j.ResolvedAt >= weekStart).ToList();
        var lostThisWeek = jobs.Where(j => j.Status == BidStatus.Lost && j.ResolvedAt >= weekStart).ToList();
        var needFollowUp = activeBids.Where(j =>
            j.Status == BidStatus.Submitted &&
            (j.FollowedUpAt ?? j.SubmittedAt ?? j.ModifiedAt) < now.AddDays(-7)).ToList();

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.MarginHorizontal(0.6f, Unit.Inch);
                page.MarginVertical(0.5f, Unit.Inch);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Segoe UI"));

                page.Header().Column(col =>
                {
                    col.Item().Background(HeaderBg).Padding(16).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("MAC CABINETS").FontSize(18).Bold().FontColor(AccentColor);
                            c.Item().Text("Weekly Bid Pipeline").FontSize(12).FontColor("#ffffff");
                        });
                        row.ConstantItem(200).AlignRight().AlignMiddle()
                            .Text(dateRange).FontSize(10).FontColor("#aaaaaa");
                    });
                    col.Item().LineHorizontal(3).LineColor(AccentColor);
                });

                page.Content().PaddingTop(12).Column(col =>
                {
                    col.Spacing(10);

                    // Summary cards
                    col.Item().Row(row =>
                    {
                        SummaryCard(row, "Active Bids", activeBids.Count.ToString(), AccentColor);
                        SummaryCard(row, "Pipeline Value", pipelineValue.ToString("C0"), AccentColor);
                        SummaryCard(row, "Submitted (7d)", submittedThisWeek.ToString(), AccentLight);
                        SummaryCard(row, "Won (7d)", wonThisWeek.Count.ToString(), GreenColor);
                        SummaryCard(row, "Lost (7d)", lostThisWeek.Count.ToString(), RedColor);
                        SummaryCard(row, "Need Follow-Up", needFollowUp.Count.ToString(),
                            needFollowUp.Count > 0 ? YellowColor : MutedText);
                    });

                    // Active bids table
                    col.Item().PaddingTop(8).Text("ACTIVE BIDS").FontSize(11).Bold().FontColor(AccentColor);

                    var sortedActive = activeBids
                        .OrderByDescending(j => DaysSinceAction(j))
                        .ToList();

                    if (sortedActive.Count > 0)
                    {
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(2.5f); // Job Name
                                c.RelativeColumn(2f);   // Company
                                c.RelativeColumn(1.2f); // Estimator
                                c.RelativeColumn(1.2f); // Amount
                                c.RelativeColumn(1f);   // Status
                                c.RelativeColumn(0.8f); // Days
                            });

                            table.Header(header =>
                            {
                                AddHeaderCell(header.Cell(),"Job Name");
                                AddHeaderCell(header.Cell(),"Company");
                                AddHeaderCell(header.Cell(),"Estimator");
                                AddHeaderCell(header.Cell(),"Amount");
                                AddHeaderCell(header.Cell(),"Status");
                                AddHeaderCell(header.Cell(),"Days");
                            });

                            foreach (var job in sortedActive)
                            {
                                var days = DaysSinceAction(job);
                                var daysColor = days >= 14 ? RedColor : days >= 7 ? YellowColor : "#000000";

                                AddDataCell(table.Cell(),job.JobName);
                                AddDataCell(table.Cell(),job.ClientCompany);
                                AddDataCell(table.Cell(),job.SubmittedBy.Split(' ')[0]);
                                AddDataCell(table.Cell(),job.Total.ToString("C0"));
                                AddDataCell(table.Cell(),job.Status.ToString());
                                table.Cell().BorderBottom(0.5f).BorderColor(LightBorder)
                                    .Padding(4).AlignMiddle()
                                    .Text(days.ToString()).FontSize(9).FontColor(daysColor).Bold();
                            }
                        });
                    }
                    else
                    {
                        col.Item().Text("No active bids.").FontSize(9).FontColor(MutedText);
                    }

                    // Won/Lost this week
                    if (wonThisWeek.Count > 0 || lostThisWeek.Count > 0)
                    {
                        col.Item().PaddingTop(12).Text("WON / LOST THIS WEEK").FontSize(11).Bold().FontColor(AccentColor);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(2.5f); // Job Name
                                c.RelativeColumn(2f);   // Company
                                c.RelativeColumn(1.2f); // Amount
                                c.RelativeColumn(0.8f); // Result
                                c.RelativeColumn(1.5f); // Lost To
                                c.RelativeColumn(1.2f); // Their Price
                                c.RelativeColumn(2f);   // Reason
                            });

                            table.Header(header =>
                            {
                                AddHeaderCell(header.Cell(),"Job Name");
                                AddHeaderCell(header.Cell(),"Company");
                                AddHeaderCell(header.Cell(),"Amount");
                                AddHeaderCell(header.Cell(),"Result");
                                AddHeaderCell(header.Cell(),"Lost To");
                                AddHeaderCell(header.Cell(),"Their Price");
                                AddHeaderCell(header.Cell(),"Reason");
                            });

                            foreach (var job in wonThisWeek)
                            {
                                AddDataCell(table.Cell(),job.JobName);
                                AddDataCell(table.Cell(),job.ClientCompany);
                                AddDataCell(table.Cell(),job.Total.ToString("C0"));
                                table.Cell().BorderBottom(0.5f).BorderColor(LightBorder)
                                    .Padding(4).AlignMiddle()
                                    .Text("WON").FontSize(9).Bold().FontColor(GreenColor);
                                AddDataCell(table.Cell(),"");
                                AddDataCell(table.Cell(),"");
                                AddDataCell(table.Cell(),"");
                            }

                            foreach (var job in lostThisWeek)
                            {
                                AddDataCell(table.Cell(),job.JobName);
                                AddDataCell(table.Cell(),job.ClientCompany);
                                AddDataCell(table.Cell(),job.Total.ToString("C0"));
                                table.Cell().BorderBottom(0.5f).BorderColor(LightBorder)
                                    .Padding(4).AlignMiddle()
                                    .Text("LOST").FontSize(9).Bold().FontColor(RedColor);
                                AddDataCell(table.Cell(),job.LostTo ?? "");
                                AddDataCell(table.Cell(),job.CompetitorPrice?.ToString("C0") ?? "");
                                AddDataCell(table.Cell(),job.LostReason ?? "");
                            }
                        });
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Generated by MAC Estimator").FontSize(8).FontColor(MutedText);
                    text.Span("  |  ").FontSize(8).FontColor(MutedText);
                    text.Span(now.ToString("M/d/yyyy h:mm tt")).FontSize(8).FontColor(MutedText);
                });
            });
        })
        .GeneratePdf(outputPath);
    }

    // ================================================================
    // B. MONTHLY BUSINESS PERFORMANCE REPORT
    // ================================================================

    public void GenerateMonthlyPerformance(string outputPath, List<JobIndexEntry> jobs, HistoricalData data)
    {
        var now = DateTime.Now;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var monthEnd = monthStart.AddMonths(1);
        var priorMonthStart = monthStart.AddMonths(-1);
        var monthLabel = now.ToString("MMMM yyyy");

        // Current month jobs
        var monthJobs = jobs.Where(j => j.ResolvedAt >= monthStart && j.ResolvedAt < monthEnd).ToList();
        var monthWon = monthJobs.Where(j => j.Status == BidStatus.Won).ToList();
        var monthLost = monthJobs.Where(j => j.Status == BidStatus.Lost).ToList();

        // Prior month jobs
        var priorJobs = jobs.Where(j => j.ResolvedAt >= priorMonthStart && j.ResolvedAt < monthStart).ToList();
        var priorWon = priorJobs.Where(j => j.Status == BidStatus.Won).ToList();
        var priorLost = priorJobs.Where(j => j.Status == BidStatus.Lost).ToList();

        // YTD
        var yearStart = new DateTime(now.Year, 1, 1);
        var ytdJobs = jobs.Where(j => j.ResolvedAt >= yearStart && j.ResolvedAt < monthEnd).ToList();
        var ytdWon = ytdJobs.Where(j => j.Status == BidStatus.Won).ToList();
        var ytdLost = ytdJobs.Where(j => j.Status == BidStatus.Lost).ToList();

        decimal WinRate(int won, int total) => total > 0 ? (decimal)won / total : 0;

        var currentWinRate = WinRate(monthWon.Count, monthWon.Count + monthLost.Count);
        var priorWinRate = WinRate(priorWon.Count, priorWon.Count + priorLost.Count);
        var ytdWinRate = WinRate(ytdWon.Count, ytdWon.Count + ytdLost.Count);

        var revenueWon = monthWon.Sum(j => j.Total);
        var avgBidSize = monthJobs.Count > 0 ? monthJobs.Average(j => j.Total) : 0;
        var avgMargin = data.Profitability?.AvgMargin ?? 0;

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.MarginHorizontal(0.6f, Unit.Inch);
                page.MarginVertical(0.5f, Unit.Inch);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Segoe UI"));

                page.Header().Column(col =>
                {
                    col.Item().Background(HeaderBg).Padding(16).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("MAC CABINETS").FontSize(18).Bold().FontColor(AccentColor);
                            c.Item().Text("Monthly Business Performance").FontSize(12).FontColor("#ffffff");
                        });
                        row.ConstantItem(200).AlignRight().AlignMiddle()
                            .Text(monthLabel).FontSize(12).FontColor("#aaaaaa");
                    });
                    col.Item().LineHorizontal(3).LineColor(AccentColor);
                });

                page.Content().PaddingTop(12).Column(col =>
                {
                    col.Spacing(8);

                    // KPI cards
                    col.Item().Row(row =>
                    {
                        SummaryCard(row, "Win Rate (Month)", currentWinRate.ToString("P0"), AccentColor);
                        SummaryCard(row, "Win Rate (Prior)", priorWinRate.ToString("P0"), MutedText);
                        SummaryCard(row, "Win Rate (YTD)", ytdWinRate.ToString("P0"), AccentLight);
                        SummaryCard(row, "Revenue Won", revenueWon.ToString("C0"), GreenColor);
                        SummaryCard(row, "Avg Bid Size", avgBidSize.ToString("C0"), AccentColor);
                        SummaryCard(row, "Avg Margin", avgMargin > 0 ? avgMargin.ToString("P0") : "N/A", AccentLight);
                    });

                    // Win rate trend: last 12 months from historical data
                    if (data.WinRates?.ByYear?.Count > 0 || data.Seasonal?.Count > 0)
                    {
                        col.Item().PaddingTop(8).Text("WIN RATE TREND (HISTORICAL)").FontSize(11).Bold().FontColor(AccentColor);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(1.5f);
                                c.RelativeColumn(1);
                                c.RelativeColumn(1);
                                c.RelativeColumn(1);
                                c.RelativeColumn(1.2f);
                            });

                            table.Header(header =>
                            {
                                AddHeaderCell(header.Cell(),"Year");
                                AddHeaderCell(header.Cell(),"Won");
                                AddHeaderCell(header.Cell(),"Lost");
                                AddHeaderCell(header.Cell(),"Win Rate");
                                AddHeaderCell(header.Cell(),"Revenue");
                            });

                            foreach (var (year, stats) in data.WinRates!.ByYear.OrderByDescending(y => y.Key))
                            {
                                AddDataCell(table.Cell(),year);
                                AddDataCell(table.Cell(),stats.Won.ToString());
                                AddDataCell(table.Cell(),stats.Lost.ToString());
                                table.Cell().BorderBottom(0.5f).BorderColor(LightBorder)
                                    .Padding(4).AlignMiddle()
                                    .Text(stats.Rate.ToString("P0")).FontSize(9)
                                    .FontColor(stats.Rate >= 0.5m ? GreenColor : stats.Rate >= 0.3m ? YellowColor : RedColor);
                                AddDataCell(table.Cell(),stats.Revenue.ToString("C0"));
                            }
                        });
                    }

                    // Estimator comparison
                    if (data.Profitability?.BidEstimators?.Count > 0)
                    {
                        col.Item().PaddingTop(10).Text("ESTIMATOR COMPARISON").FontSize(11).Bold().FontColor(AccentColor);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(2);
                                c.RelativeColumn(1);
                                c.RelativeColumn(1);
                                c.RelativeColumn(1);
                                c.RelativeColumn(1.5f);
                            });

                            table.Header(header =>
                            {
                                AddHeaderCell(header.Cell(),"Estimator");
                                AddHeaderCell(header.Cell(),"Bids");
                                AddHeaderCell(header.Cell(),"Won");
                                AddHeaderCell(header.Cell(),"Win %");
                                AddHeaderCell(header.Cell(),"Revenue Won");
                            });

                            foreach (var (name, stats) in data.Profitability.BidEstimators.OrderByDescending(e => e.Value.RevenueWon))
                            {
                                AddDataCell(table.Cell(),name);
                                AddDataCell(table.Cell(),stats.Bids.ToString());
                                AddDataCell(table.Cell(),stats.Won.ToString());
                                table.Cell().BorderBottom(0.5f).BorderColor(LightBorder)
                                    .Padding(4).AlignMiddle()
                                    .Text(stats.WinRate.ToString("P0")).FontSize(9)
                                    .FontColor(stats.WinRate >= 0.5m ? GreenColor : stats.WinRate >= 0.3m ? YellowColor : RedColor);
                                AddDataCell(table.Cell(),stats.RevenueWon.ToString("C0"));
                            }
                        });
                    }

                    // Top 10 won jobs this month
                    if (monthWon.Count > 0)
                    {
                        col.Item().PaddingTop(10).Text("TOP WON JOBS (THIS MONTH)").FontSize(11).Bold().FontColor(GreenColor);
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(3);
                                c.RelativeColumn(1.5f);
                            });
                            table.Header(header =>
                            {
                                AddHeaderCell(header.Cell(),"Job Name");
                                AddHeaderCell(header.Cell(),"Amount");
                            });
                            foreach (var job in monthWon.OrderByDescending(j => j.Total).Take(10))
                            {
                                AddDataCell(table.Cell(),job.JobName);
                                AddDataCell(table.Cell(),job.Total.ToString("C0"));
                            }
                        });
                    }

                    // Top 10 lost jobs this month
                    if (monthLost.Count > 0)
                    {
                        col.Item().PaddingTop(10).Text("TOP LOST JOBS (THIS MONTH)").FontSize(11).Bold().FontColor(RedColor);
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(2.5f);
                                c.RelativeColumn(1.2f);
                                c.RelativeColumn(1.5f);
                                c.RelativeColumn(2f);
                            });
                            table.Header(header =>
                            {
                                AddHeaderCell(header.Cell(),"Job Name");
                                AddHeaderCell(header.Cell(),"Amount");
                                AddHeaderCell(header.Cell(),"Lost To");
                                AddHeaderCell(header.Cell(),"Reason");
                            });
                            foreach (var job in monthLost.OrderByDescending(j => j.Total).Take(10))
                            {
                                AddDataCell(table.Cell(),job.JobName);
                                AddDataCell(table.Cell(),job.Total.ToString("C0"));
                                AddDataCell(table.Cell(),job.LostTo ?? "");
                                AddDataCell(table.Cell(),job.LostReason ?? "");
                            }
                        });
                    }

                    // Margin distribution
                    col.Item().PaddingTop(10).Text("MARGIN DISTRIBUTION").FontSize(11).Bold().FontColor(AccentColor);

                    var topJobs = data.Profitability?.TopJobs ?? [];
                    var worstJobs = data.Profitability?.WorstJobs ?? [];
                    var allMarginJobs = topJobs.Concat(worstJobs).Where(j => j.Margin.HasValue).ToList();

                    if (allMarginJobs.Count > 0)
                    {
                        var under15 = allMarginJobs.Count(j => j.Margin < 0.15m);
                        var from15to30 = allMarginJobs.Count(j => j.Margin >= 0.15m && j.Margin < 0.30m);
                        var from30to50 = allMarginJobs.Count(j => j.Margin >= 0.30m && j.Margin < 0.50m);
                        var over50 = allMarginJobs.Count(j => j.Margin >= 0.50m);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(2);
                                c.RelativeColumn(1);
                            });
                            table.Header(header =>
                            {
                                AddHeaderCell(header.Cell(),"Margin Bracket");
                                AddHeaderCell(header.Cell(),"Count");
                            });

                            AddDataCell(table.Cell(),"< 15%"); AddDataCell(table.Cell(),under15.ToString());
                            AddDataCell(table.Cell(),"15% - 30%"); AddDataCell(table.Cell(),from15to30.ToString());
                            AddDataCell(table.Cell(),"30% - 50%"); AddDataCell(table.Cell(),from30to50.ToString());
                            AddDataCell(table.Cell(),"50%+"); AddDataCell(table.Cell(),over50.ToString());
                        });
                    }
                    else
                    {
                        col.Item().Text("No margin data available.").FontSize(9).FontColor(MutedText);
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Generated by MAC Estimator").FontSize(8).FontColor(MutedText);
                    text.Span("  |  ").FontSize(8).FontColor(MutedText);
                    text.Span(now.ToString("M/d/yyyy h:mm tt")).FontSize(8).FontColor(MutedText);
                });
            });
        })
        .GeneratePdf(outputPath);
    }

    // ================================================================
    // C. INDIVIDUAL BID SUMMARY
    // ================================================================

    public void GenerateBidSummary(string outputPath, Estimate estimate, decimal total, decimal winProbability)
    {
        var now = DateTime.Now;

        var grandTotal = estimate.Rooms
            .Sum(r => r.LineItems.Where(li => li.IsEnabled).Sum(li => li.LineTotal) * Math.Max(r.Multiplier, 1));

        var adjustmentAmount = estimate.AdjustmentPercent != 0
            ? grandTotal * estimate.AdjustmentPercent / 100m
            : estimate.AdjustmentDollar;
        var adjustedTotal = grandTotal + adjustmentAmount;

        var probColor = winProbability >= 0.5m ? GreenColor : winProbability >= 0.3m ? YellowColor : RedColor;

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.MarginHorizontal(0.6f, Unit.Inch);
                page.MarginVertical(0.5f, Unit.Inch);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Segoe UI"));

                page.Header().Column(col =>
                {
                    col.Item().Background(HeaderBg).Padding(16).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"BID SUMMARY").FontSize(18).Bold().FontColor(AccentColor);
                            c.Item().Text(estimate.JobName).FontSize(12).FontColor("#ffffff");
                        });
                        row.ConstantItem(200).AlignRight().AlignMiddle()
                            .Text(now.ToString("MMMM d, yyyy")).FontSize(10).FontColor("#aaaaaa");
                    });
                    col.Item().LineHorizontal(3).LineColor(AccentColor);
                });

                page.Content().PaddingTop(12).Column(col =>
                {
                    col.Spacing(6);

                    // Client info + Job details side by side
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Border(0.5f).BorderColor(LightBorder).Padding(10).Column(c =>
                        {
                            c.Item().Text("CLIENT INFORMATION").FontSize(10).Bold().FontColor(AccentColor);
                            c.Item().PaddingTop(4);
                            InfoLine(c, "Name", estimate.ClientName);
                            InfoLine(c, "Company", estimate.ClientCompany);
                            InfoLine(c, "Email", estimate.ClientEmail);
                            InfoLine(c, "Phone", estimate.ClientPhone);
                        });

                        row.ConstantItem(12);

                        row.RelativeItem().Border(0.5f).BorderColor(LightBorder).Padding(10).Column(c =>
                        {
                            c.Item().Text("JOB DETAILS").FontSize(10).Bold().FontColor(AccentColor);
                            c.Item().PaddingTop(4);
                            InfoLine(c, "Job Number", estimate.JobNumber);
                            InfoLine(c, "Grade", estimate.SelectedGrade);
                            InfoLine(c, "Estimator", estimate.SubmittedBy);
                        });
                    });

                    col.Item().PaddingTop(8);

                    // Room breakdowns
                    foreach (var room in estimate.Rooms)
                    {
                        var enabledItems = room.LineItems.Where(li => li.IsEnabled).ToList();
                        if (enabledItems.Count == 0) continue;

                        var multiplier = Math.Max(room.Multiplier, 1);
                        var roomTotal = enabledItems.Sum(li => li.LineTotal) * multiplier;
                        var roomLabel = multiplier > 1 ? $"{room.Name} (x{multiplier})" : room.Name;

                        col.Item().Background(TableHeaderBg).Padding(6).Row(row =>
                        {
                            row.RelativeItem().Text(roomLabel).FontSize(10).Bold().FontColor(AccentColor);
                            row.ConstantItem(100).AlignRight()
                                .Text(roomTotal.ToString("C2")).FontSize(10).Bold();
                        });

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(3f);   // Item
                                c.RelativeColumn(0.8f); // Qty
                                c.RelativeColumn(1f);   // Rate
                                c.RelativeColumn(1f);   // Total
                            });

                            table.Header(header =>
                            {
                                AddHeaderCell(header.Cell(),"Item");
                                AddHeaderCell(header.Cell(),"Qty");
                                AddHeaderCell(header.Cell(),"Rate");
                                AddHeaderCell(header.Cell(),"Total");
                            });

                            foreach (var item in enabledItems)
                            {
                                var displayName = item.Name;
                                if (!string.IsNullOrWhiteSpace(item.Note))
                                    displayName += $" ({item.Note})";

                                AddDataCell(table.Cell(),displayName);
                                AddDataCell(table.Cell(),item.Mode == PricingMode.PerUnit
                                    ? item.Quantity.ToString("0.##")
                                    : item.VendorCost.ToString("C0"));
                                AddDataCell(table.Cell(),item.Rate.ToString(item.Mode == PricingMode.PerUnit ? "C2" : "0.00x"));
                                AddDataCell(table.Cell(),item.LineTotal.ToString("C2"));
                            }
                        });

                        if (multiplier > 1)
                        {
                            col.Item().PaddingLeft(8).Text($"Room multiplier: x{multiplier}")
                                .FontSize(8).FontColor(MutedText);
                        }

                        col.Item().PaddingTop(4);
                    }

                    // Totals section
                    col.Item().PaddingTop(6).LineHorizontal(1).LineColor(AccentColor);
                    col.Item().PaddingTop(6);

                    if (adjustmentAmount != 0 && !string.IsNullOrWhiteSpace(estimate.AdjustmentLabel))
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem();
                            row.ConstantItem(250).AlignRight().Text(text =>
                            {
                                text.Span("Subtotal:  ").FontSize(10);
                                text.Span(grandTotal.ToString("C2")).FontSize(10).Bold();
                            });
                        });

                        var adjustColor = adjustmentAmount < 0 ? RedColor : GreenColor;
                        col.Item().Row(row =>
                        {
                            row.RelativeItem();
                            row.ConstantItem(250).AlignRight().Text(text =>
                            {
                                text.Span($"{estimate.AdjustmentLabel}:  ").FontSize(10).FontColor(adjustColor);
                                text.Span(adjustmentAmount.ToString("C2")).FontSize(10).Bold().FontColor(adjustColor);
                            });
                        });

                        col.Item().PaddingTop(4);
                    }

                    col.Item().Row(row =>
                    {
                        row.RelativeItem();
                        row.ConstantItem(250).AlignRight().Background(CardBg).Padding(8).Text(text =>
                        {
                            text.Span("GRAND TOTAL:  ").FontSize(12).Bold();
                            text.Span(adjustedTotal.ToString("C2")).FontSize(14).Bold().FontColor(AccentColor);
                        });
                    });

                    // Win probability indicator
                    col.Item().PaddingTop(10).Row(row =>
                    {
                        row.RelativeItem().Border(0.5f).BorderColor(LightBorder).Padding(10).Column(c =>
                        {
                            c.Item().Text("WIN PROBABILITY").FontSize(10).Bold().FontColor(AccentColor);
                            c.Item().PaddingTop(4).Row(r =>
                            {
                                r.ConstantItem(80).Height(30).Background(probColor)
                                    .AlignCenter().AlignMiddle()
                                    .Text(winProbability.ToString("P0"))
                                    .FontSize(14).Bold().FontColor("#ffffff");
                                r.ConstantItem(12);
                                r.RelativeItem().AlignMiddle().Text(
                                    winProbability >= 0.5m ? "Good chance of winning" :
                                    winProbability >= 0.3m ? "Competitive — may need follow-up" :
                                    "Tough odds — consider pricing strategy")
                                    .FontSize(9).FontColor(MutedText);
                            });
                        });
                    });

                    // Historical comparison
                    col.Item().PaddingTop(6).Border(0.5f).BorderColor(LightBorder).Padding(10).Column(c =>
                    {
                        c.Item().Text("HISTORICAL COMPARISON").FontSize(10).Bold().FontColor(AccentColor);
                        c.Item().PaddingTop(4);

                        // Find the matching bucket
                        var bucket = FindMatchingBucket(total);
                        if (bucket is not null)
                        {
                            c.Item().Text(
                                $"Similar sized bids ({bucket.MinAmount:C0}-{bucket.MaxAmount:C0} range): " +
                                $"{bucket.WinRate:P0} win rate based on {bucket.SampleSize} bids")
                                .FontSize(9);
                        }
                        else
                        {
                            c.Item().Text("No historical data available for this bid size range.")
                                .FontSize(9).FontColor(MutedText);
                        }
                    });
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Internal use only").FontSize(8).Bold().FontColor(RedColor);
                    text.Span(" - do not distribute  |  ").FontSize(8).FontColor(MutedText);
                    text.Span("Generated by MAC Estimator  |  ").FontSize(8).FontColor(MutedText);
                    text.Span(now.ToString("M/d/yyyy h:mm tt")).FontSize(8).FontColor(MutedText);
                });
            });
        })
        .GeneratePdf(outputPath);
    }

    // ================================================================
    // HELPERS
    // ================================================================

    // Keep a reference to loaded historical data for bucket lookup
    private static HistoricalData? _historicalData;

    public static void SetHistoricalData(HistoricalData? data) => _historicalData = data;

    private static WinProbBucket? FindMatchingBucket(decimal amount)
    {
        if (_historicalData?.WinProbability?.ByAmount is null) return null;
        return _historicalData.WinProbability.ByAmount
            .FirstOrDefault(b => amount >= b.MinAmount && amount < b.MaxAmount && b.SampleSize >= 3);
    }

    private static int DaysSinceAction(JobIndexEntry job)
    {
        var lastAction = job.FollowedUpAt ?? job.SubmittedAt ?? job.ModifiedAt;
        return (int)(DateTime.Now - lastAction).TotalDays;
    }

    private static void SummaryCard(RowDescriptor row, string label, string value, string color)
    {
        row.RelativeItem().Border(0.5f).BorderColor(LightBorder).Padding(8).Column(c =>
        {
            c.Item().Text(label).FontSize(7).FontColor(MutedText);
            c.Item().Text(value).FontSize(13).Bold().FontColor(color);
        });
    }

    private static void AddHeaderCell(IContainer container, string text)
    {
        container.Background(TableHeaderBg).Padding(4)
            .Text(text).FontSize(8).Bold().FontColor(AccentColor);
    }

    private static void AddDataCell(IContainer container, string text)
    {
        container.BorderBottom(0.5f).BorderColor(LightBorder)
            .Padding(4).AlignMiddle()
            .Text(text).FontSize(9);
    }

    private static void InfoLine(ColumnDescriptor col, string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        col.Item().Row(r =>
        {
            r.ConstantItem(70).Text(label + ":").FontSize(9).Bold().FontColor(MutedText);
            r.RelativeItem().Text(value).FontSize(9);
        });
    }
}
