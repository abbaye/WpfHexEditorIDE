<div align="center">
  <img src="Images/Logo2026.png?raw=true" width="750" height="250" />
  <br/><br/>

  <h3>⚡ The Fastest Wpf Hex Editor IDE for .NET ⚡</h3>

  [![NuGet Legacy V1](https://img.shields.io/nuget/v/WPFHexaEditor?color=blue&label=NuGet%20(Legacy%20V1)&logo=nuget)](https://www.nuget.org/packages/WPFHexaEditor/)
  [![.NET Multi-Target](https://img.shields.io/badge/.NET-net48%20%7C%20net8.0--windows-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
  [![Platform](https://img.shields.io/badge/Platform-Windows%20WPF-0078D4?logo=windows)](https://github.com/abbaye/WpfHexEditorControl)
  [![C#](https://img.shields.io/badge/C%23-13.0-239120?logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
  [![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)
  [![Languages](https://img.shields.io/badge/Languages-19-success?logo=googletranslate&logoColor=white)](#-multilingual)
  [![Status](https://img.shields.io/badge/Status-Active%20Development-orange)](https://github.com/abbaye/WpfHexEditorControl/commits/master)

  <br/>

  > 🚧 **Active Development** — New features, editors and panels are being added regularly. The IDE application is under active construction. Contributions and feedback welcome!

  <br/>

  <img src="Images/Sample2026-001.png?raw=true" alt="WPF HexEditor IDE" width="900"/>
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
- **Virtual & physical folders** (like VS Solution Explorer)
- **Format versioning** with auto-migration
- **Per-file state** persistence (bookmarks, scroll, encoding)

</td>
<td width="50%">

### 🪟 VS-Style Docking
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
- **Output panel** + **Error/Diagnostics panel**

</td>
<td width="50%">

### 🔍 Binary Intelligence
- **400+ file format** auto-detection
- **Parsed Fields Panel** with structure overlay
- **Data Inspector** — 40+ type interpretations
- **19 languages** with instant switching *(partial — not all languages fully translated)*

</td>
</tr>
</table>

---

## 📝 Editors

WpfHexEditor uses a **plugin architecture** (`IDocumentEditor`) — every editor is a standalone, reusable component hosted in the docking system.

| Editor | Status | Progress | Description |
|--------|--------|----------|-------------|
| **Hex Editor** | ✅ Active | ~70% | Binary editing — insert/overwrite, 400+ format detection, search, bookmarks, TBL |
| **TBL Editor** | ✅ Active | ~60% | Character table editor for custom encodings and ROM hacking |
| **JSON Editor** | ✅ Active | ~55% | JSON editing with real-time validation, syntax highlighting and diagnostics |
| **Text Editor** | ✅ Active | ~45% | Text editing with syntax highlighting and encoding support |
| **Image Viewer** | 🔧 Stub | ~5% | Binary image viewer (planned) |
| **Audio Viewer** | 🔧 Stub | ~5% | Audio binary viewer (planned) |
| **Diff Viewer** | 🔧 Stub | ~5% | Side-by-side file comparison (planned) |
| **Disassembly Viewer** | 🔧 Stub | ~5% | Binary disassembler (planned) |
| **Entropy Viewer** | 🔧 Stub | ~5% | Entropy analysis viewer (planned) |

> **Implementing a new editor?** See [IDocumentEditor contract](Sources/WpfHexEditor.Editor.Core/) and register via `EditorRegistry`.

---

## 🧩 Standalone Controls & Libraries

All controls are **independently reusable** — no IDE required. Drop any of them into your own WPF application with a simple project reference.

### UI Controls

| Control | Frameworks | Progress | Description |
|---------|-----------|----------|-------------|
| **[HexEditor](Sources/WpfHexEditor.HexEditor/)** | net48 · net8.0-windows | ~80% | Full-featured hex editor UserControl — MVVM, 16 services, insert/overwrite, search, bookmarks, TBL, 400+ format detection |
| **[HexBox](Sources/WpfHexEditor.HexBox/)** | net48 · net8.0-windows | ~80% | Lightweight hex input field — zero external dependencies, MVVM-ready |
| **[ColorPicker](Sources/WpfHexEditor.ColorPicker/)** | net48 · net8.0-windows | ~95% | Compact color picker UserControl with RGB/HSV/hex input |
| **[BarChart](Sources/WpfHexEditor.BarChart/)** | net48 · net8.0-windows | ~75% | Byte frequency distribution chart (0x00–0xFF visualization) |

### Libraries & Infrastructure

| Library | Frameworks | Progress | Description |
|---------|-----------|----------|-------------|
| **[Docking.Wpf](Sources/WpfHexEditor.Docking.Wpf/)** | net8.0-windows | ~65% | VS-style docking engine — float, dock, auto-hide, colored tabs, 8 themes — no third-party dependency |
| **[Core](Sources/WpfHexEditor.Core/)** | net48 · net8.0-windows | — | ByteProvider, 16 services, data layer — the engine powering HexEditor |
| **[Editor.Core](Sources/WpfHexEditor.Editor.Core/)** | net48 · net8.0-windows | — | `IDocumentEditor` plugin contract, editor registry, shared interfaces |
| **[BinaryAnalysis](Sources/WpfHexEditor.BinaryAnalysis/)** | net8.0-windows | — | 400+ format detection engine, binary templates, DataInspector service |

---

## 🗂️ Analysis & IDE Panels

Panels connect to the active document automatically via the docking system.

| Panel | Description |
|-------|-------------|
| **Parsed Fields Panel** | 400+ format detection — parsed field list with type overlay and inline editing |
| **Data Inspector** | 40+ byte interpretations at caret position (int, float, GUID, date, color, …) |
| **Structure Overlay** | Visual field highlighting superimposed on the hex grid |
| **Solution Explorer** | Project tree with virtual & physical folders, Show All Files mode |
| **Properties Panel** | Context-aware properties for the active document (F4) |
| **Error Panel** | Diagnostics and validation errors from any `IDiagnosticSource` editor |
| **File Diff** | Side-by-side binary comparison with diff navigation (F7/F8) |

---

## 📸 Screenshots

<table>
<tr>
<td width="50%" align="center">
  <b>🖥️ IDE Overview</b><br/>
  <sub>VS-style docking with Solution Explorer, HexEditor and ParsedFieldsPanel</sub><br/><br/>
  <img src="Images/App-IDE-Overview.png?raw=true" alt="IDE Overview"/>
</td>
<td width="50%" align="center">
  <b>🔬 Parsed Fields Panel</b><br/>
  <sub>400+ format auto-detection with structured field analysis</sub><br/><br/>
  <img src="Images/App-ParsedFields.png?raw=true" alt="Parsed Fields"/>
</td>
</tr>
<tr>
<td width="50%" align="center">
  <b>📝 Multi-Editor Tabs</b><br/>
  <sub>HexEditor, TBL, JSON and Text editors side by side</sub><br/><br/>
  <img src="Images/App-Editors.png?raw=true" alt="Multiple Editors"/>
</td>
<td width="50%" align="center">
  <b>🗂️ Solution Explorer</b><br/>
  <sub>VS-style project tree with virtual and physical folders</sub><br/><br/>
  <img src="Images/App-SolutionExplorer.png?raw=true" alt="Solution Explorer"/>
</td>
</tr>
</table>

<details>
<summary>📷 <b>More screenshots</b> — TBL Editor, Error List, Output, Light theme</summary>

<br/>

<table>
<tr>
<td width="50%" align="center">
  <b>📋 TBL Editor</b><br/>
  <sub>Custom character table editor for ROM hacking and encodings</sub><br/><br/>
  <img src="Images/App-TBLEditor.png?raw=true" alt="TBL Editor"/>
</td>
<td width="50%" align="center">
  <b>🔴 Error Panel</b><br/>
  <sub>Diagnostics and validation errors from active editors</sub><br/><br/>
  <img src="Images/App-ErrorList.png?raw=true" alt="Error Panel"/>
</td>
</tr>
<tr>
<td width="50%" align="center">
  <b>📤 Output Panel</b><br/>
  <sub>Session log, messages and file operation feedback</sub><br/><br/>
  <img src="Images/App-Output.png?raw=true" alt="Output Panel"/>
</td>
<td width="50%" align="center">
  <b>☀️ Light Theme</b><br/>
  <sub>8 built-in themes — Dark, Light, VS2022Dark, DarkGlass, and more</sub><br/><br/>
  <img src="Images/App-Theme-Light.png?raw=true" alt="Light Theme"/>
</td>
</tr>
<tr>
<td colspan="2" align="center">
  <b>🎮 TBL Format Explained</b><br/>
  <sub>Custom character table format for game ROM editing</sub><br/><br/>
  <img src="Images/TBLExplain.png?raw=true" alt="TBL Explained" width="600"/>
</td>
</tr>
</table>

</details>

---

## ⚡ Quick Start

### Run the IDE

```bash
git clone https://github.com/abbaye/WpfHexEditorControl.git
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
- **4 functional editors** + 5 planned

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
| **.NET Framework** | 4.8 | Full support — LRU cache, parallel search |
| **.NET** | 8.0-windows | Maximum performance — Span\<T\>, SIMD, PGO |

**Recommendation:** Use .NET 8.0 for best performance (SIMD vectorization, Profile-Guided Optimization).

---

## 🌍 Multilingual

19 languages defined with instant runtime switching (no restart).

> ⚠️ **Partial translations** — not all languages are fully translated yet. English and French are the most complete. Contributions welcome!

English · French · Spanish · German · Italian · Japanese · Korean · Dutch · Polish · Portuguese · Russian · Swedish · Turkish · Chinese · Arabic · Hindi · and more

---

## ⭐ Support This Project

WPF HexEditor is **100% free and open source** (Apache 2.0) — free for personal and commercial use.

This project is developed in **free time** by passionate contributors. If you find it useful:

- ⭐ **Star this repository** — helps others discover the project
- 🍴 **Fork and contribute** — pull requests are always welcome
- 💬 **Share feedback** — report bugs or suggest features via [GitHub Issues](https://github.com/abbaye/WpfHexEditorControl/issues)

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

**Apache License 2.0** — free for personal and commercial use. See [LICENSE](LICENSE) for details.

---

## 📧 Contact & Support

- 🐛 **Bug Reports**: [GitHub Issues](https://github.com/abbaye/WpfHexEditorControl/issues)
- 💡 **Feature Requests**: [GitHub Discussions](https://github.com/abbaye/WpfHexEditorControl/discussions)
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
  **[⭐ Star Us](https://github.com/abbaye/WpfHexEditorControl)**

  <br/>
</div>
