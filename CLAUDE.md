# MAC Estimator

WPF/.NET 8 bidding tool for MAC Cabinets. Generates commercial cabinet estimates from room dimensions and specs.

## ⚠️ Construction source of truth (shared) — read first
MAC's construction constants, Cabinet Vision UCS construction methods, material specs, library IDs, and cabinet patterns are canonical in **`G:\My Drive\MAC\zIndex\MAC-Construction-Standards.md`** (machine-readable: `mac-construction-standards.json`). The same file feeds CabMan and CV UCS authoring. **Don't hardcode or re-derive specs here** — pull base height (32.5" commercial / 34.5" residential·ADA·lobby), depths (base 24", upper 30×13), width-split caps (base 36"/upper 48"), and materials (frameless 32mm, PLAM exterior, white melamine interior, 3/4" box, 1/4" back) from there so bids match what the shop actually builds.

## Build & Deploy
- Build: `dotnet build MacEstimator.sln`
- Publish: `.\publish.ps1 -Version X.Y.Z` — dotnet publish, self-contained win-x64
- Release: `.\release.ps1` — auto-bumps patch version, builds, packages with Velopack, pushes to GitHub Releases
- Users get auto-updates via Velopack (checks GitHub Releases on startup)

## Structure
```
src/MacEstimator.App/     # Main WPF application
publish/                   # Published artifacts
releases/                  # Velopack release packages
publish.ps1               # Manual publish script
release.ps1               # One-click release (bump + build + package + push)
```

## Conventions
- Same MVVM patterns as CabMan (CommunityToolkit.Mvvm)
- MAC Cabinets commercial specs: frameless/Euro 32mm, PLAM exterior, white melamine interior
- Standard pricing and material catalogs built into the app
- Target audience: Rusty (PM) and estimating staff — keep UI simple
