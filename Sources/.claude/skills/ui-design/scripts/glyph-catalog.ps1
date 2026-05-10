#!/usr/bin/env pwsh
<#
  glyph-catalog.ps1 — extract every Segoe MDL2 Assets glyph used in XAML and
  list it once with codepoint + the first nearby ToolTip / Header / x:Name we
  find. Useful to dedupe glyphs and reuse existing labels.
  Usage:
    glyph-catalog.ps1                    # default scan under repo Sources/
    glyph-catalog.ps1 -OutFile <path>
  Output:
    Glyphs: N unique  (-> <out>)
  The output file is a TSV: codepoint<TAB>count<TAB>example-context.
#>
[CmdletBinding()]
param(
    [string]$RepoRoot = (Resolve-Path "$PSScriptRoot\..\..\..\..\..").Path,
    [string]$OutFile  = (Join-Path $PSScriptRoot "..\data\glyphs.tsv"),
    [string]$ScanRoot = $null
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Core') { Write-Output "ERR=needs-pwsh7"; exit 3 }

if (-not $ScanRoot) { $ScanRoot = Join-Path $RepoRoot 'Sources' }
if (-not (Test-Path $ScanRoot)) { Write-Output "ERR=scan-root-missing"; exit 2 }

$xamlFiles = Get-ChildItem -Path $ScanRoot -Recurse -Filter '*.xaml' -ErrorAction SilentlyContinue |
             Where-Object { $_.FullName -notmatch '\\obj\\|\\bin\\' }

$entries = @{}  # codepoint -> @{ Count=N; Examples=[..] }

$mdl2Re   = [regex]'<TextBlock\s+[^>]*Text="(&#x([0-9A-Fa-f]{3,4});)"[^>]*FontFamily="Segoe MDL2 Assets"|<TextBlock\s+[^>]*FontFamily="Segoe MDL2 Assets"[^>]*Text="(&#x([0-9A-Fa-f]{3,4});)"'

foreach ($f in $xamlFiles) {
    $text = Get-Content -LiteralPath $f.FullName -Raw
    if ($text -notmatch 'Segoe MDL2 Assets') { continue }

    foreach ($m in $mdl2Re.Matches($text)) {
        $cp = if ($m.Groups[2].Value) { $m.Groups[2].Value.ToUpperInvariant() }
              else { $m.Groups[4].Value.ToUpperInvariant() }
        if (-not $entries.ContainsKey($cp)) {
            $entries[$cp] = [pscustomobject]@{ Count = 0; Examples = @() }
        }
        $entries[$cp].Count++
        if ($entries[$cp].Examples.Count -lt 2) {
            # Capture surrounding 80 chars to give context
            $ctx = $text.Substring($m.Index, [Math]::Min(120, $text.Length - $m.Index))
            $ctx = $ctx -replace '\s+',' '
            $rel = $f.FullName.Substring($RepoRoot.Length).TrimStart('\','/')
            $entries[$cp].Examples += "$rel  $($ctx.Substring(0,[Math]::Min(80,$ctx.Length)))"
        }
    }
}

$lines = @("codepoint`tcount`texample")
foreach ($cp in ($entries.Keys | Sort-Object)) {
    $row = $entries[$cp]
    $ex = if ($row.Examples.Count -gt 0) { $row.Examples[0] } else { '' }
    $lines += "U+$cp`t$($row.Count)`t$ex"
}
Set-Content -LiteralPath $OutFile -Value $lines -Encoding UTF8

"Glyphs: $($entries.Count) unique  (-> $OutFile)"
