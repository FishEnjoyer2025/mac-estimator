# PDF Exclusion Tab - Design Spec

## Overview

Add a "PDF Exclusion" tab to the MAC Estimator that lets the estimator drop an architectural PDF, extract text, and see a fitness score based on configurable good/bad keywords. Advisory only -- no automated accept/reject. All keyword configuration changes are exported to Excel on the shared drive for Dylan to review.

## Architecture

- Wrap existing MainWindow content in a `TabControl` with two tabs: "Estimate" and "PDF Exclusion"
- New `PdfExclusionViewModel` drives the tab
- PdfPig for text extraction (handles CAD/Revit PDFs with embedded text)
- ClosedXML for Excel export of keyword config

## PDF Exclusion Tab Layout

- **Top:** Drop zone (drag-and-drop + browse button) with loaded file name display
- **Left panel:** Keyword config -- two lists (Good Keywords / Bad Keywords) with add/remove
- **Right panel:** Results -- fitness score (0-100, color-coded), found keywords with page numbers, keywords not found
- **Bottom:** Collapsible extracted text preview

## Scoring (Advisory)

- Start at 50 (neutral)
- Good keyword found: +(50 / total good keywords)
- Bad keyword found: -(50 / total bad keywords)
- Clamp to 0-100
- Color: green 70+, yellow 40-69, red <40
- No accept/reject language -- just the number and the keyword hits

## Keyword Config Excel Export

- Saved to `G:\My Drive\MAC\Estimator\keyword_config.xlsx`
- Columns: Keyword | Type (Good/Bad) | Date Added
- Auto-saves on every add/remove
- Also persists as JSON alongside for app reload

## Default Keywords

**Good:** Solid Surface, PLAM, Plastic Laminate, Upper Cabinets, Base Cabinets, Casework, Millwork, Reception Desk, Nurse Station, Break Room, Mail Room, Copy Room, File Cabinets, Bookshelves, Countertop, P-Lam, HPL, Tall Cabinets, Pantry

**Bad:** Radius, Curved, Metal Fabrication, Stainless Steel, Glass Doors, Etched Glass, Custom Hardware, Stone Countertop, Granite, Quartz, Corian

## New Files

| File | Purpose |
|------|---------|
| Models/KeywordConfig.cs | Good/bad keyword lists data model |
| Models/PdfAnalysisResult.cs | Score + found/missing keyword results |
| Services/PdfTextExtractor.cs | PdfPig text extraction |
| Services/KeywordScoringService.cs | Fitness score calculation |
| Services/KeywordConfigService.cs | Load/save keywords (JSON + Excel export) |
| ViewModels/PdfExclusionViewModel.cs | Tab logic |
| Views/PdfExclusionTab.xaml | Tab UI |

## Modified Files

| File | Change |
|------|--------|
| MainWindow.xaml | Wrap content in TabControl |
| App.xaml.cs | Register new services + ViewModel in DI |
| MacEstimator.App.csproj | Add PdfPig + ClosedXML packages |

## NuGet Additions

- `PdfPig` - PDF text extraction
- `ClosedXML` - Excel generation
