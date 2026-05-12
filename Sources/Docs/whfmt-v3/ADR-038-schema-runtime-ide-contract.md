# ADR-038 — whfmt Schema–Runtime–IDE–CodeEditor Contract
**Decision ID**: ADR-038
**Date**: 2026-05-11
**Status**: ACCEPTED
**Phase**: P0 — Discovery & Contract

---

## Context

The whfmt ecosystem has grown organically to 789 format definition files containing rich
declarative data (variables, blocks, assertions, functions, forensic, syntaxDefinition, etc.).
A comprehensive audit (P0.1–P0.6) revealed significant gaps between what the catalog declares
and what the runtime/IDE actually consumes.

The goal: achieve 100% runtime parity — every declared field consumed by at least one
component (runtime, IDE UI, CodeEditor, or whfmt.* tool). No fields deleted. Open budget.

---

## Decision

### D1 — Schema canonical is camelCase (v3)

All whfmt fields are normalized to camelCase in v3. PascalCase fields (`QualityMetrics`,
`MimeTypes`, `Software`, `UseCases`, `TechnicalDetails`, `Strength`, `EntropyHint`,
`MinimumScore`) are accepted via `WhfmtVersionMigrator` (in-memory upgrade) but deprecated.

**Rationale**: System.Text.Json is case-sensitive. Runtime already reads PascalCase keys with
PascalCase TryGetProperty calls — this is intentional but inconsistent. v3 normalizes once
via bulk backfill (P11) after all consumers are updated.

### D2 — No field deletion ever

Every field currently in any whfmt file has either an existing consumer or a planned consumer
(P1-P11). Removal is forbidden. If a field is superseded (e.g. old single-signature
`detection.signature` → `detection.signatures[]`), both forms remain supported with a
migration note, and the runtime normalizes the old form at load time.

### D3 — Two-tier JSON access strategy

**Tier 1 (EmbeddedFormatCatalog.LoadHeader)**: Light header-only pass. Reads ~14 fields
for catalog indexing. Fast, runs at startup for all 789 files. No change to what it reads
without a performance justification.

**Tier 2 (GetJson + downstream parsers)**: Full JSON returned to IDE components. Each
pane/consumer parses the fields it needs via FormatMetadataExtensions or custom parsers.
New v3 fields are added to Tier 2 parsers without touching Tier 1.

### D4 — Models/ subfolder in WpfHexEditor.Core.Definitions (not a new project)

All new domain model types for the rich whfmt sub-schemas (VariableDefinition, BlockDefinition,
FunctionDefinition, AssertionRule, etc.) live in
`WpfHexEditor.Core.Definitions/Models/`. This avoids a new NuGet package while keeping
models close to the catalog.

### D5 — Expression language is a defined subset

The whfmt expression language (used in `assertions[].expression`, `blocks[].expression`,
`blocks[].condition`, `forensic.suspiciousPatterns[].condition`) is a formally-defined subset:
- Arithmetic: `+`, `-`, `*`, `/`, `%`
- Comparison: `==`, `!=`, `<`, `>`, `<=`, `>=`
- Logical: `&&`, `||`, `!`
- Bitwise: `&`, `|`, `^`, `~`, `<<`, `>>`
- Ternary: `condition ? trueExpr : falseExpr`
- Variable access: bare name or `varName[index]`
- Function calls: `functionName(arg1, arg2)`
- String: `.startsWith()`, `.includes()`, `.length`

An interpreter (`WhfmtExpressionEvaluator`) is implemented in P4 using a simple recursive
descent parser. No external dependencies.

### D6 — Function registry pattern

`functions{}` in v3 transitions from doc-strings to executable descriptors. Built-in functions
(readUInt8, readUInt16BE, extractASCIIString, parseZIPArchive, etc.) are implemented in C#
as `IWhfmtFunction` implementations registered in `WhfmtFunctionRegistry`. The
`functions{}` object in the whfmt file maps names to their descriptors — the runtime looks
up the C# implementation by name.

### D7 — Variables typed-array schema is canonical in v3

The dict-schema (`"variables": {"magic": "", "size": 0}`) is accepted for backward
compatibility but the canonical v3 form is the typed array with explicit name/type/offset/length.
`WhfmtVersionMigrator` upgrades dict-schema variables to typed-array form using type inference
from `blocks[]` storeAs references.

### D8 — CodeEditor standalone parity via new syntaxDefinition fields

Three new sub-fields are added to `syntaxDefinition` for standalone mode (P8):
- `completions[]` — keyword/symbol completion items
- `outlineRules[]` — pattern-based source outline
- `diagnosticRules[]` — pattern-based live diagnostics

These are implemented in CodeEditor's non-Roslyn path and gated by the existing
`enableInlineHints` and `enableCtrlClickNavigation` flags.

### D9 — Skill whfmt-guard becomes wrapper of whfmt.Validate NuGet (P10)

The PowerShell skill validates by calling the `whfmt.Validate` CLI (from the NuGet tool).
The skill adds pre-commit integration, IDE output panel display, and suppression marker handling.
In P10, the ~7 rule implementations in the PS script are replaced by the ~44 rules in
`whfmt.Validate.ValidationEngine`.

### D10 — Auto-commit at logical milestones authorized

User has authorized autonomous commits at logical milestones for all phases P0–P11.
Each commit uses format: `feat(whfmt-pN): <description>` or `refactor(whfmt-pN): ...`.

---

## Alternatives considered

| Alternative | Rejected because |
|---|---|
| New WpfHexEditor.Core.Definitions.Models project | Overkill — Models/ subfolder achieves same isolation |
| External LSP for syntax intelligence | Out of scope per user direction |
| Delete rarely-used fields | User constraint: 0 deletions |
| Move to JSON Schema-validated files at runtime | Performance cost; JSONC not supported by JSON Schema |

---

## Impact

- ~789 .whfmt files: no changes in P0; bulk camelCase normalization in P11
- `EmbeddedFormatCatalog`: light additions only (formatId to LoadHeader in P1)
- `FormatMetadataExtensions`: extended in P4/P5 for new fields
- `StructureEditor`: extended in P4 for validationRules, bitfields, functions
- `CodeEditor`: extended in P8 for completions/outline/diagnostics
- `whfmt.*` tools: extended progressively P3-P10
- `whfmt-guard` skill: R8/R9 added in P0; becomes wrapper in P10

---

## Phase roadmap summary

| Phase | Goal | Key deliverables |
|---|---|---|
| P0 | Discovery & contract | This ADR + P0.1-P0.7 audit docs |
| P1 | Schema stabilization | formatId in catalog, detection schema unification |
| P2 | Variables engine | WhfmtVariableStore, typed-array schema, dict→typed migration |
| P3 | Detection v2 | matchMode, MinimumScore, EntropyHint, validation.minFileSize |
| P4 | Expression + Functions | WhfmtExpressionEvaluator, WhfmtFunctionRegistry, assertions evaluation |
| P5 | IDE doc pane | Software, UseCases, references, formatRelationships in UI |
| P6 | Repair + Checksums | WhfmtRepairEngine, IDE repair action panel |
| P7 | Diff + Fuzz | IDE diff view integration, fuzz panel |
| P8 | CodeEditor standalone | completions[], outlineRules[], diagnosticRules[], Ctrl+Click |
| P9 | whfmt.Validate v2 | ~44 rules, IWhfmtDocumentProvider abstraction |
| P10 | Skill → NuGet wrapper | whfmt-guard PS becomes thin wrapper of whfmt.Validate |
| P11 | camelCase normalization | Bulk backfill all 789 files, WhfmtVersionMigrator in runtime |
