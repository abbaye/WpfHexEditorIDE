# 🚀 Using WPF HexEditor in JetBrains Rider

**Quick Answer:** Yes, WpfHexEditor works perfectly in Rider! You just add it via XAML code instead of drag-n-drop.

---

## 🔍 Why No Toolbox in Rider?

JetBrains Rider **does not have a visual WPF designer/toolbox** like Visual Studio. This is a [known limitation](https://rider-support.jetbrains.com/hc/en-us/community/posts/19643548508434-Where-are-the-WPF-Controls-Toolbox) of the IDE itself, not a bug in WpfHexEditor.

**What Rider offers instead:**
- ✅ XAML Preview (real-time visual preview)
- ✅ Full IntelliSense for XAML/C#
- ✅ Code completion for controls and properties
- ✅ Live Templates (snippets) for rapid insertion
- ❌ No drag-n-drop toolbox (not planned by JetBrains)

**Result:** You write XAML code manually, but with excellent tooling support!

---

## ⚡ Quick Start (3 Methods)

### Method 1: Manual XAML (Recommended)

**Step 1:** Install the NuGet package
```bash
dotnet add package WPFHexaEditor
```

**Step 2:** Add namespace to your Window/UserControl
```xml
<Window x:Class="YourApp.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:hex="clr-namespace:WpfHexaEditor;assembly=WPFHexaEditor">

    <!-- Your controls here -->

</Window>
```

**Step 3:** Add the HexEditor control

Start typing `<hex:` and Rider's IntelliSense will show you available controls:

```xml
<hex:HexEditor FileName="C:\data.bin" />
```

**IntelliSense in action:**
- Type `<hex:` → See `HexEditor` in completion list
- Type `<hex:HexEditor ` → See all properties (FileName, ReadOnlyMode, etc.)
- Ctrl+Space → Show available properties/values

✅ **Done!** The control works exactly like in Visual Studio.

---

### Method 2: Live Templates (Fastest) ⚡

**Setup once:** Import the Live Templates (see [Setup Live Templates](#-setup-live-templates) below)

**Then use:**

1. **`hexns`** + Tab → Add namespace declaration
   ```xml
   xmlns:hex="clr-namespace:WpfHexaEditor;assembly=WPFHexaEditor"
   ```

2. **`hexed`** + Tab → Add basic HexEditor
   ```xml
   <hex:HexEditor FileName="" />
   ```

3. **`hexedfull`** + Tab → Add HexEditor with common properties
   ```xml
   <hex:HexEditor
       FileName=""
       ReadOnlyMode="False"
       BytePerLine="16"
       AllowExtend="False" />
   ```

---

### Method 3: Copy from Samples

**Browse examples:** [Sources/Samples/](../../Sources/Samples/)

**Example - Simple hex viewer:**
```xml
<Window x:Class="YourApp.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:hex="clr-namespace:WpfHexaEditor;assembly=WPFHexaEditor"
        Title="Hex Viewer" Height="600" Width="800">

    <Grid>
        <hex:HexEditor x:Name="HexEditor"
                       FileName="C:\test.bin"
                       ReadOnlyMode="False"
                       BytePerLine="16" />
    </Grid>
</Window>
```

**Then in code-behind:**
```csharp
// Access the control
HexEditor.FileName = @"C:\myfile.dat";
HexEditor.RefreshView();

// Handle events
HexEditor.DataCopied += (s, e) =>
{
    Console.WriteLine($"Copied {e.Length} bytes");
};
```

---

## 🎯 Setup Live Templates

### Option A: Import .DotSettings File (Easiest)

1. **Download:** [WpfHexEditor.DotSettings](WpfHexEditor.DotSettings)
2. **Rider:** `File` → `Manage IDE Settings` → `Import Settings...`
3. **Select:** The `.DotSettings` file
4. **Check:** "Live templates" option
5. **Click:** Import

✅ Templates are now available! Type `hexns`, `hexed`, or `hexedfull` + Tab

---

### Option B: Manual Creation

**Create templates manually in Rider:**

1. **Open:** `File` → `Settings` → `Editor` → `Live Templates`
2. **Click:** `+` (Add) → `Template`
3. **Fill in:**

#### Template 1: Namespace (`hexns`)
- **Abbreviation:** `hexns`
- **Description:** WpfHexEditor namespace
- **Template text:**
  ```xml
  xmlns:hex="clr-namespace:WpfHexaEditor;assembly=WPFHexaEditor"
  ```
- **Applicable in:** XAML

#### Template 2: Basic Control (`hexed`)
- **Abbreviation:** `hexed`
- **Description:** Basic HexEditor control
- **Template text:**
  ```xml
  <hex:HexEditor FileName="$FILE$" />$END$
  ```
- **Variables:** `FILE` (suggest file path)
- **Applicable in:** XAML

#### Template 3: Full Control (`hexedfull`)
- **Abbreviation:** `hexedfull`
- **Description:** HexEditor with common properties
- **Template text:**
  ```xml
  <hex:HexEditor
      FileName="$FILE$"
      ReadOnlyMode="$READONLY$"
      BytePerLine="16"
      AllowExtend="False" />$END$
  ```
- **Variables:**
  - `FILE` → File path
  - `READONLY` → True/False
- **Applicable in:** XAML

4. **Click:** Apply → OK

---

## 📋 Common Properties Reference

Use IntelliSense or refer to this table:

| Property | Type | Description | Default |
|----------|------|-------------|---------|
| `FileName` | `string` | Path to file to open | `null` |
| `Stream` | `Stream` | Alternative to FileName | `null` |
| `ReadOnlyMode` | `bool` | Prevent modifications | `false` |
| `BytePerLine` | `int` | Bytes displayed per line | `16` |
| `AllowExtend` | `bool` | Allow file size extension | `true` |
| `UseFixedWidthFont` | `bool` | Use monospace font | `true` |
| `FontSize` | `double` | Editor font size | `12.0` |
| `SelectionStart` | `long` | Selection start position | `-1` |
| `SelectionStop` | `long` | Selection end position | `-1` |
| `ForegroundSelection` | `Brush` | Selection text color | System |
| `BackgroundSelection` | `Brush` | Selection background | Blue |

**See full API:** [Sources/WPFHexaEditor/README.md](../../Sources/WPFHexaEditor/README.md)

---

## 🎨 XAML Preview

Rider's XAML Preview works great with WpfHexEditor!

**Enable preview:**
1. **Open** any `.xaml` file
2. **View** → `Tool Windows` → `XAML Preview`
3. **See** live preview as you type

**Tips:**
- ✅ Preview updates in real-time
- ✅ Supports custom controls and themes
- ✅ Works with design-time data
- ⚠️ Large files may slow preview (use small test files)

**Design-time data example:**
```xml
<hex:HexEditor FileName="C:\small-test.bin" />
```

---

## 🔧 Troubleshooting

### IntelliSense Not Showing WpfHexaEditor?

**Solution 1:** Rebuild project
```bash
dotnet build
```

**Solution 2:** Clear Rider caches
1. `File` → `Invalidate Caches...`
2. Check all options
3. Click `Invalidate and Restart`

**Solution 3:** Check NuGet reference
```bash
dotnet list package
# Should show: WPFHexaEditor
```

---

### XAML Preview Shows Error?

**Common causes:**
1. **File doesn't exist:** Use a valid `FileName` path
2. **Large file:** Preview may timeout on GB files (expected)
3. **Missing reference:** Check NuGet package is installed

**Workaround:**
```xml
<!-- Use small test file for design-time preview -->
<hex:HexEditor FileName="C:\small.bin" />
```

---

### Controls Not Recognized at Runtime?

**Check target framework:**

WpfHexEditor supports:
- ✅ `.NET Framework 4.8`
- ✅ `.NET 8.0-windows`

Your `.csproj` should have:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
  </PropertyGroup>
</Project>
```

---

## 💡 Best Practices for Rider

### 1. Use Code Snippets Library

Create your own snippet collection for your common patterns:

```xml
<!-- Your custom template: Advanced HexEditor with events -->
<hex:HexEditor x:Name="HexEditor"
               FileName="{Binding FilePath}"
               ReadOnlyMode="{Binding IsReadOnly}"
               DataCopied="HexEditor_OnDataCopied"
               SelectionChanged="HexEditor_OnSelectionChanged" />
```

Save as Live Template `hexadvanced`.

---

### 2. Leverage Code-Behind IntelliSense

Rider excels at C# IntelliSense:

```csharp
// IntelliSense shows all methods/properties/events
HexEditor.                    // Ctrl+Space shows:
          FileName            // - Properties
          RefreshView()       // - Methods
          DataCopied         // - Events
          SelectionStart     // - More...
```

---

### 3. Use Find Usages (Shift+F12)

Find all places where you use HexEditor:
- Ctrl+Click on `HexEditor` → Go to definition
- Shift+F12 → Find all usages
- Alt+F7 → Find usages settings

---

### 4. Quick Documentation (Ctrl+Q)

Hover over any property/method → Press `Ctrl+Q` → See XML documentation.

**Example:** Hover over `BytePerLine` → Documentation explains valid values.

---

## 📦 Sample Projects

**Ready-to-use examples:**

1. **[Simple Viewer](../../Sources/Samples/Rider/SimpleExample/)** (Minimal setup)
2. **[Main Sample](../../Sources/Samples/WpfHexEditor.Sample.Main/)** (All features)
3. **[Legacy Samples](../../Sources/Samples/Legacy/)** (V1 examples)

**Open in Rider:**
```bash
# Clone repo
git clone https://github.com/abbaye/WpfHexEditorIDE.git
cd WpfHexEditorIDE

# Open sample in Rider
rider Sources/Samples/WpfHexEditor.Sample.Main/WpfHexEditor.Sample.Main.csproj
```

---

## 📚 Additional Resources

- **[Getting Started Guide](../../GETTING_STARTED.md)** - Complete tutorial
- **[API Documentation](../../Sources/WPFHexaEditor/README.md)** - Full reference
- **[Features List](../../FEATURES.md)** - 163+ features
- **[Migration Guide](../migration/MIGRATION.md)** - V1 → V2 upgrade
- **[Sample Applications](../../Sources/Samples/README.md)** - 7+ examples

---

## 🤝 Community

**Need help?**
- 🐛 **[Report issues](https://github.com/abbaye/WpfHexEditorIDE/issues)**
- 💬 **[Discussions](https://github.com/abbaye/WpfHexEditorIDE/discussions)**
- 📖 **[Wiki](https://github.com/abbaye/WpfHexEditorIDE/wiki)**

**Rider-specific questions?**
- ✅ Tag your issue with `rider` or `jetbrains`
- ✅ Mention Rider version (Help → About)
- ✅ Include `.csproj` target framework

---

## ✅ Summary

| Task | Rider Solution |
|------|----------------|
| Add control to form | ❌ No drag-n-drop → ✅ Write XAML manually |
| Find properties | ✅ IntelliSense (Ctrl+Space) |
| Preview design | ✅ XAML Preview window |
| Quick insertion | ✅ Live Templates (`hexed` + Tab) |
| Documentation | ✅ Ctrl+Q on any property |
| Samples | ✅ Open sample projects directly |

**Bottom line:** Rider works great with WpfHexEditor - you just write XAML code instead of using a visual designer. With IntelliSense and Live Templates, it's just as fast!

---

**Made with ❤️ for Rider users** | [Report issues with this guide](https://github.com/abbaye/WpfHexEditorIDE/issues/136)
