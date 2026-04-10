# WpfCodeEditorControl

A full-featured WPF code editor UserControl for .NET 8. Built as part of the [WpfHexEditorControl](https://github.com/abbaye/WpfHexEditorControl) project.

## What's New in 0.9.6.4

- **Fix**: LSP inlay hints and declaration hints (code lens) no longer render as ghost overlays on top of code text. Hints now correctly align with the text area origin, accounting for gutter offset, top margin, and horizontal scroll.
- **Fix**: ReadOnly mode no longer blocks text selection and caret placement. Only text modification is blocked — selection (Shift+Arrow, drag), caret click, copy (Ctrl+C), and select all (Ctrl+A) now work as expected.

## Quick Start

### CodeEditorSplitHost (full-featured: syntax highlighting, LSP, folding...)

```xml
<Window xmlns:ce="clr-namespace:WpfHexEditor.Editor.CodeEditor.Controls;assembly=WpfHexEditor.Editor.CodeEditor">
    <ce:CodeEditorSplitHost x:Name="Editor" />
</Window>
```

```csharp
// Load a file
Editor.PrimaryEditor.LoadText(File.ReadAllText("Program.cs"));
Editor.SetLanguage(languageDefinition);
```

```
dotnet add package WpfCodeEditorControl
```

### TextEditor (lightweight, plain text)

```xml
<Window xmlns:te="clr-namespace:WpfHexEditor.Editor.TextEditor;assembly=WpfHexEditor.Editor.TextEditor">
    <te:TextEditor x:Name="TextEdit" />
</Window>
```

```csharp
// Load plain text
TextEdit.LoadText(File.ReadAllText("notes.txt"));

// Read back
string content = TextEdit.Text;
```

The `TextEditor` control is a lightweight plain-text editor included in the package. Use `CodeEditorSplitHost` when you need syntax highlighting, folding, and LSP support.

## Features

### Editing
- Multi-caret editing with Ctrl+Click
- Smart auto-complete with context-aware suggestions
- Code snippets with tab expansion
- Block selection (Alt+Drag)
- Auto-indent and smart brace matching
- Undo/redo with coalescence

### Syntax & Languages
- 400+ language definitions via .whfmt format
- Syntax highlighting with customizable themes
- Code folding (regions, braces, tags)
- End-of-block hover hints

### Navigation
- Line numbers with configurable gutter
- Minimap overview
- Go to line (Ctrl+G)
- Breadcrumb navigation bar
- Bookmark support

### Search
- Find and replace with regex support
- Search highlighting across document
- Match case / whole word options

### Advanced
- LSP (Language Server Protocol) integration
- Split view (horizontal/vertical)
- Diagnostic markers (errors, warnings)
- Scroll marker panel
- Column guides
- Word wrap
- Read-only mode

### Settings
- Built-in settings panel with auto-generated UI
- JSON-based settings persistence (export/import)
- Full DependencyProperty API for programmatic control

## Included Assemblies

All bundled inside the package — zero external NuGet dependencies:

| Assembly | Purpose |
|----------|---------|
| WpfHexEditor.Editor.CodeEditor | CodeEditorSplitHost UserControl (main entry point) |
| WpfHexEditor.Core | Settings infrastructure, format detection, services |
| WpfHexEditor.Core.BinaryAnalysis | Binary analysis services |
| WpfHexEditor.Core.Definitions | 400+ embedded language/format definitions (.whfmt) |
| WpfHexEditor.Core.Events | IDE event bus |
| WpfHexEditor.Core.ProjectSystem | Language registry |
| WpfHexEditor.Editor.Core | Shared editor abstractions |
| WpfHexEditor.Editor.TextEditor | Base text editor |
| WpfHexEditor.ColorPicker | Color picker for settings |
| WpfHexEditor.SDK | Plugin contracts and interfaces |

## License

GNU Affero General Public License v3.0 (AGPL-3.0)

## Links

- [GitHub Repository](https://github.com/abbaye/WpfHexEditorControl)
- [Report Issues](https://github.com/abbaye/WpfHexEditorControl/issues)
