# ==========================================================
# Script : Download-LspServers.ps1
# Purpose: Download bundled LSP server binaries into tools/lsp/
#          Run once after cloning the repo, or with -Force to re-download.
#
# Servers:
#   OmniSharp  v1.39.12  (C# / VB.NET)   -> tools/lsp/OmniSharp/
#   clangd     v18.1.3   (C / C++ / Obj-C) -> tools/lsp/clangd/
#
# Usage:
#   .\Scripts\Download-LspServers.ps1
#   .\Scripts\Download-LspServers.ps1 -Force
# ==========================================================

[CmdletBinding()]
param(
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent
$toolsLsp = Join-Path $repoRoot 'tools\lsp'

function Download-Server {
    param(
        [string]$Name,
        [string]$Url,
        [string]$DestDir,
        [string]$ZipEntry   # subfolder inside zip to strip (e.g. "omnisharp-win-x64-net6.0")
    )

    if ((Test-Path $DestDir) -and -not $Force) {
        Write-Host "  [SKIP] $Name already present ($DestDir)" -ForegroundColor DarkGray
        return
    }

    $zipPath = Join-Path $env:TEMP "$Name-lsp-download.zip"
    Write-Host "  [DOWN] $Name  <-  $Url" -ForegroundColor Cyan

    $ProgressPreference = 'SilentlyContinue'
    Invoke-WebRequest -Uri $Url -OutFile $zipPath -UseBasicParsing

    if (Test-Path $DestDir) { Remove-Item $DestDir -Recurse -Force }
    New-Item -ItemType Directory -Path $DestDir | Out-Null

    Write-Host "  [EXTR] Extracting $Name..." -ForegroundColor Cyan
    $tmpExtract = Join-Path $env:TEMP "$Name-lsp-extract"
    if (Test-Path $tmpExtract) { Remove-Item $tmpExtract -Recurse -Force }
    Expand-Archive -Path $zipPath -DestinationPath $tmpExtract -Force

    # If the zip contains a single top-level folder, flatten it.
    $children = Get-ChildItem $tmpExtract
    if ($children.Count -eq 1 -and $children[0].PSIsContainer) {
        Copy-Item "$($children[0].FullName)\*" $DestDir -Recurse -Force
    } else {
        Copy-Item "$tmpExtract\*" $DestDir -Recurse -Force
    }

    Remove-Item $zipPath   -Force -ErrorAction SilentlyContinue
    Remove-Item $tmpExtract -Recurse -Force -ErrorAction SilentlyContinue

    Write-Host "  [ OK ] $Name -> $DestDir" -ForegroundColor Green
}

Write-Host ""
Write-Host "LSP Server Downloader" -ForegroundColor White
Write-Host "Output directory: $toolsLsp"
Write-Host ""

# ── OmniSharp v1.39.12 (C# / VB.NET) ─────────────────────────────────────────
Download-Server `
    -Name    'OmniSharp' `
    -Url     'https://github.com/OmniSharp/omnisharp-roslyn/releases/download/v1.39.12/omnisharp-win-x64-net6.0.zip' `
    -DestDir (Join-Path $toolsLsp 'OmniSharp')

# ── clangd v18.1.3 (C / C++ / Objective-C) ───────────────────────────────────
Download-Server `
    -Name    'clangd' `
    -Url     'https://github.com/clangd/clangd/releases/download/18.1.3/clangd-windows-18.1.3.zip' `
    -DestDir (Join-Path $toolsLsp 'clangd')

Write-Host ""
Write-Host "Done. Rebuild the solution to copy servers to output." -ForegroundColor Green
Write-Host ""
