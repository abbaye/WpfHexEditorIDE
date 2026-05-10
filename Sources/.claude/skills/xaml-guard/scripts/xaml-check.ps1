#!/usr/bin/env pwsh
<#
  xaml-check.ps1 — apply 10 regression rules on edited XAML files.
  Usage: xaml-check.ps1 -Files a.xaml,b.xaml [-RepoRoot ...]
  Output:
    XAML: <count> <rules-summary> | LocCoverage=<pct>%
      <file>:<line>  <rule>  <snippet>
    or
    OK
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string[]]$Files,
    [string]$RepoRoot = (Resolve-Path "$PSScriptRoot\..\..\..\..\..").Path
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Core') { Write-Output "ERR=needs-pwsh7"; exit 3 }

# Whitelist of "technical" attribute values that are NOT user-visible strings
$technicalAttrPattern = '^(\s*)?(\{[^}]+\}|x:|\$|#|System\.|0|1|true|false|None|\d|[A-Z][a-z]+([A-Z][a-z]+)+\s*$|Segoe|Consolas|Arial)'
$techAttrNames = @('x:Key','Style','Template','TargetType','Property','Path','Source','Binding','Color','Brush','FontFamily','x:Name','x:Class','xmlns','Storyboard.TargetProperty','RoutedEvent','Tag','Background','Foreground','BorderBrush','Fill','Stroke','Stretch','Visibility','HorizontalAlignment','VerticalAlignment','Orientation')

$violations = New-Object System.Collections.Generic.List[psobject]
$dynCount = 0
$attrTextCount = 0

function Add-V {
    param($file,$line,$rule,$snippet)
    $violations.Add([pscustomobject]@{
        File = $file; Line = $line; Rule = $rule; Snippet = $snippet
    })
}

function Get-FileLines {
    param([string]$path)
    return Get-Content -LiteralPath $path
}

foreach ($f in $Files) {
    if (-not (Test-Path $f)) { Add-V $f 0 'missing-file' ''; continue }
    $abs = (Resolve-Path $f).Path
    $rel = $abs.Substring($RepoRoot.Length).TrimStart('\','/')
    $ext = [IO.Path]::GetExtension($abs).ToLowerInvariant()
    if ($ext -ne '.xaml') { continue }

    $lines = Get-FileLines $abs
    $isHexEditor = $rel -match 'HexEditor'

    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        $lineNo = $i + 1

        # Rule: patcher-corruption — attribute value with no name (e.g. ="{DynamicResource X}")
        if ($line -match '(?<![\w:])="\s*\{DynamicResource ') {
            Add-V $rel $lineNo 'patcher-corruption' $line.Trim()
        }

        # Rule: xmlns-mangle — xmlns:alias:Class shape
        if ($line -match '\bxmlns:[A-Za-z_]\w*:[A-Z]') {
            Add-V $rel $lineNo 'xmlns-mangle' $line.Trim()
        }

        # Rule: messagebox-show
        if ($line -match '\bMessageBox\.Show\s*\(') {
            Add-V $rel $lineNo 'messagebox-show' $line.Trim()
        }

        # Rule: static-color-no-alpha — ContextMenu / Background with #RRGGBB only
        if ($line -match 'Background="#[0-9A-Fa-f]{6}"' -and $line -notmatch 'Background="#[0-9A-Fa-f]{8}"') {
            # Only flag if context contains ContextMenu/MenuItem
            if ($line -match 'ContextMenu|MenuItem' -or
                ($i -ge 1 -and $lines[$i-1] -match 'ContextMenu|MenuItem')) {
                Add-V $rel $lineNo 'static-color-no-alpha' $line.Trim()
            }
        }

        # Rule: dockpanel-row-grid — DockPanel inside a MenuItem template/row
        if ($line -match '<DockPanel' -and ($i -ge 2)) {
            $window = ($lines[[Math]::Max(0,$i-3)..$i] -join "`n")
            if ($window -match 'MenuItem|TabItem.*Header') {
                Add-V $rel $lineNo 'dockpanel-row-grid' $line.Trim()
            }
        }

        # Rule: hexeditor-resources — Resources.X without L10n alias
        if ($isHexEditor -and $line -match '\bResources\.[A-Z]\w+' -and $line -notmatch '\bL10n\b') {
            # Skip if it's a using/import context (handled in code-behind, not XAML mostly)
            Add-V $rel $lineNo 'hexeditor-resources' $line.Trim()
        }

        # Rule: hardcoded-text / missing-dynamic — count attrs and look for user-visible literals
        # Match attributes of interest with literal value starting with capital letter
        $attrMatches = [regex]::Matches($line, '\b(Text|Header|Title|Content|Tooltip|ToolTip|Watermark|PlaceholderText|Description)\s*=\s*"([^"]*)"')
        foreach ($m in $attrMatches) {
            $attrName = $m.Groups[1].Value
            $val = $m.Groups[2].Value
            if (-not $val) { continue }
            $attrTextCount++

            $isDynamic = $val.StartsWith('{')
            if ($isDynamic) { $dynCount++ ; continue }

            # Skip purely numeric / single-char / ASCII symbols
            if ($val.Length -lt 3) { continue }
            if ($val -match '^[\d\s\.,:;\-/\\#\$%&\*\+=]+$') { continue }
            # Skip technical-looking values
            if ($val -match '^[a-z_\-\.]+$') { continue }      # all-lowercase = key/path
            if ($val -match '^\$\{.+\}$') { continue }          # template
            if ($val -match '^[A-Z]+$') { continue }            # all-caps acronym
            # Glyphs (Segoe Fluent)
            if ($val.Length -le 2 -and ($val[0] -gt [char]0xE000)) { continue }

            # If the value contains a space and starts with capital, almost certainly user-visible
            if ($val -match '^[A-Z].*\s' -or $val -match '^[A-Z][a-z]') {
                Add-V $rel $lineNo 'hardcoded-text' "$attrName=`"$val`""
            }
        }

        # Count dynamics in any attribute (rough loc-coverage signal)
        $dynAll = [regex]::Matches($line, '"\{DynamicResource ').Count
        if ($dynAll -gt 0 -and $attrMatches.Count -eq 0) { $dynCount += $dynAll }
    }
}

# Compute coverage (attrs of interest only)
$coverage = if ($attrTextCount -gt 0) {
    [int](100.0 * $dynCount / [Math]::Max(1, $attrTextCount + $dynCount))
} else { 100 }

if ($violations.Count -eq 0) {
    if ($attrTextCount -gt 0 -or $dynCount -gt 0) {
        Write-Output "OK | LocCoverage=$coverage%"
    } else {
        Write-Output "OK"
    }
    exit 0
}

# Summarize: count by rule
$summary = ($violations | Group-Object Rule |
            Sort-Object Count -Descending |
            ForEach-Object { "$($_.Count) $($_.Name)" }) -join ', '

Write-Output "XAML: $summary | LocCoverage=$coverage%"
foreach ($v in $violations | Sort-Object File, Line) {
    "  $($v.File):$($v.Line)  $($v.Rule)  $($v.Snippet)"
}
exit 1
