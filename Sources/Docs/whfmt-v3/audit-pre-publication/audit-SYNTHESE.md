# whfmt v3 Pre-Publication Audit — Synthesis & Decision

**Date** : 2026-05-12
**Branch** : Dev2026-05-07
**Auditor** : Claude Opus 4.7 (1M context)
**Scope** : GO / STOP decision for the coordinated NuGet publication of the whfmt v3 ecosystem.

---

## 4-axis recap

| Axis | Verdict | Blockers |
|---|---|---|
| A1 — Hidden technical debt | 🟡 fixable | B1, B2, B3 (~70 min) |
| A2 — v3 contract coherence | 🟡 fixable | B4 (~5 min) |
| A3 — Peripheral tools maturity | 🟡 fixable | B7, B8 (~25 min) |
| A4 — NuGet publication plan | ✅ ready | — |

---

## Consolidated blockers (prerequisites for GO)

| ID | Description | Effort |
|---|---|---|
| B1 | Remove `GetJsonV3()` dead code (or add coverage) | 15 min |
| B2 | Mark 14 unused AST nodes `internal` | 10 min |
| B3 | Direct tests on 4 orphan surfaces | 45 min |
| B4 | Extend v3 schema `category` enum (8 values) | 5 min |
| B7 | Fix stdout encoding in `whfmt.Validate` | 10 min |
| B8 | Smoke test for `whfmt validate <fixture>` | 15 min |
| **Total** | | **~100 min** |

> All 6 blockers were resolved in commit `dd418f2e` (+ 2 follow-up bug fixes in `3fc0f96f`
> caught by B8). Phase B is complete and the audit is published.

---

## Non-blocking recommendations (v+1)

| ID | Description | When |
|---|---|---|
| B5 | Roslyn-compile smoke test for whfmt.CodeGen | v1.2.0 |
| B6 | Seed-determinism test for whfmt.Fuzz | v1.2.0 |
| B9 | Smoke stats test for whfmt.Analysis | v1.2.0 |

---

## Current state of the ecosystem

**Catalog**

- ✅ 789 `.whfmt` + 10 `.grammar` definitions, 0 ERR / 0 WARN after Lots 1-7.
- ✅ 9 `formatId` collisions resolved (Lot 5).
- ✅ 8 Unix-style files given fictive extensions (Lot 6).
- ✅ 11 exotic `valueType` values mapped to canonical (Lot 7).
- ✅ v3 schema documented (`whfmt-schema-canonical-v3.json`, 532 lines).

**Runtime**

- ✅ Complete expression evaluator (lexer + parser + AST cache).
- ✅ Variable store + function registry.
- ✅ `FormatAssertionEvaluator` bridge — P4 evaluator ↔ catalog assertions.
- ✅ Catalog API stable (`EmbeddedFormatCatalog`, 22 internal call-sites).

**Tools**

- ✅ 4/5 tools already packaged (`Analysis`, `Fuzz`, `CodeGen`, `Validate`).
- ✅ `whfmt-guard` skill operational (8 rules).
- ⚠️ 4/5 tools without dedicated tests → debt to be paid in v+1.

**Tests**

- ✅ 150 tests across the catalog/runtime test suite (was 141 pre-Phase B).
- ✅ `EmbeddedWhfmt_Tests` build gate (6 tests).
- ⚠️ Low peripheral coverage (only 1 tool has dedicated tests — `whfmt.Backfill`).

---

## Decision

### ✅ **GO** (post-Phase B)

The audit produced a CONDITIONAL GO; all conditions were lifted by commits
`dd418f2e` (Phase B B1-B4 + B7-B8) and `3fc0f96f` (2 bug fixes caught by B8).

**Validation pipeline executed post-blockers** :

- ✅ Full solution build — 0 errors.
- ✅ `dotnet test` — 150 / 150 whfmt-scope tests pass (EmbeddedWhfmt + CatalogLookups
  + Expression + WhfmtSurfaces + Backfill).
- ✅ `whfmt validate <PNG fixture>` exit code 0 with UTF-8 glyphs rendered correctly.

**Coordinated publication ordering** (plan A4) :

1. **Day 0** — `whfmt.FileFormatCatalog 1.3.2` (Core).
2. **Day 0+1** (after nuget.org indexing) — `whfmt.Analysis 1.1.1`, `whfmt.Fuzz 1.1.1`,
   `whfmt.CodeGen 1.1.2`, `whfmt.Validate 1.0.0` (initial release) in parallel.
3. **Day 0+2** — consumer smoke check (`dotnet tool install -g whfmt.Validate` +
   reference `whfmt.FileFormatCatalog` from a fresh console app).

### Risk assessment (ENABLE_GUARDIAN)

- **Risk : LOW** — All identified blockers were mechanical (dead-code removal, test
  additions, encoding fix). None touched the architecture.
- **Scope : MEDIUM** — 5 NuGet packages to coordinate, but rollback procedure is clear
  (NuGet yank within 72h).
- **Recommendation : PROCEED**.

---

## Post-audit execution log

1. ✅ **Phase B** (resolved blockers, ~100 min actual) — committed as `dd418f2e`.
2. ✅ **B8 smoke test caught 2 real bugs** before publication (`3fc0f96f`):
   - `FormatMatcher.ScoreEntry` `ArgumentOutOfRangeException` on negative-offset signatures.
   - `ValidationEngine.RunAssertions` `InvalidOperationException` on bool/null variables.
3. ✅ **Section B cleanup** — 15 internal/stub csproj marked `IsPackable=false` (`630442f2`).
4. ✅ **12-package version + README + release-notes refresh** (`13d59a9d` → `0b840baf`).
5. ✅ **Catalog guide v3 rewrite** (`754dee84`).
6. ✅ **`dotnet pack` validation** — 12/12 `.nupkg` generated to `artifacts/nuget/`.

---

## Conclusion

The whfmt v3 ecosystem is **publication-ready**. 789 validated formats, complete
runtime expression engine, documented schema, packaged tools, refreshed README and
guide on the catalog, 12 coordinated `.nupkg` ready to push.

**Final recommendation : GO.** Publish following plan A4. No architectural blocker
detected. Rollback path documented (NuGet yank + hotfix patch).
