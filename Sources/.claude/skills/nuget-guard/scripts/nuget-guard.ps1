#!/usr/bin/env pwsh
<#
  nuget-guard.ps1 — protects published-on-nuget.org packages from IDE-feature
  contamination and public-API regressions.

  Protected packages (see data/package-policy.json):
    core-xplat  : WpfHexEditor.Core.ByteProvider, WpfHexEditor.Core.BinaryAnalysis,
                  whfmt.Analysis, whfmt.Backfill, whfmt.CodeGen, whfmt.Fuzz,
                  whfmt.Validate
    wpf-control : WPFHexaEditor, WpfCodeEditor, WpfDocking

  Any file edited under a project NOT matching one of these PackageIds is
  silently ignored.

  Rules:
    nuget-api-removed         error  public type/member present in git HEAD,
                                     absent in working tree
    nuget-api-renamed         error  public signature changed (heuristic:
                                     removed + similar-name added in same file)
    nuget-tfm-drift           error  TFM diverges from policy
    nuget-usewpf-leak         error  UseWPF/UseWindowsForms enabled on core-xplat
    nuget-ide-projref         error  ProjectReference to App / Editor.* / Plugins.*
                                     (Editor.*.Core is allowed)
    nuget-ide-using           error  using or type ref to IDE-only types
    nuget-wpf-using-in-xplat  error  System.Windows.* / WebView2 in core-xplat
    nuget-version-regression  error  <Version> numerically lower than git HEAD
    nuget-release-notes-stale warn   <Version> bumped but PackageReleaseNotes
                                     unchanged

  Findings printed as `ERR|WARN <rule> <file>:<line> <detail>`. Exit code is
  the count of ERR findings (capped at 100). WARN-only runs exit 0.

  Usage:
    nuget-guard.ps1 -Files <paths...>
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string[]]$Files,
    [string]$RepoRoot = (Resolve-Path "$PSScriptRoot\..\..\..\..\..").Path,
    [string]$Policy   = (Join-Path $PSScriptRoot "..\data\package-policy.json")
)
$ErrorActionPreference = 'Continue'
if ($PSVersionTable.PSEdition -ne 'Core') { Write-Output "ERR=needs-pwsh7"; exit 3 }

# ---------- load policy ----------
if (-not (Test-Path $Policy)) { Write-Output "ERR=policy-not-found $Policy"; exit 3 }
$cfg = Get-Content -LiteralPath $Policy -Raw | ConvertFrom-Json
$idePrefixes = @($cfg.ide_only_assembly_prefixes)
$ideTypes    = @($cfg.ide_only_types)
$wpfUsings   = @($cfg.wpf_usings_forbidden_in_xplat)
$ignoreMark  = [string]$cfg.ignore_inline_marker

$errCount = 0
function Add-Err   { param([string]$r,[string]$f,[int]$ln,[string]$d) $script:errCount++; Write-Output "ERR $r $(Get-Rel $f)`:$ln $d" }
function Add-Warn  { param([string]$r,[string]$f,[int]$ln,[string]$d) Write-Output "WARN $r $(Get-Rel $f)`:$ln $d" }
function Get-Rel   { param([string]$abs)
    if ($abs.StartsWith($RepoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $abs.Substring($RepoRoot.Length).TrimStart('\','/')
    }
    return $abs
}

# ---------- owning-package resolution ----------
$packageCache = @{}
function Get-OwningCsproj {
    param([string]$filePath)
    $dir = if (Test-Path $filePath -PathType Container) { $filePath } else { [IO.Path]::GetDirectoryName($filePath) }
    while ($dir -and (Test-Path $dir)) {
        $csproj = Get-ChildItem -Path $dir -Filter *.csproj -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($csproj) { return $csproj.FullName }
        $parent = [IO.Path]::GetDirectoryName($dir)
        if (-not $parent -or $parent -eq $dir) { return $null }
        $dir = $parent
    }
    return $null
}

function Get-PackageInfo {
    param([string]$csprojPath)
    if ($packageCache.ContainsKey($csprojPath)) { return $packageCache[$csprojPath] }
    if (-not (Test-Path $csprojPath)) { $packageCache[$csprojPath] = $null; return $null }
    try { [xml]$x = Get-Content -LiteralPath $csprojPath -Raw } catch { $packageCache[$csprojPath] = $null; return $null }
    $pkgIdNode = $x.SelectSingleNode('//PackageId')
    if (-not $pkgIdNode) { $packageCache[$csprojPath] = $null; return $null }
    $pkgId = $pkgIdNode.InnerText.Trim()
    if (-not $cfg.packages.PSObject.Properties.Name -contains $pkgId) {
        $packageCache[$csprojPath] = $null; return $null
    }
    $policy = $cfg.packages.$pkgId
    $info = [pscustomobject]@{
        Csproj   = $csprojPath
        PkgId    = $pkgId
        Policy   = $policy
        Category = $policy.category
        Xml      = $x
    }
    $packageCache[$csprojPath] = $info
    return $info
}

# ---------- git helpers ----------
function Get-GitHeadContent {
    param([string]$absPath)
    $rel = Get-Rel $absPath
    $rel = $rel.Replace('\','/')
    $out = & git -C $RepoRoot show "HEAD:$rel" 2>$null
    if ($LASTEXITCODE -ne 0) { return $null }
    return $out
}

# ---------- public API extraction ----------
$publicSigRx = [regex]'^\s*(?:\[[^\]]*\]\s*)*public\s+(?:static\s+|sealed\s+|abstract\s+|virtual\s+|override\s+|partial\s+|readonly\s+|async\s+|new\s+|unsafe\s+|extern\s+)*((?:class|interface|struct|enum|record(?:\s+(?:class|struct))?|delegate)\s+[A-Za-z_][A-Za-z0-9_<>,\s]*|[A-Za-z_][\w<>?\[\],\s\.]*\s+[A-Za-z_]\w*\s*[\(\{=;])'

function Get-PublicSignatures {
    param([string]$content)
    if (-not $content) { return @() }
    $sigs = New-Object System.Collections.Generic.List[string]
    foreach ($line in ($content -split "`r?`n")) {
        $m = $publicSigRx.Match($line)
        if ($m.Success) {
            $sig = ($line.Trim() -replace '\s+',' ' -replace '\{\s*$','').TrimEnd(' ', ';', '{')
            $sigs.Add($sig)
        }
    }
    return $sigs
}

# ---------- csproj checks ----------
function Test-Csproj {
    param([string]$csprojPath,[object]$info)
    [xml]$x = $info.Xml
    $policy = $info.Policy

    # TFM
    $tfm = ($x.SelectSingleNode('//TargetFramework')).InnerText
    if ($tfm -and -not ($policy.tfm -contains $tfm)) {
        Add-Err 'nuget-tfm-drift' $csprojPath 1 "TFM '$tfm' for package '$($info.PkgId)' — policy requires one of [$($policy.tfm -join ', ')]"
    }

    # UseWPF / UseWindowsForms
    $useWpfNode = $x.SelectSingleNode('//UseWPF')
    if ($useWpfNode -and $useWpfNode.InnerText -eq 'true' -and -not $policy.use_wpf) {
        Add-Err 'nuget-usewpf-leak' $csprojPath 1 "UseWPF=true on core-xplat package '$($info.PkgId)'"
    }
    $useWfNode = $x.SelectSingleNode('//UseWindowsForms')
    if ($useWfNode -and $useWfNode.InnerText -eq 'true' -and -not $policy.use_winforms) {
        Add-Err 'nuget-usewpf-leak' $csprojPath 1 "UseWindowsForms=true on '$($info.PkgId)'"
    }

    # ProjectReference IDE leak
    foreach ($pr in $x.SelectNodes('//ProjectReference')) {
        $inc = $pr.GetAttribute('Include')
        if (-not $inc) { continue }
        $refName = [IO.Path]::GetFileNameWithoutExtension($inc)
        foreach ($pfx in $idePrefixes) {
            if ($refName.StartsWith($pfx) -and -not $refName.EndsWith('.Core')) {
                Add-Err 'nuget-ide-projref' $csprojPath 1 "ProjectReference to IDE-only '$refName' from package '$($info.PkgId)'"
            }
        }
    }

    # Version regression
    $verNode = $x.SelectSingleNode('//Version')
    if ($verNode) {
        $newVer = $verNode.InnerText.Trim()
        $headXml = Get-GitHeadContent $csprojPath
        if ($headXml) {
            try {
                [xml]$hx = $headXml
                $oldVerNode = $hx.SelectSingleNode('//Version')
                if ($oldVerNode) {
                    $oldVer = $oldVerNode.InnerText.Trim()
                    if (Compare-Version $newVer $oldVer -lt 0) {
                        Add-Err 'nuget-version-regression' $csprojPath 1 "Version $newVer < HEAD $oldVer for '$($info.PkgId)'"
                    }
                    if (Compare-Version $newVer $oldVer -gt 0) {
                        $newNotes = ($x.SelectSingleNode('//PackageReleaseNotes')).InnerText
                        $oldNotes = ($hx.SelectSingleNode('//PackageReleaseNotes')).InnerText
                        if ($newNotes -and $oldNotes -and $newNotes.Trim() -eq $oldNotes.Trim()) {
                            Add-Warn 'nuget-release-notes-stale' $csprojPath 1 "Version bumped $oldVer -> $newVer but PackageReleaseNotes unchanged"
                        }
                    }
                }
            } catch { }
        }
    }
}

function Compare-Version {
    param([string]$a,[string]$b)
    $pa = $a -split '[\.\-]' | ForEach-Object { $n=0; if ([int]::TryParse($_, [ref]$n)) { $n } else { 0 } }
    $pb = $b -split '[\.\-]' | ForEach-Object { $n=0; if ([int]::TryParse($_, [ref]$n)) { $n } else { 0 } }
    $max = [Math]::Max($pa.Count, $pb.Count)
    for ($i=0; $i -lt $max; $i++) {
        $av = if ($i -lt $pa.Count) { [int]$pa[$i] } else { 0 }
        $bv = if ($i -lt $pb.Count) { [int]$pb[$i] } else { 0 }
        if ($av -lt $bv) { return -1 }
        if ($av -gt $bv) { return 1 }
    }
    return 0
}

# ---------- cs file checks ----------
function Test-CsFile {
    param([string]$csPath,[object]$info)
    $policy   = $info.Policy
    $category = $info.Category
    $lines    = Get-Content -LiteralPath $csPath
    if (-not $lines) { return }

    for ($i=0; $i -lt $lines.Count; $i++) {
        $ln = $lines[$i]
        if ($ln -match [regex]::Escape($ignoreMark)) { continue }
        $trim = $ln.TrimStart()
        if ($trim.StartsWith('//')) { continue }

        # IDE using/type
        foreach ($pfx in $idePrefixes) {
            if ($ln -match "(?:using|namespace)\s+$([regex]::Escape($pfx))" -and $ln -notmatch "$([regex]::Escape($pfx))[A-Za-z0-9_\.]*\.Core(\s|;|$)") {
                Add-Err 'nuget-ide-using' $csPath ($i+1) "references IDE-only assembly prefix '$pfx' from package '$($info.PkgId)'"
            }
        }
        foreach ($t in $ideTypes) {
            if ($ln -match "\b$([regex]::Escape($t))\b") {
                Add-Err 'nuget-ide-using' $csPath ($i+1) "IDE-only type '$t' referenced from package '$($info.PkgId)'"
                break
            }
        }

        # WPF using in core-xplat
        if ($category -eq 'core-xplat') {
            foreach ($u in $wpfUsings) {
                if ($ln -match "using\s+$([regex]::Escape($u))\b") {
                    Add-Err 'nuget-wpf-using-in-xplat' $csPath ($i+1) "WPF/WinForms namespace '$u' in core-xplat package '$($info.PkgId)'"
                    break
                }
            }
        }
    }

    # public API removal (signature-set diff vs HEAD)
    $headContent = Get-GitHeadContent $csPath
    if ($headContent) {
        $currentText = ($lines -join "`n")
        $oldSigs = Get-PublicSignatures $headContent
        $newSigs = Get-PublicSignatures $currentText
        $newSet  = [System.Collections.Generic.HashSet[string]]::new([string[]]$newSigs)
        foreach ($s in $oldSigs) {
            if (-not $newSet.Contains($s)) {
                # Try fuzzy match: same first identifier after `public`
                $core = ($s -replace '^public\s+(?:static\s+|sealed\s+|abstract\s+|virtual\s+|override\s+|partial\s+|readonly\s+|async\s+|new\s+|unsafe\s+|extern\s+)*','')
                $token = ($core -split '[\s\(\{<]')[0]
                $fuzzy = $newSigs | Where-Object { $_ -match "\b$([regex]::Escape($token))\b" }
                if ($fuzzy -and $fuzzy.Count -gt 0) {
                    Add-Err 'nuget-api-renamed' $csPath 1 "public signature changed: HEAD had '$s' — current has '$($fuzzy[0])' (rename or signature edit breaks consumers of '$($info.PkgId)')"
                } else {
                    Add-Err 'nuget-api-removed' $csPath 1 "public signature removed: '$s' (breaks consumers of '$($info.PkgId)')"
                }
            }
        }
    }
}

# ---------- main ----------
foreach ($f in $Files) {
    if (-not (Test-Path $f)) { continue }
    $abs = (Resolve-Path $f).Path
    $ext = [IO.Path]::GetExtension($abs).ToLowerInvariant()
    if ($ext -notin '.cs', '.csproj') { continue }

    $csproj = if ($ext -eq '.csproj') { $abs } else { Get-OwningCsproj $abs }
    if (-not $csproj) { continue }
    $info = Get-PackageInfo $csproj
    if (-not $info) { continue }   # not a protected package

    if ($ext -eq '.csproj') {
        Test-Csproj $csproj $info
    } else {
        Test-CsFile $abs $info
    }
}

if ($errCount -gt 100) { $errCount = 100 }
exit $errCount
