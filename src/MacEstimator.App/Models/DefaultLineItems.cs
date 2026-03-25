namespace MacEstimator.App.Models;

public static class DefaultLineItems
{
    private static string[] GradeOptions(string suffix) =>
        [$"PLAM {suffix}", $"Paint Grade {suffix}", $"Stain Grade {suffix}"];

    public static LineItemTemplate[] All =>
    [
        new("PLAM Upper Cabinets",        115m, UnitType.LinearFoot, NameOptions: GradeOptions("Upper Cabinets")),
        new("PLAM Base Cabinets",         215m, UnitType.LinearFoot, NameOptions: GradeOptions("Base Cabinets")),
        new("PLAM Tall Cabinets",         360m, UnitType.LinearFoot, NameOptions: GradeOptions("Tall Cabinets")),
        new("Solid Surface Countertops", 1.20m, UnitType.SquareFoot, PricingMode.VendorQuoteMarkup),
        new("PLAM ADA Vanity",            225m, UnitType.LinearFoot, NameOptions: GradeOptions("ADA Vanity")),
        new("PLAM Floating Shelf",         80m, UnitType.LinearFoot, NameOptions: GradeOptions("Floating Shelf")),
        new("PLAM Wall Caps",              40m, UnitType.LinearFoot, NameOptions: GradeOptions("Wall Caps")),
        new("PLAM End Panels",             85m, UnitType.Each,       NameOptions: GradeOptions("End Panels")),
        new("PLAM Countertops",            80m, UnitType.SquareFoot, NameOptions: GradeOptions("Countertops")),
        new("Quartz Countertops",        1.20m, UnitType.SquareFoot, PricingMode.VendorQuoteMarkup),
        new("Brackets",                   125m, UnitType.Each),
        new("Stainless Steel Legs",       125m, UnitType.Each),
        new("Stain and Lacquer to Customer Color Selection", 100m, UnitType.LinearFoot,
            NameOptions: ["Stain and Lacquer to Customer Color Selection", "Paint to Customer Color Selection"]),
        new("Delivery and Installation",  100m, UnitType.LinearFoot),
    ];
}
