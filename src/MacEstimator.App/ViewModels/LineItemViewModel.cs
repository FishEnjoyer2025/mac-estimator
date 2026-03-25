using CommunityToolkit.Mvvm.ComponentModel;
using MacEstimator.App.Models;

namespace MacEstimator.App.ViewModels;

public partial class LineItemViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private decimal _quantity;

    [ObservableProperty]
    private decimal _rate;

    [ObservableProperty]
    private decimal _vendorCost;

    [ObservableProperty]
    private string _note = string.Empty;

    public UnitType Unit { get; }
    public PricingMode Mode { get; }

    public string UnitLabel => Unit switch
    {
        UnitType.LinearFoot => "LF",
        UnitType.SquareFoot => "SF",
        UnitType.Each => "EA",
        _ => ""
    };

    public bool IsPerUnit => Mode == PricingMode.PerUnit;
    public bool IsVendorQuote => Mode == PricingMode.VendorQuoteMarkup;

    public decimal LineTotal => Mode == PricingMode.PerUnit
        ? Quantity * Rate
        : VendorCost * Rate;

    public LineItemViewModel(LineItemTemplate template)
    {
        _name = template.Name;
        _rate = template.DefaultRate;
        Unit = template.Unit;
        Mode = template.Mode;
    }

    public LineItemViewModel(LineItem model)
    {
        _name = model.Name;
        _isEnabled = model.IsEnabled;
        _quantity = model.Quantity;
        _rate = model.Rate;
        _vendorCost = model.VendorCost;
        _note = model.Note;
        Unit = model.Unit;
        Mode = model.Mode;
    }

    partial void OnIsEnabledChanged(bool value) => OnPropertyChanged(nameof(LineTotal));
    partial void OnQuantityChanged(decimal value) => OnPropertyChanged(nameof(LineTotal));
    partial void OnRateChanged(decimal value) => OnPropertyChanged(nameof(LineTotal));
    partial void OnVendorCostChanged(decimal value) => OnPropertyChanged(nameof(LineTotal));

    public LineItem ToModel() => new()
    {
        Name = Name,
        IsEnabled = IsEnabled,
        Quantity = Quantity,
        Rate = Rate,
        VendorCost = VendorCost,
        Note = Note,
        Unit = Unit,
        Mode = Mode
    };
}
