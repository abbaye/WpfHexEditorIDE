# TBL (Translation Tables)

Load custom character encoding tables for ROM hacking and game translation.

---

## 📋 Description

**TBL (Translation Tables)** allow you to map byte values to custom characters for displaying non-standard encodings. This is essential for ROM hacking, game translation, and working with custom character sets.

**Key characteristics**:
- 🎮 **ROM hacking support** - essential for game translation
- 🔤 **Custom character mappings** - byte values → display characters
- 📝 **Multi-byte support** - handle 2-byte, 3-byte character encodings
- ✅ **Standard TBL format** - compatible with ROM hacking tools
- 🔄 **Bidirectional** - decode (view) and encode (edit)

**Common Use Cases**: Game ROM translation, embedded systems, custom protocols.

---

## 📝 API Methods

```csharp
// Load TBL file
public void LoadTBL(string tblFilePath)
public void LoadTBLFromString(string tblContent)

// Unload TBL
public void UnloadTBL()

// Check TBL status
public bool IsTBLLoaded { get; }
public string CurrentTBLPath { get; }

// Get mapped character
public string GetTBLCharacter(byte value)
public string GetTBLCharacter(byte[] bytes)

// Encode character to bytes
public byte[] EncodeTBLCharacter(string character)
```

**Since:** V1.0

---

## 📄 TBL File Format

TBL files use simple text format:

```
# Comments start with #
# Format: HEX=Character

# Single-byte mappings
00=<NULL>
01=A
02=B
...
20= (space)
41=あ
42=い
43=う

# Multi-byte mappings
8140=　(full-width space)
8141=、
8142=。

# Special control codes
FE=<LINE>
FF=<END>
```

---

## 🎯 Examples

### Example 1: Load TBL File

```csharp
using WpfHexaEditor;

// Load TBL file
try
{
    hexEditor.LoadTBL("japanese.tbl");
    Console.WriteLine($"TBL loaded: {hexEditor.IsTBLLoaded}");
    Console.WriteLine($"Path: {hexEditor.CurrentTBLPath}");

    // ASCII column now shows mapped characters
}
catch (Exception ex)
{
    MessageBox.Show($"Error loading TBL: {ex.Message}", "Error");
}
```

---

### Example 2: Create TBL File Programmatically

```csharp
// Create custom TBL for game translation
public class TBLCreator
{
    public static void CreateJapaneseTBL(string outputPath)
    {
        var tbl = new StringBuilder();

        // Header
        tbl.AppendLine("# Japanese Game Character Table");
        tbl.AppendLine("# Created: " + DateTime.Now);
        tbl.AppendLine();

        // Control codes
        tbl.AppendLine("00=<NULL>");
        tbl.AppendLine("FE=<LINE>");
        tbl.AppendLine("FF=<END>");
        tbl.AppendLine();

        // ASCII range (0x20-0x7E)
        tbl.AppendLine("# ASCII characters");
        for (byte b = 0x20; b < 0x7F; b++)
        {
            char c = (char)b;
            tbl.AppendLine($"{b:X2}={c}");
        }

        // Japanese hiragana (custom mapping)
        tbl.AppendLine();
        tbl.AppendLine("# Hiragana");
        string[] hiragana = { "あ", "い", "う", "え", "お", "か", "き", "く", "け", "こ" };

        for (int i = 0; i < hiragana.Length; i++)
        {
            byte value = (byte)(0x80 + i);
            tbl.AppendLine($"{value:X2}={hiragana[i]}");
        }

        // Write to file
        File.WriteAllText(outputPath, tbl.ToString());

        Console.WriteLine($"TBL file created: {outputPath}");
    }

    public static void CreateMultiByteTBL(string outputPath)
    {
        var tbl = new StringBuilder();

        tbl.AppendLine("# Multi-byte Character Table");
        tbl.AppendLine();

        // Two-byte encodings (Shift-JIS style)
        tbl.AppendLine("# Two-byte characters");
        tbl.AppendLine("8140=　");  // Full-width space
        tbl.AppendLine("8141=、");  // Japanese comma
        tbl.AppendLine("8142=。");  // Japanese period
        tbl.AppendLine("8143=，");
        tbl.AppendLine("8144=．");

        File.WriteAllText(outputPath, tbl.ToString());

        Console.WriteLine($"Multi-byte TBL created: {outputPath}");
    }
}

// Usage
TBLCreator.CreateJapaneseTBL("japanese.tbl");
TBLCreator.CreateMultiByteTBL("multibyte.tbl");
```

---

### Example 3: TBL Manager UI

```csharp
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Load TBL button
        loadTBLButton.Click += LoadTBLButton_Click;

        // Unload TBL button
        unloadTBLButton.Click += (s, e) =>
        {
            hexEditor.UnloadTBL();
            UpdateTBLStatus();
            MessageBox.Show("TBL unloaded", "Info");
        };

        // Create TBL button
        createTBLButton.Click += CreateTBLButton_Click;

        UpdateTBLStatus();
    }

    private void LoadTBLButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "TBL Files (*.tbl)|*.tbl|All Files (*.*)|*.*",
            Title = "Load Translation Table"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                hexEditor.LoadTBL(dialog.FileName);
                UpdateTBLStatus();
                MessageBox.Show(
                    $"TBL loaded successfully\n" +
                    $"File: {dialog.FileName}",
                    "Success");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error loading TBL: {ex.Message}",
                    "Error");
            }
        }
    }

    private void CreateTBLButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "TBL Files (*.tbl)|*.tbl",
            Title = "Create Translation Table",
            FileName = "custom.tbl"
        };

        if (dialog.ShowDialog() == true)
        {
            // Show TBL editor dialog
            var editor = new TBLEditorDialog();

            if (editor.ShowDialog() == true)
            {
                File.WriteAllText(dialog.FileName, editor.TBLContent);

                MessageBox.Show($"TBL file created: {dialog.FileName}", "Success");
            }
        }
    }

    private void UpdateTBLStatus()
    {
        if (hexEditor.IsTBLLoaded)
        {
            tblStatusLabel.Text = $"TBL: {Path.GetFileName(hexEditor.CurrentTBLPath)}";
            tblStatusLabel.Foreground = Brushes.Green;
            unloadTBLButton.IsEnabled = true;
        }
        else
        {
            tblStatusLabel.Text = "TBL: None";
            tblStatusLabel.Foreground = Brushes.Gray;
            unloadTBLButton.IsEnabled = false;
        }
    }
}
```

---

### Example 4: Extract Text with TBL

```csharp
// Extract and decode text strings using TBL
public class TBLTextExtractor
{
    private HexEditor _hexEditor;

    public TBLTextExtractor(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    // Extract all text strings
    public List<TextString> ExtractStrings(byte terminatorByte = 0xFF)
    {
        if (!_hexEditor.IsTBLLoaded)
        {
            throw new InvalidOperationException("No TBL loaded");
        }

        var strings = new List<TextString>();
        var currentString = new StringBuilder();
        long stringStart = -1;

        for (long i = 0; i < _hexEditor.Length; i++)
        {
            byte b = _hexEditor.GetByte(i);

            if (b == terminatorByte)
            {
                // End of string
                if (currentString.Length > 0)
                {
                    strings.Add(new TextString
                    {
                        Offset = stringStart,
                        Text = currentString.ToString(),
                        Length = i - stringStart + 1
                    });

                    currentString.Clear();
                    stringStart = -1;
                }
            }
            else
            {
                // Decode character using TBL
                string character = _hexEditor.GetTBLCharacter(b);

                if (!string.IsNullOrEmpty(character) && !character.StartsWith("<"))
                {
                    if (stringStart < 0)
                        stringStart = i;

                    currentString.Append(character);
                }
                else
                {
                    // Non-printable or control code
                    currentString.Clear();
                    stringStart = -1;
                }
            }
        }

        return strings;
    }

    // Export strings to file
    public void ExportStrings(string outputPath, byte terminatorByte = 0xFF)
    {
        var strings = ExtractStrings(terminatorByte);

        using (var writer = new StreamWriter(outputPath, false, Encoding.UTF8))
        {
            writer.WriteLine("# Extracted Game Strings");
            writer.WriteLine($"# Total: {strings.Count}");
            writer.WriteLine();

            foreach (var str in strings)
            {
                writer.WriteLine($"[0x{str.Offset:X8}] {str.Text}");
            }
        }

        Console.WriteLine($"Exported {strings.Count} strings to {outputPath}");
    }
}

public class TextString
{
    public long Offset { get; set; }
    public string Text { get; set; }
    public long Length { get; set; }
}

// Usage
var extractor = new TBLTextExtractor(hexEditor);

// Load TBL
hexEditor.LoadTBL("game.tbl");

// Extract strings
var strings = extractor.ExtractStrings(0xFF);

Console.WriteLine($"Found {strings.Count} text strings:");
foreach (var str in strings.Take(10))
{
    Console.WriteLine($"  [0x{str.Offset:X}] {str.Text}");
}

// Export
extractor.ExportStrings("extracted_strings.txt");
```

---

### Example 5: Text Editor with TBL

```csharp
// Edit text in ROM using TBL encoding
public class TBLTextEditor
{
    private HexEditor _hexEditor;

    public TBLTextEditor(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    // Replace text at position
    public bool ReplaceText(long position, string oldText, string newText)
    {
        if (!_hexEditor.IsTBLLoaded)
        {
            throw new InvalidOperationException("No TBL loaded");
        }

        // Encode new text
        byte[] newBytes = EncodeText(newText);

        // Get old text length
        byte[] oldBytes = EncodeText(oldText);

        if (newBytes.Length > oldBytes.Length)
        {
            MessageBox.Show(
                $"New text too long!\n" +
                $"Max: {oldBytes.Length} bytes\n" +
                $"New: {newBytes.Length} bytes",
                "Error");
            return false;
        }

        // Pad with spaces if shorter
        if (newBytes.Length < oldBytes.Length)
        {
            byte spaceByte = _hexEditor.EncodeTBLCharacter(" ")[0];
            Array.Resize(ref newBytes, oldBytes.Length);

            for (int i = newText.Length; i < oldBytes.Length; i++)
            {
                newBytes[i] = spaceByte;
            }
        }

        // Replace bytes
        _hexEditor.ModifyBytes(position, newBytes);

        Console.WriteLine($"Replaced text at 0x{position:X}");
        return true;
    }

    private byte[] EncodeText(string text)
    {
        var bytes = new List<byte>();

        foreach (char c in text)
        {
            string charStr = c.ToString();
            byte[] encoded = _hexEditor.EncodeTBLCharacter(charStr);

            if (encoded != null && encoded.Length > 0)
            {
                bytes.AddRange(encoded);
            }
            else
            {
                // Character not in TBL - use placeholder
                bytes.Add(0x20);  // Space
            }
        }

        return bytes.ToArray();
    }

    // Interactive text editing dialog
    public void EditTextAtPosition(long position)
    {
        // Find text string at position
        var currentText = new StringBuilder();
        long i = position;

        while (i < _hexEditor.Length)
        {
            byte b = _hexEditor.GetByte(i);

            if (b == 0xFF)  // Terminator
                break;

            string character = _hexEditor.GetTBLCharacter(b);

            if (string.IsNullOrEmpty(character) || character.StartsWith("<"))
                break;

            currentText.Append(character);
            i++;
        }

        long textLength = i - position;

        // Show edit dialog
        var dialog = new TextEditDialog
        {
            OriginalText = currentText.ToString(),
            MaxLength = (int)textLength
        };

        if (dialog.ShowDialog() == true)
        {
            ReplaceText(position, currentText.ToString(), dialog.NewText);
        }
    }
}

// Usage: ROM translation workflow
var textEditor = new TBLTextEditor(hexEditor);

// Load game's TBL
hexEditor.LoadTBL("game.tbl");

// Find dialogue at position 0x10000
string originalDialogue = "おはよう";  // "Good morning" in Japanese

// Replace with English
textEditor.ReplaceText(0x10000, originalDialogue, "Hello!");

// Save ROM
hexEditor.Save();
```

---

### Example 6: TBL Validator

```csharp
// Validate TBL file format
public class TBLValidator
{
    public static ValidationResult ValidateTBL(string tblPath)
    {
        var result = new ValidationResult { IsValid = true };

        try
        {
            var lines = File.ReadAllLines(tblPath);
            var mappings = new Dictionary<string, string>();

            for (int lineNum = 0; lineNum < lines.Length; lineNum++)
            {
                string line = lines[lineNum].Trim();

                // Skip comments and empty lines
                if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                    continue;

                // Check format: HEX=CHAR
                if (!line.Contains("="))
                {
                    result.IsValid = false;
                    result.Errors.Add($"Line {lineNum + 1}: Missing '=' separator");
                    continue;
                }

                string[] parts = line.Split('=', 2);
                string hexPart = parts[0].Trim();
                string charPart = parts.Length > 1 ? parts[1].Trim() : "";

                // Validate hex part
                if (!IsValidHex(hexPart))
                {
                    result.IsValid = false;
                    result.Errors.Add($"Line {lineNum + 1}: Invalid hex value '{hexPart}'");
                    continue;
                }

                // Check for duplicates
                if (mappings.ContainsKey(hexPart))
                {
                    result.IsValid = false;
                    result.Errors.Add($"Line {lineNum + 1}: Duplicate mapping for {hexPart}");
                }
                else
                {
                    mappings[hexPart] = charPart;
                }
            }

            result.MappingCount = mappings.Count;
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"Error reading file: {ex.Message}");
        }

        return result;
    }

    private static bool IsValidHex(string hex)
    {
        if (string.IsNullOrEmpty(hex) || hex.Length % 2 != 0)
            return false;

        return hex.All(c => "0123456789ABCDEFabcdef".Contains(c));
    }
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public int MappingCount { get; set; }

    public override string ToString()
    {
        if (IsValid)
        {
            return $"✓ Valid TBL ({MappingCount} mappings)";
        }
        else
        {
            return $"✗ Invalid TBL\n" + string.Join("\n", Errors);
        }
    }
}

// Usage
var validation = TBLValidator.ValidateTBL("game.tbl");
Console.WriteLine(validation.ToString());
```

---

### Example 7: Multi-Byte Character Handling

```csharp
// Handle multi-byte character encodings
public class MultiByteTBLHandler
{
    private HexEditor _hexEditor;

    public MultiByteTBLHandler(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    // Decode multi-byte character at position
    public string DecodeCharacterAt(long position)
    {
        if (!_hexEditor.IsTBLLoaded)
            return null;

        // Try single-byte first
        byte singleByte = _hexEditor.GetByte(position);
        string singleChar = _hexEditor.GetTBLCharacter(singleByte);

        if (!string.IsNullOrEmpty(singleChar))
            return singleChar;

        // Try two-byte
        if (position + 1 < _hexEditor.Length)
        {
            byte[] twoBytes = _hexEditor.GetBytes(position, 2);
            string twoByteChar = _hexEditor.GetTBLCharacter(twoBytes);

            if (!string.IsNullOrEmpty(twoByteChar))
                return twoByteChar;
        }

        // Try three-byte
        if (position + 2 < _hexEditor.Length)
        {
            byte[] threeBytes = _hexEditor.GetBytes(position, 3);
            string threeByteChar = _hexEditor.GetTBLCharacter(threeBytes);

            if (!string.IsNullOrEmpty(threeByteChar))
                return threeByteChar;
        }

        return null;  // Not mapped
    }

    // Get character length (1, 2, or 3 bytes)
    public int GetCharacterLength(long position)
    {
        // Try three-byte
        if (position + 2 < _hexEditor.Length)
        {
            byte[] threeBytes = _hexEditor.GetBytes(position, 3);
            if (_hexEditor.GetTBLCharacter(threeBytes) != null)
                return 3;
        }

        // Try two-byte
        if (position + 1 < _hexEditor.Length)
        {
            byte[] twoBytes = _hexEditor.GetBytes(position, 2);
            if (_hexEditor.GetTBLCharacter(twoBytes) != null)
                return 2;
        }

        // Single-byte
        return 1;
    }
}
```

---

### Example 8: Game Script Dumper

```csharp
// Dump game script for translation
public class GameScriptDumper
{
    private HexEditor _hexEditor;

    public GameScriptDumper(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    public void DumpScript(string outputPath, ScriptFormat format)
    {
        var extractor = new TBLTextExtractor(_hexEditor);
        var strings = extractor.ExtractStrings(0xFF);

        using (var writer = new StreamWriter(outputPath, false, Encoding.UTF8))
        {
            switch (format)
            {
                case ScriptFormat.PlainText:
                    DumpPlainText(writer, strings);
                    break;

                case ScriptFormat.CSV:
                    DumpCSV(writer, strings);
                    break;

                case ScriptFormat.JSON:
                    DumpJSON(writer, strings);
                    break;
            }
        }

        Console.WriteLine($"Script dumped to {outputPath}");
    }

    private void DumpPlainText(StreamWriter writer, List<TextString> strings)
    {
        foreach (var str in strings)
        {
            writer.WriteLine($"[0x{str.Offset:X8}]");
            writer.WriteLine(str.Text);
            writer.WriteLine();
        }
    }

    private void DumpCSV(StreamWriter writer, List<TextString> strings)
    {
        writer.WriteLine("Offset,Original Text,Translated Text");

        foreach (var str in strings)
        {
            writer.WriteLine($"0x{str.Offset:X8},\"{str.Text}\",\"\"");
        }
    }

    private void DumpJSON(StreamWriter writer, List<TextString> strings)
    {
        writer.WriteLine("[");

        for (int i = 0; i < strings.Count; i++)
        {
            var str = strings[i];
            writer.WriteLine("  {");
            writer.WriteLine($"    \"offset\": \"0x{str.Offset:X8}\",");
            writer.WriteLine($"    \"original\": \"{str.Text}\",");
            writer.WriteLine($"    \"translated\": \"\"");
            writer.Write("  }");

            if (i < strings.Count - 1)
                writer.WriteLine(",");
            else
                writer.WriteLine();
        }

        writer.WriteLine("]");
    }
}

public enum ScriptFormat
{
    PlainText,
    CSV,
    JSON
}

// Usage: Translation workflow
var dumper = new GameScriptDumper(hexEditor);

// Load ROM and TBL
hexEditor.FileName = "game.rom";
hexEditor.LoadTBL("game.tbl");

// Dump for translation
dumper.DumpScript("script.csv", ScriptFormat.CSV);

// Translator edits CSV...

// Re-import and patch ROM
// (implementation not shown)
```

---

## 💡 Use Cases

### 1. RPG Translation Project

Complete workflow for translating an RPG game.

### 2. Embedded System Debugging

View custom protocol messages with TBL.

### 3. Retro Game Modding

Modify dialogue and menus in classic games.

### 4. Custom Protocol Analysis

Decode proprietary character encodings.

---

## ⚠️ Important Notes

### TBL Format Compatibility

- Compatible with standard ROM hacking TBL format
- Supports single-byte and multi-byte mappings
- Comments with `#` supported

### Character Display

- TBL affects ASCII column display only
- Hex column always shows raw bytes
- Special characters like `<LINE>` displayed as-is

### Encoding/Decoding

- Encoding: character → byte(s)
- Decoding: byte(s) → character
- Both directions supported

---

## 🔗 See Also

- **[GetByte()](../byte-operations/getbyte.md)** - Read bytes to decode
- **[ModifyBytes()](../byte-operations/modifybytes.md)** - Write encoded text
- **[FindAll()](../search-operations/findall.md)** - Search for text patterns

---

**Last Updated**: 2026-02-19
**Version**: V2.0
