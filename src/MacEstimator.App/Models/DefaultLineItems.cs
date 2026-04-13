namespace MacEstimator.App.Models;

public static class DefaultLineItems
{
    private static string[] GradeOptions(string suffix) =>
        [$"PLAM {suffix}", $"Paint Grade {suffix}", $"Stain Grade {suffix}"];

    public static LineItemTemplate[] All =>
    [
        new("PLAM Upper Cabinets",        115m, UnitType.LinearFoot, NameOptions: GradeOptions("Upper Cabinets"),
            PaintGradeRate: 145m, StainGradeRate: 165m, CostFloor: 80m),
        new("PLAM Base Cabinets",         215m, UnitType.LinearFoot, NameOptions: GradeOptions("Base Cabinets"),
            PaintGradeRate: 250m, StainGradeRate: 275m, CostFloor: 150m),
        new("PLAM Tall Cabinets",         360m, UnitType.LinearFoot, NameOptions: GradeOptions("Tall Cabinets"),
            PaintGradeRate: 425m, StainGradeRate: 475m, CostFloor: 250m),
        new("Solid Surface Countertops", 1.20m, UnitType.SquareFoot, PricingMode.VendorQuoteMarkup),
        new("PLAM ADA Vanity",            225m, UnitType.LinearFoot, NameOptions: GradeOptions("ADA Vanity"),
            PaintGradeRate: 265m, StainGradeRate: 300m, CostFloor: 155m),
        new("PLAM Shelves w/ Brackets",    80m, UnitType.LinearFoot, NameOptions: GradeOptions("Shelves w/ Brackets"),
            PaintGradeRate: 95m, StainGradeRate: 110m, CostFloor: 55m),
        new("PLAM Floating Shelf",         80m, UnitType.LinearFoot, NameOptions: GradeOptions("Floating Shelf"),
            PaintGradeRate: 95m, StainGradeRate: 110m, CostFloor: 55m),
        new("PLAM Wall Caps",              40m, UnitType.LinearFoot, NameOptions: GradeOptions("Wall Caps"),
            PaintGradeRate: 55m, StainGradeRate: 65m, CostFloor: 28m),
        new("PLAM End Panels",             85m, UnitType.Each,       NameOptions: GradeOptions("End Panels"),
            PaintGradeRate: 110m, StainGradeRate: 140m, CostFloor: 60m),
        new("PLAM Countertops",            80m, UnitType.SquareFoot, NameOptions: GradeOptions("Countertops"),
            PaintGradeRate: 80m, StainGradeRate: 85m, CostFloor: 55m),
        new("PLAM Countertop w/ Support Panel", 90m, UnitType.LinearFoot, NameOptions: GradeOptions("Countertop w/ Support Panel"),
            PaintGradeRate: 100m, StainGradeRate: 115m, CostFloor: 63m),
        new("PLAM Flip-Up Countertop",     80m, UnitType.LinearFoot, NameOptions: GradeOptions("Flip-Up Countertop"),
            PaintGradeRate: 95m, StainGradeRate: 110m, CostFloor: 55m),
        new("Quartz Countertops",        1.20m, UnitType.SquareFoot, PricingMode.VendorQuoteMarkup),
        new("Brackets",                   125m, UnitType.Each, CostFloor: 85m),
        new("Stainless Steel Legs",       125m, UnitType.Each, CostFloor: 85m),
        new("Stain and Lacquer to Customer Color Selection", 100m, UnitType.LinearFoot,
            NameOptions: ["Stain and Lacquer to Customer Color Selection", "Paint to Customer Color Selection"],
            CostFloor: 70m),
        new("Delivery and Installation",  100m, UnitType.LinearFoot, CostFloor: 70m),
    ];
}
