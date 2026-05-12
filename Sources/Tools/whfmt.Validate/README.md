# whfmt.Validate

> **Binary file validator** — validate any file against 799 format definitions in seconds.

`whfmt.Validate` is a **dotnet global tool** that detects file formats and validates them for structural integrity, checksum correctness, and forensic anomalies — all powered by the [whfmt.FileFormatCatalog](https://www.nuget.org/packages/whfmt.FileFormatCatalog) library with 799 embedded `.whfmt` definitions.


> **Full documentation**: [whfmt-Validate-guide.md](https://github.com/abbaye/WpfHexEditorIDE/blob/master/Sources/Tools/whfmt.Validate/whfmt-Validate-guide.md) — API reference, architecture, integration guides, and usage examples.

---

## Install

```bash
dotnet tool install -g whfmt.Validate
```

After installation, the `whfmt` command is available globally.

---

## Commands

### `whfmt validate` — Validate files

```bash
whfmt validate <file(s)> [options]
```

**Arguments**

| Argument | Description |
|----------|-------------|
| `files`  | One or more files or directories to validate |

**Options**

| Option | Short | Description |
|--------|-------|-------------|
| `--format <name>` | `-f` | Force a specific format (name or extension). Skips auto-detection. |
| `--report <format>` | `-r` | Output format: `text` (default), `json`, `html` |
| `--output <path>` | `-o` | Write report to file instead of stdout |
| `--recursive` | `-R` | Recursively validate all files in a directory |
| `--fail-fast` | | Stop on first validation error |
| `--quiet` | `-q` | No output — only set exit code (0 = valid, 1 = invalid, 2 = error) |

**Examples**

```bash
# Validate a ZIP file (auto-detect format)
whfmt validate archive.zip

# Validate a firmware blob, force PE32 format
whfmt validate firmware.bin --format pe32

# Validate all files in a directory, output HTML report
whfmt validate ./files --recursive --report html --output report.html

# CI/CD: silent validation, fail on first error
whfmt validate build/output.exe --quiet --fail-fast && echo "OK" || echo "INVALID"

# JSON output for scripting
whfmt validate image.png --report json | jq '.isValid'
```

**Exit codes**

| Code | Meaning |
|------|---------|
| `0` | File is valid — no errors |
| `1` | Validation failed — one or more errors |
| `2` | File not found or tool error |

---

### `whfmt list` — Browse the format catalog

```bash
whfmt list [options]
```

| Option | Short | Description |
|--------|-------|-------------|
| `--category <name>` | `-c` | Filter by category (e.g. `Archives`, `Images`, `Game`, `Executables`) |
| `--search <text>` | `-s` | Filter by name substring |
| `--json` | `-j` | Output as JSON array |

**Examples**

```bash
# List all supported formats grouped by category
whfmt list

# List all game formats
whfmt list --category Game

# Search for anything related to "zip"
whfmt list --search zip

# JSON output for scripting
whfmt list --category Executables --json
```

---

### `whfmt info` — Inspect a specific format

```bash
whfmt info <format> [options]
```

| Option | Short | Description |
|--------|-------|-------------|
| `--json` | `-j` | Output full metadata as JSON |

**Examples**

```bash
# Show ZIP format metadata
whfmt info zip

# Show PE32 format in JSON (includes AI hints, forensic risk, technical details)
whfmt info pe32 --json

# Look up by extension
whfmt info .docx
```

---

## What gets validated

For each file, `whfmt validate` runs up to 4 layers of checks depending on what the format definition declares:

| Layer | Description |
|-------|-------------|
| **Format Detection** | Auto-identifies the format using magic bytes, extension, and MIME type with a confidence score |
| **Signature Verification** | Checks that expected magic bytes are present at the declared offsets |
| **Checksum Validation** | Verifies CRC32, CRC16, Adler32, MD5, SHA-1, SHA-256, or byte-sum checksums against stored or expected values |
| **Assertion Evaluation** | Evaluates structural constraints (`file_size > 0`, `version == 3`, etc.) |
| **Forensic Scan** | Detects suspicious or known-malicious byte patterns declared in the format's forensic metadata |

---

## Report formats

### Text (default)

```
  whfmt validate — archive.zip
  ────────────────────────────────────────────────────────────
  File     : C:\files\archive.zip
  Size     : 1.23 MB
  Format   : ZIP Archive (Archives)
  Confidence: 100%  [Combined]
  Forensic : LOW risk

  Passed checks:
    ✓  [Signature] MagicBytes — Signature 504B0304 @ offset 0 ✓
    ✓  [Checksum] CentralDirCRC — CRC32: A1B2C3D4 ✓

  Result   : ✓ VALID
```

### JSON

```json
{
  "file": "C:\\files\\archive.zip",
  "size": 1289421,
  "format": "ZIP Archive",
  "category": "Archives",
  "confidence": 1.0,
  "matchSource": "Combined",
  "forensicRisk": "low",
  "isValid": true,
  "errors": 0,
  "warnings": 0,
  "checks": [
    { "Category": "Signature", "Name": "MagicBytes", "Passed": true, "Detail": "Signature 504B0304 @ offset 0 ✓" }
  ],
  "issues": []
}
```

### HTML

A self-contained dark-themed HTML report — suitable for CI artifacts or sharing with teams.

---

## Supported format categories

The tool validates files across **29 categories** and **799 formats**:

| Category | Count | Examples |
|----------|-------|---------|
| Archives | 37 | ZIP, 7z, RAR, TAR, GZIP, BZIP2, XZ, LZ4 |
| Audio | 49 | MP3, FLAC, WAV, AAC, OGG, OPUS, AIFF |
| Images | 58 | PNG, JPEG, WebP, GIF, BMP, TIFF, HEIC, ICO |
| Executables | 14 | PE32, ELF, Mach-O, WASM, .NET, JAR |
| Documents | 44 | PDF, DOCX, XLSX, PPTX, ODT, RTF, EPUB |
| Game | 91 | NES ROM, SNES, GBA, PS1, Unity Bundle, PAK |
| Database | 29 | SQLite, LevelDB, RocksDB, DuckDB, MDB |
| Disk | 18 | ISO, IMG, VHD, VMDK, E01, DD |
| Crypto | 16 | PEM, DER, PFX, GPG, SSH Key, JWT |
| Firmware | 12 | Intel HEX, SREC, BIN, UF2, EFI |
| Medical | 20 | DICOM, NIfTI, MINC, Analyze, PAR/REC |
| Network | 23 | PCAP, PCAPNG, Wireshark, NetFlow, HAR |
| ... | ... | [799 total — `whfmt list` for full catalog] |

---

## CI/CD integration

```yaml
# GitHub Actions example
- name: Validate build artifacts
  run: |
    dotnet tool install -g whfmt.Validate
    whfmt validate ./artifacts --recursive --report json --output validation.json --quiet
    if [ $? -ne 0 ]; then echo "Artifact validation failed"; exit 1; fi

- name: Upload validation report
  uses: actions/upload-artifact@v4
  with:
    name: format-validation
    path: validation.json
```

```yaml
# Azure DevOps example
- script: |
    dotnet tool install -g whfmt.Validate
    whfmt validate $(Build.ArtifactStagingDirectory) -R --report html --output $(Build.ArtifactStagingDirectory)/report.html
  displayName: Validate build artifacts
```

---

## Powered by whfmt.FileFormatCatalog

`whfmt.Validate` depends on [whfmt.FileFormatCatalog](https://www.nuget.org/packages/whfmt.FileFormatCatalog) — automatically installed by NuGet. The catalog ships 799 `.whfmt` format definitions as embedded resources, covering magic-byte signatures, checksum rules, structural assertions, and forensic metadata for every supported format.

---

## License

AGPL-3.0 — see [LICENSE](https://github.com/abbaye/WpfHexEditorIDE/blob/master/LICENSE).

© 2016–2026 Derek Tremblay
