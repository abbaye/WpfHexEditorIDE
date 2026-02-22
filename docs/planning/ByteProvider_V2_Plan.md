# Plan de Refactorisation ByteProvider pour V2

---
> **✅ PLAN COMPLETED**
>
> ByteProvider V2 refactoring successfully implemented.
> **Key achievements:** Virtual/Physical separation, proper insert support, 10-100x performance
>
> This document preserved for architectural decision history.
---

## Problèmes Actuels avec ByteProvider (Legacy V1)

### 1. Confusion Virtual/Physical Positions
- **Problème**: ByteProvider mélange positions virtuelles et physiques
- `_byteModifiedDictionary` stocke par position (mais quelle position? virtuelle ou physique?)
- `AddByteAdded(byte, long position)` - la position est ambiguë
- `GetByte(long position)` - position physique, mais vérifie le dictionnaire qui peut avoir des positions virtuelles

### 2. Gestion Inadéquate des Insertions
- **Case ByteAction.Added commenté** (ligne 854 de GetCopyData):
  ```csharp
  //case ByteAction.Added: //TODO : IMPLEMENTING ADD BYTE
  //    break;
  ```
- ByteProvider ne peut pas lire correctement les bytes insérés
- Dictionary<long, ByteModified> ne peut stocker qu'UNE modification par position
- Impossible de gérer plusieurs insertions contiguës (positions virtuelles 177, 178, 179...)

### 3. Architecture Monolithique
- ByteProvider fait trop de choses:
  - Gestion du fichier (Stream)
  - Tracking des modifications (Modified, Added, Deleted)
  - Undo/Redo (UndoStack, RedoStack)
  - Copy/Paste
  - Mapping positions
- Pas de séparation des responsabilités (SOLID violation)

### 4. Performance
- Pas de caching intelligent des bytes lus
- GetCopyData parcourt le dictionnaire à chaque lecture
- Pas de lecture par batch optimisée

## Architecture Proposée pour ByteProvider V2

### Principe: Séparation des Responsabilités

```
┌─────────────────────────────────────────────────────────┐
│           HexEditorViewModel (Orchestrator)             │
│  - Coordonne les services                               │
│  - Gère la logique métier                               │
│  - Convertit Virtual ↔ Physical positions               │
└─────────────────────────────────────────────────────────┘
           │              │              │              │
           ▼              ▼              ▼              ▼
    ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐
    │  File    │  │  Edits   │  │  Undo/   │  │ Position │
    │ Provider │  │ Manager  │  │  Redo    │  │  Mapper  │
    └──────────┘  └──────────┘  └──────────┘  └──────────┘
```

### Composants

#### 1. **FileProvider** (Gestion fichier uniquement)
```csharp
public class FileProvider : IDisposable
{
    private Stream _stream;
    private readonly byte[] _cache; // Cache de lecture

    // Lecture de bytes du fichier (SEULEMENT positions physiques)
    public byte ReadByte(long physicalPosition);
    public byte[] ReadBytes(long physicalPosition, int count);

    // Écriture dans le fichier (pour Save)
    public void WriteBytes(long physicalPosition, byte[] data);

    // Métadonnées
    public long Length { get; }
    public bool IsOpen { get; }
}
```

**Responsabilité unique**: Lire/écrire bytes depuis/vers le fichier.

#### 2. **EditsManager** (Tracking des modifications)
```csharp
public class EditsManager
{
    // Trois dictionnaires séparés pour chaque type de modification
    private Dictionary<long, byte> _modifiedBytes;    // Physical pos → byte
    private Dictionary<long, InsertedByte[]> _insertedBytes; // Physical pos → array of inserted bytes
    private HashSet<long> _deletedPositions;          // Physical positions deleted

    // Modifications
    public void ModifyByte(long physicalPosition, byte value);
    public void InsertBytes(long physicalPosition, byte[] bytes);
    public void DeleteByte(long physicalPosition);

    // Queries
    public bool IsModified(long physicalPosition);
    public bool IsDeleted(long physicalPosition);
    public InsertedByte[] GetInsertedBytesAt(long physicalPosition);

    // Clear
    public void ClearAll();
}

public struct InsertedByte
{
    public byte Value;
    public long VirtualOffset; // Offset from physical position
}
```

**Responsabilité unique**: Tracker les modifications (Modified, Added, Deleted).

#### 3. **PositionMapper** (Conversion Virtual ↔ Physical)
```csharp
public class PositionMapper
{
    private EditsManager _editsManager;

    // Conversion avec cache pour performance
    private Dictionary<long, long> _virtualToPhysicalCache;
    private Dictionary<long, long> _physicalToVirtualCache;

    public long VirtualToPhysical(long virtualPosition);
    public long PhysicalToVirtual(long physicalPosition);

    // Total length incluant insertions
    public long GetVirtualLength(long physicalLength);

    // Invalidation du cache quand les éditions changent
    public void InvalidateCache();
}
```

**Responsabilité unique**: Mapper positions virtuelles ↔ physiques.

#### 4. **UndoRedoService** (Déjà existe, adapter pour nouvelles structures)
```csharp
public interface IEdit
{
    void Apply(EditsManager edits);
    void Undo(EditsManager edits);
    long Position { get; }
}

public class ModifyByteEdit : IEdit { ... }
public class InsertBytesEdit : IEdit { ... }
public class DeleteByteEdit : IEdit { ... }

public class UndoRedoService
{
    private Stack<IEdit> _undoStack;
    private Stack<IEdit> _redoStack;

    public void RecordEdit(IEdit edit);
    public void Undo();
    public void Redo();
}
```

**Responsabilité unique**: Gérer Undo/Redo des éditions.

#### 5. **ByteReader** (Service de lecture intelligent)
```csharp
public class ByteReader
{
    private FileProvider _fileProvider;
    private EditsManager _editsManager;
    private PositionMapper _positionMapper;

    // Lecture d'un byte à une position VIRTUELLE
    public byte GetByte(long virtualPosition)
    {
        var physicalPos = _positionMapper.VirtualToPhysical(virtualPosition);

        // Check if inserted byte at this virtual position
        var insertedBytes = _editsManager.GetInsertedBytesAt(physicalPos);
        foreach (var inserted in insertedBytes)
        {
            if (physicalPos + inserted.VirtualOffset == virtualPosition)
                return inserted.Value;
        }

        // Check if deleted
        if (_editsManager.IsDeleted(physicalPos))
            return 0; // Or throw exception

        // Check if modified
        if (_editsManager.IsModified(physicalPos))
            return _editsManager.GetModifiedByte(physicalPos);

        // Read from file
        return _fileProvider.ReadByte(physicalPos);
    }

    // Lecture par batch pour performance
    public byte[] GetBytes(long virtualPosition, int count);
}
```

**Responsabilité unique**: Lire bytes en tenant compte des éditions.

## Migration Strategy

### Phase 1: Créer les Nouveaux Services (Parallèle à l'ancien)
1. Créer `FileProvider` - wrapper simple autour du Stream
2. Créer `EditsManager` - nouvelle structure pour tracking
3. Créer `PositionMapper` - extraction de la logique existante
4. Créer `ByteReader` - service de lecture unifié

### Phase 2: Adapter HexEditorViewModel
1. Remplacer `_insertedBytes` dictionary par `EditsManager`
2. Utiliser `ByteReader` au lieu de `_provider.GetByte()`
3. Utiliser `PositionMapper` pour Virtual↔Physical
4. Adapter `UndoRedoService` pour utiliser `EditsManager`

### Phase 3: Adapter ByteProvider Legacy (Compatibilité V1)
1. Faire ByteProvider wrapper les nouveaux services
2. Garder l'interface publique pour V1
3. Déléguer aux nouveaux services en interne

### Phase 4: Tests et Validation
1. Tests unitaires pour chaque service
2. Tests d'intégration avec HexEditorV2
3. Tests de compatibilité avec V1 samples

## Avantages de la Nouvelle Architecture

### 1. Séparation des Responsabilités ✅
- Chaque service a UNE responsabilité claire
- Testable indépendamment
- Maintenable

### 2. Performance ✅
- `ByteReader` peut cacher les bytes lus
- `PositionMapper` cache les conversions
- Lecture par batch optimisée

### 3. Clarté Virtual/Physical ✅
- `FileProvider` → toujours PHYSICAL
- `ByteReader` → toujours VIRTUAL
- `PositionMapper` → conversion explicite

### 4. Gestion Correcte des Insertions ✅
- `EditsManager` stocke insertions séparément
- Support de multiples insertions à la même position physique
- Pas de confusion avec le dictionnaire de modifications

### 5. Extensibilité ✅
- Facile d'ajouter de nouveaux types d'éditions
- Facile d'ajouter du caching
- Facile de changer la stratégie de stockage (ex: fichier temporaire pour les éditions)

## Estimation d'Effort

- **Phase 1 (Nouveaux Services)**: 6-8 heures
- **Phase 2 (Adapter ViewModel)**: 4-5 heures
- **Phase 3 (Wrapper Legacy)**: 2-3 heures
- **Phase 4 (Tests)**: 4-5 heures

**Total**: 16-21 heures

## Notes Importantes

### Compatibilité V1
- ByteProvider legacy reste pour V1
- Wrapper interne utilise les nouveaux services
- API publique inchangée

### Performance
- Caching agressif dans ByteReader et PositionMapper
- Invalidation de cache seulement quand nécessaire
- Lecture par batch pour réduire les appels Stream

### Memory Management
- FileProvider dispose du Stream
- EditsManager peut limiter la taille des dictionnaires
- Option de flush vers fichier temporaire si trop de modifications

## Exemple de Code Complet

```csharp
// Dans HexEditorViewModel
public class HexEditorViewModel
{
    private FileProvider _fileProvider;
    private EditsManager _editsManager;
    private PositionMapper _positionMapper;
    private ByteReader _byteReader;
    private UndoRedoService _undoRedo;

    public void InsertByte(long virtualPosition, byte value)
    {
        var physicalPos = _positionMapper.VirtualToPhysical(virtualPosition);

        // Create edit
        var edit = new InsertBytesEdit(physicalPos, new[] { value });

        // Apply
        edit.Apply(_editsManager);

        // Record for undo
        _undoRedo.RecordEdit(edit);

        // Invalidate caches
        _positionMapper.InvalidateCache();
        ClearLineCache();
        RefreshVisibleLines();
    }

    private ByteData CreateByteData(long virtualPosition)
    {
        var byte = _byteReader.GetByte(virtualPosition);
        var physicalPos = _positionMapper.VirtualToPhysical(virtualPosition);

        ByteAction action = ByteAction.Nothing;
        if (_editsManager.IsModified(physicalPos))
            action = ByteAction.Modified;
        else if (_editsManager.GetInsertedBytesAt(physicalPos).Any(b => physicalPos + b.VirtualOffset == virtualPosition))
            action = ByteAction.Added;
        else if (_editsManager.IsDeleted(physicalPos))
            action = ByteAction.Deleted;

        return new ByteData
        {
            VirtualPos = virtualPosition,
            PhysicalPos = physicalPos,
            Value = value,
            Action = action,
            IsSelected = IsByteSelected(virtualPosition),
            IsCursor = IsCursor(virtualPosition)
        };
    }
}
```

## Conclusion

Cette refactorisation résoudra tous les problèmes architecturaux actuels et donnera une base solide pour V2 avec:
- **Clarté** des positions virtual/physical
- **Performance** avec caching intelligent
- **Maintenabilité** avec séparation des responsabilités
- **Extensibilité** pour futures fonctionnalités

**Recommandation**: Implémenter après avoir terminé la compatibilité V1 de base (Phases 1-9 du plan actuel).
