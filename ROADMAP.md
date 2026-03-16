# 🗺️ WpfHexEditor — Roadmap

This document tracks all planned and in-progress features for the WpfHexEditor IDE.
Features already shipped are in [CHANGELOG.md](CHANGELOG.md).

> **Legend:** 🔧 In Progress · 🔜 Planned · ✅ Done (see CHANGELOG)

---

## 🔧 In Progress

| Feature # | Title | Description | Progress |
|-----------|-------|-------------|----------|
| #81 | **Plugin Sandbox** | Out-of-process isolation via HWND embedding + full IPC bridge (menus, toolbar, events). HWND parenting, Job Object resource control, IPC HexEditor event bridge done. Auto-isolation engine done. IDE EventBus IPC bridge done. Remaining: gRPC migration, hot-reload from sandbox. | ~65% |
| #84 | **Code Editor — VS-Like Advanced** | Full-featured code editor: syntax highlighting with 26 `.whlang` definitions, URL hover/click, foreground base pass, find/replace, split view, `IEditorPersistable`. Hosts decompiled C# from Assembly Explorer. Source outline integration pending. | ~70% |
| #101–103 | **MSBuild & VS Solution Support** | Open `.sln` files via `VsSolutionLoaderPlugin`; build/rebuild/clean via MSBuild API; output routed to Build channel with severity coloring; auto-focus on build start; empty-solution guard. Project templates scaffolded. Remaining: error list navigation, incremental build. | ~60% |
| #104–106 | **Assembly Explorer + Decompilation** | .NET PE tree done. C# decompilation → Code Editor tab with syntax highlighting done (v0.2.1). Extract to Project done. Collapse All / Close All done. Remaining: full ECMA-335 token→offset resolution, ILSpy full decompilation, hex sync. | ~35% |
| #107 | **Document Model** | Unified in-memory document representation shared across all editors (hex, code, text, diff). Foundation for multi-editor collaboration, undo/redo unification and LSP integration. | ~10% |
| #85–86 | **LSP Engine / IntelliSense** | `WpfHexEditor.LSP` project created with navigation, formatting, refactoring, symbols infrastructure. `RefactoringEngine`, `RenameRefactoring`, `ExtractMethodRefactoring`, `CodeFormatter`, `SymbolTableManager`, `NavigationProvider` stubs. `CommandIntegration` mediator wired to IDE EventBus. Remaining: real parsing backend, completion provider, hover info. | ~20% |

---

## 🔜 Planned — IDE Core & Build

| Feature # | Title | Description |
|-----------|-------|-------------|
| #36 | **Service Container / DI** | `ServiceContainer` singleton with `FileService`, `EditorService`, `PanelService`, `PluginService`, `EventBus`, `TerminalService`; Singleton/Scoped/Transient lifecycle. |
| #37 | **Global CommandBus** | All IDE actions (menus, toolbar, terminal, plugins) routed through `CommandBus`; every command has Id, Handler, CanExecute context, and Category. |
| #38 | **Keyboard Shortcuts & Bindings** | `KeyBindingService`, configurable gestures per command, conflict detection, plugin-extensible, export/import. |
| #39 | **User Preferences Persistence** | `ConfigurationManager`, per-section schemas, plugin config API, export/import, cross-session persistence. |
| #40 | **Centralized Logging & Diagnostics** | `LogService` (Info/Warning/Error/Debug), `DiagnosticService` (perf metrics), `LogSink` abstraction, Output + Error Panel integration. |
| #77 | **Workspace System & State Serialization** | Plugin lazy loading done (file-extension/command activation triggers, `Dormant` state). Multi-workspace management with project-specific settings and full layout/state persistence — remaining. |
| #78 | **Command System (Internal IDE Commands)** | Central command registry for all IDE actions accessible via menu, terminal, keyboard shortcuts, and scripts. |
| #79 | **Scripting / Automation Engine** | Execute `.hxscript` files via terminal to automate IDE workflows, plugin actions, and batch operations. |
| #82 | **Service Registry & Dependency Injection** | Central DI container exposing all IDE services to plugins uniformly via `IServiceRegistry`. |
| #83 | **Options Document / IDE Settings** | Unified options panel for editor behavior, themes, plugins, and workspace config with live preview. |
| #87 | **Workspace Templates** | Pre-configured project templates for common file structures and development workflows. |
| #100 | **IDE Localization Engine** | Full i18n support for IDE UI (currently English only). HexEditor control already supports 19 languages. EN/FR initial; plugin-provided translations; dynamic switching. |
| #101 | **`.sln` Parser** | *(In Progress — see above)* Open and parse existing Visual Studio 2019/2022 `.sln` files via `VsSolutionLoaderPlugin`. Remaining: nested solution folders, shared projects, full project graph. |
| #102 | **C# / VB.NET Project Support** | *(In Progress)* `.csproj` parsing via MSBuild Locator. Remaining: VB.NET, write support, item group editing. |
| #103 | **MSBuild API Integration** | *(In Progress — see above)* Build/rebuild/clean done. Remaining: error list navigation with file/line jump, incremental builds, parallel project builds. |

---

## 🔜 Planned — Ultimate IDE Architecture (VS-Level)

Cette section présente les concepts VS-level de l’IDE, en se concentrant uniquement sur les fonctionnalités **non encore incluses** dans la roadmap actuelle.

| Feature # | Title | Description | Remarque |
|-----------|-------|-------------|----------|
| #153 | **Visual Scripting / Graph View** | Node-based visual representation for binary structures, memory flow, or data pipelines; plugin-extensible graphs for analysis and automation. | Nouveau concept, pas encore dans la roadmap |
| #154 | **Cross-Platform Ready Core** | WPF/.NET 8 foundation; potential interop with WinForms; future-proof design for possible Linux/macOS port with Avalonia/MAUI. | Nouveau concept, pas encore dans la roadmap |

---

## 🔜 Planned — Editors & Code Intelligence

| Feature # | Title | Description |
|-----------|-------|-------------|
| #84 | **Code Editor — VS-Like Advanced** | *(In Progress ~70% — see above)* Remaining: syntax folding, gutter indicators, multi-caret, diagnostics integration. |
| #85 | **LSP Engine** | *(In Progress ~20% — see above)* Infrastructure done. Remaining: real parsing backend, go-to-definition, find-references. |
| #86 | **IntelliSense v4 (LSP)** | Advanced autocomplete, signature help, quick-info, multi-caret editing, virtual scroll for >1 GB files. |
| #88 | **Dynamic Snippets** | `SnippetsManager` with context-aware snippets, dynamic variables (`CurrentLine`, `FileName`, `CursorPosition`); user/plugin/language-scoped. |
| #89 | **AI-Assisted Code Suggestions** | `AICompletionEngine` and `AIRefactoringAssistant`; contextual completions, auto-refactoring, plugin-extensible AI rules. |
| #94 | **Advanced Refactoring** | Rename symbol (workspace-wide), extract method/class, inline variable, move file between projects; AI-assisted suggestions. |
| #96 | **Code Analysis & Metrics** | Cyclomatic complexity, code duplication detection, dependency graphs; dedicated panel with filter/sort. |
| #106 | **.NET Decompilation via ILSpy** | C# skeleton view + full IL disassembly per method; "Go to Metadata Token" navigation; decompiled source in Code Editor tab. |

---

## 🔜 Planned — Assembly & Binary

| Feature # | Title | Description |
|-----------|-------|-------------|
| #81 | **Plugin Sandbox (gRPC Migration + Hot-Reload)** | HWND embedding + IPC bridge done (v0.3.0). Remaining: gRPC transport migration, collectible `AssemblyLoadContext` hot-reload from sandbox, plugin restart-less upgrade flow. |
| #97 | **Large File Optimization** | `VirtualizationEngine`, `LazyParser`, multi-core IntelliSense adapter; virtualized display for >1 GB files, incremental parsing. |

---

## 🔜 Planned — DevOps & Collaboration

| Feature # | Title | Description |
|-----------|-------|-------------|
| #41 | **Plugin Marketplace** | `MarketplaceManager` — browse, install, update from online registry; signed `.whxplugin` packages. |
| #42 | **Plugin Security & Sandboxing** | Permission declarations at install time, integrity verification, AppDomain isolation. |
| #43 | **Plugin Auto-Update** | `UpdateService` / `UpdateChecker`, rollback support, scheduled checks for IDE + plugins. |
| #44 | **Integrated Debugger** | `DebuggerService` (StartDebug, StepInto/Over/Out, Evaluate), `BreakpointsManager`, `WatchPanel`, `CallStackPanel`. |
| #90 | **Debugger — Multi-Project** | Multi-project debug sessions via EventBus; supports scripts, plugins, and workspace projects. |
| #91 | **Git Integration** | `GitManager`, `GitPanel` (commit/push/pull/branch); inline gutter diff; `GitEventAdapter` for file-change notifications. |
| #93 | **Plugin Installer / Marketplace UI** | Plugin search UI, download/update manager, sandbox enforcement at install time. |
| #95 | **Unit Testing Panel** | `TestManager`, `TestRunner`, `TestResultPanel`; auto-detect NUnit/JUnit/MSTest; run by file/project/workspace. |
| #98 | **Multi-User Collaboration** | Multi-cursor real-time editing, document sync, contextual chat/comments per line. |

---

## 🔜 Planned — UX & Infrastructure

| Feature # | Title | Description |
|-----------|-------|-------------|
| #99 | **Advanced UI/UX** | `NotificationManager`, `WorkspaceLayoutAdapter`; contextual inline notifications, layout persistence per workspace, full docking for all panels. |

---

## 🔜 Planned — Quality, Tooling & Operations

| Feature # | Title | Description |
|-----------|-------|-------------|
| #45 | **Plugin & Script Testing** | `TestRunnerService` — automated functional, compatibility, security and performance tests for plugins and scripts before execution or installation. |
| #46 | **Integrated Documentation & Contextual Help** | In-IDE documentation browser and context-sensitive help for all components (menus, commands, panels, plugins, APIs). |
| #47 | **Notification & Alert System** | Centralized `NotificationService` — inline toast alerts, priority levels, EventBus integration, plugin-extensible notification hooks. |
| #48 | **Monitoring & Analytics** | IDE health dashboard — plugin CPU/RAM usage, event frequency, command stats, latency tracking; exportable reports. |
| #49 | **Export / Import Projects & Configurations** | Full export/import of workspace configs, settings, plugin lists, templates and keybindings for portability and backup. |
| #54 | **Auto-Save & Restore** | Automatic session backup at configurable intervals; full restore on crash or abnormal exit without data loss. |
| #58 | **Reporting & Dashboards** | Dedicated report panel — code quality metrics, plugin health, build history, test results; filterable and exportable. |
| #63 | **Smart Session Backup & Auto-Resume** | Intelligent session snapshot (open files, cursors, layout, unsaved changes); seamless resume after restart or crash. |
| #65 | **Interactive Tutorials & Onboarding** | Step-by-step interactive onboarding for new users; contextual tutorial overlays for major features; plugin authors guide. |
| #67 | **Global Backup & Config Versioning** | Version-controlled snapshots of all IDE configurations, plugin manifests and workspace templates; rollback support. |
| #69 | **Dependency Monitoring & Plugin Compatibility** | Detect plugin dependency conflicts, SDK version mismatches, and incompatible combinations; alerts before install/update. |
| #71 | **CI Integration for Plugins & Scripts** | Trigger automated plugin test suites and script validation from CI pipelines; webhook support; result reporting via Output Panel. |

---

## 🔜 Planned — Distribution & Web Presence

| Feature # | Title | Description |
|-----------|-------|-------------|
| #108 | **Official Website** | Public project website — landing page, feature showcase, screenshots, documentation browser, changelog, download links and plugin registry. |
| #109 | **Installable Package** | Self-contained installer for the IDE — MSI / MSIX / WinGet package; auto-update channel; no .NET SDK required for end users; optional silent install for enterprise. |

---

## 🔜 Planned — Binary Analysis & Reverse Engineering

| Feature # | Title | Description |
|-----------|-------|-------------|
| #110 | **String Extraction Panel** | Dockable panel scanning the active binary for ASCII, Unicode, and custom-encoding strings with minimum-length filter and one-click jump to offset in hex view. |
| #111 | **Cryptographic Hash Inspector** | Compute MD5, SHA-1, SHA-256, SHA-512, CRC-32, and Adler-32 for the full file or any byte-range selection in real time, with copy-to-clipboard and reference-hash comparison. |
| #112 | **Embedded File Carver** | Detect and extract embedded PE files, ZIP archives, PNG images, and other magic-byte signatures from within a binary; carved blobs open as virtual child documents. |
| #113 | **YARA Rule Engine** | Author, compile, and run YARA rules directly in the IDE; matches highlighted in hex view and listed in a results panel with offset, length, and rule name. |
| #114 | **PE Import / Export Table Analyzer** | Parse PE32/PE64 IAT and Export Directory; display resolved DLL and function names side-by-side with raw offsets; flag suspicious ordinal-only imports. |
| #115 | **Binary Patch Recorder** | Record every byte modification as a named, annotated patch; export as binary diff, IDA script, or human-readable `.patch` file for reproducible patching. |
| #116 | **Obfuscation & Packing Detector** | Heuristic analysis of section entropy, suspicious imports, and known packer watermarks (UPX, Themida, MPRESS); produces a risk score and actionable report. |
| #117 | **Memory Snapshot Viewer** | Load Windows mini-dump (`.dmp`) or Linux core-dump files; display memory regions, thread stacks, loaded modules, and heap blocks in the hex view with structured overlays. |
| #139 | Grammar File Support | Add support for loading and parsing .syn grammar files from Synalysis; enable syntax highlighting, structured parsing, and integration with memory/code analysis workflows. |

---

## 🔜 Planned — Security & Forensics

| Feature # | Title | Description |
|-----------|-------|-------------|
| #118 | **File Signature Database** | Integrated, user-extensible magic-byte catalog that auto-identifies 500+ file types on open; shows detected format, MIME type, and confidence in the status bar. |
| #119 | **Byte Frequency & Bigram Heatmap** | 256×256 bigram dot-plot and 256-bucket byte-frequency histogram for any selection or full file — visually distinguishes encrypted, compressed, text, and binary payloads. |
| #120 | **XOR / ROT Cipher Decoder** | Brute-force single-byte XOR keys and ROT offsets over a selection; score candidates by ASCII printability; display top results with one-click "apply to selection". |
| #121 | **Audit Log & Forensic Session Journal** | Record every user action (open, edit, navigate, export) with timestamps and byte-range context into a tamper-evident journal, exportable as signed HTML or JSON report. |
| #122 | **Certificate & ASN.1 Inspector** | Parse DER/PEM X.509 certificates, PKCS#7/12 containers, and raw ASN.1 structures embedded in any binary; tree-view with field names, OIDs, validity dates, and key parameters. |
| #123 | **Vulnerability Pattern Scanner** | Curated byte-pattern rules (stack cookies, heap metadata, safe SEH markers) that flag potential vulnerability indicators in native binaries directly in the hex view. |

---

## 🔜 Planned — Developer Productivity

| Feature # | Title | Description |
|-----------|-------|-------------|
| #124 | **Hex Bookmarks & Named Regions** | Persistent named bookmarks at absolute or relative offsets with groups, colors, and comments; importable/exportable as JSON; visible in a dedicated Bookmarks Panel. |
| #125 | **Regex Search over Hex & ASCII** | Full regex search across hex byte patterns (e.g. `\xDE\xAD.{2}\xBE\xEF`) and decoded ASCII simultaneously, with match highlighting, results list, and replace support. |
| #126 | **Byte-Range Calculator** | Inline calculator panel for arithmetic, bitwise, and shift operations on raw byte values; accepts hex/decimal/binary input; shows two's-complement and IEEE-754 float interpretations. |
| #127 | **Column / Block Selection** | Rectangular block selection across hex and ASCII panes; paste, fill, or export selected columns as independent byte arrays — essential for fixed-width record formats. |
| #128 | **Binary Template Marketplace** | Community-driven repository of binary template definitions; browse, search, install, and auto-update format templates (PE, ELF, ZIP, PNG, MP4, etc.) from within the IDE. |
| #129 | **Multi-File Hex Session Tabs** | True independent hex editor sessions per tab with separate cursors, selections, undo stacks, and bookmarks — enabling parallel analysis of multiple binaries. |
| #130 | **Changeset Review Panel** | Diff-style view of all pending unsaved byte modifications across the file: offset, original value, new value, age, and per-change accept/reject controls before save. |

---

## 🔜 Planned — IDE Infrastructure

| Feature # | Title | Description |
|-----------|-------|-------------|
| #131 | **Plugin Dependency Graph Panel** | Interactive directed-graph visualization of all loaded plugins, their service dependencies, and SDK version constraints; detects cycles and orphaned dependencies. |
| #132 | **Theme Designer & Live Preview** | In-IDE XAML brush editor to create, fork, and export custom themes; changes applied live to all dockable panels; themes packaged as `.whtheme` files. |
| #133 | **Command Palette (Ctrl+Shift+P)** | VS Code-style fuzzy-search palette surfacing all IDE commands, open documents, settings, and plugin actions with keyboard navigation and recent-command history. |
| #134 | **Extension Point Debugger** | Developer panel listing every SDK extension point (menus, toolbar, statusbar, eventbus) registered by each plugin with live invocation counts and last-call stack. |
| #135 | **Workspace File Watcher** | Monitor all open project files for external changes (from other processes or Git operations); prompt to reload, diff, or merge changes without closing the editor tab. |
| #138 | **In-IDE Plugin Development** | Develop, build, hot-reload, and live-test SDK plugins directly inside the IDE — `PluginProjectTemplate` scaffolding, MSBuild integration (#103), collectible `AssemblyLoadContext` hot-reload, lightweight sandbox, full SDK IntelliSense, Plugin Dev Log panel, and `.whxplugin` packaging; no external toolchain required. |

---

## 🔜 Planned — Network & Protocol Analysis

| Feature # | Title | Description |
|-----------|-------|-------------|
| #136 | **PCAP / Network Capture Viewer** | Load `.pcap` and `.pcapng` files; display packet list, layer breakdown (Ethernet/IP/TCP/UDP/TLS), and raw payload bytes in the hex view. |
| #137 | **Protocol Dissector Plugin API** | Plugin contract allowing third parties to register custom protocol dissectors; dissected fields appear in the ParsedFields panel and hex view overlays. |

---

## ✅ Recently Shipped

| Feature | Version / Release |
|---------|-------------------|
| **Source Outline Navigation** — lazy type/member tree in Solution Explorer for `.cs`/`.xaml` files; `SourceOutlineEngine` BCL-only parser; `LoadingNode` placeholder + async expand | [0.5.0] — 2026-03-16 |
| **Assembly Explorer v0.2.1** — Decompile to C# → Code Editor tab with syntax highlighting; Extract to Project (`AssemblyCodeExtractService` + `ProjectPickerDialog`); Collapse All; Close All Assemblies | [0.5.0] — 2026-03-16 |
| **Code Editor — full syntax highlighting** — all 26 `.whlang` definitions; live `TryFindResource` brush resolution; foreground base pass; hover-only URL underline with tooltip; split host + document model cleanup | [0.5.0] — 2026-03-16 |
| **Build Output Channel** — `OutputServiceImpl.Write("Build",…)` now routes to Build channel; auto-focus via `OutputLogger.FocusChannel`; severity coloring (info/warn/error/success); empty-solution guard | [0.5.0] — 2026-03-16 |
| **VS Solution Loader** — `VsSolutionLoaderPlugin` with dynamic file-dialog filter + external solution routing; 4 project templates (Console, ClassLib, WPF, AspNet API) | [0.5.0] — 2026-03-16 |
| **LSP Infrastructure** — `WpfHexEditor.LSP`: `RefactoringEngine`, `RenameRefactoring`, `ExtractMethodRefactoring`, `CodeFormatter`, `SymbolTableManager`, `CommandIntegration` mediator (#85–86 foundation) | [0.5.0] — 2026-03-16 |
| **Docking: Tab Overflow Panel** — `TabOverflowButton` + `TabOverflowPanel` improvements; correct positioning and theme-aware context menu | [0.5.0] — 2026-03-16 |
| **IDE EventBus** — `WpfHexEditor.Events`, `IIDEEventBus`, 10 typed events, IPC bridge for sandbox plugins, options page (#80) | [0.4.0] — 2026-03-16 |
| **Plugin Lazy Loading** — `PluginActivationConfig`, `PluginActivationService`, `Dormant` state, file-extension/command triggers (#77 partial) | [0.4.0] — 2026-03-16 |
| **Capability Registry** — `IPluginCapabilityRegistry`, `PluginFeature` constants, manifest `features` field | [0.4.0] — 2026-03-16 |
| **Extension Points** — `IFileAnalyzerExtension`, `IHexViewOverlayExtension`, `IBinaryParserExtension`, `IDecompilerExtension`, `ExtensionPointCatalog` | [0.4.0] — 2026-03-16 |
| **Plugin Dependency Graph** — versioned constraints, topological ordering, cascading unload/reload | [0.4.0] — 2026-03-16 |
| **ALC Isolation Diagnostics** — assembly count, conflict detection, weak-reference GC verification | [0.4.0] — 2026-03-16 |
| **Plugin Sandbox — HWND embedding + IPC menu/toolbar/event bridges** (#81 Phase 9-12) | [0.3.0] — 2026-03-15 |
| **Auto-isolation decision engine + PluginMigrationPolicy** (#81) | [0.3.0] — 2026-03-15 |
| **SandboxJobObject** — per-plugin CPU/RAM resource limits (#81) | [0.3.0] — 2026-03-15 |
| Multi-tab terminal sessions + macro recording (#92) | [0.3.0] — 2026-03-15 |
| Assembly Explorer stub — PEReader pipeline, IDE menu, statusbar (#104 Phase 1) | [0.3.0] — 2026-03-15 |
| Plugin system (SDK, PluginHost, 9 first-party plugins, monitoring) | [0.3.0] — 2026-03-15 |
| VS-style docking engine (100% in-house) | [2.7.0] — 2026-02 |
| Project system (`.whsln` / `.whproj`) | [2.7.0] — 2026-02 |
| Insert Mode fix, save reliability, unlimited undo/redo | [2.5.0] — 2026-02 |

> Full history → [CHANGELOG.md](CHANGELOG.md)
