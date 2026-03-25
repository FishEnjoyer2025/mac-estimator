# MAC Estimator - Publish & Release Script
# Usage: .\publish.ps1 -Version 1.0.0
# Requires: dotnet tool install -g vpk

param(
    [Parameter(Mandatory=$true)]
    [string]$Version
)

$ErrorActionPreference = "Stop"

Write-Host "Publishing MAC Estimator v$Version..." -ForegroundColor Cyan

# Clean publish
dotnet publish src/MacEstimator.App/MacEstimator.App.csproj `
    -c Release `
    -r win-x64 `
    --self-contained `
    -o ./publish

Write-Host "Packaging with Velopack..." -ForegroundColor Cyan

# Package with Velopack
vpk pack `
    --packId "MacEstimator" `
    --packVersion $Version `
    --packDir ./publish `
    --mainExe "MacEstimator.App.exe" `
    --outputDir ./releases

Write-Host ""
Write-Host "Done! Release files are in ./releases/" -ForegroundColor Green
Write-Host ""
Write-Host "To push to GitHub:" -ForegroundColor Yellow
Write-Host "  gh release create v$Version ./releases/* --title 'v$Version' --notes 'MAC Estimator v$Version'" -ForegroundColor White
