// ==========================================================
// Project: WpfHexEditor.ProjectSystem
// File: AddExistingItemDialog.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026
// Description:
//     "Add Existing Item" dialog — lets the user pick one or more existing files
//     and configure how they are added to the project (copy vs. reference,
//     physical subfolder, virtual folder placement).
//
// Architecture Notes:
//     ThemedDialog base class (WindowStyle=None, custom chrome, VS2022-style).
//     Multi-file list: ItemsControl bound to List<FileEntry>.
//     Project placement: TreeView bound to FolderNode hierarchy (Manual mode)
//     or a hint TextBlock (Auto-by-type mode).
// ==========================================================

using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.ProjectSystem.Dialogs;

/// <summary>
/// "Add Existing Item" dialog.
/// After <see cref="Window.ShowDialog"/> returns <c>true</c>, read:
/// <list type="bullet">
///   <item><see cref="SelectedFilePaths"/> — source paths chosen by the user</item>
///   <item><see cref="CopyToProject"/> — whether files should be copied into the project</item>
///   <item><see cref="UseTypeSubfolder"/> — whether to place in a type-based physical subfolder</item>
///   <item><see cref="CreateVirtualFolder"/> — whether to auto-create a virtual folder by type</item>
///   <item><see cref="SelectedVirtualFolderId"/> — target virtual folder id, or <c>null</c> for root</item>
/// </list>
/// </summary>
public partial class AddExistingItemDialog : WpfHexEditor.Editor.Core.Views.ThemedDialog
{
    // ── Inner models ────────────────────────────────────────────────────────

    /// <summary>One row in the multi-file list.</summary>
    private sealed record FileEntry(
        string FullPath,
        string FileName,
        string TypeLabel,
        ProjectItemType Type,
        string IconGlyph);

    /// <summary>Node for the virtual folder TreeView.</summary>
    private sealed class FolderNode
    {
        public string? Id          { get; init; }
        public string  DisplayName { get; init; } = "";
        public ObservableCollection<FolderNode> Children { get; } = [];
    }

    // ── State ────────────────────────────────────────────────────────────────
    private readonly IProject        _project;
    private          string[]        _filePaths  = [];
    private readonly List<FileEntry> _fileEntries = [];

    // ── Output properties ────────────────────────────────────────────────────
    public IReadOnlyList<string> SelectedFilePaths       { get; private set; } = [];
    public bool                  CopyToProject           { get; private set; } = true;
    public bool                  UseTypeSubfolder        { get; private set; } = true;
    public bool                  CreateVirtualFolder     { get; private set; } = false;
    public string?               SelectedVirtualFolderId { get; private set; }

    // ── Type subfolder name map ──────────────────────────────────────────────
    public static string TypeSubfolderName(ProjectItemType type) => type switch
    {
        ProjectItemType.Binary           => "Binaries",
        ProjectItemType.Tbl              => "Tables",
        ProjectItemType.Patch            => "Patches",
        ProjectItemType.FormatDefinition => "FormatDefs",
        ProjectItemType.Json             => "JSON",
        ProjectItemType.Text             => "Text",
        ProjectItemType.Script           => "Scripts",
        ProjectItemType.Image            => "Images",
        ProjectItemType.Tile             => "Tiles",
        ProjectItemType.Audio            => "Audio",
        _                                => "Other",
    };

    // ── Constructor ──────────────────────────────────────────────────────────
    public AddExistingItemDialog(IProject project)
    {
        InitializeComponent();

        _project = project;

        PopulateTypeCombo();
        PopulateVirtualFolderTree();
        UpdatePlacementModeVisuals();

        Refresh();
    }

    // ── Initialisation helpers ───────────────────────────────────────────────

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

    private void PopulateVirtualFolderTree()
    {
        FolderTree.Items.Clear();

        var root = new FolderNode { Id = null, DisplayName = "(project root)" };

        foreach (var folder in _project.RootFolders)
            root.Children.Add(BuildFolderNode(folder));

        var rootItem = new TreeViewItem
        {
            Header     = BuildFolderNodeHeader("(project root)"),
            Tag        = root,
            IsSelected = true,
            IsExpanded = true,
        };

        foreach (var child in root.Children)
            rootItem.Items.Add(BuildTreeViewItem(child));

        FolderTree.Items.Add(rootItem);
    }

    private static FolderNode BuildFolderNode(IVirtualFolder folder)
    {
        var node = new FolderNode { Id = folder.Id, DisplayName = folder.Name };
        foreach (var child in folder.Children)
            node.Children.Add(BuildFolderNode(child));
        return node;
    }

    private static TreeViewItem BuildTreeViewItem(FolderNode node)
    {
        var item = new TreeViewItem
        {
            Header     = BuildFolderNodeHeader(node.DisplayName),
            Tag        = node,
            IsExpanded = true,
        };
        foreach (var child in node.Children)
            item.Items.Add(BuildTreeViewItem(child));
        return item;
    }

    private static StackPanel BuildFolderNodeHeader(string displayName)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(new TextBlock
        {
            Text       = "\uE8B7",
            FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
            FontSize   = 12,
            Margin     = new Thickness(0, 0, 5, 0),
            VerticalAlignment = VerticalAlignment.Center,
        });
        panel.Children.Add(new TextBlock
        {
            Text = displayName,
            VerticalAlignment = VerticalAlignment.Center,
        });
        return panel;
    }

    // ── Event handlers ───────────────────────────────────────────────────────

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
        if (SubfolderPanel is null) return;

        var copying = CopyToProjectRadio.IsChecked == true;
        SubfolderPanel.IsEnabled = copying;
        UpdatePlacementModeAvailability();
        Refresh();
    }

    private void OnDestModeChanged(object sender, RoutedEventArgs e)
    {
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
        SelectedFilePaths       = [.. _filePaths];
        CopyToProject           = CopyToProjectRadio.IsChecked == true;
        UseTypeSubfolder        = CopyToProjectRadio.IsChecked == true && CopyTypeSubfolderRadio.IsChecked == true;
        CreateVirtualFolder     = AutoFolderRadio.IsChecked == true;
        SelectedVirtualFolderId = GetSelectedVirtualFolderId();

        DialogResult = true;
    }

    // ── Private helpers ──────────────────────────────────────────────────────

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
        _fileEntries.Clear();

        foreach (var path in _filePaths)
        {
            var type = ProjectItemTypeHelper.FromExtension(Path.GetExtension(path));
            _fileEntries.Add(new FileEntry(
                FullPath:  path,
                FileName:  Path.GetFileName(path),
                TypeLabel: type.ToString(),
                Type:      type,
                IconGlyph: TypeGlyph(type)));
        }

        FileList.ItemsSource = null;
        FileList.ItemsSource = _fileEntries;

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

    private ProjectItemType GetSelectedType()
        => (TypeCombo.SelectedItem as ComboBoxItem)?.Tag as ProjectItemType?
           ?? ProjectItemType.Binary;

    private string? GetSelectedVirtualFolderId()
    {
        if (AutoFolderRadio.IsChecked == true) return null;

        return GetSelectedTreeItem()?.Tag is FolderNode node ? node.Id : null;
    }

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

    private void UpdatePlacementModeAvailability()
    {
        if (AutoFolderRadio is null) return;

        var copying       = CopyToProjectRadio.IsChecked == true;
        var typeSubfolder = CopyTypeSubfolderRadio.IsChecked == true;

        // Auto-by-type only makes sense when copying with type subfolder
        AutoFolderRadio.IsEnabled = copying && typeSubfolder;

        if (!AutoFolderRadio.IsEnabled && AutoFolderRadio.IsChecked == true)
            ManualFolderRadio.IsChecked = true;

        UpdatePlacementModeVisuals();
    }

    private void UpdatePlacementModeVisuals()
    {
        if (FolderTreeBorder is null || AutoPlacementHint is null) return;

        var isAuto = AutoFolderRadio?.IsChecked == true;

        FolderTreeBorder.Visibility  = isAuto ? Visibility.Collapsed : Visibility.Visible;
        AutoPlacementHint.Visibility = isAuto ? Visibility.Visible   : Visibility.Collapsed;

        if (isAuto)
            AutoPlacementHint.Text = $"Files will be placed in: {TypeSubfolderName(GetSelectedType())}";
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

        var copying       = CopyToProjectRadio.IsChecked == true;
        var typeSubfolder = copying && CopyTypeSubfolderRadio.IsChecked == true;
        var isAuto        = AutoFolderRadio.IsChecked == true;
        var projDir       = Path.GetDirectoryName(_project.ProjectFilePath) ?? "";
        var first         = _filePaths[0];
        var type          = GetSelectedType();
        var subName       = typeSubfolder ? TypeSubfolderName(type) : null;

        var physPreview = BuildPhysicalPreview(copying, subName, projDir, first);
        var virtPreview = BuildVirtualPreview(isAuto, subName, first);

        if (_filePaths.Length > 1)
        {
            var more = $"  (+{_filePaths.Length - 1} more)";
            physPreview += more;
            virtPreview += more;
        }

        PhysicalPreviewText.Text = physPreview;
        VirtualPreviewText.Text  = virtPreview;
        PreviewPanel.Visibility  = Visibility.Visible;
    }

    private static string BuildPhysicalPreview(bool copying, string? subName, string projDir, string first)
    {
        if (!copying) return first;
        return subName is not null
            ? Path.Combine(projDir, subName, Path.GetFileName(first))
            : Path.Combine(projDir, Path.GetFileName(first));
    }

    private string BuildVirtualPreview(bool isAuto, string? subName, string first)
    {
        if (isAuto && subName is not null)
            return $"{subName} › {Path.GetFileName(first)}";

        var selectedNode = GetSelectedTreeItem()?.Tag is FolderNode n ? n : null;
        return selectedNode?.Id is not null
            ? $"{selectedNode.DisplayName} › {Path.GetFileName(first)}"
            : Path.GetFileName(first);
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
