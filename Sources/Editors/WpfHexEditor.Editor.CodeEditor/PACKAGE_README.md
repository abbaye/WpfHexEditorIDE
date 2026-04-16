# WpfCodeEditor

A full-featured WPF code editor `UserControl` for .NET 8.  
Drop it into any WPF window — no IDE, no plugin host, no external dependencies.

```
dotnet add package WpfCodeEditor
```

---

## Quick Start

### 1 — Add the namespace

```xml
<Window
    xmlns:ce="clr-namespace:WpfHexEditor.Editor.CodeEditor.Controls;assembly=WpfHexEditor.Editor.CodeEditor">
```

### 2 — Place the control

```xml
<ce:CodeEditorSplitHost x:Name="Editor" />
```

### 3 — Load a file

```csharp
using WpfHexEditor.Editor.CodeEditor.Controls;

// Load text
Editor.PrimaryEditor.LoadText(File.ReadAllText("Program.cs"));

// Optional: apply syntax highlighting
var lang = Editor.PrimaryEditor.GetLanguageForExtension(".cs");
Editor.SetLanguage(lang);
```

### 4 — Read back

```csharp
string content = Editor.PrimaryEditor.Text;
bool isDirty  = Editor.IsDirty;
await Editor.SaveAsync(); // saves to the file that was opened
```

### Lightweight plain-text variant

```xml
xmlns:te="clr-namespace:WpfHexEditor.Editor.TextEditor;assembly=WpfHexEditor.Editor.TextEditor"
...
<te:TextEditor x:Name="TextEdit" />
```

```csharp
TextEdit.LoadText(File.ReadAllText("notes.txt"));
string content = TextEdit.Text;
```

Use `TextEditor` when you only need plain text.  
Use `CodeEditorSplitHost` for syntax highlighting, folding, search, and LSP.

---

## Features

### Editing
- Multi-caret editing (Ctrl+Click)
- Smart auto-complete — context-aware, expression-filtered
- Code snippets with Tab expansion
- Block selection (Alt+Drag)
- Drag-and-drop text blocks
- Auto-indent and smart brace matching
- Unified undo/redo engine (coalescence)

### Syntax & Languages
- 400+ language definitions (.whfmt format, embedded in the package)
- Syntax highlighting with customizable themes
- LSP semantic token coloring
- Code folding — braces, regions, tags
- End-of-block hover hints

### Navigation
- Line numbers with configurable gutter
- Minimap overview panel
- Go to line (Ctrl+G) / Go to position dialog
- Breadcrumb navigation bar
- Bookmarks
- Ctrl+F inline search panel

### Search & Replace
- Find / replace with regex support
- Match case / whole word
- Search result highlighting with scroll-bar tick marks

### Advanced
- LSP (Language Server Protocol) integration
- Split view (horizontal or vertical)
- Diagnostic markers (errors, warnings, info)
- Scroll marker panel
- Column guides
- Word wrap
- Read-only mode (selection and copy still work)
- Word occurrence highlighting

### Settings
- Built-in settings panel with auto-generated UI
- JSON settings persistence (export / import)
- Full `DependencyProperty` API for programmatic control

---

## Standalone setup (no IDE host)

`WpfCodeEditor` runs without any plugin host or shell.  
The only requirement is merging the resource dictionary so themes resolve correctly:

```xml
<!-- App.xaml -->
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="pack://application:,,,/WpfHexEditor.Editor.CodeEditor;component/Themes/Generic.xaml" />
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>
```

Context menus and pop-ups use opaque backgrounds by default; no extra theming is needed.

---

## Included Assemblies

All bundled inside the package — zero external NuGet dependencies:

| Assembly | Purpose |
|---|---|
| WpfHexEditor.Editor.CodeEditor | `CodeEditorSplitHost` UserControl — main entry point |
| WpfHexEditor.Editor.TextEditor | Base `TextEditor` — plain-text editing |
| WpfHexEditor.Editor.Core | Shared editor abstractions |
| WpfHexEditor.Core | Settings, format detection, services |
| WpfHexEditor.Core.BinaryAnalysis | Binary analysis utilities |
| WpfHexEditor.Core.Definitions | 400+ embedded language definitions |
| WpfHexEditor.Core.Events | Internal event bus |
| WpfHexEditor.Core.ProjectSystem | Language registry |
| WpfHexEditor.ColorPicker | Color picker (settings panel) |
| WpfHexEditor.SDK | Plugin contracts (required internally) |

---

## What's New in 0.9.6.7

- **New**: `Ctrl+F` inline search panel — find next/previous without a separate dialog.
- **New**: Drag-and-drop text blocks — select a region and drag to reposition.
- **New**: CodeEditor refresh command — force-reload the current document from disk.
- **New**: Undo/Redo unification — HexEditor and CodeEditor share a single `UndoEngine`.
- **New**: LSP semantic token highlighting — richer coloring driven by the language server.
- **New**: Go-to-position dialog — jump to an absolute byte/line offset.
- **New**: Empty editor tabs — open a placeholder tab before loading a file.
- **Fix**: Scroll-bar theming consistent across split-view panes.
- **Fix**: TextEditor viewport line-count sync after window resize.
- **Fix**: SmartComplete expression-aware filtering — token type at caret drives suggestion list.
- **Fix**: Plugin error routing — plugin errors forwarded to output rather than silently dropped.
- **Fix**: LSP host startup stability improvements.

## What's New in 0.9.6.6

- **Fix**: SmartComplete popup no longer steals keyboard focus. The suggestion list is non-focusable — caret blink and word-highlight timers are never interrupted. Up/Down/PageUp/PageDown/Home/End/Enter/Tab/Escape are forwarded from the editor to the popup.
- **Fix**: `CodeEditorSplitHost` — IDE docking focus (Focus() on the Grid container) is correctly routed to the active editor via `OnGotKeyboardFocus`. Secondary editor `ModifiedChanged` is now wired. `IsDirty`, `Save`, and `SaveAsync` always delegate to the primary editor (which owns the file path).
- **Fix**: Word-highlight feedback loop eliminated — cursor position is tracked before `InvalidateRegion`, preventing a second 250ms debounce cycle from firing on every arrow-key press.
- **Fix**: Arrow-key navigation now triggers `NotifyCursorMoved()` so word-highlight updates without waiting for the next render frame.
- **Fix**: Save guard — writing an empty buffer over a non-empty file on disk is blocked with a status message (prevents data loss from timing races during `OpenAsync`).
- **Fix**: LSP burst-init dispatcher calls downgraded to `DispatcherPriority.Background` so Roslyn workspace startup does not block WPF frame rendering.
- **New**: `EnableWordHighlight` setting — toggle occurrence highlighting via the Code Editor options page or `CodeEditorDefaultSettings.EnableWordHighlight`.

## What's New in 0.9.6.5

- **Fix**: LSP inlay hints and declaration hints (code lens) no longer render as ghost overlays on top of code text. Hints now correctly align with the text area origin, accounting for gutter offset, top margin, and horizontal scroll.
- **Fix**: ReadOnly mode no longer blocks text selection and caret placement. Only text modification is blocked — selection (Shift+Arrow, drag), caret click, copy (Ctrl+C), and select all (Ctrl+A) now work as expected.

---

## License

GNU Affero General Public License v3.0 (AGPL-3.0)

## Links

- [GitHub Repository](https://github.com/abbaye/WpfHexEditorIDE)
- [Report Issues](https://github.com/abbaye/WpfHexEditorIDE/issues)
