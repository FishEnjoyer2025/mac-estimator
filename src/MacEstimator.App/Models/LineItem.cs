namespace MacEstimator.App.Models;

public class LineItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public UnitType Unit { get; set; }
    public PricingMode Mode { get; set; }
    public decimal VendorCost { get; set; }
    public string Note { get; set; } = string.Empty;

    public decimal LineTotal => Mode == PricingMode.PerUnit
        ? Quantity * Rate
        : VendorCost * Rate;
}
