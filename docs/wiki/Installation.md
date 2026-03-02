# Installation Guide

Complete guide to installing and setting up WPF HexEditor in your project.

---

## 📋 Requirements

Before installing, ensure you have:

| Requirement | Version | Notes |
|-------------|---------|-------|
| **Visual Studio** | 2022+ | Or JetBrains Rider |
| **.NET Framework** | 4.8 | For legacy projects |
| **.NET** | 8.0-windows | Recommended for best performance |
| **Windows** | 7+ | WPF requires Windows |

---

## 📦 Installation Methods

### Method 1: NuGet Package Manager (Recommended)

#### Visual Studio GUI

1. **Right-click your project** in Solution Explorer
2. Select **"Manage NuGet Packages..."**
3. Click the **"Browse"** tab
4. Search for: `WPFHexaEditor`
5. Click **"Install"**

![NuGet Package Manager](https://via.placeholder.com/600x300?text=NuGet+Package+Manager)

#### Package Manager Console

```powershell
Install-Package WPFHexaEditor
```

#### .NET CLI

```bash
dotnet add package WPFHexaEditor
```

---

### Method 2: Manual .csproj Edit

Add this to your `.csproj` file:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="WPFHexaEditor" Version="2.5.0" />
  </ItemGroup>
</Project>
```

Then restore packages:
```bash
dotnet restore
```

---

### Method 3: Build from Source (Advanced)

For developers who want to build from source:

```bash
# Clone repository
git clone https://github.com/abbaye/WpfHexEditorIDE.git
cd WpfHexEditorIDE

# Restore dependencies
dotnet restore

# Build
dotnet build Sources/WPFHexaEditor/WPFHexaEditor.csproj

# Add project reference to your solution
dotnet add reference ../WpfHexEditorControl/Sources/WPFHexaEditor/WPFHexaEditor.csproj
```

---

## ✅ Verify Installation

### Check in References

After installation, verify the package appears in your project:

**Visual Studio**:
- Expand **Dependencies → Packages** in Solution Explorer
- Look for **WPFHexaEditor**

**.NET CLI**:
```bash
dotnet list package
```

Expected output:
```
Project 'YourProject' has the following package references
   [net8.0-windows]:
   Top-level Package      Requested   Resolved
   > WPFHexaEditor         2.5.0       2.5.0
```

---

## 🎯 First Use

### 1. Add Namespace to XAML

Open your `MainWindow.xaml` and add the namespace:

```xml
<Window x:Class="YourApp.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:hex="clr-namespace:WpfHexaEditor;assembly=WPFHexaEditor"
        Title="My Hex Editor" Height="450" Width="800">

    <!-- Your content here -->

</Window>
```

**Key points**:
- `xmlns:hex` is the namespace prefix (you can use any name)
- `WpfHexaEditor` is the CLR namespace (with 'a', not 'WpfHexEditor')
- `WPFHexaEditor` is the assembly name (without 'a')

---

### 2. Add the Control

Add the HexEditor control to your window:

```xml
<Window x:Class="YourApp.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:hex="clr-namespace:WpfHexaEditor;assembly=WPFHexaEditor"
        Title="My Hex Editor" Height="600" Width="900">

    <Grid>
        <hex:HexEditor x:Name="hexEditor" />
    </Grid>

</Window>
```

---

### 3. Test in Code-Behind

Open `MainWindow.xaml.cs` and test:

```csharp
using System.Windows;

namespace YourApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Test: Create sample data
            byte[] sampleData = { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"
            hexEditor.OpenMemory(sampleData);
        }
    }
}
```

---

### 4. Run Your Application

Press **F5** to build and run.

**Expected result**: Hex editor displays with sample data:
```
00000000: 48 65 6C 6C 6F                          Hello
```

✅ **Success!** You've successfully installed WPF HexEditor!

---

## 🔧 Multi-Targeting Setup

WPF HexEditor supports multiple .NET versions. Here's how to set up multi-targeting:

### .NET 8.0 Only (Recommended)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="WPFHexaEditor" Version="2.5.0" />
  </ItemGroup>
</Project>
```

### .NET Framework 4.8 Only

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <UseWPF>true</UseWPF>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="WPFHexaEditor" Version="2.5.0" />
  </ItemGroup>
</Project>
```

### Multi-Target (Both Frameworks)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net8.0-windows;net48</TargetFrameworks>
    <UseWPF>true</UseWPF>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="WPFHexaEditor" Version="2.5.0" />
  </ItemGroup>
</Project>
```

**Note**: The correct binary is automatically selected at runtime!

---

## 🌐 WinForms Integration

To use in WinForms applications:

### 1. Add References

```xml
<ItemGroup>
  <PackageReference Include="WPFHexaEditor" Version="2.5.0" />
  <FrameworkReference Include="Microsoft.WindowsDesktop.App.WPF" />
</ItemGroup>
```

### 2. Use ElementHost

```csharp
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using WpfHexaEditor;

public partial class MainForm : Form
{
    private ElementHost elementHost;
    private HexEditor hexEditor;

    public MainForm()
    {
        InitializeComponent();

        // Create ElementHost
        elementHost = new ElementHost
        {
            Dock = DockStyle.Fill
        };

        // Create HexEditor
        hexEditor = new HexEditor();
        elementHost.Child = hexEditor;

        // Add to form
        this.Controls.Add(elementHost);

        // Test with sample data
        byte[] data = { 0x48, 0x65, 0x6C, 0x6C, 0x6F };
        hexEditor.OpenMemory(data);
    }
}
```

👉 **[See complete WinForms sample](https://github.com/abbaye/WpfHexEditorIDE/tree/master/Sources/Samples/WpfHexEditor.Sample.Winform)**

---

## ❌ Common Installation Issues

### Issue 1: "Type 'HexEditor' was not found"

**Symptom**: Red squiggle under `<hex:HexEditor>`

**Causes & Solutions**:

1. **Wrong namespace**
   ```xml
   <!-- ❌ Wrong -->
   xmlns:hex="clr-namespace:WpfHexEditor"

   <!-- ✅ Correct -->
   xmlns:hex="clr-namespace:WpfHexaEditor;assembly=WPFHexaEditor"
   ```

2. **Package not installed**
   - Solution: Reinstall NuGet package
   ```bash
   dotnet add package WPFHexaEditor
   ```

3. **Build issue**
   - Solution: Clean and rebuild
   ```bash
   dotnet clean
   dotnet build
   ```

---

### Issue 2: "Could not load file or assembly"

**Symptom**: Exception when running application

**Solution 1**: Ensure correct .NET version
```xml
<!-- For .NET 8.0 -->
<TargetFramework>net8.0-windows</TargetFramework>

<!-- For .NET Framework 4.8 -->
<TargetFramework>net48</TargetFramework>
```

**Solution 2**: Enable WPF support
```xml
<PropertyGroup>
  <UseWPF>true</UseWPF>
</PropertyGroup>
```

---

### Issue 3: Control appears but is blank

**Symptom**: Control renders but shows nothing

**Solution**: Open a file or load data
```csharp
// Load file
hexEditor.FileName = "data.bin";

// Or load from memory
byte[] data = { 0x48, 0x65, 0x6C, 0x6C, 0x6F };
hexEditor.OpenMemory(data);
```

---

### Issue 4: IntelliSense not working

**Symptom**: No autocomplete for HexEditor properties/methods

**Solution 1**: Rebuild solution
```bash
dotnet clean
dotnet build
```

**Solution 2**: Restart Visual Studio

**Solution 3**: Delete `.vs` folder and rebuild

---

### Issue 5: NuGet package not found

**Symptom**: "Package 'WPFHexaEditor' is not found"

**Solution**: Check NuGet sources
```bash
# List NuGet sources
dotnet nuget list source

# Add nuget.org if missing
dotnet nuget add source https://api.nuget.org/v3/index.json -n nuget.org
```

---

## 🎨 IDE-Specific Setup

### Visual Studio 2022

1. **Install NuGet Package**
   - Tools → NuGet Package Manager → Manage NuGet Packages for Solution
   - Browse → Search "WPFHexaEditor"
   - Install

2. **Add Control to Toolbox** (Optional)
   - Right-click Toolbox → "Choose Items..."
   - Browse to: `packages\WPFHexaEditor\lib\net8.0-windows\WPFHexaEditor.dll`
   - Select `HexEditor` control
   - Click OK

3. **Drag & Drop**
   - Drag `HexEditor` from Toolbox to your XAML designer

---

### JetBrains Rider

1. **Install NuGet Package**
   - Right-click project → Manage NuGet Packages
   - Search "WPFHexaEditor"
   - Install

2. **Add Namespace**
   - Rider auto-suggests namespace when typing `<HexEditor`
   - Press Alt+Enter → Add namespace

3. **IntelliSense**
   - Full IntelliSense support for properties/methods
   - Rider shows documentation tooltips

---

### Visual Studio Code

1. **Install .NET SDK**
   ```bash
   # Check .NET is installed
   dotnet --version
   ```

2. **Install C# Extension**
   - Extension ID: `ms-dotnettools.csharp`

3. **Add Package**
   ```bash
   dotnet add package WPFHexaEditor
   ```

4. **Build & Run**
   ```bash
   dotnet build
   dotnet run
   ```

**Note**: VS Code has limited WPF designer support. Use XAML manually.

---

## 📚 Next Steps

### After installation, learn:

1. **[Quick Start Tutorial](Quick-Start)** - Build your first hex editor (5 min)
2. **[Basic Operations](Basic-Operations)** - Open, edit, save files
3. **[API Reference](API-Reference)** - Complete API documentation
4. **[Sample Applications](Sample-Applications)** - Working examples

---

## 💡 Pro Tips

### 1. Use Latest Version

Always use the latest stable version for bug fixes and features:

```bash
# Update to latest
dotnet add package WPFHexaEditor
```

Check latest version: https://www.nuget.org/packages/WPFHexaEditor/

---

### 2. Enable Nullable Reference Types (C# 8.0+)

```xml
<PropertyGroup>
  <Nullable>enable</Nullable>
</PropertyGroup>
```

WPF HexEditor is nullable-aware!

---

### 3. Optimize for Performance

For .NET 8.0, enable additional optimizations:

```xml
<PropertyGroup>
  <TieredCompilation>true</TieredCompilation>
  <TieredCompilationQuickJit>true</TieredCompilationQuickJit>
  <TieredCompilationQuickJitForLoops>true</TieredCompilationQuickJitForLoops>
</PropertyGroup>
```

---

## 🆘 Need Help?

Still having issues?

- 📖 **[FAQ](FAQ)** - Common questions
- 🐛 **[Troubleshooting](Troubleshooting)** - Fix common problems
- 💬 **[GitHub Discussions](https://github.com/abbaye/WpfHexEditorIDE/discussions)** - Ask community
- 📧 **Email**: derektremblay666@gmail.com

---

<div align="center">
  <br/>
  <p>
    <b>✅ Installation Complete!</b><br/>
    Ready to build something awesome?
  </p>
  <br/>
  <p>
    👉 <a href="Quick-Start"><b>Quick Start Tutorial</b></a> •
    <a href="Basic-Operations"><b>Basic Operations</b></a> •
    <a href="Sample-Applications"><b>Sample Apps</b></a>
  </p>
</div>
