<#
.SYNOPSIS
    Injects "formatId" into every .whfmt file that does not already have one.
.DESCRIPTION
    For each .whfmt file under FormatDefinitions\:
      - Skips files that already contain a "formatId" property.
      - Inserts  "formatId": "<STEM>",  immediately after the "formatName" line.
      - Stem = filename without extension (e.g. ZIP.whfmt -> "ZIP").
      - File encoding (UTF-8 BOM / no-BOM) and line endings are preserved.
.PARAMETER WhatIf
    Dry-run: prints every change without writing any file.
.EXAMPLE
    .\Add-FormatId.ps1 -WhatIf
    .\Add-FormatId.ps1
#>
[CmdletBinding(SupportsShouldProcess)]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$searchRoot = Join-Path $PSScriptRoot 'FormatDefinitions'
$total = 0; $skipped = 0; $modified = 0; $errors = 0

$files = Get-ChildItem -Path $searchRoot -Filter '*.whfmt' -Recurse -File

foreach ($file in $files) {
    $total++
    $stem    = $file.BaseName
    $relPath = $file.FullName.Substring($searchRoot.Length + 1)

    try {
        $raw    = [System.IO.File]::ReadAllBytes($file.FullName)
        $hasBom = ($raw.Length -ge 3 -and $raw[0] -eq 0xEF -and $raw[1] -eq 0xBB -and $raw[2] -eq 0xBF)
        $enc    = if ($hasBom) { New-Object System.Text.UTF8Encoding($true) } else { New-Object System.Text.UTF8Encoding($false) }
        $text   = $enc.GetString($raw)

        if ($text -match '"formatId"\s*:') {
            Write-Verbose "SKIP   $relPath"
            $skipped++
            continue
        }

        # Detect line ending
        $crlf = $text.Contains("`r`n")
        $nl   = if ($crlf) { "`r`n" } else { "`n" }

        # Pattern: match "formatName" line, tolerating \r before \n (CRLF files)
        # \r? before end-of-line anchor handles CRLF transparently
        $pattern = '(?m)^([ \t]*"formatName"\s*:\s*"[^"]*",?)[ \t]*\r?$'

        $newText = [System.Text.RegularExpressions.Regex]::Replace($text, $pattern, {
            param($m)
            $full   = $m.Groups[1].Value          # e.g.  "formatName": "ZIP Archive",
            $indent = ''
            if ($full -match '^([ \t]+)') { $indent = $Matches[1] }
            # Ensure the formatName line has a trailing comma
            if (-not $full.TrimEnd().EndsWith(',')) { $full = $full.TrimEnd() + ',' }
            $full + $nl + $indent + '"formatId": "' + $stem + '",'
        })

        if ($newText -eq $text) {
            Write-Warning "NOCHANGE $relPath (formatName line not matched)"
            $skipped++
            continue
        }

        $insertedLine = '  "formatId": "' + $stem + '",'
        if ($PSCmdlet.ShouldProcess($relPath, "Insert $insertedLine")) {
            [System.IO.File]::WriteAllBytes($file.FullName, $enc.GetBytes($newText))
            Write-Host "OK     $relPath" -ForegroundColor Green
            $modified++
        } else {
            Write-Host "WOULD  $relPath  -->  $insertedLine" -ForegroundColor Cyan
        }
    }
    catch {
        Write-Warning ("ERROR  " + $relPath + " -- " + $_)
        $errors++
    }
}

Write-Host ''
Write-Host '----------------------------------------' -ForegroundColor DarkGray
Write-Host "Total    : $total"
Write-Host "Modified : $modified" -ForegroundColor Green
Write-Host "Skipped  : $skipped"  -ForegroundColor Yellow
if ($errors -gt 0) { Write-Host "Errors   : $errors" -ForegroundColor Red }
