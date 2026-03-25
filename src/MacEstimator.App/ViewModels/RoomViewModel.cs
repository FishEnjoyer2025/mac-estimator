using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacEstimator.App.Models;

namespace MacEstimator.App.ViewModels;

public partial class RoomViewModel : ObservableObject
{
    [ObservableProperty]
    private string _roomName = "Room 1";

    [ObservableProperty]
    private bool _isExpanded = true;

    public ObservableCollection<LineItemViewModel> LineItems { get; } = [];

    public decimal RoomTotal => LineItems
        .Where(li => li.IsEnabled)
        .Sum(li => li.LineTotal);

    public RoomViewModel()
    {
        LineItems.CollectionChanged += OnLineItemsChanged;
    }

    public RoomViewModel(Room model) : this()
    {
        _roomName = model.Name;
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

    public Room ToModel() => new()
    {
        Name = RoomName,
        LineItems = LineItems.Select(li => li.ToModel()).ToList()
    };
}
