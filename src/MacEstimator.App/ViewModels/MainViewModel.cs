using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacEstimator.App.Models;
using MacEstimator.App.Services;
using Microsoft.Win32;

namespace MacEstimator.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly EstimateFileService _fileService;
    private readonly PdfGenerator _pdfGenerator;
    private readonly JobIndexService _jobIndexService;
    private readonly ConfigService _configService;
    private string? _currentFilePath;
    private bool _suppressModified;

    // Job info
    [ObservableProperty]
    private string _jobName = string.Empty;

    [ObservableProperty]
    private string _jobNumber = string.Empty;

    // Client info
    [ObservableProperty]
    private string _clientName = string.Empty;

    [ObservableProperty]
    private string _clientCompany = string.Empty;

    [ObservableProperty]
    private string _clientEmail = string.Empty;

    [ObservableProperty]
    private string _clientPhone = string.Empty;

    [ObservableProperty]
    private string _clientAddress = string.Empty;

    // Grade selector
    [ObservableProperty]
    private string _selectedGrade = "PLAM";

    public string[] GradeOptions { get; } = ["PLAM", "Paint Grade", "Stain Grade"];

    partial void OnSelectedGradeChanged(string value)
    {
        // Apply grade to all line items across all rooms
        foreach (var room in Rooms)
            foreach (var item in room.LineItems)
                item.SetGrade(value);
    }

    // Adjustments
    [ObservableProperty]
    private decimal _adjustmentPercent;

    [ObservableProperty]
    private string _adjustmentLabel = string.Empty;

    // Footer
    [ObservableProperty]
    private string _exclusions = Estimate.DefaultExclusions;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private string _submittedBy = "Rusty Philbrick";

    // State
    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private bool _isModified;

    public string[] SubmittedByOptions { get; } = ["Rusty Philbrick", "Josh Irsik"];

    public ObservableCollection<RoomViewModel> Rooms { get; } = [];

    public decimal GrandTotal => Rooms.Sum(r => r.RoomTotal);

    public decimal AdjustmentAmount => GrandTotal * AdjustmentPercent / 100m;

    public decimal AdjustedTotal => GrandTotal + AdjustmentAmount;

    public bool HasAdjustment => AdjustmentPercent != 0;

    public string WindowTitle
    {
        get
        {
            var name = string.IsNullOrEmpty(_currentFilePath)
                ? "Untitled"
                : System.IO.Path.GetFileNameWithoutExtension(_currentFilePath);
            return $"MAC Estimator - {name}{(IsModified ? " *" : "")}";
        }
    }

    public MainViewModel(EstimateFileService fileService, PdfGenerator pdfGenerator, JobIndexService jobIndexService, ConfigService configService)
    {
        _fileService = fileService;
        _pdfGenerator = pdfGenerator;
        _jobIndexService = jobIndexService;
        _configService = configService;
        Rooms.CollectionChanged += OnRoomsChanged;

        // Track modifications on all text properties
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not (nameof(StatusText) or nameof(IsModified) or nameof(WindowTitle)
                or nameof(GrandTotal) or nameof(AdjustmentAmount) or nameof(AdjustedTotal) or nameof(HasAdjustment)))
                MarkModified();
        };
    }

    partial void OnIsModifiedChanged(bool value) => OnPropertyChanged(nameof(WindowTitle));

    partial void OnAdjustmentPercentChanged(decimal value)
    {
        OnPropertyChanged(nameof(AdjustmentAmount));
        OnPropertyChanged(nameof(AdjustedTotal));
        OnPropertyChanged(nameof(HasAdjustment));
    }

    [RelayCommand]
    private async Task NewEstimate()
    {
        if (IsModified && !ConfirmDiscard())
            return;

        _suppressModified = true;
        try
        {
            _currentFilePath = null;
            JobName = string.Empty;
            JobNumber = string.Empty;
            ClientName = string.Empty;
            ClientCompany = string.Empty;
            ClientEmail = string.Empty;
            ClientPhone = string.Empty;
            ClientAddress = string.Empty;
            SelectedGrade = "PLAM";
            AdjustmentPercent = 0;
            AdjustmentLabel = string.Empty;
            Exclusions = Estimate.DefaultExclusions;
            Notes = string.Empty;
            SubmittedBy = "Rusty Philbrick";

            ClearRooms();

            var room = new RoomViewModel { RoomName = "Room 1" };

            // Use config-based defaults if available
            var config = await _configService.LoadAsync();
            var templates = ConfigService.ToTemplates(config);
            if (templates.Length > 0)
                room.PopulateFromConfig(templates);
            else
                room.PopulateDefaults();

            AddRoomInternal(room);
        }
        finally
        {
            _suppressModified = false;
        }

        IsModified = false;
        StatusText = "New estimate created";
    }

    private string BuildFileName()
    {
        var num = JobNumber?.Trim() ?? "";
        var name = JobName?.Trim() ?? "";
        if (!string.IsNullOrEmpty(num) && !string.IsNullOrEmpty(name))
            return $"{num} {name}";
        if (!string.IsNullOrEmpty(num))
            return num;
        if (!string.IsNullOrEmpty(name))
            return name;
        return "Untitled";
    }

    [RelayCommand]
    private async Task OpenEstimate()
    {
        if (IsModified && !ConfirmDiscard())
            return;

        var dialog = new OpenFileDialog
        {
            Filter = "MAC Estimates (*.macest)|*.macest|All Files (*.*)|*.*",
            Title = "Open Estimate",
            InitialDirectory = JobIndexService.SharedFolder
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var estimate = await _fileService.LoadAsync(dialog.FileName);
            LoadFromModel(estimate);
            _currentFilePath = dialog.FileName;
            IsModified = false;
            StatusText = $"Opened: {System.IO.Path.GetFileName(dialog.FileName)}";
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to open file:\n\n{ex.Message}",
                "Open Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task SaveEstimate()
    {
        if (_currentFilePath is not null)
        {
            await SaveToFile(_currentFilePath);
            return;
        }

        // Auto-save to shared folder with standard naming
        var fileName = BuildFileName() + ".macest";
        var path = System.IO.Path.Combine(JobIndexService.SharedFolder, fileName);
        await SaveToFile(path);
    }

    [RelayCommand]
    private async Task SaveEstimateAs()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "MAC Estimates (*.macest)|*.macest",
            Title = "Save Estimate As",
            InitialDirectory = JobIndexService.SharedFolder,
            FileName = BuildFileName()
        };

        if (dialog.ShowDialog() != true)
            return;

        await SaveToFile(dialog.FileName);
    }

    [RelayCommand]
    private async Task AddRoom()
    {
        var room = new RoomViewModel { RoomName = $"Room {Rooms.Count + 1}" };

        var config = await _configService.LoadAsync();
        var templates = ConfigService.ToTemplates(config);
        if (templates.Length > 0)
            room.PopulateFromConfig(templates);
        else
            room.PopulateDefaults();

        AddRoomInternal(room);
        MarkModified();
    }

    [RelayCommand]
    private void RemoveRoom(RoomViewModel room)
    {
        if (Rooms.Count <= 1)
        {
            System.Windows.MessageBox.Show("Cannot remove the last room.",
                "Remove Room", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            return;
        }

        var result = System.Windows.MessageBox.Show(
            $"Remove \"{room.RoomName}\" and all its items?",
            "Confirm Remove",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result != System.Windows.MessageBoxResult.Yes)
            return;

        room.PropertyChanged -= OnRoomPropertyChanged;
        Rooms.Remove(room);
        RefreshTotals();
        MarkModified();
    }

    [RelayCommand]
    private void DuplicateRoom(RoomViewModel room)
    {
        var model = room.ToModel();
        model.Name += " (Copy)";
        var copy = new RoomViewModel(model) { IsExpanded = true };
        AddRoomInternal(copy);
        MarkModified();
    }

    [RelayCommand]
    private void MoveRoomUp(RoomViewModel room)
    {
        var index = Rooms.IndexOf(room);
        if (index > 0)
        {
            Rooms.Move(index, index - 1);
            MarkModified();
        }
    }

    [RelayCommand]
    private void MoveRoomDown(RoomViewModel room)
    {
        var index = Rooms.IndexOf(room);
        if (index >= 0 && index < Rooms.Count - 1)
        {
            Rooms.Move(index, index + 1);
            MarkModified();
        }
    }

    [RelayCommand]
    private async Task GeneratePdf()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "PDF Files (*.pdf)|*.pdf",
            Title = "Generate PDF Estimate",
            InitialDirectory = JobIndexService.SharedFolder,
            FileName = BuildFileName()
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var estimate = ToModel();
            await Task.Run(() => _pdfGenerator.Generate(estimate, dialog.FileName));
            StatusText = $"PDF generated: {System.IO.Path.GetFileName(dialog.FileName)}";

            // Open the PDF in default viewer
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = dialog.FileName,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to generate PDF:\n\n{ex.Message}",
                "PDF Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task ShowSettings()
    {
        var config = await _configService.LoadAsync();
        var window = new SettingsWindow(_configService, config);
        window.Owner = System.Windows.Application.Current.MainWindow;
        window.ShowDialog();

        if (window.WasSaved)
        {
            _configService.InvalidateCache();
            StatusText = "Settings saved — new estimates will use updated rates";
        }
    }

    public bool ConfirmDiscard()
    {
        var result = System.Windows.MessageBox.Show(
            "You have unsaved changes. Discard them?",
            "Unsaved Changes",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        return result == System.Windows.MessageBoxResult.Yes;
    }

    private void MarkModified()
    {
        if (!_suppressModified && !IsModified)
            IsModified = true;
    }

    private void RefreshTotals()
    {
        OnPropertyChanged(nameof(GrandTotal));
        OnPropertyChanged(nameof(AdjustmentAmount));
        OnPropertyChanged(nameof(AdjustedTotal));
    }

    private async Task SaveToFile(string path)
    {
        try
        {
            var estimate = ToModel();
            await _fileService.SaveAsync(estimate, path);
            _currentFilePath = path;
            IsModified = false;
            StatusText = $"Saved: {System.IO.Path.GetFileName(path)}";
            OnPropertyChanged(nameof(WindowTitle));

            // Register in shared job index
            await _jobIndexService.RegisterJobAsync(estimate, path, AdjustedTotal);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to save:\n\n{ex.Message}",
                "Save Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task ShowJobHistory()
    {
        var entries = await _jobIndexService.LoadEntriesAsync();
        if (entries.Count == 0)
        {
            System.Windows.MessageBox.Show("No jobs found in the shared history.",
                "Job History", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            return;
        }

        var jobList = new System.Text.StringBuilder();
        jobList.AppendLine("Select a job number to open:\n");
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            var name = string.IsNullOrWhiteSpace(e.JobName) ? "(Untitled)" : e.JobName;
            var num = string.IsNullOrWhiteSpace(e.JobNumber) ? "" : $" #{e.JobNumber}";
            jobList.AppendLine($"  {i + 1}. {name}{num} — {e.ClientCompany} — {e.Total:C2} ({e.SubmittedBy}, {e.ModifiedAt:M/d/yyyy})");
        }

        // Use a simple dialog for now — show the list and let them open via file dialog
        // A proper ListView dialog would be better but this works for MVP
        var window = new JobHistoryWindow(entries, async (entry) =>
        {
            if (!System.IO.File.Exists(entry.FilePath))
            {
                System.Windows.MessageBox.Show($"File not found:\n{entry.FilePath}",
                    "File Missing", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (IsModified && !ConfirmDiscard())
                return;

            try
            {
                var estimate = await _fileService.LoadAsync(entry.FilePath);
                LoadFromModel(estimate);
                _currentFilePath = entry.FilePath;
                IsModified = false;
                StatusText = $"Opened: {System.IO.Path.GetFileName(entry.FilePath)}";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to open:\n\n{ex.Message}",
                    "Open Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        });
        window.Owner = System.Windows.Application.Current.MainWindow;
        window.ShowDialog();
    }

    private void AddRoomInternal(RoomViewModel room)
    {
        room.PropertyChanged += OnRoomPropertyChanged;
        Rooms.Add(room);
    }

    private void ClearRooms()
    {
        foreach (var room in Rooms)
            room.PropertyChanged -= OnRoomPropertyChanged;
        Rooms.Clear();
    }

    private void OnRoomPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RoomViewModel.RoomTotal))
        {
            RefreshTotals();
            MarkModified();
        }
    }

    private void OnRoomsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshTotals();
    }

    private Estimate ToModel() => new()
    {
        JobName = JobName,
        JobNumber = JobNumber,
        ClientName = ClientName,
        ClientCompany = ClientCompany,
        ClientEmail = ClientEmail,
        ClientPhone = ClientPhone,
        ClientAddress = ClientAddress,
        SelectedGrade = SelectedGrade,
        AdjustmentPercent = AdjustmentPercent,
        AdjustmentLabel = AdjustmentLabel,
        Exclusions = Exclusions,
        Notes = Notes,
        SubmittedBy = SubmittedBy,
        Rooms = Rooms.Select(r => r.ToModel()).ToList()
    };

    private void LoadFromModel(Estimate estimate)
    {
        _suppressModified = true;
        try
        {
            JobName = estimate.JobName;
            JobNumber = estimate.JobNumber;
            ClientName = estimate.ClientName;
            ClientCompany = estimate.ClientCompany;
            ClientEmail = estimate.ClientEmail;
            ClientPhone = estimate.ClientPhone;
            ClientAddress = estimate.ClientAddress;
            SelectedGrade = estimate.SelectedGrade;
            AdjustmentPercent = estimate.AdjustmentPercent;
            AdjustmentLabel = estimate.AdjustmentLabel;
            Exclusions = estimate.Exclusions;
            Notes = estimate.Notes;
            SubmittedBy = estimate.SubmittedBy;

            ClearRooms();
            foreach (var room in estimate.Rooms)
                AddRoomInternal(new RoomViewModel(room));
        }
        finally
        {
            _suppressModified = false;
        }
    }
}
