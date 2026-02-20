# PartialClasses/Compatibility - Couche de Compatibilité V1

Classes partielles pour **compatibilité V1 API** - wrappers qui exposent l'ancienne API V1 tout en utilisant la nouvelle implémentation V2.

## 📁 Contenu

| Fichier | Description | Lignes |
|---------|-------------|--------|
| **HexEditor.CompatibilityLayer.Properties.cs** | Propriétés V1 | ~800 |
| **HexEditor.CompatibilityLayer.Methods.cs** | Méthodes V1 | ~1200 |

## 🎯 Objectif

Permettre une migration **zéro code** de V1 vers V2 en maintenant 100% de l'API publique V1.

## 📊 Propriétés V1 Wrappées

```csharp
// Ancien nom V1 → Nouveau nom V2
public bool ReadOnlyMode { get; set; }      → IsReadOnly
public string FileName { get; set; }        → (ByteProvider.FileName)
public long SelectionStart { get; set; }    → (inchangé)
public long SelectionLength { get; set; }   → (calculé depuis SelectionStop)
```

## 🔧 Méthodes V1 Wrappées

```csharp
// API V1 maintenue
public void SubmitChanges()                 → Save()
public byte GetByte(long position)          → GetByteAt()
public void DeleteSelection()               → DeleteBytes(SelectionStart, SelectionLength)
```

## 💡 Exemple de Migration

**Ancien code V1 (fonctionne toujours):**
```csharp
hexEditor.FileName = "data.bin";
hexEditor.SelectionLength = 100;
hexEditor.SubmitChanges();
```

**Nouveau code V2 (recommandé):**
```csharp
hexEditor.FileName = "data.bin";
hexEditor.SelectionStop = hexEditor.SelectionStart + 99;
hexEditor.Save();
```

## 🔗 Ressources

- **[PartialClasses/README.md](../README.md)** - Vue d'ensemble
- **[docs/migration/MIGRATION.md](../../../docs/migration/MIGRATION.md)** - Guide complet

---

✨ Documentation par Derek Tremblay et Claude Sonnet 4.5
