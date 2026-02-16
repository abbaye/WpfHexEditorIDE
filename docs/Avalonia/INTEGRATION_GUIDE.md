# Integration Guide - WPF vs Avalonia

This guide shows side-by-side comparison of how to integrate the HexEditor control in WPF and Avalonia projects.

---

## 📦 NuGet Package Installation

### WPF Project

```xml
<!-- YourApp.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
  </PropertyGroup>

  <ItemGroup>
    <!-- Install WPF version -->
    <PackageReference Include="WpfHexaEditor.Wpf" Version="3.0.0" />
  </ItemGroup>
</Project>
```

**Package Manager Console:**
```powershell
Install-Package WpfHexaEditor.Wpf
```

**dotnet CLI:**
```bash
dotnet add package WpfHexaEditor.Wpf
```

---

### Avalonia Project

```xml
<!-- YourApp.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <!-- Avalonia packages -->
    <PackageReference Include="Avalonia" Version="11.0.0" />
    <PackageReference Include="Avalonia.Desktop" Version="11.0.0" />

    <!-- Install Avalonia version -->
    <PackageReference Include="WpfHexaEditor.Avalonia" Version="3.0.0" />
  </ItemGroup>
</Project>
```

**Package Manager Console:**
```powershell
Install-Package WpfHexaEditor.Avalonia
```

**dotnet CLI:**
```bash
dotnet add package WpfHexaEditor.Avalonia
```

---

## 🎨 XAML Integration

### WPF - MainWindow.xaml

```xml
<Window x:Class="MyWpfApp.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:hex="clr-namespace:WpfHexaEditor.Wpf.Controls;assembly=WpfHexaEditor.Wpf"
        Title="Hex Editor - WPF"
        Height="600" Width="1000">

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Toolbar -->
        <ToolBar Grid.Row="0">
            <Button Content="Open" Click="OpenFile_Click"/>
            <Button Content="Save" Click="SaveFile_Click"/>
            <Separator/>
            <Button Content="Undo" Click="Undo_Click"/>
            <Button Content="Redo" Click="Redo_Click"/>
        </ToolBar>

        <!-- HexEditor Control -->
        <hex:HexEditor Grid.Row="1"
                       x:Name="HexEditor"
                       AllowDrop="True"
                       AllowZoom="True"
                       MouseWheelScrollSpeed="3"
                       BytePerLine="16"
                       ByteSpacing="1"
                       ShowStatusBar="True"
                       ShowHeader="True"
                       ShowOffset="True"
                       ShowByteCount="True"
                       ByteModified="HexEditor_ByteModified"
                       SelectionChanged="HexEditor_SelectionChanged"
                       PositionChanged="HexEditor_PositionChanged"/>

        <!-- Status Bar -->
        <StatusBar Grid.Row="2">
            <StatusBarItem>
                <TextBlock x:Name="StatusText" Text="Ready"/>
            </StatusBarItem>
        </StatusBar>
    </Grid>
</Window>
```

---

### Avalonia - MainWindow.axaml

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:hex="clr-namespace:WpfHexaEditor.Avalonia.Controls;assembly=WpfHexaEditor.Avalonia"
        x:Class="MyAvaloniaApp.MainWindow"
        Title="Hex Editor - Avalonia"
        Width="1000" Height="600">

    <Grid RowDefinitions="Auto,*,Auto">

        <!-- Toolbar -->
        <StackPanel Grid.Row="0"
                    Orientation="Horizontal"
                    Spacing="5"
                    Margin="5">
            <Button Content="Open" Click="OpenFile_Click"/>
            <Button Content="Save" Click="SaveFile_Click"/>
            <Separator/>
            <Button Content="Undo" Click="Undo_Click"/>
            <Button Content="Redo" Click="Redo_Click"/>
        </StackPanel>

        <!-- HexEditor Control -->
        <hex:HexEditor Grid.Row="1"
                       x:Name="HexEditor"
                       AllowDrop="True"
                       AllowZoom="True"
                       MouseWheelScrollSpeed="3"
                       BytePerLine="16"
                       ByteSpacing="1"
                       ShowStatusBar="True"
                       ShowHeader="True"
                       ShowOffset="True"
                       ShowByteCount="True"
                       ByteModified="HexEditor_ByteModified"
                       SelectionChanged="HexEditor_SelectionChanged"
                       PositionChanged="HexEditor_PositionChanged"/>

        <!-- Status Bar -->
        <Border Grid.Row="2"
                Background="#F0F0F0"
                Padding="5">
            <TextBlock x:Name="StatusText" Text="Ready"/>
        </Border>
    </Grid>
</Window>
```

### 🔍 XAML Differences

| Feature | WPF | Avalonia | Notes |
|---------|-----|----------|-------|
| **Namespace** | `xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"` | `xmlns="https://github.com/avaloniaui"` | Different XML namespaces |
| **Control Namespace** | `clr-namespace:WpfHexaEditor.Wpf.Controls` | `clr-namespace:WpfHexaEditor.Avalonia.Controls` | Platform-specific assemblies |
| **Grid RowDefinitions** | `<Grid.RowDefinitions>` | `<Grid RowDefinitions="Auto,*,Auto">` | Avalonia uses compact syntax |
| **ToolBar** | `<ToolBar>` | `<StackPanel Orientation="Horizontal">` | Avalonia doesn't have ToolBar |
| **StatusBar** | `<StatusBar>` with `<StatusBarItem>` | `<Border>` with styled content | Different control |
| **API** | Identical for HexEditor | Identical for HexEditor | ✅ Same properties and events! |

---

## 💻 Code-Behind

### WPF - MainWindow.xaml.cs

```csharp
using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using WpfHexaEditor.Core.Bytes;
using WpfHexaEditor.Core.Interfaces;

namespace MyWpfApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Subscribe to events
            HexEditor.ByteModified += HexEditor_ByteModified;
            HexEditor.SelectionChanged += HexEditor_SelectionChanged;
            HexEditor.PositionChanged += HexEditor_PositionChanged;
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "All Files (*.*)|*.*",
                Title = "Open File"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    // Open file in HexEditor
                    HexEditor.FileName = dialog.FileName;
                    StatusText.Text = $"Opened: {Path.GetFileName(dialog.FileName)}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error opening file: {ex.Message}",
                                  "Error",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Error);
                }
            }
        }

        private void SaveFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                HexEditor.SubmitChanges();
                StatusText.Text = "File saved successfully";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving file: {ex.Message}",
                              "Error",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
            }
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            HexEditor.Undo();
        }

        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            HexEditor.Redo();
        }

        private void HexEditor_ByteModified(object sender, EventArgs e)
        {
            StatusText.Text = "File modified";
        }

        private void HexEditor_SelectionChanged(object sender, EventArgs e)
        {
            if (HexEditor.SelectionLength > 0)
            {
                StatusText.Text = $"Selection: {HexEditor.SelectionStart} - " +
                                $"{HexEditor.SelectionStop} " +
                                $"({HexEditor.SelectionLength} bytes)";
            }
        }

        private void HexEditor_PositionChanged(object sender, EventArgs e)
        {
            StatusText.Text = $"Position: 0x{HexEditor.Position:X8}";
        }
    }
}
```

---

### Avalonia - MainWindow.axaml.cs

```csharp
using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using WpfHexaEditor.Core.Bytes;
using WpfHexaEditor.Core.Interfaces;

namespace MyAvaloniaApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Subscribe to events (identical to WPF!)
            HexEditor.ByteModified += HexEditor_ByteModified;
            HexEditor.SelectionChanged += HexEditor_SelectionChanged;
            HexEditor.PositionChanged += HexEditor_PositionChanged;
        }

        private async void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            // Avalonia uses async file picker
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open File",
                AllowMultiple = false
            });

            if (files.Count > 0)
            {
                try
                {
                    var filePath = files[0].Path.LocalPath;

                    // Open file in HexEditor (same API as WPF!)
                    HexEditor.FileName = filePath;
                    StatusText.Text = $"Opened: {Path.GetFileName(filePath)}";
                }
                catch (Exception ex)
                {
                    // Avalonia message box
                    await ShowMessageBox("Error", $"Error opening file: {ex.Message}");
                }
            }
        }

        private async void SaveFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Same API as WPF!
                HexEditor.SubmitChanges();
                StatusText.Text = "File saved successfully";
            }
            catch (Exception ex)
            {
                await ShowMessageBox("Error", $"Error saving file: {ex.Message}");
            }
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            HexEditor.Undo(); // Same API as WPF!
        }

        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            HexEditor.Redo(); // Same API as WPF!
        }

        // Event handlers - IDENTICAL to WPF!
        private void HexEditor_ByteModified(object sender, EventArgs e)
        {
            StatusText.Text = "File modified";
        }

        private void HexEditor_SelectionChanged(object sender, EventArgs e)
        {
            if (HexEditor.SelectionLength > 0)
            {
                StatusText.Text = $"Selection: {HexEditor.SelectionStart} - " +
                                $"{HexEditor.SelectionStop} " +
                                $"({HexEditor.SelectionLength} bytes)";
            }
        }

        private void HexEditor_PositionChanged(object sender, EventArgs e)
        {
            StatusText.Text = $"Position: 0x{HexEditor.Position:X8}";
        }

        // Helper method for Avalonia message boxes
        private async Task ShowMessageBox(string title, string message)
        {
            var messageBox = new Window
            {
                Title = title,
                Width = 400,
                Height = 150,
                Content = new StackPanel
                {
                    Margin = new Thickness(20),
                    Children =
                    {
                        new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                        new Button
                        {
                            Content = "OK",
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 20, 0, 0)
                        }
                    }
                }
            };
            await messageBox.ShowDialog(this);
        }
    }
}
```

### 🔍 Code-Behind Differences

| Feature | WPF | Avalonia | Notes |
|---------|-----|----------|-------|
| **File Picker** | `OpenFileDialog` | `StorageProvider.OpenFilePickerAsync()` | Avalonia uses async |
| **Message Box** | `MessageBox.Show()` | Custom window or library | No built-in MessageBox |
| **HexEditor API** | ✅ Identical | ✅ Identical | Same properties, methods, events! |
| **Event Handlers** | ✅ Identical | ✅ Identical | Same signatures! |
| **Async/Await** | Optional | Required for dialogs | Avalonia is more async-oriented |

---

## 🎨 Advanced Features - Themes

### WPF - App.xaml

```xml
<Application x:Class="MyWpfApp.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             StartupUri="MainWindow.xaml">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <!-- Load HexEditor theme -->
                <ResourceDictionary Source="pack://application:,,,/WpfHexaEditor.Wpf;component/Resources/Themes/Dark.xaml"/>
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

**Available WPF Themes:**
- `Light.xaml`
- `Dark.xaml`
- `Cyberpunk.xaml`
- `HighContrast.xaml`

---

### Avalonia - App.axaml

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="MyAvaloniaApp.App">
    <Application.Styles>
        <!-- Avalonia Fluent theme -->
        <FluentTheme />

        <!-- Load HexEditor theme -->
        <StyleInclude Source="avares://WpfHexaEditor.Avalonia/Resources/Themes/Dark.axaml"/>
    </Application.Styles>
</Application>
```

**Available Avalonia Themes:**
- `Light.axaml`
- `Dark.axaml`
- `Cyberpunk.axaml`

---

## 🔧 Advanced Usage - Working with Bytes

### Both WPF and Avalonia (Identical Code!)

```csharp
using WpfHexaEditor.Core.Bytes;
using WpfHexaEditor.Core.Interfaces;

public class HexEditorExample
{
    public void WorkWithBytes()
    {
        // Get byte at position
        byte? byteValue = HexEditor.GetByte(0x100);

        // Modify byte
        HexEditor.ModifyByte(0x100, 0xFF);

        // Get selection
        byte[] selectedBytes = HexEditor.SelectionByteArray;

        // Search for pattern
        HexEditor.FindAll(new byte[] { 0x4D, 0x5A }, true); // Find "MZ"

        // Replace pattern
        HexEditor.ReplaceAll(
            new byte[] { 0x00, 0x00 },
            new byte[] { 0xFF, 0xFF }
        );

        // Get selection as string
        string selectedText = HexEditor.SelectionText;

        // Copy/Paste
        HexEditor.CopyToClipboard();
        HexEditor.PasteFromClipboard();

        // Undo/Redo
        HexEditor.Undo();
        HexEditor.Redo();
        HexEditor.ClearUndoRedoHistory();

        // Stream access
        using (var stream = HexEditor.GetStream())
        {
            stream.Seek(0x100, SeekOrigin.Begin);
            var buffer = new byte[256];
            stream.Read(buffer, 0, buffer.Length);
        }
    }

    public void ConfigureEditor()
    {
        // Display settings
        HexEditor.BytePerLine = 16;
        HexEditor.ByteSpacing = 1;
        HexEditor.ShowOffset = true;
        HexEditor.ShowHeader = true;
        HexEditor.ShowByteCount = true;
        HexEditor.ShowStatusBar = true;

        // Formatting
        HexEditor.OffsetFormat = OffsetFormat.Hexadecimal;
        HexEditor.ByteOrder = ByteOrder.LittleEndian;
        HexEditor.ByteSize = ByteSize.Byte;

        // Behavior
        HexEditor.AllowZoom = true;
        HexEditor.AllowDrop = true;
        HexEditor.ReadOnlyMode = false;
        HexEditor.MouseWheelScrollSpeed = 3;

        // Colors (theme-independent)
        HexEditor.SelectionBackgroundColor = Color.FromArgb(0xFF, 0x00, 0x78, 0xD4);
        HexEditor.HighlightColor = Color.FromArgb(0xFF, 0xFF, 0xFF, 0x00);
    }
}
```

**✅ Key Point:** The entire `WpfHexaEditor.Core` API is **100% identical** between WPF and Avalonia!

---

## 📊 Feature Comparison

### Supported Features Matrix

| Feature | WPF | Avalonia | Notes |
|---------|:---:|:--------:|-------|
| **File Operations** |
| Open file | ✅ | ✅ | Same API |
| Save changes | ✅ | ✅ | Same API |
| Large file support (>1GB) | ✅ | ✅ | Lazy loading |
| Memory-mapped files | ✅ | ✅ | Performance optimized |
| **Editing** |
| Insert/Delete bytes | ✅ | ✅ | Same API |
| Copy/Paste | ✅ | ✅ | Same API |
| Undo/Redo | ✅ | ✅ | Unlimited history |
| Find/Replace | ✅ | ✅ | Same API |
| Multi-byte selection | ✅ | ✅ | Same API |
| **Display** |
| Hex view | ✅ | ✅ | Same rendering |
| ASCII view | ✅ | ✅ | Same rendering |
| Offset column | ✅ | ✅ | Customizable |
| Status bar | ✅ | ✅ | File info, position |
| Byte frequency chart | ✅ | ✅ | Histogram |
| **Customization** |
| Themes | ✅ (4 themes) | ✅ (3 themes) | Dark, Light, Cyberpunk |
| Byte coloring | ✅ | ✅ | Custom highlights |
| Custom character tables | ✅ | ✅ | TBL support |
| **Performance** |
| Virtual scrolling | ✅ | ✅ | Same implementation |
| Frozen brushes | ✅ | ✅ | Optimized rendering |
| Lazy loading | ✅ | ✅ | Memory efficient |
| **Platform Features** |
| Windows | ✅ | ✅ | Full support |
| Linux | ❌ | ✅ | Avalonia only |
| macOS | ❌ | ✅ | Avalonia only |

---

## 🚀 Migration Guide (WPF → Avalonia)

If you have an existing WPF app and want to port to Avalonia:

### Step 1: Update Project File
```xml
<!-- Change from WPF to Avalonia -->
<PropertyGroup>
  <TargetFramework>net8.0</TargetFramework> <!-- Remove -windows -->
  <!-- <UseWPF>true</UseWPF> --> <!-- Remove WPF -->
</PropertyGroup>

<ItemGroup>
  <!-- Add Avalonia packages -->
  <PackageReference Include="Avalonia" Version="11.0.0" />
  <PackageReference Include="Avalonia.Desktop" Version="11.0.0" />

  <!-- Change HexEditor package -->
  <!-- <PackageReference Include="WpfHexaEditor.Wpf" Version="3.0.0" /> -->
  <PackageReference Include="WpfHexaEditor.Avalonia" Version="3.0.0" />
</ItemGroup>
```

### Step 2: Update XAML Namespace
```xml
<!-- WPF -->
xmlns:hex="clr-namespace:WpfHexaEditor.Wpf.Controls;assembly=WpfHexaEditor.Wpf"

<!-- Avalonia -->
xmlns:hex="clr-namespace:WpfHexaEditor.Avalonia.Controls;assembly=WpfHexaEditor.Avalonia"
```

### Step 3: Update File Extension
- Rename `.xaml` → `.axaml`
- Rename `.xaml.cs` → `.axaml.cs`

### Step 4: Update Code-Behind
```csharp
// WPF
using System.Windows;
using System.Windows.Controls;

// Avalonia
using Avalonia.Controls;
using Avalonia.Interactivity;
```

### Step 5: Update Platform-Specific Code
```csharp
// WPF: OpenFileDialog
var dialog = new OpenFileDialog();
if (dialog.ShowDialog() == true)
{
    var file = dialog.FileName;
}

// Avalonia: StorageProvider
var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions());
if (files.Count > 0)
{
    var file = files[0].Path.LocalPath;
}
```

### ✅ What Stays the Same
```csharp
// ALL HexEditor API calls remain IDENTICAL:
HexEditor.FileName = "file.bin";
HexEditor.ModifyByte(0x100, 0xFF);
HexEditor.FindAll(pattern, caseSensitive);
HexEditor.Undo();
HexEditor.SelectionStart = 0x200;
// etc... 100% compatible!
```

---

## 💡 Best Practices

### WPF Projects
```csharp
// ✅ DO: Use data binding
<hex:HexEditor FileName="{Binding CurrentFile}" />

// ✅ DO: Use commands
<Button Command="{Binding SaveCommand}" />

// ✅ DO: Handle memory properly
protected override void OnClosed(EventArgs e)
{
    HexEditor.CloseFile();
    base.OnClosed(e);
}
```

### Avalonia Projects
```csharp
// ✅ DO: Use async/await for I/O
private async Task LoadFileAsync(string path)
{
    await Task.Run(() => HexEditor.FileName = path);
}

// ✅ DO: Use Avalonia's theming system
<Application.Styles>
    <FluentTheme Mode="Dark" />
</Application.Styles>

// ✅ DO: Test on all target platforms
// Build and test on Windows, Linux, macOS
```

---

## 🔍 Troubleshooting

### Common Issues

#### WPF: "Could not load file or assembly"
```xml
<!-- Ensure correct TargetFramework -->
<TargetFramework>net8.0-windows</TargetFramework>
<UseWPF>true</UseWPF>
```

#### Avalonia: "Type not found"
```xml
<!-- Ensure Avalonia packages are installed -->
<PackageReference Include="Avalonia" Version="11.0.0" />
<PackageReference Include="WpfHexaEditor.Avalonia" Version="3.0.0" />
```

#### Both: "File too large"
```csharp
// Use preload strategy for large files
HexEditor.PreloadStrategy = PreloadStrategy.Partial;
```

---

## 📚 Examples

### Complete WPF Example
See: [WpfHexEditor.Sample.Main](../Sources/Samples/WpfHexEditor.Sample.Main/)

### Complete Avalonia Example
See: [AvaloniaHexEditor.Sample](../Sources/Samples/AvaloniaHexEditor.Sample/) *(Coming in v3.0)*

---

## 🎯 Summary

| Aspect | WPF | Avalonia | Migration Effort |
|--------|-----|----------|------------------|
| **API** | ✅ Identical | ✅ Identical | 🟢 None |
| **Business Logic** | ✅ Same | ✅ Same | 🟢 None |
| **XAML Namespace** | Different | Different | 🟡 Minimal (1 line) |
| **File Dialogs** | `OpenFileDialog` | `StorageProvider` | 🟡 Minimal (5-10 lines) |
| **Events** | ✅ Same | ✅ Same | 🟢 None |
| **Themes** | `.xaml` | `.axaml` | 🟡 Minimal (extension) |
| **Platform** | Windows only | Cross-platform | 🟢 Benefit! |

**Overall Migration Difficulty: 🟢 EASY** - Most code remains identical!

---

**Related Documentation:**
- [AVALONIA_PORTING_PLAN.md](./AVALONIA_PORTING_PLAN.md) - Implementation roadmap
- [AVALONIA_ARCHITECTURE.md](./AVALONIA_ARCHITECTURE.md) - Architecture diagrams

**Last Updated:** 2026-02-16
