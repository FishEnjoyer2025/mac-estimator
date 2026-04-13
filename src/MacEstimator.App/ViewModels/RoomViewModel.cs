using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacEstimator.App.Models;
using MacEstimator.App.Services;

namespace MacEstimator.App.ViewModels;

public partial class RoomViewModel : ObservableObject
{
    private static HistoricalDataService? _historicalService;

    public static void SetHistoricalService(HistoricalDataService service)
    {
        _historicalService = service;
    }

    [ObservableProperty]
    private string _roomName = "Room 1";

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private int _multiplier = 1;

    // Room template suggestions
    [ObservableProperty]
    private bool _hasSuggestions;

    [ObservableProperty]
    private string _suggestionSummary = string.Empty;

    [ObservableProperty]
    private List<RoomSuggestionItem> _suggestedItems = [];

    private RoomTemplateData? _currentTemplate;

    public ObservableCollection<LineItemViewModel> LineItems { get; } = [];

    public decimal RoomTotal => LineItems
        .Where(li => li.IsEnabled)
        .Sum(li => li.LineTotal) * Math.Max(Multiplier, 1);

    public bool HasMultiplier => Multiplier > 1;

    public string MultiplierLabel => Multiplier > 1 ? $"x{Multiplier}" : "";

    partial void OnMultiplierChanged(int value)
    {
        OnPropertyChanged(nameof(RoomTotal));
        OnPropertyChanged(nameof(HasMultiplier));
        OnPropertyChanged(nameof(MultiplierLabel));
    }

    partial void OnRoomNameChanged(string value)
    {
        RefreshRoomSuggestions();
    }

    private void RefreshRoomSuggestions()
    {
        if (_historicalService is null || string.IsNullOrWhiteSpace(RoomName))
        {
            HasSuggestions = false;
            SuggestionSummary = string.Empty;
            SuggestedItems = [];
            _currentTemplate = null;
            return;
        }

        var template = _historicalService.GetRoomTemplate(RoomName);
        _currentTemplate = template;

        if (template is null || template.Items.Count == 0)
        {
            HasSuggestions = false;
            SuggestionSummary = string.Empty;
            SuggestedItems = [];
            return;
        }

        var items = template.Items
            .OrderByDescending(i => i.Value.Count)
            .Select(i => new RoomSuggestionItem
            {
                ItemName = i.Key,
                AvgQuantity = i.Value.AvgQty,
                AvgPrice = i.Value.AvgPrice,
                Occurrences = i.Value.Count,
            })
            .ToList();

        SuggestedItems = items;
        SuggestionSummary = $"Based on {template.Count} past bids";
        HasSuggestions = true;
    }

    [RelayCommand]
    private void ApplySuggestions()
    {
        if (_currentTemplate is null) return;

        foreach (var lineItem in LineItems)
        {
            // Try to match line item name to template items
            foreach (var (templateItemName, templateItemData) in _currentTemplate.Items)
            {
                if (IsItemMatch(lineItem.Name, templateItemName))
                {
                    lineItem.IsEnabled = true;
                    if (lineItem.Quantity == 0)
                        lineItem.Quantity = Math.Round(templateItemData.AvgQty, 0);
                    break;
                }
            }
        }
    }

    public RoomViewModel()
    {
        LineItems.CollectionChanged += OnLineItemsChanged;
    }

    public RoomViewModel(Room model) : this()
    {
        _roomName = model.Name;
        _multiplier = model.Multiplier;
        foreach (var item in model.LineItems)
            AddItemInternal(new LineItemViewModel(item));
    }

    public void PopulateDefaults()
    {
        foreach (var template in DefaultLineItems.All)
            AddItemInternal(new LineItemViewModel(template));
    }

    public void PopulateFromConfig(LineItemTemplate[] templates)
    {
        foreach (var template in templates)
            AddItemInternal(new LineItemViewModel(template));
    }

    [RelayCommand]
    private void AddCustomItem()
    {
        var item = new LineItemViewModel(new LineItemTemplate("Custom Item", 0m, UnitType.LinearFoot));
        AddItemInternal(item);
    }

    [RelayCommand]
    private void RemoveItem(LineItemViewModel item)
    {
        item.PropertyChanged -= OnItemPropertyChanged;
        LineItems.Remove(item);
        OnPropertyChanged(nameof(RoomTotal));
    }

    [RelayCommand]
    private void MoveItemUp(LineItemViewModel item)
    {
        var index = LineItems.IndexOf(item);
        if (index > 0)
            LineItems.Move(index, index - 1);
    }

    [RelayCommand]
    private void MoveItemDown(LineItemViewModel item)
    {
        var index = LineItems.IndexOf(item);
        if (index >= 0 && index < LineItems.Count - 1)
            LineItems.Move(index, index + 1);
    }

    [RelayCommand]
    private void CheckAll()
    {
        foreach (var item in LineItems)
            item.IsEnabled = true;
    }

    [RelayCommand]
    private void UncheckAll()
    {
        foreach (var item in LineItems)
            item.IsEnabled = false;
    }

    private void AddItemInternal(LineItemViewModel item)
    {
        item.PropertyChanged += OnItemPropertyChanged;
        LineItems.Add(item);
    }

    private void OnItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LineItemViewModel.LineTotal) or nameof(LineItemViewModel.IsEnabled))
            OnPropertyChanged(nameof(RoomTotal));
    }

    private void OnLineItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(RoomTotal));
    }

    /// <summary>
    /// Check if a line item name matches a template item name.
    /// Handles grade prefixes (PLAM/Paint Grade/Stain Grade).
    /// </summary>
    private static bool IsItemMatch(string lineItemName, string templateItemName)
    {
        if (string.Equals(lineItemName, templateItemName, StringComparison.OrdinalIgnoreCase))
            return true;

        // Strip grade prefix from both and compare
        var stripped1 = StripGradePrefix(lineItemName);
        var stripped2 = StripGradePrefix(templateItemName);
        if (string.Equals(stripped1, stripped2, StringComparison.OrdinalIgnoreCase))
            return true;

        // Check if one contains the other (after stripping)
        if (stripped1.Contains(stripped2, StringComparison.OrdinalIgnoreCase)
            || stripped2.Contains(stripped1, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static string StripGradePrefix(string name)
    {
        foreach (var prefix in new[] { "PLAM ", "Paint Grade ", "Stain Grade " })
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return name[prefix.Length..];
        }
        return name;
    }

    public Room ToModel() => new()
    {
        Name = RoomName,
        Multiplier = Multiplier,
        LineItems = LineItems.Select(li => li.ToModel()).ToList()
    };
}

/// <summary>
/// A single suggested line item from room template history.
/// </summary>
public class RoomSuggestionItem
{
    public string ItemName { get; set; } = string.Empty;
    public decimal AvgQuantity { get; set; }
    public decimal AvgPrice { get; set; }
    public int Occurrences { get; set; }
    public string Display => $"{ItemName}: ~{AvgQuantity:F0} qty, ~${AvgPrice}/unit ({Occurrences} bids)";
}
