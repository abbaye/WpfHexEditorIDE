# WpfHexEditor.Editor.ScriptEditor

Game script and dialogue editor implementing `IDocumentEditor` + `IOpenableDocument`.
Built on top of the TextEditor engine with TBL encoding support for Japanese game scripts.

## Supported file types

`.scr`, `.msg`, `.evt`, `.script`, `.dec`

## Status

**Stub** — structure and factory registered; TBL encoding integration and line-length validation to be implemented in a future sprint.

## Standalone usage

```csharp
EditorRegistry.Instance.Register(new ScriptEditorFactory());

var editor = new ScriptEditor();
await editor.OpenAsync("path/to/dialogue.msg");
myGrid.Children.Add(editor);
```

## License

GNU Affero General Public License v3.0 — see repository root.
