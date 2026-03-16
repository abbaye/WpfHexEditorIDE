<div align="center">
  <a href="Images/Logo2026.png"><img src="Images/Logo2026.png" width="600" height="250" /></a>
  <br/><br/>

  <h3>🖥️ A full-featured open-source IDE for .NET — Binary analysis, reverse engineering & build tooling</h3>

[![.NET](https://img.shields.io/badge/.NET-8.0--windows-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
  [![Platform](https://img.shields.io/badge/Platform-Windows%20WPF-0078D4?logo=windows)](https://github.com/abbaye/WpfHexEditorIDE)
  [![IDE Version](https://img.shields.io/badge/IDE-v0.3.0-6A0DAD?logo=visualstudiocode&logoColor=white)](https://github.com/abbaye/WpfHexEditorIDE/releases)
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
- **Visual Studio `.sln` / `.csproj` / `.vbproj`** support via MSBuild *(#101-103 — planned)*
- **Virtual & physical folders** (like VS Solution Explorer)
- **Format versioning** with auto-migration
- **Per-file state** persistence (bookmarks, scroll, encoding)

</td>
<td width="50%">

### 🪟 VS-Style Docking *(🔧 100% in-house engine)*
- **Float, dock, auto-hide** any panel
- **Colored tabs** with per-document customization
- **8 built-in themes** (Dark, Light, VS2022Dark, DarkGlass, Minimal, Office, Cyberpunk, VisualStudio)
- **Tab placement** left / right / bottom

</td>
</tr>
<tr>
<td width="50%">

### 📋 IDE Infrastructure
- **IDocumentEditor** plugin contract — every editor is pluggable
- **Undo/Redo/Copy/Cut/Paste** unified via menu bindings
- **VS2022-style status bar** (edit mode, bytes/line, caret offset)
- **Output panel** + **Error/Diagnostics panel** + **Quick Search** (inline + advanced)
- **VS2026-style Options** — document tab, auto-save, live theme preview
- **Integrated Terminal** (`Ctrl+`` `) — 31 built-in commands, panel/plugin/file management
- **Plugin System** — `WpfHexEditor.SDK` open API, `.whxplugin` packages, Plugin Manager

</td>
<td width="50%">

### 🔍 Binary Intelligence
- **400+ file format** auto-detection with **format-aware editor routing**
- **Parsed Fields Panel** with structure overlay
- **Data Inspector** — 40+ type interpretations
- **Assembly Explorer** — .NET PE inspection, types, methods, fields; ILSpy decompilation *(#104-106 — in progress)*
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
| **[TBL Editor](Sources/WpfHexEditor.Editor.TblEditor/README.md)** | ✅ Active | ~60% | Character table editor for custom encodings and ROM hacking |
| **[Code Editor](Sources/WpfHexEditor.Editor.CodeEditor/README.md)** | ✅ Active | ~55% | Multi-language code editor with syntax highlighting, find/replace, `IEditorPersistable`, split view |
| **[Text Editor](Sources/WpfHexEditor.Editor.TextEditor/README.md)** | ✅ Active | ~50% | Text editing with 26 embedded language definitions, auto-detection by extension, encoding support |
| **[Script Editor](Sources/WpfHexEditor.Editor.ScriptEditor/README.md)** | ✅ Active | ~45% | `.hxscript` editor with syntax highlighting, run-in-terminal integration, `HxScriptEngine` backend |
| **[Image Viewer](Sources/WpfHexEditor.Editor.ImageViewer/README.md)** | 🔧 Active | ~30% | Binary image viewer — zoom/pan, transform pipeline (rotate/flip/crop/resize), `FileShare.ReadWrite` for concurrent open |
| **[Tile Editor](Sources/WpfHexEditor.Editor.TileEditor/README.md)** | 🔧 Active | ~30% | Tile-based graphic editor for ROM/binary assets — palette, zoom, pixel grid |
| **[Structure Editor](Sources/WpfHexEditor.Editor.StructureEditor/README.md)** | 🔧 Active | ~30% | `.whfmt` binary template editor — block DataGrid, type/offset/length editing, live save |
| **[Entropy Viewer](Sources/WpfHexEditor.Editor.EntropyViewer/README.md)** | 🔧 Active | ~25% | Visual entropy graph of binary sections — detect encryption, compression, and packed regions |
| **[Diff / Changeset Viewer](Sources/WpfHexEditor.Editor.DiffViewer/README.md)** | 🔧 Active | ~35% | Side-by-side binary comparison and changeset replay |
| **[Audio Viewer](Sources/WpfHexEditor.Editor.AudioViewer/README.md)** | 🔧 Stub | ~5% | Audio binary viewer — waveform display (planned) |
| **[Disassembly Viewer](Sources/WpfHexEditor.Editor.DisassemblyViewer/README.md)** | 🔧 Stub | ~5% | x86/x64/ARM binary disassembler (planned) |
| **[Decompiled Source Viewer](Sources/WpfHexEditor.Decompiler.Core/README.md)** | 🔜 Planned | ~0% | C# skeleton + full IL view via ILSpy backend; "Go to Metadata Token" navigation (#106) |
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
| **[Docking.Wpf](Sources/WpfHexEditor.Docking.Wpf/README.md)** | net8.0-windows | ~65% | **Custom-built** VS-style docking engine — float, dock, auto-hide, colored tabs, 8 themes — 100% in-house, zero third-party dependency |

### Libraries & Infrastructure

| Library | Frameworks | Description |
|---------|-----------|-------------|
| **[Core](Sources/WpfHexEditor.Core/README.md)** | net8.0-windows | ByteProvider, 16 services, data layer — the engine powering HexEditor |
| **[Editor.Core](Sources/WpfHexEditor.Editor.Core/README.md)** | net8.0-windows | `IDocumentEditor` plugin contract, editor registry, changeset system, shared interfaces |
| **[BinaryAnalysis](Sources/WpfHexEditor.BinaryAnalysis/README.md)** | net8.0 | 400+ format detection engine, binary templates, DataInspector service |
| **[Definitions](Sources/WpfHexEditor.Definitions/README.md)** | net8.0-windows | Embedded format catalog (400+ file signatures) and syntax definitions shared across editors and plugins |
| **[SDK](Sources/WpfHexEditor.SDK/README.md)** | net8.0-windows | Public plugin API — `IWpfHexEditorPlugin`, `IIDEHostContext`, `IUIRegistry`, 11+ service contracts incl. `ITerminalService`, `PluginCapabilities.Terminal` |
| **[PluginHost](Sources/WpfHexEditor.PluginHost/README.md)** | net8.0-windows | Runtime plugin infrastructure — discovery, load, watchdog, `PluginManagerControl`, `PermissionService` |
| **[PluginSandbox](Sources/WpfHexEditor.PluginSandbox/README.md)** | net8.0-windows | Out-of-process plugin execution host — HWND embedding, IPC menu/toolbar/event bridges, Job Object resource isolation, auto-isolation decision engine (#81) |
| **[Docking.Core](Sources/WpfHexEditor.Docking.Core/README.md)** | net8.0-windows | Abstract platform-agnostic docking contracts — `DockEngine`, layout model, `DockItemState` |
| **[Core.Terminal](Sources/WpfHexEditor.Core.Terminal/README.md)** | net8.0-windows | Command engine — 31+ built-in commands, `HxScriptEngine`, `CommandHistory`, `WriteTable` output helper |
| **[Terminal](Sources/WpfHexEditor.Terminal/README.md)** | net8.0-windows | WPF terminal panel layer — `TerminalPanel`, `TerminalPanelViewModel`, multi-tab shell session management |
| **[Core.AssemblyAnalysis](Sources/WpfHexEditor.Core.AssemblyAnalysis/README.md)** | net8.0 | BCL-only .NET PE analysis pipeline — PEReader + assembly model, foundation for Assembly Explorer plugin |
| **[Decompiler.Core](Sources/WpfHexEditor.Decompiler.Core/README.md)** | net8.0-windows | `IDecompiler` contract + stub backend for ILSpy/dnSpy integration (#106) |
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
| **[Solution Explorer](Sources/WpfHexEditor.Panels.IDE/README.md)** | ~75% | Project tree with virtual & physical folders, Show All Files, D&D from Windows Explorer, expand-state persistence, delete from disk |
| **[Properties Panel](Sources/WpfHexEditor.Panels.IDE/README.md)** | ~60% | Context-aware properties for the active document (F4) — auto-refresh on cursor idle (400 ms debounce), categorized groups, sort/copy/refresh toolbar |
| **[Error Panel](Sources/WpfHexEditor.Panels.IDE/README.md)** | ~70% | Diagnostics and validation errors from any `IDiagnosticSource` editor |
| **[Output Panel](Sources/WpfHexEditor.Panels.IDE/README.md)** | ~65% | Session log, file operation messages and build feedback |
| **[Terminal Panel](Sources/WpfHexEditor.Terminal/README.md)** | ~70% | Integrated command terminal — 31+ commands, colored output, history, `TerminalMode` (Interactive/Script/ReadOnly), session export, plugin API via `ITerminalService` |
| **[Plugin Manager](Sources/WpfHexEditor.PluginHost/README.md)** | ~65% | Browse, enable/disable, uninstall plugins; settings integration via `IPluginWithOptions`; themed toolbar with RelayCommand |
| **[Plugin Monitoring](Sources/WpfHexEditor.Panels.IDE/README.md)** | ~60% | Real-time CPU% + memory charts per plugin; pure WPF `Canvas` + `Polyline` (no charting lib); rolling history, `PerformanceCounter` + GC polling at 1 s interval |
| **[Options](Sources/WpfHexEditor.Options/README.md)** | ~75% | VS2026-style settings document tab — 9 pages: theme, display, editing, behavior, status bar, plugins, auto-save |
| **Quick Search Bar** | ~55% | Inline Ctrl+F overlay (VSCode-style) — find next/prev, regex toggle, jump to Advanced |
| **Advanced Search** | ~45% | Full-featured search dialog — 5 modes: Hex, Text, Regex, TBL, Wildcard |
| **[File Diff](Sources/WpfHexEditor.Editor.DiffViewer/README.md)** | ~30% | Side-by-side binary comparison with diff navigation (F7/F8) |
| **[Assembly Explorer](Sources/Plugins/WpfHexEditor.Plugins.AssemblyExplorer/README.md)** | ~15% | .NET PE tree — namespaces, types, methods, fields, events, resources; ECMA-335 metadata resolution (Phase 1 done, #104–105) |
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

> **JetBrains Rider:** See the **[Rider Guide](docs/IDE/RIDER_GUIDE.md)** for IntelliSense tips.

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
- **8 built-in themes** — Dark, Light, Cyberpunk and more
- **Plugin system** — open SDK + `.whxplugin` packages
- **Integrated terminal** — 31 built-in commands + macro recording
- **MSBuild / `.sln`** support *(coming — #101-103)*

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
| **Document Model** — unified in-memory document representation | 🔧 In Progress ~10% | #107 |
| **Assembly Explorer — full tree + ECMA-335 metadata** | 🔧 In Progress ~15% | #104–105 |
| **Integrated Terminal** — full multi-shell + macro | 🔧 In Progress ~70% | #92 |
| **MSBuild & Visual Studio `.sln` / `.csproj` / `.vbproj` support** | 🔜 Planned | #101–103 |
| **.NET Decompilation via ILSpy** | 🔜 Planned | #106 |
| **Code Intelligence (LSP / IntelliSense / Snippets)** | 🔜 Planned | #85–89 |
| **Integrated Debugger** | 🔜 Planned | #44, #90 |
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
| **[Rider Guide](docs/IDE/RIDER_GUIDE.md)** | JetBrains Rider setup and tips |

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
