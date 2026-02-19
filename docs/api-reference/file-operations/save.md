# Save()

Save all pending changes to the current file or a new file.

---

## 📋 Description

The `Save()` method persists all modifications, insertions, and deletions to disk. HexEditor V2 uses a **smart save algorithm** that automatically chooses between:
- **Fast path** (100x faster) - For modifications-only edits
- **Full rebuild** - For insertions/deletions (structural changes)

---

## 📝 Signatures

```csharp
// Save to current file
public void Save()

// Save to new file (Save As)
public void SaveAs(string fileName)

// Async save with progress
public async Task SaveAsync(IProgress<double> progress = null)
public async Task SaveAsync(string fileName, IProgress<double> progress = null)
```

**Since:** V1.0 (Save, SaveAs), V2.0 (SaveAsync)

---

## ⚙️ Parameters

| Parameter | Type | Description | Default |
|-----------|------|-------------|---------|
| `fileName` | `string` | Target file path (for SaveAs) | Current file |
| `progress` | `IProgress<double>` | Progress reporter (0-100%) | `null` |

---

## 🔄 Returns

| Method | Return Type | Description |
|--------|-------------|-------------|
| `Save()` | `void` | Saves synchronously |
| `SaveAs()` | `void` | Saves to new file synchronously |
| `SaveAsync()` | `Task` | Saves asynchronously with progress |

---

## 🎯 Examples

### Example 1: Basic Save

```csharp
using WpfHexaEditor;

// Open file
hexEditor.FileName = "data.bin";

// Modify some bytes
hexEditor.ModifyByte(0xFF, 0x100);
hexEditor.ModifyByte(0xAA, 0x200);

// Save changes
hexEditor.Save();

Console.WriteLine("File saved successfully");
```

### Example 2: Save with Confirmation

```csharp
private void SaveButton_Click(object sender, RoutedEventArgs e)
{
    // Check if there are unsaved changes
    if (!hexEditor.HasChanges)
    {
        MessageBox.Show("No changes to save", "Info");
        return;
    }

    try
    {
        // Show what will be saved
        var stats = hexEditor.GetDiagnostics();
        var message = $"Save changes?\n\n" +
                     $"Modifications: {stats.ModificationCount}\n" +
                     $"Insertions: {stats.InsertionCount}\n" +
                     $"Deletions: {stats.DeletionCount}";

        var result = MessageBox.Show(message, "Confirm Save",
                                    MessageBoxButton.YesNo,
                                    MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            // Save
            hexEditor.Save();

            // Update UI
            statusLabel.Text = "File saved";
            Title = Title.TrimEnd('*'); // Remove modified indicator

            MessageBox.Show("File saved successfully!", "Success");
        }
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Error saving file: {ex.Message}",
                       "Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
```

### Example 3: Save As (New File)

```csharp
private void SaveAsButton_Click(object sender, RoutedEventArgs e)
{
    var dialog = new SaveFileDialog
    {
        Filter = "All Files (*.*)|*.*|Binary Files (*.bin)|*.bin",
        Title = "Save file as",
        FileName = Path.GetFileName(hexEditor.FileName)
    };

    if (dialog.ShowDialog() == true)
    {
        try
        {
            // Save to new file
            hexEditor.SaveAs(dialog.FileName);

            // Update current file reference
            hexEditor.FileName = dialog.FileName;

            // Update UI
            statusLabel.Text = $"Saved as: {dialog.FileName}";
            Title = $"Hex Editor - {Path.GetFileName(dialog.FileName)}";

            MessageBox.Show("File saved successfully!", "Success");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error");
        }
    }
}
```

### Example 4: Async Save with Progress

```csharp
private async void SaveAsyncButton_Click(object sender, RoutedEventArgs e)
{
    // Show progress UI
    progressBar.Visibility = Visibility.Visible;
    progressBar.Value = 0;
    statusLabel.Text = "Saving...";

    // Disable save button during save
    saveButton.IsEnabled = false;

    // Create progress reporter
    var progress = new Progress<double>(percent =>
    {
        Dispatcher.Invoke(() =>
        {
            progressBar.Value = percent;
            statusLabel.Text = $"Saving: {percent:F1}%";
        });
    });

    try
    {
        // Save asynchronously
        await hexEditor.SaveAsync(progress);

        // Success
        statusLabel.Text = "File saved";
        MessageBox.Show("File saved successfully!", "Success",
                       MessageBoxButton.OK, MessageBoxImage.Information);
    }
    catch (IOException ex)
    {
        MessageBox.Show($"I/O error: {ex.Message}", "Error",
                       MessageBoxButton.OK, MessageBoxImage.Error);
    }
    catch (UnauthorizedAccessException)
    {
        MessageBox.Show("Access denied. Try running as administrator.", "Error",
                       MessageBoxButton.OK, MessageBoxImage.Error);
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Unexpected error: {ex.Message}", "Error",
                       MessageBoxButton.OK, MessageBoxImage.Error);
    }
    finally
    {
        // Hide progress UI
        progressBar.Visibility = Visibility.Collapsed;
        saveButton.IsEnabled = true;
    }
}
```

### Example 5: Auto-Save Feature

```csharp
public class AutoSaveManager
{
    private DispatcherTimer _timer;
    private HexEditor _hexEditor;
    private bool _hasUnsavedChanges;

    public AutoSaveManager(HexEditor hexEditor, int intervalSeconds = 300)
    {
        _hexEditor = hexEditor;

        // Subscribe to changes
        _hexEditor.DataChanged += (s, e) => _hasUnsavedChanges = true;

        // Setup auto-save timer
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(intervalSeconds)
        };
        _timer.Tick += AutoSave_Tick;
    }

    public void Start()
    {
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
    }

    private async void AutoSave_Tick(object sender, EventArgs e)
    {
        if (_hasUnsavedChanges && _hexEditor.HasChanges)
        {
            try
            {
                // Save to backup file
                string backupFile = _hexEditor.FileName + ".autosave";
                await _hexEditor.SaveAsync(backupFile);

                _hasUnsavedChanges = false;
                Console.WriteLine($"Auto-saved to {backupFile}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Auto-save failed: {ex.Message}");
            }
        }
    }
}

// Usage
private AutoSaveManager _autoSave;

public MainWindow()
{
    InitializeComponent();

    // Enable auto-save every 5 minutes
    _autoSave = new AutoSaveManager(hexEditor, intervalSeconds: 300);
    _autoSave.Start();
}
```

### Example 6: Save with Backup

```csharp
private void SaveWithBackup()
{
    string originalFile = hexEditor.FileName;
    string backupFile = originalFile + ".backup";

    try
    {
        // Create backup of original file
        if (File.Exists(originalFile))
        {
            File.Copy(originalFile, backupFile, overwrite: true);
            Console.WriteLine($"Backup created: {backupFile}");
        }

        // Save changes
        hexEditor.Save();

        // Success - delete backup
        if (File.Exists(backupFile))
        {
            File.Delete(backupFile);
        }

        MessageBox.Show("File saved successfully!", "Success");
    }
    catch (Exception ex)
    {
        // Restore from backup on error
        if (File.Exists(backupFile))
        {
            try
            {
                File.Copy(backupFile, originalFile, overwrite: true);
                hexEditor.Close();
                hexEditor.FileName = originalFile;

                MessageBox.Show("Save failed. Original file restored from backup.",
                               "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch
            {
                MessageBox.Show("Save failed AND backup restoration failed!",
                               "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else
        {
            MessageBox.Show($"Save failed: {ex.Message}", "Error");
        }
    }
}
```

### Example 7: Conditional Save (Smart Decisions)

```csharp
private async Task<bool> SmartSave()
{
    var diagnostics = hexEditor.GetDiagnostics();

    // No changes?
    if (!hexEditor.HasChanges)
    {
        MessageBox.Show("No changes to save", "Info");
        return false;
    }

    // Large number of changes?
    int totalChanges = diagnostics.ModificationCount +
                      diagnostics.InsertionCount +
                      diagnostics.DeletionCount;

    if (totalChanges > 10000)
    {
        var result = MessageBox.Show(
            $"You have {totalChanges:N0} changes. Save may take time. Continue?",
            "Many Changes", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return false;
    }

    // File size will change significantly?
    long sizeChange = diagnostics.InsertionCount - diagnostics.DeletionCount;
    if (Math.Abs(sizeChange) > diagnostics.FileSize * 0.1) // >10% change
    {
        var newSize = diagnostics.FileSize + sizeChange;
        var message = $"File size will change:\n" +
                     $"Current: {FormatSize(diagnostics.FileSize)}\n" +
                     $"New: {FormatSize(newSize)}\n\n" +
                     $"Continue?";

        var result = MessageBox.Show(message, "Size Change",
                                    MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return false;
    }

    // Perform save
    try
    {
        if (diagnostics.HasInsertions || diagnostics.HasDeletions)
        {
            // Full rebuild - use async with progress
            var progress = new Progress<double>(p => progressBar.Value = p);
            progressBar.Visibility = Visibility.Visible;

            await hexEditor.SaveAsync(progress);

            progressBar.Visibility = Visibility.Collapsed;
        }
        else
        {
            // Fast path - synchronous is fine
            hexEditor.Save();
        }

        MessageBox.Show("File saved successfully!", "Success");
        return true;
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Error: {ex.Message}", "Save Failed");
        return false;
    }
}

private string FormatSize(long bytes)
{
    string[] sizes = { "bytes", "KB", "MB", "GB" };
    double size = bytes;
    int order = 0;

    while (size >= 1024 && order < sizes.Length - 1)
    {
        order++;
        size /= 1024;
    }

    return $"{size:F2} {sizes[order]}";
}
```

---

## 💡 Use Cases

### 1. Patch File Generator

```csharp
// Create patch file
hexEditor.FileName = "original.bin";

// Apply modifications
hexEditor.ModifyByte(0xFF, 0x1000);
hexEditor.ModifyByte(0xAA, 0x2000);

// Save as patch
hexEditor.SaveAs("patched.bin");

// Export patch data
var diagnostics = hexEditor.GetDiagnostics();
File.WriteAllText("patch.txt",
    $"Modified {diagnostics.ModificationCount} bytes\n" +
    $"Positions: 0x1000, 0x2000");
```

### 2. Binary File Repair Tool

```csharp
// Open corrupted file
hexEditor.FileName = "corrupted.dat";

// Fix known corruption (bad header)
byte[] fixedHeader = { 0x4D, 0x5A, 0x90, 0x00 }; // MZ header
hexEditor.ModifyBytes(0, fixedHeader);

// Validate checksum at end
long checksumPos = hexEditor.Length - 4;
uint newChecksum = CalculateChecksum(hexEditor);
byte[] checksumBytes = BitConverter.GetBytes(newChecksum);
hexEditor.ModifyBytes(checksumPos, checksumBytes);

// Save repaired file
hexEditor.SaveAs("repaired.dat");
Console.WriteLine("File repaired successfully");
```

### 3. ROM Patcher (Game Modding)

```csharp
// Open ROM file
hexEditor.FileName = "game.rom";

// Find text to replace
var searchText = Encoding.ASCII.GetBytes("GAME OVER");
long position = hexEditor.FindFirst(searchText);

if (position >= 0)
{
    // Replace with mod text
    var newText = Encoding.ASCII.GetBytes("YOU WIN!");
    hexEditor.ModifyBytes(position, newText);

    // Save modded ROM
    hexEditor.SaveAs("game_modded.rom");

    MessageBox.Show("ROM patched successfully!", "Success");
}
```

---

## ⚡ Performance Characteristics

### Save Algorithm Selection

The hex editor automatically chooses the optimal save strategy:

| Edit Type | Algorithm | Performance |
|-----------|-----------|-------------|
| **Modifications only** | Fast path | ⚡⚡⚡ **100x faster** |
| **Has insertions/deletions** | Full rebuild | ⚡ Standard speed |

### Benchmarks

| File Size | Modifications | Fast Path | Full Rebuild |
|-----------|--------------|-----------|--------------|
| 1 MB | 10 mods | <1ms | ~100ms |
| 1 MB | 10 mods + 1 insertion | N/A | ~100ms |
| 10 MB | 100 mods | ~5ms | ~500ms |
| 10 MB | 100 mods + 1 insertion | N/A | ~500ms |
| 100 MB | 1,000 mods | ~50ms | ~5000ms |
| 100 MB | 1,000 mods + 1 insertion | N/A | ~5000ms |

**Key Insights**:
- ✅ Fast path time depends on **modification count**, not file size
- ⚠️ Single insertion/deletion forces **full rebuild**
- 📝 Full rebuild time is proportional to **file size**

---

## ⚠️ Important Notes

### File Access

- ✅ Requires **write permission** to save
- ✅ Use `CanWrite` property to check if save is possible
- ⚠️ Read-only files will throw `UnauthorizedAccessException`

### Data Integrity

- ✅ **Atomic writes**: Temp file created, then replaces original
- ✅ **No data loss**: Original preserved until save completes
- ✅ **Automatic rollback**: On error, original file unchanged

### Thread Safety

- ❌ **Not thread-safe**: Must save on UI thread
- ✅ Use `SaveAsync()` for responsive UI
- ✅ Progress reporting available via `IProgress<double>`

### Disk Space

For full rebuild (with insertions/deletions):
- Requires **2x file size** temporary disk space
- Temp file created in same directory as original
- Temp file automatically deleted after save

---

## 🔗 See Also

- **[Open()](open.md)** - Open file for editing
- **[Close()](close.md)** - Close current file
- **[HasChanges](haschanges.md)** - Check if file is modified
- **[GetDiagnostics()](../diagnostics/getdiagnostics.md)** - Get edit statistics

---

**Last Updated**: 2026-02-19
**Version**: V2.0
