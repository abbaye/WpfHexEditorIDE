# PartialClasses/Features - Fonctionnalités Avancées

Classes partielles contenant les **fonctionnalités avancées** optionnelles du HexEditor.

## 📁 Contenu (5 fichiers)

| Fichier | Fonctionnalité | Méthodes Clés |
|---------|----------------|---------------|
| **Bookmarks.cs** | Signets | `AddBookmark()`, `RemoveBookmark()`, `ClearBookmarks()` |
| **CustomBackgroundBlocks.cs** | Surbrillance | `SetHighlight()`, `ClearHighlight()` |
| **FileComparison.cs** | Comparaison | `CompareFiles()`, `GetDifferences()` |
| **StatePersistence.cs** | État | `SaveState()`, `LoadState()` |
| **TBL.cs** | Tables caractères | `LoadTBL()`, `UnloadTBL()` |

## 🎯 Fonctionnalités

### 🔖 Bookmarks
```csharp
hexEditor.AddBookmark(0x1000, "Header");
hexEditor.AddBookmark(0x5000, "Data");
var bookmarks = hexEditor.GetBookmarks();
```

### 🎨 Custom Background Blocks
```csharp
hexEditor.SetHighlight(0x100, 256, Colors.Yellow, "Modified section");
hexEditor.ClearHighlight(0x100);
```

### 🔍 File Comparison
```csharp
var diffs = hexEditor.CompareFiles("file1.bin", "file2.bin");
foreach (var diff in diffs)
{
    Console.WriteLine($"Diff at 0x{diff.Position:X}: {diff.OldByte:X2} → {diff.NewByte:X2}");
}
```

### 💾 State Persistence
```csharp
// Sauvegarder l'état
var state = hexEditor.SaveState();
File.WriteAllText("state.json", JsonSerializer.Serialize(state));

// Restaurer l'état
var state = JsonSerializer.Deserialize<EditorState>(File.ReadAllText("state.json"));
hexEditor.LoadState(state);
```

### 📖 TBL (Character Tables)
```csharp
// Pour éditeurs de ROMs de jeux rétro
hexEditor.LoadTBL("game.tbl");
// Les octets sont affichés avec les caractères du jeu
hexEditor.UnloadTBL();
```

## 🔗 Ressources

- **[PartialClasses/README.md](../README.md)** - Vue d'ensemble
- **[Core/CharacterTable/README.md](../../Core/CharacterTable/README.md)** - TBL

---

✨ Documentation par Derek Tremblay et Claude Sonnet 4.5
