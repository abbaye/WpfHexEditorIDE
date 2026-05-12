# P0.2 — whfmt Consumers Binary Map
Generated: 2026-05-11
Source: Grep of all TryGetProperty / JSON access patterns in Sources/**/*.cs

---

## Consumer layers identified

### Layer 1: EmbeddedFormatCatalog (Core.Definitions)
**File**: `Sources/Core/WpfHexEditor.Core.Definitions/EmbeddedFormatCatalog.cs`
**Purpose**: Singleton catalog. Reads lightweight header from every .whfmt at startup.

**Fields read in `LoadHeader()`** (System.Text.Json, case-sensitive TryGetProperty):
| JSON field | Access key | Type read |
|---|---|---|
| `formatName` | `"formatName"` | string |
| `description` | `"description"` | string |
| `category` | `"category"` | string |
| `version` | `"version"` | string |
| `author` | `"author"` | string |
| `QualityMetrics.CompletenessScore` | `"QualityMetrics"` + `"CompletenessScore"` | int |
| `extensions[]` | `"extensions"` | string[] |
| `TechnicalDetails.Platform` | `"TechnicalDetails"` + `"Platform"` | string |
| `preferredEditor` | `"preferredEditor"` | string |
| `detection.isTextFormat` | `"detection"` + `"isTextFormat"` | bool |
| `syntaxDefinition` | `"syntaxDefinition"` | presence only |
| `diffMode` | `"diffMode"` | string |
| `MimeTypes[]` | `"MimeTypes"` | string[] — **PascalCase** |
| `detection.signatures[].value` | `"signatures"` + `"value"` | string |
| `detection.signatures[].offset` | `"offset"` | int |
| `detection.signatures[].weight` | `"weight"` | double |

**Additional methods**:
- `GetSyntaxDefinitionJson()`: reads `"syntaxDefinition"` TryGetProperty, returns raw JSON text
- `GetJson()`: returns full raw JSON — consumers downstream get everything via this

**Fields NEVER read by LoadHeader** (pure documentary, gap):
`blocks`, `variables`, `assertions`, `navigation`, `inspector`, `functions`, `forensic`,
`aiHints`, `diff`, `fuzz`, `repair`, `migration`, `TechnicalDetails.*` (except Platform),
`detection.strength`, `detection.Strength`, `detection.EntropyHint`, `detection.matchMode`,
`detection.MinimumScore`, `UseCases`, `Software`, `software`, `references`, `formatRelationships`,
`exportTemplates`, `QualityMetrics.*` (except CompletenessScore)

---

### Layer 2: FormatMetadataExtensions (Core.Definitions.Metadata)
**File**: `Sources/Core/WpfHexEditor.Core.Definitions/Metadata/FormatMetadataExtensions.cs`
**Purpose**: Rich metadata extraction from full .whfmt JSON. Used by IDE panes.

**Fields read**:
| JSON field | Access key |
|---|---|
| `forensic.category` | `"forensic"` + `"category"` |
| `forensic.riskLevel` | `"riskLevel"` |
| `forensic.suspiciousPatterns[].name/description/condition` | `"suspiciousPatterns"` |
| `forensic.knownMaliciousPatterns[].name/description` | `"knownMaliciousPatterns"` |
| `aiHints.analysisContext` | `"aiHints"` + `"analysisContext"` |
| `aiHints.suggestedInspections[]` | `"suggestedInspections"` |
| `aiHints.knownVulnerabilities[]` | `"knownVulnerabilities"` |
| `navigation.bookmarks[].name/offset/offsetVar/icon` | `"navigation"` + `"bookmarks"` |
| `assertions[].name/expression/severity/message` | `"assertions"` |
| `inspector.groups[].title/icon/fields[]` | `"inspector"` + `"groups"` |
| `exportTemplates[].name/format/fields[]` | `"exportTemplates"` |
| `TechnicalDetails.endianness` | `"TechnicalDetails"` + `"endianness"` |
| `TechnicalDetails.compressionMethod` | `"compressionMethod"` |
| `TechnicalDetails.Platform` | `"Platform"` |
| `TechnicalDetails.encryption` | `"encryption"` |
| `TechnicalDetails.supportsEncryption` | `"supportsEncryption"` |
| `TechnicalDetails.bitDepth` | `"bitDepth"` |
| `TechnicalDetails.colorSpace` | `"colorSpace"` |
| `TechnicalDetails.sampleRate` | `"sampleRate"` |
| `TechnicalDetails.container` | `"container"` |
| `TechnicalDetails.dataStructure` | `"dataStructure"` |

**Note**: `inspector.groups[].title` is read but catalog uses both `title` and `name` — inconsistency in whfmt files vs parser.

---

### Layer 3: CatalogQuery (Core.Definitions.Query)
**File**: `Sources/Core/WpfHexEditor.Core.Definitions/Query/CatalogQuery.cs`
**Purpose**: Fluent query on pre-loaded EmbeddedFormatEntry — does NOT re-read JSON.

**Fields accessed** (all from EmbeddedFormatEntry model):
`Category`, `QualityScore`, `Signatures`, `Extensions`, `IsTextFormat`, `HasSyntaxDefinition`,
`PreferredEditor`, `MimeTypes`, `Platform`, `DiffMode`, `Name`, `Description`

---

### Layer 4: whfmt.Validate (Tools)
**File**: `Sources/Tools/whfmt.Validate/Engine/ValidationEngine.cs`
**Purpose**: CLI/SDK validation tool. Reads: `blocks`, `variables`, `assertions`, `navigation`,
`detection`, `functions`, `diff`, `fuzz`, `repair`.

### Layer 5: whfmt.Fuzz (Tools)
**File**: `Sources/Tools/whfmt.Fuzz/FormatFuzzer.cs`
**Purpose**: Reads `fuzz` section — strategies, field names, mutation types.

### Layer 6: whfmt.Analysis (Tools)
**File**: `Sources/Tools/whfmt.Analysis/FormatDiff.cs`
**Purpose**: Reads `diff.keyFields`, `diff.ignoreFields`, `diff.groupBy`.

### Layer 7: whfmt.Backfill (Tools)
**File**: `Sources/Tools/whfmt.Backfill/Parsing/WhfmtParser.cs`
**Purpose**: Reads `blocks`, `variables`, `assertions` for backfill/canonicalization.

### Layer 8: whfmt.CodeGen (Tools)
**File**: `Sources/Tools/whfmt.CodeGen/Generator/ParserGenerator.cs`
**Purpose**: Reads `blocks`, `variables`, `functions` to generate parser code.

### Layer 9: FormatDefinitionValidator (Core)
**File**: `Sources/Core/WpfHexEditor.Core/Tools/FormatDefinitionValidator.cs`
**Purpose**: Legacy validator. Uses PropertyNameCaseInsensitive=true — reads all fields.

### Layer 10: StructureEditor (Editor)
**File**: `Sources/Editors/WpfHexEditor.Editor.StructureEditor/ViewModels/BlockViewModel.cs`
**Purpose**: Block-level VM for the structure view pane.
**Fields used**: `blocks[]` full schema — type, name, offset, length, color, opacity,
description, hidden, endianness, storeAs, valueType, expression, condition,
trueLabel, falseLabel, count, maxIterations, action, actionVariable, actionValue,
until, maxLength, entrySize, indexVar, structRef, targetVar, label, variable

---

## Gap analysis — fields never read by any consumer

| Field | Status |
|---|---|
| `functions` | Only in whfmt.CodeGen tool, not in runtime or IDE |
| `repair[]` | Only in whfmt.Validate CLI, not in IDE |
| `migration[]` | Not consumed anywhere in IDE or runtime |
| `fuzz` | Only in whfmt.Fuzz tool |
| `diff` | Only in whfmt.Analysis tool |
| `detection.Strength` / `detection.strength` | Never read |
| `detection.EntropyHint` | Never read |
| `detection.matchMode` | Never read — catalog only uses `signatures[]` |
| `detection.MinimumScore` | Never read |
| `detection.validation.*` | Never read by runtime |
| `QualityMetrics.*` (except CompletenessScore) | Never read |
| `Software` / `software` | Never read |
| `UseCases` | Never read |
| `references` | Never read |
| `formatRelationships` | Never read |
| `forensic.notes` | Str() fallback exists but not surfaced in UI |
| `inspector.badge` | Read by FormatMetadataExtensions? No — NOT parsed |
| `inspector.primaryField` | NOT parsed |
| `inspector.showQualityScore` | NOT parsed |
| `navigation.entryPoint` | NOT parsed (only `bookmarks[]` parsed) |
| `navigation.structure[]` | NOT parsed |
| `navigation.notes` | NOT parsed |
| `blocks[].validationRules` | NOT consumed by runtime |
| `blocks[].bitfields[]` | NOT consumed by runtime |
| `blocks[].fields[]` (named containers) | Partially in StructureEditor |
| `assertions[].id` | NOT parsed (only name/expression/severity/message) |
| `assertions[].description` | NOT parsed (A_OUT style) |
| `syntaxDefinition.ideMetadata.*` | Not mapped in LanguageDefinitionSerializer |
| `syntaxDefinition.breakpointRules` | Check needed |
| `syntaxDefinition.previewSnippet` | Check needed |
| `syntaxDefinition.previewSamples` | Check needed |
| `syntaxDefinition.colorLiteralPatterns[]` | Check needed |
| `checksums` | Not consumed anywhere |
