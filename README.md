<div align="center">
  <a href="Images/Logo2026.png"><img src="Images/Logo2026.png" width="600" height="250" /></a>
  <br/><br/>

  <h3>🖥️ A full-featured open-source IDE for .NET — Binary analysis, reverse engineering & build tooling</h3>

[![.NET](https://img.shields.io/badge/.NET-8.0--windows-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
  [![Platform](https://img.shields.io/badge/Platform-Windows%20WPF-0078D4?logo=windows)](https://github.com/abbaye/WpfHexEditorIDE)
  [![IDE Version](https://img.shields.io/badge/IDE-v0.2.5-6A0DAD?logo=visualstudiocode&logoColor=white)](https://github.com/abbaye/WpfHexEditorIDE/releases)
  [![License: AGPL v3](https://img.shields.io/badge/License-AGPL_v3-blue.svg)](https://www.gnu.org/licenses/agpl-3.0)
  [![Status](https://img.shields.io/badge/Status-Active%20Development-orange)](https://github.com/abbaye/WpfHexEditorIDE/commits/master)

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
- **19 languages** with instant switching *(partial — not all languages fully translated)*

</td>
</tr>
</table>

---

## 📝 Editors

WpfHexEditor uses a **plugin architecture** (`IDocumentEditor`) — every editor is a standalone, reusable component hosted in the docking system.

| Editor | Status | Progress | Description |
|--------|--------|----------|-------------|
| **Hex Editor** | ✅ Active | ~75% | Binary editing — insert/overwrite, 400+ format detection, search, bookmarks, TBL, status bar contributor |
| **TBL Editor** | ✅ Active | ~60% | Character table editor for custom encodings and ROM hacking |
| **Code Editor** | ✅ Active | ~55% | Multi-language code editor with syntax highlighting, find/replace, `IEditorPersistable`, split view |
| **Text Editor** | ✅ Active | ~50% | Text editing with syntax highlighting and encoding support |
| **Image Viewer** | 🔧 Active | ~30% | Binary image viewer — zoom/pan, transform pipeline (rotate/flip/crop/resize), context menu, `FileShare.ReadWrite` for concurrent open |
| **Audio Viewer** | 🔧 Stub | ~5% | Audio binary viewer (planned) |
| **Diff / Changeset Viewer** | 🔧 Active | ~35% | Side-by-side binary comparison and changeset replay |
| **Disassembly Viewer** | 🔧 Stub | ~5% | Binary disassembler (planned) |
| **Structure Editor** | 🔧 Active | ~30% | Binary template / structure editor |

> **Implementing a new editor?** See [IDocumentEditor contract](Sources/WpfHexEditor.Editor.Core/) and register via `EditorRegistry`.

---

## 🧩 Standalone Controls & Libraries

All controls are **independently reusable** — no IDE required. Drop any of them into your own WPF application with a simple project reference.

### UI Controls

| Control | Frameworks | Progress | Description |
|---------|-----------|----------|-------------|
| **[HexEditor](Sources/WpfHexEditor.HexEditor/)** | net8.0-windows | ~80% | Full-featured hex editor UserControl — MVVM, 16 services, insert/overwrite, search, bookmarks, TBL, 400+ format detection |
| **[HexBox](Sources/WpfHexEditor.HexBox/)** | net8.0-windows | ~80% | Lightweight hex input field — zero external dependencies, MVVM-ready |
| **[ColorPicker](Sources/WpfHexEditor.ColorPicker/)** | net8.0-windows | ~95% | Compact color picker UserControl with RGB/HSV/hex input |
| **[Docking.Wpf](Sources/WpfHexEditor.Docking.Wpf/)** | net8.0-windows | ~65% | **Custom-built** VS-style docking engine — float, dock, auto-hide, colored tabs, 8 themes — 100% in-house, zero third-party dependency |

### Libraries & Infrastructure

| Library | Frameworks | Description |
|---------|-----------|-------------|
| **[Core](Sources/WpfHexEditor.Core/)** | net8.0-windows | ByteProvider, 16 services, data layer — the engine powering HexEditor |
| **[Editor.Core](Sources/WpfHexEditor.Editor.Core/)** | net8.0-windows | `IDocumentEditor` plugin contract, editor registry, changeset system, shared interfaces |
| **[BinaryAnalysis](Sources/WpfHexEditor.BinaryAnalysis/)** | net8.0 | 400+ format detection engine, binary templates, DataInspector service |
| **[SDK](Sources/WpfHexEditor.SDK/)** | net8.0-windows | Public plugin API — `IWpfHexEditorPlugin`, `IIDEHostContext`, `IUIRegistry`, 11+ service contracts incl. `ITerminalService`, `PluginCapabilities.Terminal` |
| **[PluginHost](Sources/WpfHexEditor.PluginHost/)** | net8.0-windows | Runtime plugin infrastructure — discovery, load, watchdog, `PluginManagerControl`, `PermissionService` |
| **[Core.Terminal](Sources/WpfHexEditor.Core.Terminal/)** | net8.0-windows | Command engine — 31+ built-in commands, `HxScriptEngine`, `CommandHistory`, `WriteTable` output helper |

---

## 🗂️ IDE Panels

Panels connect to the active document automatically via the docking system.

| Panel | Progress | Description |
|-------|----------|-------------|
| **Parsed Fields Panel** | ~75% | 400+ format detection — parsed field list with type overlay and inline editing |
| **Data Inspector** | ~65% | 40+ byte interpretations at caret position (int, float, GUID, date, color, …); plugin with settings page |
| **Structure Overlay** | ~55% | Visual field highlighting superimposed on the hex grid |
| **Solution Explorer** | ~75% | Project tree with virtual & physical folders, Show All Files, D&D from Windows Explorer, expand-state persistence, delete from disk |
| **Properties Panel** | ~60% | Context-aware properties for the active document (F4) — auto-refresh on cursor idle (400 ms debounce), categorized groups, sort/copy/refresh toolbar |
| **Error Panel** | ~70% | Diagnostics and validation errors from any `IDiagnosticSource` editor |
| **Output Panel** | ~65% | Session log, file operation messages and build feedback |
| **Terminal Panel** | ~70% | Integrated command terminal — 31+ commands, colored output, history, `TerminalMode` (Interactive/Script/ReadOnly), session export, plugin API via `ITerminalService` |
| **Plugin Manager** | ~65% | Browse, enable/disable, uninstall plugins; settings integration via `IPluginWithOptions`; themed toolbar with RelayCommand |
| **Plugin Monitoring** | ~60% | Real-time CPU% + memory charts per plugin; pure WPF `Canvas` + `Polyline` (no charting lib); rolling history, `PerformanceCounter` + GC polling at 1 s interval |
| **Options** | ~75% | VS2026-style settings document tab — 9 pages: theme, display, editing, behavior, status bar, plugins, auto-save |
| **Quick Search Bar** | ~55% | Inline Ctrl+F overlay (VSCode-style) — find next/prev, regex toggle, jump to Advanced |
| **Advanced Search** | ~45% | Full-featured search dialog — 5 modes: Hex, Text, Regex, TBL, Wildcard |
| **File Diff** | ~30% | Side-by-side binary comparison with diff navigation (F7/F8) |

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

Open `WpfHexEditorControl.sln` in Visual Studio 2022, set **WpfHexEditor.App** as startup project, and run.

### Embed the HexEditor in your WPF app

**Reference the V2 projects:**
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

> **NuGet:** A V1 legacy package (`WPFHexaEditor`) is still available on NuGet but is no longer maintained. V2 NuGet packaging is planned.

> **JetBrains Rider:** See the **[Rider Guide](docs/IDE/RIDER_GUIDE.md)** for IntelliSense tips.

**[Complete Tutorial →](GETTING_STARTED.md)**

---

## 🎯 Why Choose WPF HexEditor?

<table>
<tr>
<td width="33%">

### ⚡ Performance
- **99% faster** rendering (DrawingContext)
- **10-100x faster** search (LRU + SIMD + parallel)
- **80-90% less** memory (Span\<T\> + pooling)
- Handles **GB+ files** without freezing

</td>
<td width="33%">

### 🏗️ Clean Architecture
- **MVVM** with 16 specialized services
- **Partial classes** organized by feature
- **Plugin editors** via IDocumentEditor
- **100% testable**, zero UI in services

</td>
<td width="33%">

### 🖥️ Full IDE
- **Project system** (.whsln / .whproj)
- **VS-style docking** (no third-party lib)
- **8 themes** out of the box
- **4 functional editors** + structure, diff, image viewers
- **Plugin system** — open SDK + `.whxplugin` packages
- **Integrated terminal** — 31 commands

</td>
</tr>
<tr>
<td width="33%">

### 🔍 Binary Intelligence
- **400+ formats** auto-detected
- **Parsed Fields** with type overlay
- **Data Inspector** 40+ interpretations
- **Binary templates** support

</td>
<td width="33%">

### 🌍 Multilingual
- **19 languages** defined *(partial translations)*
- **Instant switching** at runtime
- No restart required
- Extensible with new languages

</td>
<td width="33%">

### ✅ Production Ready
- **Insert Mode** bug fixed (#145)
- **Save data loss** resolved
- **Unlimited Undo/Redo**
- **Async** file operations with progress

</td>
</tr>
</table>

---

## 🗺️ Roadmap

> Full planned feature list with issue tracking in [CHANGELOG.md — What's Next](CHANGELOG.md#whats-next).

| Feature | Status | Issues |
|---------|--------|--------|
| **MSBuild & Visual Studio `.sln` / `.csproj` / `.vbproj` support** | 🔜 Planned | #101–103 |
| **Assembly Explorer — full tree + ECMA-335 metadata** | 🔧 In Progress (Phase 1 done) | #104–105 |
| **.NET Decompilation via ILSpy** | 🔜 Planned | #106 |
| **Code Intelligence (LSP / IntelliSense / Snippets)** | 🔜 Planned | #85–89 |
| **Integrated Debugger** | 🔜 Planned | #44, #90 |
| **Git Integration** | 🔜 Planned | #91 |
| **Plugin Marketplace & Auto-Update** | 🔜 Planned | #41–43 |

---

## 📚 Documentation

### User Guides

| Document | Description |
|----------|-------------|
| **[GETTING_STARTED.md](GETTING_STARTED.md)** | Run the IDE or embed the control — step by step |
| **[FEATURES.md](FEATURES.md)** | Complete feature list — IDE, editors, panels, controls |
| **[CHANGELOG.md](CHANGELOG.md)** | Version history and what's new |
| **[MIGRATION.md](docs/migration/MIGRATION.md)** | V1 → V2 migration guide (zero code changes required) |

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

19 languages defined with instant runtime switching (no restart).

> ⚠️ **Partial translations** — not all languages are fully translated yet. English and French are the most complete. Contributions welcome!

English · French · Spanish · German · Italian · Japanese · Korean · Dutch · Polish · Portuguese · Russian · Swedish · Turkish · Chinese · Arabic · Hindi · and more

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
    V1 Contributors: ehsan69h, Janus Tida</sub>
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
