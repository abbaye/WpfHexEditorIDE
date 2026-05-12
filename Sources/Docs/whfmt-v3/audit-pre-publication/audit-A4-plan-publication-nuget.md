# Audit A4 — Plan de publication NuGet coordonné

**Date** : 2026-05-12
**Branche** : Dev2026-05-07
**Scope** : Stratégie d'ordonnancement de la publication NuGet des packages whfmt.* après résolution des blockers A1+A2+A3.

---

## A4.1 — Packages concernés

| Package | Version cible | État actuel | Type |
|---|---|---|---|
| `whfmt.FileFormatCatalog` | **1.3.1** | déjà bumpé | Core lib |
| `whfmt.Analysis` | 1.1.0 | publié | Lib |
| `whfmt.Fuzz` | 1.1.0 | publié | Lib |
| `whfmt.CodeGen` | 1.1.1 | publié | dotnet tool |
| `whfmt.Validate` | **1.0.0** | jamais publié | dotnet tool |

---

## A4.2 — Dépendances inter-packages

```
whfmt.FileFormatCatalog (core, racine)
  ├── whfmt.Analysis (consomme catalog API)
  ├── whfmt.Fuzz (consomme fuzz strategies via catalog)
  ├── whfmt.CodeGen (consomme blocks/variables via catalog)
  └── whfmt.Validate (consomme entries + expression engine)
```

**Conclusion** : `whfmt.FileFormatCatalog` est le **point d'entrée obligatoire**. Tous les outils en dépendent → il doit être publié **en premier**.

---

## A4.3 — Ordre de publication recommandé

### Étape 0 — Résolution des blockers (~100 min)

Travail préalable obligatoire (cf. A1+A2+A3) :

| ID | Action | Durée |
|---|---|---|
| B1 | Supprimer `GetJsonV3()` dead code | 15 min |
| B2 | `internal` sur 14 AST nodes inutiles en surface | 10 min |
| B3 | Tests directs sur 4 surfaces orphelines | 45 min |
| B4 | Étendre enum `category` schéma (8 valeurs) | 5 min |
| B7 | Fix encoding stdout `whfmt.Validate` | 10 min |
| B8 | Smoke test `whfmt validate <fixture>` | 15 min |

→ **Total : ~100 min** avant pouvoir lancer Étape 1.

### Étape 1 — Publication Core (J0)

**whfmt.FileFormatCatalog 1.3.1**
- Pre-flight : `dotnet test EmbeddedWhfmt_Tests` (427+ formats, 6 tests).
- Pre-flight : `dotnet pack` → vérifier taille NUPKG raisonnable.
- Publication : `dotnet nuget push *.nupkg --source nuget.org`.
- Post-flight : vérifier disponibilité ~10 min après push (delay indexation).

### Étape 2 — Publication outils dépendants (J0+1, après indexation Core)

Publier en parallèle (aucune dépendance entre eux) :
- **whfmt.Analysis 1.1.1** (bump patch pour aligner sur Core 1.3.1)
- **whfmt.Fuzz 1.1.1** (bump patch)
- **whfmt.CodeGen 1.1.2** (bump patch)
- **whfmt.Validate 1.0.0** (publication initiale)

Tous doivent référencer `whfmt.FileFormatCatalog >= 1.3.1` dans leur `.csproj`.

### Étape 3 — Validation post-publication (J0+2)

- Créer un projet console temporaire `consumer-smoke/` qui :
  1. Référence `whfmt.FileFormatCatalog 1.3.1` depuis nuget.org.
  2. Appelle `EmbeddedFormatCatalog.GetAll()` et asserte count > 400.
  3. Charge un .whfmt connu (e.g., PNG) et vérifie ses blocks.
- Installer `dotnet tool install -g whfmt.Validate` et lancer `whfmt validate fixtures/PNG.whfmt`.
- Installer `dotnet tool install -g whfmt.CodeGen` et générer un POCO depuis fixtures/ZIP.whfmt.

---

## A4.4 — Stratégie de versioning

| Type de changement | Bump |
|---|---|
| Ajout de format au catalog | patch (1.3.1 → 1.3.2) |
| Refactor interne sans surface visible | patch |
| Nouvelle propriété sur EmbeddedFormatEntry (additive, default-implemented via C# 8.0) | minor (1.3 → 1.4) |
| Suppression de champ public / méthode publique | major (1.x → 2.0) |
| `GetJson()` → `GetJsonV3()` unique (cf. CHANGELOG v4) | **major (2.0.0)** |

---

## A4.5 — Risques résiduels

| Risque | Mitigation |
|---|---|
| Catalog 1.3.1 publié, puis bug critique détecté | Yank policy : `dotnet nuget delete whfmt.FileFormatCatalog 1.3.1` (sous 72h) |
| Outils dépendants installés avant Core indexé sur nuget.org | Étape 2 explicitement à J0+1, après vérif indexation |
| Régression silencieuse runtime expression engine | Step 3 smoke test obligatoire (consumer console app) |
| Schéma JSON externe rejette catalog | B4 résout (ext enum) |
| `whfmt.Validate` stdout cassé sur Windows console | B7 résout (UTF-8 console) |

---

## A4.6 — Rollback plan

Si bug critique post-publication :
1. **NuGet yank** : `dotnet nuget delete <pkg> <version>` (delisting, pas suppression).
2. **Hotfix patch** : bump 1.3.1 → 1.3.2 avec fix isolé.
3. **Communication** : update du README + release notes GitHub.

---

## Verdict A4

✅ **Plan de publication clair et ordonné.**
- Pré-requis : 100 min de blockers (B1-B8) résolus.
- Ordre : Core (1.3.1) → indexation → outils dépendants en parallèle (J0+1) → smoke validation (J0+2).
- Backout possible via NuGet yank si régression.

**Prêt à GO** dès que B1-B4 et B7-B8 sont mergés.
