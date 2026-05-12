# Audit Pré-Publication whfmt v3 — Synthèse & Décision

**Date** : 2026-05-12
**Branche** : Dev2026-05-07
**Auditeur** : Claude Opus 4.7 (1M context)
**Scope** : Décision GO / STOP pour publication coordonnée NuGet de l'écosystème whfmt v3.

---

## Récapitulatif des 4 axes

| Axe | Verdict | Blockers |
|---|---|---|
| A1 — Dette technique cachée | 🟡 résoluble | B1, B2, B3 (~70 min) |
| A2 — Cohérence contrat v3 | 🟡 résoluble | B4 (~5 min) |
| A3 — Maturité outils périphériques | 🟡 résoluble | B7, B8 (~25 min) |
| A4 — Plan publication NuGet | ✅ prêt | — |

---

## Blockers consolidés (préalables à GO)

| ID | Description | Effort |
|---|---|---|
| B1 | Supprimer `GetJsonV3()` dead code | 15 min |
| B2 | `internal` sur 14 AST nodes inutiles | 10 min |
| B3 | Tests directs sur 4 surfaces orphelines | 45 min |
| B4 | Étendre enum `category` du schéma v3 (8 valeurs) | 5 min |
| B7 | Fix encoding stdout `whfmt.Validate` | 10 min |
| B8 | Smoke test `whfmt validate <fixture>` | 15 min |
| **Total** | | **~100 min** |

---

## Recommandations non-bloquantes (v+1)

| ID | Description | Quand |
|---|---|---|
| B5 | Roslyn-compile smoke test pour whfmt.CodeGen | v1.2.0 |
| B6 | Seed-determinism test pour whfmt.Fuzz | v1.2.0 |
| B9 | Smoke stats test pour whfmt.Analysis | v1.2.0 |

---

## État actuel de l'écosystème

**Catalogue** :
- ✅ 427+ formats, 0 ERR / 0 WARN après Lots 1-7.
- ✅ 9 collisions formatId résolues (Lot 5).
- ✅ 8 fichiers Unix avec extensions fictives (Lot 6).
- ✅ 11 valueTypes exotiques rabattus sur canoniques (Lot 7).
- ✅ Schéma v3 documenté (`whfmt-schema-canonical-v3.json`, 532 lignes).

**Runtime** :
- ✅ Expression evaluator complet (lexer + parser + AST cache).
- ✅ Variable store + function registry.
- ✅ FormatAssertionEvaluator bridge P4 ↔ catalog.
- ✅ Catalog API stable (`EmbeddedFormatCatalog`, 22 call-sites internes).

**Outils** :
- ✅ 4/5 outils déjà packagés (`Analysis`, `Fuzz`, `CodeGen`, `Validate`).
- ✅ Skill `whfmt-guard` opérationnel (8 règles).
- ⚠️ 4/5 outils sans tests dédiés → dette à combler en v+1.

**Tests** :
- ✅ 141 tests sur 12 fichiers de test catalog/runtime.
- ✅ `EmbeddedWhfmt_Tests` gate build (6 tests).
- ⚠️ Couverture périphérique faible (1 seul tool a des tests).

---

## Décision

### 🟡 **CONDITIONAL GO**

**Conditions de levée des blockers** :
1. Phase B (~100 min de travail) doit être exécutée avant `dotnet nuget push` :
   - B1+B2+B3 : nettoyage API surface (Core).
   - B4 : alignement schéma canonique.
   - B7+B8 : stabilisation `whfmt.Validate`.

2. Pipeline de validation post-blockers :
   - Build full solution propre (0 erreur, 0 warning).
   - `dotnet test` → 141 tests verts.
   - `whfmt validate Sources/Core/.../FormatDefinitions/**/*.whfmt` exit 0.

3. Publication suivant le plan A4 (Core J0 → outils J0+1 → smoke J0+2).

### Évaluation des risques (ENABLE_GUARDIAN)

- **Risk : LOW** — Tous les blockers identifiés sont mécaniques (suppression de code mort, ajout de tests, fix d'encoding). Aucun ne touche à l'architecture.
- **Scope : MEDIUM** — 5 packages NuGet à coordonner, mais procédure rollback claire (yank).
- **Recommendation : PROCEED après Phase B**.

---

## Plan d'exécution post-audit

1. **Phase B** (résolution blockers, ~100 min) — commit unique ou 6 commits granulaires.
2. **`/simplify`** sur Phase B avant push.
3. **Tag** `whfmt-v3-publish-ready` sur HEAD.
4. **Publication NuGet** selon plan A4.
5. **Mise à jour CHANGELOG** + release notes GitHub.

---

## Conclusion

L'écosystème whfmt v3 est **fonctionnellement prêt** : 427 formats validés, runtime expression engine complet, schéma documenté, outils packagés. Les blockers identifiés sont mineurs et résolubles en ~100 minutes de travail mécanique.

**Recommandation finale : GO conditionnel.** Exécuter Phase B (B1-B4, B7-B8), puis publier selon le plan A4. Aucun frein architectural détecté.
