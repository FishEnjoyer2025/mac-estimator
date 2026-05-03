# mac-estimator

Millwork cost estimation app for shop quoting. Replaces the spreadsheet-on-someone's-desktop with a tool the whole shop can use without breaking pricing accuracy.

## Why this exists

MAC Cabinets quoted jobs from a spreadsheet that lived on one estimator's desktop and drifted out of date. New hires had no fast way to learn the rate logic. When the spreadsheet broke, quoting stopped.

mac-estimator fixes that.

## What it does

```
CabinetVision part export (CSV)
                +
Configurable rate card (labor + material)
                |
                v
   [ apply rates, compute totals ]
                |
                v
   Customer-facing PDF quote
```

Specifically:

1. Reads CabinetVision part exports (per-cabinet specs and counts)
2. Applies a labor rate card and a material rate card -- both owned by the shop manager, both versioned, both auditable
3. Computes per-line and total cost
4. Generates a customer-facing PDF quote with proper formatting and the shop's branding

## Impact

- Quotes go out same-day instead of next-day
- The rate card is reviewable and auditable -- traceable to "why was the quote what it was?"
- Onboarding a new estimator went from ~2 weeks to ~3 days
- The spreadsheet on one person's desktop is no longer the bottleneck

## Status

In use at MAC Cabinets.

## Stack

Python 3.10+ | CabinetVision part export parsing | PDF generation | Versioned config

---

Built by Dylan Cupples -- CAD designer who builds tooling for drafting teams.
