# ==========================================================
# Project: whfmt.Validate
# File: smoke-test.ps1
# Description: B8 smoke test — invokes the tool on a generated PNG fixture and
#              asserts exit-code 0 (= validation runs end-to-end without error).
#              Run before publishing whfmt.Validate to NuGet.
# Usage: pwsh ./smoke-test.ps1
# ==========================================================

$ErrorActionPreference = 'Stop'
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projDir   = $scriptDir

# Generate a minimal valid PNG (89 50 4E 47 0D 0A 1A 0A + IHDR + IEND)
$fixture = Join-Path $env:TEMP "whfmt-smoke-fixture.png"
$bytes = [byte[]](
    0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A,           # PNG magic
    0x00,0x00,0x00,0x0D,                                 # IHDR length
    0x49,0x48,0x44,0x52,                                 # "IHDR"
    0x00,0x00,0x00,0x01, 0x00,0x00,0x00,0x01,            # 1x1
    0x08,0x06,0x00,0x00,0x00,                            # bit depth + color
    0x1F,0x15,0xC4,0x89,                                 # CRC (not validated by whfmt)
    0x00,0x00,0x00,0x00, 0x49,0x45,0x4E,0x44,            # IEND chunk
    0xAE,0x42,0x60,0x82                                  # IEND CRC
)
[IO.File]::WriteAllBytes($fixture, $bytes)

Write-Host "Smoke test: validating $fixture ..."
$proc = Start-Process -FilePath dotnet `
    -ArgumentList @('run', '--project', $projDir, '--no-launch-profile', '--', 'validate', $fixture) `
    -NoNewWindow -PassThru -Wait

if ($proc.ExitCode -ne 0) {
    Write-Error "B8 smoke test FAILED: exit-code $($proc.ExitCode)"
    exit 1
}

Write-Host "B8 smoke test PASSED."
Remove-Item $fixture -ErrorAction SilentlyContinue
exit 0
