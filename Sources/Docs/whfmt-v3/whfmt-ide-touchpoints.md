# P0.5 — IDE Touchpoints Cartography
Generated: 2026-05-11

---

## IDE panes and their whfmt consumption

### 1. File Open / Editor Selection (MainWindow)
**Consumer**: `MainWindow.xaml.cs`
**whfmt data used**:
- `preferredEditor` → which editor opens the file
- `extensions[]` → extension-to-entry lookup
- `syntaxDefinition` presence → whether to register language
- `diffMode` → which diff viewer
- `IsTextFormat` → text vs binary routing

### 2. Structure Pane (StructureEditor)
**Consumer**: `StructureEditor/ViewModels/BlockViewModel.cs`, `V2ViewModels.cs`
**whfmt data used**:
- Full `blocks[]` array — all types, all fields
- `color`, `opacity` → visual highlighting in hex view
- `valueType`, `storeAs`, `valueMap` → field display
- `expression` → computed field evaluation
- `variables` → runtime variable store

**Gaps**:
- `validationRules` per field not enforced
- `bitfields[]` not rendered
- `assertions[]` not evaluated in real-time
- `functions` not invoked

### 3. Format Inspector Pane
**Consumer**: `FormatMetadataExtensions.ParseInspectorGroups()`
**whfmt data used**:
- `inspector.groups[].title / .icon / .fields[]`

**Gaps**:
- `inspector.badge` not used
- `inspector.primaryField` not used
- `inspector.showQualityScore` not used
- `inspector.groups[].name` not read (only `.title`)

### 4. Forensic Pane
**Consumer**: `FormatMetadataExtensions.ParseForensic()`
**whfmt data used**:
- `forensic.category`, `forensic.riskLevel`
- `forensic.suspiciousPatterns[].name/description/condition`
- `forensic.knownMaliciousPatterns[].name/description`

**Gaps**:
- `forensic.suspiciousPatterns[].condition` stored but not evaluated
- `forensic.notes` (A_OUT style string) not read

### 5. AI Hints Pane
**Consumer**: `FormatMetadataExtensions.ParseAiHints()`
**whfmt data used**:
- `aiHints.analysisContext`
- `aiHints.suggestedInspections[]`
- `aiHints.knownVulnerabilities[]`

### 6. Navigation / Bookmarks Pane
**Consumer**: `FormatMetadataExtensions.ParseBookmarks()`
**whfmt data used**:
- `navigation.bookmarks[].name/offset/offsetVar/icon`

**Gaps**:
- `navigation.entryPoint` (named section) not used
- `navigation.structure[]` (section order) not used
- `navigation.notes` not displayed

### 7. Assertions / Validation Pane
**Consumer**: `FormatMetadataExtensions.ParseAssertions()`
**whfmt data used**:
- `assertions[].name/expression/severity/message` — read and stored

**Gaps**:
- Expressions are stored as strings but **NOT evaluated** — no expression engine exists
- `assertions[].id` not read
- `assertions[].description` (A_OUT parallel field) not read

### 8. Export Templates Panel
**Consumer**: `FormatMetadataExtensions.ParseExportTemplates()`
**whfmt data used**:
- `exportTemplates[].name/format/fields[]`

### 9. Technical Details Panel
**Consumer**: `FormatMetadataExtensions.ParseTechnicalDetails()`
**whfmt data used**:
- `TechnicalDetails.endianness/compressionMethod/Platform/encryption/supportsEncryption/bitDepth/colorSpace/sampleRate/container/dataStructure`

### 10. Code Editor (syntax highlighting, folding, formatting)
**Consumer**: LanguageDefinitionSerializer → CodeEditor pipeline
**whfmt data used**: Full `syntaxDefinition` block (see P0.3)

### 11. Format Catalog / Browser Panel
**Consumer**: `CatalogQuery` on EmbeddedFormatEntry
**whfmt data used**:
- `formatName`, `category`, `description`, `extensions[]`, `MimeTypes`, `QualityScore`, `DiffMode`, `Platform`, `HasSyntaxDefinition`, `PreferredEditor`

**Gaps**:
- `Software`, `UseCases`, `references`, `formatRelationships` never shown in browser

### 12. Format Matcher (auto-detect)
**Consumer**: `EmbeddedFormatCatalog.DetectFromBytes()`, `FormatMatcher.cs`
**whfmt data used**:
- `detection.signatures[].value/offset/weight`

**Gaps**:
- `detection.strength` never used in scoring
- `detection.matchMode` never used (always uses best-score)
- `detection.MinimumScore` never used
- `detection.EntropyHint` never used
- `detection.validation.minFileSize` never checked
- Old-schema `detection.signature/offset/weight` (not `signatures[]`) not read

---

## Cross-cutting gaps requiring new IDE infrastructure

| Gap | Priority | Phase |
|---|---|---|
| Expression evaluator (assertions, computeFromVariables, conditions) | HIGH | P4 |
| Function registry (functions object) | HIGH | P4 |
| Variables engine (dict + typed-array schemas) | HIGH | P4 |
| Detection v2: matchMode, MinimumScore, EntropyHint, minFileSize | MED | P3 |
| Old detection schema normalization (single → signatures[]) | MED | P3 |
| `inspector.badge/primaryField/showQualityScore` | LOW | P5 |
| `navigation.entryPoint/structure[]/notes` | LOW | P5 |
| `references`, `software`, `UseCases`, `formatRelationships` in doc pane | LOW | P5 |
| `forensic.suspiciousPatterns[].condition` evaluation | MED | P4 |
| `repair[]` UI in IDE | MED | P6 |
| `migration[]` UI in IDE | LOW | P8 |
| `diff` integration in IDE diff view | MED | P7 |
| `fuzz` UI panel | LOW | P7 |
| `checksums` | LOW | P6 |
| camelCase normalization for PascalCase fields | MED | P11 |
| `formatId` read into catalog entry | LOW | P1 |
