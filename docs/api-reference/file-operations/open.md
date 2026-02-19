# Open()

Open a binary file for editing in the hex editor.

---

## 📋 Description

The `Open()` method loads a binary file into the hex editor for viewing and editing. The file is opened with read/write access if possible, falling back to read-only mode if the file is locked or permissions are restricted.

---

## 📝 Signatures

```csharp
// Method 1: Set FileName property
public string FileName { get; set; }

// Method 2: Explicit Open method
public void Open(string fileName)

// Method 3: Async with progress
public async Task OpenAsync(string fileName, IProgress<double> progress = null)
```

**Since:** V1.0 (FileName property), V2.0 (OpenAsync)

---

## ⚙️ Parameters

| Parameter | Type | Description | Default |
|-----------|------|-------------|---------|
| `fileName` | `string` | Full path to the file to open | - |
| `progress` | `IProgress<double>` | Progress reporter (0-100%) | `null` |

---

## 🔄 Returns

| Method | Return Type | Description |
|--------|-------------|-------------|
| `FileName` setter | `void` | Sets property and opens file |
| `Open()` | `void` | Opens file synchronously |
| `OpenAsync()` | `Task` | Opens file asynchronously with progress |

---

## 🎯 Examples

### Example 1: Open File (Simple)

```csharp
using WpfHexaEditor;

// Create hex editor
var hexEditor = new HexEditor();

// Method 1: Set FileName property (most common)
hexEditor.FileName = @"C:\Data\file.bin";

// Method 2: Call Open() explicitly (same result)
hexEditor.Open(@"C:\Data\file.bin");

// File is now loaded and ready for editing
```

### Example 2: Open with File Dialog

```csharp
using Microsoft.Win32;

private void OpenFileButton_Click(object sender, RoutedEventArgs e)
{
    // Show Open File Dialog
    var dialog = new OpenFileDialog
    {
        Filter = "All Files (*.*)|*.*|Binary Files (*.bin)|*.bin",
        Title = "Select a file to open"
    };

    if (dialog.ShowDialog() == true)
    {
        try
        {
            // Open selected file
            hexEditor.FileName = dialog.FileName;

            // Update UI
            statusLabel.Text = $"Opened: {dialog.FileName}";
            Title = $"Hex Editor - {Path.GetFileName(dialog.FileName)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error opening file: {ex.Message}",
                          "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
```

### Example 3: Async Open with Progress Bar

```csharp
private async void OpenFileAsync_Click(object sender, RoutedEventArgs e)
{
    var dialog = new OpenFileDialog();

    if (dialog.ShowDialog() == true)
    {
        // Show progress UI
        progressBar.Visibility = Visibility.Visible;
        progressBar.Value = 0;
        statusLabel.Text = "Opening file...";

        // Create progress reporter
        var progress = new Progress<double>(percent =>
        {
            progressBar.Value = percent;
            statusLabel.Text = $"Opening: {percent:F1}%";
        });

        try
        {
            // Open file asynchronously
            await hexEditor.OpenAsync(dialog.FileName, progress);

            // Success
            statusLabel.Text = $"Opened: {dialog.FileName}";
            MessageBox.Show("File opened successfully!", "Success");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error");
        }
        finally
        {
            progressBar.Visibility = Visibility.Collapsed;
        }
    }
}
```

### Example 4: Check File Properties After Opening

```csharp
private void OpenAndDisplayInfo_Click(object sender, RoutedEventArgs e)
{
    var dialog = new OpenFileDialog();

    if (dialog.ShowDialog() == true)
    {
        hexEditor.FileName = dialog.FileName;

        // Get file information
        long fileSize = hexEditor.Length;
        bool isReadOnly = hexEditor.IsReadOnly;
        bool canWrite = hexEditor.CanWrite;

        // Format size
        string sizeText;
        if (fileSize < 1024)
            sizeText = $"{fileSize} bytes";
        else if (fileSize < 1024 * 1024)
            sizeText = $"{fileSize / 1024:N0} KB";
        else if (fileSize < 1024 * 1024 * 1024)
            sizeText = $"{fileSize / (1024 * 1024):N2} MB";
        else
            sizeText = $"{fileSize / (1024 * 1024 * 1024):N2} GB";

        // Display information
        var info = $"File: {Path.GetFileName(dialog.FileName)}\n" +
                   $"Size: {sizeText}\n" +
                   $"Read-Only: {isReadOnly}\n" +
                   $"Can Write: {canWrite}";

        MessageBox.Show(info, "File Information");
    }
}
```

### Example 5: Open with Error Handling

```csharp
private void SafeOpenFile(string fileName)
{
    try
    {
        // Validate file path
        if (string.IsNullOrEmpty(fileName))
        {
            MessageBox.Show("Please specify a file path", "Error");
            return;
        }

        // Check if file exists
        if (!File.Exists(fileName))
        {
            MessageBox.Show($"File not found: {fileName}", "Error");
            return;
        }

        // Check file size (warn for large files)
        var fileInfo = new FileInfo(fileName);
        if (fileInfo.Length > 100 * 1024 * 1024) // > 100 MB
        {
            var result = MessageBox.Show(
                $"This file is {fileInfo.Length / (1024 * 1024):N0} MB. " +
                "Opening large files may take time. Continue?",
                "Large File", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;
        }

        // Open file
        hexEditor.FileName = fileName;

        statusLabel.Text = $"Opened: {fileName}";
    }
    catch (UnauthorizedAccessException)
    {
        MessageBox.Show("Access denied. Try running as administrator.", "Error");
    }
    catch (IOException ex)
    {
        MessageBox.Show($"I/O error: {ex.Message}", "Error");
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Unexpected error: {ex.Message}", "Error");
    }
}
```

### Example 6: Open Recent Files

```csharp
public class RecentFilesManager
{
    private const int MaxRecentFiles = 10;
    private List<string> _recentFiles = new();

    public void AddRecentFile(string fileName)
    {
        // Remove if already exists
        _recentFiles.Remove(fileName);

        // Add to top
        _recentFiles.Insert(0, fileName);

        // Keep only max items
        if (_recentFiles.Count > MaxRecentFiles)
            _recentFiles.RemoveAt(MaxRecentFiles);

        // Save to settings
        SaveRecentFiles();
    }

    public void PopulateMenu(Menu recentFilesMenu, HexEditor hexEditor)
    {
        recentFilesMenu.Items.Clear();

        foreach (var file in _recentFiles)
        {
            var menuItem = new MenuItem
            {
                Header = Path.GetFileName(file),
                Tag = file
            };

            menuItem.Click += (s, e) =>
            {
                var fileName = (s as MenuItem)?.Tag as string;
                if (fileName != null && File.Exists(fileName))
                {
                    hexEditor.FileName = fileName;
                }
            };

            recentFilesMenu.Items.Add(menuItem);
        }

        if (_recentFiles.Count == 0)
        {
            recentFilesMenu.Items.Add(new MenuItem
            {
                Header = "(No recent files)",
                IsEnabled = false
            });
        }
    }

    private void SaveRecentFiles()
    {
        // Save to user settings (implementation depends on your settings system)
        Properties.Settings.Default.RecentFiles = string.Join("|", _recentFiles);
        Properties.Settings.Default.Save();
    }
}

// Usage
private RecentFilesManager _recentFiles = new();

private void OpenFile_Click(object sender, RoutedEventArgs e)
{
    var dialog = new OpenFileDialog();

    if (dialog.ShowDialog() == true)
    {
        hexEditor.FileName = dialog.FileName;
        _recentFiles.AddRecentFile(dialog.FileName);
        _recentFiles.PopulateMenu(recentFilesMenu, hexEditor);
    }
}
```

### Example 7: Drag & Drop Support

```csharp
public MainWindow()
{
    InitializeComponent();

    // Enable drag & drop
    hexEditor.AllowDrop = true;
    hexEditor.DragEnter += HexEditor_DragEnter;
    hexEditor.Drop += HexEditor_Drop;
}

private void HexEditor_DragEnter(object sender, DragEventArgs e)
{
    // Check if dragging files
    if (e.Data.GetDataPresent(DataFormats.FileDrop))
    {
        e.Effects = DragDropEffects.Copy;
    }
    else
    {
        e.Effects = DragDropEffects.None;
    }
}

private void HexEditor_Drop(object sender, DragEventArgs e)
{
    // Get dropped files
    if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
    {
        // Open first file
        string fileName = files[0];

        try
        {
            hexEditor.FileName = fileName;
            statusLabel.Text = $"Opened: {fileName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error opening file: {ex.Message}", "Error");
        }
    }
}
```

---

## 💡 Use Cases

### 1. ROM File Editor

```csharp
// Open game ROM for modding
hexEditor.FileName = @"C:\Games\game.rom";

// Find text string
var pattern = Encoding.ASCII.GetBytes("PLAYER_NAME");
long position = hexEditor.FindFirst(pattern);

if (position >= 0)
{
    // Modify game text
    byte[] newText = Encoding.ASCII.GetBytes("HERO_NAME  ");
    hexEditor.ModifyBytes(position, newText);
    hexEditor.Save();
}
```

### 2. Binary Protocol Analyzer

```csharp
// Open network packet capture
hexEditor.FileName = @"C:\Captures\packet.bin";

// Highlight protocol headers
hexEditor.AddHighlight(0, 14, Colors.LightBlue, "Ethernet Header");
hexEditor.AddHighlight(14, 20, Colors.LightGreen, "IP Header");
hexEditor.AddHighlight(34, 20, Colors.LightYellow, "TCP Header");
```

### 3. Data Recovery Tool

```csharp
// Open damaged file
hexEditor.FileName = @"C:\Recovery\corrupted.dat";

// Search for file signature (JPEG)
var jpegSignature = new byte[] { 0xFF, 0xD8, 0xFF };
var positions = hexEditor.FindAll(jpegSignature);

Console.WriteLine($"Found {positions.Count} potential JPEG headers");

// Bookmark each signature
foreach (var pos in positions)
{
    hexEditor.AddBookmark(pos, $"JPEG at 0x{pos:X}");
}
```

---

## ⚠️ Important Notes

### File Access Modes

The hex editor attempts to open files with these priorities:
1. **Read/Write** - Full editing capability
2. **Read-Only** - If file is locked or permissions restricted
3. **Fail** - If file doesn't exist or cannot be accessed

Check `hexEditor.CanWrite` to determine if editing is possible.

### Large Files

For files > 100 MB, consider:
- ✅ Use `OpenAsync()` with progress reporting
- ✅ Warn users about potential delay
- ✅ Show progress indicator
- ⚠️ Large files (>1 GB) load quickly but initial cache may take time

### Thread Safety

- ❌ **Not thread-safe**: Must open files on UI thread
- ✅ **Use `OpenAsync()`**: For responsive UI with large files
- ✅ **Dispatcher required**: If opening from background thread

### Performance

| File Size | Open Time | Memory Usage |
|-----------|-----------|--------------|
| < 1 MB | < 100ms | ~5 MB |
| 10 MB | ~200ms | ~20 MB |
| 100 MB | ~500ms | ~120 MB |
| 1 GB | ~2s | ~500 MB |

*Times are approximate and vary by hardware*

---

## 🔗 See Also

- **[Close()](close.md)** - Close the current file
- **[Save()](save.md)** - Save changes to file
- **[OpenStream()](openstream.md)** - Open from Stream instead of file
- **[OpenMemory()](openmemory.md)** - Open from byte array

---

**Last Updated**: 2026-02-19
**Version**: V2.0
