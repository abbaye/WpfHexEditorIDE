#!/usr/bin/env pwsh
<#
  token-catalog.ps1 — scan all Themes/**/Colors.xaml and produce
  data/known-tokens.json with the union of <Color> and <SolidColorBrush>
  x:Key values plus the list of detected themes.
  Usage:
    token-catalog.ps1                       # regenerate at default location
    token-catalog.ps1 -RepoRoot <path>
    token-catalog.ps1 -OutFile <path>
  Output:
    Tokens: brushes=N | colors=M | themes=K  (-> <out>)
#>
[CmdletBinding()]
param(
    [string]$RepoRoot = (Resolve-Path "$PSScriptRoot\..\..\..\..\..").Path,
    [string]$OutFile  = (Join-Path $PSScriptRoot "..\data\known-tokens.json")
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Core') { Write-Output "ERR=needs-pwsh7"; exit 3 }

$themesRoot = @()
foreach ($root in @(
    (Join-Path $RepoRoot 'Sources\Shell\WpfHexEditor.Shell\Themes'),
    (Join-Path $RepoRoot 'Sources\Docking\WpfHexEditor.Docking.Wpf\Themes'),
    (Join-Path $RepoRoot 'Sources\WpfHexEditor.App\Themes')
)) {
    if (Test-Path $root) { $themesRoot += $root }
}

$colorFiles = @()
foreach ($root in $themesRoot) {
    $colorFiles += Get-ChildItem -Path $root -Recurse -Filter 'Colors.xaml' -ErrorAction SilentlyContinue
    # Also include other top-level theme dictionaries that define keys
    $colorFiles += Get-ChildItem -Path $root -Filter '*.xaml' -ErrorAction SilentlyContinue |
                   Where-Object { $_.Name -ne 'Colors.xaml' }
}
$colorFiles = $colorFiles | Sort-Object FullName -Unique

$brushes = New-Object System.Collections.Generic.HashSet[string]
$colors  = New-Object System.Collections.Generic.HashSet[string]
$themes  = New-Object System.Collections.Generic.HashSet[string]

$colorRe  = [regex]'<Color\s+x:Key="([^"]+)"'
$brushRe  = [regex]'<SolidColorBrush\s+x:Key="([^"]+)"'
$lgBrushRe= [regex]'<LinearGradientBrush\s+x:Key="([^"]+)"'

foreach ($f in $colorFiles) {
    $rel = $f.FullName.Substring($RepoRoot.Length).TrimStart('\','/')
    # Theme name = parent folder of the file unless file is *Theme.xaml
    if ($f.Name -match '^(.+)Theme\.xaml$') { [void]$themes.Add($Matches[1]) }
    else {
        $parent = Split-Path -Leaf (Split-Path -Parent $f.FullName)
        if ($parent -ne 'Themes') { [void]$themes.Add($parent) }
    }

    $text = Get-Content -LiteralPath $f.FullName -Raw
    foreach ($m in $colorRe.Matches($text))   { [void]$colors.Add($m.Groups[1].Value) }
    foreach ($m in $brushRe.Matches($text))   { [void]$brushes.Add($m.Groups[1].Value) }
    foreach ($m in $lgBrushRe.Matches($text)) { [void]$brushes.Add($m.Groups[1].Value) }
}

$obj = [ordered]@{
    generated = (Get-Date -Format 'yyyy-MM-ddTHH:mm:ssZ')
    brushes   = ($brushes | Sort-Object)
    colors    = ($colors  | Sort-Object)
    themes    = ($themes  | Sort-Object)
}
$json = $obj | ConvertTo-Json -Depth 4
Set-Content -LiteralPath $OutFile -Value $json -Encoding UTF8

"Tokens: brushes=$($brushes.Count) | colors=$($colors.Count) | themes=$($themes.Count)  (-> $OutFile)"
