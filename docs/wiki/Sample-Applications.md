# Sample Applications

Explore real-world applications built with WPF HexEditor to learn by example.

---

## 📋 Overview

The WPF HexEditor repository includes **7+ complete sample applications** demonstrating various use cases and features. Each sample is production-ready and well-documented.

**Location**: [Sources/Samples/](https://github.com/abbaye/WpfHexEditorControl/tree/master/Sources/Samples)

---

## 🎯 Sample Applications

### 1. WpfHexEditor.Sample.MVVM

**Full-featured hex editor application** demonstrating MVVM pattern and best practices.

**Features**:
- ✅ Complete file operations (Open, Save, SaveAs, Close)
- ✅ Advanced search and replace
- ✅ Bookmarks and highlights
- ✅ Undo/Redo with history
- ✅ TBL (Translation Table) support
- ✅ Async operations with progress
- ✅ Recent files management
- ✅ Multi-language support
- ✅ Modern Material Design UI

**Technologies**:
- MVVM pattern with ViewModels
- Dependency Injection
- Command pattern
- DataBinding
- Material Design themes

**Perfect for**:
- Learning MVVM with WPF HexEditor
- Understanding best practices
- Starting your own hex editor app

**Screenshot**:
```
┌─────────────────────────────────────────────┐
│ File  Edit  View  Search  Tools  Help      │
├─────────────────────────────────────────────┤
│ 📂 Open  💾 Save  🔍 Find  ↩️ Undo  ↪️ Redo │
├─────────────────────────────────────────────┤
│ Offset    Hex                     ASCII     │
│ 00000000: 4D 5A 90 00 03 00 00 00  MZ......│
│ 00000008: 04 00 00 00 FF FF 00 00  ........│
│ ...                                         │
└─────────────────────────────────────────────┘
```

**Run it**:
```bash
cd Sources/Samples/WpfHexEditor.Sample.MVVM
dotnet run
```

---

### 2. WpfHexEditor.Sample.Winform

**WinForms integration** demonstrating how to use WPF HexEditor in Windows Forms applications.

**Features**:
- ✅ ElementHost integration
- ✅ WinForms controls interop
- ✅ Traditional Windows Forms UI
- ✅ File operations with WinForms dialogs
- ✅ Menu and toolbar integration

**Key Code**:
```csharp
// Create ElementHost
var elementHost = new ElementHost
{
    Dock = DockStyle.Fill
};

// Create HexEditor
var hexEditor = new WpfHexaEditor.HexEditor();
elementHost.Child = hexEditor;

// Add to form
this.Controls.Add(elementHost);
```

**Perfect for**:
- Existing WinForms applications
- Legacy application modernization
- Learning WPF/WinForms interop

**Run it**:
```bash
cd Sources/Samples/WpfHexEditor.Sample.Winform
dotnet run
```

---

### 3. Binary File Analyzer

**Analyze binary file structures** with automatic format detection and parsing.

**Features**:
- ✅ Automatic file type detection (PE, ELF, ZIP, PNG, JPEG, etc.)
- ✅ Header parsing and visualization
- ✅ Structure highlighting
- ✅ Checksum calculation (CRC32, MD5, SHA1, SHA256)
- ✅ Entropy analysis
- ✅ String extraction
- ✅ Export analysis report

**Use Cases**:
- Reverse engineering
- File format research
- Malware analysis
- Data recovery

**Example Analysis**:
```
File: executable.exe
Type: PE Executable (MZ header)
Size: 524,288 bytes

Structure:
├─ DOS Header (0x00 - 0x40)
├─ PE Header (0x100 - 0x178)
├─ Section .text (0x1000 - 0x50000)
├─ Section .data (0x51000 - 0x60000)
└─ Section .rsrc (0x61000 - 0x80000)

Checksums:
  CRC32: A1B2C3D4
  MD5: 5d41402abc4b2a76b9719d911017c592
  SHA256: 2c26b46b68ffc68ff99b453c1d30413413422d706...

Entropy: 7.84 (high - likely compressed/encrypted)
```

**Code Snippet**:
```csharp
// Detect file type from magic bytes
byte[] magic = hexEditor.GetBytes(0, 4);

if (magic[0] == 0x4D && magic[1] == 0x5A)
{
    // Parse PE header
    ParsePEHeader(hexEditor);
    HighlightPEStructures(hexEditor);
}
else if (magic[0] == 0x89 && magic[1] == 0x50)
{
    // Parse PNG chunks
    ParsePNGChunks(hexEditor);
}

// Calculate entropy
double entropy = CalculateEntropy(hexEditor);
entropyLabel.Text = $"Entropy: {entropy:F2}";
```

---

### 4. Binary Diff Tool

**Compare two binary files** and visualize differences side-by-side.

**Features**:
- ✅ Side-by-side hex comparison
- ✅ Diff highlighting (red = different, green = same)
- ✅ Difference statistics
- ✅ Jump to next/previous diff
- ✅ Export diff report
- ✅ Patch generation
- ✅ Ignore byte ranges
- ✅ Fast comparison (SIMD optimized)

**Use Cases**:
- ROM hacking (compare patched vs original)
- Version comparison
- Data validation
- Debugging

**UI Layout**:
```
┌───────────────────────────────────────────────────────┐
│ File 1: original.bin    |  File 2: modified.bin      │
├───────────────────────────────────────────────────────┤
│ 00000000: 4D 5A 90 00  |  00000000: 4D 5A 90 00     │
│ 00000008: 04 00 00 00  |  00000008: 04 FF 00 00  ⚠️ │
│ 00000010: FF FF 00 00  |  00000010: FF FF 00 00     │
├───────────────────────────────────────────────────────┤
│ Differences: 1,234 bytes (2.4%)                       │
│ Same: 50,000 bytes (97.6%)                            │
└───────────────────────────────────────────────────────┘
```

**Code Snippet**:
```csharp
public class BinaryDiff
{
    public long Position { get; set; }
    public byte File1Byte { get; set; }
    public byte File2Byte { get; set; }
}

private List<BinaryDiff> CompareFiles(HexEditor hex1, HexEditor hex2)
{
    var diffs = new List<BinaryDiff>();
    long minLength = Math.Min(hex1.Length, hex2.Length);

    for (long i = 0; i < minLength; i++)
    {
        byte byte1 = hex1.GetByte(i);
        byte byte2 = hex2.GetByte(i);

        if (byte1 != byte2)
        {
            diffs.Add(new BinaryDiff
            {
                Position = i,
                File1Byte = byte1,
                File2Byte = byte2
            });
        }
    }

    return diffs;
}
```

---

### 5. ROM Patcher

**Patch ROM files** for game modding and translation projects.

**Features**:
- ✅ Load ROM file
- ✅ Apply IPS/UPS/BPS patches
- ✅ Create new patches
- ✅ Checksum validation (CRC32, MD5)
- ✅ Backup original ROM
- ✅ Patch preview before applying
- ✅ Multi-patch support
- ✅ Undo patch application

**Use Cases**:
- Game ROM hacking
- ROM translation projects
- Bug fixes in game ROMs
- Custom game modifications

**Workflow**:
```
1. Load ROM → 2. Load Patch → 3. Preview → 4. Apply → 5. Save
```

**Example**:
```csharp
public class ROMPatcher
{
    private HexEditor _hexEditor;

    public void ApplyIPSPatch(string patchFile)
    {
        // Read IPS patch
        var patch = IPSPatch.Load(patchFile);

        // Apply each record
        _hexEditor.BeginBatch();

        foreach (var record in patch.Records)
        {
            if (record.IsRLE)
            {
                // RLE encoding: repeat value
                for (int i = 0; i < record.Length; i++)
                {
                    _hexEditor.ModifyByte(record.Value, record.Offset + i);
                }
            }
            else
            {
                // Direct data
                _hexEditor.ModifyBytes(record.Offset, record.Data);
            }
        }

        _hexEditor.EndBatch();
    }

    public bool ValidateChecksum(string expectedMD5)
    {
        using var md5 = MD5.Create();
        byte[] data = _hexEditor.GetAllBytes();
        byte[] hash = md5.ComputeHash(data);
        string actualMD5 = BitConverter.ToString(hash).Replace("-", "");
        return actualMD5.Equals(expectedMD5, StringComparison.OrdinalIgnoreCase);
    }
}
```

---

### 6. Data Recovery Tool

**Recover deleted or corrupted data** from binary files and disk images.

**Features**:
- ✅ Scan for file signatures
- ✅ Carve files from disk images
- ✅ Preview recovered files
- ✅ Export carved data
- ✅ Signature database (100+ file types)
- ✅ Progress reporting for large files
- ✅ False positive filtering

**Use Cases**:
- Digital forensics
- Deleted file recovery
- Disk image analysis
- Data carving

**File Signatures**:
```csharp
private Dictionary<string, byte[]> fileSignatures = new()
{
    { "JPEG", new byte[] { 0xFF, 0xD8, 0xFF } },
    { "PNG", new byte[] { 0x89, 0x50, 0x4E, 0x47 } },
    { "PDF", new byte[] { 0x25, 0x50, 0x44, 0x46 } },
    { "ZIP", new byte[] { 0x50, 0x4B, 0x03, 0x04 } },
    { "GIF", new byte[] { 0x47, 0x49, 0x46, 0x38 } },
    // ... 100+ signatures
};

public List<CarvedFile> ScanForFiles()
{
    var results = new List<CarvedFile>();

    foreach (var (fileType, signature) in fileSignatures)
    {
        var positions = _hexEditor.FindAll(signature);

        foreach (var pos in positions)
        {
            results.Add(new CarvedFile
            {
                Type = fileType,
                Offset = pos,
                Signature = signature
            });
        }
    }

    return results;
}
```

---

### 7. Hex Calculator

**Perform bitwise operations** and calculations on bytes.

**Features**:
- ✅ XOR, AND, OR, NOT operations
- ✅ ROL/ROR (rotate left/right)
- ✅ Checksum calculations (XOR, CRC-8, CRC-16, CRC-32)
- ✅ Base conversions (Hex, Decimal, Binary, Octal)
- ✅ Apply operation to selection
- ✅ Operation history
- ✅ Custom bit masks

**Use Cases**:
- Cryptography analysis
- Checksum fixing
- Bit manipulation
- Encoding/decoding

**Calculator UI**:
```
┌───────────────────────────────────┐
│ Byte at 0x100: 0xA5 (165)         │
├───────────────────────────────────┤
│ Operations:                       │
│ [ XOR ] [ AND ] [ OR ] [ NOT ]    │
│ [ INC ] [ DEC ] [ ROL ] [ ROR ]   │
├───────────────────────────────────┤
│ Value: [FF] (hex)                 │
│                                   │
│ Result: 0x5A (90)                 │
│ Binary: 01011010                  │
└───────────────────────────────────┘
```

**Example**:
```csharp
public class HexCalculator
{
    private HexEditor _hexEditor;

    // XOR operation
    public void XorByte(long position, byte xorValue)
    {
        byte currentValue = _hexEditor.GetByte(position);
        byte newValue = (byte)(currentValue ^ xorValue);
        _hexEditor.ModifyByte(newValue, position);
    }

    // Calculate CRC-32
    public uint CalculateCRC32()
    {
        uint crc = 0xFFFFFFFF;

        for (long i = 0; i < _hexEditor.Length; i++)
        {
            byte b = _hexEditor.GetByte(i);
            crc ^= b;

            for (int bit = 0; bit < 8; bit++)
            {
                if ((crc & 1) != 0)
                    crc = (crc >> 1) ^ 0xEDB88320;
                else
                    crc >>= 1;
            }
        }

        return ~crc;
    }

    // Apply operation to selection
    public void XorSelection(byte xorValue)
    {
        long start = _hexEditor.SelectionStart;
        long length = _hexEditor.SelectionLength;

        _hexEditor.BeginBatch();

        for (long i = 0; i < length; i++)
        {
            XorByte(start + i, xorValue);
        }

        _hexEditor.EndBatch();
    }
}
```

---

### 8. TBL Editor

**Edit and test Translation Tables** (TBL files) for ROM translation projects.

**Features**:
- ✅ Load/save TBL files
- ✅ Edit character mappings
- ✅ Test encoding/decoding
- ✅ Preview in hex editor
- ✅ Unicode support
- ✅ Multi-byte character support
- ✅ Export to different formats

**Use Cases**:
- Game ROM translation
- Character encoding research
- Custom font mapping

**TBL Format**:
```
# Character mappings (hex=character)
00=<NULL>
01=A
02=B
...
20= (space)
41=あ
42=い
```

---

## 🚀 Getting Started

### Clone and Build All Samples

```bash
# Clone repository
git clone https://github.com/abbaye/WpfHexEditorControl.git
cd WpfHexEditorControl

# Build all samples
dotnet build Sources/Samples/WpfHexEditor.Sample.MVVM
dotnet build Sources/Samples/WpfHexEditor.Sample.Winform

# Run MVVM sample
cd Sources/Samples/WpfHexEditor.Sample.MVVM
dotnet run
```

---

## 📚 Learning Path

### Beginner Path
1. **Start with**: WpfHexEditor.Sample.MVVM
2. **Learn**: Basic file operations, navigation, editing
3. **Time**: 30 minutes

### Intermediate Path
1. **Explore**: Binary File Analyzer
2. **Learn**: File parsing, structure highlighting, checksums
3. **Time**: 1-2 hours

### Advanced Path
1. **Study**: ROM Patcher + Data Recovery Tool
2. **Learn**: Complex operations, batch processing, algorithms
3. **Time**: 4-8 hours

---

## 💡 Common Code Patterns

### Pattern 1: File Operations with Error Handling

```csharp
private void OpenFileButton_Click(object sender, RoutedEventArgs e)
{
    var dialog = new OpenFileDialog
    {
        Filter = "All Files (*.*)|*.*",
        Title = "Open Binary File"
    };

    if (dialog.ShowDialog() == true)
    {
        try
        {
            hexEditor.FileName = dialog.FileName;
            statusLabel.Text = $"Opened: {dialog.FileName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error opening file: {ex.Message}", "Error");
        }
    }
}
```

### Pattern 2: Async Operations with Progress

```csharp
private async void OpenLargeFileButton_Click(object sender, RoutedEventArgs e)
{
    progressBar.Visibility = Visibility.Visible;

    var progress = new Progress<double>(percent =>
    {
        progressBar.Value = percent;
        statusLabel.Text = $"Opening: {percent:F1}%";
    });

    try
    {
        await hexEditor.OpenAsync(fileName, progress);
        MessageBox.Show("File opened successfully!");
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

### Pattern 3: Search with Results Display

```csharp
private void SearchButton_Click(object sender, RoutedEventArgs e)
{
    // Get search pattern
    byte[] pattern = ParseHexString(searchTextBox.Text);

    // Search
    var positions = hexEditor.FindAll(pattern);

    // Display results
    resultsListBox.Items.Clear();
    foreach (var pos in positions)
    {
        resultsListBox.Items.Add($"0x{pos:X8}");
    }

    statusLabel.Text = $"Found {positions.Count} matches";
}
```

### Pattern 4: Batch Operations

```csharp
private void FillRangeButton_Click(object sender, RoutedEventArgs e)
{
    long start = hexEditor.SelectionStart;
    long length = hexEditor.SelectionLength;
    byte fillValue = 0x00;

    // Efficient batch operation
    hexEditor.BeginBatch();

    try
    {
        for (long i = 0; i < length; i++)
        {
            hexEditor.ModifyByte(fillValue, start + i);
        }
    }
    finally
    {
        hexEditor.EndBatch();  // Update UI once
    }
}
```

---

## 🔧 Building Your Own Application

### Step 1: Choose a Sample as Template

Pick the sample closest to your use case:
- General purpose → MVVM Sample
- WinForms → WinForms Sample
- Analysis tool → Binary Analyzer
- Comparison → Binary Diff

### Step 2: Copy and Customize

```bash
# Copy sample
cp -r Sources/Samples/WpfHexEditor.Sample.MVVM MyHexApp

# Rename namespace
# Update project references
# Customize UI and features
```

### Step 3: Add Your Features

- Implement custom file parsers
- Add domain-specific operations
- Integrate with your existing tools
- Customize UI/UX

---

## 📊 Performance Tips from Samples

### Tip 1: Use BeginBatch/EndBatch
```csharp
// From ROM Patcher sample
hexEditor.BeginBatch();
foreach (var patch in patches)
    ApplyPatch(patch);
hexEditor.EndBatch();
// 100x faster than individual updates
```

### Tip 2: Async for Large Files
```csharp
// From Binary Analyzer sample
if (fileSize > 100_000_000)  // > 100 MB
{
    await hexEditor.OpenAsync(fileName, progress);
}
```

### Tip 3: Cache Frequently Accessed Data
```csharp
// From Binary Diff sample
byte[] cache1 = hexEditor1.GetBytes(0, 10000);
byte[] cache2 = hexEditor2.GetBytes(0, 10000);
// Compare from cache (faster)
```

---

## 🔗 Next Steps

### Explore Samples
- **[Browse on GitHub](https://github.com/abbaye/WpfHexEditorControl/tree/master/Sources/Samples)**
- **[Download Latest Release](https://github.com/abbaye/WpfHexEditorControl/releases)**

### Learn More
- **[API Reference](API-Reference)** - Complete API documentation
- **[Basic Operations](Basic-Operations)** - Fundamental operations guide
- **[Best Practices](Best-Practices)** - Performance optimization

### Build Something
- **[Architecture Overview](Architecture-Overview)** - System design
- **[Quick Start](Quick-Start)** - 5-minute tutorial
- **[FAQ](FAQ)** - Common questions

---

<div align="center">
  <br/>
  <p>
    <b>🎨 Ready to build your own hex editor app?</b><br/>
    Start with a sample and customize it!
  </p>
  <br/>
  <p>
    👉 <a href="https://github.com/abbaye/WpfHexEditorControl/tree/master/Sources/Samples"><b>Browse Samples</b></a> •
    <a href="Quick-Start"><b>Quick Start</b></a> •
    <a href="API-Reference"><b>API Reference</b></a>
  </p>
</div>
