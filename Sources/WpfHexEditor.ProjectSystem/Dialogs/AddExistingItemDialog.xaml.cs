//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.ProjectSystem.Dialogs;

/// <summary>
/// Custom "Add Existing Item" dialog that lets the user:
/// <list type="bullet">
///   <item>Pick one or more files via an OS file picker</item>
///   <item>Choose between referencing in-place or copying into the project</item>
///   <item>Optionally classify into a type-based physical subfolder (e.g. <c>ROMs/</c>)</item>
///   <item>Optionally place into an existing or auto-created virtual folder</item>
/// </list>
/// After <see cref="Window.ShowDialog"/> returns <c>true</c>, read:
/// <list type="bullet">
///   <item><see cref="SelectedFilePaths"/> — source paths chosen by the user</item>
///   <item><see cref="CopyToProject"/> — whether files should be copied</item>
///   <item><see cref="UseTypeSubfolder"/> — whether to place in a type-based subfolder</item>
///   <item><see cref="CreateVirtualFolder"/> — whether to auto-create a virtual folder by type</item>
///   <item><see cref="SelectedVirtualFolderId"/> — target virtual folder id, or <c>null</c> for root</item>
/// </list>
/// </summary>
public partial class AddExistingItemDialog : Window
{
    // ── State ───────────────────────────────────────────────────────────────
    private readonly IProject _project;
    private string[] _filePaths = [];

    // ── Output properties ───────────────────────────────────────────────────
    public IReadOnlyList<string> SelectedFilePaths    { get; private set; } = [];
    public bool                  CopyToProject        { get; private set; } = true;
    public bool                  UseTypeSubfolder     { get; private set; } = true;
    public bool                  CreateVirtualFolder  { get; private set; } = true;
    public string?               SelectedVirtualFolderId { get; private set; }

    // ── Type subfolder name map ─────────────────────────────────────────────
    public static string TypeSubfolderName(ProjectItemType type) => type switch
    {
        ProjectItemType.Binary          => "Binaries",
        ProjectItemType.Tbl             => "Tables",
        ProjectItemType.Patch           => "Patches",
        ProjectItemType.FormatDefinition => "FormatDefs",
        ProjectItemType.Json            => "JSON",
        ProjectItemType.Text            => "Text",
        ProjectItemType.Script          => "Scripts",
        ProjectItemType.Image           => "Images",
        ProjectItemType.Tile            => "Tiles",
        ProjectItemType.Audio           => "Audio",
        _                               => "Other",
    };

    // ── Constructor ─────────────────────────────────────────────────────────
    public AddExistingItemDialog(IProject project)
    {
        InitializeComponent();

        _project = project;

        PopulateTypeCombo();
        PopulateVirtualFolderCombo();

        Refresh();
    }

    // ── Initialisation helpers ──────────────────────────────────────────────

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

    private void PopulateVirtualFolderCombo()
    {
        VirtualFolderCombo.Items.Clear();
        VirtualFolderCombo.Items.Add(new ComboBoxItem { Content = "(project root)", Tag = (string?)null });

        foreach (var folder in _project.RootFolders)
            AddVirtualFolderItem(folder, indent: 0);

        VirtualFolderCombo.SelectedIndex = 0;
    }

    private void AddVirtualFolderItem(IVirtualFolder folder, int indent)
    {
        VirtualFolderCombo.Items.Add(new ComboBoxItem
        {
            Content = new string(' ', indent * 2) + folder.Name,
            Tag     = folder.Id,
        });

        foreach (var child in folder.Children)
            AddVirtualFolderItem(child, indent + 1);
    }

    // ── Event handlers ──────────────────────────────────────────────────────

    private void OnFileDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        var dropped = (string[])e.Data.GetData(DataFormats.FileDrop);
        // Keep only files (discard dropped directories)
        _filePaths = dropped.Where(File.Exists).ToArray();

        if (_filePaths.Length == 0) return;

        FilePathBox.Text = _filePaths.Length == 1
            ? _filePaths[0]
            : $"{_filePaths.Length} files selected";

        UpdateDetectedType();
        Refresh();
    }

    private void OnBrowse(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title      = "Select File(s) to Add",
            Multiselect = true,
            Filter     = "All Files (*.*)|*.*"
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

        // Update path display
        FilePathBox.Text = _filePaths.Length == 1
            ? _filePaths[0]
            : $"{_filePaths.Length} files selected";

        // Auto-detect type from first file
        UpdateDetectedType();

        Refresh();
    }

    private void OnTypeChanged(object sender, SelectionChangedEventArgs e) => Refresh();

    private void OnCopyModeChanged(object sender, RoutedEventArgs e)
    {
        if (SubfolderPanel is null) return;
        var copying = CopyToProjectRadio.IsChecked == true;
        SubfolderPanel.IsEnabled = copying;
        UpdateAutoVirtualFolderAvailability();
        Refresh();
    }

    private void OnDestModeChanged(object sender, RoutedEventArgs e)
    {
        UpdateAutoVirtualFolderAvailability();
        Refresh();
    }

    private void OnVirtualFolderChanged(object sender, SelectionChangedEventArgs e)
    {
        // If user manually picks a folder, disable auto-create (they made an explicit choice)
        var manualFolder = (VirtualFolderCombo.SelectedItem as ComboBoxItem)?.Tag as string;
        if (manualFolder is not null)
            AutoVirtualFolderCheck.IsChecked = false;

        Refresh();
    }

    private void OnAutoVirtualFolderChanged(object sender, RoutedEventArgs e)
    {
        if (AutoVirtualFolderCheck.IsChecked == true)
            VirtualFolderCombo.SelectedIndex = 0;  // reset to (project root) when auto is on

        Refresh();
    }

    private void OnAdd(object sender, RoutedEventArgs e)
    {
        SelectedFilePaths       = [.. _filePaths];
        CopyToProject           = CopyToProjectRadio.IsChecked == true;
        UseTypeSubfolder        = CopyToProjectRadio.IsChecked == true && CopyTypeSubfolderRadio.IsChecked == true;
        CreateVirtualFolder     = AutoVirtualFolderCheck.IsChecked == true;
        SelectedVirtualFolderId = (VirtualFolderCombo.SelectedItem as ComboBoxItem)?.Tag as string;

        DialogResult = true;
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    private void UpdateDetectedType()
    {
        if (_filePaths.Length == 0) return;

        var firstType = ProjectItemTypeHelper.FromExtension(Path.GetExtension(_filePaths[0]));

        // Check if all files share the same type
        var allSame = _filePaths.All(p =>
            ProjectItemTypeHelper.FromExtension(Path.GetExtension(p)) == firstType);

        TypeCombo.IsEnabled = true;

        if (!allSame)
        {
            // Mixed types — show "Mixed" as a non-selectable hint
            TypeCombo.IsEnabled = false;
            // Select closest item (Binary) as default; preview will be per-file
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
    {
        return (TypeCombo.SelectedItem as ComboBoxItem)?.Tag as ProjectItemType?
               ?? ProjectItemType.Binary;
    }

    private void UpdateAutoVirtualFolderAvailability()
    {
        if (AutoVirtualFolderCheck is null) return;   // called during InitializeComponent

        var copying       = CopyToProjectRadio.IsChecked == true;
        var typeSubfolder = CopyTypeSubfolderRadio.IsChecked == true;

        // Auto-create virtual folder makes sense only when classifying by type
        AutoVirtualFolderCheck.IsEnabled = copying && typeSubfolder;

        if (!AutoVirtualFolderCheck.IsEnabled)
            AutoVirtualFolderCheck.IsChecked = false;
    }

    private void Refresh()
    {
        if (AddButton is null) return;   // called during InitializeComponent

        var hasFiles = _filePaths.Length > 0;
        AddButton.IsEnabled = hasFiles;

        if (!hasFiles)
        {
            PreviewPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var copying        = CopyToProjectRadio.IsChecked == true;
        var typeSubfolder  = copying && CopyTypeSubfolderRadio.IsChecked == true;
        var autoVirtual    = AutoVirtualFolderCheck.IsChecked == true;
        var projDir        = Path.GetDirectoryName(_project.ProjectFilePath) ?? "";

        // Compute preview for the first file (representative)
        var first        = _filePaths[0];
        var type         = GetSelectedType();
        var subName      = typeSubfolder ? TypeSubfolderName(type) : null;

        // Update auto-create checkbox label to show the target folder name
        AutoVirtualFolderCheck.Content = subName is not null
            ? $"Auto-create / use folder: {subName}"
            : "Auto-create / use folder for type";

        string physPreview;
        if (!copying)
            physPreview = first;
        else if (subName is not null)
            physPreview = Path.Combine(projDir, subName, Path.GetFileName(first));
        else
            physPreview = Path.Combine(projDir, Path.GetFileName(first));

        string virtPreview;
        if (autoVirtual && subName is not null)
            virtPreview = $"{subName} › {Path.GetFileName(first)}";
        else
        {
            var manualFolder = (VirtualFolderCombo.SelectedItem as ComboBoxItem)?.Content as string;
            virtPreview = string.IsNullOrEmpty(manualFolder) || manualFolder == "(project root)"
                ? Path.GetFileName(first)
                : $"{manualFolder.Trim()} › {Path.GetFileName(first)}";
        }

        if (_filePaths.Length > 1)
        {
            physPreview += $"  (+{_filePaths.Length - 1} more)";
            virtPreview += $"  (+{_filePaths.Length - 1} more)";
        }

        PhysicalPreviewText.Text = physPreview;
        VirtualPreviewText.Text  = virtPreview;
        PreviewPanel.Visibility  = Visibility.Visible;
    }
}
