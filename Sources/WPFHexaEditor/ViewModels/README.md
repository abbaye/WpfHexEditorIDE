# ViewModels - ViewModels pour Architecture MVVM

ViewModels pour architecture MVVM du HexEditor.

## 📁 Contenu (5 fichiers)

| Fichier | Description |
|---------|-------------|
| **HexEditorViewModel.cs** | ViewModel principal du HexEditor |
| **HexBoxViewModel.cs** | ViewModel du HexBox |
| **ProgressOverlayViewModel.cs** | ViewModel de progression |
| **GiveByteViewModel.cs** | ViewModel input dialog |
| **ReplaceByteViewModel.cs** | ViewModel remplacement simple |

## 🎯 ViewModels Principaux

### HexEditorViewModel.cs

```csharp
public class HexEditorViewModel : INotifyPropertyChanged
{
    // Données
    public ObservableCollection<HexLine> VisibleLines { get; set; }
    public long VirtualLength { get; set; }

    // Position et sélection
    public long CursorPosition { get; set; }
    public long SelectionStart { get; set; }
    public long SelectionStop { get; set; }

    // État
    public bool IsModified { get; set; }
    public bool IsReadOnly { get; set; }

    // Méthodes
    public byte GetByteAt(VirtualPosition pos);
    public void UpdateVisibleLines();
}
```

### HexBoxViewModel.cs

```csharp
public class HexBoxViewModel : INotifyPropertyChanged
{
    public int BytesPerLine { get; set; } = 16;
    public long FirstVisibleLine { get; set; }
    public int VisibleLineCount { get; set; }
    public bool ShowOffset { get; set; }
    public bool ShowAscii { get; set; }
}
```

### ProgressOverlayViewModel.cs

```csharp
public class ProgressOverlayViewModel : INotifyPropertyChanged
{
    public bool IsVisible { get; set; }
    public int ProgressPercentage { get; set; }
    public string StatusMessage { get; set; }
    public ICommand CancelCommand { get; set; }
    public CancellationTokenSource CancellationTokenSource { get; set; }
}
```

## 💡 Exemples

```csharp
// Utiliser le ViewModel
var viewModel = new HexEditorViewModel
{
    ByteProvider = byteProvider
};

// Binding
hexViewport.DataContext = viewModel;
hexViewport.LinesSource = viewModel.VisibleLines;

// Mettre à jour
viewModel.CursorPosition = 0x1000;
viewModel.UpdateVisibleLines();
```

## 🔗 Ressources

- **[Models/README.md](../Models/README.md)** - HexLine, EditorState
- **[Controls/README.md](../Controls/README.md)** - Controls qui utilisent ces ViewModels
- **[SearchModule/ViewModels/README.md](../SearchModule/ViewModels/README.md)** - SearchViewModel

---

✨ Documentation par Derek Tremblay et Claude Sonnet 4.5
