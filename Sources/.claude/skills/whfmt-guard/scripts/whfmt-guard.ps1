<#
.SYNOPSIS
  whfmt-guard — validates *.whfmt format-definition files.

.DESCRIPTION
  Rules:
    R1  whfmt-jsonc-parse      (ERR)  JSONC parse, /* */ header tolerated
    R2  whfmt-version-monotone (ERR)  version >= HEAD version
    R3  whfmt-schema-required  (ERR)  formatName/formatId/extensions/category/description
    R4  whfmt-id-uniqueness    (ERR)  formatId unique across catalog
    R5  whfmt-magic-collision  (WARN) sig+offset+ext overlap with another file
    R6  whfmt-strength-enum    (WARN) detection.strength in allowed set
    R7  whfmt-placeholder-drift(WARN) {{var}} not in variables{}
    R10 whfmt-expression-refs  (WARN) expression references undeclared identifier
                                       (lightweight identifier-scan; full AST
                                       validation lives in C# whfmt.Validate)

  Exit code = ERR count (0 if WARN-only).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string[]]$Files,

    [string]$RepoRoot
)

$ErrorActionPreference = 'Stop'
# Strict mode intentionally OFF: JSON shapes vary across 440+ catalog files and
# we read fields defensively. Per-rule try/catch protects the aggregate run.

# ----- locate repo root + catalog root -----
if (-not $RepoRoot) {
    $RepoRoot = (git rev-parse --show-toplevel 2>$null)
    if (-not $RepoRoot) { $RepoRoot = (Resolve-Path "$PSScriptRoot/../../../../..").Path }
}
$RepoRoot    = (Resolve-Path $RepoRoot).Path
$CatalogRoot = Join-Path $RepoRoot 'Sources/Core/WpfHexEditor.Core.Definitions/FormatDefinitions'
if (-not (Test-Path $CatalogRoot)) {
    Write-Error "whfmt-guard: catalog root not found at $CatalogRoot"
    exit 2
}

$AllowedStrengths = @('None','Weak','Medium','Strong','VeryStrong')
$RequiredFields   = @('formatName','formatId','extensions','category','description')

# R10 — built-in functions + reserved keywords known to the whfmt expression engine.
# Identifiers in expressions that are NOT in this set AND not declared in
# variables{} / functions{} are flagged.
# SYNC: keep the function names in lock-step with WhfmtBuiltinFunctions.All in
#       Sources/Core/WpfHexEditor.Core.Definitions/Models/Functions/WhfmtBuiltinFunctions.cs
$ExprBuiltins = [System.Collections.Generic.HashSet[string]]::new([string[]]@(
    # built-in functions registered in WhfmtBuiltinFunctions (C#)
    'min','max','abs','length','hex','toUpper','toLower',
    # string instance methods (called as receiver.method())
    'startsWith','endsWith','includes','contains','trim',
    # literal keywords
    'true','false','null'
))

$findings = New-Object System.Collections.Generic.List[object]
function Add-Finding($sev, $rule, $file, $detail) {
    $findings.Add([pscustomobject]@{ Sev=$sev; Rule=$rule; File=$file; Detail=$detail })
}

# R10 — locate the whfmt-validate CLI (built from Tools/whfmt.Validate). If present,
# R10 delegates to the C# WhfmtExpressionValidator (full AST walk, R10-000…R10-003).
# Otherwise the PowerShell identifier-scan fallback runs as before.
function Find-WhfmtValidateBinary {
    # Built locally? Look for the standard MSBuild output paths first.
    $candidates = @(
        (Join-Path $RepoRoot 'Sources/Tools/whfmt.Validate/bin/Debug/net8.0/whfmt-validate.dll'),
        (Join-Path $RepoRoot 'Sources/Tools/whfmt.Validate/bin/Release/net8.0/whfmt-validate.dll')
    )
    foreach ($c in $candidates) {
        if (Test-Path -LiteralPath $c) { return $c }
    }
    # Installed as a global tool?
    $cmd = Get-Command 'whfmt' -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}
$WhfmtCli = Find-WhfmtValidateBinary

# ----- filter input -----
$targets = @()
foreach ($f in $Files) {
    if (-not $f) { continue }
    $full = if ([System.IO.Path]::IsPathRooted($f)) { $f } else { Join-Path $RepoRoot $f }
    if (-not (Test-Path $full -PathType Leaf)) { continue }
    if ($full -notmatch '\.whfmt$') { continue }
    if ($full -match '[\\/](Tests|Samples)[\\/]') { continue }
    $fullResolved = (Resolve-Path $full).Path
    $catalogPrefix = $CatalogRoot.TrimEnd('\','/')
    if (-not ($fullResolved.StartsWith($catalogPrefix, [System.StringComparison]::OrdinalIgnoreCase))) {
        Write-Verbose "skip: $fullResolved not under $catalogPrefix"
        continue
    }
    $targets += $fullResolved
}
Write-Host "[whfmt-guard] scanned: $($targets.Count) file(s)"
if ($targets.Count -eq 0) { exit 0 }

# ----- helpers -----
function Test-Prop($obj, [string]$name) {
    if ($null -eq $obj) { return $false }
    if ($obj -is [System.Collections.IDictionary]) { return $obj.Contains($name) }
    if ($obj -is [psobject]) { return @($obj.PSObject.Properties.Name) -contains $name }
    return $false
}
function Get-PropNames($obj) {
    if ($null -eq $obj) { return @() }
    if ($obj -is [System.Collections.IDictionary]) { return @($obj.Keys) }
    if ($obj -is [psobject]) { return @($obj.PSObject.Properties.Name) }
    return @()
}

# R10 helper: a string is an expression candidate when it carries an operator.
# Long letter-heavy strings with very few operators are treated as prose (the
# catalog uses forensic.suspiciousPatterns[].condition for both shapes).
function Test-LooksLikeExpression([string]$s) {
    if (-not $s) { return $false }
    $hasOp = ($s -match '==|!=|<=|>=|<<|>>|&&|\|\||[<>+\-*/%^~?()\[\]]|(?<![A-Za-z])(?:&|\|)(?![A-Za-z])')
    if (-not $hasOp) { return $false }
    $letters = ([regex]::Matches($s, '[A-Za-z]')).Count
    $opChars = ([regex]::Matches($s, '[=<>!&|+\-*/%^~?()\[\]]')).Count
    if ($opChars -eq 0) { return $false }
    if ($letters -gt 80 -and ($letters / [Math]::Max($opChars,1)) -gt 25) { return $false }
    return $true
}

# R10 helper: collects every expression-bearing string in the document.
function Get-ExprStrings($p) {
    $out = [System.Collections.Generic.List[object]]::new()
    if ((Test-Prop $p 'assertions') -and $p.assertions) {
        foreach ($a in @($p.assertions)) {
            if ((Test-Prop $a 'expression') -and $a.expression) {
                $out.Add([pscustomobject]@{ Src=[string]$a.expression; Path='assertions[].expression' })
            }
        }
    }
    if ((Test-Prop $p 'blocks') -and $p.blocks) {
        foreach ($b in @($p.blocks)) {
            if ((Test-Prop $b 'expression') -and $b.expression) {
                $out.Add([pscustomobject]@{ Src=[string]$b.expression; Path='blocks[].expression' })
            }
            if ((Test-Prop $b 'condition') -and $b.condition) {
                $out.Add([pscustomobject]@{ Src=[string]$b.condition; Path='blocks[].condition' })
            }
        }
    }
    if ((Test-Prop $p 'forensic') -and $p.forensic -and (Test-Prop $p.forensic 'suspiciousPatterns')) {
        foreach ($s in @($p.forensic.suspiciousPatterns)) {
            if ($s -is [psobject] -and (Test-Prop $s 'condition') -and $s.condition `
                -and (Test-LooksLikeExpression $s.condition)) {
                $out.Add([pscustomobject]@{ Src=[string]$s.condition; Path='forensic.suspiciousPatterns[].condition' })
            }
        }
    }
    return ,$out
}

# R10 PowerShell fallback when the C# whfmt-validate binary isn't built. Same
# heuristic identifier-scan as before but hoisted to module scope (was nested
# inside the per-target foreach which broke declaration-order under StrictMode).
function Invoke-R10PowerShellFallback($parsed, $declared, $rel) {
    $declaredFns = @()
    if ((Test-Prop $parsed 'functions') -and $parsed.functions) {
        $declaredFns = Get-PropNames $parsed.functions
    }
    $known = [System.Collections.Generic.HashSet[string]]::new($ExprBuiltins)
    foreach ($n in $declared)    { [void]$known.Add($n) }
    foreach ($n in $declaredFns) { [void]$known.Add($n) }

    foreach ($expr in (Get-ExprStrings $parsed)) {
        $stripped = [regex]::Replace($expr.Src, "'(?:[^'\\]|\\.)*'", "''")
        $stripped = [regex]::Replace($stripped,  '"(?:[^"\\]|\\.)*"', '""')
        $reported = [System.Collections.Generic.HashSet[string]]::new()
        foreach ($m in [regex]::Matches($stripped, '(?<![.\w])([A-Za-z_][A-Za-z0-9_]*)')) {
            $id = $m.Groups[1].Value
            if ($known.Contains($id)) { continue }
            if (-not $reported.Add($id)) { continue }
            Add-Finding 'WARN' 'whfmt-expression-refs' $rel ("{0}: '{1}' not declared in variables{{}} / functions{{}} / builtins" -f $expr.Path, $id)
        }
    }
}

function Read-Whfmt([string]$path) {
    $raw = Get-Content -LiteralPath $path -Raw -Encoding UTF8
    # strip UTF-8 BOM
    if ($raw.Length -gt 0 -and [int][char]$raw[0] -eq 0xFEFF) { $raw = $raw.Substring(1) }
    # strip leading /* ... */ header (only the first one, only if it precedes '{')
    $trim = $raw.TrimStart()
    if ($trim.StartsWith('/*')) {
        $end = $trim.IndexOf('*/')
        if ($end -ge 0) { $raw = $trim.Substring($end + 2) }
    }
    return $raw
}

function ConvertFrom-WhfmtJson([string]$text) {
    try {
        $clean = [regex]::Replace($text, '(?m)^\s*//.*$', '')
        # Two-pass: prefer pscustomobject (lets us use .Property dot access);
        # fall back to hashtable when JSON has same-name keys with different casing
        # (Newtonsoft, used at runtime by ImportFromJson, tolerates this; ConvertFrom-Json does not).
        try {
            $obj = ConvertFrom-Json -InputObject $clean -Depth 64
        } catch {
            if ($_.Exception.Message -match 'different casing') {
                $obj = ConvertFrom-Json -InputObject $clean -Depth 64 -AsHashtable
            } else { throw }
        }
        return [pscustomobject]@{ Ok=$true; Value=$obj; Error=$null }
    } catch {
        return [pscustomobject]@{ Ok=$false; Value=$null; Error=$_.Exception.Message }
    }
}

function Get-HeadContent([string]$relPath) {
    $head = git -C $RepoRoot show "HEAD:$relPath" 2>$null
    if ($LASTEXITCODE -ne 0) { return $null }
    return ($head -join "`n")
}

function Compare-Semver([string]$a, [string]$b) {
    # returns -1/0/1 ; treats missing/blank as 0.0
    function Parse($v) {
        $parts = @(($v -split '[.\-+]') | Where-Object { $_ -match '^\d+$' } | ForEach-Object { [int]$_ })
        while ($parts.Count -lt 3) { $parts += 0 }
        return $parts
    }
    $pa = Parse $a; $pb = Parse $b
    for ($i=0; $i -lt [Math]::Max($pa.Count,$pb.Count); $i++) {
        $va = if ($i -lt $pa.Count) { $pa[$i] } else { 0 }
        $vb = if ($i -lt $pb.Count) { $pb[$i] } else { 0 }
        if ($va -lt $vb) { return -1 }
        if ($va -gt $vb) { return  1 }
    }
    return 0
}

# ----- build catalog index once (for R4/R5) -----
$catalog = @{}  # path -> @{ id; sig; offset; exts; note }
$allWhfmt = Get-ChildItem -LiteralPath $CatalogRoot -Recurse -Filter '*.whfmt' -ErrorAction SilentlyContinue
foreach ($e in $allWhfmt) {
    try {
        $raw = Read-Whfmt $e.FullName
        $r = ConvertFrom-WhfmtJson $raw
        if (-not $r.Ok) { continue }
        $p = $r.Value
        $sig = $null; $off = 0; $note = $null
        if ((Test-Prop $p 'detection') -and $p.detection) {
            if (Test-Prop $p.detection 'signature') { $sig = [string]$p.detection.signature }
            if (Test-Prop $p.detection 'offset')    { try { $off = [int]$p.detection.offset } catch { $off = 0 } }
            if ((Test-Prop $p.detection 'validation') -and $p.detection.validation -and (Test-Prop $p.detection.validation 'note')) {
                $note = [string]$p.detection.validation.note
            }
        }
        $exts = @()
        if ((Test-Prop $p 'extensions') -and $p.extensions) {
            $exts = @($p.extensions | ForEach-Object { ([string]$_).ToLowerInvariant() })
        }
        $id = $null
        if (Test-Prop $p 'formatId') { $id = [string]$p.formatId }
        $catalog[$e.FullName] = @{ Id=$id; Sig=$sig; Offset=$off; Exts=$exts; Note=$note }
    } catch { }
}

# ----- per-target rules -----
foreach ($path in $targets) {
    $rel = [System.IO.Path]::GetRelativePath($RepoRoot, $path).Replace('\','/')
    try {
    $raw = Read-Whfmt $path
    $pr = ConvertFrom-WhfmtJson $raw

    # R1
    if (-not $pr.Ok) {
        Add-Finding 'ERR' 'whfmt-jsonc-parse' $rel $pr.Error
        continue   # later rules need a parsed object
    }
    $parsed = $pr.Value

    # R3 — required fields
    foreach ($k in $RequiredFields) {
        $present = Test-Prop $parsed $k
        if (-not $present) {
            Add-Finding 'ERR' 'whfmt-schema-required' $rel "missing field '$k'"
        } elseif ($k -eq 'extensions') {
            $val = $parsed.extensions
            if (-not $val -or @($val).Count -eq 0) {
                Add-Finding 'ERR' 'whfmt-schema-required' $rel "extensions must be a non-empty array"
            }
        } elseif (-not $parsed.$k) {
            Add-Finding 'ERR' 'whfmt-schema-required' $rel "field '$k' is empty"
        }
    }

    # R2 — version monotone
    if ((Test-Prop $parsed 'version') -and $parsed.version) {
        $headRaw = Get-HeadContent $rel
        if ($headRaw) {
            $stripped = $headRaw.TrimStart()
            if ($stripped.StartsWith('/*')) {
                $e = $stripped.IndexOf('*/'); if ($e -ge 0) { $headRaw = $stripped.Substring($e+2) }
            }
            $headPr = ConvertFrom-WhfmtJson $headRaw
            if ($headPr.Ok -and $headPr.Value.PSObject.Properties.Name -contains 'version' -and $headPr.Value.version) {
                $cmp = Compare-Semver ([string]$parsed.version) ([string]$headPr.Value.version)
                if ($cmp -lt 0) {
                    Add-Finding 'ERR' 'whfmt-version-monotone' $rel ("version {0} < HEAD {1}" -f $parsed.version, $headPr.Value.version)
                }
            }
        }
    }

    # R4 — id uniqueness
    if ((Test-Prop $parsed 'formatId') -and $parsed.formatId) {
        $thisId = [string]$parsed.formatId
        foreach ($kv in $catalog.GetEnumerator()) {
            if ($kv.Key -ieq $path) { continue }
            if ($kv.Value.Id -and ($kv.Value.Id -ieq $thisId)) {
                $otherRel = [System.IO.Path]::GetRelativePath($RepoRoot, $kv.Key).Replace('\','/')
                Add-Finding 'ERR' 'whfmt-id-uniqueness' $rel "formatId '$thisId' also used by $otherRel"
            }
        }
    }

    # R5 — magic collision (WARN, suppressed by detection.validation.note)
    $thisInfo = $catalog[$path]
    if ($thisInfo -and $thisInfo.Sig -and -not $thisInfo.Note) {
        foreach ($kv in $catalog.GetEnumerator()) {
            if ($kv.Key -ieq $path) { continue }
            $o = $kv.Value
            if (-not $o.Sig) { continue }
            if ($o.Sig -ne $thisInfo.Sig) { continue }
            if ($o.Offset -ne $thisInfo.Offset) { continue }
            $overlap = @($o.Exts | Where-Object { $thisInfo.Exts -contains $_ })
            if ($overlap.Count -gt 0) {
                $otherRel = [System.IO.Path]::GetRelativePath($RepoRoot, $kv.Key).Replace('\','/')
                Add-Finding 'WARN' 'whfmt-magic-collision' $rel ("sig {0} @ off {1} shared with {2} (ext {3})" -f $thisInfo.Sig, $thisInfo.Offset, $otherRel, ($overlap -join ','))
            }
        }
    }

    # R6 — strength enum
    if ((Test-Prop $parsed 'detection') -and (Test-Prop $parsed.detection 'strength') -and $parsed.detection.strength) {
        $s = [string]$parsed.detection.strength
        if ($AllowedStrengths -notcontains $s) {
            Add-Finding 'WARN' 'whfmt-strength-enum' $rel "detection.strength '$s' not in {$($AllowedStrengths -join ', ')}"
        }
    }

    # R7 — placeholder drift. Variables come in two schemas:
    #   dict       : { "magic": "" }
    #   typed array: [ { "name": "magic", ... } ]
    $declared = @()
    if ((Test-Prop $parsed 'variables') -and $parsed.variables) {
        $vars = $parsed.variables
        $isTypedArray = ($vars -is [System.Array]) -or
                        ($vars -is [System.Collections.IList] -and -not ($vars -is [System.Collections.IDictionary]))
        if ($isTypedArray) {
            foreach ($v in @($vars)) {
                if ((Test-Prop $v 'name') -and $v.name) { $declared += [string]$v.name }
            }
        } else {
            $declared = Get-PropNames $vars
        }
    }
    $texts = @()
    if ((Test-Prop $parsed 'description') -and $parsed.description) {
        $texts += [string]$parsed.description
    }
    if ((Test-Prop $parsed 'blocks') -and $parsed.blocks) {
        foreach ($b in @($parsed.blocks)) {
            if ((Test-Prop $b 'description') -and $b.description) {
                $texts += [string]$b.description
            }
        }
    }
    $referenced = New-Object System.Collections.Generic.HashSet[string]
    foreach ($t in $texts) {
        foreach ($m in [regex]::Matches($t, '\{\{\s*([A-Za-z_][A-Za-z0-9_]*)\s*\}\}')) {
            [void]$referenced.Add($m.Groups[1].Value)
        }
    }
    foreach ($v in $referenced) {
        if ($declared -notcontains $v) {
            Add-Finding 'WARN' 'whfmt-placeholder-drift' $rel "{{$v}} referenced but not declared in variables{}"
        }
    }

    # R10 PS fallback only (per-file scan). The C# binary path runs in a single
    # batched call AFTER the foreach loop — see post-loop section below.
    if (-not $WhfmtCli) {
        Invoke-R10PowerShellFallback $parsed $declared $rel
    }
    } catch {
        Add-Finding 'ERR' 'whfmt-guard-internal' $rel ("rule evaluation failed: " + $_.Exception.Message)
    }
}

# R10 — batched C# binary invocation. ONE process start for all targets instead
# of N (saves ~200 ms cold-start per file). Skipped when the binary isn't built;
# the PS fallback already ran per-file inside the loop above in that case.
if ($WhfmtCli -and $targets.Count -gt 0) {
    try {
        $cliArgs = @('lint-expressions', '--json') + $targets
        if ($WhfmtCli -like '*.dll') {
            $output = & dotnet $WhfmtCli @cliArgs 2>&1
        } else {
            $output = & $WhfmtCli @cliArgs 2>&1
        }
        $sawAny = $false
        foreach ($line in @($output)) {
            $text = [string]$line
            if (-not $text.StartsWith('{')) { continue }
            try {
                $obj = ConvertFrom-Json -InputObject $text -ErrorAction Stop
                $sawAny = $true
                $rel    = [System.IO.Path]::GetRelativePath($RepoRoot, $obj.file).Replace('\','/')
                Add-Finding 'WARN' 'whfmt-expression-refs' $rel ("{0}: {1} [{2}]" -f $obj.path, $obj.message, $obj.ruleId)
            } catch { }
        }
        # Defensive: surface a single internal finding when the binary exited
        # non-zero but emitted no parseable JSON (otherwise the failure is invisible).
        if ($LASTEXITCODE -ne 0 -and -not $sawAny) {
            Add-Finding 'WARN' 'whfmt-guard-internal' '' ("whfmt-validate exited {0} with no JSON output" -f $LASTEXITCODE)
        }
    } catch {
        Add-Finding 'WARN' 'whfmt-guard-internal' '' ("whfmt-validate invocation failed: " + $_.Exception.Message)
    }
}

# ----- report -----
$errCount  = @($findings | Where-Object { $_.Sev -eq 'ERR'  }).Count
$warnCount = @($findings | Where-Object { $_.Sev -eq 'WARN' }).Count

foreach ($f in $findings) {
    Write-Host ("  {0,-4} {1}: {2} — {3}" -f $f.Sev, $f.Rule, $f.File, $f.Detail)
}
Write-Host "summary: $errCount error(s), $warnCount warning(s)"

exit [Math]::Min($errCount, 100)
