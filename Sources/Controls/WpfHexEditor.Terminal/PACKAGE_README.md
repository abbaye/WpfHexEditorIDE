# WpfTerminal

A dockable WPF terminal emulator UserControl for .NET 8.

## Features

- **Multi-tab Shell Sessions** — cmd, PowerShell, bash, Git Bash
- **39 Built-in Commands** — file I/O, navigation, hex access, solution, plugins, diagnostics
- **Macro Recording/Replay** — record command sequences, replay with variables
- **HxScript Scripting** — lightweight `.hxscript` scripting engine for automation
- **Command History** — per-session history with up/down navigation
- **Find in Output** — search through terminal output with highlight
- **Export** — save output to text or HTML
- **Themeable** — uses WPF DynamicResources for seamless dark/light themes
- **MVVM Architecture** — clean separation of UI and logic

## Quick Start

```xml
<Window xmlns:term="clr-namespace:WpfHexEditor.Terminal;assembly=WpfHexEditor.Terminal">
    <term:TerminalPanel x:Name="Terminal" />
</Window>
```

```csharp
// Add a new PowerShell session
Terminal.ViewModel.AddSession("PowerShell", TerminalShellType.PowerShell);

// Execute a command programmatically
await Terminal.ViewModel.ActiveSession.ExecuteCommandAsync("Get-Process");
```

## Built-in Commands (selection)

| Command | Description |
|---------|------------|
| `cd <path>` | Change directory |
| `ls` / `dir` | List files |
| `cat <file>` | Display file content |
| `find <pattern>` | Search in files |
| `clear` | Clear output |
| `macro record` | Start recording |
| `macro play` | Replay recorded macro |
| `help` | List all commands |

## Included Assemblies

| Assembly | Description |
|----------|------------|
| WpfHexEditor.Terminal | WPF terminal panel (MVVM) |
| WpfHexEditor.Core.Terminal | Command engine, macros, scripting |
| WpfHexEditor.SDK | Plugin contracts |

## Installation

```
dotnet add package WpfTerminal
```

## License

GNU AGPL v3.0 — [GitHub Repository](https://github.com/abbaye/WpfHexEditorIDE)
