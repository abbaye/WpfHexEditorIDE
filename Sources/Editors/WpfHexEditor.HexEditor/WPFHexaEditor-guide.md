# WPFHexaEditor — Documentation

## Table of Contents

1. [Architecture](#architecture)
2. [API Reference](#api-reference)
3. [Integration Guide — Level 1: Basic Setup](#level-1-basic-setup)
4. [Integration Guide — Level 2: Byte Operations & Editing](#level-2-byte-operations--editing)
5. [Integration Guide — Level 3: Format Detection & Structure Overlay](#level-3-format-detection--structure-overlay)
6. [Integration Guide — Level 4: Advanced Integration](#level-4-advanced-integration)
7. [Settings Reference](#settings-reference)

---

## Architecture

### Assembly structure

```
WPFHexaEditor.nupkg
└── lib/net8.0-windows/
    ├── WpfHexEditor.HexEditor.dll        — HexEditor UserControl — main entry point
    ├── WpfHexEditor.Core.dll             — byte providers, format detection, search, undo/redo
    ├── WpfHexEditor.Core.BinaryAnalysis.dll — cross-platform binary analysis (no WPF dependency)
    ├── WpfHexEditor.Core.Definitions.dll — 790+ embedded format definitions (.whfmt)
    ├── WpfHexEditor.Editor.Core.dll      — shared editor abstractions
    ├── WpfHexEditor.ColorPicker.dll      — color picker control (settings panel)
    ├── WpfHexEditor.HexBox.dll           — hex display rendering control
    └── WpfHexEditor.ProgressBar.dll      — progress bar control
```

Zero external NuGet dependencies. All assemblies are bundled inside the package.

### Type ownership

| Type | Assembly | Purpose |
|---|---|---|
| `HexEditor` | HexEditor | Main UserControl — hex + ASCII panels, all editing, format overlay |
| `ByteProvider` | Core | Byte stream abstraction — file, stream, memory |
| `UndoStack` | Core | Per-document undo/redo with transaction grouping |
| `FormatDetectionEngine` | Core | TIER 1/2 format matcher — reads `.whfmt` definitions |
| `EmbeddedFormatCatalog` | Core.Definitions | FrozenSet-backed catalog of 790+ embedded definitions |
| `IParsedFieldsPanel` | Editor.Core | Contract for structured field overlay panels |
| `IUndoAwareEditor` | Editor.Core | Contract for shared undo across co-editors |

### Byte provider model

```
FileName / Stream
       │
       ▼
  ByteProvider  ←── in-memory change layer (byte-level diff)
       │
       ▼
  HexBox         — hex column rendering (one HexBox per column group)
  AsciiPanel     — ASCII column rendering
       │
       ▼
  HexEditor      — scroll, selection, editing, format overlay
```

The `ByteProvider` records all modifications as lightweight change objects. The original stream is never mutated until `SubmitChanges()` is called.

### Format detection pipeline

```
OpenFile / OpenStream
       │
       ▼
  TIER 1 — strong signature scan (magic bytes, offset 0 / fixed offsets)
       │  match found → apply blocks + fire FormatDetected
       │  no match ↓
       ▼
  TIER 2 — text heuristics + entropy analysis (only when TIER 1 has no match)
       │
       ▼
  FormatDetected event  →  custom background blocks applied to HexBox
                         →  ParsedFields panel refreshed (if connected)
```

TIER 1 short-circuits TIER 2 on any `Strong` or `Unique` signature match. Entropy is skipped for those strength levels. All format definitions are `.whfmt` JSON files embedded in `WpfHexEditor.Core.Definitions`.

### Thread safety

- All UI rendering and input handling run on the WPF UI thread.
- Format detection runs on a background thread; results are marshalled back via `Dispatcher.BeginInvoke`.
- `ByteProvider` is not thread-safe — call only from the UI thread or under a lock.

---

## API Reference

### HexEditor — File operations

```csharp
// Open
HexEdit.FileName = @"C:\path\to\file.bin";   // property setter triggers load
await HexEdit.OpenFileAsync(filePath);

// Open stream
HexEdit.Stream = File.OpenRead("data.bin");   // property setter
HexEdit.OpenStream(stream, readOnly: true);   // method variant

// Save
HexEdit.SubmitChanges();                      // save to original file
HexEdit.SubmitChanges("out.bin");             // save to new file
HexEdit.SubmitChanges("out.bin", overwrite: true);

HexEdit.Close();
HexEdit.ReloadFromDisk();
```

### HexEditor — Byte operations

```csharp
// Read
byte b = HexEdit.GetByte(offset);

// Write (adds to undo stack)
HexEdit.SetByte(offset, 0xFF);
HexEdit.ModifyByte(0xFF, offset);

// Insert / delete
HexEdit.InsertByte(0x00, offset);
HexEdit.InsertByteMany(0x00, offset, count: 16);
HexEdit.DeleteByte(offset);
HexEdit.DeleteBytes(offset, length);

// Fill selection
HexEdit.FillWithByte(0xFF, startOffset, length);

// Read selection
byte[] selection = HexEdit.GetSelectionByteArray();
```

### HexEditor — Undo / redo

```csharp
HexEdit.Undo();
HexEdit.Redo();

bool canUndo = HexEdit.CanUndo;
bool canRedo = HexEdit.CanRedo;

// Descriptions for UI dropdowns
IReadOnlyList<string> undoList = HexEdit.GetUndoDescriptions(maxCount: 20);
IReadOnlyList<string> redoList = HexEdit.GetRedoDescriptions(maxCount: 20);
```

### HexEditor — Selection and navigation

```csharp
HexEdit.SelectionStart  = 0;
HexEdit.SelectionStop   = 255;
long length = HexEdit.SelectionLength;

HexEdit.SetPosition(offset);
HexEdit.SelectAll();
HexEdit.ClearSelection();
HexEdit.DeleteSelection();

// Jump to offset dialog (Ctrl+G)
// — triggered by keyboard shortcut in the control
```

### HexEditor — Search

```csharp
byte[] pattern = new byte[] { 0xFF, 0xD8 };   // JPEG magic

long first  = HexEdit.FindFirst(pattern);
long next   = HexEdit.FindNext(pattern, currentPosition);
long last   = HexEdit.FindLast(pattern);
IEnumerable<long> all = HexEdit.FindAll(pattern);
int count = HexEdit.CountOccurrences(pattern);
```

### HexEditor — Bookmarks

```csharp
HexEdit.SetBookmark(offset);
HexEdit.RemoveBookmark(offset);
HexEdit.GoToNextBookmark();
HexEdit.GoToPreviousBookmark();
IReadOnlyList<long> marks = HexEdit.GetBookmarks();
HexEdit.ClearBookmarks();
```

### HexEditor — Custom background blocks

```csharp
// Add a colored overlay region (format overlay, annotation, diff highlight, etc.)
HexEdit.AddCustomBackgroundBlock(startOffset, length, Colors.LightBlue, "Header");
HexEdit.RemoveCustomBackgroundBlock(startOffset, length);
HexEdit.ClearCustomBackgroundBlock();

IEnumerable<(long start, long length, Color color, string label)> blocks
    = HexEdit.GetCustomBackgroundBlocks();
```

### HexEditor — Key events

```csharp
// Byte changes
HexEdit.ByteModified     += (s, e) => { /* e.BytePositionInFile, e.Byte */ };
HexEdit.BytesDeleted     += (s, e) => { };

// Selection
HexEdit.SelectionChanged += (s, e) => { /* e.Start, e.Stop, e.Length */ };
HexEdit.PositionChanged  += (s, e) => { /* e.NewPosition */ };

// File lifecycle
HexEdit.FileOpened       += (s, e) => { };
HexEdit.FileClosed       += (s, e) => { };
HexEdit.ChangesSubmited  += (s, e) => { };
HexEdit.FileExternallyChanged += (s, e) => { /* e.FilePath */ };

// Undo / redo
HexEdit.Undone           += (s, e) => { };
HexEdit.Redone           += (s, e) => { };

// Format detection
HexEdit.FormatDetected   += (s, e) => { /* e.FormatName, e.Confidence */ };
```

---

## Level 1: Basic Setup

Minimum working integration — hex editor in a WPF window.

### 1 — Install

```
dotnet add package WPFHexaEditor
```

### 2 — Merge the resource dictionary

Required so themes, brushes, and context menus resolve correctly.

```xml
<!-- App.xaml -->
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="pack://application:,,,/WpfHexEditor.HexEditor;component/Resources/Dictionary/Generic.xaml" />
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>
```

Context menus use opaque backgrounds by default. No extra theming is needed.

### 3 — Add namespace and control

```xml
<Window
    xmlns:hexe="clr-namespace:WpfHexEditor.HexEditor;assembly=WpfHexEditor.HexEditor">

    <hexe:HexEditor x:Name="HexEdit" />
```

### 4 — Open a file

```csharp
HexEdit.FileName = @"C:\path\to\file.bin";
```

### 5 — Open a stream

```csharp
HexEdit.Stream = File.OpenRead("data.bin");
```

### 6 — Save

```csharp
HexEdit.SubmitChanges();          // overwrite original
HexEdit.SubmitChanges("out.bin"); // save copy
```

---

## Level 2: Byte Operations & Editing

### Read and modify bytes

```csharp
byte b = HexEdit.GetByte(offset);
HexEdit.SetByte(offset, 0xFF);
```

### Insert and delete

```csharp
// Insert mode
HexEdit.EditMode = EditMode.Insert;
HexEdit.InsertByte(0x00, offset);
HexEdit.InsertByteMany(0x90, offset, count: 16);  // NOP sled

// Delete
HexEdit.DeleteByte(offset);
HexEdit.DeleteBytes(offset, length);
```

### Fill selection

```csharp
HexEdit.FillWithByte(0x00, SelectionStart, SelectionLength);
```

### Undo / redo

```csharp
HexEdit.Undo();
HexEdit.Redo();

// Bind to toolbar buttons
saveButton.IsEnabled    = HexEdit.CanUndo;
redoButton.IsEnabled    = HexEdit.CanRedo;
```

### Undo history dropdown

```csharp
var descriptions = HexEdit.GetUndoDescriptions(maxCount: 20);
foreach (var desc in descriptions)
    undoMenu.Items.Add(new MenuItem { Header = desc });
```

### Clipboard

```csharp
HexEdit.Copy();
HexEdit.Cut();
HexEdit.Paste();
```

### Display options

```csharp
HexEdit.BytePerLine          = 16;                         // columns
HexEdit.DataStringVisual     = DataVisualType.Hexadecimal; // Hex / Decimal / Binary
HexEdit.OffSetStringVisual   = DataVisualType.Hexadecimal; // offset bar format
HexEdit.ReadOnlyMode         = true;                       // block edits; selection still works
HexEdit.EditMode             = EditMode.Overwrite;         // or Insert
HexEdit.ByteSize             = ByteSizeType.Bit8;          // 8 / 16 / 32-bit cell width
HexEdit.ByteOrder            = ByteOrderType.LoHi;         // or HiLo
```

### Zoom

```csharp
HexEdit.AllowZoom = true;
HexEdit.ZoomScale = 1.5;    // 0.5 – 2.0
HexEdit.ZoomScaleChanged += (s, e) => { };
```

---

## Level 3: Format Detection & Structure Overlay

### Auto-detection (default)

Format detection fires automatically when a file is opened if `EnableAutoFormatDetection` is `true` (default). The engine matches against 790+ embedded `.whfmt` definitions and overlays colored background blocks on matched structures.

```csharp
HexEdit.EnableAutoFormatDetection = true;
HexEdit.AutoApplyDetectedBlocks   = true;

HexEdit.FormatDetected += (s, e) =>
{
    StatusBar.Text = $"Detected: {e.FormatName}";
};
```

### Manual trigger

```csharp
// Load external definitions from a directory
int loaded = HexEdit.LoadFormatDefinitions(@"C:\MyFormats");
```

### Custom background blocks

Use `AddCustomBackgroundBlock` to annotate arbitrary byte ranges — useful for diff views, search results, or parsed structure overlays built outside the format engine.

```csharp
HexEdit.ClearCustomBackgroundBlock();
HexEdit.AddCustomBackgroundBlock(0x00, 4,   Colors.Orange,    "Magic");
HexEdit.AddCustomBackgroundBlock(0x04, 2,   Colors.LightBlue, "Version");
HexEdit.AddCustomBackgroundBlock(0x06, 128, Colors.LightGreen,"Header");
```

### ParsedFields panel

`IParsedFieldsPanel` is the contract between the hex editor and a side panel that shows a structured tree of parsed fields (sections, fields, values).

```csharp
// Connect your IParsedFieldsPanel implementation
HexEdit.ConnectParsedFieldsPanel(myParsedFieldsPanel);
HexEdit.AutoRefreshParsedFields = true;  // reparse when bytes change

// Disconnect
HexEdit.DisconnectParsedFieldsPanel();

// React to field navigation requests
HexEdit.ParsedFieldNavigationRequested += (s, e) =>
{
    // e.StartOffset, e.Length, e.FieldName
    HexEdit.SetPosition(e.StartOffset);
    HexEdit.SelectionStart = e.StartOffset;
    HexEdit.SelectionStop  = e.StartOffset + e.Length - 1;
};
```

### Scroll marker panel

The `HexScrollMarkerPanel` is an overview sidebar (similar to VS Code's scroll bar markers). It visualizes bookmarks, search hits, custom blocks, and unsaved changes at a glance.

```xml
<!-- Place beside the HexEditor -->
<hexe:HexScrollMarkerPanel x:Name="ScrollPanel"
                           HexEditor="{Binding ElementName=HexEdit}" />
```

### Breadcrumb bar

```xml
<hexe:HexBreadcrumbBar x:Name="BreadcrumbBar"
                       HexEditor="{Binding ElementName=HexEdit}" />
```

The breadcrumb bar shows the current format structure path (e.g. `ZIP > LocalFileHeader > FileName`) and updates on scroll and click navigation.

---

## Level 4: Advanced Integration

### Session persistence — save and restore state

```csharp
// On close: capture caret and bookmarks
long savedPosition  = HexEdit.SelectionStart;
var  savedBookmarks = HexEdit.GetBookmarks();

// On reopen
HexEdit.SetPosition(savedPosition);
foreach (var mark in savedBookmarks)
    HexEdit.SetBookmark(mark);
```

### Shared undo across co-editors (IDE integration)

When a hex editor and a code editor are co-editing the same document, attach them to a shared undo engine via `IUndoAwareEditor`:

```csharp
// Both editors implement IUndoAwareEditor
((IUndoAwareEditor)HexEdit).AttachSharedUndo(sharedUndoEngine);
((IUndoAwareEditor)codeEditor).AttachSharedUndo(sharedUndoEngine);

// Undo/Redo now routes through the shared engine
HexEdit.Undo();   // pops from shared stack
```

### Read-only mode with full selection

```csharp
HexEdit.ReadOnlyMode = true;
// Selection, copy, and scroll all remain active.
// Only byte modification, insert, delete, and paste are blocked.
```

### Watch for external file changes

```csharp
HexEdit.FileExternallyChanged += (s, e) =>
{
    var result = MessageBox.Show(
        $"{e.FilePath} changed on disk. Reload?",
        "File changed",
        MessageBoxButton.YesNo);
    if (result == MessageBoxResult.Yes)
        HexEdit.ReloadFromDisk();
};
```

### Custom encoding (TBL files)

```csharp
// Set a custom character encoding for the ASCII panel
HexEdit.CustomEncoding = Encoding.GetEncoding("shift_jis");

// TBL character table support (ROM hacking)
HexEdit.ShowTblAscii    = true;
HexEdit.ShowTblDte      = true;
HexEdit.TblAsciiColor   = Colors.LightGreen;
```

### Settings persistence (JSON)

```csharp
// Export all DependencyProperty settings
string json = HexEditor.ExportSettingsToJson();
File.WriteAllText("hex-settings.json", json);

// Import on next launch
HexEditor.ImportSettingsFromJson(File.ReadAllText("hex-settings.json"));
```

### Progress events (large files)

```csharp
HexEdit.LongProcessProgressStarted   += (s, e) => progressBar.Visibility = Visibility.Visible;
HexEdit.LongProcessProgressChanged   += (s, e) => progressBar.Value = e.Progress;
HexEdit.LongProcessProgressCompleted += (s, e) => progressBar.Visibility = Visibility.Collapsed;
```

---

## Settings Reference

All settings are `DependencyProperty` on `HexEditor` — bindable in XAML or set from code.

### Display

| Property | Type | Default | Description |
|---|---|---|---|
| `BytePerLine` | `int` | `16` | Number of byte columns |
| `DataStringVisual` | `DataVisualType` | `Hexadecimal` | Cell display: Hex / Decimal / Binary |
| `OffSetStringVisual` | `DataVisualType` | `Hexadecimal` | Offset bar: Hex / Decimal |
| `ByteSize` | `ByteSizeType` | `Bit8` | Cell width: 8 / 16 / 32-bit |
| `ByteOrder` | `ByteOrderType` | `LoHi` | Multi-byte cell byte order |
| `FontSize` | `double` | `13` | Editor font size |
| `AllowZoom` | `bool` | `false` | Enable Ctrl+scroll zoom |
| `ZoomScale` | `double` | `1.0` | Current zoom factor (0.5–2.0) |

### Editing

| Property | Type | Default | Description |
|---|---|---|---|
| `ReadOnlyMode` | `bool` | `false` | Block edits; selection and copy still work |
| `EditMode` | `EditMode` | `Overwrite` | `Overwrite` or `Insert` |
| `AllowDeleteByte` | `bool` | `true` | Allow byte deletion |
| `AllowExtend` | `bool` | `true` | Allow inserting bytes beyond EOF |

### Selection

| Property | Type | Default | Description |
|---|---|---|---|
| `SelectionStart` | `long` | `0` | Selection start offset |
| `SelectionStop` | `long` | `0` | Selection end offset (inclusive) |
| `SelectionLength` | `long` | computed | Read-only selection size |
| `HasSelection` | `bool` | computed | `true` when any bytes are selected |
| `AutoHighLiteSelectionByteBrush` | `Color` | — | Highlight color for matching bytes |
| `AllowAutoHighLightSelectionByte` | `bool` | `false` | Highlight all occurrences of selected byte |

### Mouse & UX

| Property | Type | Default | Description |
|---|---|---|---|
| `MouseWheelSpeed` | `MouseWheelSpeedMode` | `System` | `System` / `Line` / `Page` |
| `AllowFileDrop` | `bool` | `true` | Accept drag-drop of files |
| `AllowTextDrop` | `bool` | `false` | Accept drag-drop of text |
| `AllowContextMenu` | `bool` | `true` | Show right-click context menu |
| `AllowMarkerClickNavigation` | `bool` | `true` | Clicking scroll markers jumps to offset |

### Format detection

| Property | Type | Default | Description |
|---|---|---|---|
| `EnableAutoFormatDetection` | `bool` | `true` | Run detection pipeline on open |
| `AutoApplyDetectedBlocks` | `bool` | `true` | Overlay colored blocks from `.whfmt` |
| `ShowFormatDetectionStatus` | `bool` | `true` | Show detection result in status bar |

### Parsed fields

| Property | Type | Default | Description |
|---|---|---|---|
| `AutoRefreshParsedFields` | `bool` | `true` | Reparse when bytes change |

### Progress

| Property | Type | Default | Description |
|---|---|---|---|
| `ShowProgressOverlay` | `bool` | `true` | Semi-transparent progress overlay |
| `ProgressRefreshRate` | `ProgressRefreshRate` | `Balanced` | `Fast` / `Balanced` / `Slow` |
