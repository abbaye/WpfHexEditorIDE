# 🚀 Getting Started with WPF HexEditor

Complete step-by-step guide to integrating and using the WPF HexEditor control in your .NET applications.

---

## 📦 Installation

### Via NuGet (Recommended)

```bash
# Using .NET CLI
dotnet add package WPFHexaEditor

# Using Package Manager Console
Install-Package WPFHexaEditor
```

### Manual Installation

1. Download the latest release from [GitHub Releases](https://github.com/abbaye/WpfHexEditorControl/releases)
2. Add reference to `WPFHexaEditor.dll` in your project
3. The library automatically works with both .NET Framework 4.8 and .NET 8.0-windows

---

## ⚡ 5-Minute Quick Start

### Step 1: Add the Namespace

In your XAML file, add the namespace declaration:

```xml
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:hex="clr-namespace:WpfHexaEditor;assembly=WPFHexaEditor"
        Title="Hex Editor Demo" Height="600" Width="800">
```

### Step 2: Add the Control

```xml
<Grid>
    <hex:HexEditor x:Name="HexEdit" />
</Grid>
```

### Step 3: Open a File (Code-Behind)

```csharp
private void Window_Loaded(object sender, RoutedEventArgs e)
{
    HexEdit.FileName = "C:\\path\\to\\your\\file.bin";
}
```

**That's it!** You now have a fully functional hex editor. 🎉

---

## 📚 Basic Usage

### Opening Files

```csharp
// Method 1: Direct property
HexEdit.FileName = @"C:\data.bin";

// Method 2: Using OpenFile method
HexEdit.OpenFile(@"C:\data.bin");

// Method 3: From stream
using (var stream = File.OpenRead(@"C:\data.bin"))
{
    HexEdit.Stream = stream;
}
```

### Reading Bytes

```csharp
// Read a single byte at position
byte value = HexEdit.GetByte(0x100);

// Read multiple bytes
byte[] data = HexEdit.GetBytes(0x100, 16);

// Get current selection
byte[] selection = HexEdit.SelectionByteArray;
```

### Modifying Bytes

```csharp
// Modify a single byte
HexEdit.SetByte(0x100, 0xFF);

// Insert bytes
HexEdit.InsertByte(0x100, 0xAB);

// Delete bytes
HexEdit.DeleteByte(0x100, 10); // Delete 10 bytes starting at position 0x100

// Fill with pattern
HexEdit.FillWithByte(0x00); // Fill selection with zeros
```

### Searching

```csharp
// Find first occurrence
long position = HexEdit.FindFirst(new byte[] { 0x4D, 0x5A }); // Find "MZ" header

// Find all occurrences
var positions = HexEdit.FindAll(new byte[] { 0xFF, 0xFF });

// Search with progress reporting
await HexEdit.FindFirstAsync(searchBytes, progress, cancellationToken);
```

### Saving Changes

```csharp
// Save to original file
HexEdit.SubmitChanges();

// Save to new file
HexEdit.SubmitChanges(@"C:\output.bin");

// Check if there are unsaved changes
if (HexEdit.HasChanges)
{
    HexEdit.SubmitChanges();
}
```

---

## 🎯 Common Scenarios

### Scenario 1: WPF Application with File Picker

```xml
<Window x:Class="HexEditorDemo.MainWindow"
        xmlns:hex="clr-namespace:WpfHexaEditor;assembly=WPFHexaEditor">
    <DockPanel>
        <Menu DockPanel.Dock="Top">
            <MenuItem Header="File">
                <MenuItem Header="Open..." Click="OpenFile_Click"/>
                <MenuItem Header="Save" Click="Save_Click"/>
            </MenuItem>
        </Menu>
        <hex:HexEditor x:Name="HexEdit" />
    </DockPanel>
</Window>
```

```csharp
private void OpenFile_Click(object sender, RoutedEventArgs e)
{
    var dialog = new OpenFileDialog();
    if (dialog.ShowDialog() == true)
    {
        HexEdit.FileName = dialog.FileName;
    }
}

private void Save_Click(object sender, RoutedEventArgs e)
{
    HexEdit.SubmitChanges();
    MessageBox.Show("File saved successfully!");
}
```

### Scenario 2: MVVM with Data Binding

**ViewModel:**
```csharp
public class HexEditorViewModel : INotifyPropertyChanged
{
    private string _filePath;
    public string FilePath
    {
        get => _filePath;
        set
        {
            _filePath = value;
            OnPropertyChanged();
        }
    }

    private long _selectionStart;
    public long SelectionStart
    {
        get => _selectionStart;
        set
        {
            _selectionStart = value;
            OnPropertyChanged();
        }
    }

    public RelayCommand OpenCommand { get; }
    public RelayCommand SaveCommand { get; }

    // ... implementation
}
```

**XAML:**
```xml
<hex:HexEditor FileName="{Binding FilePath}"
               SelectionStart="{Binding SelectionStart, Mode=TwoWay}" />
```

### Scenario 3: WinForms Integration

```csharp
using WpfHexaEditor;
using System.Windows.Forms.Integration;

public partial class Form1 : Form
{
    private HexEditor hexEditor;

    public Form1()
    {
        InitializeComponent();

        // Create WPF host
        var host = new ElementHost
        {
            Dock = DockStyle.Fill
        };

        // Create HexEditor
        hexEditor = new HexEditor();
        host.Child = hexEditor;

        // Add to form
        this.Controls.Add(host);
    }

    private void OpenFile()
    {
        using (var dialog = new OpenFileDialog())
        {
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                hexEditor.FileName = dialog.FileName;
            }
        }
    }
}
```

### Scenario 4: Reading Binary File Structures

```csharp
// Example: Parse a BMP header
HexEdit.FileName = "image.bmp";

// Read BMP signature
byte[] signature = HexEdit.GetBytes(0, 2);
string sig = Encoding.ASCII.GetString(signature); // Should be "BM"

// Read file size (little-endian)
byte[] sizeBytes = HexEdit.GetBytes(2, 4);
int fileSize = BitConverter.ToInt32(sizeBytes, 0);

// Read image width and height
int width = BitConverter.ToInt32(HexEdit.GetBytes(18, 4), 0);
int height = BitConverter.ToInt32(HexEdit.GetBytes(22, 4), 0);

Console.WriteLine($"BMP: {width}x{height}, Size: {fileSize} bytes");
```

---

## 🎨 Customization

### Colors and Appearance

```xml
<hex:HexEditor x:Name="HexEdit"
               Background="#1E1E1E"
               Foreground="White"
               SelectionFirstColor="#264F78"
               ByteModifiedColor="Orange"
               MouseOverColor="Yellow"
               FontFamily="Consolas"
               FontSize="14"/>
```

### Visibility Options

```xml
<hex:HexEditor x:Name="HexEdit"
               HexDataVisibility="Visible"
               StringDataVisibility="Visible"
               LineInfoVisibility="Visible"
               HeaderVisibility="Visible"
               StatusBarVisibility="Visible"/>
```

### Bytes Per Line

```xml
<hex:HexEditor x:Name="HexEdit"
               BytePerLine="32"/> <!-- Show 32 bytes per row -->
```

---

## ⌨️ Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| **Ctrl+C** | Copy selection |
| **Ctrl+V** | Paste |
| **Ctrl+X** | Cut |
| **Ctrl+Z** | Undo |
| **Ctrl+Y** | Redo |
| **Ctrl+F** | Open Find dialog |
| **Ctrl+G** | Go to offset |
| **Ctrl+A** | Select all |
| **Ctrl+MouseWheel** | Zoom in/out |
| **ESC** | Clear selection / Close dialogs |

---

## 📡 Events

### Handling Selection Changes

```csharp
HexEdit.SelectionStartChanged += (s, e) =>
{
    Console.WriteLine($"Selection starts at: 0x{HexEdit.SelectionStart:X}");
};

HexEdit.SelectionLengthChanged += (s, e) =>
{
    Console.WriteLine($"Selection length: {HexEdit.SelectionLength} bytes");
};
```

### Handling Modifications

```csharp
HexEdit.ByteModified += (s, e) =>
{
    Console.WriteLine($"Byte at 0x{e.BytePositionInStream:X} changed");
};

HexEdit.BytesDeleted += (s, e) =>
{
    Console.WriteLine("Bytes deleted");
};
```

### Progress Reporting

```csharp
HexEdit.LongProcessProgressChanged += (s, e) =>
{
    progressBar.Value = e.Percent; // 0-100
};

HexEdit.LongProcessProgressStarted += (s, e) =>
{
    progressBar.Visibility = Visibility.Visible;
};

HexEdit.LongProcessProgressCompleted += (s, e) =>
{
    progressBar.Visibility = Visibility.Collapsed;
};
```

---

## 🔧 Advanced Features

### Character Encoding (TBL Files)

```csharp
// Load custom character table (for game ROM editing)
HexEdit.LoadTBLFile(@"C:\game-charset.tbl");

// Switch encoding type
HexEdit.TypeOfCharacterTable = CharacterTableType.ASCII;
HexEdit.TypeOfCharacterTable = CharacterTableType.EBCDIC;

// Custom encoding
HexEdit.CustomEncoding = Encoding.GetEncoding("shift-jis");
```

### Bookmarks

```csharp
// Set bookmark at current position
HexEdit.SetBookmark(HexEdit.SelectionStart);

// Navigate bookmarks
HexEdit.GotoNextBookmark();
HexEdit.GotoPreviousBookmark();

// Clear all bookmarks
HexEdit.ClearAllBookmarks();
```

### Copy as Code

```csharp
// Copy selection as C# byte array
string code = HexEdit.GetCopyData(CopyPasteMode.CSharpCode);
// Output: byte[] data = { 0x4D, 0x5A, 0x90, 0x00 };

// Other modes: VBNetCode, JavaCode, PythonCode, etc.
```

### Custom Background Highlighting

```csharp
// Highlight a region with custom color
var block = new CustomBackgroundBlock
{
    StartOffset = 0x100,
    Length = 256,
    Color = Colors.LightBlue,
    Description = "Header"
};
HexEdit.AddCustomBackgroundBlock(block);
```

---

## 🎮 Sample Applications

### Explore Working Examples

The project includes 7+ sample applications demonstrating various features:

1. **[C# WPF Sample](Sources/Samples/WPFHexEditor.Sample.CSharp/)** - Complete demo with all features
2. **[AvalonDock Sample](Sources/Samples/WpfHexEditor.Sample.AvalonDock/)** - IDE-like dockable interface
3. **[BarChart Sample](Sources/Samples/WpfHexEditor.Sample.BarChart/)** - Data visualization
4. **[WinForms Sample](Sources/Samples/WpfHexEditor.Sample.Winform/)** - Windows Forms integration
5. **[Binary Diff Sample](Sources/Samples/WpfHexEditor.Sample.BinaryFilesDifference/)** - File comparison
6. **[Insert Anywhere Sample](Sources/Samples/WpfHexEditor.Sample.InsertByteAnywhere/)** - Dynamic insert/delete
7. **[Service Usage Sample](Sources/Samples/WpfHexEditor.Sample.ServiceUsage/)** - Console app without UI

Each sample includes full source code and demonstrates best practices.

---

## 🐛 Troubleshooting

### File Won't Open

```csharp
// Check if file is locked
if (HexEdit.IsLockedFile)
{
    MessageBox.Show("File is in use by another process");
}
```

### Performance Issues with Large Files

```csharp
// Use async operations for better responsiveness
await HexEdit.OpenFileAsync(largeFilePath, progress, cancellationToken);

// Enable async search for large files (auto-enabled for files > 100MB)
var results = await HexEdit.FindAllAsync(pattern, progress, cancellationToken);
```

### Memory Usage

The control automatically uses memory-mapped files for large files (GB+). For optimal performance:
- Use .NET 8.0 when possible (better Span&lt;T&gt; and SIMD support)
- Close files when done: `HexEdit.CloseProvider()`
- Dispose properly in your cleanup code

---

## 📖 Next Steps

- **[Feature Comparison](FEATURES.md)** - See all 163 features compared between V1 and V2
- **[Migration Guide](docs/migration/MIGRATION.md)** - Upgrading from V1 to V2 (zero code changes)
- **[Architecture Guide](ARCHITECTURE.md)** - Understand the service-based design
- **[Performance Guide](PERFORMANCE_GUIDE.md)** - Optimization tips and benchmarks
- **[API Reference](Sources/WPFHexaEditor/README.md)** - Complete API documentation

---

## 💡 Tips & Best Practices

1. **Use async methods** for operations on large files (> 100MB)
2. **Dispose properly** - Call `CloseProvider()` when switching files
3. **Leverage events** - Use progress events for long operations
4. **MVVM-friendly** - Bind to properties like `FileName`, `SelectionStart`, `SelectionLength`
5. **Test with your data** - Different file formats have different performance characteristics

---

## 🆘 Getting Help

- **Documentation**: [Complete Documentation Map](README.md#-complete-documentation-map)
- **Issues**: [GitHub Issues](https://github.com/abbaye/WpfHexEditorControl/issues)
- **Samples**: Browse the [7+ sample applications](Sources/Samples/)
- **API Reference**: [Full API docs](Sources/WPFHexaEditor/README.md)

---

**Ready to build?** Check out the [sample applications](Sources/Samples/) for complete working examples! 🚀
