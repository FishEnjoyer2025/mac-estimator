using CommunityToolkit.Mvvm.ComponentModel;
using MacEstimator.App.Models;
using MacEstimator.App.Services;

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

    // Historical pricing hint
    [ObservableProperty]
    private string _pricingHint = string.Empty;

    public bool HasPricingHint => !string.IsNullOrEmpty(PricingHint);

    public UnitType Unit { get; }
    public PricingMode Mode { get; }
    public string[]? NameOptions { get; }

    // Grade-specific default rates (from template)
    private decimal _plamRate;
    private decimal? _paintGradeRate;
    private decimal? _stainGradeRate;

    public decimal? CostFloor { get; }

    /// <summary>True when the rate is below the cost floor — a money-losing price.</summary>
    public bool IsLowMargin => CostFloor.HasValue && Mode == PricingMode.PerUnit && Rate < CostFloor.Value;

    public bool HasNameOptions => NameOptions is { Length: > 1 };

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
        _plamRate = template.DefaultRate;
        _paintGradeRate = template.PaintGradeRate;
        _stainGradeRate = template.StainGradeRate;
        CostFloor = template.CostFloor;
        Unit = template.Unit;
        Mode = template.Mode;
        NameOptions = template.NameOptions;
        _allInstances.Add(new WeakReference<LineItemViewModel>(this));
        RefreshPricingHint();
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
        NameOptions = model.NameOptions;
        _allInstances.Add(new WeakReference<LineItemViewModel>(this));
        RefreshPricingHint();
    }

    partial void OnIsEnabledChanged(bool value) => OnPropertyChanged(nameof(LineTotal));
    partial void OnQuantityChanged(decimal value) => OnPropertyChanged(nameof(LineTotal));
    partial void OnRateChanged(decimal value)
    {
        OnPropertyChanged(nameof(LineTotal));
        OnPropertyChanged(nameof(IsLowMargin));
    }
    partial void OnVendorCostChanged(decimal value) => OnPropertyChanged(nameof(LineTotal));

    partial void OnNameChanged(string value)
    {
        RefreshPricingHint();
    }

    private static HistoricalDataService? _historicalService;
    private static readonly List<WeakReference<LineItemViewModel>> _allInstances = [];

    public static void SetHistoricalService(HistoricalDataService service)
    {
        _historicalService = service;
        // Refresh pricing hints for all existing line items
        foreach (var wr in _allInstances.ToList())
        {
            if (wr.TryGetTarget(out var vm))
                vm.RefreshPricingHint();
        }
    }

    public void RefreshPricingHint()
    {
        if (_historicalService is null || Mode != PricingMode.PerUnit)
        {
            PricingHint = string.Empty;
            return;
        }

        var stats = _historicalService.GetPricing(Name);
        if (stats is null || stats.Count < 2)
        {
            PricingHint = string.Empty;
            return;
        }

        PricingHint = $"Hist: ${stats.MinUnitPrice}-${stats.MaxUnitPrice} avg ${stats.AvgUnitPrice} ({stats.Count} bids)";
        OnPropertyChanged(nameof(HasPricingHint));
    }

    /// <summary>
    /// Sets grade by matching the option that contains the given prefix.
    /// Used by the global grade selector.
    /// </summary>
    public void SetGrade(string gradePrefix)
    {
        if (NameOptions is null) return;
        var match = NameOptions.FirstOrDefault(o => o.StartsWith(gradePrefix, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
            Name = match;

        // Switch rate based on grade (only if user hasn't manually edited it away from a default)
        var newRate = gradePrefix switch
        {
            "Paint Grade" => _paintGradeRate,
            "Stain Grade" => _stainGradeRate,
            _ => _plamRate
        };
        if (newRate.HasValue)
            Rate = newRate.Value;
    }

    public LineItem ToModel() => new()
    {
        Name = Name,
        IsEnabled = IsEnabled,
        Quantity = Quantity,
        Rate = Rate,
        VendorCost = VendorCost,
        Note = Note,
        Unit = Unit,
        Mode = Mode,
        NameOptions = NameOptions
    };
}
