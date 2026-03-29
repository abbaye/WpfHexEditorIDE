<div align="center">
  <a href="Images/Logo2026.png"><img src="Images/Logo2026.png" width="600" height="250" /></a>
  <br/><br/>

  <h3>🖥️ A full-featured open-source IDE for .NET — Binary analysis, reverse engineering & build tooling</h3>

[![.NET](https://img.shields.io/badge/.NET-8.0--windows-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
  [![Platform](https://img.shields.io/badge/Platform-Windows%20WPF-0078D4?logo=windows)](https://github.com/abbaye/WpfHexEditorIDE)
  [![IDE Version](https://img.shields.io/badge/IDE-v0.6.3.8-6A0DAD?logo=visualstudiocode&logoColor=white)](https://github.com/abbaye/WpfHexEditorIDE/releases)
  [![License: AGPL v3](https://img.shields.io/badge/License-AGPL_v3-blue.svg)](https://www.gnu.org/licenses/agpl-3.0)
  [![Status](https://img.shields.io/badge/Status-Active%20Development-orange)](https://github.com/abbaye/WpfHexEditorIDE/commits/master)
  [![Roadmap](https://img.shields.io/badge/Roadmap-ROADMAP.md-brightgreen)](ROADMAP.md)

  <br/>

  > 🚧 **Active Development** — New features, editors and panels are being added regularly. The IDE application is under active construction. Contributions and feedback welcome!

  <br/>

  <a href="Images/App-Editors-Welcome.png"><img src="Images/App-Editors-Welcome.png" alt="WPF HexEditor IDE" width="900"/></a>
  <br/>
  <sub><i>WpfHexEditor — Full IDE with VS-style docking, project system, and multiple editors</i></sub>

  <p>
    <a href="#-the-ide-application"><b>The IDE</b></a> •
    <a href="#-editors"><b>Editors</b></a> •
    <a href="#-controls--libraries"><b>Controls</b></a> •
    <a href="#-analysis--ide-panels"><b>Panels</b></a> •
    <a href="#-quick-start"><b>Quick Start</b></a> •
    <a href="#-documentation"><b>Docs</b></a> •
    <a href="CHANGELOG.md"><b>Changelog</b></a>
  </p>
</div>

---

## 🖥️ The IDE Application

**WpfHexEditor** is a full-featured binary analysis IDE for Windows, built entirely with WPF and .NET. It goes far beyond a simple hex editor — think Visual Studio for binary files.

<table>
<tr>
<td width="50%">

### 🏗️ Project System
- **Solution & Project** management (`.whsln` / `.whproj`)
- **Visual Studio `.sln` / `.csproj` / `.vbproj`** — open and build VS solutions directly (#101–103 ✅ in progress)
- **MSBuild integration** — build, rebuild, clean with output routed to the Build channel
- **Virtual & physical folders** (like VS Solution Explorer)
- **Format versioning** with auto-migration
- **Per-file state** persistence (bookmarks, scroll, encoding)

</td>
<td width="50%">

### 🪟 VS-Style Docking *(🔧 100% in-house engine)*
- **Float, dock, auto-hide** any panel
- **Colored tabs** with per-document customization
- **18 built-in themes** (Dark, Light, VS2022Dark, DarkGlass, Minimal, Office, Cyberpunk, VisualStudio...)
- **Tab placement** left / right / bottom

</td>
</tr>
<tr>
<td width="50%">

### 📋 IDE Infrastructure
- **IDocumentEditor** plugin contract — every editor is pluggable
- **Shared `UndoEngine`** — coalescing (500 ms), transactions, save-point tracking; `Ctrl+Z/Y/Shift+Z` across all editors; **HexEditor block undo** — paste/cut/delete undo as a single step via `UndoGroup` composite + `BeginUndoTransaction` API; VS-style history dropdown
- **Rectangular selection** (Alt+Click) + **drag-to-move** — CodeEditor & TextEditor
- **VS2022-style status bar** (edit mode, bytes/line, caret offset, LSP server state)
- **Output panel** + **Error/Diagnostics panel** + **Dockable Search Panel** (`Ctrl+Shift+F`)
- **VS2026-style Options** — document tab, auto-save, live theme preview
- **Integrated Terminal** (`Ctrl+`` `) — 31 built-in commands, panel/plugin/file management
- **Command Palette** (`Ctrl+Shift+P`) — 9 modes: commands, LSP symbols, go-to-line, files, content grep, and more
- **Command System** — `CommandRegistry` (~45 commands), `KeyBindingService`, `KeyboardShortcutsPage`, TitleBar launcher
- **Plugin System** — `WpfHexEditor.SDK` open API, `.whxplugin` packages, Plugin Manager, **IDE EventBus** (39 types), **Capability Registry**, **Extension Points**, **Dependency Graph**, plugin signing
- **NuGet Solution Manager** — Browse/Installed/Consolidate/Updates across all projects
- **DI Infrastructure** — `AppServiceCollection` (`Microsoft.Extensions.DependencyInjection`)

</td>
<td width="50%">

### 🔍 Binary Intelligence
- **400+ file format** auto-detection with **format-aware editor routing**
- **Parsed Fields Panel** with structure overlay
- **Data Inspector** — 40+ type interpretations
- **Assembly Explorer** — .NET PE inspection, types, methods, fields; C# decompilation to Code Editor tab; Extract to Project; Collapse/Close All; **Ctrl+Click external symbol decompilation** (AppDomain + runtime + NuGet resolution) *(#104-106 — in progress)*
- **HexEditor control** — 19 languages with instant switching *(IDE UI is English only — full localization engine coming soon)*

</td>
</tr>
</table>

---

## 📝 Editors

WpfHexEditor uses a **plugin architecture** (`IDocumentEditor`) — every editor is a standalone, reusable component hosted in the docking system.

| Editor | Status | Progress | Description |
|--------|--------|----------|-------------|
| **[Hex Editor](Sources/WpfHexEditor.HexEditor/README.md)** | ✅ Active | ~75% | Binary editing — insert/overwrite, 400+ format detection, search, bookmarks, TBL, status bar contributor |
| **[TBL Editor](Sources/WpfHexEditor.Editor.TblEditor/README.md)** | ✅ Active | ~80% | Character table editor for custom encodings and ROM hacking |
| **[Code Editor](Sources/WpfHexEditor.Editor.CodeEditor/README.md)** | ✅ Active | ~85% | Multi-language code editor — VS-like navigation bar, 55+ language definitions (incl. **F# + VB.NET**), URL hover/click, find/replace, split view; **Ctrl+Click** cross-file nav + external symbol decompilation; Alt+Click rect selection; drag-to-move; shared `UndoEngine`; `#region` colorization; data-driven folding (4 strategies); **word wrap**; **multi-caret** (`Ctrl+Alt+Click`, `Ctrl+D`); gutter diagnostics + scrollbar markers; **auto-close brackets/quotes** + skip-over + wrap-selection; **end-of-block hover hint** (`}` / `#endregion` / `</Tag>`); **word highlight** (VS Code style); **sticky scroll** with line numbers (#160); **Find All References** (`Shift+F12`) + dockable panel + `F8` nav; **LSP** breadcrumb bar, inlay hints, code lens, semantic tokens, code actions (`Ctrl+.`), rename (`F2`), signature help, workspace symbols |
| **[XAML Designer](Sources/WpfHexEditor.Editor.XamlDesigner/README.md)** | ✅ Active | ~70% | Full split-pane XAML designer — live WPF rendering canvas with **bidirectional sync** (canvas↔code, ~95%); move/resize/rotate handles; property inspector (F4); multi-select + alignment guides; snap grid; `#region` colorization; error card overlay; **4 split layouts** (`Ctrl+Shift+L`); zoom/pan; VS-like `Ctrl+Z/Y` undo/redo with `DesignHistoryPanel`; Toolbox, Resource Browser, Design Data and Animation panels; right-click context menu; `Ctrl+1/2/3` view modes |
| **[Text Editor](Sources/WpfHexEditor.Editor.TextEditor/README.md)** | ✅ Active | ~50% | Text editing with 26 embedded language definitions, auto-detection by extension, encoding support |
| **[Script Editor](Sources/WpfHexEditor.Editor.ScriptEditor/README.md)** | 🔧 Active | ~10% | `.hxscript` editor with syntax highlighting, run-in-terminal integration, `HxScriptEngine` backend (#177) |
| **[Image Viewer](Sources/WpfHexEditor.Editor.ImageViewer/README.md)** | 🔧 Active | ~50% | Binary image viewer — zoom/pan, transform pipeline (rotate/flip/crop/resize), `FileShare.ReadWrite` for concurrent open |
| **[Tile Editor](Sources/WpfHexEditor.Editor.TileEditor/README.md)** | 🔧 Stub | ~5% | Tile-based graphic editor for ROM/binary assets — palette, zoom, pixel grid (#175) |
| **[Structure Editor](Sources/WpfHexEditor.Editor.StructureEditor/README.md)** | 🔧 Active | ~40% | `.whfmt` binary template editor — block DataGrid, type/offset/length editing, live save |
| **[Entropy Viewer](Sources/WpfHexEditor.Editor.EntropyViewer/README.md)** | 🔧 Active | ~45% | Visual entropy graph of binary sections — detect encryption, compression, and packed regions |
| **[Diff / Changeset Viewer](Sources/WpfHexEditor.Editor.DiffViewer/README.md)** | 🔧 Active | ~70% | Side-by-side binary comparison and changeset replay |
| **[Audio Viewer](Sources/WpfHexEditor.Editor.AudioViewer/README.md)** | 🔧 Stub | ~5% | Audio binary viewer — waveform display (#174) |
| **[Disassembly Viewer](Sources/WpfHexEditor.Editor.DisassemblyViewer/README.md)** | 🔧 Active | ~30% | x86/x64/ARM binary disassembler — decompiler integration stub (#178) |
| **[Decompiled Source Viewer](Sources/WpfHexEditor.Core.Decompiler/README.md)** | 🔜 Planned | ~0% | C# skeleton + full IL view via ILSpy backend; "Go to Metadata Token" navigation (#106) |
| **Memory Snapshot Viewer** | 🔜 Planned | ~0% | Load Windows mini-dump `.dmp` / Linux core-dump; display memory regions, thread stacks, modules (#117) |
| **PCAP / Network Capture Viewer** | 🔜 Planned | ~0% | Load `.pcap` / `.pcapng`; packet list, layer breakdown (Ethernet/IP/TCP/UDP/TLS), raw payload (#136) |

> **Implementing a new editor?** See [IDocumentEditor contract](Sources/WpfHexEditor.Editor.Core/README.md) and register via `EditorRegistry`.

---

## 🧩 Standalone Controls & Libraries

All controls are **independently reusable** — no IDE required. Drop any of them into your own WPF application with a simple project reference.

### UI Controls

| Control | Frameworks | Progress | Description |
|---------|-----------|----------|-------------|
| **[HexEditor](Sources/WpfHexEditor.HexEditor/README.md)** | net8.0-windows | ~80% | Full-featured hex editor UserControl — MVVM, 16 services, insert/overwrite, search, bookmarks, TBL, 400+ format detection |
| **[HexBox](Sources/WpfHexEditor.HexBox/README.md)** | net8.0-windows | ~80% | Lightweight hex input field — zero external dependencies, MVVM-ready |
| **[ColorPicker](Sources/WpfHexEditor.ColorPicker/README.md)** | net8.0-windows | ~95% | Compact color picker UserControl with RGB/HSV/hex input |
| **[BarChart](Sources/WpfHexEditor.BarChart/README.md)** | net48 \| net8.0-windows | ~85% | Standalone byte-frequency bar chart — visualizes distribution of all 256 byte values (0x00–0xFF) in a binary file |
| **[Shell](Sources/WpfHexEditor.Shell/README.md)** | net8.0-windows | ~65% | **Custom-built** VS-style docking engine — float, dock, auto-hide, colored tabs, 8 themes — 100% in-house, zero third-party dependency *(formerly `WpfHexEditor.Docking.Wpf`)* |

### Libraries & Infrastructure

| Library | Frameworks | Description |
|---------|-----------|-------------|
| **[Core](Sources/WpfHexEditor.Core/README.md)** | net8.0-windows | ByteProvider, 16 services, data layer — the engine powering HexEditor |
| **[Editor.Core](Sources/WpfHexEditor.Editor.Core/README.md)** | net8.0-windows | `IDocumentEditor` plugin contract, editor registry, changeset system, shared interfaces |
| **[BinaryAnalysis](Sources/WpfHexEditor.BinaryAnalysis/README.md)** | net8.0 | 400+ format detection engine, binary templates, DataInspector service |
| **[Definitions](Sources/WpfHexEditor.Definitions/README.md)** | net8.0-windows | Embedded format catalog (400+ file signatures) and syntax definitions shared across editors and plugins |
| **[Events](Sources/WpfHexEditor.Events/README.md)** | net8.0 | IDE-wide typed event bus — `IIDEEventBus`, 10 built-in event types, weak-reference subscribers, rolling event log, IPC bridge for sandbox plugins |
| **[SDK](Sources/WpfHexEditor.SDK/README.md)** | net8.0-windows | Public plugin API — `IWpfHexEditorPlugin`, `IIDEHostContext`, `IUIRegistry`, 15+ service contracts incl. `IIDEEventBus`, `IPluginCapabilityRegistry`, `IExtensionRegistry`, `ITerminalService` |
| **[PluginHost](Sources/WpfHexEditor.PluginHost/README.md)** | net8.0-windows | Runtime plugin infrastructure — discovery, load, watchdog, `PluginManagerControl`, `PermissionService` |
| **[PluginSandbox](Sources/WpfHexEditor.PluginSandbox/README.md)** | net8.0-windows | Out-of-process plugin execution host — HWND embedding, IPC menu/toolbar/event bridges, Job Object resource isolation, auto-isolation decision engine (#81) |
| **[Docking.Core](Sources/WpfHexEditor.Docking.Core/README.md)** | net8.0-windows | Abstract platform-agnostic docking contracts — `DockEngine`, layout model, `DockItemState` |
| **[Core.Terminal](Sources/WpfHexEditor.Core.Terminal/README.md)** | net8.0-windows | Command engine — 31+ built-in commands, `HxScriptEngine`, `CommandHistory`, `WriteTable` output helper |
| **[Terminal](Sources/WpfHexEditor.Terminal/README.md)** | net8.0-windows | WPF terminal panel layer — `TerminalPanel`, `TerminalPanelViewModel`, multi-tab shell session management |
| **[Core.AssemblyAnalysis](Sources/WpfHexEditor.Core.AssemblyAnalysis/README.md)** | net8.0 | BCL-only .NET PE analysis pipeline — PEReader + assembly model, foundation for Assembly Explorer plugin |
| **[Core.Decompiler](Sources/WpfHexEditor.Core.Decompiler/README.md)** | net8.0-windows | `IDecompiler` contract + stub backend for ILSpy/dnSpy integration (#106) |
| **[ProjectSystem](Sources/WpfHexEditor.ProjectSystem/README.md)** | net8.0-windows | `.whsln` / `.whproj` workspace and project model — serialization, project-to-project references, dialogs |
| **[Options](Sources/WpfHexEditor.Options/README.md)** | net8.0-windows | `AppSettingsService`, `OptionsEditorControl` — IDE settings persistence and options page infrastructure |

---

## 🗂️ IDE Panels

Panels connect to the active document automatically via the docking system.

| Panel | Progress | Description |
|-------|----------|-------------|
| **[Parsed Fields Panel](Sources/Plugins/WpfHexEditor.Plugins.ParsedFields/README.md)** | ~75% | 400+ format detection — parsed field list with type overlay and inline editing |
| **[Data Inspector](Sources/Plugins/WpfHexEditor.Plugins.DataInspector/README.md)** | ~65% | 40+ byte interpretations at caret position (int, float, GUID, date, color, …); plugin with settings page |
| **[Structure Overlay](Sources/Plugins/WpfHexEditor.Plugins.StructureOverlay/README.md)** | ~55% | Visual field highlighting superimposed on the hex grid |
| **[Solution Explorer](Sources/WpfHexEditor.Panels.IDE/README.md)** | ~85% | Project tree with virtual & physical folders, Show All Files, D&D from Windows Explorer, expand-state persistence, delete from disk; **lazy source outline** — expand any `.cs`/`.xaml` file to browse types and members with line-number navigation |
| **[Properties Panel](Sources/WpfHexEditor.Panels.IDE/README.md)** | ~60% | Context-aware properties for the active document (F4) — auto-refresh on cursor idle (400 ms debounce), categorized groups, sort/copy/refresh toolbar |
| **[Error Panel](Sources/WpfHexEditor.Panels.IDE/README.md)** | ~70% | Diagnostics and validation errors from any `IDiagnosticSource` editor |
| **[Output Panel](Sources/WpfHexEditor.Panels.IDE/README.md)** | ~75% | Session log, file operation messages; **Build channel** with severity coloring (info/warn/error/success) auto-focused when build starts; source selector, clear, word wrap, copy, auto-scroll |
| **[Terminal Panel](Sources/WpfHexEditor.Terminal/README.md)** | ~70% | Integrated command terminal — 31+ commands, colored output, history, `TerminalMode` (Interactive/Script/ReadOnly), session export, plugin API via `ITerminalService` |
| **[Plugin Manager](Sources/WpfHexEditor.PluginHost/README.md)** | ~65% | Browse, enable/disable, uninstall plugins; settings integration via `IPluginWithOptions`; themed toolbar with RelayCommand |
| **[Plugin Monitoring](Sources/WpfHexEditor.Panels.IDE/README.md)** | ~60% | Real-time CPU% + memory charts per plugin; pure WPF `Canvas` + `Polyline` (no charting lib); rolling history, `PerformanceCounter` + GC polling at 1 s interval |
| **[Options](Sources/WpfHexEditor.Options/README.md)** | ~75% | VS2026-style settings document tab — 9 pages: theme, display, editing, behavior, status bar, plugins, auto-save |
| **Quick Search Bar** | ~55% | Inline Ctrl+F overlay (VSCode-style) — find next/prev, regex toggle, jump to Advanced |
| **Advanced Search** | ~45% | Full-featured search dialog — 5 modes: Hex, Text, Regex, TBL, Wildcard |
| **[File Diff](Sources/WpfHexEditor.Editor.DiffViewer/README.md)** | ~30% | Side-by-side binary comparison with diff navigation (F7/F8) |
| **[Assembly Explorer](Sources/Plugins/WpfHexEditor.Plugins.AssemblyExplorer/README.md)** | ~35% | .NET PE tree — namespaces, types, methods, fields, events, resources; C# decompilation → Code Editor tab with syntax highlighting; Extract to Project; Collapse All / Close All (#104–106 in progress) |
| **[Archive Structure](Sources/Plugins/WpfHexEditor.Plugins.ArchiveStructure/README.md)** | ~40% | ZIP/archive tree view — browse entries, extract, inspect compressed file layouts inside the hex view |
| **[File Comparison](Sources/Plugins/WpfHexEditor.Plugins.FileComparison/README.md)** | ~45% | Binary file comparison panel — byte-level diff between two files with synchronized scrolling |
| **[File Statistics](Sources/Plugins/WpfHexEditor.Plugins.FileStatistics/README.md)** | ~55% | Byte-frequency charts, entropy score, size breakdown, and format distribution for the active binary |
| **[Format Info](Sources/Plugins/WpfHexEditor.Plugins.FormatInfo/README.md)** | ~60% | Detailed format metadata panel — detected format, MIME type, magic bytes, section list, and known offsets |
| **[Pattern Analysis](Sources/Plugins/WpfHexEditor.Plugins.PatternAnalysis/README.md)** | ~50% | Pattern detection in binary data — highlight known byte sequences, data structures, and anomalies |
| **[Custom Parser Template](Sources/Plugins/WpfHexEditor.Plugins.CustomParserTemplate/README.md)** | ~40% | Template-driven binary parser — define structures in a `.bt`-style schema, render parsed fields live |

---

## 📸 Screenshots

<div align="center">
  <b>🖥️ IDE Overview</b><br/>
  <sub>VS-style docking with Solution Explorer, HexEditor and ParsedFieldsPanel</sub><br/><br/>
  <a href="Images/App-IDE-Overview.png"><img src="Images/App-IDE-Overview.png" alt="IDE Overview" width="900"/></a>
</div>

<details>
<summary>🔬 <b>Parsed Fields Panel</b> — 400+ format auto-detection with structured field analysis</summary>
<br/>
<div align="center">
  <a href="Images/App-ParsedFields.png"><img src="Images/App-ParsedFields.png" alt="Parsed Fields" width="900"/></a>
</div>
</details>

<details>
<summary>📝 <b>Multi-Editor Tabs</b> — HexEditor, TBL, JSON and Text editors side by side</summary>
<br/>
<div align="center">
  <a href="Images/App-Editors.png"><img src="Images/App-Editors.png" alt="Multiple Editors" width="900"/></a>
</div>
</details>

<details>
<summary>🗂️ <b>Solution Explorer</b> — VS-style project tree with virtual and physical folders</summary>
<br/>
<div align="center">
  <a href="Images/App-SolutionExplorer.png"><img src="Images/App-SolutionExplorer.png" alt="Solution Explorer" width="900"/></a>
</div>
</details>

<details>
<summary>📋 <b>TBL Editor</b> — Custom character table editor for ROM hacking and encodings</summary>
<br/>
<div align="center">
  <a href="Images/App-TBLEditor.png"><img src="Images/App-TBLEditor.png" alt="TBL Editor" width="900"/></a>
</div>
</details>

<details>
<summary>🔴 <b>Error Panel</b> — Diagnostics and validation errors from active editors</summary>
<br/>
<div align="center">
  <a href="Images/App-ErrorList.png"><img src="Images/App-ErrorList.png" alt="Error Panel" width="900"/></a>
</div>
</details>

<details>
<summary>📤 <b>Output Panel</b> — Session log, messages and file operation feedback</summary>
<br/>
<div align="center">
  <a href="Images/App-Output.png"><img src="Images/App-Output.png" alt="Output Panel" width="900"/></a>
</div>
</details>

<details>
<summary>☀️ <b>Light Theme</b> — 8 built-in themes: Dark, Light, VS2022Dark, DarkGlass, and more</summary>
<br/>
<div align="center">
  <a href="Images/App-Theme-Light.png"><img src="Images/App-Theme-Light.png" alt="Light Theme" width="900"/></a>
</div>
</details>

<details>
<summary>🎮 <b>TBL Format Explained</b> — Custom character table format for game ROM editing</summary>
<br/>
<div align="center">
  <a href="Images/TBLExplain.png"><img src="Images/TBLExplain.png" alt="TBL Explained" width="600"/></a>
</div>
</details>

---

## ⚡ Quick Start

### Run the IDE

```bash
git clone https://github.com/abbaye/WpfHexEditorIDE.git
```

Open `WpfHexEditorControl.sln`, set **WpfHexEditor.App** as startup project, and run.

> **IDE compatibility:** Actively developed on **Visual Studio 2026**. Fully compatible with **Visual Studio 2022** (v17.8+). JetBrains Rider is also supported.

### Embed the HexEditor in your WPF app

**Reference the projects:**
```xml
<ProjectReference Include="..\WpfHexEditor.Core\WpfHexEditor.Core.csproj" />
<ProjectReference Include="..\WpfHexEditor.HexEditor\WpfHexEditor.HexEditor.csproj" />
```

**Add to your XAML:**
```xml
<Window xmlns:hex="clr-namespace:WpfHexEditor.HexEditor;assembly=WpfHexEditor.HexEditor">
  <hex:HexEditor FileName="data.bin" />
</Window>
```

> **NuGet:** A legacy package (`WPFHexaEditor`) is still available on NuGet but is no longer maintained. NuGet packaging is planned (#109).

> **JetBrains Rider:** Fully compatible — open the solution and press F5. SmartComplete (LSP) works out of the box.

**[Complete Tutorial →](GETTING_STARTED.md)**

---

## 🎯 Why Choose WPF HexEditor?

<table>
<tr>
<td width="33%">

### ⚡ Built for Performance
- **DrawingContext** rendering — handles GB+ files without freezing
- **LRU + SIMD + parallel** search engine
- **Span\<T\> + pooling** — minimal allocations
- **Async** file I/O with progress throughout

</td>
<td width="33%">

### 🏗️ Clean Architecture
- **MVVM** with 16+ specialized services
- **Partial classes** organized by feature
- **Plugin editors** via `IDocumentEditor`
- **100% testable** — zero UI in services
- **Open SDK** — extend anything

</td>
<td width="33%">

### 🖥️ Full IDE Experience
- **Project system** (`.whsln` / `.whproj`) — VS-like solution explorer
- **VS-style docking** — 100% in-house, no third-party lib
- **18 built-in themes** — Dark, Light, Dracula, Nord, Tokyo Night, Catppuccin and more
- **Plugin system** — open SDK + `.whxplugin` packages + sandbox signing
- **Integrated terminal** — 31 built-in commands + macro recording
- **Command Palette** (`Ctrl+Shift+P`) — 9 modes incl. file search + content grep
- **LSP Engine** — full JSON-RPC client, 10 providers, breadcrumb bar, inlay hints
- **MSBuild / `.sln`** — open and build Visual Studio solutions + incremental dirty tracking

</td>
</tr>
<tr>
<td width="33%">

### 🔍 Binary & Code Intelligence
- **400+ formats** auto-detected with format-aware routing
- **Parsed Fields** with structure overlay
- **Data Inspector** — 40+ type interpretations
- **Assembly Explorer** — .NET PE tree + ILSpy decompilation *(#104-106)*

</td>
<td width="33%">

### 🌍 Multilingual Control
- **HexEditor control** — 19 languages, instant runtime switching
- **IDE UI** — English; full localization engine coming soon *(#100)*
- Extensible — add new languages without recompiling

</td>
<td width="33%">

### ✅ Actively Maintained
- **AGPL v3.0** — fully open source
- **Active development** — features added regularly
- **Unlimited Undo/Redo** across all editors
- **Insert Mode**, save reliability, async ops — production-grade
- Contributions welcome — clean, documented codebase

</td>
</tr>
</table>

---

## 🗺️ Roadmap

> Full details in **[ROADMAP.md](ROADMAP.md)** · Issue tracking in [CHANGELOG.md — What's Next](CHANGELOG.md#whats-next).

| Feature | Status | Feature # |
|---------|--------|-----------|
| **Sticky Scroll** — scope header pinned at top; line numbers in gutter; 6 options; syntax-highlighted; allocation-free `OnRender` | ✅ Done v0.6.3.7 | #160 |
| **Find All References** — `Shift+F12`; `WorkspaceFileCache`; dockable `FindReferencesPanel`; scope filter; `F8`/`Shift+F8` nav; inline hints | ✅ Done v0.6.3.7 | #157 |
| **Auto-close Brackets & Quotes** — auto-insert, skip-over, wrap-selection; 4 options; language-scoped | ✅ Done v0.6.3.7 | #163 |
| **Quick File Open** — `Ctrl+P` → Command Palette `#` file mode | ✅ Done v0.6.3.7 | #179 |
| **Integrated Debugger UI** — Debug menu, toolbar pod, execution line highlight, gutter hover ghost, debug status bar, `BreakpointInfoPopup`, non-executable line validation | ✅ Done v0.6.3.7 | #44, #90 |
| **Workspace System** — `.whidews` ZIP+JSON format, 5 async handlers, layout/solution/file restore, options page | ✅ Done v0.6.3.7 | #77 |
| **F# / VB.NET Language Support** — `.whfmt` definitions, LSP servers, project templates, ILSpy decompilation | ✅ Done v0.6.3.7 | — |
| **Compare Files Overkill** — `DiffEngine` + Myers/Binary/Semantic algorithms, `GitDiffService`, `DiffExportService`, `SolutionExplorerCompareContributor`, 14 `DF_*` tokens | ✅ Done v0.6.3.7 | — |
| **DiffHub Document Viewer** — full-tab `DiffViewerDocument`, word-level diff, overview ruler, `Alt+Up/Down` nav | ✅ Done v0.6.3.7 | — |
| **End-of-Block Hover Hint** — hover `}` / `#endregion` / `</Tag>` → VS-style popup with opening scope; `ET_*` tokens | ✅ Done v0.6.3.7 | — |
| **Build Progress Bar** — inline `ProgressBar` in build status bar item | ✅ Done v0.6.3.7 | — |
| **InlineHints Pre-warming** — solution open triggers background `WorkspaceFileCache` rebuild + line cache warm-up | ✅ Done v0.6.3.7 | — |
| **Perf: Sticky Scroll** — infinite render loop eliminated (100% CPU / 567 MB); O(N) scan; allocation-free `OnRender`; batch yields; search debounce | ✅ Done v0.6.3.7 | — |
| **IDE EventBus** — 39 typed events, IPC bridge, Phase 1–3 coverage | ✅ Done v0.6.3.6 | #80 |
| **Command Palette** — `Ctrl+Shift+P`, 9 modes (commands/symbols/line/files/grep/…), frequency boost, content grep | ✅ Done v0.6.3.6 | #133 |
| **Command System Central** — `CommandRegistry`, `KeyBindingService`, `KeyboardShortcutsPage`, TitleBar launcher | ✅ Done v0.6.3.6 | #78 |
| **LSP Engine** — full JSON-RPC client; 10 providers; completion, sig-help, code actions, rename, inlay hints, code lens, semantic tokens | ✅ Done v0.6.3.6 | #85–86 |
| **Code Editor — Phase 2 remaining** — peek definition, code formatting, minimap, bracket pair colorization, smart indentation, column rulers, gutter change markers, expand/collapse all folds, color swatch preview | 🔧 In Progress ~85% | #158–168 |
| **Editors Phase 2** — TextEditor multi-caret/LSP, ImageViewer histogram/batch-export, DiffViewer 3-way merge, StructureEditor live sync, EntropyViewer export, AudioViewer full impl, TileEditor full impl, JsonEditor full impl, ScriptEditor debugger/REPL, DisassemblyViewer x86/ARM decoding | 🔜 Planned | #169–178 |
| **Plugins Phase 2** — AssemblyExplorer IlSpy backend, ArchiveStructure in-place edit, FileComparison diff algorithm + patch export, DiagnosticTools flame graph + leak heuristics | 🔜 Planned | #186–189 |
| **LSP Phase 3 + Roslyn** — call hierarchy, linked editing, inline value hints, LSP 3.18 pull-diagnostics, Roslyn-based C#/VB.NET/F# parser | 🔜 Planned | #190–191 |
| **Document Model Phase 1** — `IDocumentBuffer`, `LspBufferBridge`, `IBufferAwareEditor` for all editors | ✅ Done v0.6.3.6 | #107 |
| **Diagnostic Tools Plugin** — EventPipe CPU/mem graphs, heap snapshot, 4-tab panel | ✅ Done v0.6.3.6 | — |
| **Dockable Search Panel** — `Ctrl+Shift+F`; hex/text/regex/TBL modes | ✅ Done v0.6.3.6 | — |
| **Incremental Build** — dirty tracking FSW, `BuildDirtyAsync()`, `Ctrl+Alt+F7`, orange dot in Solution Explorer | ✅ Done v0.6.3.6 | #103 |
| **DI Infrastructure** — `AppServiceCollection` (`Microsoft.Extensions.DependencyInjection`) | ✅ Done v0.6.3.6 | #36 |
| **VS `.sln` / `.csproj` support + MSBuild build** | ✅ Done v0.5.0 | #101–103 |
| **Assembly Explorer** — .NET PE tree + C# decompilation → Code Editor tab | 🔧 In Progress ~75% | #104–106 |
| **Document Model Phase 2** — multi-editor collaboration, undo/redo unification, hex editor buffer | 🔧 In Progress ~40% | #107 |
| **Integrated Terminal** — full multi-shell + macro | 🔧 In Progress ~70% | #92 |
| **Code Intelligence (SmartComplete / Snippets / AI suggestions)** | 🔜 Planned | #86–89 |
| **Git Integration** | 🔜 Planned | #91 |
| **Plugin Marketplace & Auto-Update** | 🔜 Planned | #41–43 |
| **IDE Localization Engine** — full IDE UI (HexEditor control already 19 languages) | 🔜 Planned | #100 |
| **Official Website** — landing page, docs, downloads, plugin registry | 🔜 Planned | #108 |
| **Installable Package** — MSI / MSIX / WinGet, auto-update, no SDK required | 🔜 Planned | #109 |
| **In-IDE Plugin Development** — write, build, hot-reload and live-test SDK plugins without leaving the IDE | 🔜 Planned | #138 |

---

## 📚 Documentation

### User Guides

| Document | Description |
|----------|-------------|
| **[GETTING_STARTED.md](GETTING_STARTED.md)** | Run the IDE or embed the control — step by step |
| **[FEATURES.md](FEATURES.md)** | Complete feature list — IDE, editors, panels, controls |
| **[CHANGELOG.md](CHANGELOG.md)** | Version history and what's new |
| **[MIGRATION.md](docs/migration/MIGRATION.md)** | Legacy V1 → migration guide (zero code changes required) |

### Developer Reference

| Document | Description |
|----------|-------------|
| **[Architecture Overview](docs/architecture/Overview.md)** | System architecture, services, MVVM patterns |
| **[Core Systems](docs/architecture/core-systems/)** | ByteProvider, position mapping, undo/redo, rendering |
| **[Data Flow](docs/architecture/data-flow/)** | File, edit, search and save operation sequences |
| **[API Reference](docs/api-reference/)** | Full API documentation with code examples |
| **[Wiki — Getting Started](https://github.com/abbaye/WpfHexEditorIDE/wiki/Getting-Started)** | Build, run, embed the control — step by step |

---

## 🔧 Supported Frameworks

| Framework | Version | Notes |
|-----------|---------|-------|
| **.NET** | 8.0-windows | Span\<T\>, SIMD, PGO — full performance unlocked |

> **.NET Framework 4.8 support has been dropped.** The project targets .NET 8.0+ exclusively to take full advantage of modern runtime performance, Span\<T\>, SIMD vectorization, and Profile-Guided Optimization (PGO). If you need .NET Framework support, use the legacy V1 NuGet package.

---

## 🌍 Multilingual

> ⚠️ **HexEditor control only** — The embedded `HexEditorControl` supports 19 languages with instant runtime switching (no restart required). The **IDE UI is currently English only** — a full localization engine for the IDE is planned ([#100](ROADMAP.md)).

**HexEditor control languages:**
English · French · Spanish · German · Italian · Japanese · Korean · Dutch · Polish · Portuguese · Russian · Swedish · Turkish · Chinese · Arabic · Hindi · and more

> English and French are the most complete. Contributions welcome!

---

## ⭐ Support This Project

WPF HexEditor is **100% free and open source** (GNU AGPL v3.0) — free to use, modify and distribute under the terms of the AGPL.

This project is developed in **free time** by passionate contributors. If you find it useful:

- ⭐ **Star this repository** — helps others discover the project
- 🍴 **Fork and contribute** — pull requests are always welcome
- 💬 **Share feedback** — report bugs or suggest features via [GitHub Issues](https://github.com/abbaye/WpfHexEditorIDE/issues)

**Every star motivates us to keep building! 🙏**

---

## 🤝 Contributing

We welcome contributions! The codebase is well-organized and documented:

- **[Architecture Guide](docs/architecture/Overview.md)** — service-based design and patterns
- **[CONTRIBUTING.md](CONTRIBUTING.md)** — branch conventions, standards, how to submit a PR
- **Partial class structure** — every feature in its own file, easy to navigate
- **[Sample App](Sources/WpfHexEditor.App/)** — real integration to learn from

---

## 📝 License

**GNU Affero General Public License v3.0** — free to use, modify and distribute. Any modified version distributed over a network must also be made available as open source. See [LICENSE](LICENSE) for details.

---

## 📧 Contact & Support

- 🐛 **Bug Reports**: [GitHub Issues](https://github.com/abbaye/WpfHexEditorIDE/issues)
- 💡 **Feature Requests**: [GitHub Discussions](https://github.com/abbaye/WpfHexEditorIDE/discussions)
- 📧 **Email**: derektremblay666@gmail.com

---

<div align="center">
  <br/>
  <p>
    <b>✨ WPF HexEditor ✨</b><br/>
    <sub>A powerful, actively developed hex editor IDE for .NET</sub>
  </p>
  <p>
    <sub>Created by Derek Tremblay (abbaye)<br/>
    Contributors: Claude Sonnet 4.6, Claude Opus 4.6<br/>
    HexEditor Legacy Contributors: ehsan69h, Janus Tida</sub>
  </p>
  <p>
    <sub>Coded with ❤️ for the community! 😊🤟 (with a touch of AI magic ✨)</sub>
  </p>
  <br/>

  **[🚀 Quick Start](#-quick-start)** •
  **[📖 Tutorial](GETTING_STARTED.md)** •
  **[📊 Features](FEATURES.md)** •
  **[📝 Changelog](CHANGELOG.md)** •
  **[⭐ Star Us](https://github.com/abbaye/WpfHexEditorIDE)**

  <br/>
</div>
