#!/usr/bin/env pwsh
<#
  ui-check.ps1 — apply 6 design-system rules on edited XAML files.
  Skips Themes/*.xaml (themes are allowed to hardcode colors — that's their job).
  Usage:
    ui-check.ps1 -Files a.xaml,b.xaml
  Output:
    UI: <summary> | TokenCoverage=<pct>%
      <file>:<line>  <rule>  <snippet>
    or
    OK
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string[]]$Files,
    [string]$RepoRoot   = (Resolve-Path "$PSScriptRoot\..\..\..\..\..").Path,
    [string]$TokensPath = (Join-Path $PSScriptRoot "..\data\known-tokens.json")
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Core') { Write-Output "ERR=needs-pwsh7"; exit 3 }
if (-not (Test-Path $TokensPath)) { Write-Output "ERR=tokens-missing run token-catalog.ps1"; exit 2 }

$tokens = Get-Content -LiteralPath $TokensPath -Raw | ConvertFrom-Json
$brushSet = [System.Collections.Generic.HashSet[string]]::new()
foreach ($b in $tokens.brushes) { [void]$brushSet.Add($b) }
foreach ($c in $tokens.colors)  { [void]$brushSet.Add($c) }

# Canonical numeric scales
$canonicalSpacings = @(0,1,2,3,4,5,6,8,10,12,14,16,18,20,22,24,26,28,30,32,40,48,56,64)
$canonicalFontSizes = @(9,10,11,12,13,14,16,18,20,22,24,28,32,36,40,48)

# Path skip / whitelist
$skipPathRe = '\\Themes\\|\\ColorPicker\\|\.g\.cs$|\.g\.i\.cs$'

# Rules state
$violations = New-Object System.Collections.Generic.List[psobject]
$totalDynamicRefs = 0
$totalColorRefs   = 0

function Add-V {
    param($file,$line,$rule,$snippet,$severity='warn')
    $violations.Add([pscustomobject]@{
        File = $file; Line = $line; Rule = $rule; Snippet = $snippet.Trim(); Severity = $severity
    })
}

function Test-IsSpacingCanonical {
    param([string]$value)
    $parts = $value -split ','
    foreach ($p in $parts) {
        $n = 0
        if (-not [int]::TryParse($p.Trim(), [ref]$n)) { return $true } # non-numeric (binding) -> skip
        if ($canonicalSpacings -notcontains $n) { return $false }
    }
    return $true
}

foreach ($f in $Files) {
    if (-not (Test-Path $f)) { continue }
    $abs = (Resolve-Path $f).Path
    $rel = $abs.Substring($RepoRoot.Length).TrimStart('\','/')
    if ($rel -match $skipPathRe) { continue }
    if ([IO.Path]::GetExtension($abs).ToLowerInvariant() -ne '.xaml') { continue }

    $lines = Get-Content -LiteralPath $abs

    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        $lineNo = $i + 1

        # Rule: hardcoded-color
        $hcMatches = [regex]::Matches($line, '(Background|Foreground|BorderBrush|Fill|Stroke)="(#[0-9A-Fa-f]{6,8})"')
        foreach ($m in $hcMatches) {
            $totalColorRefs++
            Add-V $rel $lineNo 'hardcoded-color' "$($m.Groups[1].Value)=`"$($m.Groups[2].Value)`"" 'error'
        }

        # Rule: unknown-token
        $dynMatches = [regex]::Matches($line, '\{DynamicResource\s+([A-Za-z_][\w\.]*)\s*\}')
        foreach ($m in $dynMatches) {
            $totalDynamicRefs++
            $key = $m.Groups[1].Value
            if (-not $brushSet.Contains($key)) {
                # Allow keys that are localized strings (look like *_Name) or non-color tokens
                # Heuristic: only flag keys ending in Brush/Color/Background/Foreground/Border etc.
                if ($key -match '(Brush|Color|Background|Foreground|Border|Fill|Stroke|Highlight|Accent)$') {
                    Add-V $rel $lineNo 'unknown-token' "{DynamicResource $key}" 'error'
                }
            }
        }
        # Same for StaticResource (color/brush pattern)
        $staticMatches = [regex]::Matches($line, '\{StaticResource\s+([A-Za-z_][\w\.]*)\s*\}')
        foreach ($m in $staticMatches) {
            $key = $m.Groups[1].Value
            if (-not $brushSet.Contains($key)) {
                if ($key -match '(Brush|Color)$') {
                    # Often legitimate (local style ref); only error if appears as a brush attribute
                    if ($line -match '(Background|Foreground|BorderBrush|Fill|Stroke)="\{StaticResource') {
                        Add-V $rel $lineNo 'unknown-token' "{StaticResource $key}" 'error'
                    }
                }
            }
        }

        # Rule: non-canonical-spacing (Padding/Margin)
        $spMatches = [regex]::Matches($line, '\b(Padding|Margin)="(-?\d+(?:,-?\d+){0,3})"')
        foreach ($m in $spMatches) {
            $val = $m.Groups[2].Value
            if (-not (Test-IsSpacingCanonical $val)) {
                Add-V $rel $lineNo 'non-canonical-spacing' "$($m.Groups[1].Value)=`"$val`""
            }
        }

        # Rule: non-canonical-fontsize
        $fsMatches = [regex]::Matches($line, '\bFontSize="(\d+)"')
        foreach ($m in $fsMatches) {
            $n = [int]$m.Groups[1].Value
            if ($canonicalFontSizes -notcontains $n) {
                Add-V $rel $lineNo 'non-canonical-fontsize' "FontSize=`"$n`""
            }
        }

        # Rule: glyph-no-tooltip — flag glyph used AS the visible content of an interactive
        # element that has no other accessible name. Skip:
        #   - decorative icon slots (MenuItem.Icon / Button.Icon / etc.)
        #   - PanelIconButtonStyle / PanelIconToggleStyle (a11y handled by style)
        #   - parent has ToolTip / AutomationProperties.Name
        #   - sibling text element (TextBlock Text= or Run) inside the same interactive parent
        if ($line -match 'FontFamily="Segoe MDL2 Assets"') {
            $startBack = [Math]::Max(0, $i - 12)
            $back = ($lines[$startBack..$i] -join ' ')
            $isInIconSlot = $back -match '<(MenuItem|Button|RibbonButton|ToggleButton)\.Icon\s*>'
            if (-not $isInIconSlot) {
                $parentMatch = [regex]::Match($back, '<(Button|ToggleButton|RepeatButton|Hyperlink)\b[^/>]*>(?![\s\S]*</\1>)')
                if ($parentMatch.Success) {
                    $parentTag = $parentMatch.Value
                    $hasHeader  = $parentTag -match '\bHeader='
                    $hasContent = $parentTag -match '\bContent='
                    $hasA11y    = ($parentTag -match 'ToolTip=|AutomationProperties\.Name=') -or
                                  ($back -match 'ToolTip=|AutomationProperties\.Name=')
                    $usesPanelIcon = $parentTag -match 'Style="\{(Static|Dynamic)Resource\s+PanelIcon(Button|Toggle)'
                    # Look forward up to 12 lines for a sibling TextBlock Text= or Run providing the visible label
                    $endFwd = [Math]::Min($lines.Count - 1, $i + 12)
                    $forward = ($lines[$i..$endFwd] -join ' ')
                    $hasSiblingText = $forward -match '<TextBlock\s[^>]*Text="(?!&#x)' -or $forward -match '<Run\s[^>]*>[^<]+</Run>'
                    if (-not $hasA11y -and -not $usesPanelIcon -and -not $hasHeader -and -not $hasContent -and -not $hasSiblingText) {
                        Add-V $rel $lineNo 'glyph-no-tooltip' 'glyph-only interactive element without ToolTip / AutomationProperties.Name'
                    }
                }
            }
        }

        # Rule: reinvented-style — local <Style TargetType="Button|CheckBox|TextBox|ComboBox"> without BasedOn
        if ($line -match '<Style\s+TargetType="(Button|CheckBox|TextBox|ComboBox|ListBoxItem)"\s*>') {
            # Look back 1 line and forward 8 lines for a Setter Property="Template" without BasedOn on signature
            $sig = $line
            $window = ($lines[$i..([Math]::Min($lines.Count-1,$i+8))] -join ' ')
            if ($sig -notmatch 'BasedOn=' -and $window -match 'Setter\s+Property="Template"') {
                Add-V $rel $lineNo 'reinvented-style' $line.Trim()
            }
        }
    }
}

$tokenCoverage = if (($totalColorRefs + $totalDynamicRefs) -gt 0) {
    [int](100.0 * $totalDynamicRefs / [Math]::Max(1,$totalColorRefs + $totalDynamicRefs))
} else { 100 }

if ($violations.Count -eq 0) {
    if (($totalColorRefs + $totalDynamicRefs) -gt 0) {
        Write-Output "OK | TokenCoverage=$tokenCoverage%"
    } else { Write-Output 'OK' }
    exit 0
}

$summary = ($violations | Group-Object Rule | Sort-Object Count -Descending |
            ForEach-Object { "$($_.Count) $($_.Name)" }) -join ', '

Write-Output "UI: $summary | TokenCoverage=$tokenCoverage%"
foreach ($v in $violations | Sort-Object File, Line) {
    "  $($v.File):$($v.Line)  $($v.Rule)  $($v.Snippet)"
}
exit 1
