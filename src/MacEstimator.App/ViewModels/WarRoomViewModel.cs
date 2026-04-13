using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacEstimator.App.Models;
using MacEstimator.App.Services;

namespace MacEstimator.App.ViewModels;

public partial class WarRoomViewModel : ObservableObject
{
    private readonly JobIndexService _jobIndexService;
    private readonly HistoricalDataService _historicalService;

    /// <summary>Set by MainWindow to allow opening estimates from War Room cards.</summary>
    public Action<string>? OpenEstimateCallback { get; set; }

    [ObservableProperty]
    private bool _isLoaded;

    [ObservableProperty]
    private string _statusText = "Loading...";

    // Pipeline columns
    public ObservableCollection<BidCardViewModel> Drafts { get; } = [];
    public ObservableCollection<BidCardViewModel> Submitted { get; } = [];
    public ObservableCollection<BidCardViewModel> FollowedUp { get; } = [];
    public ObservableCollection<BidCardViewModel> Won { get; } = [];
    public ObservableCollection<BidCardViewModel> Lost { get; } = [];

    // Summary stats
    [ObservableProperty] private int _totalActive;
    [ObservableProperty] private string _totalPipelineValue = "$0";
    [ObservableProperty] private int _needsFollowUp;
    [ObservableProperty] private string _winRateText = "-";
    [ObservableProperty] private int _totalWon;
    [ObservableProperty] private int _totalLost;

    public WarRoomViewModel(JobIndexService jobIndexService, HistoricalDataService historicalService)
    {
        _jobIndexService = jobIndexService;
        _historicalService = historicalService;
    }

    [RelayCommand]
    public async Task LoadData()
    {
        try
        {
        // Auto-register any .macest files in the shared folder not already indexed
        await ScanEstimatorFolder();

        var entries = await _jobIndexService.LoadEntriesAsync();

        Drafts.Clear();
        Submitted.Clear();
        FollowedUp.Clear();
        Won.Clear();
        Lost.Clear();

        int followUpCount = 0;
        decimal pipelineValue = 0;
        int wonCount = 0, resolvedCount = 0;

        // Count all won/lost for stats but only render recent cards
        int wonTotal = 0, lostTotal = 0;

        foreach (var entry in entries.OrderByDescending(e => e.ModifiedAt))
        {
            switch (entry.Status)
            {
                case BidStatus.Draft:
                    Drafts.Add(new BidCardViewModel(entry, _historicalService, this));
                    pipelineValue += entry.Total;
                    break;
                case BidStatus.Submitted:
                    var subCard = new BidCardViewModel(entry, _historicalService, this);
                    Submitted.Add(subCard);
                    pipelineValue += entry.Total;
                    if (subCard.DaysSinceSubmitted >= 7) followUpCount++;
                    break;
                case BidStatus.FollowedUp:
                    FollowedUp.Add(new BidCardViewModel(entry, _historicalService, this));
                    pipelineValue += entry.Total;
                    break;
                case BidStatus.Won:
                    wonCount++;
                    resolvedCount++;
                    wonTotal++;
                    if (Won.Count < 10)
                        Won.Add(new BidCardViewModel(entry, _historicalService, this));
                    break;
                case BidStatus.Lost:
                case BidStatus.Declined:
                    resolvedCount++;
                    lostTotal++;
                    if (Lost.Count < 10)
                        Lost.Add(new BidCardViewModel(entry, _historicalService, this));
                    break;
            }
        }

        TotalActive = Drafts.Count + Submitted.Count + FollowedUp.Count;
        TotalPipelineValue = pipelineValue.ToString("C0");
        NeedsFollowUp = followUpCount;
        TotalWon = wonTotal;
        TotalLost = lostTotal;
        WinRateText = resolvedCount > 0 ? $"{wonCount}/{resolvedCount} ({(double)wonCount / resolvedCount:P0})" : "-";

        IsLoaded = true;
        StatusText = $"{entries.Count} bids tracked | {TotalActive} active | {NeedsFollowUp} need follow-up";
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading: {ex.Message}";
        }
    }

    /// <summary>Show a simple WPF dialog to capture lost reason data.</summary>
    private static bool ShowLostReasonDialog(JobIndexEntry entry)
    {
        var win = new Window
        {
            Title = "Lost Reason",
            Width = 380,
            Height = 320,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Application.Current.MainWindow,
            ResizeMode = ResizeMode.NoResize,
            Background = Application.Current.FindResource("BackgroundDark") as System.Windows.Media.Brush
                         ?? System.Windows.Media.Brushes.Black,
        };

        var stack = new StackPanel { Margin = new Thickness(16) };
        var fg = Application.Current.FindResource("ForegroundPrimary") as System.Windows.Media.Brush
                 ?? System.Windows.Media.Brushes.White;

        stack.Children.Add(new TextBlock { Text = "Lost to (competitor):", Foreground = fg, Margin = new Thickness(0, 0, 0, 4) });
        var lostToBox = new TextBox { Margin = new Thickness(0, 0, 0, 10) };
        stack.Children.Add(lostToBox);

        stack.Children.Add(new TextBlock { Text = "Their price (optional):", Foreground = fg, Margin = new Thickness(0, 0, 0, 4) });
        var priceBox = new TextBox { Margin = new Thickness(0, 0, 0, 10) };
        stack.Children.Add(priceBox);

        stack.Children.Add(new TextBlock { Text = "Reason:", Foreground = fg, Margin = new Thickness(0, 0, 0, 4) });
        var reasonCombo = new ComboBox { Margin = new Thickness(0, 0, 0, 16) };
        foreach (var r in new[] { "Price", "Scope", "Timing", "Relationship", "Declined to Bid", "Other" })
            reasonCombo.Items.Add(r);
        reasonCombo.SelectedIndex = 0;
        stack.Children.Add(reasonCombo);

        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var okBtn = new Button { Content = "Save", Width = 80, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        var cancelBtn = new Button { Content = "Cancel", Width = 80, IsCancel = true };
        btnPanel.Children.Add(okBtn);
        btnPanel.Children.Add(cancelBtn);
        stack.Children.Add(btnPanel);

        okBtn.Click += (_, _) => { win.DialogResult = true; };
        win.Content = stack;

        if (win.ShowDialog() != true)
            return false;

        entry.LostTo = string.IsNullOrWhiteSpace(lostToBox.Text) ? null : lostToBox.Text.Trim();
        if (decimal.TryParse(priceBox.Text.Replace("$", "").Replace(",", ""), out var price))
            entry.CompetitorPrice = price;
        entry.LostReason = reasonCombo.SelectedItem?.ToString();
        return true;
    }

    public async Task UpdateStatus(BidCardViewModel card, BidStatus newStatus)
    {
        // Show lost reason dialog when marking as Lost
        if (newStatus == BidStatus.Lost)
        {
            if (!ShowLostReasonDialog(card.Entry))
                return; // user cancelled
        }

        card.Entry.Status = newStatus;
        switch (newStatus)
        {
            case BidStatus.Submitted:
                card.Entry.SubmittedAt ??= DateTime.Now;
                break;
            case BidStatus.FollowedUp:
                card.Entry.FollowedUpAt = DateTime.Now;
                break;
            case BidStatus.Won:
            case BidStatus.Lost:
            case BidStatus.Declined:
                card.Entry.ResolvedAt = DateTime.Now;
                break;
        }

        // Save to disk
        var entries = await _jobIndexService.LoadEntriesAsync();
        var existing = entries.FirstOrDefault(e => e.Id == card.Entry.Id);
        if (existing != null)
        {
            existing.Status = card.Entry.Status;
            existing.SubmittedAt = card.Entry.SubmittedAt;
            existing.FollowedUpAt = card.Entry.FollowedUpAt;
            existing.ResolvedAt = card.Entry.ResolvedAt;
            existing.Notes = card.Entry.Notes;
        }

        // Use reflection to save (JobIndexService.SaveEntriesAsync is private)
        // Workaround: re-register the job to trigger a save
        await _jobIndexService.RegisterJobAsync(
            new Estimate { Id = card.Entry.Id, JobName = card.Entry.JobName, JobNumber = card.Entry.JobNumber,
                ClientName = card.Entry.ClientName, ClientCompany = card.Entry.ClientCompany,
                SubmittedBy = card.Entry.SubmittedBy },
            card.Entry.FilePath, card.Entry.Total);

        // Update the status fields that RegisterJobAsync doesn't touch
        entries = await _jobIndexService.LoadEntriesAsync();
        existing = entries.FirstOrDefault(e => e.Id == card.Entry.Id);
        if (existing != null)
        {
            existing.Status = card.Entry.Status;
            existing.SubmittedAt = card.Entry.SubmittedAt;
            existing.FollowedUpAt = card.Entry.FollowedUpAt;
            existing.ResolvedAt = card.Entry.ResolvedAt;
            existing.Notes = card.Entry.Notes;
            existing.LostTo = card.Entry.LostTo;
            existing.CompetitorPrice = card.Entry.CompetitorPrice;
            existing.LostReason = card.Entry.LostReason;
            await SaveEntriesDirectAsync(entries);
        }

        // Refresh the board
        await LoadData();
    }

    private async Task ScanEstimatorFolder()
    {
        try
        {
            var folder = JobIndexService.SharedFolder;
            if (!System.IO.Directory.Exists(folder)) return;

            var entries = await _jobIndexService.LoadEntriesAsync();
            var knownPaths = new HashSet<string>(entries.Select(e => e.FilePath), StringComparer.OrdinalIgnoreCase);
            bool added = false;

            foreach (var file in System.IO.Directory.GetFiles(folder, "*.macest"))
            {
                if (knownPaths.Contains(file)) continue;

                // Try to read basic info from the file
                try
                {
                    var json = await System.IO.File.ReadAllTextAsync(file);
                    var estimate = System.Text.Json.JsonSerializer.Deserialize<Models.Estimate>(json, new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    if (estimate == null) continue;

                    var total = estimate.Rooms.Sum(r =>
                        r.LineItems.Where(li => li.IsEnabled).Sum(li => li.LineTotal) * Math.Max(r.Multiplier, 1));

                    entries.Add(new JobIndexEntry
                    {
                        Id = estimate.Id,
                        JobName = estimate.JobName,
                        JobNumber = estimate.JobNumber,
                        ClientName = estimate.ClientName,
                        ClientCompany = estimate.ClientCompany,
                        SubmittedBy = estimate.SubmittedBy,
                        Total = total,
                        CreatedAt = estimate.CreatedAt,
                        ModifiedAt = estimate.ModifiedAt,
                        FilePath = file,
                        Status = BidStatus.Draft
                    });
                    added = true;
                }
                catch { }
            }

            if (added)
                await SaveEntriesDirectAsync(entries);
        }
        catch { }
    }

    private async Task SaveEntriesDirectAsync(List<JobIndexEntry> entries)
    {
        var path = System.IO.Path.Combine(JobIndexService.SharedFolder, "jobs.json");
        var tempPath = path + ".tmp";
        try
        {
            System.IO.Directory.CreateDirectory(JobIndexService.SharedFolder);
            await using var stream = System.IO.File.Open(tempPath, System.IO.FileMode.Create);
            await System.Text.Json.JsonSerializer.SerializeAsync(stream, entries, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            });
            await stream.FlushAsync();
            stream.Close();
            System.IO.File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            if (System.IO.File.Exists(tempPath))
                try { System.IO.File.Delete(tempPath); } catch { }
        }
    }
}

public partial class BidCardViewModel : ObservableObject
{
    public JobIndexEntry Entry { get; }
    private readonly HistoricalDataService _historicalService;
    private readonly WarRoomViewModel _parent;

    public string JobName => string.IsNullOrEmpty(Entry.JobName) ? "(Untitled)" : Entry.JobName;
    public string JobNumber => Entry.JobNumber;
    public string ClientCompany => Entry.ClientCompany;
    public string SubmittedBy => Entry.SubmittedBy;
    public string Total => Entry.Total.ToString("C0");
    public string CreatedDate => Entry.CreatedAt.ToString("M/d");

    public string WinProbability
    {
        get
        {
            var prob = _historicalService.GetWinProbability(Entry.Total, Entry.SubmittedBy);
            return $"{prob:P0}";
        }
    }

    public string WinProbColor
    {
        get
        {
            var prob = _historicalService.GetWinProbability(Entry.Total, Entry.SubmittedBy);
            return prob >= 0.5m ? "#13a10e" : prob >= 0.3m ? "#ffc83d" : "#d13438";
        }
    }

    public int DaysSinceSubmitted => Entry.SubmittedAt.HasValue
        ? (int)(DateTime.Now - Entry.SubmittedAt.Value).TotalDays
        : (int)(DateTime.Now - Entry.CreatedAt).TotalDays;

    public string AgingText
    {
        get
        {
            var days = DaysSinceSubmitted;
            if (days == 0) return "Today";
            if (days == 1) return "Yesterday";
            return $"{days}d ago";
        }
    }

    public bool IsOverdue => Entry.Status == BidStatus.Submitted && DaysSinceSubmitted >= 7;

    public string StatusIcon => Entry.Status switch
    {
        BidStatus.Draft => "DRAFT",
        BidStatus.Submitted => IsOverdue ? "FOLLOW UP!" : "SENT",
        BidStatus.FollowedUp => "FOLLOWED UP",
        BidStatus.Won => "WON",
        BidStatus.Lost => "LOST",
        BidStatus.Declined => "DECLINED",
        _ => ""
    };

    public BidCardViewModel(JobIndexEntry entry, HistoricalDataService historicalService, WarRoomViewModel parent)
    {
        Entry = entry;
        _historicalService = historicalService;
        _parent = parent;
    }

    [RelayCommand]
    private async Task MarkSubmitted() => await _parent.UpdateStatus(this, BidStatus.Submitted);

    [RelayCommand]
    private async Task MarkFollowedUp() => await _parent.UpdateStatus(this, BidStatus.FollowedUp);

    [RelayCommand]
    private async Task MarkWon() => await _parent.UpdateStatus(this, BidStatus.Won);

    [RelayCommand]
    private async Task MarkLost() => await _parent.UpdateStatus(this, BidStatus.Lost);

    [RelayCommand]
    private void OpenEstimate()
    {
        if (!string.IsNullOrEmpty(Entry.FilePath) && System.IO.File.Exists(Entry.FilePath))
            _parent.OpenEstimateCallback?.Invoke(Entry.FilePath);
    }
}
