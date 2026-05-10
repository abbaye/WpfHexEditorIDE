#!/usr/bin/env pwsh
<#
  pre-edit-check.ps1 — quick metrics on a file before Claude edits it.
  Output is a single dense line designed for low-token LLM ingestion:
    LOC=N | Funcs>25=K | Class>300=M | Callers=C | Module=<id> | Risk=LOW|MED|HIGH
  Risk: HIGH if Class>300 OR Funcs>25>=3, MED if Callers>=20 OR Funcs>25>=1, else LOW.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$File,
    [string]$RepoRoot = (Resolve-Path "$PSScriptRoot\..\..\..\..\..").Path
)
$ErrorActionPreference = 'Stop'

if (-not (Test-Path $File)) { Write-Output "ERR=missing:$File"; exit 2 }
if ($PSVersionTable.PSEdition -ne 'Core') { Write-Output "ERR=needs-pwsh7"; exit 3 }

$abs    = (Resolve-Path $File).Path
$lines  = Get-Content -LiteralPath $abs
$loc    = ($lines | Where-Object { $_ -match '\S' -and $_ -notmatch '^\s*//' }).Count
$ext    = [IO.Path]::GetExtension($abs).ToLowerInvariant()

# Module detection from path under Sources\
$rel    = $abs.Substring($RepoRoot.Length).TrimStart('\','/')
$module = switch -Wildcard ($rel) {
    'Sources\WpfHexEditor.App\*'           { 'App' ; break }
    'Sources\WpfHexEditor.SDK\*'           { 'SDK' ; break }
    'Sources\Editor.Core\*'                { 'Editor.Core' ; break }
    'Sources\WPFHexaEditor\*'              { 'HexControl' ; break }
    'Sources\WpfHexEditor.HexEditor*\*'    { 'HexEditor' ; break }
    'Sources\WpfHexEditor.CodeEditor*\*'   { 'CodeEditor' ; break }
    'Sources\WpfHexEditor.Plugins.*\*'     { 'Plugin' ; break }
    'Sources\Editors\*'                    { 'Editors' ; break }
    'Sources\Services\*'                   { 'Services' ; break }
    default                                { 'Unknown' }
}

# Function-length scan (C# only — count {} blocks of methods)
$funcsOver25 = 0
if ($ext -in '.cs') {
    $text = [string]::Join("`n", $lines)
    # Match a method signature followed by { ... } at brace-depth 1.
    $methodRegex = [regex]'(?m)^\s*(public|private|protected|internal|static|async|override|virtual|sealed|new|partial|\s)+\s+[A-Za-z_][\w<>,\s\?\[\]]*\s+([A-Za-z_]\w*)\s*\([^)]*\)\s*(?:where[^{]+)?\{'
    foreach ($m in $methodRegex.Matches($text)) {
        $i = $m.Index + $m.Length
        $depth = 1; $start = $i; $bodyLines = 0
        while ($i -lt $text.Length -and $depth -gt 0) {
            $c = $text[$i]
            if ($c -eq '{') { $depth++ }
            elseif ($c -eq '}') { $depth-- }
            elseif ($c -eq "`n") { $bodyLines++ }
            $i++
        }
        if ($bodyLines -gt 25) { $funcsOver25++ }
    }
}

# Class-size: any top-level type whose body exceeds 300 lines
$classOver300 = 0
if ($ext -in '.cs') {
    $text = [string]::Join("`n", $lines)
    $typeRegex = [regex]'(?m)^\s*(public|internal|abstract|sealed|partial|static|\s)+\s+(class|record|struct|interface)\s+[A-Za-z_]\w*'
    foreach ($m in $typeRegex.Matches($text)) {
        $bracePos = $text.IndexOf('{', $m.Index)
        if ($bracePos -lt 0) { continue }
        $i = $bracePos + 1; $depth = 1; $bodyLines = 0
        while ($i -lt $text.Length -and $depth -gt 0) {
            $c = $text[$i]
            if ($c -eq '{') { $depth++ }
            elseif ($c -eq '}') { $depth-- }
            elseif ($c -eq "`n") { $bodyLines++ }
            $i++
        }
        if ($bodyLines -gt 300) { $classOver300++ }
    }
}

# Callers via primary type name (best-effort, capped). Skip if rg unavailable.
$callers = -1
$typeName = [IO.Path]::GetFileNameWithoutExtension($abs) -replace '\.(xaml|g|i|generated)$',''
$rgCmd = Get-Command rg -ErrorAction SilentlyContinue
if ($rgCmd -and $typeName -match '^[A-Za-z_]\w*$') {
    $rgArgs = @('--no-messages','--type','cs','-l',"\b$typeName\b",$RepoRoot)
    $hits = & rg @rgArgs 2>$null
    if ($LASTEXITCODE -le 1) {
        $callers = if ($hits) { ($hits | Measure-Object).Count - 1 } else { 0 }
        if ($callers -lt 0) { $callers = 0 }
    } else { $callers = 0 }
}
$callersStr = if ($callers -lt 0) { 'n/a' } else { $callers.ToString() }

$risk = 'LOW'
if ($classOver300 -gt 0 -or $funcsOver25 -ge 3) { $risk = 'HIGH' }
elseif (($callers -ge 20) -or ($funcsOver25 -ge 1))  { $risk = 'MED' }

"LOC=$loc | Funcs>25=$funcsOver25 | Class>300=$classOver300 | Callers=$callersStr | Module=$module | Risk=$risk"
