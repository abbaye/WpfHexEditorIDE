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
- 57 language grammars for syntax highlighting (.whfmt, embedded in the package)
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
| WpfHexEditor.Core.Definitions | 57 language grammars for syntax highlighting + format detection catalog |
| WpfHexEditor.Core.Events | Internal event bus |
| WpfHexEditor.Core.ProjectSystem | Language registry |
| WpfHexEditor.ColorPicker | Color picker (settings panel) |
| WpfHexEditor.SDK | Plugin contracts (required internally) |

---

## What's New in 0.10.1.0

### Embedded Language Support — PHP, Markdown, and more

Building on the injection runtime introduced in 0.10.0.0, this release extends embedded language support:

- **New**: PHP-in-HTML — `<?php … ?>` zones are now syntax-highlighted as PHP inside `.html` / `.php` files.
- **New**: Markdown fenced code blocks — `` ```lang ` `` / ` ``` `` fences render per-language syntax highlighting inline.
- **New**: Any language can declare `embeddedLanguages[]` zones in its `.whfmt` — Vue SFC (`.vue`), Svelte (`.svelte`), YAML front-matter are ready with zero runtime changes.
- **Fix**: ClearType blur on embedded zone boundaries — single unified `orderedTokens` pass replaces the dual-render (gap + token) pipeline that caused independently-snapped x positions and sub-pixel misalignment.

### Internal

- **Refactor**: CodeEditor source decomposed into 6 focused partial files (rendering, folding, diagnostics, completion, LSP, commands) — no public API changes.
- **No public API changes** — drop-in upgrade from 0.10.0.0.

## What's New in 0.10.0.0

### Embedded Language Injection — HTML `<script>` and `<style>`

- **New**: `<script>` / `</script>` blocks in HTML files are syntax-highlighted as JavaScript.
- **New**: `<style>` / `</style>` blocks are syntax-highlighted as CSS.
- **Generic runtime**: `EmbeddedRangeClassifier` + `EmbeddedSyntaxHighlighter` consume `embeddedLanguages[]` from any `.whfmt` — no per-language code required.
- **New public types**: `EmbeddedSyntaxHighlighter`, `EmbeddedRangeClassifier` (namespace `WpfHexEditor.Core.ProjectSystem`).

## What's New in 0.9.9.0

- **New**: **Phase 9 Inline Code Actions** (Ctrl+. / lightbulb) — `CodeActionRegistry` + 3 mechanical fixers (`WH0032`/`WH0062`/`WH0070`) + Suppress-here, plugged into the existing LSP code-action pipeline.
- **New**: **Refactor ▶ context menu** + preview dialog + orchestrator (UI1–UI5) — rename, extract method, inline variable, change signature with diff preview.
- **New**: **Phase 10B Duplication tab redesign** — master/detail layout, side-by-side code preview with whitespace-only diff, severity chips, virtualized DataGrid, 8-item context menu, Markdown export.
- **New**: **Dynamic Snippets** with variable expansion + user snippet store (#88) — `$VAR$` placeholders resolved at insertion; `~/.wpfcodeeditor/snippets/*.json` user overlay.
- **New**: **Phase 1 Debugger integration** — Data Tips, Autos, edit-in-place, Run to Cursor, Exception Settings, Attach to Process dialog. `IDebugValueProvider` contract + `DebugValueHintsLayer` overlay.
- **New**: **+10 UI localizations** — uk-UA, cs-CZ, vi-VN, hu-HU, ro-RO, id-ID, th-TH, el-GR, da-DK, fi-FI — reaching 28 satellite resource locales total.
- **No public API breaks** — drop-in upgrade from 0.9.8.0.

## What's New in 0.9.8.0

- **New**: LSP semantic token colorization — richer syntax coloring driven by the language server.
- **New**: Zoom snap-to-pixel — GlyphRunRenderer snaps to pixel grid at each zoom level, eliminating sub-pixel blur on all font sizes.
- **New**: Roslyn inline hints upgrade — `IReferenceCountProvider` SDK contract decouples reference-count hints from the Roslyn implementation.
- **New**: Ctrl+Click links and emails — `ClickableLinksEnabled` / `ClickableEmailsEnabled` DPs; Ctrl+Click opens URLs in the default browser and email addresses in the mail client. Backported to `TextEditor` via `ScanLinksInText`.
- **Fix**: Minimap scroll — scroll position in `MinimapControl` now tracks viewport changes correctly.
- **Fix**: Satellite assemblies now correctly bundled — all 17 language `.resources.dll` files are included in the NuGet package (`IncludeSatelliteAssembliesInPackage` target added).
- **Fix**: LSP burst-init calls downgraded to `DispatcherPriority.Background` — Roslyn workspace startup no longer blocks WPF frame rendering.

## What's New in 0.9.7.0

- **Fix**: Code folding — scope guide lines now reach their correct end position in all cases (net brace-count algorithm replaces boolean push/pop; handles C# pattern matching `is { } x`, object initializers, and Allman-style blocks).
- **Fix**: Code folding — collapsing a block no longer draws spurious full-viewport vertical guide lines. Child regions inside a collapsed parent are excluded from the visible-region cache.
- **Fix**: Code folding — scroll position and line navigation (arrow keys, PageUp/Down) are now correct when blocks are collapsed. The virtualization engine's visible-rank index is converted to the correct physical line index via forward-scan.
- **Fix**: Code folding — `_firstVisibleRank` separates the VE pixel-math rank from `_firstVisibleLine` (physical index), eliminating the truncated-content and blank-editor regressions.
- **Fix**: Code folding — fold state restored on session re-open now calls `ToggleRegion` (fires `RebuildHiddenSet` + `RegionsChanged`) instead of mutating `IsCollapsed` directly, eliminating spurious guides and scroll inconsistency after restart.
- **Fix**: Code folding — diagnostic squiggle underlines and scroll-bar error ticks are repositioned when blocks are collapsed/expanded.
- **Fix**: Minimap — subscribes directly to `FoldingEngine.RegionsChanged` so the overview redraws immediately on fold toggle without depending on the host's coalescing timer.
- **Fix**: Block comment spans (e.g. `/* … */`) are now tracked across lines in `PatternFoldingStrategy`, preventing phantom fold regions inside multi-line comments.
- **New**: `endOfBlockHint.showLineNumber` and `endOfBlockHint.showLineCount` whfmt fields — control which pills appear in the end-of-block hover popup per language.

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
