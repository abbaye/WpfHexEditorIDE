# whfmt v3 — Plugin & Host Usage Guide

This guide shows how a plugin or host application consumes the whfmt v3 runtime
delivered by phases P0–P11 (commits a2851db6…f9193639) and activated in the IDE
by Piste A (commit 04a35bd0). Every API shown here is public and stable;
ABI changes are forbidden by ADR-038 D2.

Test fixtures referenced below live in
`Sources/Tests/WpfHexEditor.Tests/Unit/Whfmt*_Tests.cs` — read those for
executable examples of every snippet here.

---

## 1. Catalog lookup

Get the singleton, filter, project.

```csharp
using WpfHexEditor.Core.Definitions;
using WpfHexEditor.Core.Definitions.Query;

var catalog = EmbeddedFormatCatalog.Instance;

// Direct lookups
var gbc = catalog.GetByExtension(".gbc");
var zip = catalog.GetByMimeType("application/zip");

// Fluent query (CatalogQuery.cs)
var hiQualityMedical = catalog.Query()
    .InCategory(FormatCategory.Medical)
    .WithMinQuality(80)
    .HasMagicBytes()
    .OrderByQuality()
    .Execute();

// P1: stable formatId survives Name renames
var entry = catalog.Query().WithFormatId("ROM_GBC").First();
```

See: `WhfmtDocumentation_Tests.cs`, `WhfmtDetectionV2_Tests.cs`.

---

## 2. Magic-byte detection

`FormatMatcher.ScoreEntry` (P3 + Piste A) honors detection v3 fields
(`matchMode`, `minimumScore`, `minFileSize`, `entropyHint`).

```csharp
using WpfHexEditor.Core.Definitions.Matching;

ReadOnlySpan<byte> header = file.AsSpan(0, 512);

// Best match across catalog
var result = FormatMatcher.Match(catalog, ".bin", header);
if (result is { Confidence: > 0.8 })
    Console.WriteLine($"{result.Entry.Name} (score={result.RawScore:F2})");

// Top-N for ambiguous files
foreach (var r in FormatMatcher.GetTopMatches(catalog, header, maxResults: 3))
    Console.WriteLine($"  {r.Entry.FormatId}  conf={r.Confidence:F2}");
```

Malformed hex signatures in the catalog are skipped silently (P3 bonus guard).

---

## 3. Documentary metadata (P5–P7)

Extension methods on `EmbeddedFormatEntry` surface what the .whfmt declares.
Available without any binding to the legacy `FormatDefinition` model.

```csharp
using WpfHexEditor.Core.Definitions.Metadata;

var entry = catalog.Query().WithFormatId("ROM_GBC").First()!;

// P5 — IDE documentation pane
IReadOnlyList<SoftwareReference>  software  = entry.GetSoftware(catalog);
IReadOnlyList<string>             useCases  = entry.GetUseCases(catalog);
IReadOnlyList<DocReference>       refs      = entry.GetReferences(catalog);
IReadOnlyList<FormatRelationship> related   = entry.GetFormatRelationships(catalog);
InspectorHeader?                  inspector = entry.GetInspectorHeader(catalog);
NavigationOverview?               nav       = entry.GetNavigationOverview(catalog);
string?                           notes     = entry.GetForensicNotes(catalog);

// P6 — repair + checksums
IReadOnlyList<RepairAction> repairs   = entry.GetRepairs(catalog);
IReadOnlyList<ChecksumSpec> checksums = entry.GetChecksums(catalog);

// P7 — diff + fuzz configs
DiffConfig? diff  = entry.GetDiffConfig(catalog);
FuzzConfig? fuzz  = entry.GetFuzzConfig(catalog);
```

Returns empty lists / nulls when the section is absent. Both whfmt v2 schemas
(string-array vs object-array, dict vs array) are normalized transparently.

---

## 4. Variables engine (P2)

```csharp
using WpfHexEditor.Core.Definitions.Models;

// Parse + build a typed store from the entry
WhfmtVariableStore store = entry.BuildVariableStore(catalog);

// Inspect declarations
foreach (var def in store.Definitions)
    Console.WriteLine($"{def.Name,-20} type={def.Type,-8} offset={def.Offset}");

// After your binary parser populates values:
store.Set("cgbFlag",       0x80);
store.Set("cartridgeType", 0x13);

// Typed reads
if (store.TryGet<int>("cgbFlag", out var flag))
    Console.WriteLine(flag == 0x80 ? "GBC compat" : "GBC only");
```

Dict-schema `.whfmt` files are auto-migrated to typed form by type inference
from the literal `initialValue`.

---

## 5. Expression engine (P4)

```csharp
using WpfHexEditor.Core.Definitions.Models.Expressions;
using WpfHexEditor.Core.Definitions.Models.Functions;

// Build a ready-to-use evaluator (store + default function registry)
WhfmtExpressionEvaluator eval = entry.BuildEvaluator(catalog);

// Evaluate real .whfmt expressions
bool isGbc      = eval.EvaluateBool("cgbFlag == 128 || cgbFlag == 192");
long compressed = eval.EvaluateInt("(1 - compressedSize / uncompressedSize) * 100");
double ratio    = eval.EvaluateDouble("min(5, score)");

// Built-in functions: min, max, abs, length, hex, toUpper, toLower
// String methods: .startsWith() .endsWith() .includes() .contains() .trim()
// .length on string / byte[] / collections.

// Register a custom function
public sealed class Crc32Fn : IWhfmtFunction
{
    public string Name => "crc32";
    public object? Invoke(IReadOnlyList<object?> args)
        => /* compute CRC32 of args[0] */ 0L;
}
eval.Functions.Register(new Crc32Fn());
bool ok = eval.EvaluateBool("crc32(headerBytes) == storedChecksum");
```

Source strings are parsed once and the AST is cached for the evaluator's
lifetime — runtime cost of subsequent evaluations is an AST walk.

Errors throw `WhfmtExpressionException` carrying `Source` + `Position` +
`Identifier` (ready for IDE underline rendering).

---

## 6. Standalone CodeEditor features (P8)

For languages declared purely in `.whfmt` (no Roslyn workspace), the
syntaxDefinition can carry three new sub-arrays consumed at load time:

```json
{
  "syntaxDefinition": {
    "completions": [
      { "label": "if", "kind": "keyword", "detail": "if statement" }
    ],
    "outlineRules": [
      { "type": "class", "pattern": "^\\s*class\\s+(\\w+)", "group": 1 }
    ],
    "diagnosticRules": [
      { "id": "WH0001", "pattern": "TODO\\b", "severity": "info", "message": "TODO" }
    ]
  }
}
```

These are deserialized by `LanguageDefinitionSerializer.ParseSyntaxDefinitionBlock`
into `LanguageDefinition.Completions / OutlineRules / DiagnosticRules`.
Malformed regexes are skipped silently. See `WhfmtStandaloneFeatures_Tests.cs`.

---

## 7. Static validation (P9)

Build-time / pre-commit check that every expression in a document is parseable
and references only declared variables/functions.

```csharp
using WpfHexEditor.Core.Definitions.Models.Validation;

string json = File.ReadAllText("Custom.whfmt");
var issues = WhfmtExpressionValidator.Validate(json);
foreach (var i in issues)
    Console.WriteLine($"  {i.Severity} {i.RuleId} at {i.Path}: {i.Message}");
```

Rule IDs:
- `R10-000` — document is not valid JSONC
- `R10-001` — expression parse error
- `R10-002` — identifier not declared in `variables{}` or `functions{}`
- `R10-003` — function call to an unknown identifier

The same checks run as a `WARN` rule in the `whfmt-guard` PowerShell skill
(pre-commit gate — lightweight identifier scan, full AST is the C# validator).

---

## 8. v2 → v3 migration (P11)

The migrator normalizes legacy PascalCase root keys (`QualityMetrics`,
`MimeTypes`, `Software`, `UseCases`, `TechnicalDetails`, `detection.Strength`,
`detection.EntropyHint`, `detection.MinimumScore`) to camelCase **in memory**.
The original `.whfmt` file is never modified.

```csharp
using WpfHexEditor.Core.Definitions.Models;

// Direct migration
string v3 = WhfmtVersionMigrator.Migrate(File.ReadAllText("Legacy.whfmt"));

// Dry-run (returns the rename report without producing JSON)
foreach (var op in WhfmtVersionMigrator.DryRun(rawJson))
    Console.WriteLine(op);   // e.g. "root: MimeTypes → mimeTypes"

// Catalog opt-in (P5 Piste A): GetJsonV3 returns the migrated JSON on demand
string migrated = catalog.GetJsonV3(entry.ResourceKey);
```

Policy when both casings co-exist: camelCase wins, PascalCase is silently
dropped. Reported by `DryRun` as `"… dropped (camelCase '…' already present)"`.

---

## 9. Activation in the IDE (Piste A)

`FormatMatcher.ScoreEntry` is the single signature scorer used by both
`FormatMatcher.Match`, `FormatMatcher.GetTopMatches`, and
`EmbeddedFormatCatalog.DetectFromBytes`. All three honor v3 detection fields.

The Parsed Fields panel's "Format Metadata" group now shows:
- (existing) Category, MIME Types, Extensions, Software, Use Cases,
  Documentation Level, Quality, Signature, Related, Technical, Specs, References
- (Piste A) Structure (ordered navigation sections), Nav Notes,
  Badge Field (inspector primary), Forensic notes

`WhfmtFormatDetailVm` (the shell's Format Browser detail card) exposes the
same documentary fields via `SoftwareDisplay`, `UseCasesDisplay`,
`RelationshipsDisplay`, `NavigationStructure`, `NavigationNotes`, plus
`HasDocumentation` for tab visibility.

---

## 10. whfmt-guard skill (internal)

Pre-commit gate that runs on every `.whfmt` edit. Defined in
`Sources/.claude/skills/whfmt-guard/`. Rules:

| Rule | Severity | Detects |
|---|---|---|
| R1 jsonc-parse        | ERR  | JSONC parse error |
| R2 version-monotone   | ERR  | `version` regression vs HEAD |
| R3 schema-required    | ERR  | Missing formatName/formatId/extensions/category/description |
| R4 id-uniqueness      | ERR  | `formatId` collision in catalog |
| R5 magic-collision    | WARN | Same magic+offset+ext as another file |
| R6 strength-enum      | WARN | `detection.strength` not in {None,Weak,Medium,Strong,VeryStrong} |
| R7 placeholder-drift  | WARN | `{{var}}` in description not declared in `variables{}` |
| R10 expression-refs   | WARN | Expression identifier not in `variables{}` / `functions{}` / builtins |

Suppress a single finding by appending `// whfmt-ignore: <reason>` JSONC line
comment above the offending field.

---

## See also

- `ADR-038-schema-runtime-IDE-CodeEditor-contract.md` — architecture decision
- `whfmt-properties-catalog.md` — full property inventory across the 789 catalog files
- `whfmt-consumption-matrix.md` — which fields are consumed by which layer
- `whfmt-schema-canonical-v3.json` — JSON Schema draft for v3
