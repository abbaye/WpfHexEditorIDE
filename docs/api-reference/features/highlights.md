# Highlight Operations

Visually highlight byte ranges with colors for analysis and annotation.

---

## 📋 Description

Highlights provide **visual color overlays** on byte ranges to mark regions of interest. Unlike bookmarks (which mark single positions), highlights span ranges and provide immediate visual feedback in the hex view.

**Key characteristics**:
- 🎨 **Color overlays** on byte ranges
- 📏 **Span multiple bytes** (ranges, not single positions)
- 👁️ **Visual feedback** in hex and ASCII columns
- ✅ **Customizable colors** and transparency
- 🔍 **Multiple highlights** can overlap

**Use Cases**: Mark file structures, identify data patterns, visualize analysis results.

---

## 📝 API Methods

```csharp
// Add highlight
public void AddHighlight(long position, long length, Color color)
public void AddHighlight(long position, long length, Color color, string description)

// Remove highlight
public void RemoveHighlight(long position)
public void RemoveHighlightsByColor(Color color)
public bool RemoveHighlightByDescription(string description)

// Get highlights
public List<Highlight> GetHighlights()
public Highlight GetHighlightAt(long position)
public List<Highlight> GetHighlightsInRange(long start, long length)

// Clear
public void ClearHighlights()
public void ClearHighlightsInRange(long start, long length)

// Check existence
public bool HasHighlightAt(long position)
public int HighlightCount { get; }
```

**Since:** V1.0

---

## 🎯 Examples

### Example 1: Basic Highlights

```csharp
using WpfHexaEditor;

// Highlight file header (first 256 bytes) in light blue
hexEditor.AddHighlight(0, 256, Colors.LightBlue, "File Header");

// Highlight data section (4KB) in light green
hexEditor.AddHighlight(0x1000, 4096, Colors.LightGreen, "Data Section");

// Highlight string table in light yellow
hexEditor.AddHighlight(0x5000, 2048, Colors.LightYellow, "String Table");

Console.WriteLine($"Total highlights: {hexEditor.HighlightCount}");
```

---

### Example 2: Highlight Search Results

```csharp
// Highlight all occurrences of pattern
private void HighlightSearchResults(byte[] pattern, Color color)
{
    // Find all matches
    var positions = hexEditor.FindAll(pattern);

    if (positions.Count == 0)
    {
        MessageBox.Show("Pattern not found", "Info");
        return;
    }

    // Clear previous highlights
    hexEditor.ClearHighlights();

    // Highlight each match
    foreach (var pos in positions)
    {
        hexEditor.AddHighlight(pos, pattern.Length, color, "Match");
    }

    MessageBox.Show(
        $"Highlighted {positions.Count} matches",
        "Success");

    // Navigate to first match
    if (positions.Count > 0)
    {
        hexEditor.SetPosition(positions[0]);
    }
}

// Usage: Highlight all "0xDEADBEEF" patterns
byte[] pattern = { 0xDE, 0xAD, 0xBE, 0xEF };
HighlightSearchResults(pattern, Colors.Yellow);
```

---

### Example 3: Structure Visualization

```csharp
// Visualize file structures with colored highlights
public class StructureVisualizer
{
    private HexEditor _hexEditor;

    public StructureVisualizer(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    // Visualize PE file structure
    public void VisualizePEStructure()
    {
        // Clear existing highlights
        _hexEditor.ClearHighlights();

        // DOS Header (64 bytes)
        _hexEditor.AddHighlight(0x0000, 64, Colors.LightBlue, "DOS Header");

        // DOS Stub (variable, approximate 128 bytes)
        _hexEditor.AddHighlight(0x0040, 128, Colors.LightGray, "DOS Stub");

        // Get PE offset
        byte[] peOffsetBytes = _hexEditor.GetBytes(0x3C, 4);
        int peOffset = BitConverter.ToInt32(peOffsetBytes, 0);

        // PE Signature (4 bytes)
        _hexEditor.AddHighlight(peOffset, 4, Colors.LightGreen, "PE Signature");

        // COFF Header (20 bytes)
        _hexEditor.AddHighlight(peOffset + 4, 20, Colors.LightYellow, "COFF Header");

        // Optional Header (224 bytes for PE32)
        _hexEditor.AddHighlight(peOffset + 24, 224, Colors.LightCoral, "Optional Header");

        Console.WriteLine("PE structure visualized with color highlights");
    }

    // Visualize bitmap file
    public void VisualizeBitmap()
    {
        _hexEditor.ClearHighlights();

        // BMP Header (14 bytes)
        _hexEditor.AddHighlight(0, 14, Colors.LightBlue, "BMP Header");

        // DIB Header (40 bytes)
        _hexEditor.AddHighlight(14, 40, Colors.LightGreen, "DIB Header");

        // Get pixel array offset
        byte[] offsetBytes = _hexEditor.GetBytes(10, 4);
        int pixelArrayOffset = BitConverter.ToInt32(offsetBytes, 0);

        // Pixel Array (rest of file)
        long pixelArraySize = _hexEditor.Length - pixelArrayOffset;
        _hexEditor.AddHighlight(pixelArrayOffset, pixelArraySize,
                               Colors.LightYellow, "Pixel Array");

        Console.WriteLine("Bitmap structure visualized");
    }
}

// Usage
var visualizer = new StructureVisualizer(hexEditor);
visualizer.VisualizePEStructure();
```

---

### Example 4: Highlight Manager UI

```csharp
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Add highlight button
        addHighlightButton.Click += AddHighlightButton_Click;

        // Highlight list
        highlightListBox.SelectionChanged += HighlightListBox_SelectionChanged;

        // Remove highlight button
        removeHighlightButton.Click += RemoveHighlightButton_Click;

        // Clear all button
        clearHighlightsButton.Click += (s, e) =>
        {
            hexEditor.ClearHighlights();
            RefreshHighlightList();
        };

        RefreshHighlightList();
    }

    private void AddHighlightButton_Click(object sender, RoutedEventArgs e)
    {
        long start = hexEditor.SelectionStart;
        long length = hexEditor.SelectionLength;

        if (length == 0)
        {
            MessageBox.Show("Select bytes to highlight", "Info");
            return;
        }

        // Show color picker dialog
        var colorDialog = new ColorPickerDialog();

        if (colorDialog.ShowDialog() == true)
        {
            Color color = colorDialog.SelectedColor;

            // Add highlight
            hexEditor.AddHighlight(start, length, color,
                $"Range 0x{start:X}-0x{(start + length):X}");

            RefreshHighlightList();
            statusLabel.Text = $"Highlighted {length} bytes";
        }
    }

    private void HighlightListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (highlightListBox.SelectedItem is Highlight highlight)
        {
            // Navigate to highlight
            hexEditor.SetPosition(highlight.Position);
            hexEditor.SelectionStart = highlight.Position;
            hexEditor.SelectionLength = highlight.Length;
        }
    }

    private void RemoveHighlightButton_Click(object sender, RoutedEventArgs e)
    {
        if (highlightListBox.SelectedItem is Highlight highlight)
        {
            hexEditor.RemoveHighlight(highlight.Position);
            RefreshHighlightList();
            statusLabel.Text = "Highlight removed";
        }
    }

    private void RefreshHighlightList()
    {
        highlightListBox.Items.Clear();

        var highlights = hexEditor.GetHighlights();

        foreach (var highlight in highlights.OrderBy(h => h.Position))
        {
            // Create colored item
            var item = new ListBoxItem
            {
                Content = $"0x{highlight.Position:X8} ({highlight.Length} bytes) - {highlight.Description}",
                Background = new SolidColorBrush(highlight.Color)
            };

            highlightListBox.Items.Add(item);
        }

        removeHighlightButton.IsEnabled = (highlightListBox.SelectedIndex >= 0);
        clearHighlightsButton.IsEnabled = (highlights.Count > 0);
    }
}
```

---

### Example 5: Data Pattern Highlighting

```csharp
// Automatically highlight data patterns
public class PatternHighlighter
{
    private HexEditor _hexEditor;

    public PatternHighlighter(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    // Highlight all null byte sequences
    public void HighlightNullSequences(int minLength = 16)
    {
        byte[] pattern = Enumerable.Repeat((byte)0x00, minLength).ToArray();
        var positions = _hexEditor.FindAll(pattern);

        foreach (var pos in positions)
        {
            // Find actual length of null sequence
            long nullLength = minLength;
            while (pos + nullLength < _hexEditor.Length &&
                   _hexEditor.GetByte(pos + nullLength) == 0x00)
            {
                nullLength++;
            }

            _hexEditor.AddHighlight(pos, nullLength,
                                   Colors.LightGray, $"Null sequence ({nullLength} bytes)");
        }

        Console.WriteLine($"Highlighted {positions.Count} null sequences");
    }

    // Highlight repeated byte patterns
    public void HighlightRepeatedPatterns()
    {
        for (byte b = 0; b < 256; b++)
        {
            // Skip null (already handled)
            if (b == 0x00)
                continue;

            // Find sequences of 16+ repeated bytes
            byte[] pattern = Enumerable.Repeat(b, 16).ToArray();
            var positions = _hexEditor.FindAll(pattern);

            if (positions.Count > 0)
            {
                // Use different color per pattern
                Color color = GetColorForByte(b);

                foreach (var pos in positions)
                {
                    _hexEditor.AddHighlight(pos, 16, color,
                        $"Repeated 0x{b:X2}");
                }

                Console.WriteLine($"Found {positions.Count} sequences of 0x{b:X2}");
            }
        }
    }

    // Highlight entropy (random-looking data)
    public void HighlightHighEntropy(int chunkSize = 256)
    {
        for (long pos = 0; pos < _hexEditor.Length; pos += chunkSize)
        {
            int length = (int)Math.Min(chunkSize, _hexEditor.Length - pos);
            byte[] chunk = _hexEditor.GetBytes(pos, length);

            double entropy = CalculateEntropy(chunk);

            // High entropy (> 7.5) likely compressed/encrypted
            if (entropy > 7.5)
            {
                _hexEditor.AddHighlight(pos, length,
                    Colors.Red, $"High entropy ({entropy:F2})");
            }
        }
    }

    private double CalculateEntropy(byte[] data)
    {
        var frequency = new int[256];
        foreach (byte b in data)
            frequency[b]++;

        double entropy = 0.0;
        foreach (int count in frequency)
        {
            if (count > 0)
            {
                double probability = (double)count / data.Length;
                entropy -= probability * Math.Log(probability, 2);
            }
        }

        return entropy;
    }

    private Color GetColorForByte(byte b)
    {
        // Generate color based on byte value
        return Color.FromRgb((byte)(b * 7 % 256),
                            (byte)(b * 11 % 256),
                            (byte)(b * 13 % 256));
    }
}

// Usage
var highlighter = new PatternHighlighter(hexEditor);

highlighter.HighlightNullSequences();
highlighter.HighlightRepeatedPatterns();
highlighter.HighlightHighEntropy();
```

---

### Example 6: Diff Highlighting

```csharp
// Highlight differences between two files
public class DiffHighlighter
{
    private HexEditor _hexEditor1;
    private HexEditor _hexEditor2;

    public DiffHighlighter(HexEditor hexEditor1, HexEditor hexEditor2)
    {
        _hexEditor1 = hexEditor1;
        _hexEditor2 = hexEditor2;
    }

    public void HighlightDifferences()
    {
        // Clear highlights
        _hexEditor1.ClearHighlights();
        _hexEditor2.ClearHighlights();

        long minLength = Math.Min(_hexEditor1.Length, _hexEditor2.Length);
        int diffCount = 0;

        // Find differences
        long diffStart = -1;

        for (long i = 0; i < minLength; i++)
        {
            byte byte1 = _hexEditor1.GetByte(i);
            byte byte2 = _hexEditor2.GetByte(i);

            if (byte1 != byte2)
            {
                if (diffStart < 0)
                    diffStart = i;
            }
            else if (diffStart >= 0)
            {
                // End of diff region
                long diffLength = i - diffStart;

                _hexEditor1.AddHighlight(diffStart, diffLength,
                    Colors.LightCoral, "Different");
                _hexEditor2.AddHighlight(diffStart, diffLength,
                    Colors.LightCoral, "Different");

                diffCount++;
                diffStart = -1;
            }
        }

        // Handle diff at end
        if (diffStart >= 0)
        {
            long diffLength = minLength - diffStart;

            _hexEditor1.AddHighlight(diffStart, diffLength,
                Colors.LightCoral, "Different");
            _hexEditor2.AddHighlight(diffStart, diffLength,
                Colors.LightCoral, "Different");

            diffCount++;
        }

        // Highlight length difference
        if (_hexEditor1.Length != _hexEditor2.Length)
        {
            long maxLength = Math.Max(_hexEditor1.Length, _hexEditor2.Length);

            if (_hexEditor1.Length > _hexEditor2.Length)
            {
                _hexEditor1.AddHighlight(minLength, _hexEditor1.Length - minLength,
                    Colors.Yellow, "Extra bytes");
            }
            else
            {
                _hexEditor2.AddHighlight(minLength, _hexEditor2.Length - minLength,
                    Colors.Yellow, "Extra bytes");
            }
        }

        Console.WriteLine($"Highlighted {diffCount} difference regions");
    }
}
```

---

### Example 7: Heatmap Visualization

```csharp
// Create byte value heatmap
public class HeatmapVisualizer
{
    private HexEditor _hexEditor;

    public HeatmapVisualizer(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    // Visualize byte distribution with color gradient
    public void CreateByteValueHeatmap(long start, long length, int chunkSize = 256)
    {
        _hexEditor.ClearHighlights();

        for (long pos = start; pos < start + length; pos += chunkSize)
        {
            int chunkLength = (int)Math.Min(chunkSize, start + length - pos);
            byte[] chunk = _hexEditor.GetBytes(pos, chunkLength);

            // Calculate average byte value
            double average = chunk.Average(b => b);

            // Map to color (0-255 → Blue to Red)
            Color color = GetHeatmapColor(average);

            _hexEditor.AddHighlight(pos, chunkLength, color,
                $"Avg: {average:F1}");
        }

        Console.WriteLine("Heatmap visualization created");
    }

    private Color GetHeatmapColor(double value)
    {
        // 0-255 → Cold (Blue) to Hot (Red)
        byte intensity = (byte)value;

        if (value < 85)
        {
            // Blue → Cyan
            return Color.FromRgb(0, (byte)(value * 3), (byte)(255 - value * 3));
        }
        else if (value < 170)
        {
            // Cyan → Yellow
            byte adjusted = (byte)((value - 85) * 3);
            return Color.FromRgb(adjusted, 255, 0);
        }
        else
        {
            // Yellow → Red
            byte adjusted = (byte)((value - 170) * 3);
            return Color.FromRgb(255, (byte)(255 - adjusted), 0);
        }
    }
}

// Usage: Visualize entire file as heatmap
var heatmap = new HeatmapVisualizer(hexEditor);
heatmap.CreateByteValueHeatmap(0, hexEditor.Length);
```

---

### Example 8: Interactive Highlight Annotation

```csharp
// Allow user to annotate with highlights interactively
public class InteractiveAnnotator
{
    private HexEditor _hexEditor;
    private bool _annotationMode = false;

    public InteractiveAnnotator(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;

        // Mouse selection handler
        hexEditor.SelectionChanged += HexEditor_SelectionChanged;
    }

    public void EnableAnnotationMode()
    {
        _annotationMode = true;
        Console.WriteLine("Annotation mode enabled (select bytes and press key)");

        _hexEditor.KeyDown += HexEditor_KeyDown;
    }

    public void DisableAnnotationMode()
    {
        _annotationMode = false;
        Console.WriteLine("Annotation mode disabled");

        _hexEditor.KeyDown -= HexEditor_KeyDown;
    }

    private void HexEditor_SelectionChanged(object sender, EventArgs e)
    {
        if (!_annotationMode || _hexEditor.SelectionLength == 0)
            return;

        // Show selection info
        long start = _hexEditor.SelectionStart;
        long length = _hexEditor.SelectionLength;

        Console.WriteLine($"Selected: 0x{start:X} ({length} bytes)");
        Console.WriteLine("Press 1-9 to add colored highlight");
    }

    private void HexEditor_KeyDown(object sender, KeyEventArgs e)
    {
        if (!_annotationMode || _hexEditor.SelectionLength == 0)
            return;

        Color? color = null;

        // Number keys for colors
        switch (e.Key)
        {
            case Key.D1: color = Colors.Red; break;
            case Key.D2: color = Colors.Green; break;
            case Key.D3: color = Colors.Blue; break;
            case Key.D4: color = Colors.Yellow; break;
            case Key.D5: color = Colors.Cyan; break;
            case Key.D6: color = Colors.Magenta; break;
            case Key.D7: color = Colors.Orange; break;
            case Key.D8: color = Colors.Purple; break;
            case Key.D9: color = Colors.Pink; break;
        }

        if (color.HasValue)
        {
            _hexEditor.AddHighlight(
                _hexEditor.SelectionStart,
                _hexEditor.SelectionLength,
                color.Value,
                "User annotation");

            Console.WriteLine($"Highlighted with {color.Value}");
            e.Handled = true;
        }
    }
}

// Usage
var annotator = new InteractiveAnnotator(hexEditor);
annotator.EnableAnnotationMode();

// User can now select bytes and press 1-9 to highlight
```

---

## 💡 Use Cases

### 1. Malware Analysis

```csharp
// Highlight suspicious regions in malware sample
public class MalwareHighlighter
{
    private HexEditor _hexEditor;

    public MalwareHighlighter(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    public void HighlightSuspiciousRegions()
    {
        // Highlight packed/encrypted sections (high entropy)
        HighlightHighEntropySections();

        // Highlight suspicious strings
        HighlightSuspiciousStrings();

        // Highlight shellcode patterns
        HighlightShellcodePatterns();

        Console.WriteLine("Malware analysis highlights applied");
    }

    private void HighlightHighEntropySections()
    {
        // Implementation...
    }

    private void HighlightSuspiciousStrings()
    {
        // Strings like "cmd.exe", "regedit", etc.
        var suspiciousStrings = new[] { "cmd.exe", "powershell", "regedit" };

        foreach (var str in suspiciousStrings)
        {
            byte[] pattern = Encoding.ASCII.GetBytes(str);
            var positions = _hexEditor.FindAll(pattern);

            foreach (var pos in positions)
            {
                _hexEditor.AddHighlight(pos, pattern.Length,
                    Colors.Red, $"Suspicious: {str}");
            }
        }
    }

    private void HighlightShellcodePatterns()
    {
        // Common shellcode patterns (NOP sleds, etc.)
        byte[] nopSled = Enumerable.Repeat((byte)0x90, 16).ToArray();
        var positions = _hexEditor.FindAll(nopSled);

        foreach (var pos in positions)
        {
            _hexEditor.AddHighlight(pos, nopSled.Length,
                Colors.Orange, "Potential NOP sled");
        }
    }
}
```

---

### 2. Network Protocol Analysis

```csharp
// Highlight protocol fields in packet capture
public class ProtocolHighlighter
{
    private HexEditor _hexEditor;

    public ProtocolHighlighter(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    // Highlight IP packet fields
    public void HighlightIPPacket(long packetStart)
    {
        // IP Header (20 bytes minimum)
        _hexEditor.AddHighlight(packetStart, 20,
            Colors.LightBlue, "IP Header");

        // Source IP (4 bytes)
        _hexEditor.AddHighlight(packetStart + 12, 4,
            Colors.LightGreen, "Source IP");

        // Destination IP (4 bytes)
        _hexEditor.AddHighlight(packetStart + 16, 4,
            Colors.LightCoral, "Destination IP");

        // Protocol field
        byte protocol = _hexEditor.GetByte(packetStart + 9);

        if (protocol == 6)  // TCP
        {
            HighlightTCPSegment(packetStart + 20);
        }
        else if (protocol == 17)  // UDP
        {
            HighlightUDPSegment(packetStart + 20);
        }
    }

    private void HighlightTCPSegment(long segmentStart)
    {
        // TCP Header (20 bytes minimum)
        _hexEditor.AddHighlight(segmentStart, 20,
            Colors.LightYellow, "TCP Header");

        // Source Port
        _hexEditor.AddHighlight(segmentStart, 2,
            Colors.Cyan, "Source Port");

        // Destination Port
        _hexEditor.AddHighlight(segmentStart + 2, 2,
            Colors.Magenta, "Destination Port");
    }

    private void HighlightUDPSegment(long segmentStart)
    {
        // UDP Header (8 bytes)
        _hexEditor.AddHighlight(segmentStart, 8,
            Colors.LightYellow, "UDP Header");
    }
}
```

---

### 3. Image Format Analysis

```csharp
// Highlight image file structures
public class ImageHighlighter
{
    private HexEditor _hexEditor;

    public ImageHighlighter(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    // Highlight PNG chunks
    public void HighlightPNGChunks()
    {
        long pos = 8;  // Skip PNG signature

        while (pos < _hexEditor.Length - 12)
        {
            // Read chunk length
            byte[] lengthBytes = _hexEditor.GetBytes(pos, 4);
            uint chunkLength = BitConverter.ToUInt32(lengthBytes.Reverse().ToArray(), 0);

            // Read chunk type
            byte[] typeBytes = _hexEditor.GetBytes(pos + 4, 4);
            string chunkType = Encoding.ASCII.GetString(typeBytes);

            // Choose color based on chunk type
            Color color = chunkType switch
            {
                "IHDR" => Colors.LightBlue,
                "PLTE" => Colors.LightGreen,
                "IDAT" => Colors.LightYellow,
                "IEND" => Colors.LightCoral,
                _ => Colors.LightGray
            };

            // Highlight entire chunk (length + type + data + CRC)
            long totalChunkSize = 12 + chunkLength;
            _hexEditor.AddHighlight(pos, totalChunkSize,
                color, $"PNG Chunk: {chunkType}");

            pos += totalChunkSize;
        }

        Console.WriteLine("PNG chunks highlighted");
    }
}
```

---

### 4. Code Coverage Visualization

```csharp
// Highlight executed code regions (for binary analysis)
public class CodeCoverageVisualizer
{
    private HexEditor _hexEditor;
    private HashSet<long> _executedAddresses = new();

    public CodeCoverageVisualizer(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    // Mark address as executed
    public void MarkExecuted(long address)
    {
        _executedAddresses.Add(address);
    }

    // Visualize coverage
    public void VisualizeCoverage()
    {
        _hexEditor.ClearHighlights();

        // Group consecutive executed addresses
        var sortedAddresses = _executedAddresses.OrderBy(a => a).ToList();

        if (sortedAddresses.Count == 0)
            return;

        long rangeStart = sortedAddresses[0];
        long rangeEnd = sortedAddresses[0];

        for (int i = 1; i < sortedAddresses.Count; i++)
        {
            long addr = sortedAddresses[i];

            if (addr == rangeEnd + 1)
            {
                // Consecutive - extend range
                rangeEnd = addr;
            }
            else
            {
                // Gap - create highlight for previous range
                _hexEditor.AddHighlight(rangeStart, rangeEnd - rangeStart + 1,
                    Colors.LightGreen, "Executed");

                rangeStart = addr;
                rangeEnd = addr;
            }
        }

        // Final range
        _hexEditor.AddHighlight(rangeStart, rangeEnd - rangeStart + 1,
            Colors.LightGreen, "Executed");

        Console.WriteLine($"Code coverage: {sortedAddresses.Count} addresses executed");
    }
}
```

---

## ⚠️ Important Notes

### Overlapping Highlights

- Multiple highlights can overlap
- Later highlights drawn on top
- Use transparency for better visibility

### Performance

- Each highlight = visual rendering cost
- 1,000+ highlights may slow rendering
- Group adjacent ranges when possible

### Highlight Persistence

- Highlights **not saved to file** by default
- Use SaveState()/LoadState() to persist
- Or export/import manually

---

## 🔗 See Also

- **[AddBookmark()](bookmarks.md)** - Mark single positions (different from ranges)
- **[FindAll()](../search-operations/findall.md)** - Find patterns to highlight
- **[SetPosition()](../navigation/setposition.md)** - Navigate to highlighted region

---

**Last Updated**: 2026-02-19
**Version**: V2.0
