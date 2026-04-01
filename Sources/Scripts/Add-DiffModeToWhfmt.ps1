# =============================================================================
# Add-DiffModeToWhfmt.ps1
# Injects a root-level "diffMode" field into every .whfmt format definition.
#
# Rules:
#   "semantic" → structured data / markup / typed source languages (C#, XML, JSON…)
#   "text"     → plain-text programming languages (Python, JS, SQL, Shell…)
#   "binary"   → everything else (3D, Archives, Audio, Certificates, Images…)
# =============================================================================

$semanticFiles = @(
    "JSON", "TOML", "YAML",          # Data
    "XML",                            # Documents
    "CSharp", "VBNet", "FSharp",      # .NET languages
    "XAML", "XMLMarkup", "HTML"       # Markup / UI
)

$textFiles = @(
    "Assembly", "Batch", "C", "Cpp", "Dart", "Go",
    "Java", "JavaScript", "Kotlin", "Lua", "Markdown",
    "PHP", "Perl", "PowerShell", "Python", "Ruby",
    "Rust", "SQL", "Shell", "Swift", "TypeScript"
)

$root    = "$PSScriptRoot\WpfHexEditor.Core.Definitions\FormatDefinitions"
$files   = Get-ChildItem $root -Recurse -Filter "*.whfmt"
$updated = 0
$skipped = 0

foreach ($f in $files) {
    $stem = $f.BaseName

    $mode = if   ($semanticFiles -contains $stem) { "semantic" }
            elseif ($textFiles   -contains $stem) { "text" }
            else                                  { "binary" }

    # Strip any existing diffMode lines first (idempotent)
    $lines   = [System.IO.File]::ReadAllLines($f.FullName, [System.Text.Encoding]::UTF8)
    $hasDiff = $lines | Where-Object { $_ -match '"diffMode"' }
    if ($hasDiff) { $skipped++; continue }

    $content = $lines -join "`n"

    # Inject after the FIRST '{' only — the root JSON object opener.
    # Using IndexOf() instead of regex to avoid matching nested objects.
    $idx = $content.IndexOf('{')
    if ($idx -lt 0) { continue }
    $newContent = $content.Substring(0, $idx + 1) +
                  "`n  `"diffMode`": `"$mode`"," +
                  $content.Substring($idx + 1)

    [System.IO.File]::WriteAllText($f.FullName, $newContent, [System.Text.Encoding]::UTF8)
    $updated++
}

Write-Host "Done. Updated: $updated | Already tagged (skipped): $skipped | Total: $($files.Count)"
