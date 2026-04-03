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
| **🏗️ Project System** | Open and build `.whsln`/`.whproj` native projects or Visual Studio `.sln`/`.csproj`/`.vbproj` solutions — MSBuild build/rebuild/clean with parallel project compilation, real-time build progress bar, virtual and physical folder organization, per-file editor state persistence, project template scaffolding |
| **🪟 Docking** *(100% in-house)* | Fully custom VS-style docking engine — float, dock, auto-hide, pin, colored tab strips, **19 built-in themes** (Dark, Light, VS2022Dark, DarkGlass, Dracula, Nord, Tokyo Night, Catppuccin Mocha/Latte, Gruvbox Dark, Forest, Matrix, Synthwave 84, Cyberpunk, High Contrast…), tab placement left/right/bottom, layout undo/redo (`Ctrl+Shift+Z/Y`), serializable workspace state |
| **📋 IDE Infrastructure** | Unified editor plugin architecture via `IDocumentEditor` — shared undo engine with coalescence and VS-style history dropdown, `Ctrl+Z/Y` across all editors, rectangular block selection (`Alt+Click`), adaptive status bar, 20+ options pages, workspace system (`.whidews` save/restore), dynamic View menu (Flat/Categorized/ByDockSide), middle-click pan mode, NuGet Solution Manager, dependency injection via `Microsoft.Extensions.DependencyInjection` |
| **🧠 Code Intelligence** | **In-process Roslyn** for C#/VB.NET analysis (no external OmniSharp process) — full LSP 3.17 client supporting 13 provider types: completion, hover, signature help, code actions, rename, inlay hints, code lens, semantic tokens, call hierarchy, type hierarchy, pull diagnostics, linked editing, breadcrumb navigation bar |
| **⌨️ Command & Terminal** | Command Palette (`Ctrl+Shift+P`) with 9 search modes (commands, files, symbols, go-to-line…) — central command registry (~50 commands) with configurable keyboard shortcuts, integrated multi-tab terminal (`Ctrl+\``) with 35+ built-in commands including `plugin-reload`, extensible by plugins via `ITerminalService` API |
| **🤖 AI Assistant** | Multi-provider AI chat panel — supports Anthropic, OpenAI, Google Gemini, Ollama, and Claude Code CLI out of the box, 24 MCP tools for deep IDE integration (build, debug, navigate, analyze), streaming responses with inline code apply, `@mentions` for context injection (`@file`/`@selection`/`@errors`/`@solution`), dedicated command palette (`Ctrl+Shift+A`), conversation history, customizable prompt presets |
| **🐞 .NET Debugger** *(~60%)* | Integrated .NET debugging UI — Debug menu (Start/Stop/Restart, Step Over/Into/Out), collapsible toolbar pod, execution line highlight, full breakpoint system with conditions, hit counts, enable/disable toggle, and solution-scoped persistence, Breakpoint Explorer panel, right-click gutter popup for breakpoint editing, debug status bar · ⚠️ *Debug launch not yet functional — UI and breakpoint infrastructure are ready, runtime attach pending* |
| **🧪 Unit Testing** | Built-in test runner — auto-detects xunit, nunit, and mstest projects, runs via `dotnet test` with TRX result parsing, pass/fail/skip counters with color-coded outcome glyphs, context-sensitive detail panel (project summary, class summary, or individual test details), auto-run on successful build |
| **🔌 Plugin System** | Extensible plugin architecture — SDK 2.0.0 (API frozen, semver), `.whxplugin` package format, Plugin Manager UI, typed EventBus (39+ domain events), capability registry, extension points (menus, toolbar, titlebar, panels, status bar, terminal commands), dependency graph, plugin signing with signature validation, out-of-process sandbox with HWND embedding — **27 built-in plugins** ship with the IDE |
| **🔍 Binary Intelligence** | Deep binary analysis toolkit — 400+ format auto-detection, `.whfmt` v2.0 template engine (repeating blocks, unions, versioned structures, pointers, checksums, assertions, forensic alerts, AI hints), 20 critical formats fully defined (PE, ELF, ZIP, PNG, MP4, SQLite, PDF, JPEG, WASM…), reactive Parsed Fields panel with FormatNavigator bookmarks, format field overlay on hex view, Data Inspector (40+ type interpretations), Assembly Explorer (.NET PE tree + ILSpy C#/VB.NET decompilation) |

---

## 📝 Editors

Every editor is a standalone `IDocumentEditor` plugin — reusable outside the IDE.

| Editor | Progress | Description |
|--------|----------|-------------|
| **[Hex Editor](Sources/WpfHexEditor.HexEditor/README.md)** | ~65% | Full binary editor — insert/overwrite modes, 400+ format auto-detection, multi-mode search (hex/text/regex/wildcard/TBL), persistent bookmarks, custom encoding tables, block-level undo/redo with VS-style history dropdown |
| **[Code Editor](Sources/WpfHexEditor.Editor.CodeEditor/README.md)** | ~85% | Advanced source editor — 55+ languages (incl. F# + VB.NET), **Roslyn in-process C#/VB.NET analysis**, full LSP 3.17 suite (13 providers: completion, hover, signature help, code actions, rename, inlay hints, code lens, semantic tokens, call/type hierarchy, linked editing, pull diagnostics), sticky scroll, Find All References (`Shift+F12`), multi-caret (`Ctrl+Alt+Click`), bracket-depth colorizer, color swatch preview, column rulers, format-on-save, split view |
| **[XAML Designer](Sources/WpfHexEditor.Editor.XamlDesigner/README.md)** | ~40% | Visual WPF designer — live canvas with bidirectional XAML↔design sync, move/resize/rotate handles, property inspector (`F4`), alignment guides, snap grid, 4 split layouts, undo/redo, Toolbox panel |
| **[Document Editor](Sources/WpfHexEditor.Editor.DocumentEditor/README.md)** | ~35% | Rich document editor — WYSIWYG editing for RTF, DOCX, and ODT formats, DrawingContext-based rendering, text formatting toolbar, table support, styles panel, find/replace, page settings, split hex pane for raw inspection |
| **[Markdown Editor](Sources/WpfHexEditor.Editor.MarkdownEditor/README.md)** | ~50% | Markdown authoring — live side-by-side preview, mermaid.js diagram rendering, image paste from clipboard, document outline panel, adaptive render debounce, off-thread word count |
| **[JSON Editor](Sources/WpfHexEditor.Editor.JsonEditor/README.md)** | ~20% | JSON file viewer — syntax highlighting, auto-detection for `.json` files |
| **[TBL Editor](Sources/WpfHexEditor.Editor.TblEditor/README.md)** | ~75% | Character table editor — create and edit custom `.tbl` encoding tables for ROM hacking and retro game translation, bidirectional hex↔text preview |
| **[Text Editor](Sources/WpfHexEditor.Editor.TextEditor/README.md)** | ~40% | Plain text editor — 26 embedded language definitions with auto-detection, encoding support (UTF-8/UTF-16/ASCII/custom), line numbering, basic search |
| **[Diff / Changeset Viewer](Sources/WpfHexEditor.Editor.DiffViewer/README.md)** | ~65% | File comparison tool — binary, text, and structure diff modes with GlyphRun canvas renderers for high performance, word-level highlighting, overview ruler, Myers/Binary/Semantic algorithms, format field overlay for binary diffs |
| **[Entropy Viewer](Sources/WpfHexEditor.Editor.EntropyViewer/README.md)** | ~30% | Binary entropy visualizer — graphical entropy and byte-frequency charts to detect encrypted, compressed, or packed regions at a glance, click-to-navigate to offset |
| **[Image Viewer](Sources/WpfHexEditor.Editor.ImageViewer/README.md)** | ~40% | Image preview and editing — zoom/pan, rotate/flip/crop/resize operations, concurrent multi-file open, supports common formats (PNG/JPEG/BMP/GIF/TIFF) |
| **[Structure Editor](Sources/WpfHexEditor.Editor.StructureEditor/README.md)** | ~30% | Binary template editor — visual editor for `.whfmt` format definitions, block DataGrid with field types and offsets, live save to disk |
| **[Disassembly Viewer](Sources/WpfHexEditor.Editor.DisassemblyViewer/README.md)** | ~12% | Machine code disassembler — x86/x64/ARM instruction decoding via Iced 1.21.0, GlyphRun canvas renderer for fast scrolling, navigate-to-offset integration |
| **[Script Editor](Sources/WpfHexEditor.Editor.ScriptEditor/README.md)** | ~40% | C# scripting environment — split-view editor with C#Script language support, Roslyn-powered SmartComplete with IDE globals injection, execute scripts to automate IDE workflows |
| **[Audio Viewer](Sources/WpfHexEditor.Editor.AudioViewer/README.md)** | ~10% | Audio file visualizer — waveform rendering for WAV, MP3, FLAC, OGG, and AIFF formats, stereo left/right channel display |
| **[Class Diagram](Sources/WpfHexEditor.Editor.ClassDiagram/README.md)** | ~10% | UML class diagram generator — regex-based C#/VB.NET source analysis, interactive canvas with docking panels for class/interface/enum visualization |
| **[Resx Editor](Sources/WpfHexEditor.Editor.ResxEditor/README.md)** | ~20% | .NET resource editor — view and edit `.resx` resource files with key/value grid, string and file resource support |
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
| **[Shell](Sources/WpfHexEditor.Shell/README.md)** | ~60% | 100% in-house VS-style docking engine — float/dock/auto-hide/pin, colored tabs, 19 built-in themes, layout undo/redo, serializable workspace state |

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
| **[Core.Commands](Sources/WpfHexEditor.Core.Commands/README.md)** | Command infrastructure — central registry (~50 commands), configurable keyboard shortcuts, conflict detection, Command Palette (`Ctrl+Shift+P`, 9 search modes) |
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
| **[AI Assistant](Sources/Plugins/WpfHexEditor.Plugins.AIAssistant/README.md)** | ~80% | Multi-provider AI chat — Anthropic/OpenAI/Gemini/Ollama/Claude Code CLI, 24 MCP tools, streaming, inline apply, @mentions |
| **[Solution Explorer](Sources/WpfHexEditor.Panels.IDE/README.md)** | ~75% | Project tree — virtual/physical folders, D&D, lazy source outline (types/members navigation) |
| **[Parsed Fields](Sources/Plugins/WpfHexEditor.Plugins.ParsedFields/README.md)** | ~65% | 400+ format detection — reactive, expandable groups, FormatNavigator, forensic alerts |
| **[Data Inspector](Sources/Plugins/WpfHexEditor.Plugins.DataInspector/README.md)** | ~60% | 40+ byte interpretations at caret (int/float/GUID/date/color/…) |
| **[Assembly Explorer](Sources/Plugins/WpfHexEditor.Plugins.AssemblyExplorer/README.md)** | ~30% | .NET PE tree — types/methods/fields; C# decompilation → Code Editor tab |
| **[Archive Explorer](Sources/Plugins/WpfHexEditor.Plugins.ArchiveStructure/README.md)** | ~45% | ZIP/RAR/7z/TAR tree — browse, extract, in-place hex preview |
| **[Call Hierarchy](Sources/Plugins/WpfHexEditor.Plugins.LSPTools/README.md)** | ~65% | LSP 3.17 — incoming/outgoing call tree (`Shift+Alt+H`) |
| **[Type Hierarchy](Sources/Plugins/WpfHexEditor.Plugins.LSPTools/README.md)** | ~65% | LSP 3.17 — supertypes/subtypes tree (`Ctrl+Alt+F12`) |
| **[Breakpoint Explorer](Sources/WpfHexEditor.Panels.IDE/README.md)** | ~55% | Solution-scoped breakpoints — hit counts, enable/disable, jump-to-source |
| **[File Comparison](Sources/Plugins/WpfHexEditor.Plugins.FileComparison/README.md)** | ~55% | Byte-level diff, synchronized scrolling, DiffHub launcher |
| **[Structure Overlay](Sources/Plugins/WpfHexEditor.Plugins.StructureOverlay/README.md)** | ~40% | Visual field highlighting on the hex grid |
| **[Format Info](Sources/Plugins/WpfHexEditor.Plugins.FormatInfo/README.md)** | ~50% | Detected format, MIME, magic bytes, section list |
| **[File Statistics](Sources/Plugins/WpfHexEditor.Plugins.FileStatistics/README.md)** | ~50% | Byte-frequency charts, entropy score, size breakdown |
| **[Pattern Analysis](Sources/Plugins/WpfHexEditor.Plugins.PatternAnalysis/README.md)** | ~35% | Known byte sequences, data structures and anomaly detection |
| **[Custom Parser Template](Sources/Plugins/WpfHexEditor.Plugins.CustomParserTemplate/README.md)** | ~25% | `.bt`-style schema → live parsed fields |
| **[Terminal Panel](Sources/WpfHexEditor.Terminal/README.md)** | ~65% | Multi-tab integrated terminal — `ITerminalService` plugin API |
| **[Output Panel](Sources/WpfHexEditor.Panels.IDE/README.md)** | ~70% | Build channel with severity colors, session log, auto-scroll |
| **[Error Panel](Sources/WpfHexEditor.Panels.IDE/README.md)** | ~65% | Diagnostics from any `IDiagnosticSource` editor |
| **[Properties Panel](Sources/WpfHexEditor.Panels.IDE/README.md)** | ~50% | Context-aware properties (F4) — 400 ms debounce, categorized groups |
| **[Plugin Manager](Sources/WpfHexEditor.PluginHost/README.md)** | ~55% | Browse, enable/disable, uninstall plugins |
| **[Plugin Monitoring](Sources/WpfHexEditor.Panels.IDE/README.md)** | ~50% | Real-time CPU% + memory charts per plugin (pure WPF Canvas/Polyline) |
| **[Options](Sources/WpfHexEditor.Options/README.md)** | ~70% | VS2026-style settings — 20+ pages across Environment, Hex Editor, Code Editor (Appearance & Colors / Inline Hints / Navigation / Features / Language Servers), Text Editor, Plugin System, Build & Run, Debugger, Tools |
| **Quick Search** | ~60% | Inline `Ctrl+F` overlay — find next/prev, regex toggle |
| **Advanced Search** | ~40% | 5 modes: Hex, Text, Regex, TBL, Wildcard |

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
| <a href="Images/App-SolutionExplorer.png"><img src="Images/App-SolutionExplorer.png" alt="Solution Explorer" width="440"/></a><br/><sub>🗂️ Solution Explorer</sub> | <a href="Images/App-Theme-Light.png"><img src="Images/App-Theme-Light.png" alt="Light Theme" width="440"/></a><br/><sub>☀️ Light Theme (19 built-in themes)</sub> |
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

**In Progress / Planned:**

| Feature | Status | # |
|---------|--------|---|
| **Code Editor Phase 2** — gutter change markers, peek definition, remaining LSP providers | 🔧 ~93% | #158–168 |
| **Assembly Explorer** — ILSpy backend, Ctrl+Click cross-assembly nav | 🔧 ~75% | #104–106 |
| **Document Model Phase 2** — multi-editor collaboration, buffer unification | 🔧 ~40% | #107 |
| **Integrated Terminal** — full multi-shell + macro recording | 🔧 ~70% | #92 |
| **Editors Phase 2** — TextEditor LSP, DiffViewer 3-way merge, AudioViewer, TileEditor | 🔜 Planned | #169–178 |
| **LSP Phase 3 + Roslyn** — Roslyn C#/VB.NET/F# parser (pull-diagnostics + linked editing ✅ done) | 🔧 ~40% | #190–191 |
| **AI Assistant** — Multi-provider chat, 24 MCP tools, streaming, inline apply, @mentions | ✅ Done | — |
| **Code Intelligence** — AI suggestions (SmartComplete ✅ done · Snippets ✅ done · AI pending) | 🔧 ~65% | #86–89 |
| **Git Integration** | 🔜 Planned | #91 |
| **Plugin Marketplace & Auto-Update** | 🔜 Planned | #41–43 |
| **IDE Localization Engine** | 🔜 Planned | #100 |
| **Installable Package** — MSI / MSIX / WinGet | 🔜 Planned | #109 |
| **In-IDE Plugin Development** — write/debug/deploy plugins in the IDE (hot-reload ✅ done) | 🔧 ~40% | #138 |

<details>
<summary>✅ Completed features</summary>

| Feature | Version |
|---------|---------|
| **AI Assistant Plugin** — 5 providers (Anthropic/OpenAI/Gemini/Ollama/Claude Code CLI), 24 MCP tools, streaming chat, inline apply, @mentions, command palette, conversation history, prompt presets | v0.6.4.3 |
| **Roslyn Integration** — In-process C#/VB.NET analysis replacing OmniSharp | v0.6.4.3 |
| **Document Editor WYSIWYG** — RTF/DOCX/ODT, DrawingContext renderer, cursor/formatting/tables, styles panel, find/replace, page settings, 3 savers, 22 `DE_*` tokens | v0.6.4.1 |
| **WHFMT v2.0** — `TypeDecoderRegistry`, `ChecksumEngine`, `AssertionRunner`, `repeating`/`union`/`versionedBlocks`/`pointer`, 20 critical formats, FormatNavigator, forensic alerts | v0.6.4.1 |
| **Diff Canvas Overkill** — `BinaryDiffCanvas`/`TextDiffCanvas`/`StructureDiffCanvas` GlyphRun renderers, O(n) builder, O(1) patience sort, ThreadStatic pools, format overlay | v0.6.4.1 |
| **Breakpoint System Overkill** — hover popup, gutter ghost, solution-scoped persistence, hit counts, Breakpoint Explorer panel | v0.6.4.1 |
| **LSP Tools Plugin** — Call Hierarchy (`Shift+Alt+H`), Type Hierarchy (`Ctrl+Alt+F12`), Linked Editing Range | v0.6.4.1 |
| **Archive Explorer Plugin** — ZIP/7z/TAR tree, extract, in-place hex preview, `AR_*` tokens | v0.6.4.1 |
| **Code Editor Phase 2** — column rulers (`CE_RulerBrush`), bracket-depth colorizer (4-level `CE_Bracket_1/2/3/4`), color swatch preview, `BasicIndentFormatter`, format-on-save, `BracketDepthColorizer` direct-scan engine | v0.6.4.1 |
| **LSP C1/C2** — pull-diagnostics (LSP 3.18), linked editing ranges (`textDocument/linkedEditingRange`) | v0.6.4.1 |
| **CollectibleALC Hot-Reload** — `IWpfHexEditorPluginV2.ReloadAsync`, `PluginDevLoader` FileSystemWatcher, `plugin-reload` terminal command, cascade reload | v0.6.4.1 |
| **Script Editor Overkill** — `CodeEditorSplitHost` + C#Script language definition, `ScriptGlobalsCompletionProvider`, SmartComplete for script globals | v0.6.4.1 |
| **Options Tree Reorganization** — Code Editor: Appearance & Colors / Inline Hints / Navigation / Features / Language Servers | v0.6.4.1 |
| **Dynamic View Menu** — Flat/Categorized/ByDockSide modes, pin system | v0.6.4.1 |
| **Middle-click Pan Mode** — `PanModeController` across all editors/viewports | v0.6.4.1 |
| **Debugger Full Audit** — 11-bug fix, BP line highlight, `DB_*` tokens | v0.6.4.1 |
| **Architectural Refactoring 1–3** — `CompositeDisposable`, service extractions, `CodeEditor.cs` split into 5 partials | v0.6.4.1 |
| **HexEditor Block Undo** — `UndoGroup`, `BeginUndoTransaction`, coalescence, VS-style history dropdown | v0.6.3.8 |
| **Sticky Scroll** — allocation-free `OnRender`, 6 options, perf fix (100% CPU loop eliminated) | v0.6.3.7 |
| **Find All References** — `Shift+F12`, dockable panel, `F8` nav | v0.6.3.7 |
| **Integrated Debugger UI** — Debug menu, toolbar pod, execution line highlight, `BreakpointInfoPopup` | v0.6.3.7 |
| **Workspace System** — `.whidews` ZIP+JSON, layout/solution/file/theme restore | v0.6.3.7 |
| **Compare Files Overkill** — Myers/Binary/Semantic engines, Git diff, `DiffExportService`, `DiffHub` | v0.6.3.7 |
| **End-of-Block Hover Hint** — `}` / `#endregion` / `</Tag>` → VS-style popup | v0.6.3.7 |
| **LSP Engine** — full JSON-RPC client, 13 providers, breadcrumb, inlay hints, code lens, semantic tokens | v0.6.3.6 |
| **Command Palette** — `Ctrl+Shift+P`, 9 modes | v0.6.3.6 |
| **IDE EventBus** — 39+ typed events, IPC bridge | v0.6.3.6 |
| **VS `.sln` / MSBuild build** — incremental dirty tracking | v0.5.0 |

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
