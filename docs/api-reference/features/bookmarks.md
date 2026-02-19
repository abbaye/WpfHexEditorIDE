# Bookmark Operations

Manage bookmarks to mark and navigate to important file positions.

---

## 📋 Description

Bookmarks allow you to mark specific positions in the file for quick navigation and reference. WPF HexEditor V2 provides a complete bookmark system with add, remove, clear, and navigation capabilities.

**Key characteristics**:
- 📌 **Mark important positions** with descriptive labels
- 🔍 **Quick navigation** to bookmarked locations
- 💾 **Persistent** - saved with file state
- ✅ **Visual indicators** in hex view
- 🎨 **Customizable colors** per bookmark

---

## 📝 API Methods

```csharp
// Add bookmark
public void AddBookmark(long position, string description)
public void AddBookmark(long position, string description, Color color)

// Remove bookmark
public void RemoveBookmark(long position)
public bool RemoveBookmarkByDescription(string description)

// Get bookmarks
public List<Bookmark> GetBookmarks()
public Bookmark GetBookmarkAt(long position)

// Navigation
public void NavigateToBookmark(Bookmark bookmark)
public void NavigateToNextBookmark()
public void NavigateToPreviousBookmark()

// Clear
public void ClearBookmarks()

// Check existence
public bool HasBookmarkAt(long position)
public int BookmarkCount { get; }
```

**Since:** V1.0 (V2 added color support)

---

## 🎯 Examples

### Example 1: Add Bookmarks

```csharp
using WpfHexaEditor;

// Add bookmark at current position
hexEditor.AddBookmark(hexEditor.Position, "Current location");

// Add bookmark at specific positions
hexEditor.AddBookmark(0x0000, "File Header");
hexEditor.AddBookmark(0x1000, "Data Section Start");
hexEditor.AddBookmark(0x5000, "String Table");

Console.WriteLine($"Total bookmarks: {hexEditor.BookmarkCount}");
```

---

### Example 2: Add Bookmarks with Colors

```csharp
// Add bookmarks with different colors for categorization
hexEditor.AddBookmark(0x0000, "PE Header", Colors.LightBlue);
hexEditor.AddBookmark(0x0400, "Import Table", Colors.LightGreen);
hexEditor.AddBookmark(0x1000, "Code Section", Colors.LightYellow);
hexEditor.AddBookmark(0x5000, "Data Section", Colors.LightCoral);

Console.WriteLine("Color-coded bookmarks added");
```

---

### Example 3: Bookmark Navigator UI

```csharp
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Add bookmark button
        addBookmarkButton.Click += AddBookmarkButton_Click;

        // Bookmark list
        bookmarkListBox.SelectionChanged += BookmarkListBox_SelectionChanged;

        // Navigation buttons
        nextBookmarkButton.Click += (s, e) => hexEditor.NavigateToNextBookmark();
        prevBookmarkButton.Click += (s, e) => hexEditor.NavigateToPreviousBookmark();

        // Remove bookmark button
        removeBookmarkButton.Click += RemoveBookmarkButton_Click;

        // Refresh bookmark list
        RefreshBookmarkList();
    }

    private void AddBookmarkButton_Click(object sender, RoutedEventArgs e)
    {
        long position = hexEditor.Position;

        // Show input dialog
        var dialog = new InputDialog
        {
            Title = "Add Bookmark",
            Prompt = $"Description for bookmark at 0x{position:X}:"
        };

        if (dialog.ShowDialog() == true)
        {
            string description = dialog.Value;

            // Add bookmark
            hexEditor.AddBookmark(position, description);

            // Refresh list
            RefreshBookmarkList();

            statusLabel.Text = $"Bookmark added at 0x{position:X}";
        }
    }

    private void BookmarkListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (bookmarkListBox.SelectedItem is Bookmark bookmark)
        {
            // Navigate to selected bookmark
            hexEditor.NavigateToBookmark(bookmark);
        }
    }

    private void RemoveBookmarkButton_Click(object sender, RoutedEventArgs e)
    {
        if (bookmarkListBox.SelectedItem is Bookmark bookmark)
        {
            hexEditor.RemoveBookmark(bookmark.Position);
            RefreshBookmarkList();

            statusLabel.Text = $"Bookmark removed";
        }
    }

    private void RefreshBookmarkList()
    {
        bookmarkListBox.Items.Clear();

        var bookmarks = hexEditor.GetBookmarks();

        foreach (var bookmark in bookmarks)
        {
            bookmarkListBox.Items.Add(
                $"0x{bookmark.Position:X8} - {bookmark.Description}");
        }

        // Update button states
        removeBookmarkButton.IsEnabled = (bookmarkListBox.SelectedIndex >= 0);
        nextBookmarkButton.IsEnabled = (bookmarks.Count > 0);
        prevBookmarkButton.IsEnabled = (bookmarks.Count > 0);
    }
}
```

---

### Example 4: Auto-Bookmark Important Structures

```csharp
// Automatically bookmark file structures
public class StructureBookmarker
{
    private HexEditor _hexEditor;

    public StructureBookmarker(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    // Bookmark PE file structures
    public void BookmarkPEStructures()
    {
        // Check if PE file
        byte[] magic = _hexEditor.GetBytes(0, 2);
        if (magic[0] != 0x4D || magic[1] != 0x5A)  // "MZ"
        {
            MessageBox.Show("Not a PE file", "Error");
            return;
        }

        // DOS Header
        _hexEditor.AddBookmark(0x0000, "DOS Header", Colors.LightBlue);

        // Get PE offset
        byte[] peOffsetBytes = _hexEditor.GetBytes(0x3C, 4);
        int peOffset = BitConverter.ToInt32(peOffsetBytes, 0);

        // PE Header
        _hexEditor.AddBookmark(peOffset, "PE Header", Colors.LightGreen);

        // Optional Header
        _hexEditor.AddBookmark(peOffset + 24, "Optional Header", Colors.LightYellow);

        Console.WriteLine("PE structures bookmarked");
    }

    // Bookmark strings
    public void BookmarkStrings(int minLength = 5)
    {
        var currentString = new List<byte>();
        long stringStart = -1;
        int bookmarkCount = 0;

        for (long i = 0; i < _hexEditor.Length; i++)
        {
            byte b = _hexEditor.GetByte(i);

            if (b >= 32 && b < 127)  // Printable ASCII
            {
                if (stringStart < 0)
                    stringStart = i;

                currentString.Add(b);
            }
            else if (currentString.Count >= minLength)
            {
                // Found string - add bookmark
                string text = Encoding.ASCII.GetString(currentString.ToArray());
                _hexEditor.AddBookmark(stringStart, $"String: {text}", Colors.LightCoral);

                bookmarkCount++;
                currentString.Clear();
                stringStart = -1;
            }
            else
            {
                currentString.Clear();
                stringStart = -1;
            }
        }

        Console.WriteLine($"Bookmarked {bookmarkCount} strings");
    }
}

// Usage
var bookmarker = new StructureBookmarker(hexEditor);

bookmarker.BookmarkPEStructures();
bookmarker.BookmarkStrings();
```

---

### Example 5: Bookmark Manager with Export

```csharp
// Manage bookmarks with import/export
public class BookmarkManager
{
    private HexEditor _hexEditor;

    public BookmarkManager(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    // Export bookmarks to CSV
    public void ExportBookmarks(string filePath)
    {
        var bookmarks = _hexEditor.GetBookmarks();

        using (var writer = new StreamWriter(filePath))
        {
            // Header
            writer.WriteLine("Position,Description,Color");

            // Bookmarks
            foreach (var bookmark in bookmarks)
            {
                string colorHex = bookmark.Color.ToString();
                writer.WriteLine($"0x{bookmark.Position:X},{bookmark.Description},{colorHex}");
            }
        }

        Console.WriteLine($"Exported {bookmarks.Count} bookmarks to {filePath}");
    }

    // Import bookmarks from CSV
    public void ImportBookmarks(string filePath)
    {
        int imported = 0;

        using (var reader = new StreamReader(filePath))
        {
            // Skip header
            reader.ReadLine();

            while (!reader.EndOfStream)
            {
                string line = reader.ReadLine();
                string[] parts = line.Split(',');

                if (parts.Length >= 2)
                {
                    // Parse position
                    long position = Convert.ToInt64(parts[0].Replace("0x", ""), 16);
                    string description = parts[1];

                    // Parse color (if present)
                    Color color = Colors.Yellow;
                    if (parts.Length >= 3)
                    {
                        try
                        {
                            color = (Color)ColorConverter.ConvertFromString(parts[2]);
                        }
                        catch { }
                    }

                    // Add bookmark
                    _hexEditor.AddBookmark(position, description, color);
                    imported++;
                }
            }
        }

        Console.WriteLine($"Imported {imported} bookmarks");
    }

    // Find bookmarks by text search
    public List<Bookmark> SearchBookmarks(string searchText)
    {
        var bookmarks = _hexEditor.GetBookmarks();

        return bookmarks.Where(b =>
            b.Description.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    // Sort bookmarks by position
    public List<Bookmark> GetSortedBookmarks()
    {
        return _hexEditor.GetBookmarks()
            .OrderBy(b => b.Position)
            .ToList();
    }
}

// Usage
var manager = new BookmarkManager(hexEditor);

// Export
manager.ExportBookmarks("bookmarks.csv");

// Import
manager.ImportBookmarks("bookmarks.csv");

// Search
var results = manager.SearchBookmarks("header");
Console.WriteLine($"Found {results.Count} bookmarks matching 'header'");
```

---

### Example 6: Navigate Between Bookmarks

```csharp
// Bookmark navigation controls
public class BookmarkNavigator
{
    private HexEditor _hexEditor;
    private int _currentBookmarkIndex = -1;

    public BookmarkNavigator(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    public void NavigateToNext()
    {
        var bookmarks = _hexEditor.GetBookmarks()
            .OrderBy(b => b.Position)
            .ToList();

        if (bookmarks.Count == 0)
        {
            Console.WriteLine("No bookmarks");
            return;
        }

        _currentBookmarkIndex = (_currentBookmarkIndex + 1) % bookmarks.Count;

        var bookmark = bookmarks[_currentBookmarkIndex];
        _hexEditor.NavigateToBookmark(bookmark);

        Console.WriteLine($"Navigated to bookmark {_currentBookmarkIndex + 1}/{bookmarks.Count}: {bookmark.Description}");
    }

    public void NavigateToPrevious()
    {
        var bookmarks = _hexEditor.GetBookmarks()
            .OrderBy(b => b.Position)
            .ToList();

        if (bookmarks.Count == 0)
        {
            Console.WriteLine("No bookmarks");
            return;
        }

        _currentBookmarkIndex--;
        if (_currentBookmarkIndex < 0)
            _currentBookmarkIndex = bookmarks.Count - 1;

        var bookmark = bookmarks[_currentBookmarkIndex];
        _hexEditor.NavigateToBookmark(bookmark);

        Console.WriteLine($"Navigated to bookmark {_currentBookmarkIndex + 1}/{bookmarks.Count}: {bookmark.Description}");
    }

    public void NavigateToNearest()
    {
        long currentPosition = _hexEditor.Position;
        var bookmarks = _hexEditor.GetBookmarks();

        if (bookmarks.Count == 0)
        {
            Console.WriteLine("No bookmarks");
            return;
        }

        // Find nearest bookmark
        var nearest = bookmarks
            .OrderBy(b => Math.Abs(b.Position - currentPosition))
            .First();

        _hexEditor.NavigateToBookmark(nearest);

        Console.WriteLine($"Navigated to nearest bookmark: {nearest.Description}");
    }
}

// Usage
var navigator = new BookmarkNavigator(hexEditor);

// Keyboard shortcuts
hexEditor.KeyDown += (s, e) =>
{
    if (e.Key == Key.F2)
    {
        navigator.NavigateToNext();
        e.Handled = true;
    }
    else if (e.Key == Key.F2 && Keyboard.Modifiers == ModifierKeys.Shift)
    {
        navigator.NavigateToPrevious();
        e.Handled = true;
    }
    else if (e.Key == Key.B && Keyboard.Modifiers == ModifierKeys.Control)
    {
        navigator.NavigateToNearest();
        e.Handled = true;
    }
};
```

---

### Example 7: Contextual Bookmarks

```csharp
// Add contextual information to bookmarks
public class ContextualBookmark
{
    private HexEditor _hexEditor;

    public ContextualBookmark(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    // Bookmark with byte value context
    public void AddBookmarkWithContext(long position, string description)
    {
        // Get bytes at position for context
        int contextLength = Math.Min(16, (int)(_hexEditor.Length - position));
        byte[] context = _hexEditor.GetBytes(position, contextLength);

        string contextHex = BitConverter.ToString(context).Replace("-", " ");

        // Add bookmark with context in description
        string fullDescription = $"{description} [{contextHex}]";
        _hexEditor.AddBookmark(position, fullDescription);

        Console.WriteLine($"Bookmark added: {fullDescription}");
    }

    // Auto-describe bookmark based on content
    public void AddSmartBookmark(long position)
    {
        byte[] bytes = _hexEditor.GetBytes(position, 4);

        string description = "Unknown";

        // Try to identify content
        if (bytes[0] == 0x4D && bytes[1] == 0x5A)
        {
            description = "PE Executable Header (MZ)";
        }
        else if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            description = "JPEG Image";
        }
        else if (bytes[0] == 0x89 && bytes[1] == 0x50)
        {
            description = "PNG Image";
        }
        else if (bytes[0] == 0x50 && bytes[1] == 0x4B)
        {
            description = "ZIP Archive";
        }

        _hexEditor.AddBookmark(position, description, Colors.Yellow);

        Console.WriteLine($"Smart bookmark: {description}");
    }
}
```

---

### Example 8: Bookmark Diff Tracking

```csharp
// Track changes to bookmarked locations
public class BookmarkDiffTracker
{
    private HexEditor _hexEditor;
    private Dictionary<long, byte[]> _bookmarkSnapshots = new();

    public BookmarkDiffTracker(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    public void CaptureBookmarkSnapshots()
    {
        _bookmarkSnapshots.Clear();

        var bookmarks = _hexEditor.GetBookmarks();

        foreach (var bookmark in bookmarks)
        {
            // Capture 16 bytes at bookmark position
            int length = Math.Min(16, (int)(_hexEditor.Length - bookmark.Position));
            byte[] snapshot = _hexEditor.GetBytes(bookmark.Position, length);

            _bookmarkSnapshots[bookmark.Position] = snapshot;
        }

        Console.WriteLine($"Captured snapshots for {bookmarks.Count} bookmarks");
    }

    public void CompareBookmarks()
    {
        Console.WriteLine("Bookmark Diff Report:");

        foreach (var (position, originalSnapshot) in _bookmarkSnapshots)
        {
            int length = originalSnapshot.Length;
            byte[] currentBytes = _hexEditor.GetBytes(position, length);

            bool changed = !originalSnapshot.SequenceEqual(currentBytes);

            if (changed)
            {
                Console.WriteLine($"  ⚠️ CHANGED at 0x{position:X}");
                Console.WriteLine($"     Before: {BitConverter.ToString(originalSnapshot)}");
                Console.WriteLine($"     After:  {BitConverter.ToString(currentBytes)}");
            }
            else
            {
                Console.WriteLine($"  ✓ Unchanged at 0x{position:X}");
            }
        }
    }
}

// Usage: Track changes to important locations
var tracker = new BookmarkDiffTracker(hexEditor);

// Add bookmarks
hexEditor.AddBookmark(0x0000, "Header");
hexEditor.AddBookmark(0x1000, "Data");

// Capture snapshots
tracker.CaptureBookmarkSnapshots();

// Make edits...
hexEditor.ModifyByte(0xFF, 0x0000);

// Compare
tracker.CompareBookmarks();
```

---

## 💡 Use Cases

### 1. Binary File Analysis

```csharp
// Bookmark important structures during analysis
public class BinaryAnalyzer
{
    private HexEditor _hexEditor;

    public BinaryAnalyzer(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    public void AnalyzeAndBookmark()
    {
        Console.WriteLine("Analyzing file structure...");

        // Identify and bookmark file signature
        byte[] magic = _hexEditor.GetBytes(0, 4);
        string fileType = IdentifyFileType(magic);
        _hexEditor.AddBookmark(0, $"File Signature: {fileType}", Colors.LightBlue);

        // Find and bookmark text strings
        BookmarkTextStrings();

        // Find and bookmark repeated patterns
        BookmarkRepeatedPatterns();

        // Find and bookmark alignment boundaries
        BookmarkAlignmentBoundaries();

        var bookmarks = _hexEditor.GetBookmarks();
        Console.WriteLine($"Analysis complete: {bookmarks.Count} bookmarks added");
    }

    private string IdentifyFileType(byte[] magic)
    {
        if (magic[0] == 0x4D && magic[1] == 0x5A)
            return "PE Executable";
        else if (magic[0] == 0xFF && magic[1] == 0xD8)
            return "JPEG Image";
        else if (magic[0] == 0x89 && magic[1] == 0x50)
            return "PNG Image";
        else
            return "Unknown";
    }

    private void BookmarkTextStrings()
    {
        // Implementation...
    }

    private void BookmarkRepeatedPatterns()
    {
        // Implementation...
    }

    private void BookmarkAlignmentBoundaries()
    {
        // Bookmark every 4KB boundary
        for (long pos = 0; pos < _hexEditor.Length; pos += 4096)
        {
            if (pos > 0)
            {
                _hexEditor.AddBookmark(pos, $"4KB Boundary", Colors.LightGray);
            }
        }
    }
}
```

---

### 2. Code Navigation in Binary

```csharp
// Bookmark function entry points
public class FunctionBookmarker
{
    private HexEditor _hexEditor;

    // Common function prologue patterns
    private List<byte[]> _functionPrologues = new()
    {
        new byte[] { 0x55, 0x89, 0xE5 },  // push ebp; mov ebp, esp
        new byte[] { 0x55, 0x8B, 0xEC },  // push ebp; mov ebp, esp (alternative)
        new byte[] { 0x48, 0x89, 0x5C, 0x24 }  // mov [rsp+...], rbx (x64)
    };

    public FunctionBookmarker(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    public void BookmarkFunctionEntryPoints()
    {
        int functionsFound = 0;

        foreach (var prologue in _functionPrologues)
        {
            var positions = _hexEditor.FindAll(prologue);

            foreach (var pos in positions)
            {
                _hexEditor.AddBookmark(pos, $"Function Entry", Colors.LightGreen);
                functionsFound++;
            }
        }

        Console.WriteLine($"Bookmarked {functionsFound} potential function entry points");
    }
}
```

---

### 3. Game ROM Modding

```csharp
// Bookmark game data structures for ROM hacking
public class GameROMBookmarker
{
    private HexEditor _hexEditor;

    public GameROMBookmarker(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    public void BookmarkGameStructures()
    {
        // Bookmark ROM header
        _hexEditor.AddBookmark(0x0000, "ROM Header", Colors.LightBlue);

        // Find and bookmark text strings
        BookmarkDialogueStrings();

        // Find and bookmark level data
        BookmarkLevelData();

        // Find and bookmark sprite data
        BookmarkSpriteData();

        Console.WriteLine("Game structures bookmarked");
    }

    private void BookmarkDialogueStrings()
    {
        // Search for dialogue markers
        byte[] dialogueMarker = Encoding.ASCII.GetBytes("<MSG>");
        var positions = _hexEditor.FindAll(dialogueMarker);

        foreach (var pos in positions)
        {
            _hexEditor.AddBookmark(pos, "Dialogue Text", Colors.LightYellow);
        }
    }

    private void BookmarkLevelData()
    {
        // Level data often at fixed offsets
        long[] levelOffsets = { 0x10000, 0x20000, 0x30000 };

        for (int i = 0; i < levelOffsets.Length; i++)
        {
            _hexEditor.AddBookmark(levelOffsets[i], $"Level {i + 1} Data", Colors.LightGreen);
        }
    }

    private void BookmarkSpriteData()
    {
        // Sprite data bookmarks
        // Implementation...
    }
}
```

---

### 4. Forensic Analysis

```csharp
// Bookmark evidence locations for forensic analysis
public class ForensicBookmarker
{
    private HexEditor _hexEditor;

    public ForensicBookmarker(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    public void BookmarkEvidenceLocations()
    {
        // Find and bookmark file signatures (carved files)
        BookmarkFileSignatures();

        // Find and bookmark URLs
        BookmarkURLs();

        // Find and bookmark email addresses
        BookmarkEmailAddresses();

        // Find and bookmark timestamps
        BookmarkTimestamps();

        Console.WriteLine("Evidence locations bookmarked");
    }

    private void BookmarkFileSignatures()
    {
        // Common file signatures
        var signatures = new Dictionary<string, byte[]>
        {
            { "JPEG", new byte[] { 0xFF, 0xD8, 0xFF } },
            { "PNG", new byte[] { 0x89, 0x50, 0x4E, 0x47 } },
            { "PDF", new byte[] { 0x25, 0x50, 0x44, 0x46 } }
        };

        foreach (var (fileType, signature) in signatures)
        {
            var positions = _hexEditor.FindAll(signature);

            foreach (var pos in positions)
            {
                _hexEditor.AddBookmark(pos, $"Embedded {fileType}", Colors.Red);
            }
        }
    }

    private void BookmarkURLs()
    {
        // Search for "http://" and "https://"
        byte[] httpPattern = Encoding.ASCII.GetBytes("http://");
        byte[] httpsPattern = Encoding.ASCII.GetBytes("https://");

        var httpPos = _hexEditor.FindAll(httpPattern);
        var httpsPos = _hexEditor.FindAll(httpsPattern);

        foreach (var pos in httpPos)
        {
            _hexEditor.AddBookmark(pos, "URL (HTTP)", Colors.Orange);
        }

        foreach (var pos in httpsPos)
        {
            _hexEditor.AddBookmark(pos, "URL (HTTPS)", Colors.Orange);
        }
    }

    private void BookmarkEmailAddresses()
    {
        // Search for "@" symbol (simple email detection)
        byte[] atSymbol = { 0x40 };  // '@'
        var positions = _hexEditor.FindAll(atSymbol);

        foreach (var pos in positions)
        {
            _hexEditor.AddBookmark(pos, "Potential Email", Colors.Yellow);
        }
    }

    private void BookmarkTimestamps()
    {
        // Implementation for timestamp detection...
    }
}
```

---

## ⚠️ Important Notes

### Bookmark Persistence

- Bookmarks are **not saved to file** by default
- Use `SaveState()` / `LoadState()` to persist bookmarks
- Or export/import manually with BookmarkManager

### Position Validity

- Bookmarks track position, not content
- If file length changes (insertions/deletions), bookmark positions become invalid
- Consider saving bookmark snapshots for validation

### Maximum Bookmarks

- No hard limit on bookmark count
- Each bookmark ~100 bytes memory
- 10,000 bookmarks ≈ 1 MB RAM

---

## 🔗 See Also

- **[AddHighlight()](highlights.md)** - Visual highlighting (different from bookmarks)
- **[SetPosition()](../navigation/setposition.md)** - Navigate to position
- **[SaveState() / LoadState()](../state/savestate.md)** - Persist bookmarks

---

**Last Updated**: 2026-02-19
**Version**: V2.0 (Color Support Added)
