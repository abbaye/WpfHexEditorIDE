#!/usr/bin/env pwsh
<#
  recall.ps1 — return up to N memory ids whose keywords match the user prompt.
  Usage:
    recall.ps1 -Keywords "webview2 markdown render" [-Top 3]
  Output:
    Recall: <id1>, <id2>, <id3>
  Or:
    Recall: (empty)
  If a referenced memory file is missing, appends "(warn: <missing-id>)".
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$Keywords,
    [int]$Top = 3,
    [string]$MapPath = (Join-Path $PSScriptRoot "..\data\keyword-map.json"),
    [string]$MemoryDir = "C:\Users\khens\.claude\projects\c--Users-khens-source-repos-WpfHexEditorControl\memory"
)
$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSEdition -ne 'Core') { Write-Output "ERR=needs-pwsh7"; exit 3 }
if (-not (Test-Path $MapPath))  { Write-Output "ERR=map-missing"; exit 2 }

$map = Get-Content -LiteralPath $MapPath -Raw | ConvertFrom-Json -AsHashtable

# Normalize: lowercase, strip diacritics, keep alnum + space + dash
$norm = $Keywords.ToLowerInvariant()
$norm = [Text.Encoding]::ASCII.GetString(
    [Text.Encoding]::GetEncoding('Cyrillic').GetBytes($norm)
)
# Simpler ASCII-fold via FormD normalization
$nf = [Text.NormalizationForm]::FormD
$sb = New-Object System.Text.StringBuilder
foreach ($c in $Keywords.ToLowerInvariant().Normalize($nf).ToCharArray()) {
    if ([Globalization.CharUnicodeInfo]::GetUnicodeCategory($c) -ne [Globalization.UnicodeCategory]::NonSpacingMark) {
        [void]$sb.Append($c)
    }
}
$norm = $sb.ToString()

# Score: each map-key found in $norm contributes its hits to its memory ids
$score = @{}
foreach ($key in $map.Keys) {
    if ($key.StartsWith('_')) { continue }
    if ($norm.Contains($key)) {
        foreach ($id in $map[$key]) {
            if (-not $score.ContainsKey($id)) { $score[$id] = 0 }
            $score[$id]++
        }
    }
}

if ($score.Count -eq 0) {
    Write-Output "Recall: (empty)"
    exit 0
}

$ranked = $score.GetEnumerator() | Sort-Object -Property Value -Descending |
          Select-Object -First $Top

$ids   = @()
$warns = @()
foreach ($kv in $ranked) {
    $id = $kv.Key
    $file = Join-Path $MemoryDir "$id.md"
    if (-not (Test-Path $file)) { $warns += $id }
    $ids += $id
}

$line = "Recall: " + ($ids -join ', ')
if ($warns.Count -gt 0) { $line += "  (warn: " + ($warns -join ', ') + ")" }
Write-Output $line
