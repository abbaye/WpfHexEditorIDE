# Welcome to WPF HexEditor Wiki 👋

<div align="center">
  <img src="../../Images/Logo2026.png?raw=true" width="500" height="210" />
  <br/><br/>
  <h3>⚡ The Fastest WPF Hex Editor IDE for .NET ⚡</h3>
  <sub>Version 0.2.0 · .NET 8.0-windows · GNU Affero General Public License v3.0</sub>
</div>

---

## 🚀 Quick Navigation

<table>
<tr>
<td width="50%">

### 📖 Getting Started
- **[Installation Guide](Installation)** — Clone & run the IDE
- **[Quick Start Tutorial](Quick-Start)** — Your first project
- **[Basic Operations](Basic-Operations)** — Open, edit, save
- **[Sample Applications](Sample-Applications)** — Working examples

</td>
<td width="50%">

### 🏗️ Architecture
- **[System Overview](Architecture-Overview)** — IDE + MVVM design
- **[Core Systems](Core-Systems)** — ByteProvider, Mapper, Edits
- **[Data Flow](Data-Flow)** — Operation sequences
- **[Services Layer](Services-Layer)** — 16 specialized services

</td>
</tr>
<tr>
<td width="50%">

### 🧩 IDE Features
- **[Plugin System](Plugin-System)** — SDK, manifest, lifecycle
- **[Terminal Panel](Terminal-Panel)** — 31+ commands, scripting
- **[Plugin Monitoring](Plugin-Monitoring)** — CPU/memory charts
- **[Docking Engine](Architecture-Overview#docking)** — VS-style layout

</td>
<td width="50%">

### 📚 API & Guides
- **[API Reference](API-Reference)** — Full API docs
- **[Best Practices](Best-Practices)** — Performance tips
- **[Troubleshooting](Troubleshooting)** — Fix common issues
- **[FAQ](FAQ)** — Frequently asked questions

</td>
</tr>
</table>

---

## 💎 What is WPF HexEditor?

**WPF HexEditor** is a **full binary analysis IDE** for Windows, built entirely with WPF and .NET 8.0. It goes far beyond a hex editor control — think Visual Studio for binary files.

### As an IDE Application (`WpfHexEditor.App`)

| Feature | Description |
|---------|-------------|
| 🖥️ **VS-Style Docking** | Float, dock, auto-hide, colored tabs, 8 themes (Dark, Light, VS2022Dark, DarkGlass, Minimal, Office, Cyberpunk, VisualStudio) |
| 📁 **Project System** | `.whsln` / `.whproj` solution/project format, virtual & physical folders, format migration |
| 🧩 **Plugin System** | Open SDK (`IWpfHexEditorPluginV2`), `.whxplugin` packages, `PluginManager`, hot-unload, permissions |
| 💻 **Terminal** | 31+ built-in commands, `HxScriptEngine`, mode indicator (Interactive/Script/ReadOnly), session export |
| 📊 **Plugin Monitoring** | Real-time per-plugin CPU% + memory charts, `PerformanceCounter` + GC, rolling history |
| 🔍 **690+ Format Detection** | Format-aware editor routing, `EmbeddedFormatCatalog`, `preferredEditor` key |
| ⌨️ **Inline Search** | `Ctrl+F` quick bar + `Ctrl+Shift+F` advanced dialog (Hex/Text/Regex/TBL/Wildcard) |
| 🎛️ **Options** | VS2026-style settings tab — theme, display, editing, behavior, plugins, auto-save |

### As a Standalone Control (`WpfHexEditor.HexEditor`)

- ⚡ **99% faster rendering** via custom `DrawingContext` (vs legacy ItemsControl)
- 🔍 **10-100x faster search** — LRU cache + Boyer-Moore + SIMD + parallel multi-core
- 💾 **80-90% less memory** — `Span<T>` + `ArrayPool`
- 📁 Handles **files from bytes to gigabytes** — lazy loading, async operations
- 🏗️ **MVVM** with 16 specialized services, 100% testable
- 🌍 **19 languages** — instant runtime switching, no restart

---

## 🎯 Key Features at a Glance

### HexEditor Control
- ✅ **Insert/Overwrite modes** with true virtual position mapping
- ✅ **Unlimited Undo/Redo** via Command pattern
- ✅ **Copy as code** — C#, C, Python, hex string, binary
- ✅ **Custom background blocks** (`CustomBackgroundBlock` API)
- ✅ **TBL support** — ROM hack character tables
- ✅ **BarChart view** — byte frequency visualization
- ✅ **Scrollbar markers** — search results, bookmarks, changes

### IDE Panels
| Panel | Progress |
|-------|----------|
| Parsed Fields | ~75% |
| Data Inspector (40+ types) | ~65% |
| Structure Overlay | ~55% |
| Solution Explorer | ~75% |
| Properties Panel | ~60% |
| Error Panel | ~70% |
| Output Panel | ~65% |
| **Terminal Panel** | ~70% |
| **Plugin Manager** | ~65% |
| **Plugin Monitoring** | ~60% |
| Quick Search Bar | ~55% |
| Advanced Search | ~45% |

---

## 📦 Installation

**Run the IDE:**
```bash
git clone https://github.com/abbaye/WpfHexEditorIDE.git
```
Open `WpfHexEditorControl.sln` → set **WpfHexEditor.App** as startup → run.

**Embed the HexEditor control in your WPF app:**
```xml
<ProjectReference Include="..\WpfHexEditor.Core\WpfHexEditor.Core.csproj" />
<ProjectReference Include="..\WpfHexEditor.HexEditor\WpfHexEditor.HexEditor.csproj" />
```
```xml
<hex:HexEditor FileName="data.bin" />
```

> NuGet V1 (`WPFHexaEditor`) is available but no longer maintained. V2 targets .NET 8.0-windows exclusively.

👉 **[Complete installation guide →](Installation)**

---

## 💡 Quick Examples

### Open and Edit a File (Control)
```csharp
hexEditor.FileName = "data.bin";
byte value = hexEditor.GetByte(0x100);
hexEditor.ModifyByte(0xFF, 0x100);
hexEditor.Save();
```

### Write a Terminal Command (IDE Plugin)
```csharp
context.TerminalService.Execute("list-files --filter *.bin");
context.TerminalService.WriteTable(
    headers: new[] { "Name", "Size" },
    rows: files.Select(f => new[] { f.Name, f.Length.ToString() }));
```

### Register a Plugin Panel (SDK)
```csharp
public void Init(IIDEHostContext context)
{
    context.UIRegistry.RegisterPanel("my-panel", "My Panel", () => new MyPanelView());
    context.UIRegistry.ShowDockablePanel("my-panel");
}
```

👉 **[Plugin System guide →](Plugin-System)**

---

## 📚 Documentation Structure

### For Beginners
1. **[Installation](Installation)** — Clone and run
2. **[Quick Start](Quick-Start)** — IDE walkthrough
3. **[Basic Operations](Basic-Operations)** — Common hex tasks
4. **[Sample Applications](Sample-Applications)** — Working examples

### For Developers
1. **[Architecture Overview](Architecture-Overview)** — IDE + control design
2. **[Plugin System](Plugin-System)** — Build your own plugin
3. **[Terminal Panel](Terminal-Panel)** — Commands and scripting
4. **[API Reference](API-Reference)** — Full API docs

### For Advanced Users
1. **[Core Systems](Core-Systems)** — ByteProvider, position mapping, undo/redo
2. **[Data Flow](Data-Flow)** — Sequence diagrams
3. **[Services Layer](Services-Layer)** — 16 services documented
4. **[Plugin Monitoring](Plugin-Monitoring)** — Diagnostics and performance charts

---

## 🔗 External Resources

- **[GitHub Repository](https://github.com/abbaye/WpfHexEditorIDE)** — Source code
- **[Issue Tracker](https://github.com/abbaye/WpfHexEditorIDE/issues)** — Bug reports
- **[Discussions](https://github.com/abbaye/WpfHexEditorIDE/discussions)** — Community Q&A
- **[Changelog](../CHANGELOG.md)** — Version history and roadmap

---

## 📧 Support

- 🐛 **Bug Reports**: [GitHub Issues](https://github.com/abbaye/WpfHexEditorIDE/issues)
- 💡 **Feature Requests**: [GitHub Discussions](https://github.com/abbaye/WpfHexEditorIDE/discussions)
- 📧 **Email**: derektremblay666@gmail.com

---

## 🤝 Contributing

We welcome contributions! See **[Contributing Guide](Contributing)** for:
- Code style and file header standards
- Pull request process
- Testing requirements
- Documentation standards (`.MD` per `.CS` / `.XAML`)

---

## 📝 License

**Apache License 2.0** — Free for personal and commercial use.

---

<div align="center">
  <br/>
  <p>
    <b>Ready to get started?</b><br/>
    👉 <a href="Installation"><b>Installation</b></a> •
    <a href="Quick-Start"><b>Quick Start</b></a> •
    <a href="Plugin-System"><b>Plugin System</b></a> •
    <a href="Terminal-Panel"><b>Terminal</b></a>
  </p>
  <br/>
  <p>
    <sub>Created with ❤️ by Derek Tremblay & Contributors · Claude Sonnet 4.6</sub>
  </p>
</div>
