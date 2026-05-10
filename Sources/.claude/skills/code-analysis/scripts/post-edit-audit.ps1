#!/usr/bin/env pwsh
<#
  post-edit-audit.ps1 — verdict on a batch of edited files vs CLAUDE.md rules.
  Output:
    OK
    or
    VIOLATIONS:
      <file>: <rule>=<value>
      ...
  Exit 0 if OK, 1 if any violation.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string[]]$Files,
    [string]$RepoRoot = (Resolve-Path "$PSScriptRoot\..\..\..\..\..").Path,
    [int]$MaxFuncLines = 25,
    [int]$MaxClassLines = 300
)
$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSEdition -ne 'Core') { Write-Output "ERR=needs-pwsh7"; exit 3 }

$violations = New-Object System.Collections.Generic.List[string]

# Forbidden-pattern regex set (kept in sync with references/forbidden-patterns.md)
$forbidden = @(
    @{ Rule='avalonedit';  Re='\busing\s+ICSharpCode\b' },
    @{ Rule='msgbox.show'; Re='\bMessageBox\.Show\s*\(' },
    @{ Rule='hex-l10n';    Re='(?<!L10n\s*=\s*)Resources\.[A-Z]\w+' ; PathFilter='HexEditor' },
    @{ Rule='static-mut';  Re='\bpublic\s+static\s+[A-Za-z_]\w*(?<!readonly)\s+[A-Za-z_]\w*\s*=' }
)

foreach ($f in $Files) {
    if (-not (Test-Path $f)) { $violations.Add("${f}: missing"); continue }
    $abs  = (Resolve-Path $f).Path
    $rel  = $abs.Substring($RepoRoot.Length).TrimStart('\','/')
    $ext  = [IO.Path]::GetExtension($abs).ToLowerInvariant()
    $text = Get-Content -LiteralPath $abs -Raw

    # 1) .md inside Sources\ is forbidden
    if ($ext -eq '.md' -and $rel -match '^Sources\\') {
        $violations.Add("${rel}: md-in-sources")
        continue
    }

    if ($ext -ne '.cs' -and $ext -ne '.xaml') { continue }

    # 2) Forbidden patterns
    foreach ($p in $forbidden) {
        if ($p.PathFilter -and $rel -notmatch [regex]::Escape($p.PathFilter)) { continue }
        if ($text -match $p.Re) {
            $violations.Add("${rel}: $($p.Rule)")
        }
    }

    if ($ext -ne '.cs') { continue }

    # 3) Function-length scan (C#)
    $methodRegex = [regex]'(?m)^\s*(public|private|protected|internal|static|async|override|virtual|sealed|new|partial|\s)+\s+[A-Za-z_][\w<>,\s\?\[\]]*\s+([A-Za-z_]\w*)\s*\([^)]*\)\s*(?:where[^{]+)?\{'
    foreach ($m in $methodRegex.Matches($text)) {
        $i = $m.Index + $m.Length
        $depth = 1; $bodyLines = 0; $name = $m.Groups[2].Value
        while ($i -lt $text.Length -and $depth -gt 0) {
            $c = $text[$i]
            if ($c -eq '{') { $depth++ }
            elseif ($c -eq '}') { $depth-- }
            elseif ($c -eq "`n") { $bodyLines++ }
            $i++
        }
        if ($bodyLines -gt $MaxFuncLines) {
            $violations.Add("${rel}: func-too-long $name=$bodyLines>$MaxFuncLines")
        }
    }

    # 4) Class-size scan (C#)
    $typeRegex = [regex]'(?m)^\s*(public|internal|abstract|sealed|partial|static|\s)+\s+(class|record|struct|interface)\s+([A-Za-z_]\w*)'
    foreach ($m in $typeRegex.Matches($text)) {
        $bracePos = $text.IndexOf('{', $m.Index)
        if ($bracePos -lt 0) { continue }
        $i = $bracePos + 1; $depth = 1; $bodyLines = 0; $name = $m.Groups[3].Value
        while ($i -lt $text.Length -and $depth -gt 0) {
            $c = $text[$i]
            if ($c -eq '{') { $depth++ }
            elseif ($c -eq '}') { $depth-- }
            elseif ($c -eq "`n") { $bodyLines++ }
            $i++
        }
        if ($bodyLines -gt $MaxClassLines) {
            $violations.Add("${rel}: class-too-long $name=$bodyLines>$MaxClassLines")
        }
    }
}

if ($violations.Count -eq 0) {
    Write-Output 'OK'
    exit 0
}
Write-Output 'VIOLATIONS:'
foreach ($v in $violations) { Write-Output "  $v" }
exit 1
