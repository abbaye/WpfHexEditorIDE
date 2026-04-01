# WpfHexEditor.Editor.DisassemblyViewer

Multi-architecture disassembly viewer implementing `IDocumentEditor` + `IOpenableDocument`.

## Supported file types

`.exe`, `.dll`, `.elf`, `.so`, `.bin`, `.rom`, `.gb`, `.gba`, `.nes`, `.class`, `.wasm`

## Status

**Stub** — structure and factory registered; disassembly engine (Iced / Capstone.NET) to be wired in a future sprint.

## Standalone usage

```csharp
EditorRegistry.Instance.Register(new DisassemblyViewerFactory());

var editor = new DisassemblyViewer();
await editor.OpenAsync("path/to/target.exe");
myGrid.Children.Add(editor);
```

## License

GNU Affero General Public License v3.0 — see repository root.
