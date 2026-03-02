# Quick Start Tutorial

Get up and running with WPF HexEditor in under 5 minutes! 🚀

---

## 📋 Prerequisites

- **Visual Studio 2022** (or later) or **Rider**
- **.NET Framework 4.8** or **.NET 8.0-windows**
- **Basic C# and WPF knowledge**

---

## 🎯 Goal

By the end of this tutorial, you'll have a working hex editor that can:
- ✅ Open and display binary files
- ✅ Navigate through hex data
- ✅ Edit bytes
- ✅ Save changes

**Time:** ~5 minutes ⏱️

---

## Step 1: Create WPF Project

### Using Visual Studio

1. **File → New → Project**
2. Select **"WPF Application"**
3. Name: `MyHexEditor`
4. Framework: **.NET 8.0-windows** (recommended) or **.NET Framework 4.8**
5. Click **Create**

### Using .NET CLI

```bash
dotnet new wpf -n MyHexEditor
cd MyHexEditor
```

---

## Step 2: Install NuGet Package

### Using Package Manager Console

```powershell
Install-Package WPFHexaEditor
```

### Using .NET CLI

```bash
dotnet add package WPFHexaEditor
```

### Using Visual Studio NuGet Manager

1. **Right-click project → Manage NuGet Packages**
2. Search: `WPFHexaEditor`
3. Click **Install**

✅ **Verify**: Check that `WPFHexaEditor` appears in your Dependencies/Packages

---

## Step 3: Add HexEditor to XAML

Open `MainWindow.xaml` and replace the content:

```xml
<Window x:Class="MyHexEditor.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:hex="clr-namespace:WpfHexaEditor;assembly=WPFHexaEditor"
        Title="My Hex Editor" Height="600" Width="900">

    <Grid>
        <!-- Define rows -->
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>  <!-- Toolbar -->
            <RowDefinition Height="*"/>     <!-- Hex Editor -->
            <RowDefinition Height="Auto"/>  <!-- Status Bar -->
        </Grid.RowDefinitions>

        <!-- Toolbar -->
        <ToolBar Grid.Row="0">
            <Button Content="Open" Click="OpenButton_Click"/>
            <Button Content="Save" Click="SaveButton_Click"/>
            <Separator/>
            <Button Content="Undo" Click="UndoButton_Click"/>
            <Button Content="Redo" Click="RedoButton_Click"/>
        </ToolBar>

        <!-- Hex Editor Control -->
        <hex:HexEditor x:Name="hexEditor"
                       Grid.Row="1"
                       Margin="5"/>

        <!-- Status Bar -->
        <StatusBar Grid.Row="2">
            <StatusBarItem>
                <TextBlock x:Name="statusText" Text="Ready"/>
            </StatusBarItem>
        </StatusBar>
    </Grid>
</Window>
```

**Key points:**
- `xmlns:hex` namespace declaration imports the control
- `x:Name="hexEditor"` gives us a reference in code-behind
- Grid layout with toolbar, editor, and status bar

---

## Step 4: Add Code-Behind

Open `MainWindow.xaml.cs` and add event handlers:

```csharp
using System.Windows;
using Microsoft.Win32;

namespace MyHexEditor
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            // Open file dialog
            var dialog = new OpenFileDialog
            {
                Filter = "All Files (*.*)|*.*",
                Title = "Select a file to open"
            };

            if (dialog.ShowDialog() == true)
            {
                // Open file in hex editor
                hexEditor.FileName = dialog.FileName;
                statusText.Text = $"Opened: {dialog.FileName}";
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Save changes
            hexEditor.Save();
            statusText.Text = "File saved";
            MessageBox.Show("File saved successfully!", "Success",
                           MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            // Undo last change
            if (hexEditor.CanUndo)
            {
                hexEditor.Undo();
                statusText.Text = "Undo";
            }
        }

        private void RedoButton_Click(object sender, RoutedEventArgs e)
        {
            // Redo change
            if (hexEditor.CanRedo)
            {
                hexEditor.Redo();
                statusText.Text = "Redo";
            }
        }
    }
}
```

---

## Step 5: Run Your Application

1. **Press F5** to build and run
2. **Click "Open"** to select a file
3. **Edit bytes** by clicking and typing hex values
4. **Click "Save"** to persist changes
5. **Try Undo/Redo** to navigate edit history

🎉 **Congratulations!** You have a working hex editor!

---

## 🎨 Customize the Appearance

### Change Colors

```xml
<hex:HexEditor x:Name="hexEditor"
               SelectionFirstColor="#FF3399FF"
               SelectionSecondColor="#FF0066CC"
               ModifiedByteColor="#FFFF0000"
               InsertedByteColor="#FF00FF00"/>
```

### Adjust Layout

```xml
<hex:HexEditor x:Name="hexEditor"
               BytesPerLine="32"
               FontFamily="Consolas"
               FontSize="14"/>
```

### Hide/Show Columns

```xml
<hex:HexEditor x:Name="hexEditor"
               ShowOffset="True"
               ShowHex="True"
               ShowASCII="True"
               ShowBarChart="False"/>
```

---

## 📊 Display File Information

Add file info to your status bar:

```csharp
private void OpenButton_Click(object sender, RoutedEventArgs e)
{
    var dialog = new OpenFileDialog();

    if (dialog.ShowDialog() == true)
    {
        hexEditor.FileName = dialog.FileName;

        // Display file information
        long fileSize = hexEditor.Length;
        string sizeText = fileSize < 1024
            ? $"{fileSize} bytes"
            : $"{fileSize / 1024:N0} KB";

        statusText.Text = $"Opened: {dialog.FileName} ({sizeText})";
    }
}
```

---

## 🔍 Add Search Functionality

Extend your UI with search:

```xml
<!-- Add to ToolBar -->
<Separator/>
<TextBox x:Name="searchBox" Width="200"
         Text="Enter hex pattern..."
         GotFocus="SearchBox_GotFocus"/>
<Button Content="Find" Click="FindButton_Click"/>
```

```csharp
private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
{
    // Clear placeholder text
    if (searchBox.Text == "Enter hex pattern...")
    {
        searchBox.Text = "";
    }
}

private void FindButton_Click(object sender, RoutedEventArgs e)
{
    // Convert hex string to bytes
    string hex = searchBox.Text.Replace(" ", "");

    if (hex.Length % 2 != 0)
    {
        MessageBox.Show("Invalid hex string", "Error");
        return;
    }

    byte[] pattern = new byte[hex.Length / 2];
    for (int i = 0; i < hex.Length; i += 2)
    {
        pattern[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
    }

    // Find pattern
    long position = hexEditor.FindFirst(pattern);

    if (position >= 0)
    {
        hexEditor.SetPosition(position);
        statusText.Text = $"Found at position 0x{position:X}";
    }
    else
    {
        MessageBox.Show("Pattern not found", "Search");
        statusText.Text = "Pattern not found";
    }
}
```

---

## 📝 Track Modifications

Show when file is modified:

```csharp
public MainWindow()
{
    InitializeComponent();

    // Subscribe to data changed event
    hexEditor.DataChanged += HexEditor_DataChanged;
}

private void HexEditor_DataChanged(object sender, EventArgs e)
{
    // Update status bar
    bool isModified = hexEditor.HasChanges;
    statusText.Text = isModified ? "Modified *" : "Ready";

    // Update window title
    Title = isModified ? "My Hex Editor *" : "My Hex Editor";
}
```

---

## ⚡ Enable Async Operations

For large files, use async save with progress:

```xml
<!-- Add ProgressBar to StatusBar -->
<StatusBar Grid.Row="2">
    <StatusBarItem>
        <TextBlock x:Name="statusText" Text="Ready"/>
    </StatusBarItem>
    <StatusBarItem HorizontalAlignment="Right">
        <ProgressBar x:Name="progressBar"
                     Width="200" Height="16"
                     Visibility="Collapsed"/>
    </StatusBarItem>
</StatusBar>
```

```csharp
private async void SaveButton_Click(object sender, RoutedEventArgs e)
{
    // Show progress bar
    progressBar.Visibility = Visibility.Visible;
    progressBar.Value = 0;

    // Create progress reporter
    var progress = new Progress<double>(percent =>
    {
        progressBar.Value = percent;
        statusText.Text = $"Saving: {percent:F1}%";
    });

    try
    {
        // Async save
        await hexEditor.SaveAsync(progress);

        statusText.Text = "File saved";
        MessageBox.Show("File saved successfully!", "Success");
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Error saving file: {ex.Message}", "Error");
    }
    finally
    {
        // Hide progress bar
        progressBar.Visibility = Visibility.Collapsed;
    }
}
```

---

## 🎯 Next Steps

### Learn More

- **[Basic Operations](Basic-Operations)** - Common editing tasks
- **[API Reference](API-Reference)** - Complete API documentation
- **[Sample Applications](Sample-Applications)** - Advanced examples
- **[Best Practices](Best-Practices)** - Performance tips

### Try Advanced Features

- **[Search & Replace](API-Search-Operations)** - Pattern matching
- **[Bookmarks](API-Features#bookmarks)** - Quick navigation
- **[Highlights](API-Features#highlights)** - Visual markers
- **[TBL Files](API-Features#tbl-support)** - Custom character tables

### Build Real Applications

Check out our sample applications for inspiration:
- **[C# WPF Sample](https://github.com/abbaye/WpfHexEditorIDE/tree/master/Sources/Samples/WPFHexEditor.Sample.CSharp)** - Full-featured demo
- **[Binary Diff](https://github.com/abbaye/WpfHexEditorIDE/tree/master/Sources/Samples/WpfHexEditor.Sample.BinaryFilesDifference)** - File comparison
- **[AvalonDock Integration](https://github.com/abbaye/WpfHexEditorIDE/tree/master/Sources/Samples/WpfHexEditor.Sample.AvalonDock)** - Dockable UI

---

## ❓ Common Issues

### Issue: Control doesn't appear

**Solution**: Verify namespace declaration:
```xml
xmlns:hex="clr-namespace:WpfHexaEditor;assembly=WPFHexaEditor"
```

### Issue: "Type not found" error

**Solution**: Clean and rebuild solution:
```bash
dotnet clean
dotnet build
```

### Issue: Large file opens slowly

**Solution**: Use async operations:
```csharp
await hexEditor.OpenAsync("largefile.bin", progress);
```

---

## 🆘 Need Help?

- 📖 **[FAQ](FAQ)** - Frequently asked questions
- 🐛 **[Troubleshooting](Troubleshooting)** - Common problems
- 💬 **[GitHub Discussions](https://github.com/abbaye/WpfHexEditorIDE/discussions)** - Community Q&A
- 📧 **Email**: derektremblay666@gmail.com

---

<div align="center">
  <br/>
  <p>
    <b>🎉 You're all set!</b><br/>
    Time to build something awesome with WPF HexEditor!
  </p>
  <br/>
  <p>
    👉 <a href="Basic-Operations"><b>Learn Basic Operations</b></a> •
    <a href="Sample-Applications"><b>Explore Samples</b></a> •
    <a href="API-Reference"><b>API Docs</b></a>
  </p>
</div>
