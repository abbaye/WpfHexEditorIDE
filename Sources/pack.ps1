# pack.ps1 — Build and pack WpfHexEditor.SDK to artifacts/nuget/
# Usage: ./pack.ps1 [-Configuration Release]

param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$root      = $PSScriptRoot
$sdkProj   = Join-Path $root "WpfHexEditor.SDK\WpfHexEditor.SDK.csproj"
$outputDir = Join-Path $root "..\artifacts\nuget"

if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

Write-Host "Building $Configuration..."
dotnet build $sdkProj -c $Configuration --nologo -v minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Packing to $outputDir..."
dotnet pack $sdkProj -c $Configuration --no-build --nologo -o $outputDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$pkg = Get-ChildItem $outputDir -Filter "WpfHexEditor.SDK.*.nupkg" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Write-Host "Package: $($pkg.FullName)"
Write-Host "Done."
