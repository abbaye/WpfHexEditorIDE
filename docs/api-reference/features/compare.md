# Compare Operations

Compare two binary files and analyze differences.

---

## 📋 Description

The **Compare** feature allows you to compare two binary files byte-by-byte and visualize differences. This is essential for binary diff analysis, version comparison, and data validation.

**Key characteristics**:
- 🔍 **Byte-by-byte comparison** - precise diff detection
- 📊 **Statistics** - count differences, percentage, regions
- 🎨 **Visual highlighting** - mark differences in both files
- ⚡ **Fast comparison** - optimized for large files
- 📝 **Diff export** - save comparison results

**Use Cases**: Version control, patch analysis, data integrity verification, forensics.

---

## 📝 API Methods

```csharp
// Compare files
public CompareResult CompareFiles(string file1, string file2)
public CompareResult CompareFiles(HexEditor hexEditor1, HexEditor hexEditor2)

// Compare in memory
public CompareResult CompareBytes(byte[] bytes1, byte[] bytes2)
public CompareResult CompareStreams(Stream stream1, Stream stream2)

// Get differences
public List<DiffRegion> GetDifferences()
public List<DiffRegion> GetDifferences(long maxDifferences)

// Comparison options
public CompareOptions Options { get; set; }

// Navigate differences
public void NavigateToNextDifference()
public void NavigateToPreviousDifference()
public void NavigateToFirstDifference()
```

**Since:** V1.0 (Optimized performance)

---

## 🎯 Examples

### Example 1: Basic File Comparison

```csharp
using WpfHexaEditor;

// Compare two files
var result = hexEditor.CompareFiles("original.bin", "modified.bin");

Console.WriteLine($"Files are {(result.AreIdentical ? "identical" : "different")}");
Console.WriteLine($"Total differences: {result.DifferenceCount}");
Console.WriteLine($"Difference percentage: {result.DifferencePercentage:F2}%");
Console.WriteLine($"Total bytes compared: {result.BytesCompared}");
```

---

### Example 2: Side-by-Side Comparison UI

```csharp
public partial class CompareWindow : Window
{
    private HexEditor _hexEditor1;
    private HexEditor _hexEditor2;
    private CompareResult _compareResult;

    public CompareWindow()
    {
        InitializeComponent();

        _hexEditor1 = new HexEditor();
        _hexEditor2 = new HexEditor();

        leftPanel.Content = _hexEditor1;
        rightPanel.Content = _hexEditor2;

        // Compare button
        compareButton.Click += CompareButton_Click;

        // Navigation buttons
        nextDiffButton.Click += (s, e) => NavigateToNextDifference();
        prevDiffButton.Click += (s, e) => NavigateToPreviousDifference();

        // Export button
        exportButton.Click += ExportButton_Click;
    }

    private void CompareButton_Click(object sender, RoutedEventArgs e)
    {
        // Open file dialogs
        var dialog1 = new OpenFileDialog { Title = "Select File 1" };
        var dialog2 = new OpenFileDialog { Title = "Select File 2" };

        if (dialog1.ShowDialog() == true && dialog2.ShowDialog() == true)
        {
            // Load files
            _hexEditor1.FileName = dialog1.FileName;
            _hexEditor2.FileName = dialog2.FileName;

            // Perform comparison
            PerformComparison();
        }
    }

    private void PerformComparison()
    {
        progressBar.Visibility = Visibility.Visible;
        statusLabel.Text = "Comparing files...";

        // Compare
        _compareResult = _hexEditor1.CompareFiles(_hexEditor1, _hexEditor2);

        // Highlight differences
        HighlightDifferences();

        // Update UI
        UpdateComparisonResults();

        progressBar.Visibility = Visibility.Collapsed;
        statusLabel.Text = "Comparison complete";
    }

    private void HighlightDifferences()
    {
        // Clear existing highlights
        _hexEditor1.ClearHighlights();
        _hexEditor2.ClearHighlights();

        var differences = _compareResult.GetDifferences(1000);  // Limit to first 1000

        foreach (var diff in differences)
        {
            _hexEditor1.AddHighlight(diff.Position, diff.Length,
                Colors.LightCoral, "Different");
            _hexEditor2.AddHighlight(diff.Position, diff.Length,
                Colors.LightCoral, "Different");
        }
    }

    private void UpdateComparisonResults()
    {
        resultsTextBox.Text = $"Comparison Results:\n\n" +
            $"File 1: {_hexEditor1.FileName}\n" +
            $"Size: {_hexEditor1.Length:N0} bytes\n\n" +
            $"File 2: {_hexEditor2.FileName}\n" +
            $"Size: {_hexEditor2.Length:N0} bytes\n\n" +
            $"Status: {(_compareResult.AreIdentical ? "✓ Identical" : "✗ Different")}\n" +
            $"Differences: {_compareResult.DifferenceCount:N0} bytes ({_compareResult.DifferencePercentage:F2}%)\n" +
            $"Same: {_compareResult.SameCount:N0} bytes ({_compareResult.SamePercentage:F2}%)\n" +
            $"Diff regions: {_compareResult.DifferenceRegions.Count}";

        // Enable navigation if differences exist
        nextDiffButton.IsEnabled = (_compareResult.DifferenceCount > 0);
        prevDiffButton.IsEnabled = (_compareResult.DifferenceCount > 0);
    }

    private void NavigateToNextDifference()
    {
        var differences = _compareResult.GetDifferences();

        if (differences.Count == 0)
            return;

        // Find next difference after current position
        long currentPos = _hexEditor1.Position;
        var next = differences.FirstOrDefault(d => d.Position > currentPos);

        if (next != null)
        {
            _hexEditor1.SetPosition(next.Position);
            _hexEditor2.SetPosition(next.Position);
        }
        else
        {
            // Wrap to first
            _hexEditor1.SetPosition(differences[0].Position);
            _hexEditor2.SetPosition(differences[0].Position);
        }
    }

    private void NavigateToPreviousDifference()
    {
        var differences = _compareResult.GetDifferences();

        if (differences.Count == 0)
            return;

        // Find previous difference before current position
        long currentPos = _hexEditor1.Position;
        var prev = differences.LastOrDefault(d => d.Position < currentPos);

        if (prev != null)
        {
            _hexEditor1.SetPosition(prev.Position);
            _hexEditor2.SetPosition(prev.Position);
        }
        else
        {
            // Wrap to last
            _hexEditor1.SetPosition(differences[differences.Count - 1].Position);
            _hexEditor2.SetPosition(differences[differences.Count - 1].Position);
        }
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Text Files (*.txt)|*.txt|CSV Files (*.csv)|*.csv",
            Title = "Export Comparison Results"
        };

        if (dialog.ShowDialog() == true)
        {
            ExportComparisonResults(dialog.FileName);
            MessageBox.Show($"Results exported to {dialog.FileName}", "Success");
        }
    }

    private void ExportComparisonResults(string filePath)
    {
        using (var writer = new StreamWriter(filePath))
        {
            writer.WriteLine("Binary File Comparison Report");
            writer.WriteLine($"Generated: {DateTime.Now}");
            writer.WriteLine();
            writer.WriteLine($"File 1: {_hexEditor1.FileName}");
            writer.WriteLine($"Size: {_hexEditor1.Length:N0} bytes");
            writer.WriteLine();
            writer.WriteLine($"File 2: {_hexEditor2.FileName}");
            writer.WriteLine($"Size: {_hexEditor2.Length:N0} bytes");
            writer.WriteLine();
            writer.WriteLine($"Status: {(_compareResult.AreIdentical ? "IDENTICAL" : "DIFFERENT")}");
            writer.WriteLine($"Differences: {_compareResult.DifferenceCount:N0} bytes ({_compareResult.DifferencePercentage:F2}%)");
            writer.WriteLine();
            writer.WriteLine("Difference Regions:");

            foreach (var region in _compareResult.DifferenceRegions)
            {
                writer.WriteLine($"  0x{region.Position:X8} - 0x{(region.Position + region.Length):X8} ({region.Length} bytes)");
            }
        }
    }
}
```

---

### Example 3: Diff Statistics

```csharp
// Analyze comparison statistics
public class ComparisonAnalyzer
{
    public static void AnalyzeComparison(CompareResult result)
    {
        Console.WriteLine("=== Comparison Statistics ===");
        Console.WriteLine($"Total bytes: {result.BytesCompared:N0}");
        Console.WriteLine($"Identical bytes: {result.SameCount:N0} ({result.SamePercentage:F2}%)");
        Console.WriteLine($"Different bytes: {result.DifferenceCount:N0} ({result.DifferencePercentage:F2}%)");
        Console.WriteLine();

        if (result.File1Size != result.File2Size)
        {
            Console.WriteLine($"⚠️ File size mismatch:");
            Console.WriteLine($"  File 1: {result.File1Size:N0} bytes");
            Console.WriteLine($"  File 2: {result.File2Size:N0} bytes");
            Console.WriteLine($"  Difference: {Math.Abs(result.File1Size - result.File2Size):N0} bytes");
            Console.WriteLine();
        }

        Console.WriteLine($"Difference regions: {result.DifferenceRegions.Count}");

        if (result.DifferenceRegions.Count > 0)
        {
            var avgRegionSize = result.DifferenceCount / (double)result.DifferenceRegions.Count;
            Console.WriteLine($"Average region size: {avgRegionSize:F2} bytes");

            var largestRegion = result.DifferenceRegions.OrderByDescending(r => r.Length).First();
            Console.WriteLine($"Largest region: {largestRegion.Length} bytes at 0x{largestRegion.Position:X}");

            var smallestRegion = result.DifferenceRegions.OrderBy(r => r.Length).First();
            Console.WriteLine($"Smallest region: {smallestRegion.Length} bytes at 0x{smallestRegion.Position:X}");
        }

        Console.WriteLine();
        Console.WriteLine($"Comparison time: {result.ComparisonTime.TotalMilliseconds:F0}ms");
    }
}

// Usage
var result = hexEditor.CompareFiles("v1.bin", "v2.bin");
ComparisonAnalyzer.AnalyzeComparison(result);
```

---

### Example 4: Patch Generator

```csharp
// Generate patch file from comparison
public class PatchGenerator
{
    public static void GeneratePatch(CompareResult comparison, string outputPath)
    {
        var differences = comparison.GetDifferences();

        using (var writer = new BinaryWriter(File.Create(outputPath)))
        {
            // Write patch header
            writer.Write(Encoding.ASCII.GetBytes("PATCH"));
            writer.Write((byte)1);  // Version
            writer.Write(differences.Count);

            // Write each difference
            foreach (var diff in differences)
            {
                writer.Write(diff.Position);  // Offset
                writer.Write(diff.Length);    // Length
                writer.Write(diff.File2Data); // New data
            }
        }

        Console.WriteLine($"Patch file created: {outputPath}");
        Console.WriteLine($"Contains {differences.Count} changes");
    }

    public static void ApplyPatch(HexEditor hexEditor, string patchPath)
    {
        using (var reader = new BinaryReader(File.OpenRead(patchPath)))
        {
            // Read header
            string magic = Encoding.ASCII.GetString(reader.ReadBytes(5));
            if (magic != "PATCH")
            {
                throw new Exception("Invalid patch file");
            }

            byte version = reader.ReadByte();
            int changeCount = reader.ReadInt32();

            Console.WriteLine($"Applying patch: {changeCount} changes");

            hexEditor.BeginBatch();

            try
            {
                for (int i = 0; i < changeCount; i++)
                {
                    long offset = reader.ReadInt64();
                    int length = reader.ReadInt32();
                    byte[] data = reader.ReadBytes(length);

                    // Apply change
                    hexEditor.ModifyBytes(offset, data);
                }
            }
            finally
            {
                hexEditor.EndBatch();
            }

            Console.WriteLine("Patch applied successfully");
        }
    }
}

// Usage: Create and apply patches
var comparison = hexEditor1.CompareFiles(hexEditor1, hexEditor2);

// Generate patch
PatchGenerator.GeneratePatch(comparison, "changes.patch");

// Apply patch to file
hexEditor.FileName = "original.bin";
PatchGenerator.ApplyPatch(hexEditor, "changes.patch");
hexEditor.Save();
```

---

### Example 5: Incremental Comparison

```csharp
// Compare files in chunks for progress reporting
public class IncrementalComparer
{
    public async Task<CompareResult> CompareWithProgressAsync(
        string file1,
        string file2,
        IProgress<double> progress)
    {
        var result = new CompareResult();

        using (var stream1 = File.OpenRead(file1))
        using (var stream2 = File.OpenRead(file2))
        {
            result.File1Size = stream1.Length;
            result.File2Size = stream2.Length;

            long minLength = Math.Min(stream1.Length, stream2.Length);
            const int chunkSize = 1_000_000;  // 1 MB chunks

            long bytesCompared = 0;
            byte[] buffer1 = new byte[chunkSize];
            byte[] buffer2 = new byte[chunkSize];

            while (bytesCompared < minLength)
            {
                int toRead = (int)Math.Min(chunkSize, minLength - bytesCompared);

                // Read chunks
                await stream1.ReadAsync(buffer1, 0, toRead);
                await stream2.ReadAsync(buffer2, 0, toRead);

                // Compare chunk
                for (int i = 0; i < toRead; i++)
                {
                    if (buffer1[i] != buffer2[i])
                    {
                        result.DifferenceCount++;

                        // Track region (simplified)
                        result.DifferenceRegions.Add(new DiffRegion
                        {
                            Position = bytesCompared + i,
                            Length = 1,
                            File1Data = new[] { buffer1[i] },
                            File2Data = new[] { buffer2[i] }
                        });
                    }
                    else
                    {
                        result.SameCount++;
                    }
                }

                bytesCompared += toRead;

                // Report progress
                double percent = (bytesCompared * 100.0) / minLength;
                progress?.Report(percent);

                // Allow cancellation
                await Task.Delay(1);
            }

            result.BytesCompared = bytesCompared;
            result.AreIdentical = (result.DifferenceCount == 0 &&
                                  stream1.Length == stream2.Length);
        }

        return result;
    }
}

// Usage
var comparer = new IncrementalComparer();

progressBar.Visibility = Visibility.Visible;

var progress = new Progress<double>(p =>
{
    progressBar.Value = p;
    statusLabel.Text = $"Comparing: {p:F1}%";
});

var result = await comparer.CompareWithProgressAsync("file1.bin", "file2.bin", progress);

progressBar.Visibility = Visibility.Collapsed;

MessageBox.Show(
    $"Comparison complete\n" +
    $"Differences: {result.DifferenceCount}",
    "Done");
```

---

### Example 6: Ignore Regions

```csharp
// Compare files but ignore specific regions
public class SelectiveComparer
{
    public CompareResult CompareWithIgnoreRegions(
        HexEditor hex1,
        HexEditor hex2,
        List<(long start, long length)> ignoreRegions)
    {
        var result = new CompareResult();

        long minLength = Math.Min(hex1.Length, hex2.Length);

        for (long pos = 0; pos < minLength; pos++)
        {
            // Check if position is in ignore region
            bool shouldIgnore = ignoreRegions.Any(region =>
                pos >= region.start && pos < region.start + region.length);

            if (shouldIgnore)
            {
                continue;  // Skip comparison for this byte
            }

            // Compare bytes
            byte byte1 = hex1.GetByte(pos);
            byte byte2 = hex2.GetByte(pos);

            if (byte1 != byte2)
            {
                result.DifferenceCount++;
            }
            else
            {
                result.SameCount++;
            }

            result.BytesCompared++;
        }

        result.AreIdentical = (result.DifferenceCount == 0);

        return result;
    }
}

// Usage: Compare but ignore file headers
var ignoreRegions = new List<(long, long)>
{
    (0, 256),      // Ignore first 256 bytes (header)
    (0x1000, 64)   // Ignore 64 bytes at 0x1000 (timestamp)
};

var comparer = new SelectiveComparer();
var result = comparer.CompareWithIgnoreRegions(hexEditor1, hexEditor2, ignoreRegions);

Console.WriteLine($"Comparison (with ignored regions): {result.DifferenceCount} differences");
```

---

### Example 7: Visual Diff Viewer

```csharp
// Create visual diff representation
public class DiffVisualizer
{
    public static string CreateVisualDiff(
        HexEditor hex1,
        HexEditor hex2,
        long position,
        int contextLines = 3)
    {
        var sb = new StringBuilder();
        int bytesPerLine = 16;

        long startLine = Math.Max(0, position / bytesPerLine - contextLines);
        long endLine = Math.Min(
            Math.Max(hex1.Length, hex2.Length) / bytesPerLine,
            position / bytesPerLine + contextLines);

        for (long line = startLine; line <= endLine; line++)
        {
            long linePos = line * bytesPerLine;

            // Read bytes from both files
            byte[] bytes1 = ReadLineBytes(hex1, linePos, bytesPerLine);
            byte[] bytes2 = ReadLineBytes(hex2, linePos, bytesPerLine);

            // Check if lines differ
            bool differ = !bytes1.SequenceEqual(bytes2);

            // Format line
            string hex1Str = FormatHexLine(bytes1);
            string hex2Str = FormatHexLine(bytes2);

            if (differ)
            {
                sb.AppendLine($"0x{linePos:X8} | -{hex1Str}");
                sb.AppendLine($"           | +{hex2Str}");
            }
            else
            {
                sb.AppendLine($"0x{linePos:X8} |  {hex1Str}");
            }
        }

        return sb.ToString();
    }

    private static byte[] ReadLineBytes(HexEditor hex, long position, int count)
    {
        if (position >= hex.Length)
            return new byte[0];

        int actualCount = (int)Math.Min(count, hex.Length - position);
        return hex.GetBytes(position, actualCount);
    }

    private static string FormatHexLine(byte[] bytes)
    {
        return string.Join(" ", bytes.Select(b => $"{b:X2}"));
    }
}

// Usage: Show diff around position 0x1000
string visualDiff = DiffVisualizer.CreateVisualDiff(hexEditor1, hexEditor2, 0x1000);
Console.WriteLine(visualDiff);

// Output example:
// 0x00000FF0 |  48 65 6C 6C 6F 20 57 6F 72 6C 64 00 00 00 00 00
// 0x00001000 | -DE AD BE EF CA FE BA BE 01 02 03 04 05 06 07 08
//            | +DE AD BE EF 00 00 00 00 01 02 03 04 05 06 07 08
// 0x00001010 |  AA BB CC DD EE FF 11 22 33 44 55 66 77 88 99 00
```

---

### Example 8: Batch File Comparison

```csharp
// Compare multiple file pairs
public class BatchComparer
{
    public List<CompareResult> CompareMultipleFilePairs(
        List<(string file1, string file2)> filePairs)
    {
        var results = new List<CompareResult>();

        foreach (var (file1, file2) in filePairs)
        {
            Console.WriteLine($"Comparing: {Path.GetFileName(file1)} vs {Path.GetFileName(file2)}");

            try
            {
                var hex1 = new HexEditor();
                var hex2 = new HexEditor();

                hex1.FileName = file1;
                hex2.FileName = file2;

                var result = hex1.CompareFiles(hex1, hex2);
                result.File1Name = file1;
                result.File2Name = file2;

                results.Add(result);

                string status = result.AreIdentical ? "✓ Identical" : $"✗ {result.DifferenceCount} diffs";
                Console.WriteLine($"  {status}");

                hex1.Close();
                hex2.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error: {ex.Message}");
            }
        }

        return results;
    }

    public void ExportBatchResults(List<CompareResult> results, string outputPath)
    {
        using (var writer = new StreamWriter(outputPath))
        {
            writer.WriteLine("Batch Comparison Report");
            writer.WriteLine($"Generated: {DateTime.Now}");
            writer.WriteLine($"Total pairs: {results.Count}");
            writer.WriteLine();

            foreach (var result in results)
            {
                writer.WriteLine($"File 1: {result.File1Name}");
                writer.WriteLine($"File 2: {result.File2Name}");
                writer.WriteLine($"Status: {(result.AreIdentical ? "IDENTICAL" : "DIFFERENT")}");
                writer.WriteLine($"Differences: {result.DifferenceCount} ({result.DifferencePercentage:F2}%)");
                writer.WriteLine();
            }
        }

        Console.WriteLine($"Batch results exported to {outputPath}");
    }
}

// Usage: Compare multiple versions
var filePairs = new List<(string, string)>
{
    ("v1.0.bin", "v1.1.bin"),
    ("v1.1.bin", "v1.2.bin"),
    ("v1.2.bin", "v2.0.bin")
};

var batchComparer = new BatchComparer();
var results = batchComparer.CompareMultipleFilePairs(filePairs);

batchComparer.ExportBatchResults(results, "comparison_report.txt");
```

---

## 💡 Use Cases

### 1. Version Control / Change Detection

Track changes between software versions.

### 2. Data Integrity Verification

Verify file integrity after transfer.

### 3. Malware Analysis

Compare clean vs infected files.

### 4. Forensic Investigation

Detect tampering in evidence files.

---

## ⚠️ Important Notes

### Memory Usage

- Comparison loads both files into memory
- For files > 1 GB, use streaming comparison
- Difference list can be large for very different files

### Performance

```
Comparison Performance:
  10 MB:   ~50ms
  100 MB:  ~500ms
  1 GB:    ~5000ms
```

### Thread Safety

- ❌ Not thread-safe
- Use async methods for large files

---

## 🔗 See Also

- **[GetByte()](../byte-operations/getbyte.md)** - Read bytes for comparison
- **[AddHighlight()](highlights.md)** - Highlight differences visually
- **[FindAll()](../search-operations/findall.md)** - Find patterns

---

**Last Updated**: 2026-02-19
**Version**: 2.0 (Optimized Performance)
