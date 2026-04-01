# WpfHexEditor.Core.Terminal

Platform-agnostic terminal command engine — 39 built-in commands, HxScript scripting, macro recording/replay, shell session lifecycle, and command history. No WPF dependency.

**License:** GNU Affero General Public License v3.0
**Target:** net8.0-windows

---

## Architecture / Modules

### Contracts

- **`ITerminalCommandProvider`** — interface implemented by every command: `CommandName`, `Description`, `ExecuteAsync(args, output, context)`.
- **`ITerminalContext`** — ambient context available to all commands: working directory, IDE host, session info.
- **`ITerminalOutput`** — output sink: `WriteLine(text, kind)`, `Clear()`.

### Command Registry

- **`TerminalCommandRegistry`** — thread-safe `Dictionary<string, ITerminalCommandProvider>` (case-insensitive). Provides `FindCommand`, `GetAll`, `GetCompletions` (Tab completion). Fires `CommandExecuted` event (outside lock) for macro recording integration.

### Built-in Commands (`BuiltInCommands/` — 39 commands)

| Category | Commands |
|---|---|
| File I/O | `open-file`, `close-file`, `save-file`, `save-as`, `copy-file`, `delete-file`, `select-file` |
| Navigation | `list-files`, `list-open-files`, `open-folder`, `cd` (via context) |
| Hex access | `read-hex`, `write-hex` |
| Solution | `open-solution`, `close-solution`, `reload-solution`, `open-project`, `close-project` |
| Panels | `open-panel`, `close-panel`, `focus-panel`, `toggle-panel`, `append-panel`, `clear-panel` |
| Plugin | `plugin-list`, `run-plugin` |
| Scripting | `run-script` |
| Macros | `record` (start/stop/save), `replay-history [N]` |
| Output | `echo`, `send-output`, `send-error` |
| Diagnostics | `show-errors`, `show-logs`, `status`, `version` |
| Shell | `clear`, `history`, `help`, `exit` |

### Scripting (`Scripting/`)

- **`HxScriptParser`** — tokenizes `.hxscript` source into a list of `IScriptInstruction` objects.
- **`HxScriptEngine`** — executes instructions sequentially via `TerminalCommandRegistry`, fully async and cancellable. Returns exit code of the last instruction.
- **`ScriptInstructions/`** — concrete instruction types (command invocation, comments, conditionals).

### Macros (`Macros/`)

- **`MacroEntry`** / **`MacroSession`** — immutable records representing a recorded command sequence.
- **`ITerminalMacroService`** — service contract for record/stop/replay/save.
- **`MacroRecorder`** — subscribes to `TerminalCommandRegistry.CommandExecuted`; accumulates `MacroEntry` records.
- **`MacroReplayEngine`** — replays a `MacroSession` by dispatching commands through the registry.
- **`TerminalMacroService`** — composes recorder and replay engine; exposes the `ITerminalMacroService` contract.

### Shell Sessions (`ShellSession/`)

- **`TerminalShellType`** — enum: `HxTerminal`, `PowerShell`, `Bash`, `Cmd`.
- **`ShellSession`** — pure domain model: holds `Process`, `StreamWriter` (stdin), `CommandHistory`, session `Id`. No WPF types.
- **`IShellSessionManager`** / **`ShellSessionManager`** — thread-safe creation, retrieval, and closure of `ShellSession` instances; prevents closing the last open session.

### Other

- **`CommandHistory`** — per-session command history with up/down navigation.
- **`OutputKindToBrushConverter`** (WPF-agnostic string mapping) — used by the WPF layer.

---

## Usage

```csharp
var registry = new TerminalCommandRegistry();
// Register all built-in commands
registry.Register(new EchoCommand());
registry.Register(new OpenFileCommand(ideHostContext));
// ...

// Execute a command
var cmd = registry.FindCommand("echo");
await cmd!.ExecuteAsync(new[] { "hello" }, output, context);

// Run a script
var engine = new HxScriptEngine(registry);
int code = await engine.RunAsync(scriptSource, output, context);
```
