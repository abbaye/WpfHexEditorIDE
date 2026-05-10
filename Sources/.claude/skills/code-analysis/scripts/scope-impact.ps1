#!/usr/bin/env pwsh
<#
  scope-impact.ps1 — list .cs / .xaml files that reference a symbol.
  Usage: scope-impact.ps1 -Symbol HexByte -MaxFiles 50
  Output:
    Refs=N | Modules=A,B,C | CrossBoundary=true|false
    <module>: <file>
    ...
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$Symbol,
    [string]$RepoRoot = (Resolve-Path "$PSScriptRoot\..\..\..\..\..").Path,
    [int]$MaxFiles = 50
)
$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSEdition -ne 'Core') { Write-Output "ERR=needs-pwsh7"; exit 3 }
if ($Symbol -notmatch '^[A-Za-z_]\w*$') { Write-Output "ERR=bad-symbol"; exit 2 }

$rgCmd = Get-Command rg -ErrorAction SilentlyContinue
if (-not $rgCmd) { Write-Output "ERR=rg-not-found"; exit 4 }
$rgArgs = @('--no-messages','-l','--type','cs','--type','xml',"\b$Symbol\b",$RepoRoot)
$files  = & rg @rgArgs 2>$null
if (-not $files) { Write-Output "Refs=0 | Modules= | CrossBoundary=false"; exit 0 }

$files = $files | Sort-Object -Unique | Select-Object -First $MaxFiles
$rows  = foreach ($f in $files) {
    $rel = $f.Substring($RepoRoot.Length).TrimStart('\','/')
    $mod = switch -Wildcard ($rel) {
        'Sources\WpfHexEditor.App\*'         { 'App' ; break }
        'Sources\WpfHexEditor.SDK\*'         { 'SDK' ; break }
        'Sources\Editor.Core\*'              { 'Editor.Core' ; break }
        'Sources\WPFHexaEditor\*'            { 'HexControl' ; break }
        'Sources\WpfHexEditor.HexEditor*\*'  { 'HexEditor' ; break }
        'Sources\WpfHexEditor.CodeEditor*\*' { 'CodeEditor' ; break }
        'Sources\WpfHexEditor.Plugins.*\*'   { 'Plugin' ; break }
        'Sources\Editors\*'                  { 'Editors' ; break }
        'Sources\Services\*'                 { 'Services' ; break }
        default                              { 'Other' }
    }
    [pscustomobject]@{ Module=$mod; Rel=$rel }
}

$modules = ($rows.Module | Sort-Object -Unique) -join ','
$cross   = ($rows.Module | Sort-Object -Unique).Count -gt 1
"Refs=$($rows.Count) | Modules=$modules | CrossBoundary=$cross"
foreach ($r in ($rows | Sort-Object Module, Rel)) {
    "  $($r.Module): $($r.Rel)"
}
