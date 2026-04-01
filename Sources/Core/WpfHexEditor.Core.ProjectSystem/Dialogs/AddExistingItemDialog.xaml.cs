// ==========================================================
// Project: WpfHexEditor.ProjectSystem
// File: AddExistingItemDialog.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026
// Description:
//     "Add Existing Item" dialog — lets the user pick one or more existing files
//     and configure how they are added to the project (copy vs. reference,
//     physical destination subfolder).
//
// Architecture Notes:
//     ThemedDialog base class (WindowStyle=None, custom chrome, VS2022-style).
//     Multi-file list: ItemsControl bound to List<FileEntry>.
//     Destination folder: TreeView bound to physical directories on disk (not virtual folders).
//     New Folder: creates directory immediately on disk (Directory.CreateDirectory — idempotent).
// ==========================================================

using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Core.ProjectSystem.Dialogs;

/// <summary>
/// "Add Existing Item" dialog.
/// After <see cref="Window.ShowDialog"/> returns <c>true</c>, read:
/// <list type="bullet">
///   <item><see cref="SelectedFilePaths"/> — source paths chosen by the user</item>
///   <item><see cref="CopyToProject"/> — whether files should be copied into the project</item>
///   <item><see cref="SelectedPhysicalDestination"/> — absolute path of the target directory, or <c>null</c> for the project root</item>
/// </list>
/// </summary>
public partial class AddExistingItemDialog : WpfHexEditor.Editor.Core.Views.ThemedDialog
{
    // -- Inner model ----------------------------------------------------------

    /// <summary>One row in the multi-file list.</summary>
    private sealed record FileEntry(
        string FullPath,
        string FileName,
        string TypeLabel,
        ProjectItemType Type,
        string IconGlyph);

    // -- State ----------------------------------------------------------------
    private readonly IProject _project;
    private          string[] _filePaths = [];

    // -- Output properties ----------------------------------------------------

    /// <summary>Source paths selected by the user.</summary>
    public IReadOnlyList<string> SelectedFilePaths         { get; private set; } = [];

    /// <summary>Whether files should be copied into the project directory.</summary>
    public bool    CopyToProject               { get; private set; } = true;

    /// <summary>
    /// Absolute path of the physical directory where files will be copied.
    /// <c>null</c> means the project root directory.
    /// Only meaningful when <see cref="CopyToProject"/> is <c>true</c>.
    /// </summary>
    public string? SelectedPhysicalDestination { get; private set; }

    // -- Constructor ----------------------------------------------------------
    public AddExistingItemDialog(IProject project)
    {
        InitializeComponent();

        _project = project;

        PopulateTypeCombo();
        PopulatePhysicalFolderTree();
        UpdatePlacementModeVisuals();

        Refresh();
    }

    // -- Initialisation helpers -----------------------------------------------

    private void PopulateTypeCombo()
    {
        foreach (ProjectItemType t in Enum.GetValues<ProjectItemType>())
        {
            if (t is ProjectItemType.Unknown or ProjectItemType.Comparison)
                continue;

            TypeCombo.Items.Add(new ComboBoxItem { Content = t.ToString(), Tag = t });
        }
        TypeCombo.SelectedIndex = 0;
    }

    /// <summary>
    /// Builds the physical directory tree rooted at the project's directory.
    /// The Tag of each TreeViewItem is the absolute path (string) of that directory.
    /// </summary>
    private void PopulatePhysicalFolderTree()
    {
        FolderTree.Items.Clear();

        var projDir  = Path.GetDirectoryName(_project.ProjectFilePath) ?? "";
        var rootItem = BuildPhysicalDirItem(projDir, "(project root)", isRoot: true);
        rootItem.IsSelected = true;
        rootItem.IsExpanded = true;

        FolderTree.Items.Add(rootItem);
    }

    private static TreeViewItem BuildPhysicalDirItem(string absolutePath, string displayName, bool isRoot = false)
    {
        var item = new TreeViewItem
        {
            Header     = BuildFolderHeader(displayName),
            Tag        = absolutePath,
            IsExpanded = isRoot,
        };

        try
        {
            foreach (var sub in Directory.GetDirectories(absolutePath))
                item.Items.Add(BuildPhysicalDirItem(sub, Path.GetFileName(sub)));
        }
        catch (UnauthorizedAccessException) { /* skip inaccessible dirs */ }

        return item;
    }

    /// <summary>Builds a themed folder header (icon + label) for a TreeViewItem.</summary>
    private static StackPanel BuildFolderHeader(string displayName)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };

        var icon = new System.Windows.Controls.TextBlock
        {
            Text              = "\uE8B7",   // Folder glyph (Segoe MDL2)
            FontFamily        = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
            FontSize          = 12,
            Margin            = new Thickness(0, 0, 5, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        icon.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "DockMenuForegroundBrush");

        var label = new System.Windows.Controls.TextBlock
        {
            Text              = displayName,
            VerticalAlignment = VerticalAlignment.Center,
        };
        label.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "DockMenuForegroundBrush");

        panel.Children.Add(icon);
        panel.Children.Add(label);
        return panel;
    }

    // -- Event handlers -------------------------------------------------------

    private void OnFileDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        var dropped = (string[])e.Data.GetData(DataFormats.FileDrop);
        _filePaths = dropped.Where(File.Exists).ToArray();

        if (_filePaths.Length == 0) return;

        UpdateFilePathBox();
        UpdateDetectedType();
        RebuildFileList();
        Refresh();
    }

    private void OnBrowse(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title       = "Select File(s) to Add",
            Multiselect = true,
            Filter      = "All Files (*.*)|*.*"
                        + "|Binary / ROM Files (*.bin;*.rom;*.smc;*.nes;*.gba;*.iso)|*.bin;*.rom;*.smc;*.nes;*.gba;*.iso"
                        + "|TBL Files (*.tbl;*.tblx)|*.tbl;*.tblx"
                        + "|IPS / BPS Patches (*.ips;*.bps;*.ups;*.xdelta)|*.ips;*.bps;*.ups;*.xdelta"
                        + "|JSON / Format Definitions (*.json;*.whfmt)|*.json;*.whfmt"
                        + "|Images (*.png;*.bmp;*.jpg;*.gif;*.ico;*.tga;*.dds)|*.png;*.bmp;*.jpg;*.gif;*.ico;*.tga;*.dds"
                        + "|Audio (*.wav;*.mp3;*.ogg;*.flac)|*.wav;*.mp3;*.ogg;*.flac"
                        + "|Tile Graphics (*.chr;*.til;*.gfx)|*.chr;*.til;*.gfx"
                        + "|Script / Text (*.lua;*.py;*.asm;*.txt;*.md;*.whlang)|*.lua;*.py;*.asm;*.txt;*.md;*.whlang",
        };

        if (dlg.ShowDialog() != true) return;

        _filePaths = dlg.FileNames;

        UpdateFilePathBox();
        UpdateDetectedType();
        RebuildFileList();
        Refresh();
    }

    private void OnRemoveFileEntry(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: FileEntry entry }) return;

        _filePaths = _filePaths.Where(p => p != entry.FullPath).ToArray();

        UpdateFilePathBox();
        UpdateDetectedType();
        RebuildFileList();
        Refresh();
    }

    private void OnTypeChanged(object sender, SelectionChangedEventArgs e) => Refresh();

    private void OnCopyModeChanged(object sender, RoutedEventArgs e)
    {
        if (ManualPlacementGrid is null) return;

        UpdatePlacementModeAvailability();
        Refresh();
    }

    private void OnPlacementModeChanged(object sender, RoutedEventArgs e)
    {
        UpdatePlacementModeVisuals();
        Refresh();
    }

    private void OnFolderTreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        => Refresh();

    private void OnAdd(object sender, RoutedEventArgs e)
    {
        SelectedFilePaths          = [.. _filePaths];
        CopyToProject              = CopyToProjectRadio.IsChecked == true;
        SelectedPhysicalDestination = CopyToProject ? GetSelectedPhysicalPath() : null;

        DialogResult = true;
    }

    // -- New Folder handlers --------------------------------------------------

    private void OnNewFolder(object sender, RoutedEventArgs e)
    {
        NewFolderFormBorder.Visibility = Visibility.Visible;
        NewFolderNameBox.Text          = "";
        NewFolderNameBox.Focus();
    }

    private void OnConfirmNewFolder(object sender, RoutedEventArgs e)
        => CommitNewFolder();

    private void OnCancelNewFolder(object sender, RoutedEventArgs e)
        => HideNewFolderForm();

    private void OnNewFolderNameKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)  { CommitNewFolder();    e.Handled = true; }
        if (e.Key == Key.Escape) { HideNewFolderForm();  e.Handled = true; }
    }

    private void CommitNewFolder()
    {
        var name = NewFolderNameBox.Text.Trim();
        if (string.IsNullOrEmpty(name)) return;

        // Determine parent from currently selected tree item
        var parentPath = GetSelectedPhysicalPath()
                      ?? Path.GetDirectoryName(_project.ProjectFilePath)!;

        var newDirPath = Path.Combine(parentPath, name);

        try
        {
            // Directory.CreateDirectory is idempotent — safe to call even if it exists
            Directory.CreateDirectory(newDirPath);
        }
        catch (Exception)
        {
            // Ignore creation errors (invalid name, access denied, etc.)
            return;
        }

        HideNewFolderForm();

        // Rebuild the tree so the new directory appears, then select it
        PopulatePhysicalFolderTree();
        SelectPathInTree(newDirPath);
    }

    private void HideNewFolderForm()
    {
        NewFolderFormBorder.Visibility = Visibility.Collapsed;
        NewFolderNameBox.Text          = "";
    }

    // -- Private helpers ------------------------------------------------------

    private void UpdateFilePathBox()
    {
        FilePathBox.Text = _filePaths.Length switch
        {
            0 => "",
            1 => _filePaths[0],
            _ => $"{_filePaths.Length} files selected",
        };
    }

    private void RebuildFileList()
    {
        var entries = _filePaths.Select(path =>
        {
            var type = ProjectItemTypeHelper.FromExtension(Path.GetExtension(path));
            return new FileEntry(
                FullPath:  path,
                FileName:  Path.GetFileName(path),
                TypeLabel: type.ToString(),
                Type:      type,
                IconGlyph: TypeGlyph(type));
        }).ToList();

        FileList.ItemsSource = null;
        FileList.ItemsSource = entries;

        FileListBorder.Visibility = _filePaths.Length >= 2
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void UpdateDetectedType()
    {
        if (_filePaths.Length == 0) return;

        var firstType = ProjectItemTypeHelper.FromExtension(Path.GetExtension(_filePaths[0]));
        var allSame   = _filePaths.All(p =>
            ProjectItemTypeHelper.FromExtension(Path.GetExtension(p)) == firstType);

        TypeCombo.IsEnabled = allSame;

        if (!allSame)
        {
            SelectTypeComboItem(ProjectItemType.Binary);
            FilePathBox.Text = $"{_filePaths.Length} files selected (mixed types)";
            return;
        }

        SelectTypeComboItem(firstType);
    }

    private void SelectTypeComboItem(ProjectItemType type)
    {
        foreach (ComboBoxItem item in TypeCombo.Items)
        {
            if (item.Tag is ProjectItemType t && t == type)
            {
                TypeCombo.SelectedItem = item;
                return;
            }
        }
    }

    private void UpdatePlacementModeAvailability()
    {
        if (ManualFolderRadio is null) return;

        var copying = CopyToProjectRadio.IsChecked == true;

        ManualFolderRadio.IsEnabled = copying;
        AutoFolderRadio.IsEnabled   = true;

        // If copy is disabled, force "keep in place" mode
        if (!copying)
            AutoFolderRadio.IsChecked = true;

        UpdatePlacementModeVisuals();
    }

    private void UpdatePlacementModeVisuals()
    {
        if (ManualPlacementGrid is null || AutoPlacementHint is null) return;

        var isManual = ManualFolderRadio?.IsChecked == true;

        ManualPlacementGrid.Visibility = isManual ? Visibility.Visible   : Visibility.Collapsed;
        AutoPlacementHint.Visibility   = isManual ? Visibility.Collapsed : Visibility.Visible;

        if (!isManual)
            AutoPlacementHint.Text = "Files will be kept at their original location (reference only).";
    }

    private string? GetSelectedPhysicalPath()
        => GetSelectedTreeItem()?.Tag as string;

    private TreeViewItem? GetSelectedTreeItem()
    {
        foreach (TreeViewItem item in FolderTree.Items)
        {
            var found = FindSelectedItem(item);
            if (found is not null) return found;
        }
        return null;
    }

    private static TreeViewItem? FindSelectedItem(TreeViewItem item)
    {
        if (item.IsSelected) return item;
        foreach (TreeViewItem child in item.Items)
        {
            var found = FindSelectedItem(child);
            if (found is not null) return found;
        }
        return null;
    }

    /// <summary>
    /// Walks the tree and selects the node whose Tag matches <paramref name="absolutePath"/>.
    /// </summary>
    private void SelectPathInTree(string absolutePath)
    {
        foreach (TreeViewItem root in FolderTree.Items)
        {
            if (TrySelectPath(root, absolutePath))
                return;
        }
    }

    private static bool TrySelectPath(TreeViewItem item, string path)
    {
        if (string.Equals(item.Tag as string, path, StringComparison.OrdinalIgnoreCase))
        {
            item.IsSelected = true;
            item.BringIntoView();
            return true;
        }
        item.IsExpanded = true;
        foreach (TreeViewItem child in item.Items)
        {
            if (TrySelectPath(child, path)) return true;
        }
        return false;
    }

    private void Refresh()
    {
        if (AddButton is null) return;

        var hasFiles = _filePaths.Length > 0;
        AddButton.IsEnabled = hasFiles;

        UpdatePlacementModeVisuals();

        if (!hasFiles)
        {
            PreviewPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var copying  = CopyToProjectRadio.IsChecked == true;
        var isManual = ManualFolderRadio?.IsChecked == true;
        var first    = _filePaths[0];

        string destPreview;
        if (!copying)
        {
            destPreview = first;
        }
        else
        {
            var destDir = isManual
                ? (GetSelectedPhysicalPath() ?? Path.GetDirectoryName(_project.ProjectFilePath)!)
                : Path.GetDirectoryName(_project.ProjectFilePath)!;
            destPreview = Path.Combine(destDir, Path.GetFileName(first));
        }

        if (_filePaths.Length > 1)
            destPreview += $"  (+{_filePaths.Length - 1} more)";

        PhysicalPreviewText.Text = destPreview;
        PreviewPanel.Visibility  = Visibility.Visible;
    }

    private static string TypeGlyph(ProjectItemType type) => type switch
    {
        ProjectItemType.Binary           => "\uE7EE",  // Storage
        ProjectItemType.Tbl              => "\uE8D2",  // Table
        ProjectItemType.Patch            => "\uE70F",  // Edit
        ProjectItemType.FormatDefinition => "\uE8A5",  // Page
        ProjectItemType.Json             => "\uE943",  // Code
        ProjectItemType.Text             => "\uE8A5",  // Document
        ProjectItemType.Script           => "\uE943",  // Code
        ProjectItemType.Image            => "\uEB9F",  // Picture
        ProjectItemType.Tile             => "\uE80A",  // Tiles
        ProjectItemType.Audio            => "\uE8D6",  // Audio
        _                                => "\uE8B7",  // Folder (fallback)
    };
}
