# WPF HexEditor Samples

This directory contains various sample applications demonstrating different features and use cases of the WPF HexEditor control.

## 📁 Available Samples

### 🖥️ WPFHexEditor.Sample.CSharp
**Platform:** WPF (C#)
**Description:** Main C# WPF sample application showcasing core features of the HexEditor control.

**Features demonstrated:**
- Opening and editing binary files
- Copy/paste operations (Ctrl+C, Ctrl+V)
- Undo/redo functionality (Ctrl+Z, Ctrl+Y)
- Find and replace dialogs
- Custom character table (TBL) support
- Multiple view modes (hexadecimal, decimal, binary)
- Bookmarks
- Selection operations

**How to run:**
```bash
cd WPFHexEditor.Sample.CSharp
dotnet run
```

---

### 📐 WpfHexEditor.Sample.VB
**Platform:** WPF (VB.NET)
**Description:** Visual Basic .NET version of the WPF sample, demonstrating the same features for VB developers.

**How to run:**
```bash
cd WpfHexEditor.Sample.VB
dotnet run
```

---

### 📊 WpfHexEditor.Sample.BarChart
**Platform:** WPF (C#)
**Description:** Advanced sample showing how to visualize binary data as a bar chart alongside the hex editor.

**Features demonstrated:**
- Real-time bar chart representation of byte values
- Visual data analysis
- Custom data visualization
- Synchronized scrolling between chart and hex view

**Use cases:**
- Analyzing file entropy
- Visualizing data patterns
- Finding compressed/encrypted sections
- Binary file analysis

---

### 🪟 WpfHexEditor.Sample.Winform
**Platform:** Windows Forms (C#)
**Description:** Demonstrates how to integrate the WPF HexEditor control into a Windows Forms application using ElementHost.

**Features demonstrated:**
- WPF/WinForms interoperability
- ElementHost usage
- Cross-platform UI integration

**How to run:**
```bash
cd WpfHexEditor.Sample.Winform
dotnet run
```

---

### 🏢 WpfHexEditor.Sample.AvalonDock
**Platform:** WPF (C#)
**Description:** Shows integration with AvalonDock for professional docking and tabbed layouts.

**Features demonstrated:**
- Multiple hex editor instances in tabs
- Docking panels
- Professional IDE-like interface
- Floating windows
- Layout persistence

**Use cases:**
- Multi-file editing
- Professional binary editing tools
- IDE integration
- Advanced file comparison

---

### 📝 WpfHexEditor.Sample.InsertByteAnywhere
**Platform:** WPF (C#)
**Description:** Demonstrates advanced byte insertion and deletion capabilities.

**Features demonstrated:**
- Insert bytes at any position
- Delete bytes from selection
- Dynamic file size modification
- Insert mode vs overwrite mode

**Use cases:**
- Binary file modification
- Data injection
- Binary patching
- File format manipulation

---

### 🔍 WpfHexEditor.Sample.BinaryFilesDifference
**Platform:** WPF (C#)
**Description:** Advanced sample for comparing two binary files and highlighting differences.

**Features demonstrated:**
- Side-by-side file comparison
- Difference highlighting with custom background colors
- Synchronized scrolling
- CustomBackgroundBlock API usage

**Use cases:**
- Binary diff tools
- Version comparison
- ROM hacking (comparing different versions)
- Patch analysis

---

### 🔧 WpfHexEditor.Sample.ServiceUsage
**Platform:** Console (.NET 8.0)
**Description:** Educational console application demonstrating direct usage of all 10 services without the HexEditor UI control.

**Features demonstrated:**
- `SelectionService` - Selection validation and byte retrieval
- `FindReplaceService` - Search with cache optimization
- `ClipboardService` - Copy/paste operations
- `HighlightService` - Managing highlighted byte positions
- `ByteModificationService` - Insert, delete, and modify bytes
- `UndoRedoService` - History management
- `BookmarkService` - Bookmark creation and navigation
- `CustomBackgroundService` - Background color blocks
- `PositionService` - Position calculations and conversions
- `TblService` - Character table operations

**Use cases:**
- Learning the service API architecture
- Building headless/automated binary processing tools
- Understanding how to use services independently
- Creating custom tools without the UI control
- Unit testing and service integration

**How to run:**
```bash
cd WpfHexEditor.Sample.ServiceUsage
dotnet run
```

**Output:** Console output showing the results of each service operation with sample data.

---

## 🚀 Quick Start

### Prerequisites
- .NET 8.0 SDK or .NET Framework 4.8
- Visual Studio 2022 or later (optional)
- Windows OS

### Building All Samples
From the repository root:
```bash
cd Sources
dotnet build WpfHexEditorControl.sln
```

### Running a Specific Sample
Navigate to the sample directory and run:
```bash
dotnet run --project <SampleProjectName>.csproj
```

Or open the solution in Visual Studio and set the desired sample as the startup project.

## 📚 Learning Path

**Recommended order for learning:**

1. **Architecture First:** `WpfHexEditor.Sample.ServiceUsage` ⭐ **NEW!**
   Understand the service layer architecture and how services work independently

2. **Start with:** `WPFHexEditor.Sample.CSharp`
   Learn the basics: opening files, editing, copy/paste, undo/redo

3. **Next:** `WpfHexEditor.Sample.InsertByteAnywhere`
   Learn advanced editing: insertion, deletion, dynamic modifications

4. **Then:** `WpfHexEditor.Sample.BarChart`
   Learn data visualization and analysis techniques

5. **Advanced:** `WpfHexEditor.Sample.BinaryFilesDifference`
   Learn file comparison and custom rendering

6. **Integration:** `WpfHexEditor.Sample.AvalonDock`
   Learn professional UI integration patterns

7. **Cross-platform:** `WpfHexEditor.Sample.Winform`
   Learn WPF/WinForms interoperability

## 🎨 Common Code Patterns

### Opening a File
```csharp
hexEditor.FileName = @"C:\path\to\file.bin";
```

### Programmatic Selection
```csharp
hexEditor.SelectionStart = 0x100;
hexEditor.SelectionStop = 0x1FF;
```

### Finding Data
```csharp
byte[] searchData = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"
long position = hexEditor.FindFirst(searchData);
```

### Custom Character Table
```csharp
hexEditor.LoadTblFile(@"C:\path\to\custom.tbl");
```

### Handling Events
```csharp
hexEditor.SelectionStartChanged += (s, e) =>
{
    Console.WriteLine($"Selection at: 0x{hexEditor.SelectionStart:X}");
};
```

## 🛠️ Architecture Integration

All samples now use the new **Service-based Architecture** (10 services total):

**Stateless Services (6):**
- `SelectionService` - Selection validation and manipulation
- `FindReplaceService` - Search with optimized caching
- `ClipboardService` - Copy/paste operations
- `ByteModificationService` - Insert/delete/modify bytes
- `UndoRedoService` - Undo/redo history management
- `PositionService` - Position calculations and conversions

**Stateful Services (4):**
- `HighlightService` - Search result highlighting
- `BookmarkService` - Bookmark management
- `CustomBackgroundService` - Background color blocks
- `TblService` - Character table operations

**Learn more:**
- [Services/README.md](../WPFHexaEditor/Services/README.md) - Complete service documentation
- [WpfHexEditor.Sample.ServiceUsage](WpfHexEditor.Sample.ServiceUsage/) - Practical service usage examples

## 📖 Additional Resources

- [Main README](../../README.md) - Project overview and features
- [Services Documentation](../WPFHexaEditor/Services/README.md) - Architecture details
- [Core Documentation](../WPFHexaEditor/Core/README.md) - Core components
- [NuGet Package](https://www.nuget.org/packages/WPFHexaEditor/) - Get the library

## 💡 Tips

- **Performance:** For large files (>100MB), consider using `AllowVisualByteAdress` to view only a portion
- **Custom TBL:** Create your own character tables for game modding or proprietary formats
- **Events:** Subscribe to events like `BytesModified`, `SelectionChanged` for reactive UI
- **Theming:** Customize colors via properties like `ForegroundSecondColor`, `SelectionFirstColor`

## 🐛 Troubleshooting

**Issue:** Sample won't build
**Solution:** Ensure you're targeting .NET 8.0-windows or .NET Framework 4.8

**Issue:** File won't open
**Solution:** Check file permissions and that the file exists

**Issue:** High memory usage
**Solution:** Use `AllowVisualByteAdress` for large files

## 📝 Contributing

Want to add a new sample? Please:
1. Follow the naming convention: `WpfHexEditor.Sample.<FeatureName>`
2. Include a README in your sample directory
3. Add your sample to this index
4. Submit a pull request

---

✨ Created by Derek Tremblay (derektremblay666@gmail.com)
