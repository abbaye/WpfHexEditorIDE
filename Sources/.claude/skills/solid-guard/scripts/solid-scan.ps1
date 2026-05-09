#!/usr/bin/env pwsh
<#
  solid-scan.ps1 — apply 6 SOLID heuristics on edited C# files. ADVISORY only.
  Usage: solid-scan.ps1 -Files a.cs,b.cs
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string[]]$Files,
    [string]$RepoRoot = (Resolve-Path "$PSScriptRoot\..\..\..\..\..").Path
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Core') { Write-Output "ERR=needs-pwsh7"; exit 3 }

$skipPathRe = '\\Tests\\|\\Samples\\|\.Designer\.cs$|\.g\.cs$|\.g\.i\.cs$'
$mixedConcernsExemptRe = 'FileWatcher|FileMonitor|Adapter\.cs$|Bridge\.cs$|Module\.cs$'
$factoryExemptRe = 'Factory|Builder|Module\.cs$|HostContext\.cs$|Registrar\.cs$'

$issues = New-Object System.Collections.Generic.List[psobject]

function Add-I {
    param($file,$line,$rule,$snippet)
    $issues.Add([pscustomobject]@{
        File = $file; Line = $line; Rule = $rule; Snippet = $snippet.Trim()
    })
}

function Get-LineFromIndex {
    param([string]$text, [int]$index)
    return ($text.Substring(0, $index) -split "`n").Count
}

foreach ($f in $Files) {
    if (-not (Test-Path $f)) { continue }
    $abs = (Resolve-Path $f).Path
    $rel = $abs.Substring($RepoRoot.Length).TrimStart('\','/')
    if ([IO.Path]::GetExtension($abs).ToLowerInvariant() -ne '.cs') { continue }
    if ($rel -match $skipPathRe) { continue }

    $text  = Get-Content -LiteralPath $abs -Raw
    $lines = $text -split "`n"
    $totalLines = $lines.Count
    $isFactoryLike = $rel -match $factoryExemptRe

    # ---------- srp-mixed-concerns ----------
    $hasIO  = $text -match '\bSystem\.IO\b|\bFile\.(Open|Read|Write|Exists|Delete|Move|Copy|Create)\b|\bDirectory\.(GetFiles|GetDirectories|CreateDirectory|Exists|Delete)\b|\bnew\s+FileStream\b|\bnew\s+StreamReader\b|\bnew\s+StreamWriter\b'
    $hasUI  = $text -match '\bSystem\.Windows\.\w+|\busing\s+System\.Windows\b|\bDispatcher\.|\bUserControl\b|\bWindow\b\s*[:{]|\bDependencyObject\b'
    if ($hasIO -and $hasUI -and -not ($rel -match $mixedConcernsExemptRe)) {
        Add-I $rel 1 'srp-mixed-concerns' 'class touches System.IO/File.* AND System.Windows.* — split or use an Adapter'
    }

    # ---------- srp-class-too-broad ----------
    $publicMethods = @([regex]::Matches($text, '(?m)^\s*public\s+(?!class\b|record\b|struct\b|interface\b|enum\b|event\b|const\b|static\s+readonly\b)(?:async\s+|virtual\s+|override\s+|sealed\s+|new\s+|partial\s+)*[A-Za-z_][\w<>,\s\?\[\]]*\s+([A-Za-z_]\w*)\s*\('))
    $publicMethodCount = $publicMethods.Count
    if ($totalLines -gt 300 -and $publicMethodCount -gt 15) {
        Add-I $rel 1 'srp-class-too-broad' "$totalLines lines / $publicMethodCount public methods"
    }

    # ---------- ocp-massive-switch ----------
    $methodSigRe = [regex]'(?m)^\s*(?:public|private|protected|internal|static|async|override|virtual|sealed|new|partial|\s)+\s+[A-Za-z_][\w<>,\s\?\[\]]*\s+([A-Za-z_]\w*)\s*\([^)]*\)\s*(?:where[^{]+)?\{'
    foreach ($m in $methodSigRe.Matches($text)) {
        $methodName = $m.Groups[1].Value
        $start = $m.Index + $m.Length
        $depth = 1; $i = $start; $body = New-Object Text.StringBuilder
        while ($i -lt $text.Length -and $depth -gt 0) {
            $c = $text[$i]
            if ($c -eq '{') { $depth++ }
            elseif ($c -eq '}') { $depth-- }
            [void]$body.Append($c)
            $i++
        }
        $bodyText = $body.ToString()
        # Count case labels in switch statements only
        $switchMatches = [regex]::Matches($bodyText, 'switch\s*\([^)]+\)\s*\{')
        foreach ($sw in $switchMatches) {
            $swStart = $sw.Index + $sw.Length
            $swDepth = 1; $j = $swStart
            $swBody = New-Object Text.StringBuilder
            while ($j -lt $bodyText.Length -and $swDepth -gt 0) {
                $cc = $bodyText[$j]
                if ($cc -eq '{') { $swDepth++ }
                elseif ($cc -eq '}') { $swDepth-- }
                if ($swDepth -gt 0) { [void]$swBody.Append($cc) }
                $j++
            }
            $caseCount = ([regex]::Matches($swBody.ToString(), '(?m)^\s*case\s+')).Count
            if ($caseCount -gt 10) {
                $lineNo = Get-LineFromIndex $text ($m.Index + $sw.Index)
                Add-I $rel $lineNo 'ocp-massive-switch' "switch in $methodName has $caseCount cases — consider polymorphic dispatch"
            }
        }
    }

    # ---------- dip-newing-services ----------
    if (-not $isFactoryLike) {
        $newSvcMatches = [regex]::Matches($text, '\bnew\s+([A-Z]\w*(?:Service|Manager|Repository|Provider|Factory|Client|Engine))\s*\(')
        foreach ($m in $newSvcMatches) {
            $sym = $m.Groups[1].Value
            $lineNo = Get-LineFromIndex $text $m.Index
            $thisLine = $lines[$lineNo - 1]
            if ($thisLine -match '//\s*solid-ok\b') { continue }
            # Skip nested static factory calls: SomeService.Create(...) returning new
            if ($sym.EndsWith('Factory')) { continue }
            Add-I $rel $lineNo 'dip-newing-services' "new $sym(...) — inject via ctor instead"
        }
    }

    # ---------- dip-static-deps ----------
    $staticWriteMatches = [regex]::Matches($text, '(?m)^\s*([A-Z]\w*)\.Instance\.([A-Z]\w*)\s*=')
    foreach ($m in $staticWriteMatches) {
        $lineNo = Get-LineFromIndex $text $m.Index
        $thisLine = $lines[$lineNo - 1]
        if ($thisLine -match '//\s*solid-ok\b') { continue }
        Add-I $rel $lineNo 'dip-static-deps' "$($m.Groups[1].Value).Instance.$($m.Groups[2].Value) = ... (mutable singleton write)"
    }

    # ---------- isp-fat-interface ----------
    $ifaceMatches = [regex]::Matches($text, '(?m)^\s*public\s+interface\s+([A-Z]\w*)\s*(?::[^{]*)?\{')
    foreach ($m in $ifaceMatches) {
        $ifaceName = $m.Groups[1].Value
        $start = $m.Index + $m.Length
        $depth = 1; $i = $start; $body = New-Object Text.StringBuilder
        while ($i -lt $text.Length -and $depth -gt 0) {
            $c = $text[$i]
            if ($c -eq '{') { $depth++ }
            elseif ($c -eq '}') { $depth-- }
            if ($depth -gt 0) { [void]$body.Append($c) }
            $i++
        }
        $bodyText = $body.ToString()
        # Count member declarations: ends with ; and not inside nested braces
        $memberCount = ([regex]::Matches($bodyText, '(?m)^\s*(?:event\s+|[A-Za-z_])[\w<>,\s\?\[\]]+\s+[A-Za-z_]\w*\s*[\(\{;]')).Count
        if ($memberCount -gt 10) {
            $lineNo = Get-LineFromIndex $text $m.Index
            Add-I $rel $lineNo 'isp-fat-interface' "interface $ifaceName has $memberCount members — consider splitting"
        }
    }
    # Also flag NotImplementedException as a sign of forced ISP violation
    $notImplMatches = [regex]::Matches($text, '\bthrow\s+new\s+NotImplementedException\s*\(')
    foreach ($m in $notImplMatches) {
        $lineNo = Get-LineFromIndex $text $m.Index
        $thisLine = $lines[$lineNo - 1]
        if ($thisLine -match '//\s*solid-ok\b') { continue }
        Add-I $rel $lineNo 'isp-fat-interface' 'NotImplementedException — interface forced on this class?'
    }
}

if ($issues.Count -eq 0) { Write-Output 'OK'; exit 0 }

$summary = ($issues | Group-Object Rule | Sort-Object Count -Descending |
            ForEach-Object { "$($_.Count) $($_.Name)" }) -join ', '

Write-Output "SOLID: $summary  (advisory)"
foreach ($v in $issues | Sort-Object File, Line) {
    "  $($v.File):$($v.Line)  $($v.Rule)  $($v.Snippet)"
}
exit 1
