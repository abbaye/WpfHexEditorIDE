# Quick Start Guide: HexEditor

## Installation

### Option 1: NuGet Package (Recommended)
```powershell
Install-Package WPFHexaEditor
```

### Option 2: Build from Source
```bash
git clone https://github.com/abbaye/WpfHexEditorIDE.git
cd WpfHexEditorIDE/Sources/WPFHexaEditor
dotnet build
```

## Basic Usage

### 1. Add to XAML

```xml
<Window x:Class="MyApp.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:hex="clr-namespace:WpfHexaEditor;assembly=WPFHexaEditor">
    <Grid>
        <hex:HexEditor x:Name="HexEditor" />
    </Grid>
</Window>
```

### 2. Open a File

```csharp
// Code-behind
private void OpenFile_Click(object sender, RoutedEventArgs e)
{
    var dialog = new OpenFileDialog();
    if (dialog.ShowDialog() == true)
    {
        HexEditor.OpenFile(dialog.FileName);
    }
}
```

### 3. Save Changes

```csharp
private void Save_Click(object sender, RoutedEventArgs e)
{
    HexEditor.Save();
}
```

That's it! You now have a fully functional hex editor.

## Common Tasks

### Open and Edit a File

```csharp
// Open file
HexEditor.OpenFile(@"C:\path\to\file.bin");

// Check if file is loaded
if (HexEditor.IsFileLoaded)
{
    // Edit byte at position 0
    HexEditor.ModifyByte(0xFF, 0);

    // Save changes
    HexEditor.Save();
}
```

### Search for Bytes

```csharp
// Search for hex pattern
byte[] pattern = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"
long position = HexEditor.FindFirst(pattern);

if (position >= 0)
{
    Console.WriteLine($"Found at position: {position}");
    HexEditor.SetPosition(position); // Navigate to found position
}

// Search for next occurrence
long nextPos = HexEditor.FindNext(pattern);
```

### Copy/Paste Operations

```csharp
// Select bytes
HexEditor.SelectionStart = 0;
HexEditor.SelectionStop = 99; // Select first 100 bytes

// Copy to clipboard
HexEditor.Copy();

// Paste from clipboard
HexEditor.SetPosition(200); // Navigate to paste position
HexEditor.Paste();
```

### Navigate with Bookmarks

```csharp
// Set bookmark at current position
HexEditor.SetBookmark(HexEditor.Position);

// Navigate to next bookmark
long nextBookmark = HexEditor.GetNextBookmark(HexEditor.Position);
if (nextBookmark >= 0)
{
    HexEditor.SetPosition(nextBookmark);
}

// Clear all bookmarks
HexEditor.ClearAllBookmarks();
```

### Undo/Redo

```csharp
// Undo last operation
if (HexEditor.CanUndo)
{
    HexEditor.Undo();
}

// Redo
if (HexEditor.CanRedo)
{
    HexEditor.Redo();
}

// Undo multiple operations
HexEditor.Undo(5); // Undo last 5 operations
```

### Handle Events

```csharp
// Subscribe to events
HexEditor.ByteModified += (s, e) => {
    Console.WriteLine($"Byte modified at {e.BytePositionInStream}: {e.NewByte:X2}");
};

HexEditor.SelectionChanged += (s, e) => {
    Console.WriteLine($"Selection: {e.Start}-{e.Stop} ({e.Length} bytes)");
};

HexEditor.PositionChanged += (s, e) => {
    StatusBar.Text = $"Position: 0x{e.Position:X}";
};
```

## Advanced Features

### Insert Mode

```csharp
// Switch to insert mode (V2-only feature)
HexEditor.EditMode = EditMode.Insert;

// Insert byte at position
HexEditor.InsertByte(0xFF, 100);

// Switch back to overwrite mode
HexEditor.EditMode = EditMode.Overwrite;
```

### Custom Background Blocks

```csharp
// Highlight bytes 0-99 with yellow background
var headerBlock = new CustomBackgroundBlock(
    start: 0,
    length: 100,
    color: Brushes.Yellow,
    description: "File Header"
);
HexEditor.AddCustomBackgroundBlock(headerBlock);

// Clear all highlights
HexEditor.ClearCustomBackgroundBlock();
```

### File Comparison

```csharp
// Compare two files
HexEditor editor1 = new HexEditor();
HexEditor editor2 = new HexEditor();

editor1.OpenFile("file1.bin");
editor2.OpenFile("file2.bin");

var differences = editor1.Compare(editor2);

Console.WriteLine($"Found {differences.Count()} differences");
foreach (var diff in differences.Take(10))
{
    Console.WriteLine($"Position {diff.BytePositionInStream}: " +
                     $"{diff.Origine:X2} vs {diff.Destination:X2}");
}
```

### Save/Load Editor State

```csharp
// Save current state (position, selection, bookmarks, etc.)
HexEditor.SaveCurrentState("my-editor-state.xml");

// Later, restore the state
HexEditor.LoadCurrentState("my-editor-state.xml");
```

### Customize Appearance

```csharp
// Colors
HexEditor.SelectionFirstColor = Colors.Blue;
HexEditor.SelectionSecondColor = Colors.LightBlue;
HexEditor.ByteModifiedColor = Colors.Red;
HexEditor.MouseOverColor = Colors.Yellow;

// Display options
HexEditor.BytePerLine = 16; // 16 bytes per line
HexEditor.ShowOffset = true;
HexEditor.ShowAscii = true;
HexEditor.ShowHeader = true;
HexEditor.ShowStatusBar = true;

// Font
HexEditor.FontSize = 14;
HexEditor.FontFamily = new FontFamily("Consolas");
```

### Character Tables (TBL)

```csharp
// Load custom character table
HexEditor.LoadTBLFile("custom.tbl");

// Use ASCII
HexEditor.TypeOfCharacterTable = CharacterTableType.Ascii;

// Use UTF-8
HexEditor.TypeOfCharacterTable = CharacterTableType.Utf8;

// Advanced TBL colors
HexEditor.TblShowMte = true;
HexEditor.TblDteColor = Colors.Yellow;
HexEditor.TblMteColor = Colors.LightBlue;
```

## MVVM Pattern

### ViewModel

```csharp
public class HexEditorViewModel : INotifyPropertyChanged
{
    private string _filePath;
    private bool _isModified;
    private long _currentPosition;

    public string FilePath
    {
        get => _filePath;
        set { _filePath = value; OnPropertyChanged(); }
    }

    public bool IsModified
    {
        get => _isModified;
        set { _isModified = value; OnPropertyChanged(); }
    }

    public long CurrentPosition
    {
        get => _currentPosition;
        set { _currentPosition = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
```

### XAML Binding

```xml
<hex:HexEditor
    FileName="{Binding FilePath}"
    IsModified="{Binding IsModified, Mode=OneWayToSource}"
    Position="{Binding CurrentPosition, Mode=TwoWay}"
    ReadOnlyMode="{Binding IsReadOnly}"
    BytePerLine="16"
    ShowOffset="True"
    ShowAscii="True" />
```

## Configuration Tips

### Performance

```csharp
// Optimize for large files
HexEditor.PreloadByteInEditorMode = PreloadByteInEditor.MaxScreenVisibleLineAtDataLoad;

// Adjust bytes per line for screen size
HexEditor.BytePerLine = 32; // More bytes visible
```

### Read-Only Mode

```csharp
// Make editor read-only
HexEditor.ReadOnlyMode = true;

// Allow context menu in read-only
HexEditor.AllowContextMenu = true;
```

### Mouse Wheel Speed

```csharp
// Adjust scroll speed
HexEditor.MouseWheelSpeed = MouseWheelSpeed.Fast;
```

## Troubleshooting

### File Won't Open
- Check file permissions
- Verify file exists and is not locked by another process
- Try opening with read-only mode

### Performance Issues
- Ensure you're using the modern HexEditor control (with V2 architecture)
- Check file size (V2 handles GB+ files efficiently)
- Reduce BytePerLine for very large viewports

### Changes Not Saving
- Call `Save()` or `SubmitChanges()` explicitly
- Check `IsModified` property
- Verify file is not read-only

## Next Steps

- **Architecture**: Understand the [Architecture](Architecture.md)
- **Migration**: Migrate from V1 with [Migration Guide](MigrationGuide.md)
- **Examples**: Browse `Samples/` directory for complete examples
- **API**: See full [API Reference](ApiReference.md)

## Support

- **Documentation**: This repository's `docs/` folder
- **GitHub**: https://github.com/abbaye/WpfHexEditorIDE
- **Issues**: Report bugs or request features via GitHub Issues
