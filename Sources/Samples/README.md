# WPF HexEditor Samples

This directory contains sample applications demonstrating the features and capabilities of the WPF HexEditor control (V2 Architecture).

> **📚 Note:** Legacy V1 samples were removed from the solution in v2.6.0 (February 2026).
> Historical V1 sample projects remain in the `Legacy/` folder for reference only - they are **not** included in the build.

---

## 📁 Active V2 Samples

### 🖥️ WpfHexEditor.Sample.HexEditor
**Platform:** WPF (C# | .NET 8.0 & .NET Framework 4.8)
**Path:** `Sources/Samples/WpfHexEditor.Sample.HexEditor/`
**Description:** Primary V2 sample application showcasing the modern HexEditor architecture with all major features.

**Features demonstrated:**
- 📂 **File Operations** - Open, save, save-as, recent files
- ✂️ **Edit Operations** - Copy/paste (Ctrl+C, Ctrl+V), undo/redo (Ctrl+Z, Ctrl+Y)
- 🔍 **Search & Replace** - Find first, find all, replace all with optimized caching
- 📊 **BarChart Visualization** - Real-time byte distribution analysis (NEW in V2)
- 📝 **TBL Character Tables** - Custom character encoding support
- 🎨 **Custom Highlighting** - Background color blocks for marking regions
- 🔖 **Bookmarks** - Create and navigate bookmarks
- 📐 **Selection** - Visual selection with start/stop positions
- 🌍 **Multilingual UI** - 19 languages with instant switching
- ⚡ **Performance** - 99% faster rendering, 10-100x faster search vs V1

**How to run:**
```bash
cd Sources/Samples/WpfHexEditor.Sample.HexEditor
dotnet run
```

Or open `WpfHexEditorControl.sln` and set `WpfHexEditor.Sample.HexEditor` as the startup project.

---

### 🚀 Rider/SimpleExample
**Platform:** WPF (C# | .NET 8.0)
**Path:** `Sources/Samples/Rider/SimpleExample/`
**Description:** Minimal JetBrains Rider-focused example demonstrating basic HexEditor usage without Visual Studio toolbox.

**Features demonstrated:**
- 📦 Manual control instantiation (no designer/toolbox required)
- 📂 File opening and viewing
- 📝 Basic editing operations
- 🎯 IntelliSense-driven development
- ⚡ Rider Live Templates for fast coding

**How to run:**
```bash
cd Sources/Samples/Rider/SimpleExample
dotnet run
```

**Perfect for:**
- JetBrains Rider users
- VS Code developers
- Learning the programmatic API
- Understanding code-behind patterns

See also: [docs/IDE/RIDER_GUIDE.md](../../docs/IDE/RIDER_GUIDE.md) for complete Rider integration guide.

---

## 🚀 Quick Start

### Prerequisites
- .NET 8.0 SDK **or** .NET Framework 4.8
- Visual Studio 2022, JetBrains Rider, or VS Code
- Windows OS

### Building All Samples
From the repository root:
```bash
cd Sources
dotnet build WpfHexEditorControl.sln
```

This builds:
- WpfHexEditorCore (main library)
- WpfHexEditor.Sample.HexEditor (primary V2 sample)
- WpfHexEditor.RiderSimpleExample (Rider sample)
- WPFHexaEditor.Tests (unit tests)

### Running a Specific Sample
Navigate to the sample directory and run:
```bash
dotnet run
```

Or use Visual Studio/Rider:
1. Open `Sources/WpfHexEditorControl.sln`
2. Right-click desired sample project → "Set as Startup Project"
3. Press F5 to run

---

## 📚 Learning Path

**Recommended order:**

1. **Start with:** `WpfHexEditor.Sample.HexEditor` ⭐ **RECOMMENDED**
   - Explore all features in one comprehensive application
   - See real-world usage patterns
   - Learn the complete V2 API surface

2. **Then:** `Rider/SimpleExample`
   - Understand minimal setup requirements
   - Learn programmatic control creation
   - See clean code-behind patterns

3. **Read:** [docs/api-reference/README.md](../../docs/api-reference/README.md)
   - Detailed API documentation
   - Service layer architecture
   - Advanced scenarios

---

## 🎨 Common Code Patterns

### Basic Setup
```csharp
// XAML
<Window xmlns:hex="clr-namespace:WpfHexaEditor;assembly=WPFHexaEditor">
  <hex:HexEditor x:Name="hexEditor" />
</Window>

// Code-behind
hexEditor.FileName = @"C:\path\to\file.bin";
```

### Opening and Saving Files
```csharp
// Open file
hexEditor.FileName = @"C:\Data\binary.dat";

// Save changes
hexEditor.Save();

// Save As
hexEditor.SaveAs(@"C:\Data\modified.dat");
```

### Selection and Navigation
```csharp
// Programmatic selection
hexEditor.SelectionStart = 0x100;
hexEditor.SelectionStop = 0x1FF;

// Get selected bytes
byte[] selectedBytes = hexEditor.GetSelectionByteArray();

// Navigate to position
hexEditor.SetPosition(0x500, 1);
```

### Search Operations
```csharp
// Find first occurrence
byte[] searchData = new byte[] { 0x4D, 0x5A }; // "MZ" (PE header)
long position = hexEditor.FindFirst(searchData);

if (position != -1)
{
    hexEditor.SetPosition(position, 1);
    hexEditor.SelectionStart = position;
    hexEditor.SelectionStop = position + searchData.Length - 1;
}

// Find all occurrences
var results = hexEditor.FindAll(searchData);
Console.WriteLine($"Found {results.Count} occurrences");

// Replace all
hexEditor.ReplaceAll(searchData, newData);
```

### Custom Character Tables (TBL)
```csharp
// Load TBL file for game ROM modding
hexEditor.LoadTblFile(@"C:\Tables\pokemon.tbl");

// Unload TBL
hexEditor.UnloadTblFile();
```

### Event Handling
```csharp
// Selection changed
hexEditor.SelectionStartChanged += (s, e) =>
{
    Console.WriteLine($"Selection: 0x{hexEditor.SelectionStart:X}");
};

// Data modified
hexEditor.BytesModified += (s, e) =>
{
    Console.WriteLine("File has unsaved changes");
    UpdateSaveButton();
};

// File loaded
hexEditor.FileLoaded += (s, e) =>
{
    Console.WriteLine($"Loaded: {hexEditor.FileName} ({hexEditor.Length} bytes)");
};
```

### Bookmarks
```csharp
// Add bookmark
hexEditor.SetBookmark(0x1000);

// Navigate bookmarks
hexEditor.GoToNextBookmark();
hexEditor.GoToPreviousBookmark();

// Clear all bookmarks
hexEditor.ClearAllBookmarks();
```

### BarChart Visualization (V2 Feature)
```csharp
// Enable BarChart
hexEditor.AllowBarChart = true;
hexEditor.BarChartVisible = true;

// BarChart automatically shows byte distribution in real-time
```

---

## 🛠️ Architecture Overview

All V2 samples use the modern **Service-based Architecture** with 16 specialized services:

**Stateless Services (10):**
- `SelectionService` - Selection validation and manipulation
- `FindReplaceService` - Search with LRU caching and SIMD optimization
- `ClipboardService` - Copy/paste operations
- `ByteModificationService` - Insert/delete/modify bytes
- `UndoRedoService` - Undo/redo with batch support
- `PositionService` - Virtual/physical position mapping
- `ComparisonService` - Binary file comparison
- `VirtualizationService` - Viewport rendering optimization
- `ValidationService` - Input and operation validation
- `BarChartService` - Byte distribution analysis

**Stateful Services (6):**
- `HighlightService` - Search result highlighting
- `BookmarkService` - Bookmark management
- `CustomBackgroundService` - Background color blocks
- `TblService` - Character table operations
- `ScrollMarkerService` - Scroll position markers
- `ThemeService` - UI theming and customization

**Learn more:**
- [Sources/WPFHexaEditor/Services/README.md](../WPFHexaEditor/Services/README.md) - Complete service documentation
- [docs/architecture/Overview.md](../../docs/architecture/Overview.md) - Architecture deep dive

---

## 📂 Legacy Samples (Historical Reference)

The `Legacy/` folder contains 6 V1 sample projects that were removed from the solution in v2.6.0 (February 2026):
- `WPFHexEditor.Sample.CSharp` - Main C# WPF sample (V1)
- `WpfHexEditor.Sample.BarChart` - BarChart visualization (V1)
- `WpfHexEditor.Sample.BinaryFilesDifference` - File comparison (V1)
- `WpfHexEditor.Sample.Winform` - Windows Forms integration (V1)
- `WpfHexEditor.Sample.Performance` - Performance benchmarking (V1)
- `WpfHexEditor.Sample.ServiceUsage` - Service layer examples (V1)

**Status:** ❌ Not included in build (removed from .sln)
**Purpose:** Historical reference only
**Note:** These samples use deprecated `HexEditorLegacy` API which was removed in v2.6.0

All functionality from V1 samples is now available in `WpfHexEditor.Sample.HexEditor` (V2) with significantly better performance.

---

## 📖 Additional Resources

- [Main README](../../README.md) - Project overview and quick start
- [FEATURES.md](../../FEATURES.md) - Complete feature list (163 features)
- [GETTING_STARTED.md](../../GETTING_STARTED.md) - Beginner tutorial
- [API Reference](../../docs/api-reference/README.md) - Method documentation
- [Services Documentation](../WPFHexaEditor/Services/README.md) - Service layer guide
- [RIDER_GUIDE.md](../../docs/IDE/RIDER_GUIDE.md) - JetBrains Rider integration
- [NuGet Package](https://www.nuget.org/packages/WPFHexaEditor/) - Download library

---

## 💡 Tips & Best Practices

### Performance Optimization
- For files >100MB, use `AllowVisualByteAdress = true` to view only loaded portions
- Enable `AllowBarChart = false` for maximum rendering performance
- Use batch mode for multiple edits: `BeginBatch()` → operations → `EndBatch()`

### Custom TBL Tables
- Create `.tbl` files for game modding (Pokémon, Final Fantasy, etc.)
- Format: `XX=Character` (e.g., `01=A`)
- Multi-byte support: `XXXX=Character` (e.g., `0100=あ`)

### Event-Driven UI
- Subscribe to `BytesModified` to enable/disable save button
- Use `SelectionChanged` to update status bar
- Handle `FileLoaded` to refresh UI after file opens

### Theming
Customize colors via properties:
```csharp
hexEditor.ForegroundSecondColor = Colors.DarkGray;
hexEditor.SelectionFirstColor = Colors.LightBlue;
hexEditor.ByteModifiedColor = Colors.Orange;
hexEditor.ByteDeletedColor = Colors.Red;
```

### Multi-Language Support
```csharp
// Change UI language at runtime (no restart needed)
WpfHexaEditor.Core.ResourceHelper.SetLanguage("fr-FR"); // French
WpfHexaEditor.Core.ResourceHelper.SetLanguage("ja-JP"); // Japanese
WpfHexaEditor.Core.ResourceHelper.SetLanguage("en-US"); // English
```

---

## 🐛 Troubleshooting

**Issue:** Sample won't build
**Solution:** Ensure .NET 8.0 SDK is installed: `dotnet --version`

**Issue:** "HexEditorLegacy not found" error
**Solution:** Legacy V1 code was removed in v2.6.0. Use `HexEditor` (V2) instead.

**Issue:** File won't open
**Solution:** Check file permissions and that the file exists. Try running as administrator.

**Issue:** High memory usage with large files
**Solution:** Enable `AllowVisualByteAdress = true` to view only loaded portions

**Issue:** Slow rendering
**Solution:** Disable BarChart (`AllowBarChart = false`) and reduce `BytePerLine` value

---

## 📝 Contributing

Want to add a new sample? Please:
1. Follow naming convention: `WpfHexEditor.Sample.<FeatureName>`
2. Target .NET 8.0-windows (multi-targeting with net48 optional)
3. Include XML documentation and code comments
4. Create a README.md in your sample directory explaining the feature
5. Add entry to this index
6. Submit a pull request

See [CONTRIBUTING.md](../../CONTRIBUTING.md) for detailed guidelines.

---

✨ **Created by Derek Tremblay** (derektremblay666@gmail.com)
🤖 **Contributions by Claude Sonnet 4.5**

**License:** Apache 2.0
