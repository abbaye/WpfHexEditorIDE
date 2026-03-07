# Terminal Panel

The integrated terminal gives you a full command-line interface inside the IDE, with 31+ built-in commands, script execution, colored output, and a plugin API.

---

## 📋 Table of Contents

- [Opening the Terminal](#opening-the-terminal)
- [Terminal Modes](#terminal-modes)
- [Built-In Commands](#built-in-commands)
- [Keyboard Shortcuts](#keyboard-shortcuts)
- [Features](#features)
- [HxScript Files](#hxscript-files)
- [Plugin API (ITerminalService)](#plugin-api-iterminalservice)
- [Export Session](#export-session)
- [Architecture](#architecture)
- [See Also](#see-also)

---

## Opening the Terminal

| Method | Action |
|--------|--------|
| **Tools → Terminal** | Opens / focuses terminal panel |
| **`Ctrl+`` `** | Toggle terminal panel |
| Terminal command `open-panel terminal` | Also works |

The terminal docks at the bottom by default (`panel-terminal` ContentId). It can be floated, auto-hidden, or moved anywhere in the docking layout.

---

## Terminal Modes

The mode indicator badge is displayed in the toolbar:

| Mode | Badge | Description |
|------|-------|-------------|
| **Interactive** | `HX>` | Normal shell — user types and executes commands |
| **Script** | `SCR>` | Running a `.hxscript` file — input disabled |
| **ReadOnly** | `RO>` | Output-only — no input accepted |

Switch mode via toolbar dropdown or command:
```
set-mode interactive
set-mode script
set-mode readonly
```

---

## Built-In Commands

### Core
| Command | Description |
|---------|-------------|
| `clear` | Clear terminal output |
| `echo <text>` | Print text |
| `exit` | Close terminal |
| `help [command]` | List commands or show help for a command |
| `history` | Show command history |
| `version` | Show IDE version |

### File System
| Command | Description |
|---------|-------------|
| `copy-file <src> <dst>` | Copy a file |
| `delete-file <path>` | Delete a file |
| `list-files [--filter <pattern>]` | List files in current directory |
| `open-file <path>` | Open a file in the IDE |
| `open-folder <path>` | Open a folder in Explorer |

### Editor
| Command | Description |
|---------|-------------|
| `close-file` | Close the active file tab |
| `read-hex <offset> [count]` | Read bytes at offset from active HexEditor |
| `save-file` | Save the active file |
| `save-as <path>` | Save active file to a new path |
| `search <pattern>` | Search hex pattern in active HexEditor |
| `select-file <path>` | Select a file in the Solution Explorer |
| `write-hex <offset> <bytes>` | Write hex bytes at offset |

### Project / Solution
| Command | Description |
|---------|-------------|
| `close-project` | Close the active project |
| `close-solution` | Close the current solution |
| `open-project <path>` | Open a `.whproj` file |
| `open-solution <path>` | Open a `.whsln` file |
| `reload-solution` | Reload the solution from disk |

### Panels
| Command | Description |
|---------|-------------|
| `append-panel <id> <text>` | Append text to a panel's output |
| `clear-panel <id>` | Clear panel output |
| `close-panel <id>` | Close a dockable panel |
| `focus-panel <id>` | Focus / bring panel to front |
| `open-panel <id>` | Open / show a panel |
| `toggle-panel <id>` | Toggle panel visibility |

### Plugins
| Command | Description |
|---------|-------------|
| `plugin-list` | List all loaded plugins with status |
| `run-plugin <id>` | Activate a specific plugin |

### Diagnostics
| Command | Description |
|---------|-------------|
| `export-log [path]` | Export terminal session to file |
| `send-error <msg>` | Post an error to the Error Panel |
| `send-output <msg>` | Post a message to the Output Panel |
| `set-mode <mode>` | Change terminal mode |
| `show-errors` | Show the Error Panel |
| `show-logs` | Show the Output Panel |
| `status` | Show IDE status summary |
| `terminal-info` | Show terminal configuration |

---

## Keyboard Shortcuts

| Key | Action |
|-----|--------|
| `Enter` | Execute command |
| `↑` / `↓` | Navigate command history |
| `Tab` | Auto-complete command or path |
| `Ctrl+C` | Copy selected output |
| `Ctrl+A` | Select all output |
| `Ctrl+L` | Clear terminal (same as `clear`) |
| `Escape` | Clear input line |

---

## Features

### Colored Output

Output lines are color-coded by severity:

| Color | Severity |
|-------|----------|
| White / default | Info |
| Yellow | Warning |
| Red | Error |
| Green | Success |
| Gray | Debug / verbose |

### Copy-on-Select

Selecting text in the output automatically copies it to the clipboard (configurable via toolbar toggle).

### Auto-Scroll

The output automatically scrolls to the last line on new output. Pause auto-scroll via the **Pause** toolbar toggle to read earlier output.

### Timestamps

Enable timestamps via the **Timestamps** toolbar toggle. Each output line gets a `[HH:mm:ss]` prefix.

### Word Wrap

Toggle word wrap via the **Wrap** toolbar toggle.

### Font & Encoding

Font family, font size, and output encoding are configurable in `AppSettings.TerminalSettings`.

### 5000-Line Buffer

Terminal output is capped at 5000 lines. Older lines are automatically removed to prevent memory growth during long sessions.

---

## HxScript Files

`.hxscript` files contain a sequence of terminal commands, one per line. Execute with:

```
open-file C:\Scripts\my-analysis.hxscript
```

Or from external CLI:
```bash
WpfHexEditor.App.exe --script C:\Scripts\my-analysis.hxscript
```

Example `.hxscript`:
```
open-file C:\Data\firmware.bin
search DE AD BE EF
read-hex 0x1000 256
export-log C:\Logs\firmware-analysis.txt
```

`HxScriptEngine` parses and dispatches each line to `TerminalCommandRegistry`. Command history from scripts is persisted across sessions.

---

## Plugin API (ITerminalService)

Plugins can interact with the terminal via `IIDEHostContext.TerminalService`:

```csharp
ITerminalService terminal = context.TerminalService;

// Execute a command
terminal.Execute("list-files");

// Write a line directly (Info severity)
terminal.WriteLine("Analysis complete.");

// Write with severity
terminal.WriteLine("File not found: data.bin", TerminalSeverity.Warning);

// Write a table
terminal.WriteTable(
    headers: new[] { "Offset", "Value", "Description" },
    rows:    new[] {
        new[] { "0x0000", "4D 5A", "DOS MZ header" },
        new[] { "0x003C", "E8 00", "PE offset" }
    });

// Read history
IReadOnlyList<string> history = terminal.HistoryLines;

// Export session
terminal.ExportSession(@"C:\Logs\session.txt");
```

Declare terminal access in your plugin manifest:
```json
{
  "capabilities": ["Terminal"],
  "permissions":  ["WriteOutput"]
}
```

---

## Export Session

The **Export** toolbar button (or `export-log` command) saves the full terminal session.

Supported formats via `TerminalExportService`:

| Format | Extension | Colors |
|--------|-----------|--------|
| Plain Text | `.txt` | No |
| HTML | `.html` | Yes — CSS `<span>` |
| RTF / Word | `.rtf` | Yes — color table `\cf` |
| ANSI | `.ansi` | Yes — escape codes |
| Markdown | `.md` | No — table format |
| SpreadsheetML | `.xml` | Yes — Excel/LibreOffice |

---

## Architecture

```
WpfHexEditor.Core.Terminal/
  TerminalCommandRegistry     — registry + dispatch for all 31+ commands
  CommandHistory              — persistent history (cross-session)
  HxScriptEngine              — .hxscript file execution
  ITerminalContext            — context for command execution (CWD, output, etc.)
  ITerminalOutput             — output API (WriteLine, WriteTable, Clear)

WpfHexEditor.Terminal/
  TerminalPanel.xaml(.cs)     — VS-style dockable panel (RichTextBox output, input row)
  TerminalPanelViewModel.cs   — ITerminalContext + ITerminalOutput merged VM
  TerminalExportService.cs    — 6-format session export
  TerminalMode.cs             — Interactive / Script / ReadOnly enum

WpfHexEditor.SDK/
  ITerminalService            — plugin-facing contract
  PluginCapabilities.Terminal — capability flag for manifest
```

`TerminalPanelViewModel` implements both `ITerminalContext` (for command dispatch) and `ITerminalOutput` (for writing to the panel), keeping the service layer decoupled from WPF.

---

## See Also

- **[Plugin System](Plugin-System)** — `ITerminalService` registration
- **[Plugin Monitoring](Plugin-Monitoring)** — resource monitoring panel
- **[Architecture Overview](Architecture-Overview)** — ContentId routing
- **[FAQ](FAQ#ide-application)** — terminal FAQ
