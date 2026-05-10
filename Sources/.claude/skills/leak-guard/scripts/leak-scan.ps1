#!/usr/bin/env pwsh
<#
  leak-scan.ps1 — apply 9 leak / secret rules on edited C# files.
  Usage: leak-scan.ps1 -Files a.cs,b.cs
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string[]]$Files,
    [string]$RepoRoot = (Resolve-Path "$PSScriptRoot\..\..\..\..\..").Path
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Core') { Write-Output "ERR=needs-pwsh7"; exit 3 }

$skipPathRe = '\\Tests\\|\\Samples\\|\.Designer\.cs$|\.g\.cs$|\.g\.i\.cs$'
$longLivedSources = @(
    'Application\.Current',
    'Dispatcher\.',
    'EventBus\.',
    'IDEEventBus\.',
    '\.Instance\.'
)

$issues = New-Object System.Collections.Generic.List[psobject]

function Add-I {
    param($file,$line,$rule,$snippet,$severity='warn')
    $issues.Add([pscustomobject]@{
        File = $file; Line = $line; Rule = $rule; Severity = $severity; Snippet = $snippet.Trim()
    })
}

foreach ($f in $Files) {
    if (-not (Test-Path $f)) { continue }
    $abs = (Resolve-Path $f).Path
    $rel = $abs.Substring($RepoRoot.Length).TrimStart('\','/')
    if ([IO.Path]::GetExtension($abs).ToLowerInvariant() -ne '.cs') { continue }
    if ($rel -match $skipPathRe) { continue }

    $text = Get-Content -LiteralPath $abs -Raw
    $lines = $text -split "`n"

    # ----- secrets (line-scoped) -----
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if ($line -match '//\s*leak-ok\b' -or $line -match '//\s*fixture\b') { continue }
        if ($line -match '\b(api[_-]?key|password|secret|token|bearer)\s*=\s*"([A-Za-z0-9+/=]{16,})"') {
            Add-I $rel ($i + 1) 'secret-in-source' $line 'error'
        }
    }

    # ----- IDisposable contract -----
    if ($text -match ':\s*[^;]*IDisposable') {
        if ($text -notmatch 'public\s+(?:virtual\s+|sealed\s+override\s+|override\s+)?void\s+Dispose\s*\(\s*\)\s*\{' -and
            $text -notmatch 'void\s+IDisposable\.Dispose\s*\(\s*\)') {
            $sigLine = ($text.Substring(0, $text.IndexOf('IDisposable')) -split "`n").Count
            Add-I $rel $sigLine 'idisposable-no-dispose' 'declares IDisposable but no Dispose() body found' 'error'
        }
    }

    # ----- finalizer + missing SuppressFinalize -----
    if ($text -match '~\w+\s*\(\s*\)\s*\{') {
        $disposeBlockMatch = [regex]::Match($text, 'public\s+(?:virtual\s+|override\s+)?void\s+Dispose\s*\(\s*\)\s*\{([\s\S]*?)\n\s*\}')
        if ($disposeBlockMatch.Success) {
            $disposeBody = $disposeBlockMatch.Groups[1].Value
            if ($disposeBody -notmatch 'GC\.SuppressFinalize\s*\(\s*this\s*\)') {
                $line = ($text.Substring(0, $disposeBlockMatch.Index) -split "`n").Count
                Add-I $rel $line 'dispose-no-suppress-finalize' 'finalizer present but Dispose() missing GC.SuppressFinalize(this)'
            }
        }
    }

    # ----- event subscriptions -----
    # Capture target.Event += handler
    $subMatches = [regex]::Matches($text, '(?m)^\s*([A-Za-z_][\w\.]*)\.([A-Za-z_]\w*)\s*\+=\s*([A-Za-z_][\w\.]*)\s*;')
    foreach ($m in $subMatches) {
        $target = $m.Groups[1].Value
        $evt    = $m.Groups[2].Value
        $hnd    = $m.Groups[3].Value
        $lineNo = ($text.Substring(0, $m.Index) -split "`n").Count
        $thisLine = $lines[$lineNo - 1]
        if ($thisLine -match '//\s*leak-ok\b') { continue }

        # weak-event-candidate
        foreach ($lls in $longLivedSources) {
            if ($target -match $lls) {
                Add-I $rel $lineNo 'weak-event-candidate' "$target.$evt += $hnd  (long-lived host)"
                break
            }
        }

        # event-no-unsubscribe — search for matching unsubscribe in same file (Dispose / Unloaded / Closed / Detach)
        $unsubRe = [regex]::Escape("$target.$evt") + '\s*-=\s*' + [regex]::Escape($hnd)
        if ($text -notmatch $unsubRe) {
            # tighter check: any -= for that event/handler
            $loose = "$([regex]::Escape($evt))\s*-=\s*$([regex]::Escape($hnd))"
            if ($text -notmatch $loose) {
                Add-I $rel $lineNo 'event-no-unsubscribe' "$target.$evt += $hnd  (no -= found)"
            }
        }
    }

    # ----- static event collection (mutable) -----
    if ($text -match 'public\s+static\s+event\s+') {
        $lineNo = (($text.Substring(0, $text.IndexOf('public static event')) -split "`n").Count)
        Add-I $rel $lineNo 'static-event-collection' 'public static event (instances may leak via subscriptions)' 'error'
    }
    $staticCollMatches = [regex]::Matches($text, '(?m)^\s*(?:private|internal|protected|public)?\s*static\s+(?!readonly\s+)(?:List|Dictionary|HashSet|ConcurrentDictionary|ConcurrentBag|Queue|Stack)<')
    foreach ($m in $staticCollMatches) {
        $lineNo = ($text.Substring(0, $m.Index) -split "`n").Count
        if ($lines[$lineNo - 1] -match '//\s*leak-ok\b') { continue }
        Add-I $rel $lineNo 'static-event-collection' 'static mutable collection (leak vector if mutated by instance code)' 'error'
    }

    # ----- FileStream / File.Open without using -----
    $fsMatches = [regex]::Matches($text, '(?m)^(\s*)(?:var\s+\w+\s*=\s*)?(?:new\s+FileStream\s*\(|File\.Open(?:Read|Write|Text)?\s*\()')
    foreach ($m in $fsMatches) {
        $lineNo = ($text.Substring(0, $m.Index) -split "`n").Count
        $thisLine = $lines[$lineNo - 1]
        if ($thisLine -match '//\s*leak-ok\b') { continue }
        if ($thisLine -match '\busing\s*\(' -or $thisLine -match '\busing\s+var\b') { continue }
        # Field assignment is acceptable if class is IDisposable (then Dispose owns it)
        if ($thisLine -match '^\s*(?:private|internal|protected|public).*?=\s*new\s+FileStream') { continue }
        Add-I $rel $lineNo 'filestream-no-using' $thisLine.Trim()
    }

    # ----- Timer without stop -----
    $timerFieldMatches = [regex]::Matches($text, '(?m)^\s*(?:private|internal|protected|public).*?\b(DispatcherTimer|System\.Threading\.Timer|System\.Timers\.Timer|Timer)\s+(\w+)\s*[=;]')
    foreach ($m in $timerFieldMatches) {
        $field = $m.Groups[2].Value
        $lineNo = ($text.Substring(0, $m.Index) -split "`n").Count
        $usage = "$field\.(Stop|Dispose|Change)"
        if ($text -notmatch $usage) {
            Add-I $rel $lineNo 'timer-no-stop' "$($m.Groups[1].Value) $field never Stop()/Dispose() referenced"
        }
    }

    # ----- Process not disposed -----
    $procMatches = [regex]::Matches($text, '(?m)^(\s*)(?:var\s+\w+\s*=\s*)?(?:new\s+Process\s*\(|Process\.Start\s*\()')
    foreach ($m in $procMatches) {
        $lineNo = ($text.Substring(0, $m.Index) -split "`n").Count
        $thisLine = $lines[$lineNo - 1]
        if ($thisLine -match '//\s*leak-ok\b') { continue }
        if ($thisLine -match '\busing\s*\(' -or $thisLine -match '\busing\s+var\b') { continue }
        if ($thisLine -match 'return\s+(?:new\s+Process\s*\(|Process\.Start\s*\()') { continue }
        Add-I $rel $lineNo 'process-no-dispose' $thisLine.Trim()
    }
}

if ($issues.Count -eq 0) { Write-Output 'OK'; exit 0 }

$summary = ($issues | Group-Object Rule | Sort-Object Count -Descending |
            ForEach-Object { "$($_.Count) $($_.Name)" }) -join ', '
$secretCount = ($issues | Where-Object { $_.Rule -eq 'secret-in-source' } | Measure-Object).Count

Write-Output "Leaks: $summary | secrets=$secretCount"
foreach ($v in $issues | Sort-Object File, Line) {
    "  $($v.File):$($v.Line)  $($v.Rule)  $($v.Snippet)"
}
exit 1
