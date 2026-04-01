# WpfHexEditor.Editor.DiffViewer

Side-by-side binary diff viewer for [WpfHexEditor](https://github.com/abbaye/WpfHexEditorIDE), comparing two files at the byte level using structured diff regions.

## Features

- **`IDocumentEditor` + `IOpenableDocument`** integration — read-only, no dirty state
- **`FileDiffService`** backend — produces structured `FileDifference` regions with `Modified`, `AddedInSecond`, and `DeletedInSecond` semantics
- **Side-by-side hex canvas rendering** — up to 2000 rows rendered; color-coded highlights (red = modified, green = added, amber = deleted)
- **Diff statistics chips** — Modified / Added / Deleted region counts and similarity percentage
- **DataGrid diff list** — all regions listed with offset, type, length, and a hex byte preview for each side
- **Keyboard navigation** — Previous / Next diff buttons scroll both panes in sync
- **`CompareAsync(leftPath, rightPath)`** public API for programmatic use
- **`IEditorFactory`** registration — opened from `Tools > Compare Files…`, not by file extension

## Standalone Usage

```csharp
using WpfHexEditor.Editor.DiffViewer;
using WpfHexEditor.Editor.DiffViewer.Controls;

// Register the factory (required for IDE integration)
EditorRegistry.Instance.Register(new DiffViewerFactory());

// Or create and compare directly:
var viewer = new DiffViewer();
await viewer.CompareAsync("original.bin", "modified.bin");
myGrid.Children.Add(viewer);
```

## License

GNU Affero General Public License v3.0
