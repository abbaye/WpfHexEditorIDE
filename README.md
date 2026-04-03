<div align="center">
  <a href="Images/Logo2026.png"><img src="Images/Logo2026.png" width="600" height="250" /></a>
  <br/><br/>

  <h3>🖥️ A full-featured open-source IDE for .NET — Binary analysis, reverse engineering & build tooling</h3>

[![.NET](https://img.shields.io/badge/.NET-8.0--windows-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
  [![Platform](https://img.shields.io/badge/Platform-Windows%20WPF-0078D4?logo=windows)](https://github.com/abbaye/WpfHexEditorIDE)
  [![IDE Version](https://img.shields.io/badge/IDE-v0.6.4.3-6A0DAD?logo=visualstudiocode&logoColor=white)](https://github.com/abbaye/WpfHexEditorIDE/releases)
  [![License: AGPL v3](https://img.shields.io/badge/License-AGPL_v3-blue.svg)](https://www.gnu.org/licenses/agpl-3.0)
  [![Status](https://img.shields.io/badge/Status-Active%20Development-orange)](https://github.com/abbaye/WpfHexEditorIDE/commits/master)
  [![Roadmap](https://img.shields.io/badge/Roadmap-ROADMAP.md-brightgreen)](docs/ROADMAP.md)

  <br/>

  > 🚧 **Active Development** — New features, editors and panels are added regularly. Contributions welcome!
  >
  > 📅 *Last revised: 2026-04-02*

  <br/>

  <a href="Images/App-Editors-Welcome.png"><img src="Images/App-Editors-Welcome.png" alt="WPF HexEditor IDE" width="900"/></a>
  <br/>
  <sub><i>WpfHexEditor — Full IDE with VS-style docking, project system, and multiple editors</i></sub>

  <p>
    <a href="#-the-ide-application"><b>The IDE</b></a> •
    <a href="#-editors"><b>Editors</b></a> •
    <a href="#-standalone-controls--libraries"><b>Controls</b></a> •
    <a href="#-ide-panels"><b>Panels</b></a> •
    <a href="#-quick-start"><b>Quick Start</b></a> •
    <a href="#-documentation"><b>Docs</b></a> •
    <a href="docs/CHANGELOG.md"><b>Changelog</b></a>
  </p>
</div>

---

## 🖥️ The IDE Application

**WpfHexEditor** is a full-featured binary analysis IDE for Windows, built with WPF and .NET 8. Think Visual Studio for binary files.

| | |
|---|---|
| **📐 `.whfmt` — Declaration-Driven IDE** | The heart of WpfHexEditor is its **`.whfmt` format engine** — an in-house unified definition language that makes the entire IDE **data-driven**. No behavior is hardcoded: every file type, every editor, every language feature is declared in `.whfmt` definition files. **460+ definitions** ship built-in. **It decides everything:** which editor opens a file (Hex, Code, Image, Audio, JSON, Markdown…), how binary formats are parsed and displayed (repeating blocks, unions, versioned structures, pointers, checksums, assertions, forensic alerts, AI hints), how source code behaves in the Code Editor (syntax highlighting for 55+ languages, folding strategies, bracket pairs, comment toggles, auto-close pairs, indentation rules, end-of-block hints, breakpoint validation, whitespace rendering), and how formats are detected (magic bytes with confidence scoring and multi-signature matching). Add a new file type? Write a `.whfmt` file — no C# code needed. The IDE adapts instantly. This architecture makes WpfHexEditor fundamentally different from traditional IDEs: **the data defines the IDE, not the code.** |
| **🔍 Binary Intelligence** | Deep binary analysis toolkit — 460+ format auto-detection via magic bytes with confidence scoring, reactive Parsed Fields panel with expandable groups and FormatNavigator bookmark strip, format field color overlay directly on the hex view, Data Inspector showing 40+ type interpretations at caret (integers, floats, strings, GUIDs, dates, colors, IP addresses…), Assembly Explorer for .NET PE inspection with ILSpy C#/VB.NET decompilation |
| **🧠 Code Intelligence** | **In-process Roslyn** for C#/VB.NET analysis (no external OmniSharp process) — full LSP 3.17 client supporting 13 provider types: completion, hover, signature help, code actions, rename, inlay hints, code lens, semantic tokens, call hierarchy, type hierarchy, pull diagnostics, linked editing, breadcrumb navigation bar |
| **🤖 AI Assistant** | Multi-provider AI chat panel — supports Anthropic, OpenAI, Google Gemini, Ollama, and Claude Code CLI out of the box, 25 MCP tools for deep IDE integration (build, debug, navigate, analyze), streaming responses with inline code apply, `@mentions` for context injection (`@file`/`@selection`/`@errors`/`@solution`), dedicated command palette (`Ctrl+Shift+A`), conversation history, customizable prompt presets |
| **🔌 Plugin System** | Extensible plugin architecture — SDK 2.0.0 (API frozen, semver), `.whxplugin` package format, Plugin Manager UI, typed EventBus (39+ domain events), capability registry, extension points (menus, toolbar, titlebar, panels, status bar, terminal commands), dependency graph, plugin signing with signature validation, out-of-process sandbox with HWND embedding — **28 built-in plugins** ship with the IDE |
| **🏗️ Project System** | Open and build `.whsln`/`.whproj` native projects or Visual Studio `.sln`/`.csproj`/`.vbproj` solutions — MSBuild build/rebuild/clean with parallel project compilation, real-time build progress bar, virtual and physical folder organization, per-file editor state persistence, project template scaffolding |
| **🪟 Docking** *(100% in-house)* | Fully custom VS-style docking engine — float, dock, auto-hide, pin, colored tab strips, **16 built-in themes** (Dark, Light, VS2022Dark, DarkGlass, Dracula, Nord, Tokyo Night, Catppuccin Mocha/Latte, Gruvbox Dark, Forest, Matrix, Synthwave 84, Cyberpunk, High Contrast…), tab placement left/right/bottom, layout undo/redo (`Ctrl+Shift+Z/Y`), serializable workspace state |
| **⌨️ Command & Terminal** | Command Palette (`Ctrl+Shift+P`) with 9 search modes (commands, files, symbols, go-to-line…) — central command registry (~100 commands) with configurable keyboard shortcuts, integrated multi-tab terminal (`Ctrl+\``) with 35+ built-in commands including `plugin-reload`, extensible by plugins via `ITerminalService` API |
| **🐞 .NET Debugger** *(~60%)* | Integrated .NET debugging UI — Debug menu (Start/Stop/Restart, Step Over/Into/Out), collapsible toolbar pod, execution line highlight, full breakpoint system with conditions, hit counts, enable/disable toggle, and solution-scoped persistence, Breakpoint Explorer panel, right-click gutter popup for breakpoint editing, debug status bar · ⚠️ *Debug launch not yet functional — UI and breakpoint infrastructure are ready, runtime attach pending* |
| **🧪 Unit Testing** | Built-in test runner — auto-detects xunit, nunit, and mstest projects, runs via `dotnet test` with TRX result parsing, pass/fail/skip counters with color-coded outcome glyphs, context-sensitive detail panel (project summary, class summary, or individual test details), auto-run on successful build |
| **📋 IDE Infrastructure** | Unified editor plugin architecture via `IDocumentEditor` — shared undo engine with coalescence and VS-style history dropdown, `Ctrl+Z/Y` across all editors, rectangular block selection (`Alt+Click`), adaptive status bar, 30+ options pages, workspace system (`.whidews` save/restore), dynamic View menu (Flat/Categorized/ByDockSide), middle-click pan mode, NuGet Solution Manager, dependency injection via `Microsoft.Extensions.DependencyInjection` |

---

## 📝 Editors

Every editor is a standalone `IDocumentEditor` plugin — reusable outside the IDE.

| Editor | Progress | Description |
|--------|----------|-------------|
| **[Code Editor](Sources/WpfHexEditor.Editor.CodeEditor/README.md)** | ~85% | Advanced source editor — 55+ languages (incl. F# + VB.NET), **Roslyn in-process C#/VB.NET analysis**, full LSP 3.17 suite (13 providers: completion, hover, signature help, code actions, rename, inlay hints, code lens, semantic tokens, call/type hierarchy, linked editing, pull diagnostics), sticky scroll, Find All References (`Shift+F12`), multi-caret (`Ctrl+Alt+Click`), bracket-depth colorizer, color swatch preview, column rulers, format-on-save, split view |
| **[TBL Editor](Sources/WpfHexEditor.Editor.TblEditor/README.md)** | ~75% | Character table editor — create and edit custom `.tbl` encoding tables for ROM hacking and retro game translation, bidirectional hex↔text preview |
| **[Hex Editor](Sources/WpfHexEditor.HexEditor/README.md)** | ~65% | Full binary editor — insert/overwrite modes, 460+ format auto-detection, multi-mode search (hex/text/regex/wildcard/TBL), persistent bookmarks, custom encoding tables, block-level undo/redo with VS-style history dropdown |
| **[Diff / Changeset Viewer](Sources/WpfHexEditor.Editor.DiffViewer/README.md)** | ~65% | File comparison tool — binary, text, and structure diff modes with GlyphRun canvas renderers for high performance, word-level highlighting, overview ruler, Myers/Binary/Semantic algorithms, format field overlay for binary diffs |
| **[Markdown Editor](Sources/WpfHexEditor.Editor.MarkdownEditor/README.md)** | ~50% | Markdown authoring — live side-by-side preview, mermaid.js diagram rendering, image paste from clipboard, document outline panel, adaptive render debounce, off-thread word count |
| **[XAML Designer](Sources/WpfHexEditor.Editor.XamlDesigner/README.md)** | ~40% | Visual WPF designer — live canvas with bidirectional XAML↔design sync, move/resize/rotate handles, property inspector (`F4`), alignment guides, snap grid, 4 split layouts, undo/redo, Toolbox panel |
| **[Image Viewer](Sources/WpfHexEditor.Editor.ImageViewer/README.md)** | ~40% | Image preview and editing — zoom/pan, rotate/flip/crop/resize operations, concurrent multi-file open, supports common formats (PNG/JPEG/BMP/GIF/TIFF) |
| **[Text Editor](Sources/WpfHexEditor.Editor.TextEditor/README.md)** | ~40% | Plain text editor — 26 embedded language definitions with auto-detection, encoding support (UTF-8/UTF-16/ASCII/custom), line numbering, basic search |
| **[Script Editor](Sources/WpfHexEditor.Editor.ScriptEditor/README.md)** | ~40% | C# scripting environment — split-view editor with C#Script language support, Roslyn-powered SmartComplete with IDE globals injection, execute scripts to automate IDE workflows |
| **[Document Editor](Sources/WpfHexEditor.Editor.DocumentEditor/README.md)** | ~35% | Rich document editor — WYSIWYG editing for RTF, DOCX, and ODT formats, DrawingContext-based rendering, text formatting toolbar, table support, styles panel, find/replace, page settings, split hex pane for raw inspection |
| **[Entropy Viewer](Sources/WpfHexEditor.Editor.EntropyViewer/README.md)** | ~30% | Binary entropy visualizer — graphical entropy and byte-frequency charts to detect encrypted, compressed, or packed regions at a glance, click-to-navigate to offset |
| **[Structure Editor](Sources/WpfHexEditor.Editor.StructureEditor/README.md)** | ~30% | Binary template editor — visual editor for `.whfmt` format definitions, block DataGrid with field types and offsets, live save to disk |
| **[JSON Editor](Sources/WpfHexEditor.Editor.JsonEditor/README.md)** | ~20% | JSON file viewer — syntax highlighting, auto-detection for `.json` files |
| **[Resx Editor](Sources/WpfHexEditor.Editor.ResxEditor/README.md)** | ~20% | .NET resource editor — view and edit `.resx` resource files with key/value grid, string and file resource support |
| **[Disassembly Viewer](Sources/WpfHexEditor.Editor.DisassemblyViewer/README.md)** | ~12% | Machine code disassembler — x86/x64/ARM instruction decoding via Iced 1.21.0, GlyphRun canvas renderer for fast scrolling, navigate-to-offset integration |
| **[Class Diagram](Sources/WpfHexEditor.Editor.ClassDiagram/README.md)** | ~10% | UML class diagram generator — regex-based C#/VB.NET source analysis, interactive canvas with docking panels for class/interface/enum visualization |
| **[Audio Viewer](Sources/WpfHexEditor.Editor.AudioViewer/README.md)** | ~10% | Audio file visualizer — waveform rendering for WAV, MP3, FLAC, OGG, and AIFF formats, stereo left/right channel display |
| **[Tile Editor](Sources/WpfHexEditor.Editor.TileEditor/README.md)** | ~5% | Tile and sprite editor — planned for ROM asset editing with palette support and pixel-level tools (#175) |
| **Decompiled Source Viewer** | ~0% | .NET decompilation viewer — C# and IL source display via ILSpy, planned (#106) |
| **Memory Snapshot Viewer** | ~0% | Memory dump analyzer — Windows `.dmp` and Linux core-dump inspection, planned (#117) |
| **PCAP Viewer** | ~0% | Network capture viewer — `.pcap`/`.pcapng` packet dissection and hex payload display, planned (#136) |

> New editor? See [IDocumentEditor contract](Sources/WpfHexEditor.Editor.Core/README.md) and register via `EditorRegistry`.

---

## 🧩 Standalone Controls & Libraries

All controls are **independently reusable** — no IDE required.

### UI Controls

| Control | Progress | Description |
|---------|----------|-------------|
| **[HexEditor](Sources/WpfHexEditor.HexEditor/README.md)** | ~70% | Full-featured hex editor control — insert/overwrite modes, 400+ format auto-detection, multi-mode search, bookmarks, TBL encoding, block undo/redo, MVVM architecture with 16 injectable services |
| **[HexBox](Sources/WpfHexEditor.HexBox/README.md)** | ~70% | Lightweight single-value hex input field — drop-in replacement for TextBox, zero external dependencies |
| **[ColorPicker](Sources/WpfHexEditor.ColorPicker/README.md)** | ~90% | RGB/HSV/hex color picker with eyedropper, alpha channel, and recent colors palette |
| **[BarChart](Sources/WpfHexEditor.BarChart/README.md)** | ~80% | Byte-frequency bar chart for binary analysis — dual-target net48 + net8.0-windows |
| **[ProgressBar](Sources/WpfHexEditor.ProgressBar/README.md)** | ~85% | Animated progress and loading indicator — determinate/indeterminate modes, themeable |
| **[Terminal](Sources/WpfHexEditor.Terminal/README.md)** | ~65% | Integrated terminal emulator — multi-tab shell sessions, ANSI color support, command history, plugin-extensible via `ITerminalService` |
| **[Shell](Sources/WpfHexEditor.Shell/README.md)** | ~60% | 100% in-house VS-style docking engine — float/dock/auto-hide/pin, colored tabs, 16 built-in themes, layout undo/redo, serializable workspace state |

### Libraries

| Library | Description |
|---------|-------------|
| **[Core](Sources/WpfHexEditor.Core/README.md)** | Foundation library — ByteProvider (stream-based byte management), 16 injectable services (search, replace, copy, bookmark, undo…), format detection, data layer |
| **[Editor.Core](Sources/WpfHexEditor.Editor.Core/README.md)** | Shared editor infrastructure — `IDocumentEditor` plugin contract, editor registry, changeset tracking, shared `UndoEngine`, middle-click pan mode |
| **[BinaryAnalysis](Sources/WpfHexEditor.BinaryAnalysis/README.md)** | Binary intelligence engine — 400+ format signatures, `.whfmt` v2.0 template parser, type decoders, checksum/assertion validation, DataInspector (40+ types) |
| **[Definitions](Sources/WpfHexEditor.Definitions/README.md)** | Embedded catalog — 400+ binary format signatures, 55+ syntax highlighting definitions (`.whfmt`/`.whlang`), shipped as embedded resources |
| **[Events](Sources/WpfHexEditor.Events/README.md)** | Typed pub/sub event bus — 39+ domain events, weak references to prevent leaks, cross-process IPC bridge for sandboxed plugins |
| **[SDK](Sources/WpfHexEditor.SDK/README.md)** | **Plugin SDK (SemVer 2.0.0 frozen)** — `IWpfHexEditorPlugin` entry point, `IIDEHostContext` host services, 15+ contracts (menus, toolbar, titlebar, panels, status bar, settings, terminal commands) |
| **[Core.Roslyn](Sources/WpfHexEditor.Core.Roslyn/README.md)** | In-process Roslyn integration — C#/VB.NET incremental analysis, replaces external OmniSharp process for faster and more reliable code intelligence |
| **[Core.LSP.Client](Sources/WpfHexEditor.Core.LSP.Client/README.md)** | Language Server Protocol 3.17 client — full JSON-RPC transport, 13 provider types (completion, hover, signature help, code actions, rename, inlay hints, code lens, semantic tokens…), document sync |
| **[Core.Diff](Sources/WpfHexEditor.Core.Diff/README.md)** | Diff engine — Myers (text), binary (byte-level), semantic (structure-aware) algorithms, Git integration, export to HTML/patch |
| **[Core.Workspaces](Sources/WpfHexEditor.Core.Workspaces/README.md)** | Workspace persistence — `.whidews` format (ZIP+JSON), captures and restores full IDE state: dock layout, open files, solution, theme, editor settings |
| **[Core.MCP](Sources/WpfHexEditor.Core.MCP/README.md)** | Model Context Protocol support — JSON-RPC tool definitions enabling AI assistants to interact with IDE services (build, debug, navigate, analyze) |
| **[Core.BuildSystem](Sources/WpfHexEditor.Core.BuildSystem/README.md)** | Build orchestration — MSBuild API integration, parallel project builds, incremental dirty tracking (FileSystemWatcher per project), build progress events |
| **[Core.Debugger](Sources/WpfHexEditor.Core.Debugger/README.md)** | .NET debug adapter — breakpoint management (conditions, hit counts, persistence), step over/into/out, variable evaluation, debug session lifecycle |
| **[Core.Scripting](Sources/WpfHexEditor.Core.Scripting/README.md)** | Script execution engine — C#Script via Roslyn, IDE globals injection (`HxScriptEngine`), REPL support for automation and data exploration |
| **[Core.Terminal](Sources/WpfHexEditor.Core.Terminal/README.md)** | Terminal command engine — 35+ built-in commands, command history with persistence, extensible via `ITerminalService` plugin API |
| **[Core.Commands](Sources/WpfHexEditor.Core.Commands/README.md)** | Command infrastructure — central registry (~100 commands), configurable keyboard shortcuts, conflict detection, Command Palette (`Ctrl+Shift+P`, 9 search modes) |
| **[Core.SourceAnalysis](Sources/WpfHexEditor.Core.SourceAnalysis/README.md)** | Lightweight source analysis — regex-based type/member outline for Solution Explorer tree navigation, BCL-only (no Roslyn dependency) |
| **[Core.AssemblyAnalysis](Sources/WpfHexEditor.Core.AssemblyAnalysis/README.md)** | .NET assembly inspector — `System.Reflection.Metadata` PEReader, type/method/field model, no ILSpy dependency (BCL-only) |
| **[Core.Decompiler](Sources/WpfHexEditor.Core.Decompiler/README.md)** | Decompilation service — `IDecompiler` abstraction with ILSpy backend, C#/VB.NET output, assembly-to-source navigation |
| **[ProjectSystem](Sources/WpfHexEditor.ProjectSystem/README.md)** | Project model — `.whsln`/`.whproj` + VS `.sln`/`.csproj` support, project-to-project references, serialization, New Project dialog with templates |
| **[PluginHost](Sources/WpfHexEditor.PluginHost/README.md)** | Plugin lifecycle manager — discovery (scan + manifest), ALC-isolated loading, health watchdog, hot-reload via `CollectibleAssemblyLoadContext`, Plugin Manager UI |
| **[PluginSandbox](Sources/WpfHexEditor.PluginSandbox/README.md)** | Plugin isolation sandbox — out-of-process host with HWND embedding, bidirectional IPC, Job Object resource limits, crash containment |
| **[Docking.Core](Sources/WpfHexEditor.Docking.Core/README.md)** | Docking abstraction layer — `DockEngine` contracts, layout model (dock/float/auto-hide/tab groups), serializable state |
| **[Options](Sources/WpfHexEditor.Options/README.md)** | Settings framework — `AppSettingsService` with JSON persistence, `OptionsEditorControl` tree UI, 20+ pages, plugin-extensible via `IOptionsPage` |

---

## 🗂️ IDE Panels

| Panel | Progress | Description |
|-------|----------|-------------|
| **[AI Assistant](Sources/Plugins/WpfHexEditor.Plugins.AIAssistant/README.md)** | ~80% | Chat with AI directly in the IDE — supports 5 providers (Anthropic, OpenAI, Gemini, Ollama, Claude Code CLI), 25 MCP tools for deep IDE interaction, streaming responses, inline code apply, @mentions for context injection, conversation history and prompt presets |
| **[Parsed Fields](Sources/Plugins/WpfHexEditor.Plugins.ParsedFields/README.md)** | ~65% | Binary structure viewer — automatically parses 460+ file formats and displays field names, offsets, values, and types in an expandable tree, with FormatNavigator bookmark strip and forensic alert badges for failed integrity checks |
| **[Solution Explorer](Sources/WpfHexEditor.Panels.IDE/README.md)** | ~75% | Project navigation tree — browse virtual and physical folders, drag-and-drop file organization, lazy-loaded source outline showing types and members for quick navigation within `.cs`/`.xaml` files |
| **[Data Inspector](Sources/Plugins/WpfHexEditor.Plugins.DataInspector/README.md)** | ~60% | Byte interpretation panel — shows 40+ data type readings at the current caret position (integers, floats, strings, GUIDs, dates, colors, IP addresses…), updates live as you move through the file |
| **[Options](Sources/WpfHexEditor.Options/README.md)** | ~70% | Settings center — 30+ options pages organized in a tree (Environment, Hex Editor, Code Editor, Text Editor, Plugin System, Build & Run, Debugger, Tools), searchable, plugin-extensible |
| **[Output](Sources/WpfHexEditor.Panels.IDE/README.md)** | ~70% | Build and log output — displays build results with severity-colored messages (info/warning/error/success), session log channel, auto-scroll with manual override |
| **[Call Hierarchy](Sources/Plugins/WpfHexEditor.Plugins.LSPTools/README.md)** | ~65% | Call chain navigator — view all incoming and outgoing function calls for any symbol via LSP 3.17, expandable tree with file locations (`Shift+Alt+H`) |
| **[Type Hierarchy](Sources/Plugins/WpfHexEditor.Plugins.LSPTools/README.md)** | ~65% | Inheritance viewer — explore supertypes (base classes) and subtypes (derived classes) for any type via LSP 3.17, click to navigate (`Ctrl+Alt+F12`) |
| **[Error List](Sources/WpfHexEditor.Panels.IDE/README.md)** | ~65% | Diagnostic aggregator — collects errors and warnings from all open editors and build results, click any entry to jump directly to the source file and line |
| **[Terminal](Sources/WpfHexEditor.Terminal/README.md)** | ~65% | Integrated terminal — multi-tab shell sessions (`Ctrl+\``), 35+ built-in commands, ANSI color support, extensible by plugins via `ITerminalService` API |
| **[Unit Testing](Sources/Plugins/WpfHexEditor.Plugins.UnitTesting/README.md)** | ~60% | Test runner panel — auto-detects xunit, nunit, and mstest projects, runs tests via `dotnet test`, displays results with pass/fail/skip counters and duration, auto-run on build success |
| **Quick Search** | ~60% | Inline find overlay (`Ctrl+F`) — find next/previous with regex toggle, match highlighting across the document |
| **[File Comparison](Sources/Plugins/WpfHexEditor.Plugins.FileComparison/README.md)** | ~55% | File diff launcher — compare any two files with synchronized scrolling, DiffHub panel for quick access to recent comparisons, opens full diff viewer document |
| **[Breakpoint Explorer](Sources/WpfHexEditor.Panels.IDE/README.md)** | ~55% | Breakpoint management panel — lists all breakpoints across the solution with conditions, hit counts, enable/disable toggle, and one-click jump to source location |
| **[Plugin Manager](Sources/WpfHexEditor.PluginHost/README.md)** | ~55% | Plugin administration — browse installed plugins, enable/disable individually, view dependencies, uninstall, and check for compatibility |
| **[Format Info](Sources/Plugins/WpfHexEditor.Plugins.FormatInfo/README.md)** | ~50% | File format identifier — displays the detected format name, MIME type, magic bytes signature, and section list for the currently open file |
| **[File Statistics](Sources/Plugins/WpfHexEditor.Plugins.FileStatistics/README.md)** | ~50% | Binary analysis dashboard — byte-frequency distribution chart, Shannon entropy score, file size breakdown, useful for identifying encrypted or compressed regions |
| **[Properties](Sources/WpfHexEditor.Panels.IDE/README.md)** | ~50% | Context-aware property inspector (`F4`) — displays categorized properties for the selected item (file, project, editor element) with debounced updates |
| **[Plugin Monitoring](Sources/WpfHexEditor.Panels.IDE/README.md)** | ~50% | Plugin health dashboard — real-time CPU and memory usage charts per plugin, helps identify resource-hungry or misbehaving extensions |
| **[Archive Explorer](Sources/Plugins/WpfHexEditor.Plugins.ArchiveStructure/README.md)** | ~45% | Archive file browser — open ZIP, 7z, and TAR archives as navigable trees, extract individual entries, preview binary content directly in the hex view without extracting |
| **[Structure Overlay](Sources/Plugins/WpfHexEditor.Plugins.StructureOverlay/README.md)** | ~40% | Format field highlighter — color-codes binary structure fields directly on the hex grid based on the detected `.whfmt` format definition, hover for field details |
| **Advanced Search** | ~40% | Multi-mode search panel — 5 search modes: Hex pattern, plain text, regex, TBL-encoded, and wildcard matching |
| **[Pattern Analysis](Sources/Plugins/WpfHexEditor.Plugins.PatternAnalysis/README.md)** | ~35% | Pattern scanner — detects known byte sequences, recognizable data structures, and anomalies within binary files for reverse engineering and forensic analysis |
| **[Assembly Explorer](Sources/Plugins/WpfHexEditor.Plugins.AssemblyExplorer/README.md)** | ~30% | .NET assembly browser — load any .NET DLL/EXE to inspect namespaces, types, methods, and fields in a tree view, double-click to decompile to C# or VB.NET in a syntax-highlighted Code Editor tab |
| **[Custom Parser Template](Sources/Plugins/WpfHexEditor.Plugins.CustomParserTemplate/README.md)** | ~25% | Template-driven parser — define custom binary structure schemas (similar to 010 Editor `.bt` templates) and see live parsed field output in the Parsed Fields panel |

---

## 📸 Screenshots

<div align="center">
  <b>🖥️ IDE Overview</b><br/>
  <sub>VS-style docking with Solution Explorer, HexEditor and ParsedFieldsPanel</sub><br/><br/>
  <a href="Images/App-IDE-Overview.png"><img src="Images/App-IDE-Overview.png" alt="IDE Overview" width="900"/></a>
</div>

<details>
<summary>More screenshots</summary>
<br/>

| | |
|---|---|
| <a href="Images/App-ParsedFields.png"><img src="Images/App-ParsedFields.png" alt="Parsed Fields" width="440"/></a><br/><sub>🔬 Parsed Fields — 400+ format detection</sub> | <a href="Images/App-Editors.png"><img src="Images/App-Editors.png" alt="Multiple Editors" width="440"/></a><br/><sub>📝 Multi-Editor Tabs</sub> |
| <a href="Images/App-SolutionExplorer.png"><img src="Images/App-SolutionExplorer.png" alt="Solution Explorer" width="440"/></a><br/><sub>🗂️ Solution Explorer</sub> | <a href="Images/App-Theme-Light.png"><img src="Images/App-Theme-Light.png" alt="Light Theme" width="440"/></a><br/><sub>☀️ Light Theme (16 built-in themes)</sub> |
| <a href="Images/App-Output.png"><img src="Images/App-Output.png" alt="Output Panel" width="440"/></a><br/><sub>📤 Output Panel</sub> | <a href="Images/App-ErrorList.png"><img src="Images/App-ErrorList.png" alt="Error Panel" width="440"/></a><br/><sub>🔴 Error Panel</sub> |
| <a href="Images/App-TBLEditor.png"><img src="Images/App-TBLEditor.png" alt="TBL Editor" width="440"/></a><br/><sub>📋 TBL Editor</sub> | <a href="Images/TBLExplain.png"><img src="Images/TBLExplain.png" alt="TBL Explained" width="440"/></a><br/><sub>🎮 TBL Format</sub> |

</details>

---

## ⚡ Quick Start

**Run the IDE:**
```bash
git clone https://github.com/abbaye/WpfHexEditorIDE.git
```
Open `WpfHexEditorControl.sln`, set **WpfHexEditor.App** as startup project, press F5.

> Developed on **Visual Studio 2026**. Compatible with **VS 2022** (v17.8+) and **JetBrains Rider**.

**Embed the HexEditor in your WPF app:**
```xml
<!-- Project reference -->
<ProjectReference Include="..\WpfHexEditor.Core\WpfHexEditor.Core.csproj" />
<ProjectReference Include="..\WpfHexEditor.HexEditor\WpfHexEditor.HexEditor.csproj" />
```
```xml
<!-- XAML -->
<Window xmlns:hex="clr-namespace:WpfHexEditor.HexEditor;assembly=WpfHexEditor.HexEditor">
  <hex:HexEditor FileName="data.bin" />
</Window>
```

> **[Complete Tutorial →](docs/GETTING_STARTED.md)** · NuGet packaging planned (#109)

---

## 🗺️ Roadmap

> Full details: **[ROADMAP.md](docs/ROADMAP.md)** · **[CHANGELOG.md](docs/CHANGELOG.md)**

**In Progress:**

| Feature | Status | # |
|---------|--------|---|
| **Code Editor Phase 2** — remaining: peek definition (`Alt+F12`), gutter change markers, code minimap, expand/collapse all folds | 🔧 ~93% | #158–168 |
| **.NET Debugger** — UI complete (menus, toolbar, breakpoints, explorer), remaining: runtime attach and debug launch | 🔧 ~60% | #44, #90 |
| **Assembly Explorer Phase 2** — full ILSpy backend, ECMA-335 token→offset, hex sync, PDB source-link | 🔧 ~75% | #104–106, #186 |
| **Integrated Terminal Phase 2** — ANSI/Xterm emulation, split panes, SSH/WSL, macro recording | 🔧 ~70% | #92, #180 |
| **Document Model Phase 2** — multi-editor collaboration, cross-editor undo/redo unification | 🔧 ~70% | #107 |
| **LSP Phase 3 + Roslyn** — Roslyn in-process ✅ done, remaining: F# parser, cross-language symbol resolution | 🔧 ~60% | #190–193 |
| **Code Intelligence** — AI Assistant ✅ done, SmartComplete ✅ done, remaining: inline ghost-text completions, AI refactoring | 🔧 ~70% | #86–89 |
| **In-IDE Plugin Development** — hot-reload ✅ done, remaining: integrated scaffolding, debug-in-IDE, packaging UI | 🔧 ~40% | #138 |

**Planned:**

| Feature | Status | # |
|---------|--------|---|
| **Editors Phase 2** — TextEditor LSP, DiffViewer 3-way merge, AudioViewer playback, TileEditor pixel tools | 🔜 Planned | #169–178 |
| **Git Integration** — commit/push/pull/branch, inline gutter diff, blame | 🔜 Planned | #91 |
| **Plugin Marketplace & Auto-Update** — online registry, signed packages, auto-update | 🔜 Planned | #41–43 |
| **IDE Localization Engine** — full i18n for IDE UI (EN/FR initial, plugin-provided translations) | 🔜 Planned | #100 |
| **Installable Package** — MSI / MSIX / WinGet, auto-update channel, no SDK required | 🔜 Planned | #109 |
| **Official Website** — landing page, feature showcase, documentation browser, plugin registry | 🔜 Planned | #108 |

<details>
<summary>✅ Completed features</summary>

| Feature | Version |
|---------|---------|
| **AI Assistant Plugin** — multi-provider AI chat with 5 built-in providers, 25 MCP IDE tools, streaming responses, inline code apply, @mentions for context, conversation history | v0.6.4.3 |
| **Roslyn Integration** — in-process C#/VB.NET code analysis replacing external OmniSharp process | v0.6.4.3 |
| **Document Editor** — WYSIWYG rich document editing for RTF, DOCX, and ODT with formatting toolbar, tables, styles panel, find/replace, page settings | v0.6.4.1 |
| **Binary Format Engine v2.0** — repeating blocks, unions, versioned structures, pointers, checksums, assertions, forensic alerts across 20 critical formats (PE/ELF/ZIP/PNG/MP4/SQLite/PDF…) | v0.6.4.1 |
| **Diff Viewer Upgrade** — high-performance GlyphRun canvas renderers for binary, text, and structure diffs with word-level highlighting and format field overlay | v0.6.4.1 |
| **Breakpoint System** — full breakpoint management with conditions, hit counts, solution-scoped persistence, Breakpoint Explorer panel, right-click gutter editing popup | v0.6.4.1 |
| **Call & Type Hierarchy** — LSP-powered call hierarchy (`Shift+Alt+H`) and type hierarchy (`Ctrl+Alt+F12`) panels with linked editing ranges | v0.6.4.1 |
| **Archive Explorer** — browse ZIP, 7z, and TAR archives as trees, extract entries, preview binary content in-place | v0.6.4.1 |
| **Code Editor Enhancements** — column rulers, bracket-depth colorizer (4 levels), inline color swatch preview, format-on-save, auto-close brackets/quotes | v0.6.4.1 |
| **Plugin Hot-Reload** — live-reload plugins during development without restarting the IDE, cascade reload for dependencies | v0.6.4.1 |
| **Script Editor** — split-view C#Script editor with Roslyn-powered SmartComplete and IDE globals injection | v0.6.4.1 |
| **Dynamic View Menu** — organize panels by category, dock side, or flat list with pin favorites | v0.6.4.1 |
| **Middle-click Pan** — hold middle mouse button to pan across all editors and viewports | v0.6.4.1 |
| **Debugger UI Foundation** — debug menu, toolbar pod, execution line highlight, gutter hover ghost, 11-bug audit fix | v0.6.4.1 |
| **HexEditor Block Undo** — atomic undo for paste/cut/delete, coalescence for hex digit typing, VS-style history dropdown | v0.6.3.8 |
| **Sticky Scroll** — scope headers pinned at top while scrolling, allocation-free rendering, click-to-navigate | v0.6.3.7 |
| **Find All References** — `Shift+F12` with dockable results panel, `F8`/`Shift+F8` navigation between matches | v0.6.3.7 |
| **Workspace System** — save and restore full IDE state (layout, files, theme, solution) as `.whidews` workspace files | v0.6.3.7 |
| **Compare Files** — Myers, binary, and semantic diff algorithms with Git integration, DiffHub launcher, export as patch | v0.6.3.7 |
| **End-of-Block Hover Hint** — hover over `}`, `#endregion`, or `</Tag>` to see the opening block header in a VS-style popup | v0.6.3.7 |
| **LSP Engine** — full JSON-RPC Language Server Protocol client with 13 provider types, breadcrumb bar, inlay hints, code lens, semantic tokens | v0.6.3.6 |
| **Command Palette** — `Ctrl+Shift+P` with 9 search modes (commands, files, symbols, go-to-line, recent, help…) | v0.6.3.6 |
| **IDE EventBus** — typed pub/sub event system with 39+ domain events and cross-process IPC bridge for sandboxed plugins | v0.6.3.6 |
| **VS Solution + MSBuild** — open Visual Studio solutions, build/rebuild/clean via MSBuild API with incremental dirty tracking | v0.5.0 |

</details>

---

## 📚 Documentation

| | |
|---|---|
| **[GETTING_STARTED.md](docs/GETTING_STARTED.md)** | Run the IDE or embed the control |
| **[FEATURES.md](docs/FEATURES.md)** | Complete feature list |
| **[CHANGELOG.md](docs/CHANGELOG.md)** | Version history |
| **[MIGRATION.md](docs/migration/MIGRATION.md)** | Legacy V1 → V2 migration |
| **[Architecture Overview](docs/architecture/Overview.md)** | Services, MVVM, data flow |
| **[API Reference](docs/api-reference/)** | Full API docs with examples |
| **[Wiki](https://github.com/abbaye/WpfHexEditorIDE/wiki/Getting-Started)** | Getting started |

---

## 🔧 Requirements

**.NET 8.0-windows** — Span\<T\>, SIMD, PGO. .NET Framework 4.8 is no longer supported (use legacy NuGet `WPFHexaEditor` for .NET Framework).

**HexEditor control** supports 19 UI languages (English · French · Spanish · German · Italian · Japanese · Korean · Dutch · Polish · Portuguese · Russian · Swedish · Turkish · Chinese · Arabic · Hindi · and more) with instant runtime switching. IDE UI is English only — localization engine planned (#100).

---

## ⭐ Support & Contributing

WPF HexEditor is **100% free and open source** (GNU AGPL v3.0).

- ⭐ **Star this repo** — helps others discover it
- 🍴 **Fork & contribute** — see **[CONTRIBUTING.md](.github/CONTRIBUTING.md)**
- 🐛 **Bug reports** — [GitHub Issues](https://github.com/abbaye/WpfHexEditorIDE/issues)
- 💡 **Feature requests** — [GitHub Discussions](https://github.com/abbaye/WpfHexEditorIDE/discussions)
- 📧 **Email** — derektremblay666@gmail.com

---

<div align="center">
  <sub>Built with ❤️ by the WpfHexEditor community · AGPL v3.0</sub>
</div>
