# 🗺️ WpfHexEditor — Roadmap

This document tracks all planned and in-progress features for the WpfHexEditor IDE.
Features already shipped are in [CHANGELOG.md](CHANGELOG.md).

> **Legend:** 🔧 In Progress · 🔜 Planned · ✅ Done (see CHANGELOG)

---

## 🔧 In Progress

| Feature # | Title | Description | Progress |
|-----------|-------|-------------|----------|
| #92 | **Integrated Terminal** | Multi-tab terminal with PowerShell/Bash/CMD/HxTerminal sessions, macro recording, command history replay. Core layer done; full IDE integration pending. | ~70% |
| #104–105 | **Assembly Explorer** | .NET PE tree (namespaces, types, methods, fields, events, resources). Phase 1 (PEReader pipeline, stub tree, IDE menu/statusbar) done. ECMA-335 full metadata resolution pending. | ~15% |
| #107 | **Document Model** | Unified in-memory document representation shared across all editors (hex, code, text, diff). Foundation for multi-editor collaboration, undo/redo unification and LSP integration. | ~10% |

---

## 🔜 Planned — IDE Core & Build

| Feature # | Title | Description |
|-----------|-------|-------------|
| #36 | **Service Container / DI** | `ServiceContainer` singleton with `FileService`, `EditorService`, `PanelService`, `PluginService`, `EventBus`, `TerminalService`; Singleton/Scoped/Transient lifecycle. |
| #37 | **Global CommandBus** | All IDE actions (menus, toolbar, terminal, plugins) routed through `CommandBus`; every command has Id, Handler, CanExecute context, and Category. |
| #38 | **Keyboard Shortcuts & Bindings** | `KeyBindingService`, configurable gestures per command, conflict detection, plugin-extensible, export/import. |
| #39 | **User Preferences Persistence** | `ConfigurationManager`, per-section schemas, plugin config API, export/import, cross-session persistence. |
| #40 | **Centralized Logging & Diagnostics** | `LogService` (Info/Warning/Error/Debug), `DiagnosticService` (perf metrics), `LogSink` abstraction, Output + Error Panel integration. |
| #77 | **Workspace System & State Serialization** | Multi-workspace management with project-specific settings and full layout/state persistence across sessions. |
| #78 | **Command System (Internal IDE Commands)** | Central command registry for all IDE actions accessible via menu, terminal, keyboard shortcuts, and scripts. |
| #79 | **Scripting / Automation Engine** | Execute `.hxscript` files via terminal to automate IDE workflows, plugin actions, and batch operations. |
| #80 | **Event Bus (IPluginEventBus)** | Central pub-sub for async communication between plugins and IDE services without tight coupling. |
| #82 | **Service Registry & Dependency Injection** | Central DI container exposing all IDE services to plugins uniformly via `IServiceRegistry`. |
| #83 | **Options Document / IDE Settings** | Unified options panel for editor behavior, themes, plugins, and workspace config with live preview. |
| #87 | **Workspace Templates** | Pre-configured project templates for common file structures and development workflows. |
| #100 | **IDE Localization Engine** | Full i18n support for IDE UI (currently English only). HexEditor control already supports 19 languages. EN/FR initial; plugin-provided translations; dynamic switching. |
| #101 | **`.sln` Parser** | Open and parse existing Visual Studio 2019/2022 `.sln` files; project graph resolution, nested solution folders, shared projects. |
| #102 | **C# / VB.NET Project Support** | Full `.csproj` / `.vbproj` parsing — properties, item groups, package references, project-to-project references; read/write support. |
| #103 | **MSBuild API Integration** | Build, rebuild, clean via embedded MSBuild API from within the IDE; output to Output Panel; errors/warnings to Error Panel with file/line navigation. |

---

## 🔜 Planned — Editors & Code Intelligence

| Feature # | Title | Description |
|-----------|-------|-------------|
| #84 | **Code Editor — VS-Like Advanced** | Full-featured code editor with syntax folding, multi-language support, gutter indicators, split view, and diagnostics integration. |
| #85 | **LSP Engine** | Incremental symbol parsing, folding, go-to-definition, find-references via Language Server Protocol. |
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
| #81 | **Plugin Sandbox (Extreme Isolation)** | Out-of-process plugin execution via gRPC/Named Pipes for security and fault isolation. |
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

## ✅ Recently Shipped

| Feature | Version / Release |
|---------|-------------------|
| Multi-tab terminal sessions + macro recording (#92 Phase 1) | Unreleased — 2026-03 |
| Assembly Explorer stub — PEReader pipeline, IDE menu, statusbar (#104 Phase 1) | Unreleased — 2026-03 |
| Plugin system (SDK, PluginHost, 7 first-party plugins) | Unreleased — 2026-03 |
| VS-style docking engine (100% in-house) | [2.7.0] — 2026-02 |
| Project system (`.whsln` / `.whproj`) | [2.7.0] — 2026-02 |
| Insert Mode fix, save reliability, unlimited undo/redo | [2.5.0] — 2026-02 |

> Full history → [CHANGELOG.md](CHANGELOG.md)
