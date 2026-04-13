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
    private readonly ReportGenerator _reportGenerator;
    private readonly JobIndexService _jobIndexService;
    private readonly ConfigService _configService;
    private readonly HistoricalDataService _historicalService;
    private readonly PricingConfigService _pricingConfigService;
    private readonly PdfTextExtractor _pdfTextExtractor;
    private readonly PlanReaderService _planReaderService;
    private readonly GeminiService _geminiService;
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
    private decimal _adjustmentValue;

    [ObservableProperty]
    private string _adjustmentMode = "%";

    [ObservableProperty]
    private string _adjustmentLabel = string.Empty;

    public string[] AdjustmentModeOptions { get; } = ["%", "$"];

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

    public decimal AdjustmentAmount => AdjustmentMode == "%"
        ? GrandTotal * AdjustmentValue / 100m
        : AdjustmentValue;

    public decimal AdjustedTotal => GrandTotal + AdjustmentAmount;

    public bool HasAdjustment => AdjustmentValue != 0;

    // Win probability — updates when grand total changes
    public string WinProbabilityText
    {
        get
        {
            var total = AdjustedTotal > 0 ? AdjustedTotal : GrandTotal;
            if (total <= 0) return "";
            var prob = _historicalService.GetWinProbability(total, SubmittedBy);
            return $"{prob:P0} win chance";
        }
    }

    public string WinProbabilityColor
    {
        get
        {
            var total = AdjustedTotal > 0 ? AdjustedTotal : GrandTotal;
            if (total <= 0) return "#888888";
            var prob = _historicalService.GetWinProbability(total, SubmittedBy);
            return prob switch
            {
                >= 0.5m => "#13a10e",  // green
                >= 0.3m => "#ffc83d",  // yellow
                _ => "#d13438"          // red
            };
        }
    }

    public bool IsAdjustmentDeduct => AdjustmentAmount < 0;

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

    // Contractor auto-complete suggestions
    [ObservableProperty]
    private List<string> _contractorSuggestions = [];

    [ObservableProperty]
    private bool _showContractorSuggestions;

    [RelayCommand]
    private void ApplyContractorSuggestion(string company)
    {
        ShowContractorSuggestions = false;
        ClientCompany = company;
        var contact = _historicalService.GetContact(company);
        if (contact is null) return;
        if (!string.IsNullOrEmpty(contact.Name) && string.IsNullOrEmpty(ClientName))
            ClientName = contact.Name;
        if (!string.IsNullOrEmpty(contact.Email) && string.IsNullOrEmpty(ClientEmail))
            ClientEmail = contact.Email;
        if (!string.IsNullOrEmpty(contact.Phone) && string.IsNullOrEmpty(ClientPhone))
            ClientPhone = contact.Phone;
    }

    partial void OnClientCompanyChanged(string value)
    {
        if (_suppressModified || string.IsNullOrWhiteSpace(value) || value.Length < 2)
        {
            ShowContractorSuggestions = false;
            return;
        }

        var names = _historicalService.GetContractorNames();
        var matches = names
            .Where(n => n.Contains(value, StringComparison.OrdinalIgnoreCase))
            .Take(8)
            .ToList();

        ContractorSuggestions = matches;
        ShowContractorSuggestions = matches.Count > 0;
    }

    public MainViewModel(EstimateFileService fileService, PdfGenerator pdfGenerator, ReportGenerator reportGenerator, JobIndexService jobIndexService, ConfigService configService, HistoricalDataService historicalService, PricingConfigService pricingConfigService, PdfTextExtractor pdfTextExtractor, PlanReaderService planReaderService, GeminiService geminiService)
    {
        _fileService = fileService;
        _pdfGenerator = pdfGenerator;
        _reportGenerator = reportGenerator;
        _jobIndexService = jobIndexService;
        _configService = configService;
        _historicalService = historicalService;
        _pricingConfigService = pricingConfigService;
        _pdfTextExtractor = pdfTextExtractor;
        _planReaderService = planReaderService;
        _geminiService = geminiService;
        Rooms.CollectionChanged += OnRoomsChanged;

        // Track modifications on all text properties
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not (nameof(StatusText) or nameof(IsModified) or nameof(WindowTitle)
                or nameof(GrandTotal) or nameof(AdjustmentAmount) or nameof(AdjustedTotal) or nameof(HasAdjustment) or nameof(IsAdjustmentDeduct)
                or nameof(WinProbabilityText) or nameof(WinProbabilityColor)))
                MarkModified();
        };
    }

    partial void OnIsModifiedChanged(bool value) => OnPropertyChanged(nameof(WindowTitle));

    partial void OnAdjustmentValueChanged(decimal value)
    {
        OnPropertyChanged(nameof(AdjustmentAmount));
        OnPropertyChanged(nameof(AdjustedTotal));
        OnPropertyChanged(nameof(HasAdjustment));
        OnPropertyChanged(nameof(IsAdjustmentDeduct));
    }

    partial void OnAdjustmentModeChanged(string value)
    {
        OnPropertyChanged(nameof(AdjustmentAmount));
        OnPropertyChanged(nameof(AdjustedTotal));
        OnPropertyChanged(nameof(IsAdjustmentDeduct));
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
            AdjustmentValue = 0;
            AdjustmentMode = "%";
            AdjustmentLabel = string.Empty;
            Exclusions = Estimate.DefaultExclusions;
            Notes = string.Empty;
            SubmittedBy = "Rusty Philbrick";

            ClearRooms();

            var room = new RoomViewModel { RoomName = "Room 1" };

            // Load templates from pricing config spreadsheet (creates xlsx if missing)
            room.PopulateFromConfig(await _pricingConfigService.LoadAsync());

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
    /// <summary>Open an estimate file by path (called from War Room).</summary>
    public async void OpenEstimateFromPath(string filePath)
    {
        if (IsModified && !ConfirmDiscard()) return;
        try
        {
            var estimate = await _fileService.LoadAsync(filePath);
            LoadFromModel(estimate);
            _currentFilePath = filePath;
            IsModified = false;
            StatusText = $"Opened: {System.IO.Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to open estimate:\n{ex.Message}",
                "Open Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
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

        room.PopulateFromConfig(await _pricingConfigService.LoadAsync());

        AddRoomInternal(room);
        MarkModified();
    }

    [RelayCommand]
    private async Task ImportFromPlans()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "PDF Files (*.pdf)|*.pdf|All Files (*.*)|*.*",
            Title = "Import Rooms from Plans",
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            StatusText = "Reading PDF...";

            // Extract text from PDF (runs OCR if available)
            var pages = await Task.Run(() => _pdfTextExtractor.ExtractText(dialog.FileName));
            if (pages.Count == 0)
            {
                System.Windows.MessageBox.Show("Could not extract any text from the PDF.\nMake sure OCR (tessdata) is available for scanned plans.",
                    "Import Failed", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                StatusText = "Import failed — no text extracted";
                return;
            }

            // Combine all pages into one text block
            var allText = string.Join("\n", pages.OrderBy(p => p.Key).Select(p => p.Value));

            // Parse rooms and items
            StatusText = "Parsing rooms...";
            var extractedRooms = _planReaderService.ExtractRooms(allText);

            if (extractedRooms.Count == 0)
            {
                System.Windows.MessageBox.Show("No rooms or cabinet items were detected in this PDF.\nThe plan may not contain recognizable room/cabinet keywords.",
                    "Nothing Found", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                StatusText = "Import found no rooms";
                return;
            }

            // Build summary for confirmation
            var totalItems = extractedRooms.Sum(r => r.Items.Count);
            var itemsWithQty = extractedRooms.Sum(r => r.Items.Count(i => i.Quantity.HasValue));
            var summary = $"Found {extractedRooms.Count} room(s) with {totalItems} cabinet item(s)";
            if (itemsWithQty > 0)
                summary += $" ({itemsWithQty} with quantities)";
            summary += $".\n\nRooms:\n";
            foreach (var room in extractedRooms)
            {
                summary += $"  - {room.Name}: {string.Join(", ", room.Items.Select(i => i.MatchedTemplate))}\n";
            }
            summary += "\nImport these rooms into the estimate?";

            var result = System.Windows.MessageBox.Show(summary, "Import from Plans",
                System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);

            if (result != System.Windows.MessageBoxResult.Yes)
            {
                StatusText = "Import cancelled";
                return;
            }

            // Clear existing rooms (remove default Room 1)
            ClearRooms();

            // Load current templates for populating rooms
            var templates = await _pricingConfigService.LoadAsync();

            // Create rooms and populate
            foreach (var extracted in extractedRooms)
            {
                var room = new RoomViewModel { RoomName = extracted.Name, IsExpanded = true };
                room.PopulateFromConfig(templates);

                // Enable matched items, set quantities, disable unmatched
                var matchedTemplates = new HashSet<string>(
                    extracted.Items.Select(i => i.MatchedTemplate),
                    StringComparer.OrdinalIgnoreCase);

                foreach (var lineItem in room.LineItems)
                {
                    if (matchedTemplates.Contains(lineItem.Name))
                    {
                        lineItem.IsEnabled = true;

                        // Set quantity if we found one
                        var extractedItem = extracted.Items
                            .FirstOrDefault(i => string.Equals(i.MatchedTemplate, lineItem.Name, StringComparison.OrdinalIgnoreCase));
                        if (extractedItem?.Quantity is > 0)
                            lineItem.Quantity = extractedItem.Quantity.Value;

                        // Notes left blank — OCR context is not useful
                    }
                    else
                    {
                        lineItem.IsEnabled = false;
                    }
                }

                AddRoomInternal(room);
            }

            MarkModified();

            StatusText = $"Imported {extractedRooms.Count} room(s) from plans";
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to import from plans:\n\n{ex.Message}",
                "Import Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            StatusText = "Import failed";
        }
    }

    [RelayCommand]
    private async Task AiBid()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "PDF Files (*.pdf)|*.pdf|All Files (*.*)|*.*",
            Title = "Select Architectural Plans for AI Analysis",
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            StatusText = "AI is reading the plans...";

            var progress = new Progress<string>(msg => StatusText = msg);
            var result = await Task.Run(() => _geminiService.AnalyzePlans(dialog.FileName, progress));

            if (result.Rooms.Count == 0)
            {
                System.Windows.MessageBox.Show("AI couldn't identify any rooms or cabinet items in this PDF.",
                    "No Results", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                StatusText = "AI found no rooms";
                return;
            }

            // Build summary
            var totalItems = result.Rooms.Sum(r => r.Items.Count);
            var summary = $"AI found {result.Rooms.Count} room(s) with {totalItems} item(s)";
            if (!string.IsNullOrEmpty(result.ProjectName))
                summary += $"\nProject: {result.ProjectName}";
            if (!string.IsNullOrEmpty(result.Grade) && result.Grade != "PLAM")
                summary += $"\nDetected grade: {result.Grade}";
            summary += "\n\nRooms:\n";
            foreach (var room in result.Rooms)
            {
                summary += $"  {room.Name}:\n";
                foreach (var item in room.Items)
                    summary += $"    - {item.Item}: {item.Quantity} {item.Unit}" +
                        (string.IsNullOrEmpty(item.Notes) ? "" : $" ({item.Notes})") + "\n";
            }
            if (!string.IsNullOrEmpty(result.Notes))
                summary += $"\nAI Notes: {result.Notes}";
            summary += "\n\nApply to estimate?";

            var confirm = System.Windows.MessageBox.Show(summary, "AI Bid Results",
                System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
            if (confirm != System.Windows.MessageBoxResult.Yes)
            {
                StatusText = "AI bid cancelled";
                return;
            }

            // Clear existing rooms
            ClearRooms();

            // Set grade if detected
            if (!string.IsNullOrEmpty(result.Grade))
                SelectedGrade = result.Grade;

            // Fill client/project info from AI
            if (!string.IsNullOrEmpty(result.ProjectName) && string.IsNullOrEmpty(JobName))
                JobName = result.ProjectName;
            if (!string.IsNullOrEmpty(result.ClientCompany) && string.IsNullOrEmpty(ClientCompany))
                ClientCompany = result.ClientCompany;
            if (!string.IsNullOrEmpty(result.ClientName) && string.IsNullOrEmpty(ClientName))
                ClientName = result.ClientName;
            if (!string.IsNullOrEmpty(result.ClientAddress) && string.IsNullOrEmpty(ClientAddress))
                ClientAddress = result.ClientAddress;

            // Load templates and populate rooms
            var templates = await _pricingConfigService.LoadAsync();

            // Map AI item names to template names
            var templateNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Upper Cabinets"] = "PLAM Upper Cabinets",
                ["Base Cabinets"] = "PLAM Base Cabinets",
                ["Tall Cabinets"] = "PLAM Tall Cabinets",
                ["Solid Surface Countertops"] = "Solid Surface Countertops",
                ["ADA Vanity"] = "PLAM ADA Vanity",
                ["Floating Shelf"] = "PLAM Floating Shelf",
                ["Wall Caps"] = "PLAM Wall Caps",
                ["End Panels"] = "PLAM End Panels",
                ["PLAM Countertops"] = "PLAM Countertops",
                ["Quartz Countertops"] = "Quartz Countertops",
                ["Brackets"] = "Brackets",
                ["Stainless Steel Legs"] = "Stainless Steel Legs",
                ["Stain and Lacquer"] = "Stain and Lacquer to Customer Color Selection",
                ["Delivery and Installation"] = "Delivery and Installation",
            };

            foreach (var aiRoom in result.Rooms)
            {
                var room = new RoomViewModel
                {
                    RoomName = aiRoom.Name,
                    IsExpanded = true,
                    Multiplier = aiRoom.Multiplier > 0 ? aiRoom.Multiplier : 1
                };
                room.PopulateFromConfig(templates);

                // Build set of matched template names for this room
                var matchedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var aiItem in aiRoom.Items)
                {
                    // Try direct mapping, then fuzzy match
                    if (templateNameMap.TryGetValue(aiItem.Item, out var mapped))
                        matchedNames.Add(mapped);
                    else
                    {
                        // Fuzzy: find template containing the AI item name
                        var match = templates.FirstOrDefault(t =>
                            t.Name.Contains(aiItem.Item, StringComparison.OrdinalIgnoreCase) ||
                            aiItem.Item.Contains(t.Name.Replace("PLAM ", ""), StringComparison.OrdinalIgnoreCase));
                        if (match != null)
                            matchedNames.Add(match.Name);
                    }
                }

                foreach (var lineItem in room.LineItems)
                {
                    if (matchedNames.Contains(lineItem.Name))
                    {
                        lineItem.IsEnabled = true;

                        // Find the AI item that matched
                        var aiItem = aiRoom.Items.FirstOrDefault(ai =>
                        {
                            if (templateNameMap.TryGetValue(ai.Item, out var m))
                                return string.Equals(m, lineItem.Name, StringComparison.OrdinalIgnoreCase);
                            return lineItem.Name.Contains(ai.Item, StringComparison.OrdinalIgnoreCase);
                        });

                        if (aiItem is not null)
                        {
                            if (aiItem.Quantity > 0)
                                lineItem.Quantity = aiItem.Quantity;
                        }
                    }
                    else
                    {
                        lineItem.IsEnabled = false;
                    }
                }

                // Apply grade to this room's items
                if (!string.IsNullOrEmpty(result.Grade))
                {
                    foreach (var li in room.LineItems)
                        li.SetGrade(result.Grade);
                }

                AddRoomInternal(room);
            }

            MarkModified();

            StatusText = $"AI populated {result.Rooms.Count} room(s) with {result.Rooms.Sum(r => r.Items.Count)} items";
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"AI bid failed:\n\n{ex.Message}",
                "AI Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            StatusText = "AI bid failed";
        }
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
    private async Task GenerateWeeklyReport()
    {
        try
        {
            StatusText = "Generating weekly pipeline report...";
            var folder = ReportGenerator.GetReportsFolder();
            var fileName = $"Weekly_Pipeline_{DateTime.Now:yyyy-MM-dd}.pdf";
            var outputPath = System.IO.Path.Combine(folder, fileName);
            var jobs = await _jobIndexService.LoadEntriesAsync();

            await Task.Run(() => _reportGenerator.GenerateWeeklyPipeline(outputPath, jobs));
            StatusText = $"Report saved: {fileName}";

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = outputPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to generate weekly report:\n\n{ex.Message}",
                "Report Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            StatusText = "Report generation failed";
        }
    }

    [RelayCommand]
    private async Task GenerateMonthlyReport()
    {
        try
        {
            StatusText = "Generating monthly performance report...";
            var folder = ReportGenerator.GetReportsFolder();
            var fileName = $"Monthly_Performance_{DateTime.Now:yyyy-MM}.pdf";
            var outputPath = System.IO.Path.Combine(folder, fileName);
            var jobs = await _jobIndexService.LoadEntriesAsync();
            var data = await _historicalService.LoadAsync() ?? new Models.HistoricalData();

            await Task.Run(() => _reportGenerator.GenerateMonthlyPerformance(outputPath, jobs, data));
            StatusText = $"Report saved: {fileName}";

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = outputPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to generate monthly report:\n\n{ex.Message}",
                "Report Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            StatusText = "Report generation failed";
        }
    }

    [RelayCommand]
    private async Task GenerateBidSummary()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(JobName))
            {
                System.Windows.MessageBox.Show("Save or name the estimate before generating a bid summary.",
                    "Bid Summary", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }

            StatusText = "Generating bid summary...";
            var folder = ReportGenerator.GetReportsFolder();
            var safeName = string.Join("_", JobName.Split(System.IO.Path.GetInvalidFileNameChars()));
            var fileName = $"Bid_Summary_{safeName}_{DateTime.Now:yyyy-MM-dd}.pdf";
            var outputPath = System.IO.Path.Combine(folder, fileName);

            var estimate = ToModel();
            var total = AdjustedTotal > 0 ? AdjustedTotal : GrandTotal;
            var winProb = _historicalService.GetWinProbability(total, SubmittedBy);

            // Load historical data for bucket comparison
            var histData = await _historicalService.LoadAsync();
            ReportGenerator.SetHistoricalData(histData);

            await Task.Run(() => _reportGenerator.GenerateBidSummary(outputPath, estimate, total, winProb));
            StatusText = $"Report saved: {fileName}";

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = outputPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to generate bid summary:\n\n{ex.Message}",
                "Report Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            StatusText = "Report generation failed";
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
        OnPropertyChanged(nameof(HasAdjustment));
        OnPropertyChanged(nameof(IsAdjustmentDeduct));
        OnPropertyChanged(nameof(WinProbabilityText));
        OnPropertyChanged(nameof(WinProbabilityColor));
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
        AdjustmentPercent = AdjustmentMode == "%" ? AdjustmentValue : 0,
        AdjustmentDollar = AdjustmentMode == "$" ? AdjustmentValue : 0,
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
            if (estimate.AdjustmentDollar != 0)
            {
                AdjustmentMode = "$";
                AdjustmentValue = estimate.AdjustmentDollar;
            }
            else
            {
                AdjustmentMode = "%";
                AdjustmentValue = estimate.AdjustmentPercent;
            }
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
