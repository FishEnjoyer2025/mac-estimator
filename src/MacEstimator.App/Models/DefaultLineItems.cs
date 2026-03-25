namespace MacEstimator.App.Models;

public static class DefaultLineItems
{
    public static LineItemTemplate[] All =>
    [
        new("PLAM Upper Cabinets",        115m, UnitType.LinearFoot),
        new("PLAM Base Cabinets",         215m, UnitType.LinearFoot),
        new("PLAM Tall Cabinets",         360m, UnitType.LinearFoot),
        new("Solid Surface Countertops", 1.20m, UnitType.SquareFoot, PricingMode.VendorQuoteMarkup),
        new("PLAM ADA Vanity",            225m, UnitType.LinearFoot),
        new("PLAM Floating Shelf",         80m, UnitType.LinearFoot),
        new("PLAM Wall Caps",              40m, UnitType.LinearFoot),
        new("PLAM End Panels",             85m, UnitType.Each),
        new("PLAM Countertops",            80m, UnitType.SquareFoot),
        new("Quartz Countertops",        1.20m, UnitType.SquareFoot, PricingMode.VendorQuoteMarkup),
        new("Brackets",                   125m, UnitType.Each),
        new("Stainless Steel Legs",       125m, UnitType.Each),
        new("Delivery and Installation",  100m, UnitType.LinearFoot),
    ];
}
