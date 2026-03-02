# 🚀 Getting Started with WpfHexEditor

Two ways to use this project: **run the full IDE** or **embed the HexEditor control** in your own WPF app.

---

## Option A — Run the IDE

The fastest way to explore the project.

**Requirements:** Visual Studio 2022 · .NET 8.0 SDK · Windows

```bash
git clone https://github.com/abbaye/WpfHexEditorIDE.git
```

1. Open `WpfHexEditorControl.sln` in Visual Studio 2022
2. Set **`WpfHexEditor.App`** as the startup project
3. Press **F5**

The IDE launches with VS-style docking. You can:
- **File → New Solution** — create a project with binary, TBL, JSON, or text files
- **File → Open** — open any binary file directly in a new tab
- Drag panels to float, dock, or auto-hide
- Switch themes via **View → Theme**

---

## Option B — Embed the HexEditor in Your WPF App

### Step 1 — Reference the V2 Projects

In your `.csproj`:

```xml
<ItemGroup>
  <ProjectReference Include="..\WpfHexEditor.Core\WpfHexEditor.Core.csproj" />
  <ProjectReference Include="..\WpfHexEditor.HexEditor\WpfHexEditor.HexEditor.csproj" />
</ItemGroup>
```

> **NuGet:** The V1 legacy package (`WPFHexaEditor`) is still on NuGet but is no longer maintained. V2 NuGet packaging is planned. For now, use project references.

### Step 2 — Add the Namespace

```xml
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:hex="clr-namespace:WpfHexEditor.HexEditor;assembly=WpfHexEditor.HexEditor"
        Title="My App" Height="600" Width="900">
```

### Step 3 — Add the Control

```xml
<Grid>
    <hex:HexEditor x:Name="HexEdit" />
</Grid>
```

### Step 4 — Open a File

```csharp
HexEdit.FileName = @"C:\path\to\file.bin";
// or
HexEdit.OpenFile(@"C:\path\to\file.bin");
// or from stream
HexEdit.Stream = File.OpenRead(@"C:\path\to\file.bin");
```

**Done!** You now have a fully functional hex editor. 🎉

---

## IDE Support

| IDE | Notes |
|-----|-------|
| **Visual Studio 2022** | ✅ Full toolbox, XAML designer, IntelliSense |
| **JetBrains Rider** | ✅ XAML IntelliSense + preview — no visual toolbox (Rider limitation) · See [Rider Guide](docs/IDE/RIDER_GUIDE.md) |
| **VS Code** | ✅ XAML extension for syntax highlighting |

---

## Common API Usage

### Read & Write Bytes

```csharp
// Read
byte value = HexEdit.GetByte(0x100);
byte[] data = HexEdit.GetBytes(0x100, 16);
byte[] selection = HexEdit.SelectionByteArray;

// Write
HexEdit.SetByte(0x100, 0xFF);
HexEdit.InsertByte(0x100, 0xAB);
HexEdit.DeleteByte(0x100, 10);
```

### Search

```csharp
long pos = HexEdit.FindFirst(new byte[] { 0x4D, 0x5A }); // MZ header
var all  = HexEdit.FindAll(new byte[] { 0xFF, 0xFF });
await HexEdit.FindFirstAsync(bytes, progress, cancellationToken);
```

### Save

```csharp
HexEdit.SubmitChanges();                  // Save to original file
HexEdit.SubmitChanges(@"C:\output.bin"); // Save As
if (HexEdit.HasChanges) { ... }
```

### Customization

```xml
<hex:HexEditor Background="#1E1E1E"
               Foreground="White"
               SelectionFirstColor="#264F78"
               ByteModifiedColor="Orange"
               FontFamily="Consolas"
               BytePerLine="16" />
```

### TBL Character Tables

```csharp
HexEdit.LoadTBL(@"C:\game-charset.tbl");
HexEdit.TypeOfCharacterTable = CharacterTableType.ASCII;
HexEdit.CustomEncoding = Encoding.GetEncoding("shift-jis");
```

### Bookmarks

```csharp
HexEdit.SetBookmark(HexEdit.SelectionStart);
HexEdit.GotoNextBookmark();
HexEdit.GotoPreviousBookmark();
HexEdit.ClearAllBookmarks();
```

### Copy as Code

```csharp
string code = HexEdit.GetCopyData(CopyPasteMode.CSharpCode);
// → byte[] data = { 0x4D, 0x5A, 0x90, 0x00 };
// Other modes: VBNetCode, JavaCode, PythonCode, ...
```

### Custom Background Highlighting

```csharp
HexEdit.AddCustomBackgroundBlock(new CustomBackgroundBlock
{
    StartOffset = 0x100,
    Length = 256,
    Color = Colors.LightBlue,
    Description = "Header"
});
```

---

## Keyboard Shortcuts (HexEditor Control)

| Shortcut | Action |
|----------|--------|
| **Ctrl+C** | Copy selection |
| **Ctrl+V** | Paste |
| **Ctrl+X** | Cut |
| **Ctrl+Z** | Undo |
| **Ctrl+Y** | Redo |
| **Ctrl+F** | Quick search (inline bar) |
| **Ctrl+Shift+F** | Advanced search dialog |
| **Ctrl+G** | Go to offset |
| **Ctrl+A** | Select all |
| **F3 / Shift+F3** | Find next / previous |
| **Ins** | Toggle Insert / Overwrite mode |
| **Ctrl+MouseWheel** | Zoom |
| **ESC** | Close search bar / clear selection |

---

## Events

```csharp
HexEdit.SelectionStartChanged   += (s, e) => { /* ... */ };
HexEdit.SelectionLengthChanged  += (s, e) => { /* ... */ };
HexEdit.ByteModified            += (s, e) => { /* offset: e.BytePositionInStream */ };
HexEdit.LongProcessProgressChanged += (s, e) => { progressBar.Value = e.Percent; };
```

---

## Next Steps

- **[FEATURES.md](FEATURES.md)** — complete feature list (IDE, editors, panels, controls)
- **[CHANGELOG.md](CHANGELOG.md)** — what's new in each version
- **[Architecture Guide](docs/architecture/Overview.md)** — MVVM + service design
- **[API Reference](docs/api-reference/)** — full API documentation
- **[CONTRIBUTING.md](CONTRIBUTING.md)** — how to contribute
