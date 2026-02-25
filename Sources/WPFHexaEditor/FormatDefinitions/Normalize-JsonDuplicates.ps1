# Normalize-JsonDuplicates.ps1
# Removes duplicate snake_case JSON sections from format definition files.
# Uses text-based processing because ConvertFrom-Json fails on duplicate keys.
# Keeps PascalCase versions (matching C# FormatDefinition model).

param(
    [string]$RootPath = $PSScriptRoot,
    [switch]$DryRun
)

# Snake_case keys to remove (their PascalCase equivalents stay)
$keysToRemove = @(
    "quality_metrics",
    "software",
    "use_cases",
    "mime_types",
    "format_relationships",
    "technical_details",
    "version_history"
)

$totalFiles = 0
$modifiedFiles = 0
$totalKeysRemoved = 0

function Remove-JsonTopLevelKey {
    param(
        [string]$Json,
        [string]$Key
    )

    # Match a top-level key with its value (object, array, or primitive)
    # Pattern: "key": value, (with optional trailing comma)
    # Value can be: { ... }, [ ... ], "string", number, true/false/null

    # First try to match object or array value (with brace/bracket counting)
    $pattern = '(?m)^  "' + [regex]::Escape($Key) + '"\s*:\s*'
    $match = [regex]::Match($Json, $pattern)

    if (-not $match.Success) {
        return @{ Json = $Json; Removed = $false }
    }

    $startPos = $match.Index
    $valueStart = $match.Index + $match.Length

    # Determine value type and find end
    $firstChar = $Json[$valueStart]
    $endPos = $valueStart

    if ($firstChar -eq '{' -or $firstChar -eq '[') {
        # Count braces/brackets to find matching close
        $openChar = $firstChar
        $closeChar = if ($firstChar -eq '{') { '}' } else { ']' }
        $depth = 0
        $inString = $false
        $escaped = $false

        for ($i = $valueStart; $i -lt $Json.Length; $i++) {
            $c = $Json[$i]

            if ($escaped) {
                $escaped = $false
                continue
            }

            if ($c -eq '\') {
                $escaped = $true
                continue
            }

            if ($c -eq '"') {
                $inString = -not $inString
                continue
            }

            if (-not $inString) {
                if ($c -eq $openChar) { $depth++ }
                elseif ($c -eq $closeChar) {
                    $depth--
                    if ($depth -eq 0) {
                        $endPos = $i + 1
                        break
                    }
                }
            }
        }
    }
    else {
        # Primitive value - find end (next comma or newline before closing brace)
        $restMatch = [regex]::Match($Json.Substring($valueStart), '[,\n]')
        if ($restMatch.Success) {
            $endPos = $valueStart + $restMatch.Index
        }
    }

    # Extend to include trailing comma and whitespace
    $removeEnd = $endPos
    $remaining = $Json.Substring($removeEnd)
    $trailingMatch = [regex]::Match($remaining, '^\s*,?\s*\r?\n?')
    if ($trailingMatch.Success) {
        $removeEnd += $trailingMatch.Length
    }

    # Also remove leading newline if present
    $removeStart = $startPos
    if ($removeStart -gt 0 -and $Json[$removeStart - 1] -eq "`n") {
        $removeStart--
        if ($removeStart -gt 0 -and $Json[$removeStart - 1] -eq "`r") {
            $removeStart--
        }
    }

    $newJson = $Json.Substring(0, $removeStart) + $Json.Substring($removeEnd)

    return @{ Json = $newJson; Removed = $true }
}

Get-ChildItem -Path $RootPath -Filter "*.json" -Recurse | ForEach-Object {
    $file = $_
    $totalFiles++

    try {
        $json = Get-Content -Path $file.FullName -Raw -Encoding UTF8
        $keysRemovedThisFile = 0

        foreach ($key in $keysToRemove) {
            # Check if the key exists in the file
            if ($json -match ('"' + [regex]::Escape($key) + '"')) {
                $result = Remove-JsonTopLevelKey -Json $json -Key $key
                if ($result.Removed) {
                    $json = $result.Json
                    $keysRemovedThisFile++
                }
            }
        }

        # Clean up any trailing comma before closing brace
        $json = $json -replace ',(\s*\r?\n\s*})\s*$', '$1'

        if ($keysRemovedThisFile -gt 0) {
            $modifiedFiles++
            $totalKeysRemoved += $keysRemovedThisFile

            if (-not $DryRun) {
                [System.IO.File]::WriteAllText($file.FullName, $json, [System.Text.UTF8Encoding]::new($false))
            }
            Write-Host "  [$keysRemovedThisFile keys] $($file.Name)" -ForegroundColor Yellow
        }
    }
    catch {
        Write-Host "  ERROR: $($file.Name) - $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "=== Summary ===" -ForegroundColor Cyan
Write-Host "  Total files scanned: $totalFiles"
Write-Host "  Files modified: $modifiedFiles"
Write-Host "  Total keys removed: $totalKeysRemoved"
if ($DryRun) {
    Write-Host "  (DRY RUN - no files were modified)" -ForegroundColor Magenta
}
