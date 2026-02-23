# ============================================
# Format Definition Validator Script
# Apache 2.0 - 2026
# ============================================

param(
    [string]$FormatDefinitionsPath = "",
    [string]$OutputPath = "ValidationReport.md",
    [switch]$Verbose
)

Write-Host "=== WPF HexaEditor - Format Definition Validator ===" -ForegroundColor Cyan
Write-Host ""

# Find FormatDefinitions directory
if ([string]::IsNullOrEmpty($FormatDefinitionsPath)) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $FormatDefinitionsPath = Join-Path $scriptDir "..\FormatDefinitions"
    $FormatDefinitionsPath = [System.IO.Path]::GetFullPath($FormatDefinitionsPath)
}

if (-not (Test-Path $FormatDefinitionsPath)) {
    Write-Host "ERROR: FormatDefinitions directory not found at: $FormatDefinitionsPath" -ForegroundColor Red
    exit 1
}

Write-Host "Format Definitions Path: $FormatDefinitionsPath" -ForegroundColor Green
Write-Host ""

# Get all JSON files
$jsonFiles = Get-ChildItem -Path $FormatDefinitionsPath -Filter "*.json" -Recurse

Write-Host "Found $($jsonFiles.Count) JSON files" -ForegroundColor Yellow
Write-Host ""

# Initialize counters
$totalFiles = 0
$validFiles = 0
$invalidFiles = 0
$totalErrors = 0
$totalWarnings = 0
$invalidFilesList = @()
$detailedResults = @()

# Required top-level properties
$requiredProperties = @(
    "formatName",
    "version",
    "extensions",
    "description",
    "category",
    "author",
    "detection",
    "variables",
    "blocks"
)

# Validate each file
foreach ($file in $jsonFiles) {
    $totalFiles++
    $fileName = $file.Name
    $relativePath = $file.FullName.Replace($FormatDefinitionsPath, "").TrimStart('\', '/')

    if ($Verbose) {
        Write-Host "Validating: $relativePath" -ForegroundColor Gray
    }

    $fileErrors = @()
    $fileWarnings = @()
    $isValid = $true

    try {
        # Read and parse JSON
        $jsonContent = Get-Content $file.FullName -Raw -ErrorAction Stop
        $json = $jsonContent | ConvertFrom-Json -ErrorAction Stop

        # Check required properties
        foreach ($prop in $requiredProperties) {
            if (-not ($json.PSObject.Properties.Name -contains $prop)) {
                $fileErrors += "Missing required property: $prop"
                $isValid = $false
            }
            else {
                $value = $json.$prop

                # Validate non-empty strings
                if ($prop -in @("formatName", "version", "description", "category", "author")) {
                    if ([string]::IsNullOrWhiteSpace($value)) {
                        $fileErrors += "Property '$prop' cannot be empty"
                        $isValid = $false
                    }
                }

                # Validate extensions array
                if ($prop -eq "extensions") {
                    if ($value -isnot [Array] -or $value.Count -eq 0) {
                        $fileErrors += "Property 'extensions' must be a non-empty array"
                        $isValid = $false
                    }
                }

                # Validate detection object
                if ($prop -eq "detection") {
                    if ($null -eq $value) {
                        $fileErrors += "Property 'detection' cannot be null"
                        $isValid = $false
                    }
                    else {
                        if (-not ($value.PSObject.Properties.Name -contains "signature")) {
                            $fileErrors += "Detection section missing required property: signature"
                            $isValid = $false
                        }
                        if (-not ($value.PSObject.Properties.Name -contains "offset")) {
                            $fileErrors += "Detection section missing required property: offset"
                            $isValid = $false
                        }
                        if (-not ($value.PSObject.Properties.Name -contains "required")) {
                            $fileWarnings += "Detection section missing recommended property: required"
                        }

                        # Validate signature hex format
                        if ($value.signature) {
                            $sig = $value.signature
                            if ($sig.Length % 2 -ne 0) {
                                $fileErrors += "Detection.signature must have even number of hex digits"
                                $isValid = $false
                            }
                            if ($sig -notmatch '^[0-9A-Fa-f]+$') {
                                $fileErrors += "Detection.signature contains invalid hex characters"
                                $isValid = $false
                            }
                        }
                    }
                }

                # Validate variables object
                if ($prop -eq "variables") {
                    if ($value -isnot [PSCustomObject] -and $value -ne $null) {
                        $fileErrors += "Property 'variables' must be an object"
                        $isValid = $false
                    }
                }

                # Validate blocks array
                if ($prop -eq "blocks") {
                    if ($value -isnot [Array] -or $value.Count -eq 0) {
                        $fileErrors += "Property 'blocks' must be a non-empty array"
                        $isValid = $false
                    }
                    else {
                        # Check first block has required properties
                        $firstBlock = $value[0]
                        if (-not ($firstBlock.PSObject.Properties.Name -contains "type")) {
                            $fileErrors += "Block[0] missing required property: type"
                            $isValid = $false
                        }
                        if (-not ($firstBlock.PSObject.Properties.Name -contains "name")) {
                            $fileWarnings += "Block[0] missing recommended property: name"
                        }
                    }
                }
            }
        }
    }
    catch {
        $fileErrors += "JSON parsing error: $($_.Exception.Message)"
        $isValid = $false
    }

    # Update counters
    if ($isValid) {
        $validFiles++
        if ($Verbose) {
            Write-Host "  ✓ VALID" -ForegroundColor Green
        }
    }
    else {
        $invalidFiles++
        $invalidFilesList += $relativePath
        Write-Host "  ✗ INVALID - $relativePath" -ForegroundColor Red
        foreach ($err in $fileErrors) {
            Write-Host "    ERROR: $err" -ForegroundColor Red
        }
    }

    $totalErrors += $fileErrors.Count
    $totalWarnings += $fileWarnings.Count

    # Store detailed result
    $detailedResults += [PSCustomObject]@{
        File = $relativePath
        IsValid = $isValid
        Errors = $fileErrors
        Warnings = $fileWarnings
    }
}

# Calculate success rate
$successRate = if ($totalFiles -gt 0) { [math]::Round(($validFiles / $totalFiles) * 100, 1) } else { 0 }

# Display summary
Write-Host ""
Write-Host "=== Validation Summary ===" -ForegroundColor Cyan
Write-Host "Total Files:    $totalFiles" -ForegroundColor White
Write-Host "Valid Files:    $validFiles" -ForegroundColor Green
Write-Host "Invalid Files:  $invalidFiles" -ForegroundColor $(if ($invalidFiles -gt 0) { "Red" } else { "Green" })
Write-Host "Total Errors:   $totalErrors" -ForegroundColor $(if ($totalErrors -gt 0) { "Red" } else { "Green" })
Write-Host "Total Warnings: $totalWarnings" -ForegroundColor Yellow
Write-Host "Success Rate:   $successRate%" -ForegroundColor $(if ($successRate -eq 100) { "Green" } else { "Yellow" })
Write-Host ""

# Generate Markdown report
$reportContent = "# Format Definition Validation Report`r`n`r`n"
$reportContent += "**Generated:** $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')`r`n"
$reportContent += "**Path:** $FormatDefinitionsPath`r`n`r`n"
$reportContent += "## Summary`r`n`r`n"
$reportContent += "- **Total Files:** $totalFiles`r`n"
$reportContent += "- **Valid Files:** $validFiles`r`n"
$reportContent += "- **Invalid Files:** $invalidFiles`r`n"
$reportContent += "- **Total Errors:** $totalErrors`r`n"
$reportContent += "- **Total Warnings:** $totalWarnings`r`n"
$reportContent += "- **Success Rate:** $successRate%`r`n`r`n"

if ($invalidFiles -gt 0) {
    $reportContent += "## Invalid Files`r`n`r`n"
    $reportContent += "The following files have validation errors:`r`n`r`n"
    foreach ($file in $invalidFilesList) {
        $reportContent += "- ``$file```r`n"
    }
    $reportContent += "`r`n"
}

$reportContent += "## Detailed Results`r`n`r`n"

foreach ($result in $detailedResults) {
    if (-not $result.IsValid -or $result.Warnings.Count -gt 0) {
        $status = if ($result.IsValid) { "⚠️ VALID (with warnings)" } else { "❌ INVALID" }
        $reportContent += "### $status - $($result.File)`r`n`r`n"

        if ($result.Errors.Count -gt 0) {
            $reportContent += "**Errors:**`r`n"
            foreach ($err in $result.Errors) {
                $reportContent += "- $err`r`n"
            }
            $reportContent += "`r`n"
        }
        if ($result.Warnings.Count -gt 0) {
            $reportContent += "**Warnings:**`r`n"
            foreach ($warn in $result.Warnings) {
                $reportContent += "- $warn`r`n"
            }
            $reportContent += "`r`n"
        }
    }
}

# Required Sections Reference
$reportContent += "## Required Sections Reference`r`n`r`n"
$reportContent += "All format definition JSON files must contain these 9 top-level sections:`r`n`r`n"
$reportContent += "1. **formatName** (string) - Human-readable format name`r`n"
$reportContent += "2. **version** (string) - Definition version (e.g., `"1.0`")`r`n"
$reportContent += "3. **extensions** (array) - File extensions (e.g., [`".zip`"])`r`n"
$reportContent += "4. **description** (string) - Detailed format description`r`n"
$reportContent += "5. **category** (string) - Format category (e.g., `"Game`", `"Archives`")`r`n"
$reportContent += "6. **author** (string) - Definition author (typically `"WPFHexaEditor Team`")`r`n"
$reportContent += "7. **detection** (object) - Detection rules with signature, offset, required`r`n"
$reportContent += "8. **variables** (object) - State variables (can be empty {})`r`n"
$reportContent += "9. **blocks** (array) - Block definitions (at least 1 block required)`r`n"

# Save report
$reportPath = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) $OutputPath
$reportContent | Out-File -FilePath $reportPath -Encoding UTF8

Write-Host "Report saved to: $reportPath" -ForegroundColor Green

# Exit with appropriate code
exit $(if ($invalidFiles -eq 0) { 0 } else { 1 })
