# PartialClasses/Core - Opérations Essentielles

Classes partielles contenant les **opérations core essentielles** pour le fonctionnement du HexEditor.

## 📁 Contenu (7 fichiers)

| Fichier | Responsabilité | Méthodes Clés |
|---------|---------------|---------------|
| **FileOperations.cs** | Fichier I/O | `Open()`, `Save()`, `SaveAs()`, `Close()` |
| **StreamOperations.cs** | Stream/Mémoire | `OpenStream()`, `OpenMemory()` |
| **ByteOperations.cs** | Manipulation octets | `GetByte()`, `ModifyByte()`, `InsertByte()`, `DeleteBytes()` |
| **EditOperations.cs** | Édition | `Undo()`, `Redo()`, `Copy()`, `Cut()`, `Paste()`, `Clear*()` |
| **BatchOperations.cs** | Mode batch | `BeginBatch()`, `EndBatch()` |
| **AsyncOperations.cs** | Opérations async | `SaveAsync()`, `LoadAsync()` |
| **Diagnostics.cs** | Diagnostics | `GetDiagnostics()`, `GetCacheStatistics()` |

## 🎯 Méthodes Principales

### FileOperations.cs
```csharp
public void Open(string fileName)
public void Save()
public void SaveAs(string fileName)
public void Close()
```

### ByteOperations.cs
```csharp
public byte GetByteAt(VirtualPosition position)
public void ModifyByte(byte value, long position)
public void InsertBytes(long position, byte[] data)
public void DeleteBytes(long startPosition, long length)
```

### EditOperations.cs
```csharp
public void Undo()
public void Redo()
public void Copy()
public void Cut()
public void Paste()
public void ClearModifications()
public void ClearInsertions()
public void ClearDeletions()
```

## 💡 Exemples

**Opérations de base:**
```csharp
// Ouvrir fichier
hexEditor.Open("data.bin");

// Modifier octet
hexEditor.ModifyByte(0xFF, 0x100);

// Sauvegarder
hexEditor.Save();
```

**Mode batch (3x plus rapide):**
```csharp
hexEditor.BeginBatch();
for (long i = 0; i < 1000; i++)
    hexEditor.ModifyByte((byte)(i % 256), i);
hexEditor.EndBatch();
```

## 🔗 Ressources

- **[PartialClasses/README.md](../README.md)** - Vue d'ensemble
- **[Core/Bytes/README.md](../../Core/Bytes/README.md)** - ByteProvider

---

✨ Documentation par Derek Tremblay et Claude Sonnet 4.5
