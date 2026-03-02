# Welcome to WPF HexEditor Wiki 👋

<div align="center">
  <img src="../../Images/Logo.png?raw=true" width="420" height="100" />
  <br/><br/>
  <h3>⚡ The Fastest WPF Hex Editor Control for .NET ⚡</h3>
</div>

---

## 🚀 Quick Navigation

<table>
<tr>
<td width="50%">

### 📖 Getting Started
- **[Installation Guide](Installation)** - NuGet package setup
- **[Quick Start Tutorial](Quick-Start)** - Your first hex editor
- **[Basic Operations](Basic-Operations)** - Open, edit, save files
- **[Sample Applications](Sample-Applications)** - 7+ working examples

</td>
<td width="50%">

### 🏗️ Architecture
- **[System Overview](Architecture-Overview)** - MVVM design
- **[Core Systems](Core-Systems)** - ByteProvider, Mapper, Edits
- **[Data Flow](Data-Flow)** - Operation sequences
- **[Services Layer](Services-Layer)** - 15 specialized services

</td>
</tr>
<tr>
<td width="50%">

### 📚 API Reference
- **[File Operations API](API-File-Operations)** - Open, Save, Close
- **[Byte Operations API](API-Byte-Operations)** - Read, Modify, Insert, Delete
- **[Search API](API-Search-Operations)** - Find, Replace, Count
- **[Edit Operations API](API-Edit-Operations)** - Undo, Redo, Clear
- **[Features API](API-Features)** - Bookmarks, Highlights, TBL

</td>
<td width="50%">

### 💡 Guides
- **[Best Practices](Best-Practices)** - Performance tips
- **[Common Patterns](Common-Patterns)** - Frequent use cases
- **[Troubleshooting](Troubleshooting)** - Fix common issues
- **[FAQ](FAQ)** - Frequently asked questions

</td>
</tr>
</table>

---

## 💎 What is WPF HexEditor?

**WPF HexEditor** is a powerful, high-performance hex editor control for .NET applications. Built with modern MVVM architecture, it delivers **professional-grade binary editing** with:

- ⚡ **99% faster rendering** than legacy versions
- 🔍 **10-100x faster search** with LRU cache + SIMD
- 💾 **80-90% less memory** usage
- 📁 Handles **files from bytes to gigabytes**

**Perfect for:**
- 🔬 Binary file analysis
- 🎮 Game ROM modding
- 🔧 Data recovery
- 🌐 Protocol debugging
- 🔐 Security research

---

## 🎯 Key Features

### Core Capabilities
- ✅ **Multi-format editing** - Hex, Decimal, Binary, Octal
- ✅ **Insert/Overwrite modes** - Full insert mode support
- ✅ **Unlimited Undo/Redo** - Complete history management
- ✅ **Copy as code** - Generate byte arrays for multiple languages
- ✅ **Async operations** - Progress reporting + cancellation

### Advanced Features
- 🔍 **Smart search** - LRU cache, SIMD acceleration, parallel multi-core
- 📍 **Scrollbar markers** - Visual indicators for search/bookmarks/changes
- 📊 **BarChart view** - Byte frequency visualization
- 🪟 **AvalonDock support** - Professional IDE integration
- 🌍 **19 languages** - Multilingual with instant switching

### Developer Features
- 🏗️ **MVVM architecture** - Clean separation of concerns
- 🔧 **15 specialized services** - Modular, testable, extensible
- 📦 **Zero dependencies** - No external packages required
- 🎯 **Modern codebase** - Legacy code removed in v2.6.0 (Feb 2026)

---

## 📦 Installation

**NuGet Package**:
```bash
dotnet add package WPFHexaEditor
```

**Basic Usage**:
```xml
<Window xmlns:hex="clr-namespace:WpfHexaEditor;assembly=WPFHexaEditor">
  <hex:HexEditor FileName="data.bin" />
</Window>
```

👉 **[Complete installation guide →](Installation)**

---

## 📚 Documentation Structure

### For Beginners
1. **[Installation](Installation)** - Set up the control
2. **[Quick Start](Quick-Start)** - Basic example
3. **[Basic Operations](Basic-Operations)** - Common tasks
4. **[Sample Applications](Sample-Applications)** - Working examples

### For Developers
1. **[Architecture Overview](Architecture-Overview)** - System design
2. **[API Reference](API-Reference)** - Complete API docs
3. **[Best Practices](Best-Practices)** - Performance tips
4. **[Common Patterns](Common-Patterns)** - Recipes

### For Advanced Users
1. **[Core Systems](Core-Systems)** - Internal architecture
2. **[Data Flow](Data-Flow)** - Operation sequences
3. **[Services Layer](Services-Layer)** - Service documentation
4. **[Extending HexEditor](Extending-HexEditor)** - Custom features

---

## 🎓 Learning Path

**Beginner** (30 minutes):
1. Read [Quick Start](Quick-Start)
2. Run [C# WPF Sample](Sample-Applications#c-wpf-sample)
3. Try [Basic Operations](Basic-Operations)

**Intermediate** (2 hours):
1. Study [Architecture Overview](Architecture-Overview)
2. Explore [API Reference](API-Reference)
3. Review [Common Patterns](Common-Patterns)

**Advanced** (1 day):
1. Deep dive into [Core Systems](Core-Systems)
2. Understand [Data Flow](Data-Flow)
3. Build custom features with [Services](Services-Layer)

---

## 💡 Quick Examples

### Open and Edit a File

```csharp
// Open file
hexEditor.FileName = "data.bin";

// Read byte
byte value = hexEditor.GetByte(0x100);

// Modify byte
hexEditor.ModifyByte(0xFF, 0x100);

// Save
hexEditor.Save();
```

👉 **[See more examples →](Basic-Operations)**

### Search for Pattern

```csharp
// Find pattern
var pattern = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
long position = hexEditor.FindFirst(pattern);

if (position >= 0)
{
    Console.WriteLine($"Found at 0x{position:X}");
}
```

👉 **[Advanced search examples →](API-Search-Operations)**

### Async Save with Progress

```csharp
// Save with progress bar
var progress = new Progress<double>(percent =>
{
    progressBar.Value = percent;
});

await hexEditor.SaveAsync("output.bin", progress);
```

👉 **[Async operations guide →](Async-Operations)**

---

## 🔗 External Resources

- **[GitHub Repository](https://github.com/abbaye/WpfHexEditorIDE)** - Source code
- **[NuGet Package](https://www.nuget.org/packages/WPFHexaEditor/)** - Package downloads
- **[Issue Tracker](https://github.com/abbaye/WpfHexEditorIDE/issues)** - Bug reports
- **[Discussions](https://github.com/abbaye/WpfHexEditorIDE/discussions)** - Community Q&A

---

## 📧 Support

- 🐛 **Bug Reports**: [GitHub Issues](https://github.com/abbaye/WpfHexEditorIDE/issues)
- 💡 **Feature Requests**: [GitHub Discussions](https://github.com/abbaye/WpfHexEditorIDE/discussions)
- 📧 **Email**: derektremblay666@gmail.com

---

## 🤝 Contributing

We welcome contributions! See **[Contributing Guide](Contributing)** for:
- Code style guidelines
- Pull request process
- Testing requirements
- Documentation standards

---

## 📝 License

**Apache License 2.0** - Free for personal and commercial use.

---

<div align="center">
  <br/>
  <p>
    <b>Ready to get started?</b><br/>
    👉 <a href="Installation"><b>Installation Guide</b></a> •
    <a href="Quick-Start"><b>Quick Start Tutorial</b></a> •
    <a href="Sample-Applications"><b>Sample Apps</b></a>
  </p>
  <br/>
  <p>
    <sub>Created with ❤️ by Derek Tremblay & Contributors</sub>
  </p>
</div>
