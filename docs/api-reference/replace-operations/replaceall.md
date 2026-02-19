# ReplaceAll()

Find and replace all occurrences of a byte pattern with another pattern.

---

## 📋 Description

The `ReplaceAll()` method searches for all occurrences of a byte pattern and replaces them with another pattern. It combines the power of **FindAll()** with efficient batch editing, supporting both **same-length** (modify) and **different-length** (delete + insert) replacements.

**Key characteristics**:
- ⚡ **Fast batch operation** - uses BeginBatch/EndBatch internally
- 🔄 **Same-length optimization** - uses ModifyBytes (no position shifting)
- 📏 **Different-length support** - uses DeleteBytes + InsertBytes
- ✅ **Fully undoable** - single undo restores all replacements
- 🎯 **Returns replacement count** for verification
- 💾 **Atomic operation** - all or nothing (via undo)

**Important**: For **same-length** patterns, replacement is 10x faster as no position recalculation is needed.

---

## 📝 Signatures

```csharp
// Replace all occurrences
public int ReplaceAll(byte[] findPattern, byte[] replacePattern)

// Replace first N occurrences
public int ReplaceAll(byte[] findPattern, byte[] replacePattern, int maxReplacements)
```

**Since:** V1.0 (V2 optimized with batch operations)

---

## ⚙️ Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `findPattern` | `byte[]` | Pattern to search for |
| `replacePattern` | `byte[]` | Pattern to replace with |
| `maxReplacements` | `int` | Maximum number of replacements (optional, -1 = unlimited) |

---

## 🔄 Returns

| Return Type | Description |
|-------------|-------------|
| `int` | Number of replacements performed |

---

## 🎯 Examples

### Example 1: Basic Replace All

```csharp
using WpfHexaEditor;

// Open file
hexEditor.FileName = "data.bin";

// Replace all occurrences
byte[] findPattern = { 0xDE, 0xAD };
byte[] replacePattern = { 0xCA, 0xFE };

int count = hexEditor.ReplaceAll(findPattern, replacePattern);

Console.WriteLine($"Replaced {count} occurrences");
MessageBox.Show($"Replaced {count} instances", "Complete");
```

---

### Example 2: Replace with Confirmation

```csharp
private void ReplaceAllButton_Click(object sender, RoutedEventArgs e)
{
    // Parse patterns
    byte[] findPattern = ParseHexString(findTextBox.Text);
    byte[] replacePattern = ParseHexString(replaceTextBox.Text);

    if (findPattern == null || findPattern.Length == 0)
    {
        MessageBox.Show("Enter valid find pattern", "Error");
        return;
    }

    if (replacePattern == null || replacePattern.Length == 0)
    {
        MessageBox.Show("Enter valid replace pattern", "Error");
        return;
    }

    // Find count first
    var positions = hexEditor.FindAll(findPattern);

    if (positions.Count == 0)
    {
        MessageBox.Show("Pattern not found", "Not Found");
        return;
    }

    // Confirm replacement
    string findHex = BitConverter.ToString(findPattern);
    string replaceHex = BitConverter.ToString(replacePattern);

    var result = MessageBox.Show(
        $"Replace all {positions.Count} occurrences?\n\n" +
        $"Find: {findHex}\n" +
        $"Replace: {replaceHex}\n\n" +
        $"This operation can be undone.",
        "Confirm Replace All",
        MessageBoxButton.YesNo,
        MessageBoxImage.Question);

    if (result != MessageBoxResult.Yes)
        return;

    // Perform replacement
    var stopwatch = Stopwatch.StartNew();
    int count = hexEditor.ReplaceAll(findPattern, replacePattern);
    stopwatch.Stop();

    // Show result
    MessageBox.Show(
        $"Replaced {count} occurrences in {stopwatch.ElapsedMilliseconds}ms",
        "Success");

    statusLabel.Text = $"Replaced {count} patterns";
}
```

---

### Example 3: Replace with Different Lengths

```csharp
// Replace shorter pattern with longer one
private void ReplaceWithExpansion()
{
    // Find: 2 bytes
    byte[] findPattern = { 0xFF, 0xFF };

    // Replace: 4 bytes (expands by 2 bytes per match)
    byte[] replacePattern = { 0xDE, 0xAD, 0xBE, 0xEF };

    long oldLength = hexEditor.Length;

    // Replace (automatic delete + insert)
    int count = hexEditor.ReplaceAll(findPattern, replacePattern);

    long newLength = hexEditor.Length;
    long expansion = newLength - oldLength;

    Console.WriteLine($"Replaced {count} occurrences");
    Console.WriteLine($"File expanded by {expansion} bytes");
    Console.WriteLine($"  Old: {oldLength} bytes");
    Console.WriteLine($"  New: {newLength} bytes");
}

// Replace longer pattern with shorter one
private void ReplaceWithShrink()
{
    // Find: 4 bytes
    byte[] findPattern = { 0xDE, 0xAD, 0xBE, 0xEF };

    // Replace: 2 bytes (shrinks by 2 bytes per match)
    byte[] replacePattern = { 0x00, 0x00 };

    long oldLength = hexEditor.Length;
    int count = hexEditor.ReplaceAll(findPattern, replacePattern);
    long newLength = hexEditor.Length;

    long shrinkage = oldLength - newLength;

    Console.WriteLine($"Replaced {count} occurrences");
    Console.WriteLine($"File shrunk by {shrinkage} bytes");
}
```

---

### Example 4: Limit Number of Replacements

```csharp
// Replace only first 10 occurrences
private void ReplaceLimited()
{
    byte[] findPattern = { 0x00, 0x00 };
    byte[] replacePattern = { 0xFF, 0xFF };
    int maxReplacements = 10;

    // Find total count
    var allPositions = hexEditor.FindAll(findPattern);
    Console.WriteLine($"Found {allPositions.Count} total matches");

    // Replace first 10 only
    int count = hexEditor.ReplaceAll(findPattern, replacePattern, maxReplacements);

    Console.WriteLine($"Replaced first {count} matches");
    Console.WriteLine($"Remaining: {allPositions.Count - count} matches");
}
```

---

### Example 5: Replace with Undo Support

```csharp
// Replace with easy undo
private void ReplaceWithUndo()
{
    byte[] findPattern = { 0xAA, 0xBB };
    byte[] replacePattern = { 0xCC, 0xDD };

    // Replace (creates single undo entry for all replacements)
    int count = hexEditor.ReplaceAll(findPattern, replacePattern);

    Console.WriteLine($"Replaced {count} occurrences");
    Console.WriteLine($"Can undo: {hexEditor.CanUndo}");  // True

    // User wants to undo?
    var result = MessageBox.Show(
        $"Replaced {count} occurrences. Undo?",
        "Undo Replacement?",
        MessageBoxButton.YesNo);

    if (result == MessageBoxResult.Yes)
    {
        // Single undo restores ALL replacements
        hexEditor.Undo();

        Console.WriteLine("All replacements undone");

        // Verify
        var verifyPositions = hexEditor.FindAll(findPattern);
        Console.WriteLine($"Original pattern found again: {verifyPositions.Count} times");
    }
}
```

---

### Example 6: Replace Text Strings

```csharp
// Replace text strings in binary file
public class TextReplacer
{
    private HexEditor _hexEditor;

    public TextReplacer(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    // Replace ASCII text
    public int ReplaceText(string findText, string replaceText)
    {
        byte[] findBytes = Encoding.ASCII.GetBytes(findText);
        byte[] replaceBytes = Encoding.ASCII.GetBytes(replaceText);

        return _hexEditor.ReplaceAll(findBytes, replaceBytes);
    }

    // Replace with padding/truncation to maintain length
    public int ReplaceTextSameLength(string findText, string replaceText)
    {
        byte[] findBytes = Encoding.ASCII.GetBytes(findText);
        byte[] replaceBytes = Encoding.ASCII.GetBytes(replaceText);

        // Pad or truncate to same length
        if (replaceBytes.Length < findBytes.Length)
        {
            // Pad with spaces
            Array.Resize(ref replaceBytes, findBytes.Length);
            for (int i = replaceText.Length; i < replaceBytes.Length; i++)
            {
                replaceBytes[i] = 0x20;  // Space
            }
        }
        else if (replaceBytes.Length > findBytes.Length)
        {
            // Truncate
            Array.Resize(ref replaceBytes, findBytes.Length);
        }

        return _hexEditor.ReplaceAll(findBytes, replaceBytes);
    }

    // Replace with null-padding (C-style)
    public int ReplaceTextNullPadded(string findText, string replaceText)
    {
        byte[] findBytes = Encoding.ASCII.GetBytes(findText);
        byte[] replaceBytes = Encoding.ASCII.GetBytes(replaceText);

        // Pad with nulls
        if (replaceBytes.Length < findBytes.Length)
        {
            Array.Resize(ref replaceBytes, findBytes.Length);
            // Remaining bytes already 0x00
        }
        else if (replaceBytes.Length > findBytes.Length)
        {
            Array.Resize(ref replaceBytes, findBytes.Length);
        }

        return _hexEditor.ReplaceAll(findBytes, replaceBytes);
    }
}

// Usage: ROM translation
var replacer = new TextReplacer(hexEditor);

// Replace game text
int count = replacer.ReplaceTextSameLength("GAME OVER", "YOU WIN!!");
Console.WriteLine($"Replaced {count} game over messages");

// Replace with exact text
count = replacer.ReplaceText("Player 1", "Player One");
Console.WriteLine($"Replaced {count} player names");
```

---

### Example 7: Sanitize Sensitive Data

```csharp
// Replace all occurrences of sensitive patterns
public class DataSanitizer
{
    private HexEditor _hexEditor;

    // Patterns to sanitize
    private Dictionary<string, (byte[] pattern, byte[] replacement)> sanitizeRules = new()
    {
        // Replace credit card patterns with X
        { "Credit Card Marker", (
            Encoding.ASCII.GetBytes("4111-1111-1111-1111"),
            Encoding.ASCII.GetBytes("XXXX-XXXX-XXXX-XXXX")
        )},

        // Replace SSN patterns
        { "SSN Marker", (
            Encoding.ASCII.GetBytes("123-45-6789"),
            Encoding.ASCII.GetBytes("XXX-XX-XXXX")
        )},

        // Replace password strings
        { "Password", (
            Encoding.ASCII.GetBytes("password="),
            Encoding.ASCII.GetBytes("redacted=")
        )}
    };

    public DataSanitizer(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    public Dictionary<string, int> SanitizeAll()
    {
        var results = new Dictionary<string, int>();

        _hexEditor.BeginBatch();

        try
        {
            foreach (var (name, (pattern, replacement)) in sanitizeRules)
            {
                int count = _hexEditor.ReplaceAll(pattern, replacement);
                results[name] = count;

                if (count > 0)
                {
                    Console.WriteLine($"Sanitized {count} instances of {name}");
                }
            }
        }
        finally
        {
            _hexEditor.EndBatch();
        }

        return results;
    }
}

// Usage
var sanitizer = new DataSanitizer(hexEditor);
var results = sanitizer.SanitizeAll();

int totalSanitized = results.Values.Sum();
if (totalSanitized > 0)
{
    hexEditor.Save();
    MessageBox.Show($"Sanitized {totalSanitized} sensitive patterns", "Complete");
}
```

---

### Example 8: Pattern-Based Code Patching

```csharp
// Apply code patches to binary
public class CodePatcher
{
    private HexEditor _hexEditor;

    public CodePatcher(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    // Replace instruction sequences
    public int PatchInstructions(Dictionary<string, (byte[] find, byte[] replace)> patches)
    {
        int totalPatches = 0;

        _hexEditor.BeginBatch();

        try
        {
            foreach (var (patchName, (findBytes, replaceBytes)) in patches)
            {
                // Verify same length for code patches
                if (findBytes.Length != replaceBytes.Length)
                {
                    Console.WriteLine($"⚠️ Skipping {patchName}: different lengths");
                    continue;
                }

                int count = _hexEditor.ReplaceAll(findBytes, replaceBytes);

                if (count > 0)
                {
                    Console.WriteLine($"✓ Applied {patchName}: {count} locations");
                    totalPatches += count;
                }
            }
        }
        finally
        {
            _hexEditor.EndBatch();
        }

        return totalPatches;
    }
}

// Usage: Apply game patches
var patches = new Dictionary<string, (byte[], byte[])>
{
    // NOP out anti-debug check (JNZ -> NOP NOP)
    { "Remove Anti-Debug", (
        new byte[] { 0x75, 0x05 },  // JNZ +5
        new byte[] { 0x90, 0x90 }   // NOP NOP
    )},

    // Change jump destination
    { "Redirect Jump", (
        new byte[] { 0xEB, 0x10 },  // JMP +16
        new byte[] { 0xEB, 0x20 }   // JMP +32
    )},

    // Change comparison value
    { "Unlock Feature", (
        new byte[] { 0x83, 0xF8, 0x00 },  // CMP EAX, 0
        new byte[] { 0x83, 0xF8, 0x01 }   // CMP EAX, 1
    )}
};

var patcher = new CodePatcher(hexEditor);
int patchCount = patcher.PatchInstructions(patches);

if (patchCount > 0)
{
    hexEditor.Save();
    MessageBox.Show($"Applied {patchCount} code patches", "Success");
}
```

---

## 💡 Use Cases

### 1. Malware Removal

```csharp
// Remove malware signatures by replacing with NOPs
public class MalwareRemover
{
    private HexEditor _hexEditor;

    // Known malware signatures and their replacements
    private Dictionary<string, (byte[] signature, byte[] replacement)> malwarePatches = new()
    {
        {
            "Trojan Shellcode",
            (
                new byte[] { 0xE8, 0x00, 0x00, 0x00, 0x00, 0x5D },  // Malicious code
                new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90, 0x90 }   // NOP sled
            )
        },
        {
            "Backdoor Call",
            (
                new byte[] { 0xFF, 0x15, 0x00, 0x10, 0x40, 0x00 },  // Call to backdoor
                new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90, 0x90 }   // NOP sled
            )
        }
    };

    public MalwareRemover(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    public int RemoveMalware()
    {
        int totalRemoved = 0;

        _hexEditor.BeginBatch();

        try
        {
            foreach (var (name, (signature, replacement)) in malwarePatches)
            {
                int count = _hexEditor.ReplaceAll(signature, replacement);

                if (count > 0)
                {
                    Console.WriteLine($"✓ Removed {name}: {count} instances");
                    totalRemoved += count;
                }
            }
        }
        finally
        {
            _hexEditor.EndBatch();
        }

        return totalRemoved;
    }
}

// Usage
var remover = new MalwareRemover(hexEditor);
int removed = remover.RemoveMalware();

if (removed > 0)
{
    hexEditor.Save();
    MessageBox.Show($"Removed {removed} malware patterns", "Clean");
}
```

---

### 2. Binary Protocol Rewriting

```csharp
// Rewrite protocol headers in network capture
public class ProtocolRewriter
{
    private HexEditor _hexEditor;

    public ProtocolRewriter(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    // Change protocol version
    public int UpgradeProtocolVersion(byte oldVersion, byte newVersion)
    {
        // HTTP/1.0 -> HTTP/1.1
        byte[] oldHeader = Encoding.ASCII.GetBytes($"HTTP/1.{oldVersion}");
        byte[] newHeader = Encoding.ASCII.GetBytes($"HTTP/1.{newVersion}");

        return _hexEditor.ReplaceAll(oldHeader, newHeader);
    }

    // Change server identification
    public int ReplaceServerHeader(string oldServer, string newServer)
    {
        // Ensure same length
        if (oldServer.Length != newServer.Length)
        {
            newServer = newServer.PadRight(oldServer.Length).Substring(0, oldServer.Length);
        }

        byte[] oldBytes = Encoding.ASCII.GetBytes($"Server: {oldServer}");
        byte[] newBytes = Encoding.ASCII.GetBytes($"Server: {newServer}");

        return _hexEditor.ReplaceAll(oldBytes, newBytes);
    }
}

// Usage
var rewriter = new ProtocolRewriter(hexEditor);

int upgraded = rewriter.UpgradeProtocolVersion(0, 1);
Console.WriteLine($"Upgraded {upgraded} HTTP/1.0 to HTTP/1.1");

int changed = rewriter.ReplaceServerHeader("Apache/2.4", "CustomSrv");
Console.WriteLine($"Changed {changed} server headers");
```

---

### 3. Data Format Migration

```csharp
// Migrate old data format to new format
public class FormatMigrator
{
    private HexEditor _hexEditor;

    public FormatMigrator(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    // Migrate file format markers
    public bool MigrateFormat()
    {
        _hexEditor.BeginBatch();

        try
        {
            // Replace format version marker
            byte[] oldMagic = { 0x46, 0x4D, 0x54, 0x31 };  // "FMT1"
            byte[] newMagic = { 0x46, 0x4D, 0x54, 0x32 };  // "FMT2"

            int magicCount = _hexEditor.ReplaceAll(oldMagic, newMagic);

            // Update record separators
            byte[] oldSep = { 0x1E };  // Record separator
            byte[] newSep = { 0x00 };  // Null separator

            int sepCount = _hexEditor.ReplaceAll(oldSep, newSep);

            // Update field delimiters
            byte[] oldDelim = { 0x1F };  // Unit separator
            byte[] newDelim = { 0x7C };  // Pipe |

            int delimCount = _hexEditor.ReplaceAll(oldDelim, newDelim);

            Console.WriteLine("Format Migration:");
            Console.WriteLine($"  Magic updated: {magicCount}");
            Console.WriteLine($"  Separators: {sepCount}");
            Console.WriteLine($"  Delimiters: {delimCount}");

            return magicCount > 0;
        }
        finally
        {
            _hexEditor.EndBatch();
        }
    }
}

// Usage
var migrator = new FormatMigrator(hexEditor);

if (migrator.MigrateFormat())
{
    hexEditor.Save();
    MessageBox.Show("Format migrated successfully", "Success");
}
```

---

### 4. Bulk Color Palette Modification

```csharp
// Replace colors in image palette
public class PaletteModifier
{
    private HexEditor _hexEditor;

    public PaletteModifier(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    // Replace RGB color values
    public int ReplaceColor(byte[] oldRGB, byte[] newRGB)
    {
        if (oldRGB.Length != 3 || newRGB.Length != 3)
        {
            throw new ArgumentException("RGB must be 3 bytes");
        }

        return _hexEditor.ReplaceAll(oldRGB, newRGB);
    }

    // Batch color replacement
    public Dictionary<string, int> ReplacePalette(
        Dictionary<string, (byte[] oldRGB, byte[] newRGB)> colors)
    {
        var results = new Dictionary<string, int>();

        _hexEditor.BeginBatch();

        try
        {
            foreach (var (colorName, (oldRGB, newRGB)) in colors)
            {
                int count = _hexEditor.ReplaceAll(oldRGB, newRGB);
                results[colorName] = count;

                if (count > 0)
                {
                    Console.WriteLine($"Replaced {colorName}: {count} pixels");
                }
            }
        }
        finally
        {
            _hexEditor.EndBatch();
        }

        return results;
    }
}

// Usage: Change game sprite colors
var modifier = new PaletteModifier(hexEditor);

var palette = new Dictionary<string, (byte[], byte[])>
{
    { "Red", (new byte[] { 0xFF, 0x00, 0x00 }, new byte[] { 0x00, 0xFF, 0x00 }) },  // Red -> Green
    { "Blue", (new byte[] { 0x00, 0x00, 0xFF }, new byte[] { 0xFF, 0xFF, 0x00 }) }  // Blue -> Yellow
};

var results = modifier.ReplacePalette(palette);

int totalReplaced = results.Values.Sum();
Console.WriteLine($"Total pixels recolored: {totalReplaced}");
```

---

## ⚡ Performance Tips

### Same-Length Replacements Are Faster

```csharp
// Fast: Same length (uses ModifyBytes)
byte[] find = { 0x41, 0x42, 0x43, 0x44 };  // 4 bytes
byte[] replace = { 0x58, 0x59, 0x5A, 0x57 };  // 4 bytes
int count = hexEditor.ReplaceAll(find, replace);
// Time: ~10ms for 1000 replacements

// Slower: Different length (uses DeleteBytes + InsertBytes)
byte[] find2 = { 0x41, 0x42 };  // 2 bytes
byte[] replace2 = { 0x58, 0x59, 0x5A, 0x57 };  // 4 bytes
int count2 = hexEditor.ReplaceAll(find2, replace2);
// Time: ~100ms for 1000 replacements (10x slower)
```

### Batch Multiple Replacements

```csharp
// If doing multiple replace operations, use BeginBatch/EndBatch
hexEditor.BeginBatch();

hexEditor.ReplaceAll(pattern1, replacement1);
hexEditor.ReplaceAll(pattern2, replacement2);
hexEditor.ReplaceAll(pattern3, replacement3);

hexEditor.EndBatch();  // Single UI update!
```

---

## ⚠️ Important Notes

### Replacement Count

- Returns number of **actual replacements** performed
- Returns 0 if pattern not found

### Pattern Cannot Overlap Replacement

- If patterns overlap during replacement, results may be unexpected
- Example: Replacing "AA" with "AAA" will find new "AA" patterns

```csharp
// Careful with overlapping patterns!
// Original: AA AA AA
byte[] find = { 0xAA, 0xAA };
byte[] replace = { 0xAA, 0xAA, 0xAA };
// Result may not be what you expect!
```

### Single Undo Entry

- All replacements grouped into **single undo operation**
- One Undo() restores all replacements

### Thread Safety

- ❌ Not thread-safe
- Must be called from UI thread

---

## 🔗 See Also

- **[ReplaceFirst()](replacefirst.md)** - Replace only first occurrence
- **[FindAll()](../search-operations/findall.md)** - Find all occurrences without replacing
- **[ModifyBytes()](../byte-operations/modifybytes.md)** - Directly modify bytes (same length)
- **[DeleteBytes()](../byte-operations/deletebytes.md)** + **[InsertBytes()](../byte-operations/insertbytes.md)** - Manual replace (different length)

---

**Last Updated**: 2026-02-19
**Version**: V2.0 (Optimized batch operations)
