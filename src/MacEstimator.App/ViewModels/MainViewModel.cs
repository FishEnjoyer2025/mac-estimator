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

    public MainViewModel(EstimateFileService fileService, PdfGenerator pdfGenerator)
    {
        _fileService = fileService;
        _pdfGenerator = pdfGenerator;
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
    private void NewEstimate()
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
            AdjustmentPercent = 0;
            AdjustmentLabel = string.Empty;
            Exclusions = Estimate.DefaultExclusions;
            Notes = string.Empty;
            SubmittedBy = "Rusty Philbrick";

            ClearRooms();

            var room = new RoomViewModel { RoomName = "Room 1" };
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

    [RelayCommand]
    private async Task OpenEstimate()
    {
        if (IsModified && !ConfirmDiscard())
            return;

        var dialog = new OpenFileDialog
        {
            Filter = "MAC Estimates (*.macest)|*.macest|All Files (*.*)|*.*",
            Title = "Open Estimate"
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
        if (_currentFilePath is null)
        {
            await SaveEstimateAs();
            return;
        }

        await SaveToFile(_currentFilePath);
    }

    [RelayCommand]
    private async Task SaveEstimateAs()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "MAC Estimates (*.macest)|*.macest",
            Title = "Save Estimate As",
            FileName = string.IsNullOrWhiteSpace(JobName) ? "Untitled" : JobName
        };

        if (dialog.ShowDialog() != true)
            return;

        await SaveToFile(dialog.FileName);
    }

    [RelayCommand]
    private void AddRoom()
    {
        var room = new RoomViewModel { RoomName = $"Room {Rooms.Count + 1}" };
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
            FileName = string.IsNullOrWhiteSpace(JobName) ? "Estimate" : JobName
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
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to save:\n\n{ex.Message}",
                "Save Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
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
