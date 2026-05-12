# P0.4 — whfmt Consumption Matrix
Generated: 2026-05-11
Diff of P0.1 (all fields in catalog) vs P0.2 (binary format consumers) vs P0.3 (syntax consumers)

Legend: ✅ Consumed | ⚠️ Partial | ❌ Gap | 🔧 Tools only | 📋 Planned (P1-P11)

---

## Root-level fields

| Field | In Catalog | EmbeddedFormatCatalog | FormatMetadataExtensions | CatalogQuery | whfmt.Tools | Status | Phase |
|---|---|---|---|---|---|---|---|
| `formatName` | ✅ | ✅ | - | ✅ (Name) | - | ✅ | - |
| `formatId` | ✅ | ❌ not read | - | - | ✅ | ⚠️ | P1 |
| `version` | ✅ | ✅ | - | - | - | ⚠️ | - |
| `extensions` | ✅ | ✅ | - | ✅ | - | ✅ | - |
| `description` | ✅ | ✅ | - | ✅ | - | ✅ | - |
| `category` | ✅ | ✅ | - | ✅ | - | ✅ | - |
| `author` | ✅ | ✅ | - | - | - | ✅ | - |
| `preferredEditor` | ✅ | ✅ | - | ✅ | - | ✅ | - |
| `diffMode` (root) | ✅ | ✅ | - | ✅ | - | ✅ | - |
| `references` | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | P5 doc pane |
| `software` (camelCase) | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | P5 doc pane |
| `Software` (PascalCase) | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | P5 doc pane |
| `UseCases` | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | P5 doc pane |
| `MimeTypes` | ✅ | ✅ (PascalCase) | ❌ | ✅ | ❌ | ⚠️ | P3 normalize |
| `QualityMetrics.CompletenessScore` | ✅ | ✅ | ❌ | ✅ | ❌ | ✅ | - |
| `QualityMetrics.*` (other) | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | P5 |
| `TechnicalDetails.Platform` | ✅ | ✅ | ✅ | ✅ | ❌ | ✅ | - |
| `TechnicalDetails.*` (other) | ✅ | ❌ | ✅ (10 fields) | ❌ | ❌ | ⚠️ | - |
| `formatRelationships` | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | P5 |

---

## `detection` fields

| Field | Catalog | Runtime | Status | Phase |
|---|---|---|---|---|
| `detection.signature` (old single) | ✅ | ❌ not read — only `signatures[]` | ❌ | P3 |
| `detection.signatures[]` (new multi) | ✅ | ✅ | ✅ | - |
| `detection.offset` (old single) | ✅ | ❌ | ❌ | P3 |
| `detection.weight` (old single) | ✅ | ❌ | ❌ | P3 |
| `detection.strength` / `Strength` | ✅ | ❌ never read | ❌ | P3 |
| `detection.EntropyHint` | ✅ | ❌ never read | ❌ | P6 |
| `detection.matchMode` | ✅ | ❌ never read | ❌ | P3 |
| `detection.MinimumScore` | ✅ | ❌ never read | ❌ | P3 |
| `detection.required` | ✅ | ❌ never read | ❌ | P3 |
| `detection.isTextFormat` | ✅ | ✅ | ✅ | - |
| `detection.validation.minFileSize` | ✅ | ❌ | ❌ | P3 |
| `detection.validation.maxSignatureOffset` | ✅ | ❌ | ❌ | P3 |
| `detection.validation.note` | ✅ | ❌ | ❌ | doc only |
| `detection.validation` (string form) | ✅ | ❌ | ❌ | P3 |

---

## `variables` fields

| Field | Catalog | Runtime | Status | Phase |
|---|---|---|---|---|
| `variables` (dict schema) | ✅ | ❌ not read by catalog | ❌ | P4 |
| `variables` (typed array schema) | ✅ | ❌ not read by catalog | ❌ | P4 |

---

## `blocks[]` fields

| Field | Catalog | StructureEditor | Status | Phase |
|---|---|---|---|---|
| `blocks[].type` | ✅ | ✅ | ✅ | - |
| `blocks[].name` | ✅ | ✅ | ✅ | - |
| `blocks[].offset` | ✅ | ✅ | ✅ | - |
| `blocks[].length` | ✅ | ✅ | ✅ | - |
| `blocks[].color` | ✅ | ✅ | ✅ | - |
| `blocks[].opacity` | ✅ | ✅ | ✅ | - |
| `blocks[].description` | ✅ | ✅ | ✅ | - |
| `blocks[].storeAs` | ✅ | ✅ | ✅ | - |
| `blocks[].valueType` | ✅ | ✅ | ✅ | - |
| `blocks[].valueMap` | ✅ | ✅ | ✅ | - |
| `blocks[].expression` | ✅ | ✅ | ✅ | - |
| `blocks[].hidden` | ✅ | ✅ | ✅ | - |
| `blocks[].endianness` | ✅ | ✅ | ✅ | - |
| `blocks[].validationRules` | ✅ | ❌ not in VM | ❌ | P4 |
| `blocks[].bitfields[]` | ✅ | ❌ not in VM | ❌ | P4 |
| `blocks[].fields[]` (containers) | ✅ | ⚠️ partial | ⚠️ | P4 |
| `blocks[].variable` (metadata) | ✅ | ✅ | ✅ | - |
| `blocks[].count` | ✅ | ✅ | ✅ | - |
| `blocks[].maxIterations` | ✅ | ✅ | ✅ | - |
| `blocks[].action` | ✅ | ✅ | ✅ | - |
| `blocks[].until` | ✅ | ✅ | ✅ | - |
| `blocks[].condition` | ✅ | ✅ | ✅ | - |
| `blocks[].structRef` | ✅ | ✅ | ✅ | - |
| `blocks[].targetVar` | ✅ | ✅ | ✅ | - |

---

## `assertions[]` fields

| Field | Catalog | FormatMetadataExtensions | StructureEditor | Status | Phase |
|---|---|---|---|---|---|
| `assertions[].name` | ✅ | ✅ | ❌ | ⚠️ | P4 |
| `assertions[].expression` | ✅ | ✅ (stored) | ❌ not evaluated | ❌ | P4 |
| `assertions[].severity` | ✅ | ✅ | ❌ | ⚠️ | P4 |
| `assertions[].message` | ✅ | ✅ | ❌ | ⚠️ | P4 |
| `assertions[].id` | ✅ | ❌ | ❌ | ❌ | P4 |
| `assertions[].description` (A_OUT) | ✅ | ❌ | ❌ | ❌ | P4 |

---

## `inspector` fields

| Field | FormatMetadataExtensions | Status | Phase |
|---|---|---|---|
| `inspector.groups[].title` | ✅ | ✅ | - |
| `inspector.groups[].icon` | ✅ | ✅ | - |
| `inspector.groups[].fields[]` | ✅ | ✅ | - |
| `inspector.groups[].name` | ❌ (only .title read) | ⚠️ | P3 normalize |
| `inspector.badge` | ❌ | ❌ | P5 |
| `inspector.primaryField` | ❌ | ❌ | P5 |
| `inspector.showQualityScore` | ❌ | ❌ | P5 |

---

## `navigation` fields

| Field | FormatMetadataExtensions | Status | Phase |
|---|---|---|---|
| `navigation.bookmarks[].name` | ✅ | ✅ | - |
| `navigation.bookmarks[].offset` | ✅ | ✅ | - |
| `navigation.bookmarks[].icon` | ✅ | ✅ | - |
| `navigation.bookmarks[].offsetVar` | ✅ | ✅ | - |
| `navigation.entryPoint` | ❌ | ❌ | P5 |
| `navigation.structure[]` | ❌ | ❌ | P5 |
| `navigation.notes` | ❌ | ❌ | P5 |

---

## `forensic` fields

| Field | FormatMetadataExtensions | Status | Phase |
|---|---|---|---|
| `forensic.category` | ✅ | ✅ | - |
| `forensic.riskLevel` | ✅ | ✅ | - |
| `forensic.suspiciousPatterns[].name` | ✅ | ✅ | - |
| `forensic.suspiciousPatterns[].description` | ✅ | ✅ | - |
| `forensic.suspiciousPatterns[].condition` | ✅ | ✅ (stored, not evaluated) | ⚠️ P4 eval |
| `forensic.knownMaliciousPatterns[]` | ✅ | ✅ | ✅ | - |
| `forensic.notes` (A_OUT style) | ✅ | ❌ | ❌ | P5 |

---

## `aiHints` fields

| Field | FormatMetadataExtensions | Status | Phase |
|---|---|---|---|
| `aiHints.analysisContext` | ✅ | ✅ | - |
| `aiHints.suggestedInspections[]` | ✅ | ✅ | - |
| `aiHints.knownVulnerabilities[]` | ✅ | ✅ | - |

---

## `functions`, `diff`, `fuzz`, `repair`, `migration` — gap summary

| Section | IDE consumer | Status | Phase |
|---|---|---|---|
| `functions` | None (tools only) | ❌ | P4 function registry |
| `diff` | whfmt.Analysis tool only | 🔧 | P7 IDE diff view |
| `fuzz` | whfmt.Fuzz tool only | 🔧 | P7 IDE fuzz panel |
| `repair[]` | whfmt.Validate tool only | 🔧 | P6 IDE repair action |
| `migration[]` | Not consumed anywhere | ❌ | P8 migration assistant |
| `checksums` | Not consumed anywhere | ❌ | P6 |

---

## syntaxDefinition — gap summary

| Field group | Consumed | Status | Phase |
|---|---|---|---|
| `rules[].type + .pattern` | ✅ LanguageDefinitionSerializer | ✅ | - |
| `rules[].colorKey` | ❌ in DTO only, not projected | ❌ | P3 theme integration |
| `snippets[]` | ✅ | ✅ | - |
| `foldingRules.*` | ✅ | ✅ | - |
| `breakpointRules.*` | ✅ | ✅ | - |
| `bracketPairs[]` | ✅ | ✅ | - |
| `columnRulers[]` | ✅ | ✅ | - |
| `formattingRules.*` | ⚠️ subset via supportedRules | ⚠️ | P3 complete |
| `colorLiteralPatterns[]` | ✅ | ✅ | - |
| `previewSnippet` / `previewSamples` | ✅ | ✅ | - |
| `ideMetadata.*` | ✅ | ✅ | - |
| `enableInlineHints` | flag stored but no data engine | ❌ | P8 standalone |
| `enableCtrlClickNavigation` | flag stored but no resolver | ❌ | P8 standalone |
| `diagnosticPrefix` | ✅ | ✅ (Roslyn only) | ⚠️ P8 standalone |
| `keywords[]` / `completions[]` | ❌ | ❌ | P8 standalone |
