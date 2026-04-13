using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacEstimator.App.Models;
using MacEstimator.App.Services;

namespace MacEstimator.App.ViewModels;

public partial class InsightsViewModel : ObservableObject
{
    private readonly HistoricalDataService _historicalService;

    [ObservableProperty]
    private string _statusText = "Loading...";

    [ObservableProperty]
    private bool _isLoaded;

    // Summary stats
    [ObservableProperty]
    private int _totalBids;

    [ObservableProperty]
    private int _totalJobsCosted;

    [ObservableProperty]
    private string _overallWinRate = "-";

    [ObservableProperty]
    private string _avgProfitMargin = "-";

    [ObservableProperty]
    private string _dataDate = "-";

    [ObservableProperty]
    private string _refreshButtonText = "Refresh Data";

    [ObservableProperty]
    private bool _isNotRefreshing = true;

    // Win rates by year
    public ObservableCollection<YearRow> YearRows { get; } = [];

    // Estimator performance
    public ObservableCollection<EstimatorRow> EstimatorRows { get; } = [];

    // Top contractors
    public ObservableCollection<ContractorRow> ContractorRows { get; } = [];

    // Seasonal
    public ObservableCollection<SeasonalRow> SeasonalRows { get; } = [];

    // Top/worst jobs
    public ObservableCollection<JobRow> TopJobs { get; } = [];
    public ObservableCollection<JobRow> WorstJobs { get; } = [];

    public InsightsViewModel(HistoricalDataService historicalService)
    {
        _historicalService = historicalService;
    }

    [RelayCommand]
    public async Task RefreshData()
    {
        IsNotRefreshing = false;
        RefreshButtonText = "Running...";
        StatusText = "Running extract_bid_data.py...";

        try
        {
            var scriptPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "tools", "extract_bid_data.py");
            // Also try relative to exe for published builds
            if (!System.IO.File.Exists(scriptPath))
                scriptPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "extract_bid_data.py");
            // Fallback: use known repo path
            if (!System.IO.File.Exists(scriptPath))
                scriptPath = @"C:\Users\Dylan\mac-estimator\tools\extract_bid_data.py";

            var psi = new ProcessStartInfo
            {
                FileName = "python3",
                Arguments = $"\"{scriptPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            // Try python3 first, fall back to python
            try
            {
                using var proc = Process.Start(psi);
                if (proc is not null)
                {
                    await proc.WaitForExitAsync();
                }
            }
            catch
            {
                psi.FileName = "python";
                using var proc = Process.Start(psi);
                if (proc is not null)
                {
                    await proc.WaitForExitAsync();
                }
            }

            // Reload data
            _historicalService.ClearCache();
            IsLoaded = false;
            YearRows.Clear();
            EstimatorRows.Clear();
            ContractorRows.Clear();
            SeasonalRows.Clear();
            TopJobs.Clear();
            WorstJobs.Clear();
            await LoadData();
            StatusText = $"Data refreshed at {DateTime.Now:HH:mm} | " + StatusText;
        }
        catch (Exception ex)
        {
            StatusText = $"Refresh failed: {ex.Message}";
        }
        finally
        {
            IsNotRefreshing = true;
            RefreshButtonText = "Refresh Data";
        }
    }

    [RelayCommand]
    public async Task LoadData()
    {
        if (IsLoaded) return;

        var data = await _historicalService.LoadAsync();
        if (data is null)
        {
            StatusText = "No historical data found. Run tools/extract_bid_data.py first.";
            return;
        }

        TotalBids = data.Summary.TotalBidsTracked;
        TotalJobsCosted = data.Summary.TotalJobsCosted;
        OverallWinRate = $"{data.Summary.OverallWinRate:P0}";
        AvgProfitMargin = data.Summary.AvgProfitMargin.HasValue
            ? $"{data.Summary.AvgProfitMargin.Value:P0}" : "-";
        DataDate = data.Generated;

        // Year rows
        foreach (var (year, wr) in data.WinRates.ByYear.OrderByDescending(x => x.Key))
        {
            YearRows.Add(new YearRow
            {
                Year = year,
                Won = wr.Won,
                Lost = wr.Lost,
                WinRate = $"{wr.Rate:P0}",
                Revenue = $"{wr.Revenue:C0}"
            });
        }

        // Estimator rows — merge cost data (margin) with bid data (win rate)
        var allEstimators = new HashSet<string>();
        foreach (var k in data.Profitability.ByEstimator.Keys) allEstimators.Add(k);
        foreach (var k in data.Profitability.BidEstimators.Keys) allEstimators.Add(k);

        foreach (var name in allEstimators.OrderByDescending(n =>
            data.Profitability.BidEstimators.TryGetValue(n, out var bs) ? bs.Bids : 0))
        {
            var hasCost = data.Profitability.ByEstimator.TryGetValue(name, out var margin);
            var hasBid = data.Profitability.BidEstimators.TryGetValue(name, out var bidStats);

            EstimatorRows.Add(new EstimatorRow
            {
                Name = name,
                AvgMargin = hasCost ? $"{margin!.Avg:P0}" : "-",
                JobCount = hasCost ? margin!.Count : 0,
                Bids = hasBid ? bidStats!.Bids : 0,
                Won = hasBid ? bidStats!.Won : 0,
                WinRate = hasBid ? $"{bidStats!.WinRate:P0}" : "-",
                RevenueWon = hasBid ? $"{bidStats!.RevenueWon:C0}" : "-"
            });
        }

        // Contractor rows
        foreach (var c in data.Contractors.Where(x => x.Revenue > 0).Take(20))
        {
            ContractorRows.Add(new ContractorRow
            {
                Name = c.Name,
                Jobs = c.Jobs,
                Revenue = $"{c.Revenue:C0}"
            });
        }

        // Seasonal rows
        var monthNames = new[] { "", "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
        foreach (var (month, entry) in data.Seasonal.OrderBy(x => int.Parse(x.Key)))
        {
            var m = int.Parse(month);
            SeasonalRows.Add(new SeasonalRow
            {
                Month = m <= 12 ? monthNames[m] : month,
                BidCount = entry.Count,
                TotalValue = $"{entry.TotalValue:C0}"
            });
        }

        // Top/worst jobs — filtered to recent cabinets, sorted by profit $
        foreach (var j in data.Profitability.TopJobs.Take(10))
        {
            TopJobs.Add(new JobRow
            {
                Job = j.Job,
                Name = j.Name,
                Margin = $"{j.Margin:P0}",
                Revenue = $"{j.Revenue:C0}",
                Profit = $"{j.Profit:C0}"
            });
        }

        foreach (var j in data.Profitability.WorstJobs.Take(10))
        {
            WorstJobs.Add(new JobRow
            {
                Job = j.Job,
                Name = j.Name,
                Margin = $"{j.Margin:P0}",
                Revenue = $"{j.Revenue:C0}",
                Profit = $"{j.Profit:C0}"
            });
        }

        IsLoaded = true;
        StatusText = $"Data from {data.Generated} | {TotalBids} bids, {TotalJobsCosted} costed jobs";
    }

    // Row types for DataGrid binding
    public class YearRow
    {
        public string Year { get; set; } = "";
        public int Won { get; set; }
        public int Lost { get; set; }
        public string WinRate { get; set; } = "";
        public string Revenue { get; set; } = "";
    }

    public class EstimatorRow
    {
        public string Name { get; set; } = "";
        public string AvgMargin { get; set; } = "";
        public int JobCount { get; set; }
        public int Bids { get; set; }
        public int Won { get; set; }
        public string WinRate { get; set; } = "";
        public string RevenueWon { get; set; } = "";
    }

    public class ContractorRow
    {
        public string Name { get; set; } = "";
        public int Jobs { get; set; }
        public string Revenue { get; set; } = "";
    }

    public class SeasonalRow
    {
        public string Month { get; set; } = "";
        public int BidCount { get; set; }
        public string TotalValue { get; set; } = "";
    }

    public class JobRow
    {
        public string Job { get; set; } = "";
        public string Name { get; set; } = "";
        public string Margin { get; set; } = "";
        public string Revenue { get; set; } = "";
        public string Profit { get; set; } = "";
    }
}
