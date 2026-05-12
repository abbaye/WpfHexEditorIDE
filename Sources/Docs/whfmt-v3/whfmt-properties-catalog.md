# P0.1 — whfmt Property Catalog Audit
Generated: 2026-05-11
Files in catalog: ~789 (confirmed via Glob)
Files sampled for deep inspection: 22+ across 11 categories

---

## Root-level property inventory

### Always present (mandatory, camelCase)
| Property | Type | Notes |
|---|---|---|
| `formatName` | string | Human-readable name |
| `formatId` | string | Canonical ID (e.g. "CSharp") |
| `version` | string | SemVer |
| `extensions` | string[] | File extensions |
| `description` | string | Long description |
| `category` | string | Folder-level category |
| `author` | string | Always "WPFHexaEditor Team" |
| `detection` | object | Detection rules |

### Very common (>70%), camelCase
| Property | Presence est. | Type |
|---|---|---|
| `blocks` | ~85% | array |
| `variables` | ~80% | object (two schemas: dict OR array of typed objects) |
| `forensic` | ~75% | object |
| `aiHints` | ~75% | object |
| `preferredEditor` | ~90% | string |
| `references` | ~70% | string[] or object |
| `diffMode` | ~60% | string: "binary" \| "text" \| "semantic" |

### Common (30-70%), mixed casing
| Property | Casing | Presence est. |
|---|---|---|
| `QualityMetrics` | **PascalCase** | ~80% |
| `MimeTypes` | **PascalCase** | ~70% |
| `Software` | **PascalCase** | ~65% (also `software` camelCase in some files) |
| `UseCases` | **PascalCase** | ~65% |
| `TechnicalDetails` | **PascalCase** | ~65% |
| `assertions` | camelCase | ~50% |
| `inspector` | camelCase | ~50% |
| `navigation` | camelCase | ~45% |
| `exportTemplates` | camelCase | ~45% |
| `formatRelationships` | camelCase (mostly) | ~55% |
| `software` | camelCase | ~20% (duplicate key issue with `Software`) |

### Occasional (5-30%)
| Property | Presence est. | Notes |
|---|---|---|
| `syntaxDefinition` | ~7% | Only in Programming/SourceCode and text formats |
| `functions` | ~25% | Object with named string values |
| `diff` | ~15% | Object with `keyFields`, `ignoreFields`, `note`, `groupBy` |
| `fuzz` | ~20% | Object with `strategies[]`, `preserveChecksums`, `maxMutationsPerFile` |
| `repair` | ~5% | Array of repair actions |
| `migration` | ~3% | Array of migration paths |
| `checksums` | ~2% | Object with checksum definitions |

---

## `detection` object schema (field inventory)

### Old schema (single-signature, ~60% of files)
```json
{
  "signature": "hex-string",
  "offset": 0,
  "weight": 0.8,
  "strength": "Medium",    ← camelCase
  "Strength": "Medium",    ← PascalCase in ~40% of these
  "required": true,
  "validation": {
    "minFileSize": 5,
    "maxSignatureOffset": 0,
    "note": "discriminator note"
  },
  "isTextFormat": true
}
```

### New schema (multi-signature, ~40% of files — A_OUT style)
```json
{
  "signatures": [
    { "value": "hex", "offset": 0, "label": "...", "weight": 0.7 }
  ],
  "matchMode": "any" | "best" | "all",
  "MinimumScore": 82,
  "Strength": "Medium",     ← ALWAYS PascalCase in new schema
  "EntropyHint": {          ← ALWAYS PascalCase
    "min": 4.0,
    "max": 7.5,
    "note": "..."
  },
  "validation": "string"    ← sometimes a plain string (not object)!
}
```

**Note**: `detection.strength` / `detection.Strength` — both casings exist. Valid values: "Strong", "Medium", "Weak".
**Note**: `detection.EntropyHint` is PascalCase (never consumed by runtime).

---

## `variables` object — two competing schemas

### Schema A (object/dict, simple — ~70% of files using variables)
```json
"variables": {
  "currentOffset": 0,
  "magic": "",
  "headerSize": 0
}
```

### Schema B (typed array — ~30% of files using variables, newer format)
```json
"variables": [
  { "name": "aoutMagic", "type": "uint16", "offset": 0, "length": 2, "endian": "big", "description": "..." }
]
```

Schema B has richer semantics: `name`, `type`, `offset`, `length`, `endian`, `description`.

---

## `blocks[]` type values

| Block type | Description |
|---|---|
| `signature` | Magic bytes / identifier region |
| `field` | Named data field with offset/length/valueType |
| `metadata` | Displays a stored variable |
| `header` | Named container (A_OUT style) |
| `data` | Data region |
| `conditional` | Branch based on expression |
| `computeFromVariables` | Computed field from expression |
| `loop` | Repeating block |
| `repeating` | Alternative repeating syntax |
| `action` | Mutation action (increment var, etc.) |
| `sentinel` | Read until condition |
| `union` | Multiple overlapping interpretations |
| `nested` | Reference to named struct |
| `pointer` | Dereference a variable as offset |
| `bitfield` | Sub-byte field inside a parent |

## `blocks[].valueType` values

| valueType | Notes |
|---|---|
| `uint8`, `uint16`, `uint32`, `uint64` | Unsigned integers |
| `int8`, `int16`, `int32`, `int64` | Signed integers |
| `float32`, `float64` | Floating point |
| `ascii` | ASCII string |
| `utf8` | UTF-8 string |
| `utf16le`, `utf16be` | UTF-16 strings |
| `bytes` | Raw byte array |
| `hex` | Hex representation |

## `blocks[]` extra fields (beyond type/name/offset/length)

| Field | Used in types |
|---|---|
| `color` | field, signature, header |
| `opacity` | field, signature, header |
| `storeAs` | field, signature |
| `valueType` | field, signature |
| `valueMap` | field (enum display) |
| `validationRules` | field (`allowedValues`, `min`, `max`) |
| `endianness` | field |
| `bitfields` | field (sub-fields array) |
| `hidden` | field |
| `endianness` | field |
| `expression` | computeFromVariables, conditional |
| `trueLabel` / `falseLabel` | conditional |
| `count` | loop |
| `maxIterations` | loop |
| `action` / `actionVariable` / `actionValue` | action |
| `until` / `maxLength` / `untilInclusive` | sentinel |
| `entrySize` / `indexVar` | repeating |
| `condition` | union |
| `structRef` | nested |
| `targetVar` / `label` | pointer |
| `fields[]` | header/named container |
| `variable` | metadata |

---

## `assertions[]` fields

| Field | Notes |
|---|---|
| `name` | Required |
| `expression` | Required, JS-like expression string |
| `severity` | "error" \| "warning" \| "info" |
| `message` | Optional human message |
| `id` | Optional (A_OUT style: "A1", "A2") |
| `description` | Optional (A_OUT style — parallel to `message`) |

---

## `inspector` object fields

| Field | Notes |
|---|---|
| `badge` | String — variable name to show as badge |
| `primaryField` | Primary variable for highlighting |
| `showQualityScore` | bool |
| `groups[]` | Array of groups |
| `groups[].name` | OR `groups[].title` (both casings exist!) |
| `groups[].icon` | Optional icon hint |
| `groups[].fields[]` | Array of field/variable names |

---

## `navigation` object fields

| Field | Notes |
|---|---|
| `bookmarks[]` | Array of bookmarks |
| `bookmarks[].name` | Display name |
| `bookmarks[].offset` | Byte offset |
| `bookmarks[].icon` | Icon hint |
| `entryPoint` | Named section (A_OUT style) |
| `structure[]` | Ordered section names |
| `notes` | String description |

---

## `forensic` object fields

| Field | Notes |
|---|---|
| `category` | Category string (e.g. "medical", "game", "other") |
| `riskLevel` | "low" \| "medium" \| "high" \| "critical" \| "HIGH" (case inconsistent) |
| `suspiciousPatterns[]` | Array of patterns — can be string[] OR object[] |
| `suspiciousPatterns[].name` | (object form) |
| `suspiciousPatterns[].description` | (object form) |
| `suspiciousPatterns[].condition` | (object form) |
| `knownMaliciousPatterns[]` | Array of `{name, description}` |
| `notes` | String (A_OUT style) |

---

## `syntaxDefinition` sub-object fields (Programming formats only)

| Field | Notes |
|---|---|
| `id` | Language ID (e.g. "csharp") |
| `name` | Display name |
| `extensions[]` | File extensions |
| `diagnosticPrefix` | "CS", "VB", etc. |
| `lineCommentPrefix` | "//" |
| `blockCommentStart` / `blockCommentEnd` | "/*" / "*/" |
| `enableInlineHints` | bool |
| `enableCtrlClickNavigation` | bool |
| `rules[]` | Syntax tokenization rules |
| `rules[].type` | Token type string |
| `rules[].colorKey` | Theme color key |
| `rules[].pattern` | Regex pattern |
| `foldingRules` | Folding configuration object |
| `foldingRules.startPatterns[]` | Regex patterns |
| `foldingRules.endPatterns[]` | Regex patterns |
| `foldingRules.namedRegionStart` / `namedRegionEnd` | Regex |
| `foldingRules.endOfBlockHint` | Object |
| `breakpointRules` | Breakpoint configuration |
| `breakpointRules.nonExecutablePatterns[]` | Regex patterns |
| `breakpointRules.statementContinuationPatterns[]` | Regex |
| `bracketPairs[]` | `{open, close}` pairs |
| `columnRulers[]` | int[] column positions |
| `formattingRules` | Formatting configuration object |
| `formattingRules.indentSize` | int |
| `formattingRules.useTabs` | bool |
| `formattingRules.trimTrailingWhitespace` | bool |
| `formattingRules.insertFinalNewline` | bool |
| `formattingRules.lineEnding` | "lf" \| "crlf" \| "cr" |
| `formattingRules.braceStyle` | "allman" \| "k&r" |
| `formattingRules.organizeImports` | bool |
| `formattingRules.separateSystemImports` | bool |
| `formattingRules.maxLineLength` | int |
| `formattingRules.supportedRules[]` | string[] of implemented rule names |
| `previewSnippet` | string — sample code for preview |
| `previewSamples` | Object with before/after pairs per rule |
| `colorLiteralPatterns[]` | Regex patterns for color detection |
| `ideMetadata` | Object with IDE-specific flags |
| `ideMetadata.isSourceFile` | bool |
| `ideMetadata.isStructuredDataFile` | bool |
| `ideMetadata.supportsClassDiagram` | bool |
| `ideMetadata.supportsSourceOutline` | bool |
| `ideMetadata.isProjectLanguage` | bool |
| `ideMetadata.languageColor` | Hex color string |
| `ideMetadata.aliases[]` | string[] |
| `ideMetadata.iconGlyph` | string |
| `ideMetadata.diffMode` | string |

### syntaxDefinition rule types (from LanguageDefinitionSerializer type map)
BlockComment, String, Comment, Preprocessor, RegionName, RegionKeyword,
ControlFlow, Keyword, Type, UserType, Attribute, Number, Field,
NamespaceDecl, UsingRef, Tag, AttrName, AttrValue, TagBracket, Entity,
DocType, ProcInstr, CData, Variable, Symbol, Key, Cmdlet, BatchLabel,
Boolean, Macro, Annotation, Decorator, Register, Directive, Label,
Condition, Section, Value, Anchor, Operator, Bracket, Identifier

---

## `diff` object fields
| Field | Notes |
|---|---|
| `keyFields[]` | string[] — fields that identify format semantics |
| `ignoreFields[]` | string[] — fields to skip in diff |
| `groupBy` | Optional grouping variable name |
| `note` | Documentation string |

## `fuzz` object fields
| Field | Notes |
|---|---|
| `preserveChecksums` | bool |
| `maxMutationsPerFile` | int |
| `strategies[]` | Array of mutation strategies |
| `strategies[].field` | Target field name |
| `strategies[].mutation` | "corrupt_signature" \| "enum_sweep" \| "boundary_values" \| "bit_flip" \| "overflow" \| "random_bytes" |
| `strategies[].weight` | int — relative probability |
| `strategies[].rate` | float — mutation rate |
| `strategies[].description` | Documentation |
| `note` | Documentation string |

## `repair[]` array fields
| Field | Notes |
|---|---|
| `name` | Repair action name |
| `trigger` | Condition description |
| `action` | "recompute_checksum" \| "fix_header" \| etc. |
| `target` | Variable name to fix |
| `algorithm` | "crc32" \| "md5" \| etc. |
| `description` | Documentation |

## `migration[]` array fields
| Field | Notes |
|---|---|
| `from` / `to` | Description of migration path |
| `description` | Documentation |
| `changes[]` | Array of field changes |
| `changes[].field` | Target field |
| `changes[].changeType` | "set_value" \| etc. |
| `changes[].value` | New value |
| `changes[].description` | Documentation |

---

## PascalCase vs camelCase analysis

**PascalCase root keys** (currently in catalog):
- `QualityMetrics` (with sub-keys: `CompletenessScore`, `DocumentationLevel`, `BlocksDefined`, `ValidationRules`, `LastUpdated`, `PriorityFormat`, `AutoRefined`, `ReviewedBy`)
- `MimeTypes`
- `Software` (conflicts with camelCase `software` in some files like ROM_GBC)
- `UseCases`
- `TechnicalDetails` (with sub-keys mixed: `Endianness`, `Platform`, `endianness`)

**PascalCase inside `detection`**:
- `Strength` (vs `strength` in old schema)
- `EntropyHint` (and its sub-keys `min`, `max`, `note`)
- `MinimumScore`

**Impact**: `System.Text.Json` is case-sensitive by default. Runtime uses `TryGetProperty` with exact strings, so PascalCase fields are intentionally read by PascalCase lookups and camelCase fields by camelCase lookups — NOT a bug, just inconsistency to normalize in v3.

---

## `functions` object pattern

Functions are a root-level object with named string values describing what each function does:
```json
"functions": {
  "readUInt8": "Read 8-bit header fields",
  "readUInt16BE": "Read big-endian 16-bit",
  "extractASCIIString": "Parse text headers"
}
```
Currently these are **documentation only** — not executed by runtime. Target for P4 runtime function registry.

---

## Categories found in catalog
3D, Archives, Audio, CAD, Certificates, Crypto, Data, Database, Disk, Documents,
Executables, Game, Images, Medical, Programming, Security, System, Video, Other

---

## `references` field — two schemas (inconsistency)

### Schema A (string array — most files)
```json
"references": ["Spec Name", "https://..."]
```

### Schema B (object with named arrays — ROM_GBC style)
```json
"references": {
  "specifications": ["..."],
  "WebLinks": ["https://..."]
}
```

---

## `formatRelationships` field — two schemas

### Schema A (object with named keys — most files)
```json
"formatRelationships": { "vendor": "...", "related": ["NII"] }
```

### Schema B (array of objects — A_OUT style)
```json
"formatRelationships": [
  { "format": "ELF", "relationship": "ELF replaced a.out" }
]
```
