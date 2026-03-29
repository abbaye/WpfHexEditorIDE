# validate-sdk.ps1 — Smoke-test WpfHexEditor.SDK NuGet package
# Creates a temp WPF project, references the local .nupkg, compiles a minimal plugin, verifies 0 errors.
# Usage: ./validate-sdk.ps1  (run after pack.ps1)

$ErrorActionPreference = "Stop"

$root      = $PSScriptRoot
$nugetDir  = Join-Path $root "..\artifacts\nuget"
$pkg       = Get-ChildItem $nugetDir -Filter "WpfHexEditor.SDK.*.nupkg" | Sort-Object LastWriteTime -Descending | Select-Object -First 1

if (-not $pkg) {
    Write-Error "No WpfHexEditor.SDK.*.nupkg found in $nugetDir — run pack.ps1 first."
    exit 1
}

# Extract version from filename: WpfHexEditor.SDK.2.0.0.nupkg
$version = ($pkg.BaseName -replace '^WpfHexEditor\.SDK\.', '')

$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "whide-sdk-validate-$(Get-Random)"
New-Item -ItemType Directory -Path $tempDir | Out-Null

try {
    # Create local NuGet.config pointing to our artifacts folder
    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="local" value="$($nugetDir -replace '\\','/')" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
"@ | Set-Content (Join-Path $tempDir "NuGet.config")

    # Create a minimal WPF project
    Push-Location $tempDir
    dotnet new wpf -n MinimalPlugin --no-restore --framework net8.0-windows | Out-Null
    Pop-Location

    $projDir = Join-Path $tempDir "MinimalPlugin"

    # Add the SDK package reference
    dotnet add (Join-Path $projDir "MinimalPlugin.csproj") package WpfHexEditor.SDK --version $version --no-restore | Out-Null

    # Write a minimal plugin that implements IPlugin
    @"
using WpfHexEditor.SDK;

namespace MinimalPlugin;

public sealed class TestPlugin : IPlugin
{
    public string Name        => "TestPlugin";
    public string Description => "Smoke-test plugin for SDK validation";
    public string Version     => "1.0.0";
    public string Author      => "Validator";

    public void Initialize(IIDEHostContext context) { }
    public void Shutdown()                          { }
}
"@ | Set-Content (Join-Path $projDir "TestPlugin.cs")

    Write-Host "Restoring..."
    dotnet restore (Join-Path $projDir "MinimalPlugin.csproj") --nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host "Building..."
    dotnet build (Join-Path $projDir "MinimalPlugin.csproj") -c Release --nologo -v minimal
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host "SDK validation PASSED — minimal plugin compiled successfully against WpfHexEditor.SDK $version."
}
finally {
    Remove-Item -Recurse -Force $tempDir -ErrorAction SilentlyContinue
}
