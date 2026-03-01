# WpfHexEditor.Editor.StructureEditor

Binary structure editor implementing `IDocumentEditor` + `IOpenableDocument`.
Loads a `.whfmt` format definition and displays an editable field tree synchronized with the active HexEditor.

## Supported file types

`.whfmt`

## Status

**Stub** — structure and factory registered; field tree rendering and bidirectional sync with HexEditor to be implemented in a future sprint.

## Standalone usage

```csharp
EditorRegistry.Instance.Register(new StructureEditorFactory());

var editor = new StructureEditorControl();
await editor.OpenAsync("path/to/format.whfmt");
myGrid.Children.Add(editor);
```

## License

Apache 2.0 — see repository root.
