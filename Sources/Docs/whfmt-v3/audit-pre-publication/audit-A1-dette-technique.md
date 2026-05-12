# Audit A1 — Dette technique cachée

**Date:** 2026-05-12
**Scope:** Validation pré-publication NuGet `whfmt.FileFormatCatalog` 1.3.0
**Méthode:** Lecture statique du code, analyse de couverture de tests, revue de surface publique

---

## A1.1 — Coverage tests vs surface publique

### ✅ Surfaces bien couvertes (12 fichiers de tests, 141 tests)

| Surface | Test file | Tests | Status |
|---|---|---|---|
| `EmbeddedFormatEntry` (nouveaux champs FormatId/MatchMode/MinimumScore/MinFileSize/EntropyMin/EntropyMax) | `WhfmtDetectionV2_Tests.cs` | 6 | ✅ |
| `IEmbeddedFormatCatalog.GetByName` + `GetByFormatId` | `WhfmtCatalogLookups_Tests.cs` | 6 | ✅ |
| `WhfmtVersionMigrator.Migrate/DryRun` | `WhfmtVersionMigrator_Tests.cs` | 8 | ✅ |
| `WhfmtVariableStore` + `WhfmtVariableParser` | `WhfmtVariables_Tests.cs` | 16 | ✅ |
| `WhfmtValueType` + `WhfmtValueTypes.Parse` | `WhfmtVariables_Tests.cs` | (5/16) | ✅ |
| Expression engine (lexer, parser, evaluator) | `WhfmtExpression_Tests.cs` | 44 | ✅ |
| `WhfmtExpressionValidator` | `WhfmtExpressionValidator_Tests.cs` | 10 | ✅ |
| Standalone CodeEditor (P8: completions/outline/diagnostics) | `WhfmtStandaloneFeatures_Tests.cs` | 9 | ✅ |
| `FormatDocumentationExtensions` (P5) | `WhfmtDocumentation_Tests.cs` | 11 | ✅ |
| `FormatRepairExtensions` (P6) | `WhfmtRepair_Tests.cs` | 6 | ✅ |
| `FormatDiffFuzzExtensions` (P7) + `FuzzMutation` enum | `WhfmtDiffFuzz_Tests.cs` | 6 | ✅ |
| `FormatAssertionEvaluator` (F7) | `WhfmtAssertionEvaluator_Tests.cs` | 7 | ✅ |
| `ViewModelBase.SetField` string overload (F2) | `WhfmtViewModelSetField_Tests.cs` | 4 | ✅ |

### ⚠️ Surfaces avec couverture indirecte uniquement

| Surface | Couverture indirecte via | Recommandation |
|---|---|---|
| `FormatMatcher.ScoreEntry` (P3 + Piste A) | `EmbeddedFormatCatalog.DetectFromBytes` tests | **⚠️ À tester directement** — c'est la méthode utilisée par `FormatMatcher.Match` et `GetTopMatches`. 1 bug subtil sur la logique `matched/MinimumScore` y vit. |
| `FormatSummaryBuilder.BuildOneLiner/BuildPlainText/BuildMarkdown/BuildDiagnosticDump` | Pas testé du tout | **⚠️ Aucun test** — méthodes publiques utilitaires. Risque faible (composition de strings) mais sortie peut casser silencieusement. |
| `FormatDocumentationExtensions.GetDocumentationBundle` (P5 + /simplify) | Pas testé directement, juste les 3 sous-méthodes | **⚠️ À tester** — c'est le path optimisé hot des plugins. Une régression sur la dispatch (header/nav/forensic) ne serait pas détectée. |
| `EmbeddedFormatCatalog.GetJsonV3` (Piste A) | Aucun test | **⚠️ Voir A1.4 — dead code aujourd'hui** |

**Coverage gap résumé:** 4 surfaces publiques sans test direct. Effort pour combler = ~30 min (4 fichiers × 2-3 tests chacun). **Recommandation: combler avant publication.**

---

## A1.2 — Edge cases grammaire expression P4

Lecture du code `WhfmtExpressionEvaluator.cs` lignes 90-253. Findings:

### 🟡 Comportements documentés mais surprenants

1. **Division par zéro** (`EvalBinary` line 114) : `ToDouble(l) / ToDouble(r)` → IEEE 754 → `Infinity` ou `NaN`, **pas d'exception**. Le caller voit un double bizarre. Pas un bug — c'est le contrat IEEE — mais à documenter dans USAGE.md.

2. **Modulo par zéro** (line 115) : idem, `NaN`. Aucune assertion catalog ne fait ça aujourd'hui.

3. **Shift count overflow** (lines 123-124) : `(int)ToInt64(r)` peut être négatif ou > 63. .NET shift sémantique = `count & 0x3F` pour Int64 → wrap-around silencieux. Surprise pour expressions générées.

4. **String + number = concat** (line 111) : `"abc" + 5` → `"abc5"` (sémantique JavaScript). Documenté en P4 comme intentionnel mais surprenant pour devs Python.

5. **`"1" == 1` retourne `true`** : `AreEqual` line 251 utilise `ToDouble(l) == ToDouble(r)`. Conversion implicite. **Cohérent avec JS `==`** mais pose problème quand on compare un hex magic "MZ" à un int.

### 🟠 Bug potentiel (faible probabilité, à vérifier)

6. **`null.length` ne crashe pas** (line 152) : `EvalMember` traite `null` → retourne `0L`. ✅ OK, vérifié.

7. **`expression invalide cachée par .Compile() cached`** : `_astCache.GetOrAdd(source, Parse)` — si Parse throw `WhfmtExpressionException`, l'exception remonte au caller mais **rien n'est mis en cache**. Sur re-eval, re-parse. Pas un bug.

8. **AST cache unbounded** (line 32, `ConcurrentDictionary<string, WhfmtExprNode>`) — pour un évaluateur qui sert un seul format catalog (~5 expressions × 789 formats max = ~4000 entrées), c'est trivial. Pour un évaluateur réutilisé avec expressions **dynamiquement générées par un caller**, croissance non bornée. **À documenter** comme limitation; pas critique pour v1.3.0.

### 🟢 Sécurité

9. **DoS via expression de 10K caractères** : le parser est récursif descendant, stack ~grammar depth. Une expression `((((...))))` profonde pourrait stack-overflow. Pas vérifié. **Risque faible**: catalog content est trusted, mais si plugin tiers passe input user → risque. **Recommandation: ajouter un test "expression > 1KB throws sanely" en suivi**.

---

## A1.3 — Behavior diff `GetJson` vs `GetJsonV3`

### Trouvaille critique

`GetJsonV3` (livré en Piste A pour la migration camelCase opt-in) est **utilisé nulle part** dans le runtime actuel:

```
$ grep -rn "GetJsonV3" Sources/
Core.Definitions/EmbeddedFormatCatalog.cs:149:    public string GetJsonV3(string resourceKey)
Core.Definitions/Metadata/FormatMetadataExtensions.cs:355:    // (commentaire de doc seulement)
```

Tous les 22+ call-sites de `GetJson` consomment le JSON **brut** (PascalCase préservé). Le migrator P11 + `GetJsonV3` étaient là "pour les plugins tiers qui veulent v3 canonical".

### Implications pour publication NuGet

- **Si on publie 1.3.0 avec `GetJsonV3` public** : on s'engage à le maintenir. Mais aujourd'hui aucun consommateur in-tree → on ne peut pas valider qu'il fait ce qu'il faut.
- **Si on supprime `GetJsonV3`** : ABI break (pas grave, méthode jamais utilisée hors-tests) mais Piste A devient incomplete (commit `04a35bd0` mentionne explicitement la méthode).
- **Si on `internal`** : préservé pour usage futur sans engagement public, peut être promu plus tard.

**Recommandation:** Garder `GetJsonV3` **public mais ajouter un test dédié** (au moins 1 test "GetJsonV3 returns migrated PascalCase→camelCase") pour valider qu'il marche **avant** publication. Sinon **internal**.

---

## A1.4 — Public surface review

### Types publics ajoutés (P0→F8 + Lots) avec recommandation

| Type / Namespace | Visibilité actuelle | Justification | Recommandation |
|---|---|---|---|
| `WpfHexEditor.Core.Definitions.Models.WhfmtVersionMigrator` | public static | Plugins peuvent migrer .whfmt lu d'un disque externe | **Keep public** |
| `Models.WhfmtVariableStore` | public sealed class | Cœur du runtime, consommé par evaluator | **Keep public** |
| `Models.VariableDefinition` (record) | public sealed | Modèle de donnée fondamental | **Keep public** |
| `Models.WhfmtValueType` (enum) + `WhfmtValueTypes` (static) | public | Tools / plugins / validators consomment | **Keep public** |
| `Models.WhfmtEndian` (enum) | public | Lié à VariableDefinition | **Keep public** |
| `Models.Functions.IWhfmtFunction` | public interface | Extension point — custom functions | **Keep public** |
| `Models.Functions.WhfmtFunctionRegistry` | public sealed | API d'extension | **Keep public** |
| `Models.Expressions.WhfmtExpressionEvaluator` | public sealed | API critique évaluation | **Keep public** |
| `Models.Expressions.WhfmtExpressionException` | public sealed | IDE diagnostic rendering | **Keep public** |
| `Models.Expressions.WhfmtExprNode` (abstract record) | public | **⚠️ Probablement internal** — qui sub-classe une AST? | **🔍 Considérer internal** |
| `Models.Expressions.NumberNode/StringNode/.../TernaryNode` (10 records) | public sealed | Idem | **🔍 Considérer internal** |
| `Models.Expressions.UnaryOp` + `BinaryOp` (enums) | public | Idem (consommés uniquement par walker) | **🔍 Considérer internal** |
| `Models.Validation.WhfmtIssueSeverity` + `WhfmtValidationIssue` | public | Tools / IDE diagnostics | **Keep public** |
| `Models.Validation.WhfmtExpressionValidator` | public static | Tool CLI + skill | **Keep public** |
| `Metadata.SoftwareReference / DocReference / FormatRelationship / InspectorHeader / NavigationOverview` (records P5) | public sealed | IDE doc-pane VM | **Keep public** |
| `Metadata.RepairAction / ChecksumSpec / IWhfmtRepairExecutor / RepairResult` (P6) | public | Host implements IWhfmtRepairExecutor | **Keep public** |
| `Metadata.DiffConfig / FuzzConfig / FuzzMutation / FuzzStrategy` (P7) | public | Tools | **Keep public** |
| `Metadata.AssertionStatus / AssertionResult` (F7) | public sealed | IDE consommation future | **Keep public** |
| `Metadata.FormatAssertionEvaluator` (static, F7) | public | API critique runtime | **Keep public** |
| `WpfHexEditor.Core.ProjectSystem.Languages.WhfmtCompletionItem / WhfmtCompletionKind / WhfmtOutlineRule / WhfmtDiagnosticRule / WhfmtDiagnosticSeverity` (P8) | public | Consommés par CodeEditor (cross-assembly) | **Keep public** |
| `WhfmtJsonOptions` (Models/, P11 /simplify) | internal | OK | **Keep internal** |
| `EmbeddedFormatCatalog.GetJsonV3` | public | **⚠️ Dead code** | **🔍 Test ou internal** |

### Surfaces potentiellement à `internal`-iser pour ne pas se lier les mains

Le sous-namespace `Models.Expressions.*` (AST nodes + ops) expose **l'interne** de l'évaluateur. Si on veut, plus tard:
- Changer le parser (e.g. utiliser une lib externe ou un compilateur LL)
- Optimiser l'AST en bytecode
- Ajouter de nouveaux node types sans ABI break

…le fait d'avoir tout en `public` nous lie. **Recommandation: internal-iser les 14 types AST**. Le caller utilise `WhfmtExpressionEvaluator.Evaluate(string)` qui retourne `object?` — il n'a besoin d'aucun type AST.

**Impact:** breaking change vs 1.2.0? Non, car 1.2.0 n'avait pas ces types (introduits en P4). C'est purement la publication initiale de v1.3.0 qui décide.

---

## Synthèse Axe 1

### Bloquants avant publication NuGet 1.3.0

| # | Item | Effort | Priorité |
|---|---|---|---|
| B1 | `GetJsonV3` dead code → test ou `internal` | 15 min | 🔴 Haut |
| B2 | Models.Expressions.* AST nodes → `internal` | 30 min | 🟠 Moyen |
| B3 | 4 surfaces sans test direct (ScoreEntry, FormatSummaryBuilder, GetDocumentationBundle, GetJsonV3) | 30 min | 🟠 Moyen |

### Acceptable / à documenter (pas bloquant)

| # | Item | Action |
|---|---|---|
| D1 | AST cache unbounded sur evaluator long-vivant | Doc USAGE.md |
| D2 | Sémantique JS-like (string+number, "1"==1) | Doc USAGE.md |
| D3 | Division par zéro = Infinity (pas exception) | Doc USAGE.md |
| D4 | Shift count > 63 wrap-around | Doc USAGE.md |

### Sécurité (à suivre, pas bloquant)

| # | Item | Action |
|---|---|---|
| S1 | Stack overflow potentiel expression profonde | Test ad-hoc en suivi |

**Total effort des bloquants:** ~75 min avant publication. Tous fixables sans toucher l'ABI.
