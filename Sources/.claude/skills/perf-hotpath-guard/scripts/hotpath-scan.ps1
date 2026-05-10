#!/usr/bin/env pwsh
<#
  hotpath-scan.ps1 — apply 9 perf anti-pattern rules to hot-method bodies in
  edited C# files.
  Usage: hotpath-scan.ps1 -Files a.cs,b.cs
  Output:
    HotPath: <n> issues
      <file>:<line>  <rule>  <method> -> <snippet>
    or
    OK
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string[]]$Files,
    [string]$RepoRoot = (Resolve-Path "$PSScriptRoot\..\..\..\..\..").Path
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Core') { Write-Output "ERR=needs-pwsh7"; exit 3 }

# Hot-method name patterns (regex on method name only)
$hotMethodNames = @(
    '^OnRender$',
    '^MeasureOverride$',
    '^ArrangeOverride$',
    '^OnPreviewMouseMove$',
    '^OnScrollChanged$',
    '^Render\w+$',
    '^Draw\w+$',
    '^Paint\w+$'
)

# Hot directory hints (path-based heuristic)
$hotPathHints = @('HexEditor', 'Rendering', 'Viewport', 'GlyphRun', 'Renderer', 'ScrollHandler')

# Skip patterns
$skipPatterns = @('\\Test', '\\Mock', '\.Designer\.cs$', '\.g\.cs$', '\.g\.i\.cs$')

$issues = New-Object System.Collections.Generic.List[psobject]

function Add-I {
    param($file,$line,$rule,$method,$snippet)
    $issues.Add([pscustomobject]@{
        File = $file; Line = $line; Rule = $rule; Method = $method; Snippet = $snippet.Trim()
    })
}

function Test-IsHotMethod {
    param([string]$methodName, [string]$relPath, [string]$annotationLine)
    if ($annotationLine -match '//\s*COLD\b') { return $false }
    if ($annotationLine -match '//\s*HOT\b') { return $true }
    foreach ($pat in $hotMethodNames) {
        if ($methodName -match $pat) { return $true }
    }
    # Renderer-file heuristic: method starting with capital inside *Renderer.cs/Viewport.cs
    if ($relPath -match 'Renderer\.cs$|Viewport\.cs$|GlyphRun\.cs$' -and $methodName -match '^[A-Z]') {
        # Only the obvious cases (rendering verbs)
        if ($methodName -match '^(Render|Draw|Paint|Layout|Compose|Build|Emit)') { return $true }
    }
    return $false
}

# Rule definitions: regex + label. Some have inline-suppression "// alloc-ok"
$rules = @(
    @{ Name='alloc-in-render';        Re='\bnew\s+(?!ReadOnlySpan|Span|StringBuilder|StringComparer|EventArgs|PropertyChangedEventArgs|System\.|InvalidOperationException|ArgumentException|ArgumentNullException|NotSupportedException)[A-Z]\w*\s*\(' },
    @{ Name='linq-in-hot';            Re='\.(ToList|ToArray|Where|Select|OrderBy|OrderByDescending|GroupBy|Aggregate|Any\(.|All\(.)\(' },
    @{ Name='string-concat-loop';     Re='\+=\s*"|"\s*\+\s*[a-zA-Z_]' ; ContextLoop=$true },
    @{ Name='dispatcher-cosmetic';    Re='\bDispatcher\.BeginInvoke\b' },
    @{ Name='formatted-text-recreate';Re='\bnew\s+FormattedText\s*\(' },
    @{ Name='glyph-run-recreate';     Re='\bnew\s+GlyphRun\s*\(' },
    @{ Name='findvisualchild-render'; Re='\bVisualTreeHelper\.(GetChild|GetParent|GetChildrenCount)\b' },
    @{ Name='regex-in-render';        Re='\bnew\s+Regex\s*\(|\bRegex\.Match\s*\(|\bRegex\.IsMatch\s*\(' }
)

foreach ($f in $Files) {
    if (-not (Test-Path $f)) { continue }
    $abs = (Resolve-Path $f).Path
    $rel = $abs.Substring($RepoRoot.Length).TrimStart('\','/')
    if ([IO.Path]::GetExtension($abs).ToLowerInvariant() -ne '.cs') { continue }

    $skip = $false
    foreach ($sp in $skipPatterns) { if ($rel -match $sp) { $skip = $true; break } }
    if ($skip) { continue }

    $isHotPath = $false
    foreach ($h in $hotPathHints) { if ($rel -match $h) { $isHotPath = $true; break } }

    $text = Get-Content -LiteralPath $abs -Raw
    $lines = $text -split "`n"

    # Find method signatures with body braces
    $methodRegex = [regex]'(?m)^\s*(public|private|protected|internal|static|async|override|virtual|sealed|new|partial|\s)+\s+[A-Za-z_][\w<>,\s\?\[\]]*\s+([A-Za-z_]\w*)\s*\([^)]*\)\s*(?:where[^{]+)?\{'
    foreach ($m in $methodRegex.Matches($text)) {
        $methodName = $m.Groups[2].Value
        $sigLineNo = ($text.Substring(0, $m.Index) -split "`n").Count
        $annotation = if ($sigLineNo -ge 2) { $lines[$sigLineNo - 2] } else { '' }

        # Decide hotness: either method name matches OR file is in hot path AND name is render-y
        $hot = Test-IsHotMethod $methodName $rel $annotation
        if (-not $hot -and -not $isHotPath) { continue }
        if (-not $hot) { continue }

        # Walk the body
        $i = $m.Index + $m.Length
        $depth = 1; $bodyStart = $i; $inLoop = $false; $loopDepth = 0
        $bodyText = ''
        while ($i -lt $text.Length -and $depth -gt 0) {
            $c = $text[$i]
            if ($c -eq '{') { $depth++ }
            elseif ($c -eq '}') { $depth-- }
            $bodyText += $c
            $i++
        }

        # Apply rules per body-line
        $bodyLines = $bodyText -split "`n"
        $startLineNo = ($text.Substring(0, $bodyStart) -split "`n").Count

        $loopRegex = [regex]'\b(for|foreach|while|do)\s*\('
        for ($k = 0; $k -lt $bodyLines.Count; $k++) {
            $bl = $bodyLines[$k]
            $absLine = $startLineNo + $k

            if ($bl -match '//\s*alloc-ok\b') { continue }

            foreach ($rule in $rules) {
                if ($bl -match $rule.Re) {
                    if ($rule.ContextLoop) {
                        # Look back up to 5 lines for a loop construct
                        $hasLoop = $false
                        for ($j = [Math]::Max(0,$k-5); $j -le $k; $j++) {
                            if ($bodyLines[$j] -match $loopRegex) { $hasLoop = $true; break }
                        }
                        if (-not $hasLoop) { continue }
                    }
                    Add-I $rel $absLine $rule.Name $methodName $bl
                }
            }
        }
    }
}

if ($issues.Count -eq 0) { Write-Output 'OK'; exit 0 }

Write-Output "HotPath: $($issues.Count) issues"
foreach ($v in $issues | Sort-Object File, Line) {
    "  $($v.File):$($v.Line)  $($v.Rule)  $($v.Method) -> $($v.Snippet)"
}
exit 1
