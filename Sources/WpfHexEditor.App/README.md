# WpfHexEditor.App

**Type:** Executable (`net8.0-windows`)
**Role:** Main IDE host — entry point and orchestration shell for the entire WpfHexEditor IDE.

---

## Responsibility

`WpfHexEditor.App` is the root startup project. It:

- Bootstraps the WPF application and loads the global theme
- Creates and wires the docking engine, editor registry, and all built-in panels
- Discovers and loads plugins via `WpfPluginHost`
- Bridges every subsystem (build, terminal, solution explorer, output, errors) to a shared `IDEHostContext` used by plugins
- Manages the document lifecycle (open, close, dirty state, save, reload)
- Exposes the build system UI (ConfigurationManager, startup project, build output/error adapters)

---

## Architecture

### Partial Class Decomposition

`MainWindow` is split into 6 files, each owning a distinct concern:

| File | Concern |
|------|---------|
| `MainWindow.xaml.cs` | UI bootstrap, docking engine, editor lifecycle, keyboard shortcuts |
| `MainWindow.DocumentModel.cs` | DocumentManager, title/dirty propagation, auto-serialize timer |
| `MainWindow.Build.cs` | Build system wiring, configurations, startup project, build commands |
| `MainWindow.FileChangeBar.cs` | External file-change detection and reload pipeline |
| `MainWindow.PluginSystem.cs` | Plugin discovery/load, service adapter wiring, IDE EventBus, Plugin Manager tab |
| `MainWindow.Commands.cs` | `IIDECommand` registration, keyboard gesture bindings, Tools menu entries |

### Service Adapter Pattern

Every IDE subsystem is exposed to plugins through a typed adapter implementing an SDK interface. Plugins never reference `MainWindow` directly.

| Adapter | SDK Interface | Subsystem |
|---------|--------------|-----------|
| `DockingAdapter` | `IDockingAdapter` | Docking engine — panels/tabs |
| `MenuAdapter` | `IMenuAdapter` | Main menu contributions |
| `StatusBarAdapter` | `IStatusBarAdapter` | Status bar items |
| `HexEditorServiceImpl` | `IHexEditorService` | Active hex editor proxy |
| `DocumentHostService` | `IDocumentHostService` | Open file / navigate to line |
| `OutputServiceImpl` | `IOutputService` | Output panel channels |
| `ErrorPanelServiceImpl` | `IErrorPanelService` | Diagnostics / error list |
| `ThemeServiceImpl` | `IThemeService` | Theme switching |
| `TerminalServiceImpl` | `ITerminalService` | Terminal sessions |
| `SolutionExplorerServiceImpl` | `ISolutionExplorerService` | Solution tree navigation |

### Core App Modules

Built-in functionality that was previously external plugins but is now integrated directly into the App project as first-class modules:

| Module | Folder | Description |
|--------|--------|-------------|
| `DebugModule` | `Debug/` | DAP-based integrated debugger — nine panels (Locals, Autos, Watch, Call Stack, Threads, Tasks, Registers, Memory, Disassembly); `IDebugAdapterRegistry` + `IDebugVisualizerRegistry` extension points preserved for SDK plugins; VS-style Call Stack toolbar (search, navigate ←/→, Show All Threads, Show External Code) |
| `AssemblyExplorerModule` | `AssemblyExplorer/` | .NET PE tree, C# / VB.NET decompilation, ILSpy backend, CFG canvas, assembly diff, ECMA-335 token→offset, hex sync; lazy activation (Dormant until first open) |

### Null / Stub Services

`NullCodeEditorService` and `NullParsedFieldService` are no-op implementations returned when no relevant editor is active, preventing null-reference errors in plugin code.

---

## File Structure

```
WpfHexEditor.App/
├── App.xaml / App.xaml.cs                  — WPF entry point, CLI arg parsing, theme init
├── MainWindow.xaml / .cs                   — IDE shell layout + partial orchestration
├── MainWindow.DocumentModel.cs             — Document lifecycle
├── MainWindow.Build.cs                     — Build system
├── MainWindow.FileChangeBar.cs             — File monitor
├── MainWindow.PluginSystem.cs              — Plugin system
├── MainWindow.Commands.cs                  — IIDECommand registry, keyboard gestures, Tools menu
├── OutputLogger.cs                         — Static logging facade → OutputPanel
│
├── Debug/                                  — DebugModule (core, not a plugin)
│   ├── DebugModule.cs                      — Module registration + panel shell pre-build
│   ├── Panels/                             — Locals, Autos, Watch, CallStack, Threads, Tasks,
│   │                                         Registers, Memory, Disassembly XAML panels
│   └── ViewModels/                         — Panel ViewModels (DAP-wired)
│
├── AssemblyExplorer/                       — AssemblyExplorerModule (core, not a plugin)
│   ├── AssemblyExplorerModule.cs           — Module registration + lazy activation
│   └── ...                                 — Views, ViewModels, Services
│
├── Services/
│   └── DebugVisualizerRegistry.cs          — IDebugVisualizerRegistry implementation
│
├── Build/
│   ├── BuildErrorListAdapter.cs            — Routes build diagnostics → ErrorPanel
│   ├── BuildOutputAdapter.cs               — Routes build output → OutputPanel
│   ├── BuildStatusBarAdapter.cs            — Updates status bar during builds
│   └── ConfigurationManagerDialog.xaml/.cs — Add/edit build configurations
│
├── Controls/
│   ├── DocumentTabHeader.xaml/.cs          — Tab header with dirty indicator (●)
│   ├── DocumentInfoBar.xaml/.cs            — Orange reload/conflict warning bar
│   ├── OutputPanel.xaml/.cs                — Multi-channel log UI
│   ├── WelcomePanel.xaml/.cs               — VS Start Page with recent files
│   ├── PluginQuickStatusPopup.xaml/.cs     — Plugin load/unload toast
│   ├── EditorToolbarItemTemplateSelector   — Dynamic editor toolbar DataTemplate selector
│   └── TblItemTemplateSelector             — TBL dropdown DataTemplate selector
│
├── Dialogs/
│   ├── GoToOffsetDialog.xaml/.cs           — Ctrl+G jump to byte offset
│   ├── SaveChangesDialog.xaml/.cs          — Save/Discard/Cancel on close
│   ├── PasteConflictDialog.xaml/.cs        — Paste size conflict resolver
│   ├── ImportEmbeddedFormatDialog.xaml/.cs — Import .whfmt format definitions
│   ├── ImportEmbeddedSyntaxDialog.xaml/.cs — Import .whsyntax syntax definitions
│   ├── SolutionPropertyPagesDialog.xaml/.cs — Multi-page solution properties
│   └── SolutionPropertyPages/
│       ├── BuildDependenciesPage.cs        — Project build dependency order
│       ├── ConfigurationPropertiesPage.cs  — Per-configuration build settings
│       ├── SourceFilesPage.cs              — Included/excluded source files
│       └── StartupProjectsPage.cs          — F5 startup project selection
│
├── Models/
│   └── TblSelectionItem.cs                 — TBL selection dropdown VM
│
├── Services/                               — All SDK adapter implementations (see above)
│
├── Themes/
│   └── DialogStyles.xaml                   — Dialog button styles + orange InfoBar styles
│
└── ViewModels/
    └── PluginQuickStatusViewModel.cs       — Plugin toast notification state
```

---

## Startup Flow

```
App.OnStartup()
  └─ Parse --open <path> or bare file association arg
  └─ Load global theme (WpfHexEditor.Shell Dark theme)
  └─ Create MainWindow

MainWindow.OnLoaded()
  ├─ Restore docking layout from %AppData%\WpfHexEditor\layout.json
  ├─ Create all singleton panels (SolutionExplorer, Output, Errors, Terminal, …)
  ├─ InitDocumentManager() — subscribe to title/dirty events
  ├─ RegisterCoreModules()
  │    ├─ DebugModule.Register() — pre-build 9 debug panel shells; wire IDebugAdapterRegistry
  │    └─ AssemblyExplorerModule.Register() — register Dormant; activate on first open
  ├─ InitializePluginSystemAsync()
  │    ├─ Build all service adapters
  │    ├─ Assemble IDEHostContext
  │    ├─ DiscoverPluginsAsync() — scan Plugins folder
  │    ├─ LoadAllAsync() — init each plugin in priority order
  │    ├─ RestoreSession() or open startup file
  │    └─ Fire IDEInitializedEvent
  └─ Start auto-serialize timer (Tracked document mode)
```

---

## Document Lifecycle

```
Open file
  └─ Determine editor type (HexEditor / CodeEditor / TextEditor / …)
  └─ Create editor control + DockItem (ContentId = "doc-{uuid}")
  └─ DocumentManager.Register(contentId, editor)

Tab activated
  └─ Update _connectedHexEditor
  └─ Notify StatusBar, PropertyPanel, FocusContextService
  └─ SyncActiveDocument(contentId)

Close / shutdown
  └─ CheckDirtyDocuments() → SaveChangesDialog if unsaved
  └─ ShutdownThenCloseAsync()
        ├─ AutoSaveLayout()
        ├─ PluginHost.UnloadAll()
        └─ Application.Shutdown()
```

---

## Build System Integration

```
Solution loaded
  └─ InitializeBuildSystemAsync()
        ├─ BuildSystem + ConfigurationManager
        ├─ BuildOutputAdapter → OutputPanel (Build channel)
        ├─ BuildErrorListAdapter → ErrorPanel
        ├─ BuildStatusBarAdapter → StatusBar
        └─ StartupProjectRunner

Build command (F6 / Ctrl+Shift+B)
  └─ ClearDiagnostics()
  └─ BuildSystem.BuildSolutionAsync()
        └─ publishes BuildStarted/OutputLine/Progress/Succeeded/Failed events
```

---

## Well-Known Content IDs

| Content ID | Panel / Document |
|-----------|-----------------|
| `panel-solution-explorer` | Solution Explorer |
| `panel-errors` | Error List |
| `panel-terminal` | Integrated Terminal |
| `plugin-manager` | Plugin Manager document tab |
| `panel-debug-locals` | Locals debug panel |
| `panel-debug-autos` | Autos debug panel |
| `panel-debug-watch` | Watch debug panel |
| `panel-debug-callstack` | Call Stack debug panel |
| `panel-debug-threads` | Threads debug panel |
| `panel-debug-tasks` | Tasks debug panel |
| `panel-debug-registers` | Registers debug panel |
| `panel-debug-memory` | Memory Window debug panel |
| `panel-debug-disassembly` | Disassembly debug panel |
| `panel-assembly-explorer` | Assembly Explorer panel |
| `doc-{uuid}` | Any open document |
| `doc-projprops-{name}` | Solution/project property pages |
| `doc-nuget-solution-{name}` | Solution-level NuGet manager |
| `doc-nuget-{name}` | Project-level NuGet manager |

---

## Theme & Style

- Global theme loaded from `WpfHexEditor.Shell` (Dark by default; switchable at runtime)
- Key brush tokens: `DockWindowBackgroundBrush`, `DockMenuBackgroundBrush`, `DockAccentBrush`, `DockTabActiveBrush`
- Custom styles in `Themes/DialogStyles.xaml`: `InfoBarButtonStyle` (flat buttons on orange banner), `TitleBarButtonStyle`

---

## Key Dependencies

| Project | Role |
|---------|------|
| `WpfHexEditor.Shell` | Docking engine + 8 themes |
| `WpfHexEditor.Editor.Core` | `IDocumentEditor`, `DocumentManager`, `UndoEngine`, `IDialogService` |
| `WpfHexEditor.PluginHost` | Plugin discovery + loading |
| `WpfHexEditor.BuildSystem` | Build orchestration engine |
| `WpfHexEditor.ProjectSystem` | Solution / project model |
| `WpfHexEditor.Panels.IDE` | Solution Explorer, Properties panels |
| `WpfHexEditor.Terminal` | Integrated terminal |
| `WpfHexEditor.Core.LSP.Client` | JSON-RPC LSP client engine |
| `WpfHexEditor.Core.Roslyn` | In-process Roslyn language client |
| All 14 Editor modules | Pluggable editor controls |

---

## Design Patterns Used

| Pattern | Where |
|---------|-------|
| **Adapter** | All service adapters (DockingAdapter, MenuAdapter, etc.) |
| **Partial class** | MainWindow split across 5 domain files |
| **Facade** | OutputLogger, DocumentHostService |
| **Observer** | DocumentManager events → MainWindow handlers |
| **Null Object** | NullCodeEditorService, NullParsedFieldService |
| **Template Selector** | EditorToolbarItemTemplateSelector, TblItemTemplateSelector |
| **Singleton** | All built-in panels, OutputPanel, _pluginHost |
