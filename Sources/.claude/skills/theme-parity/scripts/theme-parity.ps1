#!/usr/bin/env pwsh
<#
  theme-parity.ps1 — verify that every theme exposes the same key surface
  as the reference (Dark). Also checks color/brush pairing and *Theme.xaml
  merge order.
  Usage:
    theme-parity.ps1 -RefreshReference            # regenerate reference-keys.txt from Dark
    theme-parity.ps1 -Files <Colors.xaml...>     # check edited themes
    theme-parity.ps1 -Files <Colors.xaml> -SuggestPatch
#>
[CmdletBinding()]
param(
    [string[]]$Files,
    [switch]$RefreshReference,
    [switch]$SuggestPatch,
    [string]$ReferenceThemeName = 'Dark',
    [string]$RepoRoot = (Resolve-Path "$PSScriptRoot\..\..\..\..\..").Path,
    [string]$RefFile  = (Join-Path $PSScriptRoot "..\data\reference-keys.txt")
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Core') { Write-Output "ERR=needs-pwsh7"; exit 3 }

$colorRe    = [regex]'<Color\s+x:Key="([^"]+)"'
$brushAnyRe = [regex]'<SolidColorBrush\s+x:Key="([^"]+)"'

function Get-KeysFromXaml {
    param([string]$path)
    $text = Get-Content -LiteralPath $path -Raw
    $colors  = @($colorRe.Matches($text)  | ForEach-Object { $_.Groups[1].Value })
    $brushes = @($brushAnyRe.Matches($text) | ForEach-Object { $_.Groups[1].Value })
    return [pscustomobject]@{
        Colors  = ($colors  | Sort-Object -Unique)
        Brushes = ($brushes | Sort-Object -Unique)
        Text    = $text
    }
}

function Find-ColorsXaml {
    param([string]$themeName)
    $c1 = Join-Path $RepoRoot "Sources\Docking\WpfHexEditor.Docking.Wpf\Themes\$themeName\Colors.xaml"
    $c2 = Join-Path $RepoRoot "Sources\Shell\WpfHexEditor.Shell\Themes\$themeName\Colors.xaml"
    foreach ($c in @($c1, $c2)) { if (Test-Path $c) { return $c } }
    return $null
}

# ---------- RefreshReference mode ----------
if ($RefreshReference) {
    $refXaml = Find-ColorsXaml $ReferenceThemeName
    if (-not $refXaml) { Write-Output "ERR=reference-theme-not-found:$ReferenceThemeName"; exit 2 }
    $ref = Get-KeysFromXaml $refXaml
    $allKeys = ($ref.Colors + $ref.Brushes) | Sort-Object -Unique
    Set-Content -LiteralPath $RefFile -Value $allKeys -Encoding UTF8
    Write-Output "Reference: $ReferenceThemeName  keys=$($allKeys.Count)  (-> $RefFile)"
    if (-not $Files) { exit 0 }
}

if (-not $Files) { Write-Output "ERR=no-files"; exit 2 }

# Load reference key set
if (-not (Test-Path $RefFile)) {
    # First-time bootstrap from Dark
    $refXaml = Find-ColorsXaml $ReferenceThemeName
    if (-not $refXaml) { Write-Output "ERR=reference-theme-not-found:$ReferenceThemeName"; exit 2 }
    $ref = Get-KeysFromXaml $refXaml
    $allKeys = ($ref.Colors + $ref.Brushes) | Sort-Object -Unique
    Set-Content -LiteralPath $RefFile -Value $allKeys -Encoding UTF8
}
$referenceKeys = Get-Content -LiteralPath $RefFile | Where-Object { $_ -and $_.Trim() }
$refSet = [System.Collections.Generic.HashSet[string]]::new()
foreach ($k in $referenceKeys) { [void]$refSet.Add($k.Trim()) }

# ---------- inspect each provided file ----------
$themesScanned = 0
$totalIssues = 0

foreach ($f in $Files) {
    if (-not (Test-Path $f)) { Write-Output "  ${f}: missing"; continue }
    $abs = (Resolve-Path $f).Path
    $rel = $abs.Substring($RepoRoot.Length).TrimStart('\','/')
    $name = [IO.Path]::GetFileName($abs).ToLowerInvariant()

    # Theme-merge-order check (only on *Theme.xaml wrappers)
    if ($name -match '^[a-z0-9]+theme\.xaml$') {
        $text = Get-Content -LiteralPath $abs -Raw
        $merges = [regex]::Matches($text, '<ResourceDictionary\s+Source="[^"]*?([A-Za-z0-9_]+)\.xaml"')
        $idxColors = -1; $idxOther = -1; $i = 0
        foreach ($m in $merges) {
            $base = $m.Groups[1].Value.ToLowerInvariant()
            if ($base -eq 'colors')                                { if ($idxColors -lt 0) { $idxColors = $i } }
            elseif ($base -in @('menu','tabcontrol','contentcontrols','panelcommon')) { if ($idxOther -lt 0) { $idxOther = $i } }
            $i++
        }
        Write-Output "Theme wrapper: $rel"
        if ($idxColors -ge 0 -and $idxOther -ge 0 -and $idxColors -gt $idxOther) {
            Write-Output "  theme-merge-order  Colors.xaml merged after templates (must come first)"
            $totalIssues++
        } else {
            Write-Output "  Merge order: OK"
        }
        continue
    }

    if ($name -ne 'colors.xaml') { continue }
    $themesScanned++

    # Extract theme name from path
    $themeName = (Split-Path -Leaf (Split-Path -Parent $abs))

    $cur = Get-KeysFromXaml $abs
    $curAll = ($cur.Colors + $cur.Brushes) | Sort-Object -Unique

    $missing = @()
    foreach ($k in $referenceKeys) {
        if (-not ($curAll -contains $k)) { $missing += $k }
    }

    $orphans = @()
    foreach ($k in $curAll) {
        if (-not $refSet.Contains($k)) { $orphans += $k }
    }

    # Color/brush pairing
    $colorBase = $cur.Colors | ForEach-Object { ($_ -replace 'Color$','') } | Sort-Object -Unique
    $brushBase = $cur.Brushes | ForEach-Object { ($_ -replace 'Brush$','') } | Sort-Object -Unique
    $colorsNoBrush = $colorBase | Where-Object {
        $_.EndsWith('Color')  -eq $false -and ($brushBase -notcontains $_) -and
        ($cur.Colors -contains "${_}Color") -and ($cur.Brushes -notcontains "${_}Brush")
    }

    Write-Output "Theme: $themeName  ($rel)"
    Write-Output "  Keys: $($curAll.Count)  vs reference($ReferenceThemeName)=$($referenceKeys.Count)"
    if ($missing.Count -gt 0) {
        $sample = ($missing | Select-Object -First 8) -join ', '
        Write-Output "  theme-key-missing: $($missing.Count)  ($sample$(if ($missing.Count -gt 8) { ', ...' }))"
        $totalIssues += $missing.Count
    }
    if ($orphans.Count -gt 0) {
        $sample = ($orphans | Select-Object -First 5) -join ', '
        Write-Output "  theme-key-orphan: $($orphans.Count)  ($sample$(if ($orphans.Count -gt 5) { ', ...' }))"
    }
    if ($colorsNoBrush.Count -gt 0) {
        Write-Output "  theme-color-no-brush: $($colorsNoBrush.Count)"
        $totalIssues += $colorsNoBrush.Count
    }

    # Patch suggestion
    if ($SuggestPatch -and $missing.Count -gt 0) {
        $refXaml = Find-ColorsXaml $ReferenceThemeName
        if ($refXaml) {
            $refText = Get-Content -LiteralPath $refXaml -Raw
            Write-Output ""
            Write-Output "  --- patch suggestion (review before pasting) ---"
            foreach ($k in $missing | Select-Object -First 30) {
                $colorMatch = [regex]::Match($refText, "<Color\s+x:Key=`"$([regex]::Escape($k))`"[^>]*>([^<]+)</Color>")
                if ($colorMatch.Success) {
                    Write-Output "  <Color x:Key=`"$k`">$($colorMatch.Groups[1].Value)</Color>"
                } else {
                    $brushMatch = [regex]::Match($refText, "<SolidColorBrush\s+x:Key=`"$([regex]::Escape($k))`"[^>]*/?>")
                    if ($brushMatch.Success) {
                        Write-Output "  $($brushMatch.Value)"
                    }
                }
            }
            if ($missing.Count -gt 30) { Write-Output "  ... ($($missing.Count - 30) more)" }
        }
    }
}

if ($totalIssues -eq 0 -and $themesScanned -gt 0) { exit 0 }
if ($totalIssues -gt 0) { exit 1 }
exit 0
