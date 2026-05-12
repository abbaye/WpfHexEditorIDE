# Audit A3 — Maturité des outils périphériques

**Date** : 2026-05-12
**Branche** : Dev2026-05-07
**Scope** : Évaluer si les 5 outils du dossier `Sources/Tools/whfmt.*` sont prêts pour la publication (NuGet ou usage interne).

---

## A3.0 — Inventaire

| Outil | Version | Kind | Package | README | C# files | Tests |
|---|---|---|---|---|---|---|
| whfmt.Analysis | 1.1.0 | Library | ✅ `whfmt.Analysis` | ✅ | 6 | ❌ |
| whfmt.Backfill | (Exe, IsPackable=false) | Internal exe | — interne — | ❌ | 16 | ✅ WhfmtBackfill.Tests |
| whfmt.CodeGen | 1.1.1 | dotnet tool | ✅ `whfmt.CodeGen` (`whfmt-codegen`) | ✅ | 20 | ❌ |
| whfmt.Fuzz | 1.1.0 | Library | ✅ `whfmt.Fuzz` | ✅ | 7 | ❌ |
| whfmt.Validate | 1.0.0 | dotnet tool | ✅ `whfmt.Validate` (`whfmt`) | ✅ | 16 | ❌ |

---

## A3.1 — whfmt.Analysis (v1.1.0, lib)

**Rôle** : Statistiques + reports sur catalogue (densité, couverture, gaps catégoriels).

**Force** : 6 fichiers C#, README présent, déjà v1.1.0 → 2 itérations publiées.

**Faiblesses** :
- ❌ Aucun test unitaire dédié (statistiques peuvent partir en silence).
- ⚠️ Pas d'invariants de sortie documentés (le format des rapports peut changer en patch).

**Verdict** : 🟡 **Publiable** mais sans garantie de stabilité de sortie. Ajouter 1-2 tests de smoke (catalog small fixture → comptage attendu) recommandé en v1.2.0.

---

## A3.2 — whfmt.Backfill (interne)

**Rôle** : Outil de migration v2→v3 / Lots cleanup.

**État** : `IsPackable=false` → assumé interne. C'est cohérent (one-shot migration).

**Force** : ✅ **Le seul tool qui a des tests** (`Sources/Tests/WhfmtBackfill.Tests/`).

**Verdict** : ✅ **OK** — pas de publication prévue, son rôle est consommé une fois.

---

## A3.3 — whfmt.CodeGen (v1.1.1, dotnet tool)

**Rôle** : Génère des classes C# à partir de .whfmt (POCO+reader).

**Force** : Déjà packagé comme `dotnet tool`, command `whfmt-codegen`, déjà à v1.1.1.

**Faiblesses** :
- ❌ Aucun test (générer du C# sans tests d'or n'est pas bridé).
- ⚠️ Le code généré n'est pas testé contre compilation Roslyn → risque de générer du code qui ne compile pas.

**Action recommandée v1.2.0** : Roslyn-compile smoke test sur un échantillon (PNG, ZIP, ELF) — ~30 min.

**Verdict** : 🟡 **Publiable** mais B5 conseillé (smoke compile test) avant 1.2.0.

---

## A3.4 — whfmt.Fuzz (v1.1.0, lib)

**Rôle** : Mutation library pour générer corpus de test à partir des `fuzz.strategies` du catalogue.

**Faiblesses** :
- ❌ Aucun test → pour une lib de fuzzing, c'est paradoxal.
- ⚠️ Les mutations doivent être déterministes pour reproductibilité (seed) — non vérifié par test.

**Action recommandée v1.2.0** : Tests de reproductibilité (même seed → même mutation) — ~20 min.

**Verdict** : 🟡 **Publiable** mais B6 (seed-determinism test) recommandé.

---

## A3.5 — whfmt.Validate (v1.0.0, dotnet tool)

**Rôle** : CLI `whfmt validate|lint-expressions|...` consommé par le skill `whfmt-guard` R10.

**Force** : Déjà packagé comme tool, command `whfmt`.

**Faiblesses** :
- ❌ Aucun test direct (le skill `whfmt-guard` test indirectement via R10 wrapper, mais c'est implicite).
- ⚠️ Encoding bug stdout (mentionné dans le bilan post-cleanup) → 1 ERR résiduel.
- ⚠️ v1.0.0 → première publication, aucun feedback terrain.

**Action recommandée avant publication NuGet 1.0.0** : 
- **B7** : Fixer le bug encoding stdout (UTF-8 console output) — ~10 min.
- **B8** : Smoke test `whfmt validate <fixture>` retourne exit-code 0 — ~15 min.

**Verdict** : 🟡 **À corriger** avant publication initiale.

---

## A3.6 — Couverture de tests croisée

```
Library                    Tests           Coverage
─────────────────────────────────────────────────────
whfmt.FileFormatCatalog    ✅ EmbeddedWhfmt_Tests + 11 autres  bon
whfmt.Analysis             ❌                                  zéro
whfmt.Backfill             ✅ WhfmtBackfill.Tests              bon
whfmt.CodeGen              ❌                                  zéro
whfmt.Fuzz                 ❌                                  zéro
whfmt.Validate             ❌                                  zéro
```

**Constat** : 4 outils sur 5 ont **zéro** tests. C'est la dette technique la plus visible de l'écosystème whfmt.

---

## Verdict global A3

| Outil | Statut publication |
|---|---|
| whfmt.Analysis 1.1.0 | 🟡 Publiable as-is, smoke test conseillé |
| whfmt.Backfill | ✅ Interne, OK |
| whfmt.CodeGen 1.1.1 | 🟡 Publiable, Roslyn-compile smoke conseillé |
| whfmt.Fuzz 1.1.0 | 🟡 Publiable, seed-determinism conseillé |
| whfmt.Validate 1.0.0 | 🟠 **Encoding bug à fixer** + smoke test (B7+B8) |

**Blockers à résoudre pour publication coordonnée des outils** :
- **B7** : Fix `whfmt.Validate` stdout UTF-8 encoding (~10 min) — **bloque whfmt.Validate 1.0.0**.
- **B8** : Smoke test `whfmt validate <fixture>` (~15 min) — **bloque whfmt.Validate 1.0.0**.

**Recommandations non-bloquantes** (post-publication, v+1) :
- B5 : Roslyn-compile smoke test pour whfmt.CodeGen.
- B6 : Seed-determinism test pour whfmt.Fuzz.
- B9 : Smoke test stats pour whfmt.Analysis.

**Total cumulé (A1+A2+A3 blockers)** :
- B1 GetJsonV3 dead code — 15 min
- B2 AST nodes internal — 10 min
- B3 Direct tests 4 surfaces — 45 min
- B4 Schema category enum — 5 min
- B7 Validate encoding — 10 min
- B8 Validate smoke test — 15 min
- **Total : ~100 min avant publication coordonnée.**
