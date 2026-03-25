# MAC Estimator - One-click release script
# Usage: .\release.ps1
# Does everything: bumps version, builds, packages, pushes to GitHub

$ErrorActionPreference = "Stop"

# Read current version from csproj
$csproj = "src/MacEstimator.App/MacEstimator.App.csproj"
$xml = [xml](Get-Content $csproj)
$currentVersion = $xml.Project.PropertyGroup.Version
Write-Host "Current version: $currentVersion" -ForegroundColor Cyan

# Auto-bump patch version
$parts = $currentVersion.Split('.')
$parts[2] = [int]$parts[2] + 1
$newVersion = $parts -join '.'
Write-Host "New version:     $newVersion" -ForegroundColor Green

# Update csproj
$content = Get-Content $csproj -Raw
$content = $content -replace "<Version>$currentVersion</Version>", "<Version>$newVersion</Version>"
Set-Content $csproj $content -NoNewline

# Build
Write-Host "`nBuilding..." -ForegroundColor Cyan
dotnet publish $csproj -c Release -r win-x64 --self-contained -o ./publish
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

# Package
Write-Host "`nPackaging with Velopack..." -ForegroundColor Cyan
vpk pack --packId "MacEstimator" --packVersion $newVersion --packDir ./publish --mainExe "MacEstimator.App.exe" --outputDir ./releases
if ($LASTEXITCODE -ne 0) { throw "Packaging failed" }

# Commit version bump
Write-Host "`nCommitting version bump..." -ForegroundColor Cyan
git add $csproj
git commit -m "Bump version to $newVersion"
git push

# Push release to GitHub
Write-Host "`nCreating GitHub release..." -ForegroundColor Cyan
gh release create "v$newVersion" ./releases/* --title "v$newVersion" --notes "MAC Estimator v$newVersion" --latest

Write-Host "`nDone! v$newVersion is live." -ForegroundColor Green
Write-Host "Users will get the update next time they open the app." -ForegroundColor Yellow
