#!/usr/bin/env pwsh
<#
  loc-source-guard.ps1 — advisory checks for localization rules in XAML/CS
  source files. Complements loc-parity.ps1 (which covers resx parity only).

  Rules (all warn-only):
    R1 loc-static-required        DynamicResource on a resx key → use StaticResource
                                  (or x:Static l10n:Resources.Key)
    R2 loc-hardcoded-string       user-visible literal in XAML attribute or
                                  CS UI property assignment
    R3a loc-idemessagebox-literal IdeMessageBox.Show("literal", ...)
    R3b loc-messagebox-legacy     MessageBox.Show used instead of IdeMessageBox
    R4 loc-locdict-missing        assembly has *Resources.resx but App.xaml /
                                  module entry-point does not merge a
                                  LocalizedResourceDictionary

  Usage:
    loc-source-guard.ps1 -Files <paths...>

  All findings printed to stdout as `WARN <rule> <file>:<line> <detail>`.
  Exit code is always 0 (advisory).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string[]]$Files,
    [string]$RepoRoot = (Resolve-Path "$PSScriptRoot\..\..\..\..\..").Path,
    [string]$AllowList = (Join-Path $PSScriptRoot "..\data\allowlist.json")
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Core') { Write-Output "ERR=needs-pwsh7"; exit 3 }

# ---------- load allow-list ----------
if (-not (Test-Path $AllowList)) {
    Write-Output "ERR=allowlist-not-found $AllowList"
    exit 3
}
$cfg = Get-Content -LiteralPath $AllowList -Raw | ConvertFrom-Json
$skipGlobs   = @($cfg.skip_path_globs)
$xamlAttrs   = @($cfg.xaml_user_visible_attrs)
$xamlIgnore  = @($cfg.xaml_ignore_value_patterns) | ForEach-Object { [regex]$_ }
$csUiAssign  = @($cfg.cs_ui_property_assign)     | ForEach-Object { [regex]$_ }
$ignoreMark  = [string]$cfg.ignore_inline_marker

# ---------- helpers ----------
function Test-Skip {
    param([string]$path)
    $p = $path.Replace('\','/')
    foreach ($g in $skipGlobs) {
        $rx = '^' + [regex]::Escape($g).Replace('\*\*','.*').Replace('\*','[^/]*') + '$'
        if ($p -imatch $rx) { return $true }
    }
    return $false
}

function Get-RelPath {
    param([string]$abs)
    if ($abs.StartsWith($RepoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $abs.Substring($RepoRoot.Length).TrimStart('\','/')
    }
    return $abs
}

function Write-Finding {
    param([string]$rule,[string]$file,[int]$line,[string]$detail)
    $rel = Get-RelPath $file
    Write-Output "WARN $rule $rel`:$line $detail"
}

# ---------- R1 + R2 (XAML) ----------
function Invoke-XamlChecks {
    param([string]$path,[string[]]$lines)

    # R1: {DynamicResource <Key>} where <Key> looks like a loc key
    # Heuristic: key uses one of the known prefixes (APP_, HE_, CD_, ...) OR
    # ends with a common loc suffix (_Title, _Label, _ToolTip, _Header, _Text,
    # _Message, _Caption, _Description). Anything else (brushes, styles) is
    # ignored.
    $locKeyRx  = [regex]'\{DynamicResource\s+((APP_|HE_|CD_|DocEd_|DBG_|DS_|Git_|AsmExplorer_|XD_|PA_|PF_|RX_|SE_|SR_|FS_|FI_|AI_)[A-Za-z0-9_]+|[A-Za-z][A-Za-z0-9_]*_(Title|Label|ToolTip|Header|Text|Message|Caption|Description|Tooltip|Hint|Placeholder|Watermark))\}'

    for ($i = 0; $i -lt $lines.Count; $i++) {
        $ln = $lines[$i]
        if ($ln -match [regex]::Escape($ignoreMark)) { continue }

        $m = $locKeyRx.Match($ln)
        if ($m.Success) {
            Write-Finding 'loc-static-required' $path ($i+1) "use StaticResource (or x:Static l10n:Resources.X) for key '$($m.Groups[1].Value)'"
        }

        # R2: user-visible attributes with string literal value
        foreach ($attr in $xamlAttrs) {
            $attrRx = [regex]("\b$attr\s*=\s*`"([^`"]*)`"")
            foreach ($am in $attrRx.Matches($ln)) {
                $val = $am.Groups[1].Value
                $skip = $false
                foreach ($ig in $xamlIgnore) { if ($ig.IsMatch($val)) { $skip = $true; break } }
                if ($skip) { continue }
                if ($val -match '^\{(StaticResource|DynamicResource|Binding|x:Static|TemplateBinding)') { continue }
                Write-Finding 'loc-hardcoded-string' $path ($i+1) "$attr=`"$val`" — extract to *Resources.resx key"
            }
        }
    }
}

# ---------- R2 + R3 (CS) ----------
function Invoke-CsChecks {
    param([string]$path,[string[]]$lines)

    $ideShowRx   = [regex]'IdeMessageBox\s*\.\s*Show\s*\('
    $msgBoxRx    = [regex]'(?<![A-Za-z0-9_\.])MessageBox\s*\.\s*Show\s*\('
    $dialogSvcRx = [regex]'IDialogService\s*\.\s*Show\s*\('

    for ($i = 0; $i -lt $lines.Count; $i++) {
        $ln = $lines[$i]
        if ($ln -match [regex]::Escape($ignoreMark)) { continue }
        $trim = $ln.TrimStart()
        if ($trim.StartsWith('//')) { continue }

        # R3b: legacy MessageBox (ADR-009)
        if ($msgBoxRx.IsMatch($ln)) {
            Write-Finding 'loc-messagebox-legacy' $path ($i+1) 'use IdeMessageBox via IDEHostContext.Dialogs (ADR-009)'
        }

        # R3a: IdeMessageBox.Show("literal", ...) or IDialogService.Show("literal", ...)
        foreach ($rx in @($ideShowRx, $dialogSvcRx)) {
            $m = $rx.Match($ln)
            if (-not $m.Success) { continue }
            $tail = $ln.Substring($m.Index + $m.Length)
            # crude: first argument is a literal if it starts with `"` (and not `$"` interpolation token used solely for fmt)
            if ($tail -match '^\s*\"([^\"]+)\"') {
                $lit = $Matches[1]
                # Allow string.Format / nameof wrappers — they wouldn't match this regex anyway.
                Write-Finding 'loc-idemessagebox-literal' $path ($i+1) "literal `"$lit`" — pass a resx key instead"
            }
        }

        # R2: UI property assignment to a string literal (rough)
        foreach ($rx in $csUiAssign) {
            $m = $rx.Match($ln)
            if (-not $m.Success) { continue }
            $tail = $ln.Substring($m.Index + $m.Length)
            if ($tail -match '^\s*\"([^\"]+)\"') {
                $val = $Matches[1]
                # Skip if it looks like a path / numeric / single glyph
                if ($val -match '^[\s]*$') { continue }
                if ($val -match '^[0-9\.\-:/\\]+$') { continue }
                if ($val.Length -le 2) { continue }
                Write-Finding 'loc-hardcoded-string' $path ($i+1) "UI property literal `"$val`" — use resx key"
            }
        }
    }
}

# ---------- R4 (LocalizedResourceDictionary wiring) ----------
function Invoke-LocDictCheck {
    param([string]$resxPath)
    # Walk up looking for a sibling App.xaml or a *.csproj. If a csproj has any
    # *Resources.resx but the assembly entry XAML (App.xaml / Module*.xaml)
    # does not contain "LocalizedResourceDictionary", warn once per assembly.
    $dir = [IO.Path]::GetDirectoryName($resxPath)
    while ($dir -and (Test-Path $dir)) {
        $csproj = Get-ChildItem -Path $dir -Filter *.csproj -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($csproj) {
            $entries = Get-ChildItem -Path $dir -Recurse -Filter App.xaml -ErrorAction SilentlyContinue
            $entries += Get-ChildItem -Path $dir -Recurse -Filter Module.xaml -ErrorAction SilentlyContinue
            if (-not $entries -or $entries.Count -eq 0) { return }
            $found = $false
            foreach ($e in $entries) {
                $txt = Get-Content -LiteralPath $e.FullName -Raw -ErrorAction SilentlyContinue
                if ($txt -and $txt -match 'LocalizedResourceDictionary') { $found = $true; break }
            }
            if (-not $found) {
                Write-Finding 'loc-locdict-missing' $csproj.FullName 1 "assembly has *Resources.resx but no LocalizedResourceDictionary merged in App.xaml/Module.xaml (ADR-005)"
            }
            return
        }
        $parent = [IO.Path]::GetDirectoryName($dir)
        if ($parent -eq $dir) { break }
        $dir = $parent
    }
}

# ---------- main ----------
$seenAssembly = @{}

foreach ($f in $Files) {
    if (-not (Test-Path $f)) { continue }
    $abs = (Resolve-Path $f).Path
    if (Test-Skip $abs) { continue }

    $ext = [IO.Path]::GetExtension($abs).ToLowerInvariant()
    switch ($ext) {
        '.xaml' {
            $lines = Get-Content -LiteralPath $abs
            Invoke-XamlChecks $abs $lines
        }
        '.cs' {
            $lines = Get-Content -LiteralPath $abs
            Invoke-CsChecks $abs $lines
        }
        '.resx' {
            $name = [IO.Path]::GetFileName($abs)
            if ($name -match 'Resources\.resx$' -and $name -notmatch 'Resources\.[a-z]{2}(-[A-Z0-9]{2,3})?\.resx$') {
                # base resx — check wiring once per containing assembly
                $key = ([IO.Path]::GetDirectoryName($abs))
                if (-not $seenAssembly.ContainsKey($key)) {
                    $seenAssembly[$key] = $true
                    Invoke-LocDictCheck $abs
                }
            }
        }
        default { }
    }
}
