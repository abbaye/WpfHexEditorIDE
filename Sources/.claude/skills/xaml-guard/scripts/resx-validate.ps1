#!/usr/bin/env pwsh
<#
  resx-validate.ps1 — verify a .resx parses as valid XML and that its keys
  match the neighbouring *Resources.Designer.cs (parity).
  Usage: resx-validate.ps1 -File AppResources.resx [-RepoRoot ...]
  Output:
    RESX OK | keys=N
    or
    RESX <issue>: <details>
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$File,
    [string]$RepoRoot = (Resolve-Path "$PSScriptRoot\..\..\..\..\..").Path
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Core') { Write-Output "ERR=needs-pwsh7"; exit 3 }
if (-not (Test-Path $File)) { Write-Output "RESX missing: $File"; exit 2 }

$abs = (Resolve-Path $File).Path
$rel = $abs.Substring($RepoRoot.Length).TrimStart('\','/')

# 1) Parse XML
try {
    [xml]$doc = Get-Content -LiteralPath $abs -Raw
} catch {
    Write-Output "RESX malformed: $rel  $($_.Exception.Message)"
    exit 1
}

# 2) Verify the schema/header is present (root <root> with <data> children)
if ($doc.DocumentElement.Name -ne 'root') {
    Write-Output "RESX malformed: $rel  root element=$($doc.DocumentElement.Name) (expected 'root')"
    exit 1
}

$dataNodes = @($doc.SelectNodes('//data[@name]'))
$keys = $dataNodes | ForEach-Object { $_.GetAttribute('name') } | Sort-Object -Unique

# 3) Designer.cs parity (best-effort — only base resx, not satellites)
$baseName = [IO.Path]::GetFileNameWithoutExtension($abs)
$dir = [IO.Path]::GetDirectoryName($abs)
$designer = Join-Path $dir "$baseName.Designer.cs"
$missingInDesigner = @()
if ((Test-Path $designer) -and ($baseName -notmatch '\.[a-z]{2}(-[A-Z]{2})?$')) {
    $designerText = Get-Content -LiteralPath $designer -Raw
    foreach ($k in $keys) {
        # Designer property is the key with non-identifier chars stripped
        $propName = $k -replace '[^A-Za-z0-9_]', '_'
        $pat = '\b(public|internal)\s+static\s+string\s+' + [regex]::Escape($propName) + '\b'
        if ($designerText -notmatch $pat) {
            $missingInDesigner += $k
        }
    }
}

if ($missingInDesigner.Count -gt 0) {
    $sample = ($missingInDesigner | Select-Object -First 5) -join ', '
    Write-Output "RESX designer-drift: $rel  $($missingInDesigner.Count) keys not in Designer.cs (e.g. $sample)"
    exit 1
}

Write-Output "RESX OK | $rel  keys=$($keys.Count)"
exit 0
