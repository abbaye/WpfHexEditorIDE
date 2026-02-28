# Exemples de mise à jour des versions

Ce document montre des exemples concrets de fichiers JSON avant et après la mise à jour des versions.

---

## Exemple 1: Format stable (peu de commits)

**Fichier:** `FormatDefinitions/Text/PlainText.json`

### Avant
```json
{
  "formatName": "Plain Text",
  "version": "1.0",
  "extensions": [".txt", ".text"],
  "category": "Text Documents",
  ...
}
```

### Après
```json
{
  "formatName": "Plain Text",
  "version": "1.4",
  "extensions": [".txt", ".text"],
  "category": "Text Documents",
  ...
  "version_history": {
    "commit_count": 4,
    "change_percentage": 472.0,
    "last_updated": "2026-02-24"
  }
}
```

**Analyse:**
- Version: 1.0 → 1.4 (4 commits)
- Changement: 472% (fichier très remanié malgré peu de commits)
- Format le plus stable du projet

---

## Exemple 2: Format moyennement actif

**Fichier:** `FormatDefinitions/Images/WEBP.json`

### Avant
```json
{
  "formatName": "WebP Image",
  "version": "1.0",
  "extensions": [".webp"],
  "category": "Images",
  ...
}
```

### Après
```json
{
  "formatName": "WebP Image",
  "version": "1.8",
  "extensions": [".webp"],
  "category": "Images",
  ...
  "version_history": {
    "commit_count": 8,
    "change_percentage": 344.78,
    "last_updated": "2026-02-24"
  }
}
```

**Analyse:**
- Version: 1.0 → 1.8 (8 commits)
- Changement: 344.78% (refactorings multiples)
- Format typique de la majorité (187 formats en version 1.8)

---

## Exemple 3: Format très actif

**Fichier:** `FormatDefinitions/Data/MSGPACK.json`

### Avant
```json
{
  "formatName": "MessagePack",
  "version": "1.0",
  "extensions": [".msgpack", ".mp"],
  "category": "Data",
  ...
}
```

### Après
```json
{
  "formatName": "MessagePack",
  "version": "2.1",
  "extensions": [".msgpack", ".mp"],
  "category": "Data",
  ...
  "version_history": {
    "commit_count": 11,
    "change_percentage": 284.78,
    "last_updated": "2026-02-24"
  }
}
```

**Analyse:**
- Version: 1.0 → 2.1 (11 commits - le maximum)
- Changement: 284.78%
- Format le plus actif, parmi les 5 formats en version 2.1

---

## Exemple 4: Format avec changements massifs

**Fichier:** `FormatDefinitions/Archives/ZIP.json`

### Avant
```json
{
  "formatName": "ZIP Archive",
  "version": "1.0",
  "extensions": [".zip", ".jar", ".apk", ".xpi"],
  "category": "Archives",
  ...
}
```

### Après
```json
{
  "formatName": "ZIP Archive",
  "version": "1.9",
  "extensions": [".zip", ".jar", ".apk", ".xpi"],
  "category": "Archives",
  ...
  "version_history": {
    "commit_count": 9,
    "change_percentage": 418.97,
    "last_updated": "2026-02-24"
  }
}
```

**Analyse:**
- Version: 1.0 → 1.9 (9 commits)
- Changement: 418.97% (un des plus élevés, 8e position)
- Format complexe avec de nombreuses extensions et enrichissements

---

## Interprétation des métriques

### Version
- **1.4 - 1.7**: Formats peu modifiés (4-7 commits)
- **1.8 - 1.9**: Formats moyennement actifs (8-9 commits) - **89% des formats**
- **2.0+**: Formats très actifs (10+ commits) - formats prioritaires

### Pourcentage de changement
- **< 250%**: Changements modérés
- **250% - 350%**: Changements importants (typique)
- **> 350%**: Refactorings majeurs multiples

Le pourcentage élevé est normal car:
1. On cumule TOUS les commits (pas juste le dernier)
2. Les enrichissements successifs modifient le même fichier plusieurs fois
3. Un fichier peut être entièrement refactorisé puis enrichi ensuite

**Exemple:** ZIP.json (253 lignes actuelles)
- Total changements: 1,060 lignes (additions + suppressions)
- Pourcentage: 1,060 / 253 = 418.97%
- Cela signifie: ~4.2× la taille du fichier a été modifiée au total

---

## Formats les plus modifiés (TOP 5)

1. **MessagePack** (Data) - 11 commits, version 2.1
2. **IBM DB2 Database** (Database) - 11 commits, version 2.1
3. **Game Boy Advance ROM** (Game) - 11 commits, version 2.1, 425% changement
4. **Nintendo GameCube Disc** (Game) - 11 commits, version 2.1
5. **Nintendo Wii Disc** (Game) - 11 commits, version 2.1

## Formats avec le plus de changements (TOP 5)

1. **Windows Executable (PE)** (Executables) - 487.8% changement
2. **Plain Text** (Text Documents) - 472.0% changement
3. **Static Library Archive** (Programming) - 449.7% changement
4. **PNG Image** (Images) - 431.6% changement
5. **Game Boy Advance ROM** (Game) - 425.3% changement

## Formats les plus stables (TOP 5)

1. **Plain Text** (Text Documents) - 4 commits, version 1.4
2. **IPS Patch** (Game) - 7 commits, version 1.7
3. **Sega Dreamcast Disc Image** (Game) - 7 commits, version 1.7
4. **Sega Genesis/Mega Drive ROM** (Game) - 7 commits, version 1.7
5. **PlayStation 2 Disc Image** (Game) - 7 commits, version 1.7

---

## Utilisation des métadonnées

### Pour la documentation
```json
"version_history": {
  "commit_count": 8,        // Combien de fois le format a été modifié
  "change_percentage": 344.78, // Intensité totale des changements
  "last_updated": "2026-02-24" // Date de dernière analyse
}
```

### Pour le développement
- **Prioriser les tests** sur les formats version 2.0+ (plus actifs)
- **Identifier les formats matures** (version 1.4-1.7, peu de commits)
- **Suivre l'évolution** en réexécutant l'analyse périodiquement

### Pour les utilisateurs
- Formats 2.0+: En développement actif, peuvent évoluer
- Formats 1.8-1.9: Matures et stables
- Formats 1.4-1.7: Très stables, peu de changements

---

*Généré le 2026-02-24*
