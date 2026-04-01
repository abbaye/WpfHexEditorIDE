# WpfHexEditor.Editor.TileEditor

Tile graphics editor implementing `IDocumentEditor` + `IOpenableDocument`.
Displays raw tile data in a configurable grid (width, height, bpp) with palette support.

## Supported file types

`.chr`, `.til`, `.gfx`

## Status

**Stub** — structure and factory registered; tile rendering engine and palette editor to be implemented in a future sprint.

## Standalone usage

```csharp
EditorRegistry.Instance.Register(new TileEditorFactory());

var editor = new TileEditor();
await editor.OpenAsync("path/to/sprites.chr");
myGrid.Children.Add(editor);
```

## License

GNU Affero General Public License v3.0 — see repository root.
