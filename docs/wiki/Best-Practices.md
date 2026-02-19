# Best Practices

Performance tips, optimization strategies, and recommended patterns for using WPF HexEditor V2.

---

## 📋 Overview

This guide covers **proven strategies** for getting the best performance, reliability, and user experience from WPF HexEditor V2.

**Topics**:
- ⚡ Performance optimization
- 💾 Memory management
- 🔍 Efficient searching
- 💽 File operations
- 🎨 UI responsiveness
- 🐛 Error handling
- 🔧 Common patterns

---

## ⚡ Performance Optimization

### Use Batch Operations

**❌ Slow: Individual operations**
```csharp
// 1000 separate UI updates!
for (int i = 0; i < 1000; i++)
{
    hexEditor.ModifyByte(0xFF, i);
}
// Time: ~3000ms
```

**✅ Fast: Batch operations**
```csharp
// Single UI update
hexEditor.BeginBatch();
for (int i = 0; i < 1000; i++)
{
    hexEditor.ModifyByte(0xFF, i);
}
hexEditor.EndBatch();
// Time: ~100ms (30x faster!)
```

**Rule**: Whenever making **more than 10 edits**, use BeginBatch/EndBatch.

---

### Use ModifyBytes Instead of Loop

**❌ Slow: Loop with ModifyByte**
```csharp
for (int i = 0; i < 1000; i++)
{
    hexEditor.ModifyByte(0xFF, 1000 + i);
}
// Time: ~2000ms
```

**✅ Fast: Single ModifyBytes call**
```csharp
byte[] values = Enumerable.Repeat((byte)0xFF, 1000).ToArray();
hexEditor.ModifyBytes(1000, values);
// Time: ~20ms (100x faster!)
```

**Rule**: For **contiguous** byte modifications, use ModifyBytes().

---

### Use CountOccurrences for Large Result Sets

**❌ Memory-hungry: FindAll with millions of results**
```csharp
// 10 million null bytes → ~80 MB RAM!
List<long> positions = hexEditor.FindAll(new byte[] { 0x00 });
```

**✅ Memory-efficient: CountOccurrences**
```csharp
// Just count them → ~0 MB RAM
int count = hexEditor.CountOccurrences(new byte[] { 0x00 });
Console.WriteLine($"Found {count} null bytes");
```

**Rule**: If you only need the **count** (not positions), use CountOccurrences().

---

### Cache Frequently Read Bytes

**❌ Slow: Read same bytes repeatedly**
```csharp
for (int i = 0; i < 100; i++)
{
    byte b = hexEditor.GetByte(1000);  // Read file 100 times!
    ProcessByte(b);
}
```

**✅ Fast: Cache bytes in memory**
```csharp
byte[] cache = hexEditor.GetBytes(1000, 100);  // Read once
for (int i = 0; i < 100; i++)
{
    ProcessByte(cache[i]);  // From RAM
}
```

**Rule**: If accessing same bytes **multiple times**, cache them first.

---

### Disable UI Updates During Long Operations

**✅ Good pattern: Suspend rendering**
```csharp
// Disable UI updates
hexEditor.BeginBatch();
progressBar.Visibility = Visibility.Visible;

try
{
    for (long i = 0; i < largeCount; i++)
    {
        hexEditor.ModifyByte(0xFF, i);

        // Update progress periodically (not every byte!)
        if (i % 10000 == 0)
        {
            progressBar.Value = i * 100 / largeCount;
            Application.Current.Dispatcher.Invoke(
                DispatcherPriority.Background,
                new Action(() => { }));  // Allow UI refresh
        }
    }
}
finally
{
    hexEditor.EndBatch();  // Single UI update at end
    progressBar.Visibility = Visibility.Collapsed;
}
```

---

## 💾 Memory Management

### Close Files When Done

**✅ Always close files explicitly**
```csharp
// Open file
hexEditor.FileName = "data.bin";

// ... work with file ...

// Close when done
hexEditor.Close();  // Releases file handle & memory
```

**Rule**: Call Close() to free resources, especially for large files.

---

### Dispose Properly in Using Blocks

**✅ Good pattern: Using statement**
```csharp
public async Task ProcessFile(string fileName)
{
    using (var stream = File.OpenRead(fileName))
    {
        hexEditor.OpenStream(stream);

        // Process file...

        // Stream automatically closed/disposed
    }
}
```

---

### Limit Selection Size for Large Files

**❌ Bad: Select entire 1 GB file**
```csharp
hexEditor.SelectAll();  // 1 GB in memory!
byte[] selection = hexEditor.GetSelection();  // OutOfMemoryException!
```

**✅ Good: Process in chunks**
```csharp
long chunkSize = 1_000_000;  // 1 MB chunks

for (long pos = 0; pos < hexEditor.Length; pos += chunkSize)
{
    int length = (int)Math.Min(chunkSize, hexEditor.Length - pos);
    byte[] chunk = hexEditor.GetBytes(pos, length);

    ProcessChunk(chunk);
}
```

**Rule**: Process large files in **chunks** (1-10 MB) to avoid memory issues.

---

### Clear Undo History for Large Edits

**✅ Free memory after bulk operations**
```csharp
// Perform large batch edit
hexEditor.BeginBatch();
// ... 1 million edits ...
hexEditor.EndBatch();

// Save changes
hexEditor.Save();

// Clear undo history (frees memory)
hexEditor.ClearUndoHistory();

Console.WriteLine("Undo history cleared - memory freed");
```

**Rule**: After saving, **clear undo history** if you don't need undo capability.

---

## 🔍 Efficient Searching

### Reuse Search Patterns

**✅ Search cache automatically accelerates**
```csharp
byte[] pattern = { 0xDE, 0xAD, 0xBE, 0xEF };

// First search: 50ms (cold cache)
long pos1 = hexEditor.FindFirst(pattern);

// Second search: 0.5ms (hot cache - 100x faster!)
long pos2 = hexEditor.FindFirst(pattern);
```

**Rule**: V2 **automatically caches** recent searches. Reuse patterns when possible.

---

### Search in Specific Ranges

**❌ Slow: Search entire file**
```csharp
// Search all 10 GB!
long pos = hexEditor.FindFirst(pattern);
```

**✅ Fast: Search known region**
```csharp
// Search only file header (first 1 MB)
long searchEnd = Math.Min(hexEditor.Length, 1_048_576);
long pos = hexEditor.FindFirst(pattern, 0, searchEnd);
```

**Rule**: If you know the **approximate location**, limit search range.

---

### Use Parallel Search for Large Files

**✅ Automatic in V2**
```csharp
// For files > 10 MB, V2 automatically uses parallel search
// (all CPU cores utilized)
List<long> positions = hexEditor.FindAll(pattern);
// 8-core CPU: ~7x faster than single-threaded!
```

**Rule**: V2 **automatically parallelizes** for large files. No code changes needed.

---

### Search Incrementally for UI Responsiveness

**✅ Good pattern: Async search with cancellation**
```csharp
private CancellationTokenSource _searchCts;

private async Task SearchIncrementallyAsync(byte[] pattern)
{
    _searchCts = new CancellationTokenSource();

    await Task.Run(() =>
    {
        const long chunkSize = 10_000_000;  // 10 MB
        long position = 0;

        while (position < hexEditor.Length)
        {
            // Check cancellation
            if (_searchCts.Token.IsCancellationRequested)
                return;

            // Search chunk
            long chunkEnd = Math.Min(position + chunkSize, hexEditor.Length);
            long match = hexEditor.FindFirst(pattern, position, chunkEnd);

            if (match >= 0)
            {
                Dispatcher.Invoke(() => DisplayResult(match));
            }

            // Update progress
            int progress = (int)((chunkEnd * 100) / hexEditor.Length);
            Dispatcher.Invoke(() => progressBar.Value = progress);

            position = chunkEnd;
        }
    }, _searchCts.Token);
}

private void CancelSearch()
{
    _searchCts?.Cancel();
}
```

---

## 💽 File Operations

### Use Async for Large Files

**❌ Blocks UI thread**
```csharp
hexEditor.FileName = "large.bin";  // UI freezes!
```

**✅ Async with progress**
```csharp
private async void OpenLargeFileAsync()
{
    progressBar.Visibility = Visibility.Visible;

    var progress = new Progress<double>(p =>
    {
        progressBar.Value = p;
        statusLabel.Text = $"Opening: {p:F1}%";
    });

    try
    {
        await hexEditor.OpenAsync("large.bin", progress);
        MessageBox.Show("File opened successfully");
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Error: {ex.Message}");
    }
    finally
    {
        progressBar.Visibility = Visibility.Collapsed;
    }
}
```

**Rule**: Files **> 100 MB** should always use OpenAsync().

---

### Check File Properties Before Opening

**✅ Validate first**
```csharp
private bool SafeOpenFile(string fileName)
{
    // Check existence
    if (!File.Exists(fileName))
    {
        MessageBox.Show("File not found", "Error");
        return false;
    }

    // Check size
    var fileInfo = new FileInfo(fileName);

    if (fileInfo.Length > 2_147_483_648)  // > 2 GB
    {
        var result = MessageBox.Show(
            $"File is {fileInfo.Length / 1024 / 1024} MB. Continue?",
            "Large File",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return false;
    }

    // Check read permissions
    try
    {
        using (File.OpenRead(fileName)) { }
    }
    catch (UnauthorizedAccessException)
    {
        MessageBox.Show("Access denied", "Error");
        return false;
    }

    // Open file
    hexEditor.FileName = fileName;
    return true;
}
```

---

### Save with Backup

**✅ Always create backup**
```csharp
private bool SaveWithBackup()
{
    if (!hexEditor.HasChanges)
    {
        MessageBox.Show("No changes to save");
        return false;
    }

    string fileName = hexEditor.FileName;
    string backupFile = fileName + ".bak";

    try
    {
        // Create backup
        if (File.Exists(fileName))
        {
            File.Copy(fileName, backupFile, overwrite: true);
            Console.WriteLine($"Backup created: {backupFile}");
        }

        // Save changes
        hexEditor.Save();

        MessageBox.Show("File saved successfully", "Success");
        return true;
    }
    catch (Exception ex)
    {
        // Restore from backup on error
        if (File.Exists(backupFile))
        {
            File.Copy(backupFile, fileName, overwrite: true);
            Console.WriteLine("Restored from backup");
        }

        MessageBox.Show($"Error saving: {ex.Message}", "Error");
        return false;
    }
}
```

---

### Handle Save Errors Gracefully

**✅ Comprehensive error handling**
```csharp
private async Task<bool> SaveWithErrorHandlingAsync()
{
    try
    {
        progressBar.Visibility = Visibility.Visible;

        var progress = new Progress<double>(p => progressBar.Value = p);

        await hexEditor.SaveAsync(progress);

        MessageBox.Show("File saved successfully", "Success");
        return true;
    }
    catch (UnauthorizedAccessException)
    {
        MessageBox.Show("Access denied. Try running as Administrator.", "Error");
        return false;
    }
    catch (IOException ex)
    {
        MessageBox.Show($"I/O error: {ex.Message}\nDisk full?", "Error");
        return false;
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Unexpected error: {ex.Message}", "Error");
        return false;
    }
    finally
    {
        progressBar.Visibility = Visibility.Collapsed;
    }
}
```

---

## 🎨 UI Responsiveness

### Show Progress for Long Operations

**✅ Keep user informed**
```csharp
private async Task ProcessLargeFileAsync()
{
    progressBar.Visibility = Visibility.Visible;
    progressBar.Maximum = hexEditor.Length;

    await Task.Run(() =>
    {
        for (long i = 0; i < hexEditor.Length; i++)
        {
            byte b = hexEditor.GetByte(i);
            ProcessByte(b);

            // Update every 10,000 bytes
            if (i % 10000 == 0)
            {
                Dispatcher.Invoke(() =>
                {
                    progressBar.Value = i;
                    statusLabel.Text = $"Processing: {i * 100 / hexEditor.Length}%";
                });
            }
        }
    });

    progressBar.Visibility = Visibility.Collapsed;
    MessageBox.Show("Processing complete");
}
```

---

### Disable Controls During Operations

**✅ Prevent concurrent operations**
```csharp
private async Task PerformOperationAsync()
{
    // Disable UI
    openButton.IsEnabled = false;
    saveButton.IsEnabled = false;
    searchButton.IsEnabled = false;
    hexEditor.IsEnabled = false;

    try
    {
        await LongRunningOperationAsync();
    }
    finally
    {
        // Re-enable UI
        openButton.IsEnabled = true;
        saveButton.IsEnabled = true;
        searchButton.IsEnabled = true;
        hexEditor.IsEnabled = true;
    }
}
```

---

### Update Status Bar Efficiently

**❌ Bad: Update every event**
```csharp
hexEditor.ByteModified += (s, e) =>
{
    statusLabel.Text = $"Modified: 0x{e.Position:X}";  // 1000 updates/sec!
};
```

**✅ Good: Throttle updates**
```csharp
private DispatcherTimer _statusUpdateTimer;
private string _pendingStatusUpdate;

public MainWindow()
{
    InitializeComponent();

    // Update status every 100ms max
    _statusUpdateTimer = new DispatcherTimer
    {
        Interval = TimeSpan.FromMilliseconds(100)
    };
    _statusUpdateTimer.Tick += (s, e) =>
    {
        if (_pendingStatusUpdate != null)
        {
            statusLabel.Text = _pendingStatusUpdate;
            _pendingStatusUpdate = null;
        }
    };
    _statusUpdateTimer.Start();

    hexEditor.ByteModified += (s, e) =>
    {
        _pendingStatusUpdate = $"Modified: 0x{e.Position:X}";
    };
}
```

---

## 🐛 Error Handling

### Validate User Input

**✅ Always validate**
```csharp
private byte[] ParseHexInput(string input)
{
    if (string.IsNullOrWhiteSpace(input))
    {
        throw new ArgumentException("Hex input cannot be empty");
    }

    // Remove spaces, dashes
    input = input.Replace(" ", "").Replace("-", "");

    // Validate hex characters
    if (!System.Text.RegularExpressions.Regex.IsMatch(input, "^[0-9A-Fa-f]+$"))
    {
        throw new FormatException("Invalid hex characters");
    }

    // Validate even length
    if (input.Length % 2 != 0)
    {
        throw new FormatException("Hex string must have even length");
    }

    // Parse bytes
    byte[] bytes = new byte[input.Length / 2];
    for (int i = 0; i < bytes.Length; i++)
    {
        bytes[i] = Convert.ToByte(input.Substring(i * 2, 2), 16);
    }

    return bytes;
}
```

---

### Check Conditions Before Operations

**✅ Validate state**
```csharp
private void SaveButton_Click(object sender, RoutedEventArgs e)
{
    // Check if file is open
    if (hexEditor.FileName == null)
    {
        MessageBox.Show("No file opened", "Error");
        return;
    }

    // Check if has changes
    if (!hexEditor.HasChanges)
    {
        MessageBox.Show("No changes to save", "Info");
        return;
    }

    // Check if read-only
    if (hexEditor.ReadOnlyMode)
    {
        MessageBox.Show("File is read-only", "Error");
        return;
    }

    // Perform save
    SaveWithBackup();
}
```

---

### Use Try-Catch for Critical Operations

**✅ Catch and handle errors**
```csharp
private void ModifyByteWithValidation(long position, byte value)
{
    try
    {
        // Validate position
        if (position < 0 || position >= hexEditor.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(position),
                $"Position {position} is out of range");
        }

        // Modify
        hexEditor.ModifyByte(value, position);

        // Log success
        Console.WriteLine($"Modified 0x{position:X} → 0x{value:X2}");
    }
    catch (ArgumentOutOfRangeException ex)
    {
        MessageBox.Show(ex.Message, "Invalid Position");
    }
    catch (InvalidOperationException ex)
    {
        MessageBox.Show($"Cannot modify: {ex.Message}", "Error");
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Unexpected error: {ex.Message}", "Error");
        // Log for debugging
        Console.WriteLine($"Exception: {ex}");
    }
}
```

---

## 🔧 Common Patterns

### Pattern: Safe Edit with Validation

```csharp
private bool SafeEdit(long position, byte value)
{
    // 1. Validate position
    if (position < 0 || position >= hexEditor.Length)
    {
        MessageBox.Show("Position out of range");
        return false;
    }

    // 2. Check writable
    if (hexEditor.ReadOnlyMode || !hexEditor.CanWrite)
    {
        MessageBox.Show("File is read-only");
        return false;
    }

    // 3. Get old value for logging
    byte oldValue = hexEditor.GetByte(position);

    // 4. Perform edit
    try
    {
        hexEditor.ModifyByte(value, position);
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Error: {ex.Message}");
        return false;
    }

    // 5. Log change
    Console.WriteLine($"0x{position:X}: 0x{oldValue:X2} → 0x{value:X2}");

    return true;
}
```

---

### Pattern: Batch Operation with Progress

```csharp
private void FillRangeWithProgress(long start, long length, byte value)
{
    progressBar.Visibility = Visibility.Visible;
    progressBar.Maximum = length;

    hexEditor.BeginBatch();

    try
    {
        for (long i = 0; i < length; i++)
        {
            hexEditor.ModifyByte(value, start + i);

            // Update every 1000 bytes
            if (i % 1000 == 0)
            {
                progressBar.Value = i;
                Application.Current.Dispatcher.Invoke(
                    DispatcherPriority.Background,
                    new Action(() => { }));
            }
        }
    }
    finally
    {
        hexEditor.EndBatch();
        progressBar.Visibility = Visibility.Collapsed;
    }
}
```

---

### Pattern: Search with Results Display

```csharp
private void SearchAndDisplay(byte[] pattern)
{
    var stopwatch = Stopwatch.StartNew();

    // Search
    List<long> positions = hexEditor.FindAll(pattern);

    stopwatch.Stop();

    // Display results
    resultsListBox.Items.Clear();

    if (positions.Count == 0)
    {
        MessageBox.Show("Pattern not found");
        return;
    }

    // Add to list (limit to first 1000)
    foreach (var pos in positions.Take(1000))
    {
        resultsListBox.Items.Add($"0x{pos:X8}");
    }

    // Show summary
    statusLabel.Text = $"Found {positions.Count} matches in {stopwatch.ElapsedMilliseconds}ms";

    if (positions.Count > 1000)
    {
        statusLabel.Text += " (showing first 1000)";
    }

    // Navigate to first
    hexEditor.SetPosition(positions[0]);
}
```

---

### Pattern: Async Operation with Cancellation

```csharp
private CancellationTokenSource _cts;

private async Task ProcessWithCancellationAsync()
{
    _cts = new CancellationTokenSource();
    cancelButton.IsEnabled = true;

    try
    {
        await Task.Run(() =>
        {
            for (long i = 0; i < hexEditor.Length; i++)
            {
                // Check cancellation
                if (_cts.Token.IsCancellationRequested)
                {
                    Dispatcher.Invoke(() =>
                        MessageBox.Show("Operation cancelled"));
                    return;
                }

                // Process byte
                byte b = hexEditor.GetByte(i);
                ProcessByte(b);

                // Update progress
                if (i % 10000 == 0)
                {
                    Dispatcher.Invoke(() =>
                        progressBar.Value = i * 100 / hexEditor.Length);
                }
            }
        }, _cts.Token);

        MessageBox.Show("Processing complete");
    }
    catch (OperationCanceledException)
    {
        MessageBox.Show("Operation cancelled");
    }
    finally
    {
        cancelButton.IsEnabled = false;
    }
}

private void CancelButton_Click(object sender, RoutedEventArgs e)
{
    _cts?.Cancel();
}
```

---

## 📊 Performance Checklist

### Before Releasing Your Application

- [ ] **Batch operations**: Use BeginBatch/EndBatch for bulk edits
- [ ] **Async file operations**: Use OpenAsync/SaveAsync for files > 100 MB
- [ ] **Progress reporting**: Show progress for operations > 2 seconds
- [ ] **Error handling**: Catch and handle all exceptions gracefully
- [ ] **Memory management**: Close files when done, clear undo history after save
- [ ] **Input validation**: Validate all user input before processing
- [ ] **Cancel support**: Allow users to cancel long operations
- [ ] **Status updates**: Keep user informed of current operation
- [ ] **Disable UI**: Prevent concurrent operations
- [ ] **Backup files**: Always create backup before saving

---

## 🔗 Next Steps

### Learn More

- **[Architecture Overview](Architecture-Overview)** - Understand how V2 works
- **[API Reference](API-Reference)** - Complete API documentation
- **[Sample Applications](Sample-Applications)** - Real-world examples
- **[Troubleshooting](Troubleshooting)** - Common issues and solutions

---

<div align="center">
  <br/>
  <p>
    <b>⚡ Optimize your hex editor app!</b><br/>
    Follow these best practices for maximum performance.
  </p>
  <br/>
  <p>
    👉 <a href="Architecture-Overview"><b>Architecture</b></a> •
    <a href="Sample-Applications"><b>Examples</b></a> •
    <a href="API-Reference"><b>API Reference</b></a>
  </p>
</div>
