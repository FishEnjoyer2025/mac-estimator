namespace MacEstimator.App.Models;

public enum UnitType
{
    LinearFoot,
    SquareFoot,
    Each
}

public enum PricingMode
{
    PerUnit,
    VendorQuoteMarkup
}

public record LineItemTemplate(
    string Name,
    decimal DefaultRate,
    UnitType Unit,
    PricingMode Mode = PricingMode.PerUnit);
