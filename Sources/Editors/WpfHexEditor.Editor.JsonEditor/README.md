# WpfHexEditor.Editor.JsonEditor

Dedicated JSON and JSONC editor for [WpfHexEditor](https://github.com/abbaye/WpfHexEditorIDE) with syntax highlighting, structural validation, and IDE integration.

> **Status: planned / stub** — the project file exists and dependencies are resolved, but source files have not yet been committed. JSON files are currently handled by `WpfHexEditor.Editor.CodeEditor` (`.json`, `.whjson`, `.whfmt`).

## Planned Features

- **Dedicated `IDocumentEditor` + `IOpenableDocument`** for `.json` and `.jsonc` files
- **JSON/JSONC syntax highlighting** — comments, string literals, property keys, numbers, booleans, null
- **Structural validation** — real-time parse error diagnostics surfaced via `IDiagnosticSource`
- **JSON path display** — caret-aware JSON path shown in the status bar
- **Format / Minify** — pretty-print and minify commands in the editor toolbar
- **`IEditorFactory`** registration for `.json` and `.jsonc`

## Dependencies (from project.assets.json)

- `WpfHexEditor.Editor.Core`
- `WpfHexEditor.Core`
- `WpfHexEditor.Definitions`
- `WpfHexEditor.HexEditor`
- `WpfHexEditor.BinaryAnalysis`

## License

GNU Affero General Public License v3.0
