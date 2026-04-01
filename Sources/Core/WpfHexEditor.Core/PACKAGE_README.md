# WPF HexEditor Control v2.2.0

A powerful, fully customizable hex editor control for .NET applications with modern service-based architecture.

> **Version 2.2.0** - Major architecture update with 10 services, 80+ unit tests, and comprehensive documentation

## 🚀 Quick Start

### Installation

```bash
dotnet add package WPFHexaEditor
```

### Basic Usage

```xaml
<Window xmlns:hex="clr-namespace:WpfHexaEditor;assembly=WPFHexaEditor">
    <hex:HexEditor x:Name="HexEdit" Width="Auto" Height="Auto"/>
</Window>
```

```csharp
// Load a file
HexEdit.FileName = @"C:\data\file.bin";

// Search for pattern
byte[] pattern = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"
var positions = HexEdit.FindAll(pattern, true); // Find and highlight

// Modify bytes
HexEdit.SelectionStart = 100;
HexEdit.SelectionStop = 200;
HexEdit.FillWithByte(0xFF);

// Save changes
HexEdit.SubmitChanges();
```

## ✨ Key Features

- **Binary File Editing** - Full read/write support for files and streams
- **Multiple Views** - Hexadecimal, decimal, and binary display modes
- **Service Architecture** - 10 specialized, testable services
- **Search & Replace** - Pattern matching with intelligent caching (10-100x faster)
- **Undo/Redo** - Unlimited history management
- **Custom Character Tables** - TBL file support for game hacking and ROM editing
- **Insert/Overwrite Modes** - Dynamic file size modification
- **80+ Unit Tests** - Comprehensive test coverage with xUnit
- **6 Languages** - Localized UI (EN, FR, PL, PT, RU, ZH)
- **Well Documented** - 19 README files + complete wiki

## 🏗️ Modern Architecture

The control uses a **service-based architecture** that separates business logic from UI:

```
HexEditor Control (UI Layer)
    ├── ClipboardService      - Copy/paste operations
    ├── FindReplaceService    - Search with caching
    ├── UndoRedoService       - History management
    ├── SelectionService      - Selection validation
    ├── HighlightService      - Visual byte marking
    ├── ByteModificationService - Insert/delete/modify
    ├── BookmarkService       - Bookmark management
    ├── TblService            - Character table handling
    ├── PositionService       - Position calculations
    └── CustomBackgroundService - Background colors
```

**Benefits:**
- ✅ Testable components (80+ unit tests)
- ✅ Reusable services (use without UI in console apps, APIs, etc.)
- ✅ Maintainable codebase (separation of concerns)
- ✅ Performance optimized (intelligent caching, memory-mapped files)

## 🎯 Common Use Cases

- **Binary Analysis** - Inspect and modify binary files
- **ROM Hacking** - Edit game ROMs with custom character tables
- **File Patching** - Apply binary patches to executables
- **Data Forensics** - Analyze file structures and hidden data
- **Debugging Tools** - Build hex viewing/editing tools
- **Save File Editing** - Modify game save files

## 📦 Framework Support

- ✅ **.NET Framework 4.8** - For Windows desktop applications
- ✅ **.NET 8.0-windows** - Latest .NET with modern C# features

Works in both **WPF** and **WinForms** applications (via ElementHost).

## 📚 Documentation

- **[GitHub Wiki](https://github.com/abbaye/WpfHexEditorIDE/wiki)** - Complete documentation
- **[Getting Started Guide](https://github.com/abbaye/WpfHexEditorIDE/wiki/Getting-Started)** - Installation and examples
- **[API Reference](https://github.com/abbaye/WpfHexEditorIDE/blob/master/Sources/WPFHexaEditor/README.md)** - Complete API documentation
- **[Architecture Overview](https://github.com/abbaye/WpfHexEditorIDE/wiki/Architecture)** - Service layer design
- **[8 Sample Applications](https://github.com/abbaye/WpfHexEditorIDE/tree/master/Sources/Samples)** - Working examples
- **[FAQ](https://github.com/abbaye/WpfHexEditorIDE/wiki/FAQ)** - Common questions

## 🎓 Advanced Examples

### Search and Replace

```csharp
// Find all occurrences
var findPattern = new byte[] { 0x00, 0x00 };
var positions = HexEdit.FindAll(findPattern, true);

// Replace all
var replaceWith = new byte[] { 0xFF, 0xFF };
HexEdit.ReplaceAll(findPattern, replaceWith);
```

### Custom Character Tables (TBL)

```csharp
// Load TBL file for game hacking
HexEdit.LoadTblFile(@"C:\tables\pokemon_gen1.tbl");
// Now hex view shows game-specific characters
```

### Using Services Without UI

```csharp
// Use services in console app, API, or background worker
var provider = new ByteProvider("file.bin");
var service = new FindReplaceService();

var position = service.FindFirst(provider, pattern, 0);
var allPositions = service.FindAll(provider, pattern); // Cached!
```

### Bookmarks and Highlighting

```csharp
// Add bookmark
HexEdit.SetBookMark(1000);

// Add custom highlighting
HexEdit.AddCustomBackgroundBlock(100, 50, Colors.Yellow);
```

## 🧪 Quality Assurance

- **80+ Unit Tests** - All services comprehensively tested
- **xUnit Framework** - Modern testing approach
- **CI/CD Ready** - Automated testing support
- **Test Coverage** - SelectionService, FindReplaceService, HighlightService, and more

## 🎉 What's New in v2.2.0

### 🏗️ Service-Based Architecture
- **10 Specialized Services** - Complete separation of business logic from UI
- **6 Stateless + 4 Stateful** - Optimized for testability and performance
- **Service APIs** - Use services independently in console apps, APIs, background workers

### 🧪 Comprehensive Testing
- **80+ Unit Tests** - xUnit test coverage for all service components
- **Testable Design** - Services can be tested in isolation without UI
- **Quality Assurance** - SelectionService, FindReplaceService, HighlightService fully tested

### 📚 Complete Documentation
- **GitHub Wiki** - 6+ comprehensive pages (Getting Started, Architecture, FAQ, Contributing)
- **19 README Files** - Every major folder documented with examples
- **Learning Path** - Beginner to advanced progression guide
- **Sample Apps** - 8 working examples including Service Usage console app

### 🐛 Critical Bug Fixes
- **Search Cache** - Fixed invalidation after data modifications
- **Accurate Results** - Search now properly cleared at all 11 modification points
- **Cache Performance** - 10-100x faster repeated searches with intelligent caching

### ⚡ Performance Improvements
- **Intelligent Caching** - FindReplaceService with optimized result storage
- **Memory Optimization** - Stateless services reduce memory footprint
- **Large Files** - Memory-mapped file support for multi-GB files

### 🔧 Enhanced Compatibility
- **.NET 8.0-windows** - Full support with modern C# features
- **.NET Framework 4.8** - Maintained for legacy applications
- **100% Backward Compatible** - No breaking changes to public API

## 🤝 Contributing

Contributions welcome! Check out:
- **[Contributing Guide](https://github.com/abbaye/WpfHexEditorIDE/wiki/Contributing)**
- **[Issue Tracker](https://github.com/abbaye/WpfHexEditorIDE/issues)**
- **[Discussions](https://github.com/abbaye/WpfHexEditorIDE/discussions)**

## 📜 License

**GNU Affero General Public License v3.0** - See [LICENSE](https://github.com/abbaye/WpfHexEditorIDE/blob/master/LICENSE)

Copyright 2016-2026 Derek Tremblay and contributors

---

✨ **Ready to start?** Check out the [Getting Started Guide](https://github.com/abbaye/WpfHexEditorIDE/wiki/Getting-Started)!

Created by Derek Tremblay • Contributors: ehsan69h, Janus Tida, Claude Sonnet 4.5
