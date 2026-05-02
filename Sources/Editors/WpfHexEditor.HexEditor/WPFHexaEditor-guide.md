# WPFHexaEditor — Documentation

## Table of Contents

1. [Architecture](#architecture)
2. [API Reference](#api-reference)
3. [Integration Guide — Level 1: Basic Setup](#level-1-basic-setup)
4. [Integration Guide — Level 2: Editing & Undo](#level-2-editing--undo)
5. [Integration Guide — Level 3: Format Detection & Overlay](#level-3-format-detection--overlay)
6. [Integration Guide — Level 4: Search & Export](#level-4-search--export)
7. [Settings Reference](#settings-reference)

---

## Architecture

### Assembly structure

```
WPFHexaEditor.nupkg
└── lib/net8.0-windows/
    ├── WpfHexEditor.HexEditor.dll        — HexEditor UserControl, main entry point
    ├── WpfHexEditor.Core.dll             — byte providers, format detection, search, undo/redo
    ├── WpfHexEditor.Core.BinaryAnalysis.dll — cross-platform binary analysis (no WPF)
    ├── WpfHexEditor.Core.Definitions.dll — 790+ embedded .whfmt format definitions
    ├── WpfHexEditor.Core.Localization.dll — localized strings (17 languages)
    ├── WpfHexEditor.Editor.Core.dll      — shared editor abstractions and undo engine
    ├── WpfHexEditor.ColorPicker.dll      — color picker (settings panel)
    ├── WpfHexEditor.HexBox.dll           — hex display rendering control
    └── WpfHexEditor.ProgressBar.dll      — progress bar control
```

Zero external NuGet dependencies. All assemblies are bundled inside the package.

### Type ownership

| Type | Assembly | Purpose |
|---|---|---|
| `HexEditor` | HexEditor | Main `UserControl` — all rendering, editing, search, format overlay |
| `HexBreadcrumbBar` | HexEditor | Visual structure navigator over detected format fields |
| `HexScrollMarkerPanel` | HexEditor | Overview panel — bookmarks, search hits, changes |
| `ByteProvider` | Core | Pluggable data source (file, stream, memory) |
| `UndoEngine` | Editor.Core | Undo/redo engine — `UndoGroup` transactions and coalescence |
| `EmbeddedFormatCatalog` | Core.Definitions | Lazy-loaded 790+ format definitions, thread-safe singleton |
| `DataInspectorService` | Core.BinaryAnalysis | Multi-format byte interpretation (int/float/date/GUID/network) |
| `DataStatisticsService` | Core.BinaryAnalysis | Shannon entropy, byte distribution, anomaly detection |

### Scroll and virtualization model

```
VerticalScrollBar.Value  (line index)
        │
        ▼
HexViewport.ComputeVisibleLines()
   → firstLine = (long)ScrollValue
   → lastLine  = firstLine + ViewportLineCount
        │
        ▼  render only visible byte rows
OnRender — hex glyph runs + ASCII glyph runs per line
        │
        ▼
HexScrollMarkerPanel — normalized tick marks across all lines
```

Only visible rows are rendered. Line height is fixed (`FontSize * LineHeightFactor`) for O(1) scroll math.

### Thread safety

- All rendering and input runs on the WPF UI thread.
- Format detection runs on a `Task` background thread; results posted back via `Dispatcher.BeginInvoke`.
- `UndoEngine` is not thread-safe — all mutations must occur on the UI thread.
- `EmbeddedFormatCatalog.GetAll()` is fully thread-safe (frozen set after first load).

---

## API Reference

### HexEditor — Content

```csharp
// Open by path (closes previous, resets undo stack)
string FileName { get; set; }

// Open by stream
Stream Stream { get; set; }

// Close and release resources
void CloseFile();

// Raw byte access
byte  GetByte(long offset);
void  SetByte(long offset, byte value);            // recorded in UndoEngine
byte[] GetBytes(long offset, int count);
void  SetBytes(long offset, byte[] values);

// Cursor / selection
long SelectionStart  { get; set; }
long SelectionLength { get; set; }
long SelectionStop   { get; }
```

### HexEditor — Undo / Persistence

```csharp
void Undo();
void Redo();
bool CanUndo { get; }
bool CanRedo { get; }

// Save
void SubmitChanges();                  // overwrite original
void SubmitChanges(string fileName);   // save to new path
bool IsModified { get; }
```

### HexEditor — Navigation

```csharp
// Go to offset
void SetPosition(long offset);
void SetPosition(long offset, HexScrollStrategy strategy);   // Top | Center | Bottom

// Go to position dialog (Ctrl+G)
void ShowGotoDialog();

long FirstVisibleBytePosition { get; }
long LastVisibleBytePosition  { get; }
```

### HexEditor — Search

```csharp
void FindNext(string pattern,   SearchMode mode);   // Hex | Text | Regex
void FindAll (string pattern,   SearchMode mode);
void ClearHighlights();

event EventHandler<FindAllResultEventArgs>? FindAllCompleted;
```

### HexEditor — Format Detection

```csharp
// Trigger format detection (called automatically on file open)
Task DetectFormatAsync();

// Detected format
FileFormatInfo? DetectedFormat { get; }

// Field overlay
bool ShowFormatFieldOverlay { get; set; }

event EventHandler<FileFormatInfo?>? FormatDetected;
```

### HexEditor — Events

```csharp
event EventHandler?                         SelectionChanged;
event EventHandler?                         ByteModified;
event EventHandler<FileFormatInfo?>?        FormatDetected;
event EventHandler?                         FileOpened;
event EventHandler?                         FileClosed;
event EventHandler<ByteEventArgs>?          ByteDeleted;
event EventHandler<ByteEventArgs>?          ByteAdded;
```

---

## Level 1: Basic Setup

### 1 — Install

```
dotnet add package WPFHexaEditor
```

### 2 — Merge the resource dictionary

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

Context menus use opaque backgrounds by default after this merge. No extra theming required.

### 3 — Add namespace and control

```xml
<Window
    xmlns:hexe="clr-namespace:WpfHexEditor.HexEditor;assembly=WpfHexEditor.HexEditor">

    <hexe:HexEditor x:Name="HexEdit" BytePerLine="16" />
```

### 4 — Open a file

```csharp
HexEdit.FileName = @"C:\path\to\file.bin";
```

### 5 — Open a stream

```csharp
HexEdit.Stream = File.OpenRead("data.bin");
```

### 6 — Read back

```csharp
byte value = HexEdit.GetByte(offset);
bool modified = HexEdit.IsModified;
```

---

## Level 2: Editing & Undo

### In-place byte editing

The user edits directly in the hex or ASCII panel. Each keystroke is recorded as a discrete `UndoEntry` in `UndoEngine`. Consecutive same-offset edits are coalesced into a single undo step.

### Programmatic edits

```csharp
// Single byte — added to undo stack
HexEdit.SetByte(0x1000, 0xFF);

// Multi-byte block — recorded as UndoGroup
HexEdit.SetBytes(0x1000, new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });

// Fill selection with a value
HexEdit.FillSelection(0x00);
```

### Undo / redo

```csharp
if (HexEdit.CanUndo) HexEdit.Undo();
if (HexEdit.CanRedo) HexEdit.Redo();
```

The undo history dropdown is built into the toolbar.

### Cut / copy / paste

```csharp
HexEdit.CopyToClipboard(CopyPasteMode.HexString);   // "DE AD BE EF"
HexEdit.CopyToClipboard(CopyPasteMode.AsciiString);
HexEdit.CopyToClipboard(CopyPasteMode.CSharpCode);  // "new byte[] { 0xDE, 0xAD }"
HexEdit.PasteFromClipboard();
```

### Save

```csharp
// Overwrite original file
HexEdit.SubmitChanges();

// Save to new path
HexEdit.SubmitChanges(@"C:\out\patched.bin");
```

---

## Level 3: Format Detection & Overlay

### Automatic detection

Format detection runs automatically on every `FileName` or `Stream` assignment. The result is surfaced via `FormatDetected`.

```csharp
HexEdit.FormatDetected += (_, info) =>
{
    if (info is not null)
        StatusBar.Text = $"Detected: {info.Name} ({info.Category})";
};
```

### Manual trigger

```csharp
await HexEdit.DetectFormatAsync();
FileFormatInfo? fmt = HexEdit.DetectedFormat;
```

### Field overlay

When a format is detected, colored semi-transparent blocks are drawn over the known field regions:

```csharp
HexEdit.ShowFormatFieldOverlay = true;   // default: true after detection
```

### BreadcrumbBar

The `HexBreadcrumbBar` provides a clickable structure navigator above the hex panel. It shows the field hierarchy at the current cursor position.

```xml
<hexe:HexBreadcrumbBar HexEditor="{Binding ElementName=HexEdit}" />
```

Navigation chips are built from `ParsedFields` populated during format detection. Clicking a chip scrolls to that field and selects its byte range.

### Entropy and statistics

```csharp
using WpfHexEditor.Core.BinaryAnalysis;

var stats = new DataStatisticsService();
var result = await stats.ComputeAsync(stream);

double entropy     = result.ShannonEntropy;   // 0.0 (uniform) – 8.0 (random)
double compression = result.CompressionRatio;
var   distribution = result.ByteDistribution; // byte[] → count[]
```

---

## Level 4: Search & Export

### Find

```csharp
// Find next hex sequence
HexEdit.FindNext("FF D8 FF", SearchMode.Hex);

// Find next ASCII text
HexEdit.FindNext("MThd", SearchMode.Text);

// Find next regex pattern
HexEdit.FindNext(@"\x50\x4B\x03\x04", SearchMode.Regex);
```

Search results are highlighted in the viewport and marked with ticks in `HexScrollMarkerPanel`.

### Find all

```csharp
HexEdit.FindAllCompleted += (_, e) =>
{
    foreach (long offset in e.Offsets)
        Console.WriteLine($"Match at 0x{offset:X8}");
};

HexEdit.FindAll("FF D8 FF", SearchMode.Hex);
```

### Intel HEX export

```csharp
using WpfHexEditor.Core.BinaryAnalysis;

var svc = new IntelHexService();
await svc.ExportAsync(stream, "output.hex");
```

### Motorola S-Record export

```csharp
var svc = new SRecordService();
await svc.ExportAsync(stream, "output.s19", SRecordFormat.S19);
```

### Binary template

```csharp
var compiler = new BinaryTemplateCompiler();
var template = compiler.Compile(File.ReadAllText("struct.bt"));
var fields   = template.Parse(stream);
```

### Data inspector

```csharp
using WpfHexEditor.Core.BinaryAnalysis;

var inspector = new DataInspectorService();
var result    = inspector.Interpret(bytes, offset: 0);

int    int32val  = result.Int32;
float  float32   = result.Float;
double float64   = result.Double;
string utfStr    = result.Utf8String;
string guidStr   = result.Guid;
string ipAddr    = result.IPv4Address;
```

---

## Settings Reference

All settings are `DependencyProperty` on `HexEditor` — bindable in XAML or set from code.

| Property | Type | Default | Description |
|---|---|---|---|
| `BytePerLine` | `int` | `16` | Number of bytes displayed per row |
| `ByteOrderMark` | `ByteOrderType` | `LittleEndian` | Byte order for multi-byte interpretations |
| `ReadOnly` | `bool` | `false` | Block all edits; selection and copy still work |
| `ShowAsciiPanel` | `bool` | `true` | Show ASCII column beside hex |
| `ShowColumnHighlight` | `bool` | `false` | Highlight the column under the cursor |
| `ShowRowHighlight` | `bool` | `true` | Highlight the row under the cursor |
| `ShowLineNumber` | `bool` | `true` | Show offset/line numbers on the left |
| `OffsetBase` | `OffsetBaseType` | `Hexadecimal` | Display offsets in hex or decimal |
| `ShowFormatFieldOverlay` | `bool` | `true` | Colored field blocks after format detection |
| `FontSize` | `double` | `13` | Editor font size |
| `FontFamily` | `FontFamily` | Consolas | Editor font |
| `ByteToolTipDisplayMode` | `ByteToolTipDisplayMode` | `Always` | When to show byte tooltip on hover |
| `ByteToolTipDetailLevel` | `ByteToolTipDetailLevel` | `Standard` | How much info the tooltip shows |
| `MouseWheelSpeed` | `int` | `3` | Lines scrolled per wheel notch |
| `AllowExtend` | `bool` | `false` | Allow appending bytes at end of file |
| `StatusBarVisible` | `bool` | `true` | Show built-in status bar |
| `HighlightSelectionStart` | `bool` | `true` | Pin highlight on selection start offset |

### Persist settings to JSON

```csharp
// Export
string json = HexEditorDefaultSettings.ExportToJson();
File.WriteAllText("hexeditor-settings.json", json);

// Import
HexEditorDefaultSettings.ImportFromJson(File.ReadAllText("hexeditor-settings.json"));
```

### Theme brushes (DynamicResource keys)

Override in your `ResourceDictionary` to apply a custom theme:

```xml
<SolidColorBrush x:Key="HE_Background"        Color="#1E1E1E" />
<SolidColorBrush x:Key="HE_ForegroundHex"     Color="#D4D4D4" />
<SolidColorBrush x:Key="HE_ForegroundAscii"   Color="#9CDCFE" />
<SolidColorBrush x:Key="HE_SelectionBrush"    Color="#264F78" />
<SolidColorBrush x:Key="HE_RowHighlight"      Color="#2A2D2E" />
<SolidColorBrush x:Key="HE_ColumnHighlight"   Color="#2A2D2E" />
<SolidColorBrush x:Key="HE_ModifiedByte"      Color="#CE9178" />
```
