# Models - Classes de Modèles de Données

Classes de données pour l'état de l'éditeur et le rendu.

## 📁 Contenu (6 fichiers)

| Fichier | Description |
|---------|-------------|
| **EditMode.cs** | Enum modes d'édition (Insert, Overwrite) |
| **EditorState.cs** | État de l'éditeur (sérialisable) |
| **HexBoxState.cs** | État du HexBox |
| **HexLine.cs** | Ligne de données + classe ByteData |
| **Position.cs** | Structure de position |
| **ProgressRefreshRate.cs** | Enum taux de rafraîchissement |

## 🎯 Classes Principales

### HexLine.cs + ByteData

```csharp
// Ligne affichée dans le viewport
public class HexLine
{
    public long LineNumber { get; set; }
    public ObservableCollection<ByteData> Bytes { get; set; }
}

// Donnée d'un octet
public class ByteData
{
    public VirtualPosition VirtualPos { get; set; }
    public byte Value { get; set; }
    public ByteAction Action { get; set; }  // None, Modified, Inserted, Deleted
}
```

### EditorState.cs

```csharp
// État sérialisable
public class EditorState
{
    public string FileName { get; set; }
    public long CursorPosition { get; set; }
    public long SelectionStart { get; set; }
    public long SelectionStop { get; set; }
    public List<Bookmark> Bookmarks { get; set; }
    public double ZoomLevel { get; set; }
}
```

### Position.cs

```csharp
// Position virtuelle (avec insertions/deletions)
public struct VirtualPosition
{
    public long Value { get; set; }
    public bool IsValid { get; }
}

// Position physique (dans le fichier réel)
public struct PhysicalPosition
{
    public long Value { get; set; }
    public bool IsValid { get; }
}
```

## 💡 Exemples

```csharp
// Créer une ligne
var line = new HexLine
{
    LineNumber = 0,
    Bytes = new ObservableCollection<ByteData>()
};

for (int i = 0; i < 16; i++)
{
    line.Bytes.Add(new ByteData
    {
        VirtualPos = new VirtualPosition(i),
        Value = 0xFF,
        Action = ByteAction.None
    });
}

// Sauvegarder l'état
var state = new EditorState
{
    FileName = hexEditor.FileName,
    CursorPosition = hexEditor.CursorPosition
};
File.WriteAllText("state.json", JsonSerializer.Serialize(state));
```

## 🔗 Ressources

- **[Controls/README.md](../Controls/README.md)** - HexViewport utilise HexLine
- **[ViewModels/README.md](../ViewModels/README.md)** - ViewModels utilisent ces modèles

---

✨ Documentation par Derek Tremblay et Claude Sonnet 4.5
