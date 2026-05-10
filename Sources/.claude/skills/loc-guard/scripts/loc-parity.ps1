#!/usr/bin/env pwsh
<#
  loc-parity.ps1 — verify base.resx ↔ <base>.<lang>.resx parity across all
  detected satellites of the edited resx file(s).
  Usage: loc-parity.ps1 -Files AppResources.resx,...
  If a satellite is provided, the script resolves its base and checks that
  satellite only.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string[]]$Files,
    [string]$RepoRoot = (Resolve-Path "$PSScriptRoot\..\..\..\..\..").Path,
    [string]$SnapshotFile = (Join-Path $PSScriptRoot "..\data\satellites-snapshot.tsv"),
    [switch]$WriteSnapshot
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Core') { Write-Output "ERR=needs-pwsh7"; exit 3 }

$satNameRe = [regex]'^(?<base>[A-Za-z0-9_]+Resources)\.(?<lang>[a-z]{2}(?:-[A-Z0-9]{2,3})?)\.resx$'

function Get-ResxKeys {
    param([string]$path)
    try {
        [xml]$doc = Get-Content -LiteralPath $path -Raw
    } catch {
        return $null
    }
    if ($doc.DocumentElement.Name -ne 'root') { return $null }
    $kv = @{}
    foreach ($n in $doc.SelectNodes('//data[@name]')) {
        $name = $n.GetAttribute('name')
        $valNode = $n.SelectSingleNode('value')
        $val = if ($valNode) { $valNode.InnerText } else { '' }
        $kv[$name] = $val
    }
    return $kv
}

function Get-Placeholders {
    param([string]$value)
    if (-not $value) { return ,@() }
    $hits = @()
    foreach ($m in [regex]::Matches($value, '\{(\d+)(?::[^}]*)?\}')) {
        $hits += [int]$m.Groups[1].Value
    }
    if ($hits.Count -eq 0) { return ,@() }
    return ,@($hits | Sort-Object -Unique)
}

function Resolve-Base {
    param([string]$resxPath)
    $name = [IO.Path]::GetFileName($resxPath)
    $m = $satNameRe.Match($name)
    if ($m.Success) {
        # It's a satellite — return its base
        $dir = [IO.Path]::GetDirectoryName($resxPath)
        $baseName = "$($m.Groups['base'].Value).resx"
        $basePath = Join-Path $dir $baseName
        return @{ Base = $basePath; SingleLang = $m.Groups['lang'].Value }
    }
    return @{ Base = $resxPath; SingleLang = $null }
}

function Find-Satellites {
    param([string]$basePath)
    $dir = [IO.Path]::GetDirectoryName($basePath)
    $baseStem = [IO.Path]::GetFileNameWithoutExtension($basePath)  # e.g. AppResources
    $pattern = "$baseStem.*.resx"
    $hits = Get-ChildItem -Path $dir -Filter $pattern -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -ne "$baseStem.resx" }
    return $hits
}

# ---------- main loop ----------
$snapshotRows = New-Object System.Collections.Generic.List[string]
$bases = @{}

foreach ($f in $Files) {
    if (-not (Test-Path $f)) { Write-Output "  ${f}: missing"; continue }
    $abs = (Resolve-Path $f).Path
    if ([IO.Path]::GetExtension($abs).ToLowerInvariant() -ne '.resx') { continue }

    $info = Resolve-Base $abs
    $basePath = $info.Base
    if (-not (Test-Path $basePath)) {
        Write-Output "  ${abs}: base resx not found ($basePath)"
        continue
    }
    $key = $basePath
    if (-not $bases.ContainsKey($key)) {
        $bases[$key] = $info.SingleLang
    }
}

foreach ($basePath in $bases.Keys) {
    $singleLang = $bases[$basePath]
    $baseStem = [IO.Path]::GetFileNameWithoutExtension($basePath)
    $rel = $basePath.Substring($RepoRoot.Length).TrimStart('\','/')

    $baseKv = Get-ResxKeys $basePath
    if ($null -eq $baseKv) {
        Write-Output "Base ${rel}: malformed XML"
        continue
    }
    $baseKeys = $baseKv.Keys | Sort-Object

    $satFiles = Find-Satellites $basePath
    if ($singleLang) {
        $satFiles = $satFiles | Where-Object { $_.Name -like "$baseStem.$singleLang.resx" }
    }

    Write-Output "Loc ${baseStem} ($($satFiles.Count) satellites checked)  base-keys=$($baseKeys.Count)"

    foreach ($sat in ($satFiles | Sort-Object Name)) {
        $satMatch = $satNameRe.Match($sat.Name)
        if (-not $satMatch.Success) { continue }
        $lang = $satMatch.Groups['lang'].Value
        $satKv = Get-ResxKeys $sat.FullName
        if ($null -eq $satKv) {
            Write-Output "  $lang  malformed  XML parse error"
            $snapshotRows.Add("$baseStem`t$lang`t-1`t-1`t-1")
            continue
        }

        $missing  = @()
        $orphan   = @()
        $phMis    = @()
        $untrans  = @()

        foreach ($k in $baseKeys) {
            if (-not $satKv.ContainsKey($k)) { $missing += $k; continue }
            $bV = $baseKv[$k]
            $sV = $satKv[$k]
            $bP = @(Get-Placeholders $bV)
            $sP = @(Get-Placeholders $sV)
            $bSig = ($bP -join ',')
            $sSig = ($sP -join ',')
            if ($bSig -ne $sSig) { $phMis += $k }
            if (($lang -notmatch '^en') -and $sV -and ($sV -eq $bV) -and ($bV.Length -ge 3)) {
                $untrans += $k
            }
        }
        foreach ($k in $satKv.Keys) {
            if (-not $baseKv.ContainsKey($k)) { $orphan += $k }
        }

        $parts = @()
        if ($missing.Count -gt 0) {
            $parts += "$($missing.Count) missing-key"
            if ($missing.Count -le 5) { $parts[-1] += " ($($missing -join ', '))" }
        }
        if ($orphan.Count -gt 0) {
            $parts += "$($orphan.Count) orphan-key"
            if ($orphan.Count -le 5) { $parts[-1] += " ($($orphan -join ', '))" }
        }
        if ($phMis.Count -gt 0) {
            $parts += "$($phMis.Count) placeholder-mismatch"
            if ($phMis.Count -le 3) { $parts[-1] += " ($($phMis -join ', '))" }
        }
        if ($untrans.Count -gt 0) { $parts += "$($untrans.Count) untranslated (warn)" }

        if ($parts.Count -eq 0) {
            Write-Output "  $lang  OK    keys=$($satKv.Count)"
        } else {
            Write-Output "  $lang  $(($parts -join ' | '))"
        }
        $snapshotRows.Add("$baseStem`t$lang`t$($satKv.Count)`t$($missing.Count)`t$($phMis.Count)")
    }
}

if ($WriteSnapshot -and $snapshotRows.Count -gt 0) {
    $header = "base`tlang`tkeys`tmissing`tplaceholder-mismatch"
    Set-Content -LiteralPath $SnapshotFile -Value (@($header) + $snapshotRows) -Encoding UTF8
    Write-Output ""
    Write-Output "Snapshot written: $SnapshotFile"
}
