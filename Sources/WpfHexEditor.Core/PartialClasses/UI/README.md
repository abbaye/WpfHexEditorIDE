# PartialClasses/UI - Interface Utilisateur

Classes partielles pour **fonctionnalités UI et interaction utilisateur**.

## 📁 Contenu (5 fichiers)

| Fichier | Responsabilité | Méthodes Clés |
|---------|---------------|---------------|
| **Events.cs** | Gestion événements | Toutes les méthodes `Raise*Event` |
| **Clipboard.cs** | Presse-papiers | `CopyToClipboard()`, `PasteFromClipboard()` |
| **Highlights.cs** | Surbrillance | `HighlightByte()`, `ClearHighlights()` |
| **Zoom.cs** | Zoom | `ZoomIn()`, `ZoomOut()`, `ResetZoom()` |
| **UIHelpers.cs** | Utilitaires UI | `ScrollTo()`, `SetFocus()`, `UpdateVisual()` |

## 🎯 Méthodes

### 📋 Clipboard.cs

```csharp
// Copier en différents formats
public void CopyToClipboard(CopyPasteMode mode);
// Modes: HexString, CSharpCode, ASCIIString, TBLString

// Coller
public void PasteFromClipboard();

// Copier vers stream
public void CopyToStream(Stream stream);
```

### 🎨 Highlights.cs

```csharp
// Mettre en évidence une position
public void HighlightByte(long position, Color color);

// Mettre en évidence une plage
public void HighlightRange(long start, long length, Color color);

// Effacer les surbrillances
public void ClearHighlights();
public void ClearHighlight(long position);
```

### 🔍 Zoom.cs

```csharp
// Zoom
public void ZoomIn();    // +10%
public void ZoomOut();   // -10%
public void ResetZoom(); // 100%

// Zoom personnalisé
public double ZoomLevel { get; set; } // 0.5 à 3.0
```

### 📍 UIHelpers.cs

```csharp
// Scroll
public void ScrollTo(long position);
public void ScrollToEnd();

// Focus
public void SetFocus();

// Refresh
public void RefreshView();
public void InvalidateVisual();
```

## 💡 Exemples

**Copier/coller:**
```csharp
// Sélectionner et copier
hexEditor.SelectionStart = 0x100;
hexEditor.SelectionStop = 0x1FF;
hexEditor.CopyToClipboard(CopyPasteMode.HexString);

// Coller
hexEditor.SetPosition(0x500);
hexEditor.PasteFromClipboard();
```

**Surbrillance:**
```csharp
// Mettre en évidence les octets modifiés
hexEditor.HighlightRange(0x100, 256, Colors.Orange);

// Mettre en évidence les résultats de recherche
foreach (var pos in searchResults)
    hexEditor.HighlightByte(pos, Colors.Yellow);
```

**Zoom:**
```csharp
// Zoom in/out
hexEditor.ZoomIn();
hexEditor.ZoomOut();
hexEditor.ZoomLevel = 1.5;  // 150%
```

**Navigation:**
```csharp
// Aller à une position
hexEditor.ScrollTo(0x1000);
hexEditor.SetPosition(0x1000);
hexEditor.SetFocus();
```

## 🔗 Ressources

- **[PartialClasses/README.md](../README.md)** - Vue d'ensemble
- **[Controls/README.md](../../Controls/README.md)** - HexViewport

---

✨ Documentation par Derek Tremblay et Claude Sonnet 4.5
