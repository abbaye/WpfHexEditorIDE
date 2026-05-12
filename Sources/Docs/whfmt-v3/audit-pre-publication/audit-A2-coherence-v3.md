# Audit A2 — Cohérence du contrat v3

**Date** : 2026-05-12
**Branche** : Dev2026-05-07
**Scope** : Vérifier que le contrat v3 (schéma canonique, enums, mappings, renames) est cohérent avec la réalité du catalogue post-Lots 1-7 et du runtime C#.

---

## A2.1 — Lot 7 : mappings valueType exotiques → canoniques

**Question** : Les mappings exotiques (`uint24→uint32`, `vint→int64`, `filetime→int64`, `dostime→uint16`, etc.) ont-ils un impact runtime ?

**Méthode** : Recherche de `WhfmtValueType.` (l'enum déclaratif) dans toute la solution.

**Résultat** : 4 fichiers seulement utilisent l'enum :

| Fichier | Rôle |
|---|---|
| `Models/WhfmtValueType.cs` | Définition de l'enum |
| `Models/VariableDefinition.cs` | Stockage déclaratif |
| `Models/WhfmtVariableParser.cs` | Parser JSON → enum |
| `WhfmtVariables_Tests.cs` | Tests parser |

**Conclusion** : Aucun reader binaire runtime n'utilise `WhfmtValueType` pour discriminer la lecture (pas de `switch (vt) { case Uint24: ReadUInt24() ... }`). Le runtime byte-parser n'existe pas encore (P11 différé). Le Lot 7 est donc :

- ✅ **Safe** côté runtime — aucune régression de lecture (les exotiques étaient déjà non-lus).
- ✅ **Honnête** côté schéma — `uint32` est désormais la promesse minimale et le runtime futur saura toujours la satisfaire.
- ⚠️ **Lossy par design** — `uint24` interprété comme `uint32` perd 8 bits hauts ; documenté dans Lot 7 (commentaire de tête du script).

**Verdict A2.1** : ✅ Cohérent.

---

## A2.2 — Lot 5 : renommages formatId catégorisés

**Question** : Les renommages (`DER→DER_CRYPTO`, `PAK→PAK_GAME`, `YAML→YAML_LANG`, `PDB→PDB_DEBUG` pour Programming, etc.) ont-ils des call-sites C# qui se réfèrent à l'ancien id ?

**Méthode** : Grep des chaînes hardcodées `"DER"`, `"PEM"`, `"P12"`, `"PKCS7"`, `"PAK"`, `"NSF"`, `"YAML"`, `"SYSLOG"`, `"PDB"` dans `.cs` hors test.

**Résultat** :

- `ContentAnalyzer.cs` — label UI "YAML" (chaîne libre, non un formatId).
- `MarkdownEditorHost.xaml.cs` — label de tab "YAML" (idem).
- **0 référence à un formatId catalog dans le code de runtime.**

**Conclusion** : Le contrat formatId n'est consommé que via `EmbeddedFormatCatalog.GetById()` dynamiquement. Les renommages sont uniquement visibles via le catalogue lui-même → pas de surface API à versionner.

**Verdict A2.2** : ✅ Cohérent. Aucune migration code requise.

---

## A2.3 — Schéma JSON v3 vs réalité catalogue post-cleanup

Le fichier canonique `whfmt-schema-canonical-v3.json` (532 lignes) a été comparé à l'état réel post-Lots 1-7.

### A2.3.a — `category` enum incomplet 🚨

Le schéma ligne 19-21 énumère **19 catégories** :

```
3D, Archives, Audio, CAD, Certificates, Crypto, Data, Database,
Disk, Documents, Executables, Game, Images, Medical, Programming,
Security, System, Video, Other
```

Le catalogue physique contient **27 répertoires** :

```
3D, Archives, Audio, CAD, Certificates, Crypto, Data, Database,
Disk, Documents, Executables, Firmware, Fonts, Game, GIS, Images,
IoT, MachineLearning, Medical, Network, Programming, RomHacking,
Science, Subtitles, Synalysis, System, Text, Video, Virtualization
```

**Manquants au schéma** (8) : `Firmware`, `Fonts`, `GIS`, `IoT`, `MachineLearning`, `Network`, `RomHacking`, `Science`, `Subtitles`, `Synalysis`, `Text`, `Virtualization`. Le schéma a aussi `Security` et `Other` non utilisés dans le catalogue.

**Impact** : Une validation JSON Schema externe (Ajv, validators online) rejetterait ~150 fichiers du catalogue. Le runtime C# ignore l'enum (texte libre) → aucun crash interne. La `RuleCategory` côté `whfmt-guard` n'utilise pas cette liste.

**Action requise** : Mettre à jour le schéma v3 pour aligner l'enum avant publication. Soit étendre l'enum, soit relâcher en `string` libre.

### A2.3.b — Schéma `matchMode` enum ✅

Le schéma ligne 62 : `["any", "best", "all"]` avec default `"best"`. Post-Lot 1+7, le catalogue ne contient que ces 3 valeurs (le résidu `"first"` a été corrigé par Lot 7).

**Verdict** : ✅ Aligné.

### A2.3.c — `valueType` ouvert ✅

Le schéma ligne 172 : `"valueType": { "type": "string" }` — pas d'enum strict. Donc même si Lot 7 ramène 9 valeurs exotiques au canonique, le schéma n'imposait rien. Documenter dans la doc canonique la liste recommandée :
```
uint8|uint16|uint32|uint64|int8|int16|int32|int64|float32|float64|ascii|utf8|utf16le|bytes|hex
```
(c'est déjà la description du champ `variables[].type` ligne 118).

**Verdict** : ✅ Aligné par construction (champ libre), mais documentation à harmoniser entre `blocks[].valueType` et `variables[].type`.

### A2.3.d — `references` dual form ✅

Le schéma ligne 32-44 accepte les deux formes (array | object). Post-cleanup, les deux formes coexistent dans le catalogue. C'est conforme. **Note** : v3 préfère array (description ligne 43) mais ne déprécie pas object.

### A2.3.e — `formatRelationships` dual form ✅

Idem `references`. Dict form majoritaire, array form (style A_OUT) minoritaire. Schéma OK.

### A2.3.f — Champs PascalCase deprecated ⚠️

Le schéma ligne 528-531 liste 8 alias PascalCase acceptés en migration :
```
QualityMetrics, MimeTypes, Software, UseCases, TechnicalDetails,
Strength, EntropyHint, MinimumScore
```

Validé : `WhfmtVersionMigrator` les migre. Aucun catalogue post-cleanup n'expose encore de PascalCase (Lots 1-3 ont tout normalisé). **Action** : Ces alias peuvent être retirés en v4 sans casser le catalogue interne.

**Verdict A2.3** : ⚠️ Un blocker (A2.3.a — enum `category` incomplet) à corriger avant publication.

---

## A2.4 — CHANGELOG implicite v3 → v4 (futur)

Liste des changements actuellement non-breaking en v3 mais qui deviendront breaking ou nettoyables en v4 :

| Item | v3 status | v4 plan |
|---|---|---|
| PascalCase aliases (8 fields) | accepté via migrator | **retirer** — migrator obsolète |
| `references` object form | accepté | déprécier → array seul |
| `formatRelationships` dict form | accepté | déprécier → array seul |
| `inspector.groups[].title` (alias de `name`) | accepté | retirer |
| `validation` plain string (ligne 97) | deprecated | retirer |
| `functions` doc-string form | accepté | exiger executable form |
| `variables` dict form (ligne 105-109) | accepté | retirer — array typé seul |
| `GetJson()` legacy API | exposé | retirer → `GetJsonV3()` seul (cf. audit A1.B1 inverse) |
| Exotic valueTypes (`uint24`, `vint`, etc.) | déjà migrés via Lot 7 | reader runtime natif (P11) |
| Categories enum | trop restrictive | étendre ou relâcher (A2.3.a) |

**Estimation v4** : ~3 semaines de refactor (migrator removal + dual-form removal + runtime byte-parser P11).

---

## Verdict global A2

| Sous-axe | Statut |
|---|---|
| A2.1 Lot 7 mappings | ✅ Cohérent |
| A2.2 Lot 5 renommages | ✅ Cohérent |
| A2.3.a Enum category | 🚨 **Blocker** |
| A2.3.b-f Autres schéma | ✅ Cohérent |
| A2.4 CHANGELOG v4 | 📋 Documenté |

**Blockers à résoudre avant NuGet 1.3.0** :
- **B4** : Étendre `category` enum dans `whfmt-schema-canonical-v3.json` (8 valeurs manquantes) — **~5 min**.

Total blockers cumulés depuis A1 + A2 :
- B1 GetJsonV3 dead code (15 min)
- B2 AST nodes internal (10 min)
- B3 Direct tests on 4 surfaces (45 min)
- **B4 Schema category enum (5 min)**

**Total : ~75 min** d'effort résiduel avant publication.
