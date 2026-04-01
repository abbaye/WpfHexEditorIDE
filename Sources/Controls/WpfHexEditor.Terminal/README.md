# WpfHexEditor.Terminal

WPF terminal panel — multi-tab shell session UI, `TerminalPanelViewModel` orchestrator, and `ShellSessionViewModel` per-tab state. Implements Feature #92.

**License:** GNU Affero General Public License v3.0
**Target:** net8.0-windows · WPF

---

## Architecture / Modules

### WPF Controls

- **`TerminalPanel.xaml` / `.cs`** — dockable `UserControl` (VS-like panel).
  - 5-row grid: toolbar / session tab strip / output ListBox / input row / status bar.
  - `ListBox` session tab strip bound to `TerminalPanelViewModel.Sessions`; active tab indicated by `DockAccentBrush` left border.
  - `ToolbarOverflowManager` with 5 groups (collapse order: `TbgScrollNav` → `TbgMacro`).
  - Re-subscribes to `OutputLines.CollectionChanged` on active session switch via `SubscribeToActiveSessionOutput()` / `UnsubscribeOutputLines()`.
  - Macro toolbar group: Record / Stop / Replay / Save buttons.
  - "+" button opens a `ContextMenu` to start a new session (HxTerminal, PowerShell, Bash).

### ViewModels

- **`TerminalPanelViewModel`** — MVVM orchestrator (Proxy pattern).
  - Owns `ObservableCollection<ShellSessionViewModel> Sessions` and `ActiveSession`.
  - Changing `ActiveSession` raises `PropertyChanged` for all proxied per-session properties (`OutputLines`, `CommandInput`, `IsRunning`, `WorkingDirectory`, `CurrentModeLabel`, etc.).
  - Shares a single `TerminalCommandRegistry` across all sessions (built-in commands registered once).
  - Exposes `SessionManager` for SDK/`ITerminalService` bridge.
  - Commands: `AddHxTerminalCommand`, `AddPowerShellCommand`, `AddBashCommand`, `CloseActiveSessionCommand`, `CopyAllCommand`, `SaveOutputCommand`, macro `StartRecording` / `StopRecording` / `ReplayMacro` / `SaveMacro`.

- **`ShellSessionViewModel`** — per-tab state wrapper (implements `ITerminalContext`, `ITerminalOutput`, `INotifyPropertyChanged`, `IDisposable`).
  - Wraps a `ShellSession` (domain model from `WpfHexEditor.Core.Terminal`).
  - Owns `ObservableCollection<TerminalOutputLine> OutputLines` (WPF layer only — avoids WPF dependency in Core).
  - Shell process resolution: CMD (`cmd.exe /k`), PowerShell (`pwsh.exe -NoLogo -NoExit`), Bash (`bash.exe` / `wsl.exe` fallback).
  - Asynchronously reads process stdout/stderr and appends `TerminalOutputLine` on the UI thread.

### Data Types

- **`TerminalOutputLine`** — record: `Text`, `OutputKind` (Standard / Info / Warning / Error / HxCommand).
- **`TerminalMode`** — enum used for per-session shell type display.
- **`TerminalExportService`** — exports terminal output to a plain-text file.
- **`OutputKindToBrushConverter`** — `IValueConverter` mapping `OutputKind` to theme brushes.

---

## Key Design Decisions

- `ObservableCollection<TerminalOutputLine>` lives only in `ShellSessionViewModel` (WPF layer) to avoid a WPF reference in `WpfHexEditor.Core.Terminal`.
- Code-behind tracks `_subscribedOutputLines` and rewires the collection-changed handler whenever `ActiveSession` changes, preventing stale listeners.
- Macro capture is decoupled: `MacroRecorder` subscribes to `TerminalCommandRegistry.CommandExecuted`; no coupling to specific command implementations.
- All sessions share the same `TerminalCommandRegistry` instance; each session has its own `CommandHistory` and OS process.

---

## Usage

```csharp
// Instantiate the panel ViewModel with the IDE context
var vm = new TerminalPanelViewModel(ideHostContext);

// Wire SDK bridge (called by MainWindow plugin system)
terminalService.SetOutput(vm.GetActiveOutput());
terminalService.SetSessionManager(vm.SessionManager);

// Open a new session programmatically
vm.OpenSession(TerminalShellType.PowerShell);
```
