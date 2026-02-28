# WPFHexaEditor

Main WPF Hex Editor control library - a fast, fully customizable hex editor for .NET applications.

## 📁 Project Structure

```
WPFHexaEditor/
├── 🎯 Main Controls
│   ├── HexEditor.xaml.cs       # Primary hex editor control (6000+ lines)
│   ├── HexBox.xaml.cs          # Container/layout control
│   ├── HexByte.cs              # Hexadecimal byte display control
│   ├── StringByte.cs           # Text/ASCII byte display control
│   ├── BaseByte.cs             # Base class for byte display controls
│   └── FastTextLine.cs         # Optimized text rendering component
│
├── 📂 Core/                    # Core infrastructure and utilities
│   ├── Bytes/                  # ByteProvider, byte manipulation
│   ├── CharacterTable/         # TBL file support
│   ├── Converters/             # WPF value converters
│   ├── EventArguments/         # Custom event args
│   ├── Interfaces/             # Core interfaces
│   ├── MethodExtention/        # Extension methods
│   └── Native/                 # Windows API P/Invoke
│
├── 🔧 Services/                # Business logic services (10 services)
│   ├── ClipboardService.cs
│   ├── FindReplaceService.cs
│   ├── UndoRedoService.cs
│   ├── SelectionService.cs
│   ├── HighlightService.cs
│   ├── ByteModificationService.cs
│   ├── BookmarkService.cs
│   ├── TblService.cs
│   ├── PositionService.cs
│   └── CustomBackgroundService.cs
│
├── 💬 Dialog/                  # UI dialogs
│   ├── FindWindow.xaml
│   ├── FindReplaceWindow.xaml
│   ├── GiveByteWindow.xaml
│   └── ReplaceByteWindow.xaml
│
├── 🌍 Properties/              # Assembly info and localization
│   ├── AssemblyInfo.cs
│   ├── Resources.resx          # English (default)
│   ├── Resources.fr-CA.resx    # French Canadian
│   ├── Resources.pl-PL.resx    # Polish
│   ├── Resources.pt-BR.resx    # Brazilian Portuguese
│   ├── Resources.ru-RU.resx    # Russian
│   └── Resources.zh-CN1.resx   # Simplified Chinese
│
└── 📦 Resources/               # Embedded resources (icons, images)
```

## 🎯 Main Component: HexEditor

**[HexEditor.xaml.cs](HexEditor.xaml.cs)** is the primary control that users interact with.

### Key Features:
- ✅ Binary file editing with full undo/redo support
- ✅ Read/write streams and files (any size)
- ✅ Hexadecimal and ASCII/text views side-by-side
- ✅ Search and replace (byte patterns, text, regex)
- ✅ Insert/overwrite modes with dynamic file resizing
- ✅ Copy/paste with multiple formats (hex, text, binary)
- ✅ Bookmarks and custom highlighting
- ✅ TBL file support for custom character encodings
- ✅ Selection and multi-byte operations
- ✅ Scroll markers for visual navigation
- ✅ Customizable appearance (colors, fonts, width)
- ✅ High performance with lazy loading for large files

### Architecture (Service-Based):

The HexEditor delegates business logic to 10 specialized services:

```
HexEditor (Main Controller - 6000+ lines)
    │
    ├── ByteProvider (Data Access Layer)
    │   └── Stream/File I/O with modification tracking
    │
    └── 10 Services (Business Logic Layer)
        ├── ClipboardService      - Copy/paste operations
        ├── FindReplaceService    - Search with caching
        ├── UndoRedoService       - History management
        ├── SelectionService      - Selection logic
        ├── HighlightService      - Visual markers (stateful)
        ├── ByteModificationService - Insert/delete/modify
        ├── BookmarkService       - Bookmark management
        ├── TblService            - Character table handling
        ├── PositionService       - Position calculations
        └── CustomBackgroundService - Background colors
```

**Benefits:**
- Separation of concerns (UI vs business logic)
- Testable services (see [WPFHexaEditor.Tests](../WPFHexaEditor.Tests/))
- Reusable logic (services used in samples)
- Maintainable codebase (reduced complexity)

## 🧩 Byte Display Controls

### HexByte.cs
- Displays byte as hexadecimal (`FF`, `A0`, etc.)
- Handles keyboard input for hex editing
- Two-character input with validation
- Inherits from `BaseByte`

### StringByte.cs
- Displays byte as character (`.`, `A`, `é`, etc.)
- ASCII/ANSI display mode
- Custom character table (TBL) support
- Inherits from `BaseByte`

### BaseByte.cs
- Abstract base class for byte display controls
- Provides common functionality:
  - Selection rendering
  - Mouse interaction
  - Focus management
  - Read-only mode
  - Action indicators (modified, deleted, inserted)
  - Color schemes

## 🎓 Usage Example

### Basic Usage:

```csharp
// In XAML
<wpfHexEditor:HexEditor x:Name="HexEdit"
                        Width="Auto"
                        Height="Auto"/>

// In code-behind
// Open a file
HexEdit.FileName = @"C:\data\file.bin";

// Or use a stream
using var stream = File.OpenRead("data.bin");
HexEdit.Stream = stream;

// Modify bytes
HexEdit.SelectionStart = 100;
HexEdit.SelectionStop = 110;
HexEdit.FillWithByte(0xFF);

// Search
var positions = HexEdit.FindAll(new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }); // "Hello"

// Undo/Redo
HexEdit.Undo();
HexEdit.Redo();

// Save changes
HexEdit.SubmitChanges();
```

### Advanced Usage:

```csharp
// Load custom character table (TBL)
HexEdit.LoadTblFile(@"pokemon_gen1.tbl");

// Set custom background colors
HexEdit.AddCustomBackgroundBlock(100, 20, Colors.Yellow);

// Bookmarks
HexEdit.AddBookmark(500);
HexEdit.SetPosition(500, 1); // Jump to bookmark

// Insert mode (vs overwrite)
HexEdit.InsertMode = true;
HexEdit.InsertByte(0xFF, 1000);

// Events
HexEdit.ByteModified += (s, e) =>
{
    Console.WriteLine($"Byte at {e.Position:X} changed");
};
```

## 🔗 Multi-Targeting

Supports multiple .NET frameworks:
- ✅ **.NET Framework 4.8** - For legacy Windows applications
- ✅ **.NET 8.0-windows** - Latest .NET with modern C# features

## 🌍 Internationalization

Built-in support for 6 languages:
- 🇬🇧 English (default)
- 🇨🇦 French Canadian
- 🇵🇱 Polish
- 🇧🇷 Brazilian Portuguese
- 🇷🇺 Russian
- 🇨🇳 Simplified Chinese

Language resources in [Properties/](Properties/) folder.

## 📚 Documentation

### Detailed Component Documentation:
- **[Core/](Core/README.md)** - Core infrastructure overview
- **[Services/](Services/README.md)** - Service architecture (10 services)
- **[Dialog/](Dialog/README.md)** - Find/Replace dialogs
- **[Core/Bytes/](Core/Bytes/README.md)** - ByteProvider and data layer
- **[Core/CharacterTable/](Core/CharacterTable/README.md)** - TBL file support
- **[Core/Converters/](Core/Converters/README.md)** - WPF value converters
- **[Core/EventArguments/](Core/EventArguments/README.md)** - Event argument classes
- **[Core/Interfaces/](Core/Interfaces/README.md)** - Interface definitions
- **[Core/MethodExtention/](Core/MethodExtention/README.md)** - Extension methods
- **[Core/Native/](Core/Native/README.md)** - Windows API P/Invoke

### Sample Applications:
See [Samples/](../Samples/README.md) for 7 example applications demonstrating various features.

### Unit Tests:
See [WPFHexaEditor.Tests/](../WPFHexaEditor.Tests/README.md) for 80+ unit tests covering all services.

## 🎨 Customization

The HexEditor is highly customizable via properties:

```csharp
// Appearance
HexEdit.Foreground = Brushes.White;
HexEdit.Background = Brushes.Black;
HexEdit.BytePerLine = 16;  // Bytes per line (8, 16, 32, etc.)

// Behavior
HexEdit.ReadOnlyMode = false;
HexEdit.AllowByteInsertion = true;
HexEdit.AutoScrollToHighlight = true;

// Display
HexEdit.ByteSpacing = 5;
HexEdit.HexDataVisibility = Visibility.Visible;
HexEdit.StringDataVisibility = Visibility.Visible;

// Data format
HexEdit.ByteOrder = ByteOrder.LittleEndian;
HexEdit.DataStringVisual = DataVisualType.Hexadecimal;
```

## 🚀 Performance

Optimizations for large files:
- **Lazy Loading**: Only loads visible bytes into memory
- **Virtual Scrolling**: Renders only visible portion
- **Memory-Mapped Files**: Efficient I/O for multi-GB files
- **Change Tracking**: Only modified bytes stored in memory
- **Native APIs**: P/Invoke for high-performance operations
- **FastTextLine**: Optimized text rendering component

Capable of handling multi-GB files with low memory footprint.

## 🔧 Building

```bash
# Build all targets
dotnet build WpfHexEditorCore.csproj

# Build specific framework
dotnet build -f net8.0-windows
dotnet build -f net48

# Run tests
cd ../WPFHexaEditor.Tests
dotnet test
```

## 📦 NuGet Package

Published as: **WpfHexaEditor** on NuGet.org

```bash
dotnet add package WpfHexaEditor
```

## 🎯 Design Philosophy

1. **Separation of Concerns**: UI (HexEditor) vs Business Logic (Services)
2. **Testability**: Services are stateless/testable in isolation
3. **Performance**: Optimized for large files
4. **Extensibility**: Plugin architecture via services
5. **Backward Compatibility**: No breaking API changes

## 🐛 Debugging

Common issues:
- **File locked**: Ensure file is not open in another application
- **Out of memory**: Enable read-only mode for very large files
- **Slow scrolling**: Check BytePerLine setting (16 is optimal)
- **Character encoding**: Load appropriate TBL file for custom encodings

## 📜 License

**Apache 2.0 License** - See [LICENSE.txt](LICENSE.txt)

Copyright 2016-2026
- Original Author: Derek Tremblay
- Contributors: ehsan69h, Janus Tida, Claude Sonnet 4.5

---

✨ High-performance WPF hex editor control for .NET applications
