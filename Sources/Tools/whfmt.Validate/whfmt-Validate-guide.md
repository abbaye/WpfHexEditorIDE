# whfmt.Validate — Documentation (v1.0.0)

> **What you get** — A **dotnet global tool** (`whfmt`) that validates binary
> files against the 799 `.whfmt` definitions shipped with
> `whfmt.FileFormatCatalog` 1.3.2: format detection by magic bytes
> (negative-offset signatures supported), structural assertions evaluated by
> the runtime expression engine, checksum verification
> (CRC32 / MD5 / SHA1 / SHA256 / Adler32), forensic-pattern matching, and a
> dedicated `lint-expressions` subcommand for statically validating the
> expression-bearing fields inside `.whfmt` definitions themselves.
> Reports are emitted as plain UTF-8 text, JSON, or self-contained HTML.

## Table of Contents

1. [Installation](#installation)
2. [Architecture & dependencies](#architecture--dependencies)
3. [CLI reference](#cli-reference)
4. [Internal API (Engine)](#internal-api-engine)
5. [Usage examples](#usage-examples)
6. [Integration with other whfmt.* packages](#integration-with-other-whfmt-packages)
7. [Schema v3 fields consumed](#schema-v3-fields-consumed)
8. [Exit codes & CI integration](#exit-codes--ci-integration)
9. [Troubleshooting](#troubleshooting)
10. [License](#license)

---

## Installation

```bash
dotnet tool install --global whfmt.Validate --version 1.0.0
# command is named `whfmt` (not `whfmt-validate`)
whfmt --help
```

Update / uninstall:

```bash
dotnet tool update --global whfmt.Validate
dotnet tool uninstall --global whfmt.Validate
```

The tool requires **.NET 8** runtime. Console output is UTF-8 (forced via
`Console.OutputEncoding`) so emoji status glyphs render correctly on Windows.

---

## Architecture & dependencies

```
whfmt.Validate.nupkg     (PackAsTool=true, ToolCommandName=whfmt)
└── tools/net8.0/any/
    └── whfmt-validate.dll
            └─ depends on → whfmt.FileFormatCatalog 1.3.2
            └─ depends on → System.CommandLine 2.0.0-beta4
```

### Pipeline

```
validate <files…>
    └── per file:
          └── FormatFileAnalyzer.Analyze(catalog, file, headerSize=512)
                └── FormatMatcher score → EmbeddedFormatEntry
          └── ValidationEngine:
                ├── Magic-byte verification        ─── blocks[].isSignature
                ├── Structural assertions          ─── assertions[]   (via WhfmtExpressionEvaluator)
                ├── Checksum execution             ─── checksums[]
                └── Forensic pattern matching      ─── forensic.suspiciousPatterns[]
    └── ReportRenderer → text | json | html → stdout / --output file

lint-expressions <whfmtFiles…>
    └── WhfmtExpressionValidator.Validate(content)
          → ruleId, severity, path, message, position

repair <files…>
    └── ParseRepairRules(json)           ─── repair[] block
    └── ApplyRepairs(data, actions)      ─── force-set, recompute-checksum, …
    └── (dry-run | overwrite | --output)
```

---

## CLI reference

The root command is `whfmt`. Subcommands: `validate`, `list`, `info`,
`repair`, `lint-expressions`.

### `whfmt validate`

```
whfmt validate <files…> [options]
```

| Argument / option | Short | Default | Description |
|---|---|---|---|
| `<files…>` | — | required | One or more files or directories. Arity = `OneOrMore`. |
| `--format` | `-f` | autodetect | Force a specific format name or extension. |
| `--report` | `-r` | `text` | Report format: `text`, `json`, `html`. |
| `--output` | `-o` | stdout | Write report(s) to this file. Combined JSON/HTML is emitted for multi-file batches. |
| `--recursive` | `-R` | `false` | Walk directories recursively. |
| `--fail-fast` | — | `false` | Exit on the first invalid file. |
| `--quiet` | `-q` | `false` | Suppress output; only return an exit code. |

Per-file checks emitted in the report:

- **Magic bytes** — each `blocks[].isSignature == true` block is compared against `value`.
- **Assertions** — each `assertions[].expression` is evaluated.
- **Checksums** — each `checksums[]` entry is recomputed and compared.
- **Forensic** — `forensic.suspiciousPatterns[]` raise warnings.

### `whfmt list`

```
whfmt list [options]
```

| Option | Short | Description |
|---|---|---|
| `--category` | `-c` | Filter by category (e.g. `Archives`, `Images`, `Game`). |
| `--search` | `-s` | Substring filter on name. |
| `--json` | `-j` | Emit a JSON array `[ { name, category, extensions, qualityScore } ]`. |

### `whfmt info`

```
whfmt info <format> [--json]
```

Looks up by name or extension. Prints name, category, extensions, quality
score, text flag, MIME types, signatures (with weights), diff mode,
preferred editor, forensic risk level, and AI hints. With `--json`, emits a
machine-readable object including the full `metadata.Forensic`,
`metadata.AiHints`, and `metadata.TechnicalDetails`.

### `whfmt repair`

```
whfmt repair <files…> [options]
```

| Option | Short | Default | Description |
|---|---|---|---|
| `<files…>` | — | required | One or more files (arity `OneOrMore`). |
| `--format` | `-f` | autodetect | Force format. |
| `--output` | `-o` | overwrite in place | Output file (single) or directory (multiple). |
| `--dry-run` | `-d` | `false` | Show what would change without writing. |
| `--verbose` | `-v` | `false` | Print every repair action applied. |

Repair actions are driven by the `.whfmt → repair[]` block (force-set fields
to canonical values, recompute checksums, fix magic bytes). Files without
`repair[]` rules are skipped with `[NO RULES]`.

### `whfmt lint-expressions`

```
whfmt lint-expressions <whfmtFiles…> [--json]
```

Statically validates expression-bearing fields inside `.whfmt` files:

- `assertions[].expression`
- `blocks[].expression` / `blocks[].condition`
- `forensic.suspiciousPatterns[].condition`

Powered by `WhfmtExpressionValidator` from
`WpfHexEditor.Core.Definitions.Models.Validation`. Reports parse errors,
undeclared identifiers, and unknown function calls.

| Option | Description |
|---|---|
| `--json` | Emit one JSON object per line per issue (consumable by the `whfmt-guard` PowerShell skill). Each object: `{ file, ruleId, severity, path, message, source, position }`. |

Exit code is `0` when no issues found, `1` otherwise.

---

## Internal API (Engine)

These types are `internal` to the tool — listed here for contributors.

| Type | Purpose |
|---|---|
| `ValidationEngine` | `Validate(string filePath, string? forcedFormat) → ValidationReport`. Reads the first 512 bytes for header detection, then the full file for checksum/assertion verification. |
| `ValidationReport` | Aggregates `Checks` (passed/failed steps) and `Issues` (error/warning/info). Exposes `ErrorCount`, `WarningCount`, `InfoCount`, `IsValid`, `FileNotFound`. |
| `ValidationCheck` | `(Category, Name, Passed, Detail)` — one row in the report. |
| `ValidationIssue` | `(Severity, Category, Name, Message)` — one entry, severities: `error`, `warning`, `info`. |
| `ChecksumAlgorithms` | Static dispatcher mapping algorithm name → `byte[] → byte[]` hash. |
| `ReportRenderer` | `RenderText / RenderJson / RenderHtml` on a single `ValidationReport`. |

---

## Usage examples

### 1. Quick validation

```bash
whfmt validate hello.png
```

### 2. Batch with HTML report

```bash
whfmt validate ./assets --recursive --report html --output report.html
```

### 3. CI gate (JSON + jq)

```bash
whfmt validate dist/*.bin --report json --output dist-report.json
jq '.[] | select(.isValid == false) | .fileName' dist-report.json
```

### 4. Forced format + fail-fast

```bash
whfmt validate suspicious.bin -f PNG --fail-fast
```

### 5. Inspect a format catalog entry

```bash
whfmt info PNG
whfmt info .png --json | jq '.MimeTypes, .Forensic.RiskLevel'
whfmt list --category Archives
whfmt list --search jpeg --json
```

### 6. Repair corrupted files (dry-run first)

```bash
whfmt repair broken.png --dry-run --verbose
# … inspect the proposed actions, then …
whfmt repair broken.png -o repaired/
```

### 7. Lint a `.whfmt` you are authoring

```bash
whfmt lint-expressions ./MyFormat.whfmt
# CI mode
whfmt lint-expressions ./Definitions/*.whfmt --json | jq
```

### 8. Quiet mode in a shell pipeline

```bash
if whfmt validate input.zip --quiet; then
    echo "VALID"
else
    case $? in
        1) echo "INVALID";;
        2) echo "ERROR (file missing or unknown format)";;
    esac
fi
```

---

## Integration with other whfmt.* packages

| Package | Synergy |
|---|---|
| `whfmt.FileFormatCatalog` | Provides the format definitions, the detection façade, the runtime expression engine and the lint validator. |
| `whfmt.Fuzz` | Pipe each `FuzzVariant.Data` through `whfmt validate -f <format>` to verify the mutant triggers the expected failure modes. |
| `whfmt.Analysis` | Run `whfmt validate` to weed out malformed files, then diff the surviving valid pairs with `FormatDiff.Compare`. |
| `whfmt.CodeGen` | `whfmt info <format>` shows the same `.whfmt` metadata that `whfmt-codegen generate` will consume. |

---

## Schema v3 fields consumed

```jsonc
{
  "name": "PNG",
  "category": "Images",

  "blocks": [
    { "type": "field", "name": "magic", "offset": 0, "length": 8,
      "isSignature": true, "value": "89 50 4E 47 0D 0A 1A 0A" }
  ],

  "assertions": [
    { "id":         "PNG.dimensions.positive",
      "expression": "imageWidth > 0 && imageHeight > 0",
      "severity":   "error",
      "message":    "Image dimensions must be positive." },
    { "id":         "PNG.colorType.valid",
      "expression": "colorType in [0, 2, 3, 4, 6]",
      "severity":   "error" }
  ],

  "checksums": [
    { "algorithm": "crc32",
      "storedAt":  { "fixedOffset": -4, "length": 4 },
      "dataRange": { "fixedOffset": 0,  "fixedLength": -4 } }
  ],

  "forensic": {
    "suspiciousPatterns": [
      { "condition": "fileSize < 100",
        "description": "Suspiciously small PNG",
        "severity":   "warning" }
    ]
  },

  "repair": [
    { "action": "set", "field": "magic", "value": "89 50 4E 47 0D 0A 1A 0A" },
    { "action": "recomputeChecksum", "algorithm": "crc32" }
  ]
}
```

| Field | Consumed by | Effect |
|---|---|---|
| `blocks[].isSignature` + `value` | `validate` | Magic-byte verification check. |
| `assertions[]` | `validate` | Each expression is evaluated via `WhfmtExpressionEvaluator`; failures become `ValidationIssue`s. |
| `checksums[]` | `validate`, `repair` | Per-algorithm recompute + compare. |
| `forensic.suspiciousPatterns[]` | `validate` | Adds `info` / `warning` issues. |
| `repair[]` | `repair` | `set`, `recomputeChecksum`, etc. |
| (all expression fields) | `lint-expressions` | Static parse + undeclared-var + unknown-function check. |

---

## Exit codes & CI integration

| Code | Meaning | Source |
|---|---|---|
| `0` | All inputs valid / no lint issues. | Default. |
| `1` | At least one input invalid (errors > 0) or lint reported ≥ 1 issue. | `ValidationReport.IsValid == false`. |
| `2` | File missing, unknown format, or no full definition (irrecoverable). | `report.FileNotFound` or format-not-found. |

`--fail-fast` short-circuits the batch as soon as code 1 is set. `--quiet`
suppresses all output (only the exit code matters).

### GitHub Actions snippet

```yaml
- uses: actions/setup-dotnet@v4
  with: { dotnet-version: '8.x' }
- run: dotnet tool install --global whfmt.Validate
- run: whfmt validate dist --recursive --report json --output report.json
- uses: actions/upload-artifact@v4
  with: { name: whfmt-report, path: report.json }
```

---

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `[UNKNOWN FORMAT]` | No signature matched. | Pass `--format <name>` or `-f .ext`. |
| `[NO DEFINITION]` | Format known by signature only — no full `.whfmt` available. | Try a different file; not all entries have full definitions. |
| `[NO RULES]` (repair) | Format `.whfmt` has no `repair[]` block. | Cannot repair — expected. |
| Assertion fails on a known-good file | The `.whfmt`'s assertion references an undeclared variable or the expression engine returned `false`. | Run `whfmt lint-expressions <file.whfmt>` to inspect; file an issue if the assertion is wrong. |
| Emoji glyphs (✓ / ✗ / ☠) display as `?` on Windows | Terminal is not UTF-8 (the tool forces `Console.OutputEncoding = UTF8`, but legacy hosts may override). | Run from Windows Terminal / pwsh 7+; or use `--report json`. |
| Negative-offset signatures (e.g. `-4` for FAT_BINARY / TRUECRYPT) failing | Pre-1.3.2 catalog bug. | Update to `whfmt.FileFormatCatalog ≥ 1.3.2` (this tool ships with 1.3.2). |
| `validate` very slow on large files | Whole file is read for checksum + assertion variable extraction. | Acceptable for current scope; use `--quiet` in CI to skip rendering. |
| `lint-expressions` reports `undeclared` for legit fields | The field is declared by `storeAs` not `name`. | Use `storeAs` consistently in expressions; update the `.whfmt`. |

---

## License

GNU AGPL v3.0. See `https://www.gnu.org/licenses/agpl-3.0.html`.

Copyright © 2016-2026 Derek Tremblay.
