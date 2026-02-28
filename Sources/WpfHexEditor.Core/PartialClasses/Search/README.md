# PartialClasses/Search - Recherche et Remplacement

Classes partielles pour **opérations de recherche/remplacement**.

## 📁 Contenu (2 fichiers)

| Fichier | Responsabilité | Méthodes Clés |
|---------|---------------|---------------|
| **Search.cs** | Recherche | `FindFirst()`, `FindNext()`, `FindAll()`, `CountOccurrences()` |
| **FindReplace.cs** | Remplacement | `ReplaceFirst()`, `ReplaceNext()`, `ReplaceAll()` |

## 🎯 Méthodes

### 🔍 Search.cs

```csharp
// Trouver premier
public long FindFirst(byte[] pattern);
public Task<long> FindFirstAsync(byte[] pattern, IProgress<int> progress);

// Trouver suivant/précédent
public long FindNext();
public long FindPrevious();

// Trouver tout
public List<long> FindAll(byte[] pattern);
public Task<List<long>> FindAllAsync(byte[] pattern, IProgress<int> progress);

// Compter occurrences
public int CountOccurrences(byte[] pattern);
```

### 🔄 FindReplace.cs

```csharp
// Remplacer premier
public bool ReplaceFirst(byte[] findPattern, byte[] replacePattern);

// Remplacer suivant
public bool ReplaceNext(byte[] findPattern, byte[] replacePattern);

// Remplacer tout
public int ReplaceAll(byte[] findPattern, byte[] replacePattern);
public Task<int> ReplaceAllAsync(byte[] find, byte[] replace, IProgress<int> progress);
```

## 💡 Exemples

**Recherche simple:**
```csharp
var pattern = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"
long pos = hexEditor.FindFirst(pattern);
if (pos >= 0)
    hexEditor.SetPosition(pos, pattern.Length);
```

**Recherche tout avec progression:**
```csharp
var progress = new Progress<int>(p => progressBar.Value = p);
var results = await hexEditor.FindAllAsync(pattern, progress);
Console.WriteLine($"Trouvé {results.Count} occurrences");
```

**Remplacement:**
```csharp
var find = new byte[] { 0x4F, 0x4C, 0x44 };      // "OLD"
var replace = new byte[] { 0x4E, 0x45, 0x57 };   // "NEW"
int count = hexEditor.ReplaceAll(find, replace);
Console.WriteLine($"{count} remplacements effectués");
```

## 🔗 Ressources

- **[PartialClasses/README.md](../README.md)** - Vue d'ensemble
- **[SearchModule/README.md](../../SearchModule/README.md)** - Nouveau module v2.2+
- **[Services/README.md](../../Services/README.md)** - FindReplaceService

---

✨ Documentation par Derek Tremblay et Claude Sonnet 4.5
