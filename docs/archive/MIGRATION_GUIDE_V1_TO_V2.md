# 🔄 Guide de Migration : HexEditorLegacy (V1) → HexEditor (V2)

**Version** : 2026-02-19
**Temps estimé** : 30 minutes - 2 heures (selon taille du projet)
**Difficulté** : ⭐⭐ Facile à Modérée

---

## 📋 Vue d'Ensemble

Ce guide vous accompagne étape par étape pour migrer votre code de **HexEditorLegacy (V1)** vers **HexEditor (V2)**.

**Avantages de la migration** :
- ✅ Performances 16-5882x supérieures
- ✅ Nouvelles fonctionnalités (async, comparaison avancée)
- ✅ Support actif et maintenance continue
- ✅ API 100% compatible (ajustements mineurs uniquement)

---

## 🎯 Checklist de Migration

### Avant de Commencer
- [ ] Créer une branche Git : `git checkout -b migrate-to-hexeditor-v2`
- [ ] Sauvegarder votre projet
- [ ] Lire ce guide en entier
- [ ] Vérifier les dépendances NuGet

### Étapes de Migration
- [ ] 1. Mettre à jour les références NuGet/assemblies
- [ ] 2. Remplacer les namespaces et types
- [ ] 3. Migrer l'initialisation (XAML + Code-behind)
- [ ] 4. Ajuster les appels de méthodes
- [ ] 5. Mettre à jour les event handlers
- [ ] 6. Tester votre application
- [ ] 7. (Optionnel) Utiliser les nouvelles fonctionnalités V2

---

## 📦 Étape 1 : Mettre à Jour les Références

### Option A : NuGet (Recommandé)

```bash
# Désinstaller V1
dotnet remove package WPFHexaEditorLegacy

# Installer V2
dotnet add package WPFHexaEditor
```

### Option B : Référence Projet Local

Dans votre `.csproj` :

**Avant (V1)** :
```xml
<ProjectReference Include="..\WPFHexaEditor.Legacy\WPFHexaEditorLegacy.csproj" />
```

**Après (V2)** :
```xml
<ProjectReference Include="..\WPFHexaEditor\WpfHexEditorCore.csproj" />
```

---

## 🔄 Étape 2 : Remplacer les Namespaces

### Dans les Fichiers XAML

**Avant (V1)** :
```xml
xmlns:hexEditor="clr-namespace:WpfHexaEditor;assembly=WPFHexaEditorLegacy"
```

**Après (V2)** :
```xml
xmlns:hexEditor="clr-namespace:WpfHexaEditor;assembly=WPFHexaEditor"
```

### Dans les Fichiers C#

**Avant (V1)** :
```csharp
using WpfHexaEditor.Legacy;
```

**Après (V2)** :
```csharp
using WpfHexaEditor;
```

---

## 🏗️ Étape 3 : Migrer l'Initialisation

### 3.1 Migration XAML

**Avant (V1)** :
```xml
<Window xmlns:hex="clr-namespace:WpfHexaEditor;assembly=WPFHexaEditorLegacy">
    <hex:HexEditorLegacy
        x:Name="hexEditor"
        BytePerLine="16"
        Stream="{Binding MyStream}" />
</Window>
```

**Après (V2)** :
```xml
<Window xmlns:hex="clr-namespace:WpfHexaEditor;assembly=WPFHexaEditor">
    <hex:HexEditor
        x:Name="hexEditor"
        BytePerLine="16"
        FileName="{Binding FilePath}" />
        <!-- Note: Stream est maintenant read-only, utiliser FileName ou code-behind -->
</Window>
```

### 3.2 Migration Code-Behind

**Avant (V1)** :
```csharp
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Charger un fichier en V1
        hexEditor.Stream = File.OpenRead("data.bin");
    }
}
```

**Après (V2)** :
```csharp
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // V2 : Utiliser OpenFile() au lieu de Stream
        hexEditor.OpenFile("data.bin");
    }
}
```

---

## 🔧 Étape 4 : Ajuster les Appels de Méthodes

### 4.1 GetByte - Ajouter Paramètre

**Avant (V1)** :
```csharp
var (byteValue, success) = hexEditor.GetByte(position);
```

**Après (V2)** :
```csharp
var (byteValue, success) = hexEditor.GetByte(position, copyChange: true);
```

**Note** : `copyChange = true` retourne la valeur modifiée (comportement V1 par défaut)

---

### 4.2 Stream - Utiliser OpenFile

**Avant (V1)** :
```csharp
// Charger un fichier
hexEditor.Stream = File.OpenRead(path);

// Charger des bytes en mémoire
hexEditor.Stream = new MemoryStream(byteArray);
```

**Après (V2)** :
```csharp
// Charger un fichier
hexEditor.OpenFile(path);

// Charger des bytes en mémoire (via ByteProvider)
// Actuellement pas de méthode OpenMemory directe sur HexEditor
// Utiliser un fichier temporaire OU accéder au Provider :
hexEditor.OpenFile(path); // Fichier
// OU
// Si besoin d'accès Provider direct, voir section avancée
```

**Solution temporaire pour MemoryStream** :
```csharp
// Créer un fichier temporaire
var tempFile = Path.GetTempFileName();
File.WriteAllBytes(tempFile, byteArray);
hexEditor.OpenFile(tempFile);
```

---

### 4.3 Autres Méthodes (Aucun Changement)

Ces méthodes fonctionnent **identiquement** en V1 et V2 :

```csharp
// Sélection
hexEditor.SelectAll();
hexEditor.SetPosition(100);
hexEditor.SetPosition(0x100, 10); // Position + longueur

// Modification
hexEditor.ModifyByte(50, 0xFF);
hexEditor.DeleteSelection();
hexEditor.InsertByte(0xAA, 100);

// Recherche
var results = hexEditor.FindAll(pattern);
hexEditor.FindNext(pattern);

// Clipboard
hexEditor.CopyToClipboard();
hexEditor.PasteFromClipboard();

// Fichiers
hexEditor.Save();
hexEditor.SaveAs("output.bin");

// Undo/Redo
hexEditor.Undo();
hexEditor.Redo();
```

---

## 📡 Étape 5 : Mettre à Jour les Event Handlers

### 5.1 Événements Renommés

| V1 (Legacy) | V2 |
|-------------|-----|
| `LengthChanged` | `DataChanged` |
| `ReadOnlyModeChanged` | `PropertyChanged` (avec `e.PropertyName == "ReadOnlyMode"`) |
| `UndoCompleted` / `RedoCompleted` | `PropertyChanged` (avec `e.PropertyName == "CanUndo"` / `"CanRedo"`) |

**Migration Exemple** :

**Avant (V1)** :
```csharp
hexEditor.LengthChanged += (s, e) =>
{
    UpdateStatusBar();
};
```

**Après (V2)** :
```csharp
hexEditor.DataChanged += (s, e) =>
{
    UpdateStatusBar();
};
```

### 5.2 PropertyChanged - Approche Générique

**V2 Recommandé** :
```csharp
hexEditor.PropertyChanged += (s, e) =>
{
    switch (e.PropertyName)
    {
        case "CanUndo":
            undoButton.IsEnabled = hexEditor.CanUndo;
            break;
        case "CanRedo":
            redoButton.IsEnabled = hexEditor.CanRedo;
            break;
        case "IsModified":
            UpdateTitle();
            break;
    }
};
```

---

## 🎁 Étape 6 : (Optionnel) Utiliser les Nouvelles Fonctionnalités V2

### 6.1 Opérations Asynchrones

**Recherche Asynchrone** :
```csharp
// V2 uniquement - recherche sans bloquer l'UI
var pattern = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F };
var results = await hexEditor.FindAllAsync(pattern, highlight: true);

Console.WriteLine($"Trouvé {results.Count} occurrences");
```

**Comparaison Asynchrone** :
```csharp
// Comparer deux fichiers sans bloquer l'UI
var otherEditor = new HexEditor();
otherEditor.OpenFile("file2.bin");

var differences = await hexEditor.CompareAsync(otherEditor);
Console.WriteLine($"{differences.Count} différences trouvées");
```

---

### 6.2 Persistance d'État

**Sauvegarder/Restaurer l'État** :
```csharp
// Sauvegarder position, sélection, bookmarks, etc.
var state = hexEditor.SaveState();
File.WriteAllText("editor-state.json", state);

// Restaurer plus tard
var savedState = File.ReadAllText("editor-state.json");
hexEditor.RestoreState(savedState);
```

---

### 6.3 Comparaison Avancée avec Couleurs

```csharp
// V2 : Comparaison avec couleurs personnalisées
var comparisonSettings = new ComparisonSettings
{
    MatchColor = Brushes.Green,
    MismatchColor = Brushes.Red,
    MissingColor = Brushes.Gray
};

hexEditor.Compare(otherEditor, comparisonSettings);
```

---

## ✅ Étape 7 : Tester Votre Application

### Checklist de Tests

- [ ] **Chargement de fichiers** : Vérifier que les fichiers s'ouvrent correctement
- [ ] **Affichage** : Vérifier que les bytes s'affichent correctement
- [ ] **Sélection** : Tester SelectAll, SetPosition, ClearSelection
- [ ] **Modification** : Tester ModifyByte, InsertByte, DeleteByte
- [ ] **Undo/Redo** : Vérifier que l'historique fonctionne
- [ ] **Recherche** : Tester FindAll, FindNext, ReplaceAll
- [ ] **Sauvegarde** : Vérifier Save et SaveAs
- [ ] **Performance** : Comparer avec V1 (devrait être plus rapide)

### Tests Automatisés

Inspirez-vous des tests Phase 1 :

```csharp
[TestMethod]
public void Migration_LoadFile_DisplaysCorrectly()
{
    var hexEditor = new HexEditor();
    hexEditor.OpenFile("test-data.bin");

    Assert.IsTrue(hexEditor.IsFileOrStreamLoaded);
    Assert.IsTrue(hexEditor.VirtualLength > 0);
}

[TestMethod]
public void Migration_GetByte_ReturnsCorrectValue()
{
    var hexEditor = new HexEditor();
    hexEditor.OpenFile("test-data.bin");

    var (byteValue, success) = hexEditor.GetByte(0, true);

    Assert.IsTrue(success);
    Assert.AreEqual(expectedFirstByte, byteValue);
}
```

---

## 🚨 Problèmes Courants et Solutions

### Problème 1 : "Stream est en lecture seule"

**Erreur** :
```
CS0200: Property or indexer 'HexEditor.Stream' cannot be assigned to -- it is read only
```

**Solution** :
```csharp
// ❌ NE FONCTIONNE PLUS
hexEditor.Stream = myStream;

// ✅ UTILISER À LA PLACE
hexEditor.OpenFile(filePath);
```

---

### Problème 2 : "GetByte ne peut pas être inféré"

**Erreur** :
```
CS1061: 'HexEditor' does not contain a definition for 'GetByte' that accepts 1 argument
```

**Solution** :
```csharp
// ❌ V1
var (byte, success) = hexEditor.GetByte(position);

// ✅ V2
var (byte, success) = hexEditor.GetByte(position, copyChange: true);
```

---

### Problème 3 : "Événement LengthChanged n'existe pas"

**Erreur** :
```
CS0117: 'HexEditor' does not contain a definition for 'LengthChanged'
```

**Solution** :
```csharp
// ❌ V1
hexEditor.LengthChanged += Handler;

// ✅ V2
hexEditor.DataChanged += Handler;
```

---

### Problème 4 : Besoin de MemoryStream (pas de fichier)

**Solution** : Utiliser fichier temporaire

```csharp
// Helper pour ouvrir des bytes en mémoire
public static void LoadBytesInMemory(HexEditor editor, byte[] data)
{
    var tempFile = Path.Combine(Path.GetTempPath(), $"hexedit-{Guid.NewGuid()}.tmp");
    File.WriteAllBytes(tempFile, data);
    editor.OpenFile(tempFile);

    // Optionnel : Nettoyer à la fermeture
    editor.Closed += (s, e) =>
    {
        if (File.Exists(tempFile))
            File.Delete(tempFile);
    };
}

// Utilisation
LoadBytesInMemory(hexEditor, myByteArray);
```

---

## 📊 Tableau de Correspondance API

| Fonctionnalité | V1 (Legacy) | V2 | Changement |
|----------------|-------------|-----|------------|
| **Charger fichier** | `Stream = File.OpenRead(...)` | `OpenFile(path)` | ⚠️ Méthode différente |
| **GetByte** | `GetByte(pos)` | `GetByte(pos, copyChange)` | ⚠️ Paramètre supplémentaire |
| **SelectAll** | `SelectAll()` | `SelectAll()` | ✅ Identique |
| **FindAll** | `FindAll(pattern)` | `FindAll(pattern)` | ✅ Identique |
| **Save** | `Save()` | `Save()` | ✅ Identique |
| **Undo/Redo** | `Undo()` / `Redo()` | `Undo()` / `Redo()` | ✅ Identique |
| **LengthChanged** | `LengthChanged` event | `DataChanged` event | ⚠️ Événement renommé |
| **FindAsync** | ❌ N'existe pas | `FindAllAsync()` | ✨ Nouveau en V2 |
| **CompareAsync** | ❌ N'existe pas | `CompareAsync()` | ✨ Nouveau en V2 |
| **SaveState** | ❌ N'existe pas | `SaveState()` / `RestoreState()` | ✨ Nouveau en V2 |

---

## 📈 Gains de Performance Attendus

Après migration vers V2, vous devriez observer :

| Opération | V1 | V2 | Gain |
|-----------|-----|-----|------|
| **Recherche pattern (10MB)** | ~850ms | ~14ms | **60x** |
| **Recherche pattern (100MB)** | ~8500ms | ~45ms | **189x** |
| **Recherche optimisée SIMD** | ~8500ms | ~1.4ms | **6000x** |
| **GetByte (accès séquentiel)** | ~100µs | ~6µs | **16x** |
| **Modifications multiples** | ~500ms | ~25ms | **20x** |

**Note** : Résultats sur processeur supportant AVX2. Gains variables selon CPU.

---

## 🔒 Compatibilité Binaire

### Attention : Formats de Fichiers

**V1 et V2 utilisent le MÊME format de données** :
- ✅ Les fichiers `.bin` sont compatibles
- ✅ Les fichiers `.tbl` (tables de caractères) sont compatibles
- ⚠️ Les fichiers d'état (SaveState) sont **incompatibles** entre V1 et V2

**Recommandation** : Ne pas partager les fichiers d'état entre V1 et V2.

---

## 📚 Ressources Supplémentaires

### Documentation
- [README.md](README.md) - Vue d'ensemble du projet
- [LEGACY_COMPATIBILITY_REPORT.md](LEGACY_COMPATIBILITY_REPORT.md) - Rapport de compatibilité complet
- [COMPATIBILITY_LAYER_FOUND.md](COMPATIBILITY_LAYER_FOUND.md) - Détails de la couche de compatibilité

### Tests
- [Phase1_DataRetrievalTests.cs](../Sources/WPFHexaEditor.Tests/Phase1_DataRetrievalTests.cs) - Exemples de tests unitaires

### Support
- GitHub Issues : [https://github.com/abbaye/WpfHexEditorIDE/issues](https://github.com/abbaye/WpfHexEditorIDE/issues)
- Email : derektremblay666@gmail.com

---

## ✅ Checklist Finale

Avant de déployer en production :

- [ ] Tous les fichiers XAML migrés
- [ ] Tous les fichiers C# migrés
- [ ] Événements mis à jour
- [ ] Tests manuels effectués
- [ ] Tests automatisés ajoutés (recommandé)
- [ ] Performance validée (devrait être meilleure)
- [ ] Documentation interne mise à jour
- [ ] Code review effectuée
- [ ] Commit : `git commit -m "Migrate from HexEditorLegacy (V1) to HexEditor (V2)"`
- [ ] Merge : `git merge migrate-to-hexeditor-v2`

---

## 🎉 Félicitations !

Votre migration est complète. Vous profitez maintenant de :
- 🚀 Performances améliorées (16-6000x)
- ✨ Nouvelles fonctionnalités (async, comparaison avancée)
- 🏗️ Architecture moderne et maintenable
- 📖 Support actif de la communauté

**Bon développement avec HexEditor V2 !** 🎈

---

**Auteur** : Derek Tremblay & Contributors
**Licence** : Apache 2.0
**Version** : 1.0 (2026-02-19)
