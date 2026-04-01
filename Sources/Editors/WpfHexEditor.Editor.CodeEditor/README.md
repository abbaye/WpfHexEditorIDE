# WpfHexEditor.Editor.CodeEditor

Multi-language code and structured-text editor for [WpfHexEditor](https://github.com/abbaye/WpfHexEditorIDE) with syntax highlighting driven by the `LanguageRegistry` and a split-view host.

## Features

- **Full `IDocumentEditor` + `IOpenableDocument` integration** (Undo/Redo, Save, Dirty tracking, Commands)
- **Split-view** — togglable horizontal split; both panes share the same `CodeDocument`
- **Syntax highlighting** built from `.whlang` / `LanguageDefinition` rule sets via `SyntaxRuleHighlighter`
- **SmartComplete popup** (`SmartCompletePopup`) with completion candidates
- **Snippet support** — `SnippetManager` injects snippets from the active language definition
- **Gutter** — line numbers via `GutterControl`
- **Format-script editor** (`FormatScriptEditorControl`) for `.whfmt` scripting files
- **Properties panel** integration (`IPropertyProvider`) — surfaces line count, byte size via F4
- **`IEditorFactory`** registration for `.json`, `.whfmt`, `.whjson`, `.whlang`, and any extension mapped in the embedded format catalog or `LanguageRegistry`

## Standalone Usage

```csharp
using WpfHexEditor.Editor.CodeEditor;
using WpfHexEditor.Editor.Core;

// Register with the editor registry (enables open-by-extension)
EditorRegistry.Instance.Register(new CodeEditorFactory());

// Or create directly and load a file:
var host = new CodeEditorSplitHost();
await ((IOpenableDocument)host).OpenAsync("path/to/config.json");
myGrid.Children.Add(host);
```

## License

GNU Affero General Public License v3.0
