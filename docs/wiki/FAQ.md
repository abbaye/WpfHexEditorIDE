# Frequently Asked Questions (FAQ)

Common questions and answers about WPF HexEditor V2.

---

## 📦 Installation & Setup

### Q: How do I install WPF HexEditor?

**A:** Install via NuGet Package Manager:

```bash
# .NET CLI
dotnet add package WPFHexaEditor

# Package Manager Console
Install-Package WPFHexaEditor
```

Then add to your XAML:
```xml
<Window xmlns:hex="clr-namespace:WpfHexaEditor;assembly=WPFHexaEditor">
  <hex:HexEditor FileName="data.bin" />
</Window>
```

👉 **[Complete installation guide](Installation)**

---

### Q: What .NET versions are supported?

**A:** WPF HexEditor supports:
- ✅ **.NET Framework 4.8** (Windows only)
- ✅ **.NET 8.0-windows** (recommended for best performance)

The NuGet package uses **multi-targeting**, so the correct binary is automatically selected based on your project's target framework.

**Recommendation**: Use .NET 8.0 for maximum performance (Span&lt;T&gt;, SIMD, PGO).

---

### Q: Can I use this in WinForms?

**A:** Yes! Use `ElementHost` to host the WPF control:

```csharp
using System.Windows.Forms.Integration;

// Add ElementHost to your form
var host = new ElementHost
{
    Dock = DockStyle.Fill
};

// Create HexEditor
var hexEditor = new WpfHexaEditor.HexEditor();
host.Child = hexEditor;

// Add to form
this.Controls.Add(host);
```

👉 **[See WinForms sample](https://github.com/abbaye/WpfHexEditorControl/tree/master/Sources/Samples/WpfHexEditor.Sample.Winform)**

---

## 🔧 Usage & Features

### Q: How do I make the control read-only?

**A:** Set the `ReadOnlyMode` property:

```csharp
hexEditor.ReadOnlyMode = true;
```

Or in XAML:
```xml
<hex:HexEditor ReadOnlyMode="True" FileName="data.bin" />
```

---

### Q: Can I open files larger than 1 GB?

**A:** Yes! V2 handles files from bytes to gigabytes efficiently:

- Files < 100 MB: Fast immediate load
- Files > 100 MB: Use `OpenAsync()` with progress
- Files > 1 GB: Fully supported with lazy loading

```csharp
// For large files, use async
var progress = new Progress<double>(p => progressBar.Value = p);
await hexEditor.OpenAsync("large.bin", progress);
```

---

### Q: How do I search for a hex pattern?

**A:** Use the `FindFirst()` method:

```csharp
// Search for byte pattern
var pattern = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
long position = hexEditor.FindFirst(pattern);

if (position >= 0)
{
    Console.WriteLine($"Found at 0x{position:X}");
    hexEditor.SetPosition(position);  // Scroll to match
}
```

👉 **[Complete search documentation](API-Search-Operations)**

---

### Q: How do I copy bytes as C# code?

**A:** Use the clipboard service with format:

```csharp
// Copy 16 bytes as C# array
hexEditor.CopyToClipboard(0x100, 16, ClipboardFormat.CSharpArray);

// Clipboard now contains:
// new byte[] { 0x41, 0x42, 0x43, ... }
```

Available formats:
- `HexString` - "41 42 43 44"
- `CSharpArray` - `new byte[] { 0x41, 0x42 }`
- `CArray` - `{0x41, 0x42}`
- `Binary` - "01000001 01000010"

---

### Q: Can I undo/redo changes?

**A:** Yes! Unlimited undo/redo is built-in:

```csharp
// Undo last change
if (hexEditor.CanUndo)
    hexEditor.Undo();

// Redo
if (hexEditor.CanRedo)
    hexEditor.Redo();

// Clear all history
hexEditor.ClearUndoHistory();
```

---

### Q: How do I highlight specific byte ranges?

**A:** Use the `AddHighlight()` method:

```csharp
// Highlight file header (first 256 bytes) in light blue
hexEditor.AddHighlight(0, 256, Colors.LightBlue, "File Header");

// Highlight data section in light green
hexEditor.AddHighlight(0x1000, 2048, Colors.LightGreen, "Data Section");
```

---

## ⚡ Performance

### Q: Why is V2 so much faster than V1?

**A:** V2 uses several advanced optimizations:

1. **Custom DrawingContext Rendering** (99% faster)
   - V1: ItemsControl + DataTemplate (slow)
   - V2: Direct pixel rendering (blazing fast)

2. **Smart Search with LRU Cache** (10-100x faster)
   - Repeated searches are cached
   - SIMD acceleration (AVX2/SSE2)
   - Parallel multi-core for large files

3. **Span&lt;T&gt; + Memory Pooling** (80-90% less memory)
   - Zero-copy operations
   - Reduced GC pressure

👉 **[Performance benchmarks](https://github.com/abbaye/WpfHexEditorControl#-key-stats)**

---

### Q: My large file opens slowly. What can I do?

**A:** Use async operations with progress:

```csharp
// Show progress bar
progressBar.Visibility = Visibility.Visible;

var progress = new Progress<double>(percent =>
{
    progressBar.Value = percent;
    statusLabel.Text = $"Opening: {percent:F1}%";
});

// Open asynchronously
await hexEditor.OpenAsync("large.bin", progress);

progressBar.Visibility = Visibility.Collapsed;
```

Also consider:
- ✅ Warn users for files > 100 MB
- ✅ Use read-only mode if editing not needed
- ✅ Close other applications to free memory

---

### Q: How can I improve search performance?

**A:** V2 search is already optimized, but you can:

1. **Use CountOccurrences() instead of FindAll()**
   ```csharp
   // Memory efficient (no position storage)
   int count = hexEditor.CountOccurrences(pattern);
   ```

2. **Search from known position**
   ```csharp
   // Don't search entire file if you know approximate location
   long pos = hexEditor.FindFirst(pattern, startPosition: 0x10000);
   ```

3. **Cache frequently searched patterns**
   - Search cache is automatic in V2
   - Repeated searches are 10-100x faster

---

## 🐛 Troubleshooting

### Q: I get "Type not found" error when adding the control

**A:** Common causes:

1. **NuGet package not installed**
   - Solution: Install WPFHexaEditor via NuGet

2. **Wrong namespace**
   - ❌ Wrong: `xmlns:hex="clr-namespace:WpfHexEditor"`
   - ✅ Correct: `xmlns:hex="clr-namespace:WpfHexaEditor;assembly=WPFHexaEditor"`

3. **Build issue**
   - Solution: Clean and rebuild
   ```bash
   dotnet clean
   dotnet build
   ```

---

### Q: The control appears but is blank

**A:** Check that you've opened a file:

```csharp
// Make sure to set FileName or call Open()
hexEditor.FileName = "data.bin";

// Or
hexEditor.Open("data.bin");
```

Also verify the file exists:
```csharp
if (!File.Exists("data.bin"))
{
    MessageBox.Show("File not found!");
}
```

---

### Q: I get "Access denied" when saving

**A:** This means write permission is denied. Try:

1. **Run as Administrator**
2. **Check file is not read-only**
   ```csharp
   var fileInfo = new FileInfo(hexEditor.FileName);
   if (fileInfo.IsReadOnly)
   {
       MessageBox.Show("File is read-only!");
   }
   ```

3. **Save to different location**
   ```csharp
   hexEditor.SaveAs(@"C:\Temp\output.bin");
   ```

4. **Check CanWrite property**
   ```csharp
   if (!hexEditor.CanWrite)
   {
       MessageBox.Show("File opened in read-only mode");
   }
   ```

---

### Q: Changes aren't saving

**A:** Verify you're calling Save():

```csharp
// Don't forget to save!
hexEditor.ModifyByte(0xFF, 0x100);
hexEditor.Save();  // ← Must call this

// Or use SaveAs
hexEditor.SaveAs("output.bin");
```

Also check for errors:
```csharp
try
{
    hexEditor.Save();
}
catch (Exception ex)
{
    MessageBox.Show($"Save failed: {ex.Message}");
}
```

---

### Q: Insert mode isn't working

**A:** Verify insert mode is enabled:

```csharp
// Enable insert mode
hexEditor.AllowInsertMode = true;

// Check current mode
if (hexEditor.IsInInsertMode)
{
    Console.WriteLine("Insert mode active");
}
else
{
    Console.WriteLine("Overwrite mode active");
}
```

**Note**: Issue #145 (Insert Mode bug) was fixed in V2. Make sure you're using the latest version.

---

## 🔄 Migration from V1

### Q: Is V2 compatible with V1 code?

**A:** Yes! V2 is **100% compatible** with V1:

- ✅ Same API methods
- ✅ Same properties
- ✅ Same events
- ✅ Zero code changes required

Just update the NuGet package and everything works!

👉 **[Migration guide](https://github.com/abbaye/WpfHexEditorControl/blob/master/MIGRATION.md)**

---

### Q: What critical bugs were fixed in V2?

**A:** All major V1 bugs are fixed:

| Bug | V1 Status | V2 Status |
|-----|-----------|-----------|
| **Issue #145: Insert Mode** | ⚠️ Critical | ✅ **FIXED** |
| **Save Data Loss** | ⚠️ Critical | ✅ **FIXED** |
| **Search Cache** | ⚠️ | ✅ **FIXED** |
| **Binary Search** | ⚠️ | ✅ **FIXED** |

V2 is **production-ready** ✅

---

### Q: Should I upgrade from V1 to V2?

**A:** **Absolutely yes!** Benefits:

- ⚡ **99% faster** rendering
- 🔍 **10-100x faster** search
- 💾 **80-90% less** memory
- 🐛 **All critical bugs** fixed
- 🏗️ **Better architecture** (MVVM + Services)
- ✅ **Zero migration** effort (100% compatible)

---

## 🎨 Customization

### Q: How do I change colors?

**A:** Use color properties:

```xml
<hex:HexEditor SelectionFirstColor="#FF3399FF"
               SelectionSecondColor="#FF0066CC"
               ModifiedByteColor="#FFFF0000"
               InsertedByteColor="#FF00FF00"
               DeletedByteColor="#FFAAAAAA"/>
```

Or in code:
```csharp
hexEditor.SelectionFirstColor = Color.FromRgb(51, 153, 255);
hexEditor.ModifiedByteColor = Colors.Red;
```

---

### Q: Can I change the font?

**A:** Yes! Use standard WPF font properties:

```xml
<hex:HexEditor FontFamily="Consolas"
               FontSize="14"
               FontWeight="Bold"/>
```

Recommended monospace fonts:
- Consolas (default)
- Courier New
- Cascadia Code
- Fira Code

---

### Q: How do I hide certain columns?

**A:** Use visibility properties:

```csharp
// Hide offset column
hexEditor.ShowOffset = false;

// Hide ASCII column
hexEditor.ShowASCII = false;

// Hide hex column (ASCII only mode)
hexEditor.ShowHex = false;

// Hide BarChart
hexEditor.ShowBarChart = false;
```

---

### Q: Can I change bytes per line?

**A:** Yes! Use `BytesPerLine` property:

```csharp
// Default is 16
hexEditor.BytesPerLine = 16;

// Show more bytes per line
hexEditor.BytesPerLine = 32;  // or 64, 128, etc.

// Show less (for narrow displays)
hexEditor.BytesPerLine = 8;
```

---

## 🌍 Localization

### Q: What languages are supported?

**A:** 19 languages built-in:
- English, French, Spanish, German, Italian
- Portuguese, Russian, Chinese (Simplified/Traditional)
- Japanese, Korean, Polish, Turkish, Dutch
- Swedish, Norwegian, Danish, Finnish, Czech

---

### Q: How do I change the language?

**A:** Set the UI culture:

```csharp
using System.Globalization;
using System.Threading;

// Change to French
Thread.CurrentThread.CurrentUICulture = new CultureInfo("fr-FR");

// Change to Japanese
Thread.CurrentThread.CurrentUICulture = new CultureInfo("ja-JP");
```

Language switches instantly - no restart needed!

---

## 📚 More Help

### Still have questions?

- 📖 **[Complete Documentation](Home)** - Full guides
- 🎓 **[Quick Start Tutorial](Quick-Start)** - Get started fast
- 💻 **[Sample Applications](Sample-Applications)** - Working examples
- 🐛 **[GitHub Issues](https://github.com/abbaye/WpfHexEditorControl/issues)** - Bug reports
- 💬 **[Discussions](https://github.com/abbaye/WpfHexEditorControl/discussions)** - Community Q&A
- 📧 **Email**: derektremblay666@gmail.com

---

<div align="center">
  <br/>
  <p>
    <b>Didn't find your answer?</b><br/>
    👉 <a href="https://github.com/abbaye/WpfHexEditorControl/discussions"><b>Ask on GitHub Discussions</b></a>
  </p>
</div>
