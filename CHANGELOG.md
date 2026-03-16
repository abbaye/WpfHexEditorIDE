# Changelog

All notable changes to **WpfHexEditor** are documented here.

Format: [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) · Versioning: [Semantic Versioning](https://semver.org/spec/v2.0.0.html)

---

## [0.5.1] — 2026-03-16 — Source Outline Patch

### 🔧 Fixed

- **Solution Explorer: source type nodes auto-expanded on first lazy load** — `SourceTypeNodeVm` inherited `IsExpanded = true` from the base class default, causing class/struct/interface nodes to appear fully expanded (all members visible) the moment a `.cs` or `.xaml` file node was expanded for the first time. Type nodes are now explicitly initialized with `IsExpanded = false` in `ApplyOutlineToNode`; the user must intentionally expand a type node to reveal its members.

---

## [0.5.0] — 2026-03-16 — Code Editor, Source Outline, Build Output, Assembly Explorer & VS Solution Loader

### ✨ Added — Solution Explorer: Source Outline Navigation

- **`SourceOutlineEngine`** (`WpfHexEditor.Core.SourceAnalysis`) — BCL-only regex-based parser that extracts types and members with 1-based line numbers from `.cs` and `.xaml` files; results are cached per file path
- **`ISourceOutlineService`** — contract for source outline providers; injected into `SolutionExplorerViewModel`
- **`SourceTypeModel` / `SourceMemberModel` / `SourceOutlineModel`** — immutable models for parsed types and members
- **`SolutionExplorerNodeVm`** — lazy expansion via `TreeViewItem.ExpandedEvent`; `LoadingNode` placeholder shown while parsing; outline nodes expose `LineNumber` for direct navigation
- **`SolutionExplorerViewModel`** — `OutlineService` wired; async expand handler calls `SourceOutlineEngine` on background thread
- **`SolutionExplorerPanel.xaml.cs`** — `TreeViewItem.Expanded` handler triggers on-demand source parse; prevents double-expansion
- Source outline is activated automatically for `.cs` and `.xaml` nodes in the Solution Explorer tree

### ✨ Added — Assembly Explorer Improvements (v0.2.1)

- **Collapse All** — toolbar button + context menu item collapses the entire assembly tree to root nodes
- **Close All Assemblies** — toolbar button (`ToolbarRightPanel`, always visible) + context menu item (red `#EF4444`) clears all loaded assemblies in one action; `CloseAllCommand` wired in `AssemblyExplorerViewModel`
- **Decompile to C# → Code Editor tab** — `OnDecompile` now sets `ViewModel.SelectedNode` before executing `OpenInEditorCommand`; ensures right-click context menu decompilation always opens the correct node in a Code Editor tab
- **C# Syntax Highlighting in decompiled output** — decompiled C# skeleton rendered with full token-level syntax coloring in the Code Editor tab (keywords, types, strings, comments, numbers, operators)
- **Extract to Project workflow** — `AssemblyCodeExtractService` extracts decompiled C# from any type/method/assembly node and places it in a chosen project; `ProjectPickerDialog` lets the user select the target project; wired via `ExtractToProjectAsync` in `AssemblyExplorerPanel`

### ✨ Added — Build Output Routing (#103 partial)

- **`OutputServiceImpl.Write("Build", …)`** — fixed: now routes to `OutputLogger.BuildInfo/BuildWarn/BuildError/BuildSuccess` (was silently routing to the General channel)
- **`OutputLogger.FocusChannel(string source)`** — new static method; switches the Output panel to the specified tab on the UI thread via `Dispatcher.InvokeAsync`
- **`OutputPanel.SetActiveSource(string source)`** — new `internal` method; iterates `SourceComboBox.Items` to match and programmatically select the correct channel
- **Auto-focus Build tab** — `MainWindow.Build.cs` calls `OutputLogger.FocusChannel(SourceBuild)` in all build runners (`RunBuildSolutionAsync`, `RunRebuildSolutionAsync`, `RunCleanSolutionAsync`, `RunBuildProjectByIdAsync`, `RunRebuildProjectByIdAsync`) before awaiting the build
- **Empty-solution guard** — if no solution or projects are loaded, `RunBuildSolutionAsync` logs a gold warning "No solution or projects loaded — nothing to build." and returns immediately instead of silently succeeding

### ✨ Added — VS Solution Loader: Project Templates

- **`DotNetProjectTemplate`** — abstract base with `Name`, `Description`, `DefaultProjectName`, `SupportedLanguages`, `GenerateFiles()`
- **`ConsoleAppTemplate`** — .NET 8 console application scaffold
- **`ClassLibraryTemplate`** — .NET 8 class library scaffold
- **`WpfAppTemplate`** — WPF application scaffold (net8.0-windows)
- **`AspNetApiTemplate`** — ASP.NET Core Web API scaffold
- Templates are discovered via `VsSolutionLoaderPlugin` and used by "New Project" dialogs

### ✨ Added — Code Editor Enhancements (#84 partial)

- **Full `.whlang` syntax coloring** — all 26 embedded language definitions now produce token-level colored output (keyword, type, string, comment, number, operator, url, preprocessor, attribute, identifier)
- **Live brush resolution** — `ResolveBrushForKind()` uses `TryFindResource` directly on the editor control instead of the WPF DP system; eliminates "brush not found" fallback to transparent on theme switches
- **Foreground base pass** — entire document gets the theme foreground color before token colorization to prevent uncolored regions from rendering transparent
- **Hover-only URL underline** — hyperlinks in code are underlined only on `MouseEnter`; tooltip shows the full URL; click opens in browser
- **URL baseline fix** — underline drawn closer to text baseline for visual consistency with VS Code
- **`CodeEditorSplitHost`** improvements — split host properly propagates theme changes to both panes
- **`CodeDocument` model cleanup** — simplified document state; reduced unnecessary allocations
- **`CodeEditorFactory`** registration aligned with updated factory contracts

### ✨ Added — LSP Infrastructure

- **`RefactoringEngine`** (`WpfHexEditor.LSP`) — central registry for `IRefactoring` implementations; `Register(IRefactoring)` + `GetAvailable(RefactoringContext)` for contextual filtering; resolves `CS0246` compile error in `CommandIntegration`

### ✨ Added — Docking Engine: Tab Overflow

- **`TabOverflowButton`** — improved overflow button for docked tab groups; correct positioning, theme-aware context menu
- **`TabOverflowPanel`** — manages collapsed tab groups; updated hit-testing and layout logic
- **`DockControl` / `DockTabControl`** — wired tab overflow to new panel; `DockTabEventWirer` updated to relay overflow events

### 🔧 Fixed

- **Assembly Explorer: Decompile context menu** — right-click "Decompile to C#" had no effect; `SelectedNode` was not set before `OpenInEditorCommand` executed (`eb536cc6`)
- **Assembly Explorer: AssemblyDiffPanel crash** — `XamlParseException: 'ContextMenu' cannot have a logical or visual parent` at `InitializeComponent()`; root cause: Button with TextBlock child + `<Button.ContextMenu>` property element causes parent conflict in WPF XAML; fixed by converting Button content to `Content="&#xE712;"` attribute form
- **Code Editor: syntax highlight transparent on load** — `ResolveBrushForKind` referenced DP-system brushes that weren't in scope at paint time; now uses `TryFindResource` on the control element (`ff19b091`)
- **Build system: output not visible** — all build output was routed to the General channel with `[Build]` prefix; now correctly routes to the dedicated Build channel (`d335b505`)
- **Build toolbar/menu: disabled after solution open** — build commands stayed grayed out after opening a `.sln`; fixed by refreshing command states on `SolutionLoadedEvent` (`d8fe1882`)
- **File access: FileShare.None causing lock conflicts** — analysis services opened files exclusively; replaced with `FileShare.ReadWrite` throughout + external change detection (`b3090989`)
- **Changeset tracking: performance regression** — `.whchg` tracking was O(n²) on large files; optimized and disabled by default (`f140c544`)
- **Startup: System.Text.Json 10 transitive dependency** — MSBuild packages pulled in JSON 10 which conflicted with WPF serialization; pinned to 8.x in affected projects (`1027f192`)
- **Grammar Explorer: embedded grammar discovery** — deferred panel docking and reliable discovery of grammars bundled as embedded resources (`a7196376`)
- **SolutionLoader: VS/WH/MSBuild build order** — plugins not included in App build order; added project references (`7a7b318d`)
- **PluginDev: duplicate DevPluginHandle + missing using** — removed duplicate type, added `System.IO` import (`eb656c35`)
- **Compilation errors: PluginProjectTemplate, CodeEditorOptionsPage, Initializers** — various CS errors fixed (`131cd20b`)

### ⚡ Changed

- **SolutionLoader: dynamic file-dialog filter** — `ISolutionLoader` plugins now declare their supported file extensions; the Open Solution dialog filter is built dynamically from all registered loaders (`ba9279b2`)
- **External solution routing via `ISolutionLoader`** — `.sln` files are routed to the correct loader plugin instead of being hard-coded in `MainWindow`
- **IProjectTemplate**: extended contract with `DefaultProjectName`, `SupportedLanguages`, `GenerateFiles()`

---

## What's Next

> Planned features — subject to change. Feature numbers map to DevPlans.

### Solution Explorer — Source Outline
- **Expand-state persistence** — remember which type nodes were expanded per file across sessions (user preference, opt-in)
- **Collapse All Outline** — toolbar button to collapse all type nodes inside an expanded file node back to the class-level view
- **Go to Definition from outline click** — single-click on a member node opens the file and positions the caret at the member declaration; currently only navigates to line 1 for type nodes
- **Outline refresh on save** — automatically re-parse and update the outline when a `.cs` / `.xaml` file is saved from within the IDE (currently only refreshes on external file change)
- **Synalysis UFWB Grammar Support (#177)** — new plugin `WpfHexEditor.Plugins.SynalysisGrammar`; parses `.grammar` files and applies structured overlays to binary content in the Hex Editor; 10 embedded grammars (PNG, ZIP, PE, ELF, BMP, WAV, MP4, SQLite, EXT4, GZIP)

### Plugin System — Remaining
- **Hot-load / Hot-unload UI** — expose collectible `AssemblyLoadContext` hot-reload in the Plugin Manager UI (ALC mechanism complete; toolbar button + status indicator pending)
- **#41 Plugin Marketplace** — online registry browse/install/update, signed packages (marketplace panel UI done; registry backend and package signing pending)
- **#43 Auto-Update** — `UpdateService` / `UpdateChecker`, rollback support, scheduled checks for IDE + plugins
- **gRPC transport migration** — replace named-pipe IPC with gRPC for sandbox plugins

### Image Viewer — Remaining
- Batch export, format conversion (PNG/JPEG/BMP/TIFF)
- Histogram panel, color picker, EXIF metadata viewer

### IDE Core Infrastructure
- **#36 Service Container / Dependency Injection** — `ServiceContainer` singleton; `FileService`, `EditorService`, `PanelService`, `PluginService`, `EventBus`, `TerminalService` — Singleton/Scoped/Transient lifecycle
- **#37 Global CommandBus** — all IDE actions (menus, toolbar, terminal, plugins) routed through `CommandBus`; every command has an Id, Handler, CanExecute context, and Category
- **#38 Keyboard Shortcuts & Bindings** — `KeyBindingService`, configurable gestures per command, conflict detection, plugin-extensible, export/import
- **#39 User Preferences Persistence** — `ConfigurationManager`, per-section schemas, plugin config API, export/import, cross-session persistence
- **#40 Centralized Logging & Diagnostics** — `LogService` (Info/Warning/Error/Debug), `DiagnosticService` (perf metrics), `LogSink` abstraction, Output + Error Panel integration

### Code Intelligence / Editor
- **#85 LSP Engine** — incremental symbol parsing, folding, go-to-definition, find-references (`RefactoringEngine` registry in place; full LSP pipeline pending)
- **#86 IntelliSense** — autocomplete, quick-info, signature help, multi-caret, virtual scroll for >1 GB files
- **#88 Dynamic Snippets** — `SnippetsManager`, `SnippetEditorDialog`; user/plugin/language-scoped; dynamic variables (`CurrentLine`, `FileName`, `CursorPosition`); priority: user > imported > built-in
- **#89 AI-Assisted Code Suggestions** — `AICompletionEngine`, `AIRefactoringAssistant`; contextual completions, auto-refactoring, plugin-extensible AI rules

### Debugging & Testing
- **#44 / #90 Integrated Debugger** — `DebuggerService` (StartDebug, StepInto/Over/Out, Evaluate), `BreakpointsManager`, `WatchPanel`, `CallStackPanel`; supports scripts, plugins and workspace multi-projects via EventBus
- **#95 Unit Testing Panel** — `TestManager`, `TestRunner`, `TestResultPanel`; auto-detect NUnit/JUnit/PyTest; run by file/project/workspace; scaffolding from workspace templates

### Source Control
- **#91 Git Integration** — `GitManager`, `GitPanel` (commit/push/pull/branch); inline gutter diff; `GitEventAdapter` for file-change notifications; plugin hooks for pre-commit linters

### Code Analysis & Refactoring
- **#94 Advanced Refactoring** — `RefactoringManager`, `ASTAnalyzer`; rename symbol (workspace-wide), extract method/class, inline variable, move file between projects; AI-assisted suggestions
- **#96 Code Analysis & Metrics** — `CodeAnalysisManager`, `DependencyGraphEngine`, `MetricsCalculator`; cyclomatic complexity, code duplication, dependency graphs; dedicated panel with filter/sort

### Performance
- **#97 Large File Optimization** — `VirtualizationEngine`, `LazyParser`, `MultiThreadedIntelliSenseAdapter`; virtualized display for >1 GB files, incremental parsing, multi-core IntelliSense, workspace memory management

### Collaboration & UX
- **#98 Multi-User Collaboration** — `CollaborationManager`, `DocumentSyncEngine`; multi-cursor real-time editing, contextual chat/comments per line, EventBus integration
- **#99 Advanced UI/UX** — `NotificationManager`, `WorkspaceLayoutAdapter`; contextual inline notifications, layout persistence per workspace, full docking for all panels
- **#100 Internationalization / Localization** — `LocalizationManager`, `TranslationLoader`; EN/FR initial, plugin-provided translations, dynamic switching per workspace

### MSBuild Remaining
- **#103 MSBuild API Integration** — incremental build targets, full embedded MSBuild API, errors and warnings surfaced in Error Panel with file/line navigation (build output routing done in v0.5.0; targets API and Error Panel integration pending)

---

## [0.4.0] — 2026-03-16 — Plugin Architecture v2: EventBus, Lazy Loading, Capability Registry, Extension Points & Dependency Graph

### ✨ Added — WpfHexEditor.Events (new project)

- **`WpfHexEditor.Events`** — new `net8.0` leaf project (no WPF dependency); referenced by SDK, PluginHost, and App
- **`IDEEventBase`** — abstract record base for all IDE events: `EventId`, `Source`, `Timestamp`, `CorrelationId`
- **`IDEEventContext`** — per-publish context: `PublisherPluginId`, `IsFromSandbox`, `CancellationToken`
- **`IIDEEventBus`** — typed pub/sub interface with sync and async publish; context-aware subscribe overloads; `IEventRegistry EventRegistry` diagnostics accessor
- **`IDEEventBus`** — implementation: `ReaderWriterLockSlim`, weak-reference handler entries, lazy purge on publish, rolling event log (last 100 entries), `GetLog()` / `ClearLog()` for diagnostics
- **`IEventRegistry` / `EventRegistry`** — subscriber count tracking per event type; `GetAllEntries()` for options page

**10 built-in IDE event types** (`WpfHexEditor.Events.IDEEvents`):
- `FileOpenedEvent` — `FilePath`, `FileExtension`, `FileSize`
- `FileClosedEvent` — `FilePath`
- `WorkspaceChangedEvent` — `WorkspacePath`, `PreviousWorkspacePath`
- `PluginLoadedEvent` — `PluginId`, `PluginName`, `IsolationMode`
- `PluginUnloadedEvent` — `PluginId`
- `EditorSelectionChangedEvent` — `Offset`, `Length`, `SelectedBytes`
- `DocumentSavedEvent` — `FilePath`
- `TerminalCommandExecutedEvent` — `Command`, `ShellType`
- `BuildStartedEvent`, `BuildSucceededEvent`, `BuildFailedEvent`

### ✨ Added — Feature 1: Lazy Loading (#77)

- **`PluginActivationConfig`** (SDK) — manifest sub-model: `FileExtensions`, `Commands`, `OnStartup`; plugins with `onStartup: false` remain dormant until activated
- **`PluginState.Dormant`** (SDK) — new state between `Unloaded` and `Loading`; dormant plugins are discovered but not loaded
- **`PluginManifest.Activation`** (SDK) — optional `PluginActivationConfig?` property; backward compatible (null = eager load)
- **`PluginActivationService`** (PluginHost) — watches `FileOpenedEvent` on `IDEEventBus`; triggers `LoadPluginAsync` for dormant plugins matching file extension or command triggers; prevents double-activation via `HashSet<string>`
- **`WpfPluginHost`** — `LoadAllAsync` partitions plugins into startup vs dormant; `RegisterDormantPlugin()`; wires `_activationService`
- **Plugin Manager UI** — `IsDormant`, `ActivationTriggerLabel`, `LoadNowCommand` on `PluginListItemViewModel`; dormant badge (purple) + lazy info card with "Load Now" button in `PluginManagerControl.xaml`

### ✨ Added — Feature 2: ALC Isolation Diagnostics

- **`PluginAssemblyConflictInfo`** (SDK) — `record(AssemblyName, HostVersion, RequestedVersion, DetectedAt)`
- **`PluginLoadContext`** (PluginHost) — `LoadedAssemblies` list, `DependencyConflictDetected` event, `CreateWeakReference()`; host version always wins on conflict
- **`PluginEntry`** — `LoadContextWeakRef` (`WeakReference<PluginLoadContext>`), `AssemblyConflicts` list, `LoadedAssemblyCount`
- **Plugin Manager UI** — ALC metrics card (InProcess only): assembly count + conflict count; expandable conflicts list; `ZeroToGreenNonZeroToOrange` converter

### ✨ Added — Feature 3: Capability Registry

- **`PluginFeature`** (SDK) — `static class` with `const string` well-known feature names: `HexViewOverlay`, `BinaryAnalyzer`, `PEParser`, `DisassemblyProvider`, `DecompilerProvider`, `FormatDetector`, `StructureTemplate`, `ScriptEngine`, `TerminalExtension`
- **`IPluginCapabilityRegistry`** (SDK) — `FindPluginsWithFeature()`, `PluginHasFeature()`, `GetFeaturesForPlugin()`, `GetAllRegisteredFeatures()`
- **`PluginCapabilityRegistry`** (PluginHost) — live queries over `WpfPluginHost._entries`; no caching
- **`PluginCapabilityRegistryAdapter`** (PluginHost) — lazy wrapper resolving circular dependency; `.SetInner()` called after host construction
- **`PluginManifest.Features`** (SDK) — `List<string>` feature declarations; e.g. `"features": ["HexViewOverlay", "BinaryAnalyzer"]`
- **`IIDEHostContext.CapabilityRegistry`** (SDK) — exposes capability registry to all plugins
- **Plugin Manager UI** — feature chip strip (`WrapPanel` of pill `Border` elements) in plugin detail pane

### ✨ Added — Feature 4: IDE EventBus Integration

- **`IIDEHostContext.IDEEvents`** (SDK) — exposes `IIDEEventBus` to plugins; sandbox plugins receive events via IPC bridge
- **`SandboxPluginProxy`** — `WireIDEEventBridgeToSandbox()`: subscribes to `FileOpenedEvent` + `EditorSelectionChangedEvent` on `IDEEventBus` and forwards to sandbox process via named-pipe `IDEEventNotification` messages
- **`SandboxedPluginRunner`** — `HandleIDEEventNotification()`: deserializes event payload, resolves concrete type from `WpfHexEditor.Events` assembly, publishes to `SandboxLocalEventBus` via reflection
- **`IDEEventBusOptionsPage.xaml`** (PluginHost) — new Options page under `Plugin System > Event Bus`: rolling event log (last 100), subscriber count table per event type, "Clear log" button; full theme compliance
- **`WpfPluginHost`** — publishes `PluginLoadedEvent` / `PluginUnloadedEvent` on every load/unload
- **`MainWindow.PluginSystem.cs`** (App) — constructs `IDEEventBus`, registers 10 well-known events in registry, wires `FileOpened` → `FileOpenedEvent`, `SelectionChanged` → `EditorSelectionChangedEvent`

### ✨ Added — Feature 5: Extension Points

- **Extension point contracts** (SDK, `WpfHexEditor.SDK.ExtensionPoints`):
  - `IFileAnalyzerExtension` — `Task<FileAnalysisResult> AnalyzeAsync(string filePath, CancellationToken ct)` + `FileAnalysisResult` record
  - `IHexViewOverlayExtension` — `IEnumerable<HexOverlayRegion> GetOverlays(byte[] data, long offset, long length)` + `HexOverlayRegion` record
  - `IBinaryParserExtension` — `ParsedStructure? TryParse(Stream data, string fileExtension)` + `ParsedStructure` record
  - `IDecompilerExtension` — `Task<string> DecompileAsync(byte[] data, CancellationToken ct)`
  - `IExtensionWithContext` — optional init interface; `Initialize(IIDEHostContext context)` called by host after instantiation
- **`ExtensionPointCatalog`** (SDK) — static `IReadOnlyDictionary<string, Type>` mapping well-known point names to contract types
- **`IExtensionRegistry`** (SDK) — `GetExtensions<T>()`, `Register<T>()`, `UnregisterAll(pluginId)`, `GetAllEntries()`
- **`ExtensionRegistry`** (PluginHost) — thread-safe impl (`ReaderWriterLockSlim`); snapshot on `GetExtensions<T>()` prevents mutation during iteration
- **`PluginManifest.Extensions`** (SDK) — `Dictionary<string, string>` mapping extension point name → fully-qualified class name in plugin assembly
- **`WpfPluginHost`** — `RegisterExtensionContributions()` after `InitializeAsync`; `ExtensionRegistry.UnregisterAll()` during `UnloadPluginAsync`
- **`IIDEHostContext.ExtensionRegistry`** (SDK) — exposes extension registry to all plugins
- **Plugin Manager UI** — extensions chip strip in plugin detail pane; `Extensions` / `HasExtensions` on `PluginListItemViewModel`
- **`MainWindow.PluginSystem.cs`** — wires file-open handler to call `GetExtensions<IFileAnalyzerExtension>()` and routes results to Output Panel

### ✨ Added — Feature 6: Plugin Dependency Graph

- **`PluginDependencySpec`** (SDK) — parsed versioned constraint: `PluginId` + `PluginVersionConstraint`; `Parse("BinaryAnalysisCore >=1.0")` — backward compatible with plain IDs
- **`PluginVersionConstraint`** (SDK) — single-class parser; operators `>=`, `>`, `<=`, `<`, `=`, `^`; `bool Satisfies(Version candidate)`; no NuGet dependency
- **`PluginDependencyGraph`** (PluginHost) — adjacency-list graph with forward + reverse edges; `Build()`, `GetLoadOrder()`, `GetDependents()`, `GetCascadedUnloadOrder()`, `GetCascadedReloadOrder()`, `Validate()`; `DependencyValidationError` record with `DependencyErrorKind` (Missing | VersionMismatch | Circular)
- **`WpfPluginHost`** — replaces inline `TopologicalSort()` with `_dependencyGraph.GetLoadOrder()`; `CascadingUnloadAsync()` and `CascadingReloadAsync()`; plugins with unmet dependencies marked `Incompatible` and skipped at startup
- **`PluginManifest.Dependencies`** — existing `List<string>` now supports versioned syntax: `"BinaryAnalysisCore >=1.0"`; plain IDs still work (any version)
- **Plugin Manager UI** — `DependsOn` chip list (green=satisfied, red=missing), `DependedOnBy` list, `HasUnresolvedDeps`; Cascade Unload / Cascade Reload buttons; dependency section in right pane

### 🔧 Changed

- All 10 plugin `.csproj` files — `<PluginIsolationMode>` changed from `Sandbox` → `Auto`; `Auto` is now the correct default (host decides InProcess vs Sandbox based on trust level and capability declarations)
- `StandaloneIDEHostContext` (Sample.Terminal) — implements 3 new `IIDEHostContext` members (`IDEEvents`, `CapabilityRegistry`, `ExtensionRegistry`) with null-object stubs
- `ExtensionRegistry` — promoted from `internal` to `public` (required by `App` project)
- `IDEEventBusOptionsPage` — uses concrete `IDEEventBus` type (diagnostics methods not on interface)

---

## [0.3.0] — 2026-03-15 — Plugin System Phase 5-12, Sandbox & IDE Enhancements

### ✨ Added — Plugin Sandbox (Phases 9–12)

- **HWND Embedding** — out-of-process plugin UI surfaces embedded directly into the IDE docking system via Win32 HWND parenting; plugins run fully isolated but appear native
- **IPC Menu/Toolbar Bridge** — sandbox plugins register menus, toolbar items, and options pages in the host IDE via named-pipe IPC; `SandboxPluginProxy` dispatches menu clicks back to plugin process
- **IPC HexEditor Event Bridge** (Phase 12) — `SelectionChanged`, `FileOpened`, `FormatDetected`, and `ViewportScrolled` events forwarded from host to sandbox plugins; `ParsedFields` panel refresh now works from sandbox context
- **`SandboxJobObject`** — Windows Job Object wrapper constraining sandbox process CPU and memory usage; enforces per-plugin resource budgets with configurable limits
- **Auto Isolation Mode / Decision Engine** — `PluginMigrationPolicy` evaluates plugin trust level, crash history, and resource usage to automatically select `InProcess` vs `Sandbox` isolation; no manual configuration required
- **`PluginMigrationMonitor`** — tracks plugin migrations between isolation modes; exposes upgrade/downgrade history, trigger reasons, and stability metrics; observable from Plugin Manager

### ✨ Added — Plugin Options UI

- `UI/Options/` — dedicated options sub-panel within Plugin Manager for per-plugin sandbox configuration, migration policy overrides, and resource budget thresholds
- `AssemblyExplorerOptionsPage.xaml` — updated options page with sandbox-compatible bindings
- `DataInspectorOptionsPage.xaml` — updated options page with sandbox-compatible bindings

### ✨ Added — Plugin Manager Improvements

- `PluginListItemViewModel` — extended with `IsolationMode`, `MigrationStatus`, `SandboxHealth`, `LastMigrationReason`; badge indicators for auto-isolation decisions
- `PluginManagerControl.xaml` — isolation mode column, migration history button, sandbox health indicator in list; options sub-panel per plugin
- `PluginManagerViewModel` — wires migration monitor; exposes `MigrationHistoryCommand`; auto-refreshes on isolation mode change

### ✨ Added — Themes

- All 8 theme `Colors.xaml` files updated — new brush tokens for sandbox health badges, isolation mode indicators, and plugin options UI

### 🧪 Added — Tests

- `PluginMigrationMonitor_Tests` — unit tests covering migration trigger logic, history accumulation, and observer notification
- `PluginMigrationPolicy_Tests` — unit tests covering decision engine: trust thresholds, crash-rate fallback, manual override precedence

---

### ✨ Added — Plugin System (5 new projects)

**`WpfHexEditor.SDK`** — public plugin contract layer
- `IWpfHexEditorPlugin` / `IWpfHexEditorPluginV2` — plugin lifecycle (Init / Activate / Deactivate / Dispose)
- `IIDEHostContext` — full IDE access gateway for plugins
- `IUIRegistry` — panel registration, Show / Hide / Toggle / Focus API
- `IDockingAdapter` — ShowDockablePanel / HideDockablePanel / ToggleDockablePanel / FocusDockablePanel
- Service contracts: `IHexEditorService`, `ICodeEditorService`, `IOutputService`, `IErrorPanelService`, `ISolutionExplorerService`, `IParsedFieldService`
- `IPluginWithOptions` — optional interface; plugins exposing it get an auto-registered Options page under "Plugins"
- `IFocusContextService`, `IPluginEventBus`, `IPluginState`, `IPluginDiagnostics`, `IPermissionService`, `IMarketplaceService`, `IThemeService`
- `PluginManifest` with `ResolvedDirectory` (runtime-only, `JsonIgnore`)

**`WpfHexEditor.PluginHost`** — runtime host infrastructure
- `WpfPluginHost` — discovery (`%AppData%\WpfHexEditor\Plugins\` + `bin\Plugins\`), load, unload, enable/disable
- `PluginEntry` — internal mutation API: `SetState`, `SetInstance`, `SetInitDuration`, `SetLoadedAt`, `SetFaultException`, `Unload()` (clears collectible `AssemblyLoadContext`)
- `PluginCrashHandler` — `HandleCrash` + async `HandleCrashAsync()` overload
- `PluginWatchdog` — `WrapAsync()` returns `Task<TimeSpan>` (elapsed time)
- `PluginOptionsRegistry` — thread-safe runtime registry of plugin options pages keyed by plugin ID
- `PluginLoadContext` — collectible `AssemblyLoadContext` for hot-unload
- `PluginManifestValidator`, `PluginDiagnosticsCollector`, `PluginScheduler`, `PluginEventBus`, `PluginStateSerializer`
- `PermissionService` — `PermissionChangedEventArgs` via object-initializer (PluginId, Permission, IsGranted)
- `UIRegistry` — delegates Show/Hide/Toggle/Focus to `IDockingAdapter`
- `PluginManagerControl.xaml` — enable/disable/uninstall actions, live status, `PluginListItemViewModel` (LoadedAt, AssemblyPath, OptionsPageFactory)
- `SandboxPluginProxy` — out-of-process proxy over named-pipe `IpcChannel`

**`WpfHexEditor.PluginSandbox`** — isolated host process (stub, `net8.0-windows`)
- Console host entry point; named-pipe IPC with main IDE process

**`WpfHexEditor.Core.Terminal`** — command engine (31 built-in commands)
- Category **Core**: `clear`, `echo`, `exit`, `help`, `history`, `version`
- Category **File System**: `copy-file`, `delete-file`, `list-files`, `open-file`, `open-folder`
- Category **Editor**: `close-file`, `read-hex`, `save-file`, `save-as`, `search`, `select-file`, `write-hex`
- Category **Project / Solution**: `close-project`, `close-solution`, `open-project`, `open-solution`, `reload-solution`
- Category **Panels**: `append-panel`, `clear-panel`, `close-panel`, `focus-panel`, `open-panel`, `toggle-panel`
- Category **Plugins**: `plugin-list`, `run-plugin`
- Category **Diagnostics**: `send-error`, `send-output`, `show-errors`, `show-logs`, `status`
- `HxScriptEngine` — executes `.hxscript` files; `CommandHistory` with persistence

**`WpfHexEditor.Terminal`** — WPF dockable terminal panel
- `TerminalPanel.xaml` — VS-style dockable panel, prompt input, colored output
- `TerminalPanelViewModel` — implements both `ITerminalContext` and `ITerminalOutput`; `TerminalOutputLine` with severity-to-brush converter

**7 First-party plugin packages** (`Sources/Plugins/`)
- `WpfHexEditor.Plugins.DataInspector` — wraps DataInspectorPanel; implements `IPluginWithOptions` (`DataInspectorOptionsPage`: display format, endianness, auto-refresh interval)
- `WpfHexEditor.Plugins.StructureOverlay` — wraps StructureOverlayPanel
- `WpfHexEditor.Plugins.FileStatistics` — wraps FileStatisticsPanel
- `WpfHexEditor.Plugins.PatternAnalysis` — wraps PatternAnalysisPanel
- `WpfHexEditor.Plugins.FileComparison` — wraps FileComparisonPanel
- `WpfHexEditor.Plugins.ArchiveStructure` — wraps ArchiveStructurePanel
- `WpfHexEditor.Plugins.CustomParserTemplate` — wraps CustomParserTemplatePanel

**Packaging tools** (`Sources/Tools/`)
- `WpfHexEditor.PackagingTool` — `whxpack` CLI, `ManifestFinalizer` (SHA-256), `PackageBuilder` → `.whxplugin` (ZIP)
- `WpfHexEditor.PluginInstaller` — WPF installer dialog, `PluginPackageExtractor`, `--silent` mode, `PluginInstallException`

### ✨ Added — App Integration (Plugin System)
- `MainWindow.PluginSystem.cs` — `InitializePluginSystemAsync`, `ShutdownPluginSystemAsync`, `UpdatePluginFocusContext`
- `OnOpenTerminal()` — creates `TerminalPanel` + `TerminalPanelViewModel`, docks as `"panel-terminal"`
- `OnOpenPluginManager()` — docks `PluginManagerControl` as `"plugin-manager"`
- **Tools menu** — Plugin Manager + Terminal entries; toolbar separators with `DataTrigger`-controlled visibility
- Dynamic Options registration — each plugin implementing `IPluginWithOptions` gets `OptionsPageRegistry.RegisterDynamic()` on load; `UnregisterDynamic()` on disable/unload

### ✨ Added — Options Module
- `HexEditorBehaviorPage.xaml(.cs)` — new Options page: data interpretation, scroll markers, advanced behavior
- `HexEditorStatusBarPage.xaml(.cs)` — new Options page: status bar element visibility toggles
- `PluginSystemOptionsPage.xaml(.cs)` — plugin loading settings, sandbox mode, auto-update policy
- `OptionsPageRegistry.RegisterDynamic(category, pageName, factory)` / `UnregisterDynamic(pageId)` — runtime plugin options page registration
- `AppSettings` — expanded with `SolutionExplorerSettings`, `CodeEditorDefaultSettings`, `TextEditorDefaultSettings`, plugin system settings (~30 new properties)

### ✨ Added — Service Layer
- `DockingAdapter` — `ShowDockablePanel`, `HideDockablePanel`, `ToggleDockablePanel`, `FocusDockablePanel` (uses `DockItemState.Hidden` check)
- `ErrorPanelServiceImpl` — `PostDiagnostic` now accepts `DiagnosticSeverity` enum (was string); maps SDK→Core severity; uses `AddSource`
- `SolutionExplorerServiceImpl` — `OpenFileHandler` delegate avoids circular App↔SDK reference
- `HexEditorServiceImpl` — `ReadBytes`, `GetSelectedBytes`, `SearchHex`, `SearchText`, `GoToOffset`

### ✨ Added — Docking Improvements
- `AutoHideBar` — `SnapshotReady` event (fires after open animation + Render priority) and `Dismissing` event (before close animation)
- `DockControl` — wires `SnapshotReady`/`Dismissing`; `MinCaptureSize` guard in `CaptureAutoHideSnapshot`
- `TabHoverPreview` — title-only fallback card for never-activated tabs; hides popup on selected tab; title from `DockItem.Tag`
- `AutoHideBarHoverPreview` — button-relative placement replaces `PlacementMode.Mouse` (Win32 layered window bug)
- `DockTabControl` — float threshold check moved before reorder-dispatch; vertical drag cancels reorder and floats instead
- `DockDragManager` — guard for `DocumentTabHost.Node == null`

### ✨ Added — Solution Explorer Improvements
- **Expand-state persistence** — captures/restores `IsExpanded` per node across `Rebuild()` in same session
- **Drag & drop from Windows Explorer** — `DataFormats.FileDrop` handled; files added to project without dialog
- **Delete from disk** — `ItemDeleteFromDiskRequested` event; moves to Recycle Bin with confirmation

### ✨ Added — HexEditor Enhancements
- `HexEditor.StatusBarContributor` — `RaiseHexStatusChanged()` fired on `ByteGrouping`, `OffSetStringVisual`, `DefaultCopyToClipboardMode` property changes
- net48 compatibility — `(CopyPasteMode[])Enum.GetValues(typeof(CopyPasteMode))` (generic `Enum.GetValues<T>()` not available in net48)

### ✨ Added — Format-Aware Editor Selection
- `EmbeddedFormatCatalog` — parses `preferredEditor` and `detection.isTextFormat` from `.whfmt` JSON
- `EmbeddedFormatEntry` record — `string? PreferredEditor` and `bool IsTextFormat`
- `EditorRegistry.FindFactory(string filePath, string? preferredId)` — preferred-first, fallback first-match
- 427 `.whfmt` format definitions annotated with `"preferredEditor"` key
- `MainWindow.GetPreferredEditorId()` — consults `EmbeddedFormatCatalog` (PreferredEditor → IsTextFormat → null)

### ✨ Added — Image Viewer
- `IImageTransform` + `ImageTransformPipeline` — composable transform architecture
- Transforms: `RotateImageTransform`, `FlipImageTransform`, `CropImageTransform`, `ResizeImageTransform`, `ScaleImageTransform`
- `ResizeImageDialog.xaml(.cs)` — width/height input with aspect ratio lock
- `ImageContextMenu.xaml` — right-click context menu (copy, save, zoom, transform actions)
- `ImageViewer.xaml(.cs)` — major enhancement: zoom/pan, transform toolbar, context menu, theme compliance
- `DockEngine` / `FloatingWindow` — minor additions for ImageViewer floating window sizing

### ✨ Added — Plugin Monitoring Panel (Phases 1–5 complete)
- `PluginMonitoringViewModel` — observes `PluginEntry` collection, polls CPU% and memory MB at 1 s interval via `PerformanceCounter` / GC; rolling 60-point chart history; per-plugin CPU estimation (`processCpu × avgMs / sumAvgMs`)
- `PluginMonitoringPanel.xaml(.cs)` — VS-style dockable panel; global `Canvas`+`Polyline` charts + per-plugin `SparklineControl` columns in DataGrid; detail pane with 4-tab `TabControl` (Overview / CPU+RAM / Permissions / Settings); alert badge in toolbar; export dropdown (CSV / JSON / event log / copy table); drag-drop `.whxplugin` install
- `SparklineControl` — new `FrameworkElement`-based mini chart (no external lib); renders CPU and RAM rolling sparklines directly via `OnRender`; bound to `PluginMiniChartViewModel` (60-point history)
- `PluginPermissionRowViewModel` — interactive permissions editor: toggles `IsGranted` → calls `PermissionService.Grant/Revoke` immediately; risk badge (High/Medium/Low); declared vs granted columns
- `PluginAlertEngine` — debounced per-plugin threshold evaluation (CPU%, MemMB, ExecTimeMs); 60 s cooldown per plugin per metric; raises `AlertTriggered` event consumed by toolbar badge
- `PluginDiagnosticsExporter` — exports metrics to CSV, full diagnostics to JSON, crash summary to plain text; no external NuGet; triggered via toolbar export commands
- `WpfPluginHost` — expanded load/unload/enable/disable lifecycle; diagnostic hooks; hot-unload via collectible `AssemblyLoadContext`
- `PluginManagerViewModel` — full VM with Enable/Disable/Uninstall/Reload commands, filter/search, `SelectionChanged` wiring to monitoring panel; drag-drop `.whxplugin` install via `PluginManagerControl`
- `PluginManagerControl` — DataGrid with status column, toolbar actions, details pane with sparkline charts; drag-drop install dropzone overlay; `PluginListItemViewModel` extended with `LiveCpuPercent`, `LiveMemoryMb`, `DiagnosticsSummary`, sparkline history
- Plugin `.csproj` files — SDK manifest metadata (`PluginId`, `PluginVersion`, `PluginEntryPoint`, …) for auto-generated `manifest.json` at build time

### ✨ Added — Options
- `PluginSystemOptionsPage` — complete settings page: monitoring enabled/interval, sandbox mode, auto-update, trusted publisher; bound to `AppSettings.PluginSystemSettings`
- `AppSettings.PluginSystemSettings` — `MonitoringEnabled`, `MonitoringIntervalMs`, `MaxHistoryPoints`, sandbox policy

### ✨ Added — Terminal Panel Overhaul
- `TerminalMode` enum — `Interactive` / `Script` / `ReadOnly` modes with visual indicator
- `TerminalExportService` — export full session to `.txt` / `.log` file with timestamp header
- `ITerminalService` (SDK) — new service contract exposing `Execute`, `Clear`, `ExportSession`, `HistoryLines`; consumed by plugins via `IIDEHostContext.TerminalService`
- `TerminalServiceImpl` — App-side implementation wiring `TerminalPanelViewModel` to the SDK contract; registered in `IDEHostContext`
- `ITerminalOutput.WriteTable()` — helper for tabular output (header + rows) with column alignment
- `TerminalCommandRegistry` — new built-in commands: `export-log`, `set-mode`, `terminal-info`
- `TerminalPanelViewModel` — TerminalMode state machine; per-mode prompt styling; `ExportSession()` delegation to `TerminalExportService`; improved async command dispatch; `ITerminalContext` / `ITerminalOutput` merged into single VM
- `TerminalPanel.xaml` — mode indicator badge; export toolbar button; resize-handle polish; full keyboard history (↑/↓) wired to `CommandHistory`
- `IIDEHostContext.TerminalService` — plugins can now read/write terminal programmatically
- `PluginCapabilities.Terminal` — new capability flag; plugins declare terminal access requirement in manifest
- `IDEHostContext` / `PluginHost.IDEHostContext` — exposes `ITerminalService`; `PluginPermission.Terminal` entry

### ✨ Added — PropertiesPanel Auto-Refresh
- `HexEditor.PropertyProvider` — `DependencyPropertyDescriptor.AddValueChanged` on `SelectionStartProperty` / `SelectionStopProperty`; bypasses anti-recursion guard that silently dropped `SelectionChanged` during keyboard navigation
- 400 ms one-shot `DispatcherTimer` debounce — `PropertiesChanged` fires only after cursor is idle; eliminates synchronous `FileStream` + entropy calls on every keypress
- `BuildDocumentGroup()` — `EditMode` bound as `enum` (not `.ToString()`); ComboBox `SelectedItem` now matches `AllowedValues` correctly (was always empty)

### 🔧 Changed
- `PluginEntry` promoted from `internal` to `public`
- Section separators normalized from Unicode box-drawing (`──`) to ASCII (`--`) across 134 files
- `MainWindow.xaml` — Tools menu: Plugin Monitoring entry added; `PluginMonitoringPanel` dock item registered
- `SolutionExplorerPanel` — toolbar layout polish; drag-drop from Windows Explorer refined
- Plugin `.csproj` SDK manifest metadata (`PluginId`, `PluginVersion`, `PluginEntryPoint`, …) updated and bumped across all 7 first-party plugin packages
- `MainWindow` — `SetActiveEditor` now called only on hex editor tab activation; non-hex tabs (Code, JSON, TBL…) no longer reset the active editor reference, keeping panels connected
- `PluginManagerControl` — tab hover / selected theming aligned with all 8 global themes; `RelayCommand` wired to enable/disable/uninstall toolbar actions; list item foreground driven by `DockMenuForegroundBrush`

### ✨ Added — Output Service
- `OutputPanel.GetRecentLinesFromSource(source, count)` — returns the last N lines from a specific source channel as plain strings
- `OutputPanel.ActiveSource` — exposes the currently selected source channel name
- `OutputLogger.GetRecentLines(count)` — reads recent lines from the active source; thread-safe via `Dispatcher.Invoke`
- `OutputServiceImpl.GetRecentLines()` — fully wired (was a `TODO` stub returning empty list)

### ✨ Added — Format Definitions
- Source code format definitions (`Programming/SourceCode/`) — `.whfmt` files for Shell, Bash, PowerShell, Batch, and related scripting formats; extends the 427-entry embedded format catalog

### 🐛 Fixed
- `PermissionService` — `PermissionChangedEventArgs` object-initializer form; missing `System.IO` using
- `PluginManifestValidator` — constructor signature and `result.IsValid` (was `!result.HasErrors`)
- `SolutionExplorerPanel` — CS0136 variable scope collision in `OnTreeDragOver`
- `HexEditor.Events.cs — EnsurePositionVisible` DOWN scroll: replaced centering formula (`lineNumber - visibleLines/2`) with `LastVisibleBytePosition`-based edge trigger; scroll now advances exactly 1 line per arrow-down keypress at viewport bottom; `VerticalScroll.Value` synced in all scroll paths
- `ImageViewer` — `FileShare.ReadWrite` (was `Read`) allows concurrent access when HexEditor has the same file open; eliminates `IOException` on dual open
- `MainWindow` — panels (ParsedFields, DataInspector, Properties…) now auto-refresh on file open; panel refresh dispatched non-blocking via `Dispatcher.InvokeAsync(DispatcherPriority.Background)`
- `PluginMonitoringPanel` / `TerminalPanel` — panels showed placeholder on layout restore; `BuildContentForItem` now routes `TerminalPanelContentId` and `PluginMonitorContentId` to their factories; `DataContext` wired via deferred `Dispatcher.InvokeAsync` after `InitializePluginSystemAsync`
- `WelcomePanel` — `## What's Next` roadmap section was silently dropped by the changelog parser (regex required `## [Label]` brackets); special-cased before regex; rendered as distinct purple roadmap block, never counted against `MaxVersionsDisplayed`
- VisualStudio theme — `PM_*` brush keys corrected to match VS2022 palette; theme file was referencing wrong color tokens
- `BinaryAnalysisPanel` removed from solution (replaced by ParsedFields plugin); dangling project reference cleaned up

### 🐛 Fixed — DataInspector — ActiveView Scope + SDK Viewport API (2026-03-08) — Issue #171

- **DataInspector not reactive to viewport scroll** — Added `ActiveView` as a third `DataScope` value. The panel subscribes to `IHexEditorService.ViewportScrolled` when active, with a `_viewportRefreshPending` coalescing flag to prevent Dispatcher flooding during fast scroll. `_wholeFileChartLoaded` flag prevents full-file reload on every `SelectionChanged`. *(Fixes #171)*
- **SDK — viewport API** — `IHexEditorService` now exposes `long FirstVisibleByteOffset`, `long LastVisibleByteOffset`, and `event EventHandler ViewportScrolled`. `HexEditorServiceImpl` subscribes to `HexEditor.VerticalScrollBarChanged` and forwards it via `ViewportScrolled`.
- **Chart position configurable** — Toolbar combo (Left / Right / Top / Bottom) persisted in `DataInspectorOptions`. `RebuildLayout(ChartPosition)` dynamically reconfigures `MainAreaGrid` row/column definitions and `Grid` attached properties on `ChartContainer`, `ChartSplitter`, and `ListContainer`.
- **Files:** `IHexEditorService.cs`, `HexEditorServiceImpl.cs`, `DataInspectorPanel.xaml(.cs)`, `DataInspectorOptions.cs`, `DataInspectorOptionsPage.xaml(.cs)`, `DataInspectorPlugin.cs`

### 🐛 Fixed — Status Bar (2026-03-08) — Issues #168 #169 #172 #173 #174

- **File Size missing from IDE status bar** — `HexEditor.StatusBarContributor` now exposes a `_sbFileSize` item (first position, read-only) reporting `VirtualLength` via `FormatFileSize()`. Refreshed on every file operation / selection change / mode change via `RaiseHexStatusChanged()`. *(Fixes #169, #172)*
- **Stale status bar values on tab switch** — Added `void RefreshStatusBarItems()` to `IStatusBarContributor`. All editors implement it (`HexEditor` → `RefreshStatusBarItemValues()`, `TblEditor`, `CodeEditor`, `ImageViewer`, `EmptyStatusBarContributor` no-op). `OnActiveDocumentChanged` now calls it unconditionally. `RefreshText.Text` cleared when a non-hex editor becomes active. *(Fixes #173)*
- **Split-pane focus does not update status bar** — `StoreContent()` subscribes to `UIElement.IsKeyboardFocusWithinChanged` for all non-panel content. On focus-enter, `SyncStatusBarToFocusedEditor(contentId)` performs a lightweight sync (updates `ActiveDocumentEditor`, `ActiveStatusBarContributor`, calls `RefreshStatusBarItems()` + `SyncActiveDocument()`). Skips if the same contributor/editor is already active. *(Fixes #168, #174)*
- **Files:** `IStatusBarContributor.cs`, `HexEditor.StatusBarContributor.cs`, `MainWindow.xaml.cs`, `TblEditor.xaml.cs`, `CodeEditor.cs`, `ImageViewer.xaml.cs`
- **Commit:** `05c19630`

### 🐛 Fixed — Docking (2026-03-08) — Issue #170

- **Outer/root-level dock support** — `DockEngine.DockAtRoot(item, direction)` wraps `Layout.RootNode` entirely via `WrapWithSplit()`, creating true full-width/height panels outside all side panels (like VS outer dock targets). `DockEdgeOverlayWindow` rewritten with dual indicator sets: outer (36 px, at window edge → root-level) and inner (28 px, at document host → existing behaviour). `HitTestEx()` returns `(Direction, IsOuter)`. `DockDragManager` uses `_isOuterEdgeDrop` flag and `ComputeInnerBoundsInCenterHost()` to route `OnFloatingMouseUp` to `DockAtRoot()` when outer. *(Fixes #170)*
- **Drop preview mismatch** — Inner hits now snap preview to `DocumentTabHost` bounds; outer hits snap to `CenterHost` bounds, matching the actual panel width created.
- **Files:** `DockEngine.cs`, `DockEdgeOverlayWindow.cs`, `DockDragManager.cs`

### 🐛 Fixed — Reset Layout / DockingAdapter (2026-03-08)

- **Open documents lost after Reset Layout** — `OnResetLayout` now snapshots open `doc-*` DockItems before calling `SetupDefaultLayout()` and re-docks them after. `_contentCache.Clear()` removed from the reset path to preserve editor instances.
- **Plugin menus broken after Reset / Load Layout** — `DockingAdapter` fields (`_engine`, `_layout`) made non-readonly; `_allKnownPanels` dict tracks every registered panel. New `RebindLayout(engine, layout)` updates refs and re-defers panels absent from the new layout. `ApplyLayout()` calls `_dockingAdapter?.RebindLayout(...)` after every layout change.
- **Files:** `DockingAdapter.cs`, `MainWindow.PluginSystem.cs`, `MainWindow.xaml.cs`

### ✨ Added — Document Lifecycle Service (2026-03-08)

- `DocumentModel` — observable per-tab state: `Title`, `IsDirty`, `CanUndo`, `CanRedo`, `IsBusy`, `IsReadOnly`, `IsActive`. Wires `IDocumentEditor` events → properties via `AttachEditor` / `DetachEditor`.
- `IDocumentManager` / `DocumentManager` — central document registry: `Register`, `Unregister`, `AttachEditor`, `SetActive`, `GetDirty`; typed events: `DocumentRegistered`, `DocumentUnregistered`, `ActiveDocumentChanged`, `DocumentDirtyChanged`, `DocumentTitleChanged`.
- `MainWindow.DocumentModel.cs` — `InitDocumentManager()`, `RegisterDocumentFromItem()`, `UnregisterDocument()`, `SyncActiveDocument()`; routes `DocumentTitleChanged` → `DockItem.Title`; `DocumentDirtyChanged` → `CommandManager.InvalidateRequerySuggested()`.
- `CollectAllDirtyItems` now queries `_documentManager.GetDirty()` (O(n) over models) instead of walking the layout tree.
- **Files:** `DocumentModel.cs`, `IDocumentManager.cs`, `DocumentManager.cs`, `MainWindow.DocumentModel.cs`, `MainWindow.xaml.cs`

### 🐛 Fixed — Plugin Monitor: metrics always 0 for event-driven plugins (2026-03-08) — Issue #176

- **Root cause** — `Diagnostics.Record(nonZeroTime)` was only called during Init / Shutdown / Reload. The periodic 5 s sampling tick passed `TimeSpan.Zero`. `AverageExecutionTime` explicitly filters zero-duration samples, so all plugins had `avgMs = 0` after startup except Archive Structure (4.2 ms init). After the previous weight-formula fix, plugins with `avgMs = 0` received `weight = 0` — correct math, wrong observable result: actively working plugins (e.g. Data Inspector analysing a 2 MB file) displayed 0% CPU / 0 MB RAM.
- **`TimedHexEditorService`** (new) — Proxy/Decorator over `IHexEditorService`. Intercepts the add accessors of all 5 events (`SelectionChanged`, `ViewportScrolled`, `FileOpened`, `FormatDetected`, `ActiveEditorChanged`). Times each plugin's handler invocation via `Stopwatch` and records the elapsed time to `PluginDiagnosticsCollector`. After a few selection changes the plugin accumulates non-zero `avgMs` and receives a proportional CPU/RAM share in the Plugin Monitor.
- **`PluginScopedContext`** (new) — Lightweight `IIDEHostContext` Decorator created once per plugin. Substitutes `HexEditor` with the timed proxy; all other 10 services delegate to the shared base context.
- **`WpfPluginHost.LoadPluginAsync`** — now creates `TimedHexEditorService` + `PluginScopedContext` per plugin and passes `pluginContext` to `InitializeAsync` instead of the shared `_hostContext`.
- **`PluginAlertEngine`** — Added `StartupGracePeriod` (30 s). Alerts suppressed while `Uptime < 30s`, eliminating false "CPU 100% exceeds threshold 50%" alerts fired for every plugin during batch init.
- **Files:** `TimedHexEditorService.cs` (new), `PluginScopedContext.cs` (new), `WpfPluginHost.cs`, `PluginAlertEngine.cs`
- **Commit:** `5c75f60c`

### 🐛 Fixed — Plugin Monitor (2026-03-08) ⚠️ Partial — #175 still open

- **Sparklines not updating** — `PluginMonitorRow.MiniChart` is now a proper observable property (backing field + `OnPropertyChanged()`). Row pre-assigns `MiniChart` before `Rows.Add()` to avoid null-binding on initial render.
- **CPU reading reflects idle process CPU, not startup spike** — `WpfPluginHost` added `_lastSampledCpuPercent` field updated exclusively in `OnSamplingTick`; `PluginMonitoringViewModel.Refresh()` reads `_host.LastSampledCpuPercent` instead of per-plugin ring-buffer first sample (which captured the ~100% startup spike).
- **Initial events not shown** — `_dispatcher` stored in VM constructor; `AddEvent()` guards mutations with `_dispatcher.CheckAccess()`; `SynthesizeInitialLoadEvents()` called at construction to replay already-loaded plugin events.
- **Weight fallback incorrectly distributes metrics** — Partial fix applied in `50f800ca`: non-loaded plugins → weight = 0; `sumExecMs > 0 && avgMs = 0` → weight = 0; `sumExecMs = 0` → equal share. **Issue #175 remains open** — further investigation needed for metric attribution accuracy under real multi-plugin load.
- **Files:** `PluginMonitoringViewModel.cs`, `WpfPluginHost.cs`

---

## [0.3.0 cont.] — 2026-03 — IDE & Project System

### ✨ Added — Project System
- **Solution & Project management** (`.whsln` / `.whproj` formats) with `SolutionManager` singleton
- **Virtual & physical folders** in Solution Explorer — `PhysicalFolderNodeVm`, `PhysicalFileNodeVm`
- **Show All Files** mode — scans disk recursively and shows untracked files at 45% opacity
- **Format versioning pipeline** — `IFormatMigrator` + `MigrationPipeline` (current version: 2)
- **V1→V2 format migration** — in-memory only, file never modified automatically
- **Atomic upgrade** — `UpgradeFormatAsync` creates `.v{N}.bak` backups before writing
- **Read-only format mode** — `ISolution.IsReadOnlyFormat` blocks saves on unupgraded files
- **File templates** — Binary, TBL, JSON, Text with `FileTemplateRegistry`
- **`IEditorPersistable`** interface — bookmarks, scroll, caret, encoding restored per file
- **`IItemLink`** — typed links between project items (e.g. `.bin` ↔ `.tbl`)

### ✨ Added — IDE Panels
- **Error Panel** (`IErrorPanel`) — VS-style diagnostics panel with severity filtering and navigation
- **`IDiagnosticSource`** interface — any editor can push diagnostics to the Error Panel
- **JsonEditor diagnostics** — real-time JSON validation errors forwarded to Error Panel
- **TblEditor diagnostics** — validation errors forwarded to Error Panel
- **`ERR_*` theme keys** — Error Panel colors in all 8 themes

### ✨ Added — Search
- **QuickSearchBar** — inline Ctrl+F overlay (VSCode-style), no dialog popup
- **Ctrl+Shift+F** → opens full AdvancedSearchDialog (5 search modes)
- **"⋯" button** in QuickSearchBar — closes bar and opens AdvancedSearchDialog

### ✨ Added — IDE
- **WelcomePanel** — VS Start Page-style welcome document shown on IDE launch (`doc-welcome` ContentId)
  - Quick Actions: New File, Open File, Open Project / Solution, Options
  - Recent Files & Recent Projects filtered to physically existing paths only (deleted entries hidden)
  - Live changelog fetched from GitHub (`raw.githubusercontent.com`) — loading state + offline fallback
  - Resources section: GitHub Repository + Report an Issue buttons (opens default browser)
  - App `Logo.ico` displayed in header
  - Full theme compliance via existing `DockBackgroundBrush` / `DockTabActiveBrush` / `DockMenuForegroundBrush` keys
  - Fix: `Loaded -= OnLoaded` prevents dialog-loop caused by Click-handler accumulation on docking re-attach

### ✨ Added — Themes & UI
- **`PanelCommon.xaml`** — shared panel toolbar styles (30px VS-style toolbar, Segoe MDL2 icon buttons)
- **`Panel_*` theme keys** — `ToolbarBrush`, `ToolbarBorderBrush`, `ToolbarButtonHoverBrush`, etc. in all 8 themes
- **ParsedFieldsPanel** refactored to VS-style toolbar (draggable title bar removed)
- **`PFP_*` theme keys** — ParsedFieldsPanel colors in all 8 themes

### ✨ Added — Docking
- **Tab colorization** — per-tab custom color with `TabSettingsDialog`
- **Left / right tab strip placement** — configurable per dock group
- **`TabSettingsDialog`** — color picker + placement selector

### ✨ Added — HexEditor API
- **`LoadTBL(string)`** public API — load a TBL file programmatically
- **Auto-apply project TBL** — when a `.bin` is opened and its project has a linked `.tbl`, it is applied automatically
- **`TblEditorRequested`** event — opens TBL Editor without circular reference (Core→TblEditor)

### 🔧 Changed
- `SolutionExplorer` moved to `WpfHexEditor.Panels.IDE` (was `WpfHexEditor.WindowPanels`)
- `ParsedFieldsPanel` — `TitleBarDragStarted` event removed; docking system handles floating
- App status bar — HexEditor internal status bar hidden (`ShowStatusBar = false`); App owns the status bar

---

## [2.7.0] — 2026-02 — IDE Application & Editor Plugin System

### ✨ Added — WpfHexEditor.App
- **Full IDE application** with VS-style docking (`WpfHexEditor.Docking.Wpf`)
- **`IDocumentEditor`** plugin contract — Undo, Redo, Copy, Cut, Paste, Save, IsDirty, IsReadOnly
- **`EditorRegistry`** — plugin registration at startup (`EditorRegistry.Instance.Register(...)`)
- **Content factory with cache** — `Dictionary<string, UIElement>` keyed by `ContentId`
- **`ActiveDocumentEditor`** INPC property — drives Edit menu bindings
- **`ActiveHexEditor`** INPC property — drives status bar DataContext
- **VS2022-style status bar** — left: editor status messages · center: EditMode + BytePerLine · right: panel count
- **Auto-sync** via `DockHost.ActiveItemChanged` → connects/disconnects ParsedFieldsPanel per active tab

### ✨ Added — Editors
- **TBL Editor** (`WpfHexEditor.Editor.TblEditor`) — standalone `TblEditorControl`, implements `IDocumentEditor`
  - Virtualized DataGrid, inline Ctrl+F search overlay, status bar
  - `TblExportService.ExportToTblFile()` for save/export
- **JSON Editor** (`WpfHexEditor.Editor.JsonEditor`) — implements `IDocumentEditor` + `IDiagnosticSource`
  - Real-time validation, `PerformValidation()`, `EnableValidation` toggle
- **Text Editor** (`WpfHexEditor.Editor.TextEditor`) — implements `IDocumentEditor` + `IEditorPersistable`
  - Caret/scroll persistence (1-based in DTO, 0-based in VM)

### ✨ Added — Panels
- **ParsedFieldsPanel** singleton — auto-connects to active HexEditor via `IParsedFieldsPanel` interface
- **DataInspectorPanel** — 40+ byte type interpretations at caret
- **SolutionExplorerPanel** — hierarchical VM tree, `ISolutionExplorerPanel`
- **PropertiesPanel** — context-aware F4 panel via `IPropertyProvider` / `IPropertiesPanel`

### ✨ Added — HexEditor IDocumentEditor
- `HexEditor` implements `IDocumentEditor` via `HexEditor.DocumentEditor.cs`
- `RaiseHexStatusChanged()` — fires `StatusMessage` after load, edit mode change, bytes-per-line change
- `IDiagnosticSource` implementation — exposes `HexEditor.DiagnosticSource.cs`
- `IEditorPersistable` implementation — bookmarks, scroll, caret, encoding, edit mode

### 🔧 Changed — Docking Engine
- `CreateTabControl` — all `DockGroupNode` panels always get title bar + `TabStripPlacement = Dock.Bottom`
- `FloatGroup()` — `GroupFloated` event fires **before** `LayoutChanged` (prevents duplicate windows)
- `FindWindowForItem` — checks both active item and non-active group items
- `RestoreFloatingWindows()` — called by `RebuildVisualTree()` for persisted float positions
- `FloatLeft / FloatTop / LastDockSide` serialized in `DockItemDto`

---

## [2.6.0] — 2026-02-22 — V1 Legacy Removal & Multi-Byte Fixes

### 🚨 Breaking — V1 Legacy Removed
Complete removal of Legacy V1 code after 12-month deprecation period:
- `HexEditorLegacy.xaml/.cs` (6,521 LOC)
- `ByteProviderLegacy.cs` (1,890 LOC)
- 6 legacy sample projects (5,079 LOC)
- V1 rendering classes: `BaseByte`, `HexByte`, `StringByte`, `FastTextLine` (2,051 LOC)
- V1 search dialogs: `FindWindow`, `FindReplaceWindow`

**Total removed: 17,093 lines of code (-30% codebase)**

`CompatibilityLayer` (725 LOC) is **kept** for API backward compatibility — zero migration required.

### 🐛 Fixed — Multi-Byte Mode (ByteSize 16/32)
- Click positioning in Bit16/32 modes for both hex and ASCII panels
- Keyboard navigation left/right in multi-byte modes
- Unified hit testing via single `HitTestByteWithArea` method (click, mouseover, drag, auto-scroll)
- ASCII mouseover alignment in multi-byte modes
- `GetDisplayCharacter` — returns all bytes in multi-byte groups (e.g. `"MZ"` instead of `"M"`)
- `GetCharacterDisplayWidth` — FormattedText measurement for pixel-perfect alignment
- ByteOrder setting respected in ASCII panel (HiLo/LoHi)
- TBL hex key building in multi-byte mode uses `Values[]` array

### ✨ Added
- **`RestoreOriginalByte(long)`** — restore a single modified byte to its original value
- **`RestoreOriginalBytes(long[])`** / **`RestoreOriginalBytes(IEnumerable<long>)`** — batch restore
- **`RestoreOriginalBytesInRange(long, long)`** — restore a contiguous range
- **`RestoreAllModifications()`** — clear all modifications at once
- Full Undo/Redo integration for restore operations
- Category localization **`CategoryKeyboardMouse`** added to all 19 language files

---

## [2.5.0] — 2026-02-14 — Critical Bug Fixes & V2 Architecture

### 🐛 Fixed — Critical
**Issue #145 — Insert Mode Hex Input** ✅
- Typing consecutive hex chars in Insert Mode now works: `FFFFFFFF` → `FF FF FF FF` (was `F0 F0 F0 F0`)
- Root cause: `PositionMapper.PhysicalToVirtual()` returned wrong position for inserted bytes
- Files: `PositionMapper.cs`, `ByteReader.cs`, `ByteProvider.cs`

**Save Data Loss Bug** ✅
- Catastrophic data loss (MB → KB file corruption) fully resolved
- Same PositionMapper bug caused `ByteReader` to read wrong bytes during save
- Fast save path added for modification-only edits (10-100x faster)

### 🚀 Performance
- **10-100x faster save** — debug logging overhead removed from all production paths
- **Fast save path** — modification-only edits bypass full virtual read

### 🏗️ Architecture
- **MVVM + 16 specialized services**
- Extracted 2,500+ lines of business logic into services
- 80+ unit tests (xUnit, .NET 8.0-windows)

---

## [2.2.0] — 2026-01 — Search Performance & Service Architecture

### 🚀 Performance
- **LRU Search Cache** — 10-100x faster repeated searches, O(1) lookup, auto-invalidation at all 11 modification points
- **Parallel Multi-Core Search** — 2-4x faster for files > 100MB, automatic threshold
- **SIMD Vectorization** (net5.0+) — AVX2/SSE2, 4-8x faster single-byte search, processes 32 bytes/cycle
- **Span\<T\> + ArrayPool** — 2-5x faster, 90% less memory allocation
- **Profile-Guided Optimization** (.NET 8.0+) — 10-30% CPU boost, 30-50% faster startup

### 🏗️ Architecture — Service Layer (10 Services)
`ClipboardService` · `FindReplaceService` · `UndoRedoService` · `SelectionService` · `HighlightService` · `ByteModificationService` · `BookmarkService` · `TblService` · `PositionService` · `CustomBackgroundService`

### 🧪 Testing
- 80+ unit tests: `SelectionServiceTests` (35), `FindReplaceServiceTests` (35), `HighlightServiceTests` (10+)

---

## [2.1.0] — 2025 — V2 Rendering Engine

### ✨ Added
- `HexEditorV2` control with custom `DrawingContext` rendering
- MVVM architecture with `HexEditorViewModel`
- True Insert Mode with virtual position mapping
- Custom background blocks for byte range highlighting
- BarChart visualization mode

### 🚀 Performance
- Rendering: **99% faster** vs V1 (DrawingContext vs ItemsControl)
- Memory: **80-90% reduction**

---

## [2.0.0] — 2024 — Multi-Targeting & Async

### ✨ Added
- .NET 8.0-windows support
- Multi-targeting: .NET Framework 4.8 + .NET 8.0-windows
- Async file operations with progress and cancellation
- C# 12 / 13 language features

---

## [1.x] — 2023 and earlier

Legacy V1 monolithic architecture. See [GitHub Releases](https://github.com/abbaye/WpfHexEditorIDE/releases) for historical notes.

V1 NuGet package (`WPFHexaEditor`) remains available for existing users but is no longer maintained. See [Migration Guide](docs/migration/MIGRATION.md).

---

## Legend

| Icon | Meaning |
|------|---------|
| 🚀 | Performance improvement |
| 🐛 | Bug fix |
| ✨ | New feature |
| 🔧 | Internal change / refactor |
| 🏗️ | Architecture change |
| 🧪 | Testing |
| 🚨 | Breaking change |
