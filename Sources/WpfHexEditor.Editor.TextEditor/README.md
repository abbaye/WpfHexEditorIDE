# WpfHexEditor.Editor.TextEditor

Multi-language text editor for [WpfHexEditor](https://github.com/abbaye/WpfHexEditorControl) with **100% in-house syntax highlighting** — no external dependencies.

## Features

- **26 embedded language definitions** (`.whlang` JSON files)
- **Auto-detection** by file extension
- **Full IDocumentEditor** integration (Undo/Redo, Save, Dirty tracking, Commands)
- **IOpenableDocument** — opens directly from a file path via the EditorRegistry
- Virtualized line rendering (only visible lines are painted)
- Monospace `DrawingContext`-based renderer (same pattern as HexViewport)
- Caret blinking, keyboard navigation, mouse click-to-caret
- Configurable encoding (UTF-8, Latin-1, Shift-JIS, …)
- Status bar: language, caret position, encoding

## Supported Languages

| Category   | Languages |
|------------|-----------|
| Assembly   | x86/x64, 6502 (NES/SNES), Z80 (GameBoy/MSX), ARM/Thumb (GBA/DS/mobile), MIPS (PSX/N64/PSP) |
| C-style    | C/C++, C#, Java, JavaScript/TypeScript, Rust, Go, Swift, Kotlin, PHP, Dart |
| Scripting  | Python, Lua, Ruby, Perl, Shell/Bash/Batch/PowerShell |
| Data       | XML/HTML/XAML, JSON (JSONC/JSON5), INI/CFG/CONF, YAML/TOML, SQL |
| Misc       | Markdown, Plain Text |

## Standalone Usage (without App or ProjectSystem)

```csharp
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.TextEditor;
using WpfHexEditor.Editor.TextEditor.Controls;

// Register with the editor registry (optional — for automatic open-by-extension)
EditorRegistry.Instance.Register(new TextEditorFactory());

// Or create directly:
var editor = new TextEditor();
await editor.OpenAsync("path/to/script.lua");
myGrid.Children.Add(editor);
```

## Adding Custom Language Definitions

Place `.whlang` JSON files in:

```
%AppData%\WpfHexEditor\SyntaxDefinitions\my_language.whlang
```

Files in that directory are merged at startup and **override** embedded definitions with the same name.

### `.whlang` format

```jsonc
{
  "name": "My Language",
  "extensions": [ ".myext" ],
  "lineComment": "//",
  "blockCommentStart": "/*",
  "blockCommentEnd": "*/",
  "rules": [
    { "type": "Keyword", "pattern": "\\b(my|keywords|here)\\b", "colorKey": "TE_Keyword" },
    { "type": "Comment", "pattern": "//.*$",                    "colorKey": "TE_Comment" }
  ]
}
```

Available `colorKey` values: `TE_Keyword`, `TE_Comment`, `TE_String`, `TE_Number`, `TE_Register`, `TE_Label`, `TE_Operator`, `TE_Type`, `TE_Directive`, `TE_Foreground`.

## Theme Integration

Add `TE_*` brush keys to your theme `Colors.xaml` (after `PP_*`, before `AccentColor`):

```xml
<!-- TE_* — Text Editor -->
<SolidColorBrush x:Key="TE_Background"           Color="#1E1E1E"/>
<SolidColorBrush x:Key="TE_Foreground"           Color="#D4D4D4"/>
<SolidColorBrush x:Key="TE_LineNumberBackground" Color="#252526"/>
<SolidColorBrush x:Key="TE_LineNumberForeground" Color="#6E7681"/>
<SolidColorBrush x:Key="TE_CurrentLineBrush"     Color="#282828"/>
<SolidColorBrush x:Key="TE_Keyword"              Color="#569CD6"/>
<SolidColorBrush x:Key="TE_Comment"              Color="#6A9955"/>
<SolidColorBrush x:Key="TE_String"               Color="#CE9178"/>
<SolidColorBrush x:Key="TE_Number"               Color="#B5CEA8"/>
<SolidColorBrush x:Key="TE_Register"             Color="#9CDCFE"/>
<SolidColorBrush x:Key="TE_Label"                Color="#C8C840"/>
<SolidColorBrush x:Key="TE_Operator"             Color="#D4D4D4"/>
<SolidColorBrush x:Key="TE_Type"                 Color="#4EC9B0"/>
<SolidColorBrush x:Key="TE_Directive"            Color="#C586C0"/>
```

## License

MIT
