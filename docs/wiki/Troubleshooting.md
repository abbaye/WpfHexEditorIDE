# Troubleshooting

Common problems and their solutions for WPF HexEditor V2.

---

## 📋 Overview

This guide helps you diagnose and fix common issues with WPF HexEditor V2.

**Quick Links**:
- [Installation Issues](#-installation-issues)
- [File Operation Errors](#-file-operation-errors)
- [Performance Problems](#-performance-problems)
- [Display & Rendering Issues](#-display--rendering-issues)
- [Edit Operation Failures](#-edit-operation-failures)
- [Search Problems](#-search-problems)
- [Memory Issues](#-memory-issues)

---

## 🔧 Installation Issues

### Issue: "Type 'HexEditor' was not found"

**Symptoms**: Red squiggle under `<hex:HexEditor>` in XAML.

**Causes**:
1. NuGet package not installed
2. Wrong namespace
3. Build error

**Solution 1**: Verify NuGet package installed
```bash
# Check if package exists
dotnet list package

# If missing, install
dotnet add package WPFHexaEditor
```

**Solution 2**: Check namespace in XAML
```xml
<!-- ❌ Wrong -->
<Window xmlns:hex="clr-namespace:WpfHexEditor">

<!-- ✅ Correct -->
<Window xmlns:hex="clr-namespace:WpfHexaEditor;assembly=WPFHexaEditor">
```

**Solution 3**: Clean and rebuild
```bash
dotnet clean
dotnet build
```

---

### Issue: "Could not load file or assembly"

**Symptoms**: Exception when running application.

**Error Message**:
```
System.IO.FileNotFoundException: Could not load file or assembly
'WPFHexaEditor, Version=...'
```

**Solution 1**: Check target framework
```xml
<!-- For .NET 8.0 -->
<TargetFramework>net8.0-windows</TargetFramework>

<!-- For .NET Framework 4.8 -->
<TargetFramework>net48</TargetFramework>
```

**Solution 2**: Enable WPF support
```xml
<PropertyGroup>
  <UseWPF>true</UseWPF>
</PropertyGroup>
```

**Solution 3**: Restore packages
```bash
dotnet restore
dotnet build
```

---

### Issue: IntelliSense not working

**Symptoms**: No autocomplete for HexEditor properties/methods.

**Solution 1**: Rebuild solution
```bash
dotnet clean
dotnet build
```

**Solution 2**: Restart Visual Studio

**Solution 3**: Delete .vs folder
```bash
# Close Visual Studio first
rmdir /s .vs
# Reopen Visual Studio
```

---

## 💾 File Operation Errors

### Issue: "Access to the path is denied"

**Symptoms**: Exception when opening or saving file.

**Error Message**:
```
System.UnauthorizedAccessException: Access to the path
'C:\Program Files\data.bin' is denied.
```

**Solution 1**: Run as Administrator
- Right-click application → "Run as administrator"

**Solution 2**: Check file permissions
```csharp
var fileInfo = new FileInfo(fileName);

if (fileInfo.IsReadOnly)
{
    MessageBox.Show("File is read-only. Cannot save.");
    return;
}
```

**Solution 3**: Save to different location
```csharp
// Save to user's temp directory instead
string tempPath = Path.Combine(Path.GetTempPath(), "output.bin");
hexEditor.SaveAs(tempPath);
```

---

### Issue: "File is being used by another process"

**Symptoms**: Cannot open or save file.

**Error Message**:
```
System.IO.IOException: The process cannot access the file
because it is being used by another process.
```

**Solution 1**: Close other applications
- Check Task Manager for processes using the file

**Solution 2**: Open in read-only mode
```csharp
hexEditor.ReadOnlyMode = true;
hexEditor.FileName = "locked.bin";
```

**Solution 3**: Copy file first
```csharp
string tempFile = Path.GetTempFileName();
File.Copy(lockedFile, tempFile, overwrite: true);

hexEditor.FileName = tempFile;
```

---

### Issue: "Out of memory" when opening large file

**Symptoms**: Exception or crash when opening large files.

**Error Message**:
```
System.OutOfMemoryException: Exception of type
'System.OutOfMemoryException' was thrown.
```

**Solution 1**: Use 64-bit build
```xml
<PropertyGroup>
  <PlatformTarget>x64</PlatformTarget>
</PropertyGroup>
```

**Solution 2**: Use async open with progress
```csharp
private async void OpenLargeFileAsync()
{
    var progress = new Progress<double>(p => progressBar.Value = p);

    try
    {
        await hexEditor.OpenAsync(largeFile, progress);
    }
    catch (OutOfMemoryException)
    {
        MessageBox.Show(
            "File too large for available memory.\n" +
            "Try closing other applications.",
            "Out of Memory");
    }
}
```

**Solution 3**: Increase available memory
- Close other applications
- Increase virtual memory (pagefile)
- Use machine with more RAM

---

### Issue: File saves but changes are lost

**Symptoms**: Save completes but file unchanged.

**Common Causes**:
1. Forgot to call Save()
2. ReadOnlyMode enabled
3. Exception during save (silent failure)

**Solution 1**: Verify Save() is called
```csharp
// Make edits
hexEditor.ModifyByte(0xFF, 0x100);

// MUST call Save() to persist changes!
hexEditor.Save();
```

**Solution 2**: Check HasChanges property
```csharp
if (hexEditor.HasChanges)
{
    hexEditor.Save();
    MessageBox.Show("Changes saved");
}
else
{
    MessageBox.Show("No changes to save");
}
```

**Solution 3**: Add error handling
```csharp
try
{
    hexEditor.Save();
    Console.WriteLine("Save successful");
}
catch (Exception ex)
{
    MessageBox.Show($"Save failed: {ex.Message}", "Error");
}
```

---

## ⚡ Performance Problems

### Issue: Slow rendering / UI freezes

**Symptoms**: UI freezes or becomes unresponsive.

**Solution 1**: Use batch operations
```csharp
// ❌ Slow
for (int i = 0; i < 10000; i++)
{
    hexEditor.ModifyByte(0xFF, i);
}

// ✅ Fast
hexEditor.BeginBatch();
for (int i = 0; i < 10000; i++)
{
    hexEditor.ModifyByte(0xFF, i);
}
hexEditor.EndBatch();
```

**Solution 2**: Use async operations
```csharp
await Task.Run(() =>
{
    // Long operation here
    for (long i = 0; i < hexEditor.Length; i++)
    {
        ProcessByte(hexEditor.GetByte(i));
    }
});
```

**Solution 3**: Process in chunks with progress
```csharp
const long chunkSize = 100_000;

for (long pos = 0; pos < hexEditor.Length; pos += chunkSize)
{
    // Process chunk
    long length = Math.Min(chunkSize, hexEditor.Length - pos);
    byte[] chunk = hexEditor.GetBytes(pos, (int)length);

    ProcessChunk(chunk);

    // Allow UI to update
    await Task.Delay(1);
}
```

---

### Issue: Search is very slow

**Symptoms**: FindFirst/FindAll takes minutes on large files.

**Solution 1**: Verify V2 is being used
```csharp
// Check version
Console.WriteLine($"HexEditor version: {hexEditor.Version}");
// Should be "2.x.x"

// V2 search is 10-100x faster than V1
```

**Solution 2**: Limit search range
```csharp
// ❌ Slow: Search entire 10 GB file
long pos = hexEditor.FindFirst(pattern);

// ✅ Fast: Search first 100 MB only
long searchEnd = Math.Min(hexEditor.Length, 100_000_000);
long pos = hexEditor.FindFirst(pattern, 0, searchEnd);
```

**Solution 3**: Use CountOccurrences instead of FindAll
```csharp
// ❌ Slow: Store 1 million positions
List<long> positions = hexEditor.FindAll(pattern);  // ~8 MB RAM

// ✅ Fast: Just count
int count = hexEditor.CountOccurrences(pattern);  // ~0 MB RAM
```

---

### Issue: Save takes forever

**Symptoms**: Save operation takes minutes or hours.

**Solution 1**: Check if fast path is being used
```csharp
// Fast path (modifications only): 100x faster
// Full rebuild (with insertions/deletions): slower

// Tip: Prefer ModifyByte over InsertByte+DeleteByte when possible
```

**Solution 2**: Use async save with progress
```csharp
private async Task SaveWithProgressAsync()
{
    progressBar.Visibility = Visibility.Visible;

    var progress = new Progress<double>(p =>
    {
        progressBar.Value = p;
        statusLabel.Text = $"Saving: {p:F1}%";
    });

    await hexEditor.SaveAsync(progress);

    progressBar.Visibility = Visibility.Collapsed;
}
```

**Solution 3**: Clear undo history before saving
```csharp
// If you don't need undo after save:
hexEditor.ClearUndoHistory();  // Frees memory
hexEditor.Save();  // Faster save
```

---

## 🎨 Display & Rendering Issues

### Issue: Control appears but is blank

**Symptoms**: HexEditor renders but shows no data.

**Solution 1**: Verify file is opened
```csharp
// Check if file loaded
if (hexEditor.FileName == null)
{
    MessageBox.Show("No file opened");
    return;
}

Console.WriteLine($"File: {hexEditor.FileName}");
Console.WriteLine($"Length: {hexEditor.Length} bytes");
```

**Solution 2**: Check file exists
```csharp
string fileName = "data.bin";

if (!File.Exists(fileName))
{
    MessageBox.Show($"File not found: {fileName}");
    return;
}

hexEditor.FileName = fileName;
```

**Solution 3**: Verify file has content
```csharp
if (hexEditor.Length == 0)
{
    MessageBox.Show("File is empty (0 bytes)");
}
```

---

### Issue: Colors not displaying correctly

**Symptoms**: Modified/inserted bytes not colored.

**Solution 1**: Check color properties
```xml
<hex:HexEditor ModifiedByteColor="Red"
               InsertedByteColor="Green"
               DeletedByteColor="Gray" />
```

**Solution 2**: Verify edits are tracked
```csharp
hexEditor.ModifyByte(0xFF, 0x100);

// Check if tracked
Console.WriteLine($"Has changes: {hexEditor.HasChanges}");
```

---

### Issue: Text rendering issues (garbled characters)

**Symptoms**: Hex or ASCII text appears corrupted.

**Solution 1**: Set font explicitly
```xml
<hex:HexEditor FontFamily="Consolas"
               FontSize="12" />
```

**Solution 2**: Check DPI settings
```csharp
// Ensure DPI awareness
[assembly: DisableDpiAwareness]  // or use DpiAware
```

---

## ✏️ Edit Operation Failures

### Issue: "Insert mode not allowed"

**Symptoms**: InsertByte() throws exception.

**Error Message**:
```
System.InvalidOperationException: Insert mode is not enabled
```

**Solution**: Enable insert mode
```csharp
hexEditor.AllowInsertMode = true;

// Now insertions work
hexEditor.InsertByte(0xFF, 0x100);
```

---

### Issue: Cannot modify read-only file

**Symptoms**: ModifyByte() doesn't work.

**Solution 1**: Check ReadOnlyMode
```csharp
if (hexEditor.ReadOnlyMode)
{
    MessageBox.Show("File is read-only");
    return;
}

hexEditor.ModifyByte(0xFF, 0x100);
```

**Solution 2**: Check CanWrite property
```csharp
if (!hexEditor.CanWrite)
{
    MessageBox.Show("Cannot write to file");
    return;
}
```

---

### Issue: Undo not working

**Symptoms**: Undo() has no effect.

**Solution 1**: Check CanUndo
```csharp
if (hexEditor.CanUndo)
{
    hexEditor.Undo();
}
else
{
    MessageBox.Show("Nothing to undo");
}
```

**Solution 2**: Verify undo history not cleared
```csharp
// Don't clear undo if you need undo capability
// hexEditor.ClearUndoHistory();  // ← Don't call this!
```

**Solution 3**: Check undo depth
```csharp
int undoDepth = hexEditor.UndoDepth;
Console.WriteLine($"Undo stack depth: {undoDepth}");

if (undoDepth == 0)
{
    Console.WriteLine("Undo stack is empty");
}
```

---

### Issue: LIFO insertion order confusing

**Symptoms**: Multiple insertions at same position appear in reverse order.

**Expected Behavior**: LIFO (Last-In-First-Out) is by design.

**Example**:
```csharp
// Insert A, B, C at position 100
hexEditor.InsertByte(0x41, 100);  // A
hexEditor.InsertByte(0x42, 100);  // B
hexEditor.InsertByte(0x43, 100);  // C

// Result: C B A (reversed!)
```

**Solution**: Use InsertBytes for correct order
```csharp
// ✅ Correct order
byte[] data = { 0x41, 0x42, 0x43 };  // A B C
hexEditor.InsertBytes(100, data);

// Result: A B C (correct!)
```

---

## 🔍 Search Problems

### Issue: Pattern not found (but it exists)

**Symptoms**: FindFirst returns -1, but pattern is in file.

**Solution 1**: Verify pattern is correct
```csharp
byte[] pattern = { 0xDE, 0xAD, 0xBE, 0xEF };

Console.WriteLine($"Searching for: {BitConverter.ToString(pattern)}");
// Output: "DE-AD-BE-EF"

long pos = hexEditor.FindFirst(pattern);
Console.WriteLine($"Found at: 0x{pos:X} (or -1 if not found)");
```

**Solution 2**: Search is case-sensitive (for text)
```csharp
// ❌ Won't find "hello"
byte[] pattern = Encoding.ASCII.GetBytes("HELLO");

// ✅ Correct pattern
byte[] pattern = Encoding.ASCII.GetBytes("hello");
```

**Solution 3**: Check search range
```csharp
// Search entire file (not just first part)
long pos = hexEditor.FindFirst(pattern, 0, hexEditor.Length);
```

---

### Issue: Search results incorrect after edits

**Symptoms**: Search finds old data, not modified data.

**Solution**: Search works on virtual view (includes edits)
```csharp
// Modify byte
hexEditor.ModifyByte(0xFF, 0x100);

// Search WILL find 0xFF at 0x100 (even before save)
byte[] pattern = { 0xFF };
long pos = hexEditor.FindFirst(pattern);  // Finds 0x100
```

If not working, verify changes are applied:
```csharp
// Check if byte was actually modified
byte value = hexEditor.GetByte(0x100);
Console.WriteLine($"Value at 0x100: 0x{value:X2}");  // Should be 0xFF
```

---

## 💾 Memory Issues

### Issue: "OutOfMemoryException" during operations

**Symptoms**: Application crashes with out of memory error.

**Solution 1**: Use 64-bit build
```xml
<PropertyGroup>
  <PlatformTarget>x64</PlatformTarget>
</PropertyGroup>
```

**Solution 2**: Process in chunks
```csharp
// ❌ Bad: Load entire file into memory
byte[] allBytes = hexEditor.GetAllBytes();  // May fail for large files

// ✅ Good: Process in chunks
const int chunkSize = 1_000_000;  // 1 MB

for (long pos = 0; pos < hexEditor.Length; pos += chunkSize)
{
    int length = (int)Math.Min(chunkSize, hexEditor.Length - pos);
    byte[] chunk = hexEditor.GetBytes(pos, length);

    ProcessChunk(chunk);
}
```

**Solution 3**: Clear resources
```csharp
// Close file when done
hexEditor.Close();

// Clear undo history
hexEditor.ClearUndoHistory();

// Force garbage collection (as last resort)
GC.Collect();
GC.WaitForPendingFinalizers();
```

---

### Issue: Memory leak (memory keeps growing)

**Symptoms**: Memory usage increases over time.

**Solution 1**: Close files explicitly
```csharp
// ALWAYS close when done
hexEditor.Close();
```

**Solution 2**: Clear undo history periodically
```csharp
// After saving, clear undo
hexEditor.Save();
hexEditor.ClearUndoHistory();  // Frees memory
```

**Solution 3**: Dispose properly
```csharp
// If creating new HexEditor instances:
var editor = new HexEditor();
// ... use editor ...
editor.Close();
editor = null;  // Allow GC
```

---

## 🐛 Debugging Tips

### Enable Diagnostic Logging

```csharp
public MainWindow()
{
    InitializeComponent();

    // Log all operations
    hexEditor.ByteModified += (s, e) =>
        Console.WriteLine($"Modified: 0x{e.Position:X} → 0x{e.NewValue:X2}");

    hexEditor.ByteInserted += (s, e) =>
        Console.WriteLine($"Inserted: 0x{e.Position:X} ← 0x{e.Value:X2}");

    hexEditor.ByteDeleted += (s, e) =>
        Console.WriteLine($"Deleted: 0x{e.Position:X} ({e.Count} bytes)");

    hexEditor.FileOpened += (s, e) =>
        Console.WriteLine($"Opened: {e.FileName} ({e.Length} bytes)");

    hexEditor.FileSaved += (s, e) =>
        Console.WriteLine($"Saved: {e.FileName}");
}
```

---

### Check Version

```csharp
// Verify you're using V2
Console.WriteLine($"WPF HexEditor version: {hexEditor.Version}");
// Should output: "2.x.x"

if (hexEditor.Version.Major < 2)
{
    Console.WriteLine("⚠️ Warning: Using old V1! Upgrade to V2 for 99% faster performance");
}
```

---

### Inspect Edit Tracking

```csharp
// Check what edits are tracked
Console.WriteLine("Edit Status:");
Console.WriteLine($"  Has changes: {hexEditor.HasChanges}");
Console.WriteLine($"  Modifications: {hexEditor.ModificationCount}");
Console.WriteLine($"  Insertions: {hexEditor.InsertionCount}");
Console.WriteLine($"  Deletions: {hexEditor.DeletionCount}");
Console.WriteLine($"  Undo depth: {hexEditor.UndoDepth}");
Console.WriteLine($"  Redo depth: {hexEditor.RedoDepth}");
```

---

## 💬 Still Having Issues?

### Get Help

- 📖 **[FAQ](FAQ)** - Frequently asked questions
- 💻 **[Sample Applications](Sample-Applications)** - Working examples
- 🐛 **[GitHub Issues](https://github.com/abbaye/WpfHexEditorControl/issues)** - Report bugs
- 💬 **[GitHub Discussions](https://github.com/abbaye/WpfHexEditorControl/discussions)** - Ask questions
- 📧 **Email**: derektremblay666@gmail.com

### Before Reporting Bug

Please provide:
1. **Version**: `hexEditor.Version`
2. **OS**: Windows version
3. **.NET**: Target framework
4. **File size**: Approximate size
5. **Steps to reproduce**: Minimal code example
6. **Error message**: Full exception stack trace

---

<div align="center">
  <br/>
  <p>
    <b>❓ Didn't find your issue?</b><br/>
    Ask on GitHub Discussions!
  </p>
  <br/>
  <p>
    👉 <a href="https://github.com/abbaye/WpfHexEditorControl/discussions"><b>Community Support</b></a> •
    <a href="FAQ"><b>FAQ</b></a> •
    <a href="Best-Practices"><b>Best Practices</b></a>
  </p>
</div>
