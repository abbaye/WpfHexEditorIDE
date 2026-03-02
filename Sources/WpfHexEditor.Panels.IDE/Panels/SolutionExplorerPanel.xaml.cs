//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Panels.IDE.ViewModels;

namespace WpfHexEditor.Panels.IDE;

/// <summary>
/// VS2026-style Solution Explorer panel.
/// Implements <see cref="ISolutionExplorerPanel"/>.
/// </summary>
public partial class SolutionExplorerPanel : UserControl, ISolutionExplorerPanel
{
    private readonly SolutionExplorerViewModel _vm = new();
    private SolutionExplorerNodeVm? _contextMenuTarget;
    private IReadOnlyList<IEditorFactory> _editorFactories = [];

    public SolutionExplorerPanel()
    {
        InitializeComponent();
        DataContext = _vm;
        // Use AddHandler with handledEventsToo=true so we receive MouseLeftButtonUp
        // even when the TreeViewItem has already marked the event as handled.
        // This is required for the slow-click rename to work reliably.
        SolutionTree.AddHandler(
            UIElement.MouseLeftButtonUpEvent,
            new MouseButtonEventHandler(OnTreeMouseLeftButtonUp),
            handledEventsToo: true);
    }

    // ── ISolutionExplorerPanel ────────────────────────────────────────────────

    public void SetSolution(ISolution? solution)
        => _vm.SetSolution(solution);

    public void SyncWithFile(string absolutePath)
    {
        // Walk tree and select the FileNodeVm matching the path
        SelectNodeByPath(absolutePath, _vm.Roots);
    }

    /// <inheritdoc/>
    public void SetEditorRegistry(IReadOnlyList<IEditorFactory> factories)
        => _editorFactories = factories;

    public event EventHandler<ProjectItemActivatedEventArgs>?      ItemActivated;
    public event EventHandler<ProjectItemEventArgs>?               ItemSelected;
    public event EventHandler<ProjectItemEventArgs>?               ItemRenameRequested;
    public event EventHandler<ProjectItemEventArgs>?               ItemDeleteRequested;
    public event EventHandler<ItemMoveRequestedEventArgs>?         ItemMoveRequested;
    public event EventHandler<OpenWithSpecificEditorEventArgs>?    OpenWithSpecificRequested;

    // ── Tree events ───────────────────────────────────────────────────────────

    private void OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is FileNodeVm fn && fn.Project is not null)
            ItemSelected?.Invoke(this, new ProjectItemEventArgs { Item = fn.Source, Project = fn.Project });
    }

    private void OnItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Cancel any pending slow-click rename — double-click means "activate"
        _slowClickTimer?.Stop();
        _slowClickTimer     = null;
        _slowClickCandidate = null;

        if (SolutionTree.SelectedItem is FileNodeVm fn && fn.Project is not null)
            ItemActivated?.Invoke(this, new ProjectItemActivatedEventArgs { Item = fn.Source, Project = fn.Project });
    }

    // ── Toolbar ───────────────────────────────────────────────────────────────

    private void OnCollapseAll(object sender, RoutedEventArgs e)
    {
        foreach (var root in _vm.Roots)
            CollapseAll(root);
    }

    private void OnSyncWithActiveDocument(object sender, RoutedEventArgs e)
    {
        // Host should call SyncWithFile(); toolbar button is a convenience trigger
    }

    private void OnRefresh(object sender, RoutedEventArgs e)
        => _vm.Rebuild();

    // ── Search box ────────────────────────────────────────────────────────────

    private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        => SearchPlaceholder.Visibility = Visibility.Collapsed;

    private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        => SearchPlaceholder.Visibility =
            string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;

    // ── Context menu ──────────────────────────────────────────────────────────

    /// <summary>
    /// Adapts all context-menu item visibility to <paramref name="node"/>.
    /// Returns <see langword="true"/> if at least one item is visible (the menu should open).
    /// </summary>
    private bool UpdateContextMenu(SolutionExplorerNodeVm? node)
    {
        _contextMenuTarget = node;

        var  file         = node as FileNodeVm;
        var  physFile     = node as PhysicalFileNodeVm;
        bool isSolution   = node is SolutionNodeVm;
        bool isProject    = node is ProjectNodeVm;
        bool isFolder     = node is FolderNodeVm;
        bool isFile       = file is not null;
        bool isPhysFolder = node is PhysicalFolderNodeVm;
        bool isPhysFile   = physFile is not null;
        bool isPhysIn     = isPhysFile &&  physFile!.IsInProject;
        bool isPhysNotIn  = isPhysFile && !physFile!.IsInProject;
        bool isChangeset  = node is ChangesetNodeVm;

        bool isTbl = file?.Source.ItemType == ProjectItemType.Tbl;
        bool isDefault = file?.IsDefaultTbl == true;
        // "Convert to TBLX" only for plain .tbl; .tblx is already the advanced format
        bool isThingyTbl = isTbl && string.Equals(
            Path.GetExtension(file?.Source.Name ?? string.Empty), ".tbl", StringComparison.OrdinalIgnoreCase);

        bool isExternal   = file?.IsExternal == true;
        bool canOpen      = isFile || isPhysIn;
        bool canAdd       = isProject || isFolder;
        bool hasExplorer  = isSolution || isProject || isFolder || isFile || isPhysFolder || isPhysFile;
        bool hasCopyPath  = isProject || isFolder || isFile || isPhysFolder || isPhysFile;
        bool hasAfterNav  = isSolution || isProject || isFolder || isFile || isPhysIn;
        bool hasProp      = isFile || isProject;

        // Open / Open With (file and physical-file-in-project)
        OpenMenuItem    .Visibility = canOpen ? Visibility.Visible : Visibility.Collapsed;
        OpenWithMenuItem.Visibility = canOpen ? Visibility.Visible : Visibility.Collapsed;
        OpenSeparator   .Visibility = canOpen ? Visibility.Visible : Visibility.Collapsed;

        // Dynamically rebuild the "Open With ›" submenu based on the current file extension
        if (canOpen)
            RebuildOpenWithSubmenu(file?.Source.AbsolutePath ?? (physFile?.IsInProject == true ? physFile.LinkedItem?.AbsolutePath : null));

        // Add / folder operations (project & folder only)
        AddNewItemMenuItem       .Visibility = canAdd    ? Visibility.Visible : Visibility.Collapsed;
        AddExistingItemMenuItem  .Visibility = canAdd    ? Visibility.Visible : Visibility.Collapsed;
        ImportFormatMenuItem     .Visibility = isProject ? Visibility.Visible : Visibility.Collapsed;
        NewFolderMenuItem        .Visibility = canAdd    ? Visibility.Visible : Visibility.Collapsed;
        NewPhysicalFolderMenuItem.Visibility = canAdd    ? Visibility.Visible : Visibility.Collapsed;
        AddFolderFromDiskMenuItem.Visibility = canAdd    ? Visibility.Visible : Visibility.Collapsed;
        AddSeparator             .Visibility = canAdd    ? Visibility.Visible : Visibility.Collapsed;

        // Include in Project (Show All Files — physical file not yet tracked)
        IncludeInProjectMenuItem.Visibility = isPhysNotIn ? Visibility.Visible : Visibility.Collapsed;
        IncludeSeparator        .Visibility = isPhysNotIn ? Visibility.Visible : Visibility.Collapsed;

        // TBL — Set and Clear are mutually exclusive; Convert only for plain .tbl (not .tblx)
        SetDefaultTblMenuItem  .Visibility = (isTbl && !isDefault) ? Visibility.Visible : Visibility.Collapsed;
        ClearDefaultTblMenuItem.Visibility = (isTbl &&  isDefault) ? Visibility.Visible : Visibility.Collapsed;
        ConvertToTblxMenuItem  .Visibility = isThingyTbl           ? Visibility.Visible : Visibility.Collapsed;
        ApplyTblToActiveMenuItem.Visibility = isTbl                ? Visibility.Visible : Visibility.Collapsed;
        ApplyTblToAllMenuItem  .Visibility = isTbl                 ? Visibility.Visible : Visibility.Collapsed;
        TblSeparator           .Visibility = isTbl                 ? Visibility.Visible : Visibility.Collapsed;

        // Navigation: Open in Explorer (all) / Copy Path (all except solution)
        OpenInExplorerMenuItem.Visibility = hasExplorer               ? Visibility.Visible : Visibility.Collapsed;
        CopyPathMenuItem      .Visibility = hasCopyPath               ? Visibility.Visible : Visibility.Collapsed;
        NavigationSeparator   .Visibility = hasExplorer && hasAfterNav ? Visibility.Visible : Visibility.Collapsed;

        // Rename / Remove / Exclude
        RenameMenuItem            .Visibility = (isSolution || isFile || isFolder || isProject) ? Visibility.Visible : Visibility.Collapsed;
        RemoveMenuItem            .Visibility = (isFile || isFolder)                             ? Visibility.Visible : Visibility.Collapsed;
        ExcludeFromProjectMenuItem.Visibility = isPhysIn                                         ? Visibility.Visible : Visibility.Collapsed;

        // Import external file (file node whose path is outside the project directory)
        ImportExternalSeparator    .Visibility = isExternal ? Visibility.Visible : Visibility.Collapsed;
        ImportExternalFileMenuItem .Visibility = isExternal ? Visibility.Visible : Visibility.Collapsed;

        // Changeset (.whchg) actions
        ChangesetSeparator           .Visibility = isChangeset ? Visibility.Visible : Visibility.Collapsed;
        WriteChangesetToDiskMenuItem .Visibility = isChangeset ? Visibility.Visible : Visibility.Collapsed;
        DiscardChangesetMenuItem     .Visibility = isChangeset ? Visibility.Visible : Visibility.Collapsed;

        // Properties
        PropertiesSeparator.Visibility = hasProp ? Visibility.Visible : Visibility.Collapsed;
        PropertiesMenuItem .Visibility = hasProp ? Visibility.Visible : Visibility.Collapsed;

        // Solution bottom: Save All + Close Solution
        SolutionBottomSeparator.Visibility = isSolution ? Visibility.Visible : Visibility.Collapsed;
        SaveAllMenuItem        .Visibility = isSolution ? Visibility.Visible : Visibility.Collapsed;
        CloseSolutionSeparator .Visibility = isSolution ? Visibility.Visible : Visibility.Collapsed;
        CloseSolutionMenuItem  .Visibility = isSolution ? Visibility.Visible : Visibility.Collapsed;

        return canOpen || canAdd || isPhysNotIn || isTbl || hasExplorer
            || isSolution || isProject || isFolder || isFile || isPhysIn || isChangeset || isExternal;
    }

    private void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        // Determine the node under the cursor via hit-testing
        var pt   = Mouse.GetPosition(SolutionTree);
        var hit  = SolutionTree.InputHitTest(pt) as DependencyObject;
        var tvi  = FindAncestor<TreeViewItem>(hit);
        var node = tvi?.DataContext as SolutionExplorerNodeVm;

        // VS-style: right-click selects the node
        if (node is not null && !node.IsSelected)
            node.IsSelected = true;

        // Cancel the popup if no item would be visible (empty area, solution node)
        if (!UpdateContextMenu(node))
            e.Handled = true;
    }

    private void OnAddNewItem(object sender, RoutedEventArgs e)
    {
        var (project, folderId) = GetContextProjectAndFolder();
        if (project is null) return;
        AddNewItemRequested?.Invoke(this, new AddItemRequestedEventArgs
        {
            Project        = project,
            TargetFolderId = folderId,
        });
    }

    private void OnAddExistingItem(object sender, RoutedEventArgs e)
    {
        var (project, folderId) = GetContextProjectAndFolder();
        if (project is null) return;
        AddExistingItemRequested?.Invoke(this, new AddItemRequestedEventArgs
        {
            Project        = project,
            TargetFolderId = folderId,
        });
    }

    private void OnNewFolder(object sender, RoutedEventArgs e)
    {
        var (project, folderId) = GetContextProjectAndFolder();
        if (project is null) return;
        FolderCreateRequested?.Invoke(this, new FolderCreateRequestedEventArgs
        {
            Project        = project,
            ParentFolderId = folderId,
            CreatePhysical = true,
        });
    }

    private void OnNewPhysicalFolder(object sender, RoutedEventArgs e)
    {
        var (project, folderId) = GetContextProjectAndFolder();
        if (project is null) return;
        FolderCreateRequested?.Invoke(this, new FolderCreateRequestedEventArgs
        {
            Project        = project,
            ParentFolderId = folderId,
            CreatePhysical = false,
        });
    }

    private void OnAddFolderFromDisk(object sender, RoutedEventArgs e)
    {
        var (project, folderId) = GetContextProjectAndFolder();
        if (project is null) return;
        FolderFromDiskRequested?.Invoke(this, new FolderFromDiskRequestedEventArgs
        {
            Project        = project,
            ParentFolderId = folderId,
        });
    }

    private void OnShowAllFiles(object sender, RoutedEventArgs e)
        => _vm.ShowAllFiles = ShowAllFilesButton.IsChecked == true;

    private void OnOpen(object sender, RoutedEventArgs e)
    {
        switch (_contextMenuTarget)
        {
            case FileNodeVm fn when fn.Project is not null:
                ItemActivated?.Invoke(this, new ProjectItemActivatedEventArgs { Item = fn.Source, Project = fn.Project });
                break;
            case PhysicalFileNodeVm pf when pf.IsInProject && pf.LinkedItem is not null && pf.Project is not null:
                ItemActivated?.Invoke(this, new ProjectItemActivatedEventArgs { Item = pf.LinkedItem, Project = pf.Project });
                break;
        }
    }

    /// <summary>
    /// Rebuilds the "Open With ›" submenu items for <paramref name="filePath"/>.
    /// The "Hex Editor" item is always present; additional items are added for each
    /// registered factory whose <see cref="IEditorFactory.CanOpen"/> returns true.
    /// </summary>
    private void RebuildOpenWithSubmenu(string? filePath)
    {
        // Remove all items except the permanent "Hex Editor" one (index 0)
        while (OpenWithMenuItem.Items.Count > 1)
            OpenWithMenuItem.Items.RemoveAt(OpenWithMenuItem.Items.Count - 1);

        if (filePath is null) return;

        foreach (var factory in _editorFactories)
        {
            if (!factory.CanOpen(filePath)) continue;
            OpenWithMenuItem.Items.Add(MakeOpenWithSubItem(factory.Descriptor.DisplayName, factory.Descriptor.Id));
        }
    }

    private MenuItem MakeOpenWithSubItem(string header, string factoryId)
    {
        var item = new MenuItem { Header = header, Tag = factoryId };
        item.Click += OnOpenWithSubItem;
        return item;
    }

    private void OnOpenWithSubItem(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi) return;

        // Tag == "__hex__" or null → Hex Editor fallback; otherwise it's a factory id
        var factoryId = mi.Tag is "__hex__" ? null : mi.Tag as string;

        switch (_contextMenuTarget)
        {
            case FileNodeVm fn when fn.Project is not null:
                OpenWithSpecificRequested?.Invoke(this, new OpenWithSpecificEditorEventArgs
                    { Item = fn.Source, Project = fn.Project, FactoryId = factoryId });
                break;
            case PhysicalFileNodeVm pf when pf.IsInProject && pf.LinkedItem is not null && pf.Project is not null:
                OpenWithSpecificRequested?.Invoke(this, new OpenWithSpecificEditorEventArgs
                    { Item = pf.LinkedItem, Project = pf.Project, FactoryId = factoryId });
                break;
        }
    }

    private void OnOpenInExplorer(object sender, RoutedEventArgs e)
    {
        switch (_contextMenuTarget)
        {
            case FileNodeVm fn:
                var fp = fn.Source.AbsolutePath;
                if (!string.IsNullOrEmpty(fp) && File.Exists(fp))
                    Process.Start("explorer.exe", $"/select,\"{fp}\"");
                break;
            case PhysicalFileNodeVm pf:
                if (!string.IsNullOrEmpty(pf.PhysicalPath) && File.Exists(pf.PhysicalPath))
                    Process.Start("explorer.exe", $"/select,\"{pf.PhysicalPath}\"");
                break;
            default:
                var dir = GetExplorerPath(_contextMenuTarget);
                if (dir is not null && Directory.Exists(dir))
                    Process.Start("explorer.exe", dir);
                break;
        }
    }

    private void OnCopyPath(object sender, RoutedEventArgs e)
    {
        var path = GetCopyPath(_contextMenuTarget);
        if (path is not null)
            Clipboard.SetText(path);
    }

    private void OnIncludeInProject(object sender, RoutedEventArgs e)
    {
        if (_contextMenuTarget is not PhysicalFileNodeVm pf || pf.IsInProject || pf.Project is null) return;
        PhysicalFileIncludeRequested?.Invoke(this, new PhysicalFileIncludeRequestedEventArgs
        {
            PhysicalPath = pf.PhysicalPath,
            Project      = pf.Project,
        });
    }

    private void OnExcludeFromProject(object sender, RoutedEventArgs e)
    {
        if (_contextMenuTarget is not PhysicalFileNodeVm pf || !pf.IsInProject
            || pf.LinkedItem is null || pf.Project is null) return;
        ItemDeleteRequested?.Invoke(this, new ProjectItemEventArgs { Item = pf.LinkedItem, Project = pf.Project });
    }

    private void OnImportExternalFile(object sender, RoutedEventArgs e)
    {
        if (_contextMenuTarget is not FileNodeVm fn || !fn.IsExternal || fn.Project is null) return;
        ImportExternalFileRequested?.Invoke(this, new ImportExternalFileRequestedEventArgs
        {
            Item    = fn.Source,
            Project = fn.Project,
        });
    }

    private void OnSaveAll(object sender, RoutedEventArgs e)
        => SaveAllRequested?.Invoke(this, EventArgs.Empty);

    private void OnImportFormatDefinition(object sender, RoutedEventArgs e)
    {
        if (_contextMenuTarget is not ProjectNodeVm pv) return;
        ImportFormatDefinitionRequested?.Invoke(this, new AddItemRequestedEventArgs
        {
            Project        = pv.Source,
            TargetFolderId = null,
        });
    }

    private void OnConvertToTblx(object sender, RoutedEventArgs e)
    {
        if (_contextMenuTarget is not FileNodeVm fn || fn.Project is null) return;
        ConvertTblRequested?.Invoke(this, new ProjectItemEventArgs { Item = fn.Source, Project = fn.Project });
    }

    /// <summary>
    /// Returns the target project and optional folder id inferred from the current context menu node.
    /// </summary>
    private (IProject? project, string? folderId) GetContextProjectAndFolder()
        => _contextMenuTarget switch
        {
            ProjectNodeVm pv => (pv.Source, null),
            FolderNodeVm  fv => (fv.Project, fv.Folder.Id),
            FileNodeVm    fn => (fn.Project, null),
            _                => (null, null),
        };

    private void OnSetDefaultTbl(object sender, RoutedEventArgs e)
    {
        if (_contextMenuTarget is not FileNodeVm fn || fn.Project is null) return;
        DefaultTblChangeRequested?.Invoke(this, new DefaultTblChangeEventArgs
        {
            Project = fn.Project,
            TblItem = fn.Source,
        });
        _vm.RefreshDefaultTbl(fn.Project);
    }

    private void OnClearDefaultTbl(object sender, RoutedEventArgs e)
    {
        if (_contextMenuTarget is not FileNodeVm fn || fn.Project is null) return;
        DefaultTblChangeRequested?.Invoke(this, new DefaultTblChangeEventArgs
        {
            Project = fn.Project,
            TblItem = null,
        });
        _vm.RefreshDefaultTbl(fn.Project);
    }

    private void OnApplyTblToActive(object sender, RoutedEventArgs e)
    {
        if (_contextMenuTarget is not FileNodeVm fn || fn.Project is null) return;
        ApplyTblRequested?.Invoke(this, new ApplyTblRequestedEventArgs
        {
            Project    = fn.Project,
            TblItem    = fn.Source,
            ApplyToAll = false,
        });
    }

    private void OnApplyTblToAll(object sender, RoutedEventArgs e)
    {
        if (_contextMenuTarget is not FileNodeVm fn || fn.Project is null) return;
        ApplyTblRequested?.Invoke(this, new ApplyTblRequestedEventArgs
        {
            Project    = fn.Project,
            TblItem    = fn.Source,
            ApplyToAll = true,
        });
    }

    private void OnRename(object sender, RoutedEventArgs e)
    {
        if      (_contextMenuTarget is SolutionNodeVm sv) StartInlineSolutionEdit(sv);
        else if (_contextMenuTarget is FileNodeVm     fn) StartInlineEdit(fn);
        else if (_contextMenuTarget is FolderNodeVm   fv) StartInlineFolderEdit(fv);
        else if (_contextMenuTarget is ProjectNodeVm  pv) StartInlineProjectEdit(pv);
    }

    private void OnRemove(object sender, RoutedEventArgs e)
    {
        if (_contextMenuTarget is FileNodeVm fn && fn.Project is not null)
            ItemDeleteRequested?.Invoke(this, new ProjectItemEventArgs { Item = fn.Source, Project = fn.Project });
        else if (_contextMenuTarget is FolderNodeVm fv && fv.Project is not null)
            FolderDeleteRequested?.Invoke(this, new FolderDeleteEventArgs { Folder = fv.Folder, Project = fv.Project });
    }

    /// <inheritdoc/>
    public event EventHandler<NodePropertiesEventArgs>? PropertiesRequested;

    private void OnProperties(object sender, RoutedEventArgs e)
    {
        switch (_contextMenuTarget)
        {
            case ProjectNodeVm pv:
                PropertiesRequested?.Invoke(this, new NodePropertiesEventArgs { Project = pv.Source });
                break;
            case FileNodeVm fn when fn.Project is not null:
                PropertiesRequested?.Invoke(this, new NodePropertiesEventArgs { Project = fn.Project, Item = fn.Source });
                break;
        }
    }

    // ── Path helpers ──────────────────────────────────────────────────────────

    /// <summary>Returns the directory to reveal when the user chooses "Open in File Explorer".</summary>
    private static string? GetExplorerPath(SolutionExplorerNodeVm? node) => node switch
    {
        SolutionNodeVm       sv  => Path.GetDirectoryName(sv.Source.FilePath),
        ProjectNodeVm        pv  => Path.GetDirectoryName(pv.Source.ProjectFilePath),
        FolderNodeVm         fv  => GetFolderPhysicalPath(fv),
        FileNodeVm           fn  => Path.GetDirectoryName(fn.Source.AbsolutePath),
        PhysicalFolderNodeVm pfv => pfv.PhysicalPath,
        PhysicalFileNodeVm   pf  => Path.GetDirectoryName(pf.PhysicalPath),
        _                        => null,
    };

    /// <summary>Returns the path to copy to the clipboard via "Copy Path".</summary>
    private static string? GetCopyPath(SolutionExplorerNodeVm? node) => node switch
    {
        ProjectNodeVm        pv  => pv.Source.ProjectFilePath,
        FolderNodeVm         fv  => GetFolderPhysicalPath(fv),
        FileNodeVm           fn  => fn.Source.AbsolutePath,
        PhysicalFolderNodeVm pfv => pfv.PhysicalPath,
        PhysicalFileNodeVm   pf  => pf.PhysicalPath,
        _                        => null,
    };

    private static string? GetFolderPhysicalPath(FolderNodeVm fv)
    {
        if (fv.Project is null) return null;
        var projDir = Path.GetDirectoryName(fv.Project.ProjectFilePath);
        if (fv.Folder.PhysicalRelativePath is { } rel)
            return Path.Combine(projDir ?? string.Empty, rel);
        return projDir;
    }

    // ── Additional public events ───────────────────────────────────────────────

    /// <inheritdoc/>
    public event EventHandler<SolutionRenameRequestedEventArgs>? SolutionRenameRequested;

    /// <inheritdoc/>
    public event EventHandler<ProjectRenameRequestedEventArgs>? ProjectRenameRequested;

    /// <inheritdoc/>
    public event EventHandler? CloseSolutionRequested;

    private void OnCloseSolution(object sender, RoutedEventArgs e)
        => CloseSolutionRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Raised when the user requests a change to the project default TBL.
    /// </summary>
    public event EventHandler<DefaultTblChangeEventArgs>? DefaultTblChangeRequested;

    /// <summary>
    /// Raised when the user chooses "Apply to Active Document" or "Apply to All Documents"
    /// on a TBL file node. The host loads the TBL into the appropriate HexEditor(s).
    /// </summary>
    public event EventHandler<ApplyTblRequestedEventArgs>? ApplyTblRequested;

    /// <inheritdoc/>
    public event EventHandler<AddItemRequestedEventArgs>? AddNewItemRequested;

    /// <inheritdoc/>
    public event EventHandler<AddItemRequestedEventArgs>? AddExistingItemRequested;

    /// <inheritdoc/>
    public event EventHandler<AddItemRequestedEventArgs>? ImportFormatDefinitionRequested;

    /// <inheritdoc/>
    public event EventHandler<ProjectItemEventArgs>? ConvertTblRequested;

    /// <inheritdoc/>
    public event EventHandler<FolderRenameEventArgs>? FolderRenameRequested;

    /// <inheritdoc/>
    public event EventHandler<FolderDeleteEventArgs>? FolderDeleteRequested;

    /// <inheritdoc/>
    public event EventHandler<FolderCreateRequestedEventArgs>? FolderCreateRequested;

    /// <inheritdoc/>
    public event EventHandler<FolderFromDiskRequestedEventArgs>? FolderFromDiskRequested;

    /// <inheritdoc/>
    public event EventHandler<OpenWithRequestedEventArgs>? OpenWithRequested;

    /// <inheritdoc/>
    public event EventHandler<PhysicalFileIncludeRequestedEventArgs>? PhysicalFileIncludeRequested;

    /// <inheritdoc/>
    public event EventHandler<ImportExternalFileRequestedEventArgs>? ImportExternalFileRequested;

    /// <inheritdoc/>
    public event EventHandler? SaveAllRequested;

    /// <inheritdoc/>
    public void BeginFolderRename(IVirtualFolder folder)
    {
        if (FindFolderNodeVm(_vm.Roots, folder) is FolderNodeVm fv)
            StartInlineFolderEdit(fv);
    }

    /// <inheritdoc/>
    public bool ShowAllFiles
    {
        get => _vm.ShowAllFiles;
        set
        {
            _vm.ShowAllFiles              = value;
            ShowAllFilesButton.IsChecked  = value;
        }
    }

    private static FolderNodeVm? FindFolderNodeVm(IEnumerable<SolutionExplorerNodeVm> nodes, IVirtualFolder folder)
    {
        foreach (var node in nodes)
        {
            if (node is FolderNodeVm fv && fv.Folder.Id == folder.Id) return fv;
            var found = FindFolderNodeVm(node.Children, folder);
            if (found is not null) return found;
        }
        return null;
    }

    // ── F2 Inline rename ──────────────────────────────────────────────────────

    private void OnTreePreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F2)
        {
            if      (SolutionTree.SelectedItem is SolutionNodeVm sv) { StartInlineSolutionEdit(sv); e.Handled = true; }
            else if (SolutionTree.SelectedItem is FileNodeVm     fn) { StartInlineEdit(fn);          e.Handled = true; }
            else if (SolutionTree.SelectedItem is FolderNodeVm   fv) { StartInlineFolderEdit(fv);    e.Handled = true; }
            else if (SolutionTree.SelectedItem is ProjectNodeVm  pv) { StartInlineProjectEdit(pv);   e.Handled = true; }
        }
    }

    private void StartInlineEdit(FileNodeVm fn)
    {
        fn.BeginEdit();
        // Wait for the DataTemplate to swap to the TextBox before trying to focus it
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, () =>
        {
            if (FindTreeViewItem(SolutionTree, fn) is TreeViewItem tvi)
            {
                if (FindChild<TextBox>(tvi, "InlineEditBox") is TextBox tb)
                {
                    tb.Focus();
                    tb.SelectAll();
                }
            }
        });
    }

    private void OnInlineEditKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;
        var fn = tb.DataContext as FileNodeVm;

        if (e.Key == Key.Return)
        {
            CommitInlineEdit(fn);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            fn?.CancelEdit();
            SolutionTree.Focus();
            e.Handled = true;
        }
    }

    private void OnInlineEditLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
            CommitInlineEdit(tb.DataContext as FileNodeVm);
    }

    private void CommitInlineEdit(FileNodeVm? fn)
    {
        if (fn is null || !fn.IsEditing) return;

        var oldName = fn.Source.Name;
        var newName = fn.CommitEdit();

        if (!string.IsNullOrWhiteSpace(newName)
            && !string.Equals(newName, oldName, StringComparison.OrdinalIgnoreCase)
            && fn.Project is not null)
        {
            ItemRenameRequested?.Invoke(this, new ProjectItemEventArgs
            {
                Item    = fn.Source,
                Project = fn.Project,
                NewName = newName,
            });
        }

        SolutionTree.Focus();
    }

    // ── F2 Inline rename — solution ───────────────────────────────────────────

    private void StartInlineSolutionEdit(SolutionNodeVm sv)
    {
        sv.BeginEdit();
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, () =>
        {
            if (FindTreeViewItem(SolutionTree, sv) is TreeViewItem tvi)
            {
                if (FindChild<TextBox>(tvi, "SolutionInlineEditBox") is TextBox tb)
                {
                    tb.Focus();
                    tb.SelectAll();
                }
            }
        });
    }

    private void OnSolutionInlineEditKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;
        var sv = tb.DataContext as SolutionNodeVm;

        if (e.Key == Key.Return)
        {
            CommitInlineSolutionEdit(sv);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            sv?.CancelEdit();
            SolutionTree.Focus();
            e.Handled = true;
        }
    }

    private void OnSolutionInlineEditLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
            CommitInlineSolutionEdit(tb.DataContext as SolutionNodeVm);
    }

    private void CommitInlineSolutionEdit(SolutionNodeVm? sv)
    {
        if (sv is null || !sv.IsEditing) return;

        var oldName = sv.Source.Name;
        var newName = sv.CommitEdit();

        if (!string.IsNullOrWhiteSpace(newName)
            && !string.Equals(newName, oldName, StringComparison.OrdinalIgnoreCase))
        {
            SolutionRenameRequested?.Invoke(this, new SolutionRenameRequestedEventArgs
            {
                Solution = sv.Source,
                NewName  = newName,
            });
            // Solution.Name is updated synchronously by RenameSolutionAsync before its first
            // await; rebuild here to reflect the new name immediately.
            _vm.Rebuild();
        }

        SolutionTree.Focus();
    }

    // ── F2 Inline rename — folder ─────────────────────────────────────────────

    private void StartInlineFolderEdit(FolderNodeVm fv)
    {
        fv.BeginEdit();
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, () =>
        {
            if (FindTreeViewItem(SolutionTree, fv) is TreeViewItem tvi)
            {
                if (FindChild<TextBox>(tvi, "FolderInlineEditBox") is TextBox tb)
                {
                    tb.Focus();
                    tb.SelectAll();
                }
            }
        });
    }

    private void OnFolderInlineEditKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;
        var fv = tb.DataContext as FolderNodeVm;

        if (e.Key == Key.Return)
        {
            CommitInlineFolderEdit(fv);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            fv?.CancelEdit();
            SolutionTree.Focus();
            e.Handled = true;
        }
    }

    private void OnFolderInlineEditLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
            CommitInlineFolderEdit(tb.DataContext as FolderNodeVm);
    }

    private void CommitInlineFolderEdit(FolderNodeVm? fv)
    {
        if (fv is null || !fv.IsEditing) return;

        var oldName = fv.Folder.Name;
        var newName = fv.CommitEdit();

        if (!string.IsNullOrWhiteSpace(newName)
            && !string.Equals(newName, oldName, StringComparison.OrdinalIgnoreCase)
            && fv.Project is not null)
        {
            FolderRenameRequested?.Invoke(this, new FolderRenameEventArgs
            {
                Folder  = fv.Folder,
                Project = fv.Project,
                NewName = newName,
            });
            // VirtualFolder has no INPC — rebuild the tree now that the host has
            // updated vf.Name (RenameFolderAsync runs synchronously before its first await).
            _vm.Rebuild();
        }

        SolutionTree.Focus();
    }

    // ── F2 Inline rename — project ────────────────────────────────────────────

    private void StartInlineProjectEdit(ProjectNodeVm pv)
    {
        pv.BeginEdit();
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, () =>
        {
            if (FindTreeViewItem(SolutionTree, pv) is TreeViewItem tvi)
            {
                if (FindChild<TextBox>(tvi, "ProjectInlineEditBox") is TextBox tb)
                {
                    tb.Focus();
                    tb.SelectAll();
                }
            }
        });
    }

    private void OnProjectInlineEditKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;
        var pv = tb.DataContext as ProjectNodeVm;

        if (e.Key == Key.Return)
        {
            CommitInlineProjectEdit(pv);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            pv?.CancelEdit();
            SolutionTree.Focus();
            e.Handled = true;
        }
    }

    private void OnProjectInlineEditLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
            CommitInlineProjectEdit(tb.DataContext as ProjectNodeVm);
    }

    private void CommitInlineProjectEdit(ProjectNodeVm? pv)
    {
        if (pv is null || !pv.IsEditing) return;

        var oldName = pv.Source.Name;
        var newName = pv.CommitEdit();

        if (!string.IsNullOrWhiteSpace(newName)
            && !string.Equals(newName, oldName, StringComparison.OrdinalIgnoreCase))
        {
            ProjectRenameRequested?.Invoke(this, new ProjectRenameRequestedEventArgs
            {
                Project = pv.Source,
                NewName = newName,
            });
            // Project.Name is updated synchronously by RenameProjectAsync before its first
            // await; rebuild here to reflect the new name immediately.
            _vm.Rebuild();
        }

        SolutionTree.Focus();
    }

    // ── DragDrop ──────────────────────────────────────────────────────────────

    private const string DragDataFormat = "SolutionExplorerFileNode";
    private Point       _dragStartPoint;
    private FileNodeVm? _draggedNode;

    // Slow-click rename (click on already-selected item → rename after delay)
    private SolutionExplorerNodeVm?                      _slowClickCandidate;
    private System.Windows.Threading.DispatcherTimer?    _slowClickTimer;

    private void OnTreeMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Cancel any pending slow-click rename (fresh click = reset)
        _slowClickTimer?.Stop();
        _slowClickTimer     = null;
        _slowClickCandidate = null;

        _dragStartPoint = e.GetPosition(null);
        _draggedNode    = null;

        if (e.OriginalSource is DependencyObject src)
        {
            var tvi = FindAncestor<TreeViewItem>(src);
            if (tvi?.DataContext is FileNodeVm fn)
                _draggedNode = fn;

            // Record slow-click candidate only when the node is already selected
            if      (tvi?.DataContext is SolutionNodeVm sv2 && sv2.IsSelected) _slowClickCandidate = sv2;
            else if (tvi?.DataContext is FileNodeVm     fn2 && fn2.IsSelected) _slowClickCandidate = fn2;
            else if (tvi?.DataContext is FolderNodeVm   fv2 && fv2.IsSelected) _slowClickCandidate = fv2;
            else if (tvi?.DataContext is ProjectNodeVm  pv2 && pv2.IsSelected) _slowClickCandidate = pv2;
        }
    }

    private void OnTreeMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var candidate = _slowClickCandidate;
        _slowClickCandidate = null;
        if (candidate is null || candidate.IsEditing) return;

        // Confirm the release is on the same node
        var tvi = FindAncestor<TreeViewItem>(e.OriginalSource as DependencyObject);
        if (tvi?.DataContext != candidate) return;

        // Start the rename timer — fires after 600 ms if no other click/drag occurs
        _slowClickTimer = new System.Windows.Threading.DispatcherTimer
            { Interval = TimeSpan.FromMilliseconds(600) };
        _slowClickTimer.Tick += (_, _) =>
        {
            _slowClickTimer?.Stop();
            _slowClickTimer = null;
            if      (candidate is SolutionNodeVm sv && sv.IsSelected && !sv.IsEditing) StartInlineSolutionEdit(sv);
            else if (candidate is FileNodeVm     fn && fn.IsSelected && !fn.IsEditing) StartInlineEdit(fn);
            else if (candidate is FolderNodeVm   fv && fv.IsSelected && !fv.IsEditing) StartInlineFolderEdit(fv);
            else if (candidate is ProjectNodeVm  pv && pv.IsSelected && !pv.IsEditing) StartInlineProjectEdit(pv);
        };
        _slowClickTimer.Start();
    }

    private void OnTreeMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _draggedNode is null) return;

        var pos  = e.GetPosition(null);
        var diff = _dragStartPoint - pos;
        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        // Drag threshold exceeded — cancel any pending slow-click rename
        _slowClickTimer?.Stop();
        _slowClickTimer     = null;
        _slowClickCandidate = null;

        // Don't start drag if we're currently editing inline
        if (_draggedNode.IsEditing) return;

        var data = new DataObject(DragDataFormat, _draggedNode);
        DragDrop.DoDragDrop(SolutionTree, data, DragDropEffects.Move);
        _draggedNode = null;
    }

    private void OnTreeDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DragDataFormat))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        var dragged = e.Data.GetData(DragDataFormat) as FileNodeVm;
        var target  = GetDropTarget(e.OriginalSource as DependencyObject);

        // Must have a valid target that belongs to the same project
        e.Effects = (target is not null && dragged?.Project is not null)
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnTreeDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DragDataFormat)) return;
        if (e.Data.GetData(DragDataFormat) is not FileNodeVm draggedFile) return;
        if (draggedFile.Project is null) return;

        var target = GetDropTarget(e.OriginalSource as DependencyObject);
        if (target is null) return;

        string? targetFolderId = target switch
        {
            FolderNodeVm fv => fv.Folder.Id,
            ProjectNodeVm   => null,    // drop on project root
            _               => null,
        };

        // Refuse dropping a file onto the same folder it is already in
        // (this is a best-effort check; SolutionManager will handle edge-cases)
        ItemMoveRequested?.Invoke(this, new ItemMoveRequestedEventArgs
        {
            Item           = draggedFile.Source,
            Project        = draggedFile.Project,
            TargetFolderId = targetFolderId,
        });

        // Rebuild the view to reflect the new tree structure
        _vm.Rebuild();
    }

    /// <summary>Returns the nearest <see cref="FolderNodeVm"/> or <see cref="ProjectNodeVm"/>
    /// that is a valid drop target, or <see langword="null"/>.</summary>
    private static SolutionExplorerNodeVm? GetDropTarget(DependencyObject? source)
    {
        if (source is null) return null;
        var tvi = FindAncestor<TreeViewItem>(source);
        return tvi?.DataContext switch
        {
            FolderNodeVm  fv => fv,
            ProjectNodeVm pv => pv,
            _                => null,
        };
    }

    // ── Visual-tree helpers ───────────────────────────────────────────────────

    private static TreeViewItem? FindTreeViewItem(ItemsControl container, object item)
    {
        if (container.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem tvi)
            return tvi;

        foreach (var child in container.Items)
        {
            if (container.ItemContainerGenerator.ContainerFromItem(child) is not TreeViewItem c) continue;
            var found = FindTreeViewItem(c, item);
            if (found is not null) return found;
        }
        return null;
    }

    private static T? FindChild<T>(DependencyObject parent, string name) where T : FrameworkElement
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T el && el.Name == name) return el;
            var found = FindChild<T>(child, name);
            if (found is not null) return found;
        }
        return null;
    }

    private static T? FindAncestor<T>(DependencyObject source) where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T t) return t;
            source = VisualTreeHelper.GetParent(source);
        }
        return null;
    }

    // ── Tree helpers ──────────────────────────────────────────────────────────

    private static void CollapseAll(SolutionExplorerNodeVm node)
    {
        node.IsExpanded = false;
        foreach (var child in node.Children)
            CollapseAll(child);
    }

    private static bool SelectNodeByPath(string path, IEnumerable<SolutionExplorerNodeVm> nodes)
    {
        foreach (var node in nodes)
        {
            if (node is FileNodeVm fn &&
                string.Equals(fn.Source.AbsolutePath, path, StringComparison.OrdinalIgnoreCase))
            {
                fn.IsSelected = true;
                return true;
            }
            if (SelectNodeByPath(path, node.Children))
            {
                node.IsExpanded = true; // ensure parent is expanded so the node is visible
                return true;
            }
        }
        return false;
    }

    // ── Changeset context menu actions ────────────────────────────────────────

    /// <inheritdoc/>
    public event EventHandler<ProjectItemEventArgs>? WriteToDiskRequested;

    /// <inheritdoc/>
    public event EventHandler<ProjectItemEventArgs>? DiscardChangesetRequested;

    private void OnWriteChangesetToDisk(object sender, RoutedEventArgs e)
    {
        if (_contextMenuTarget is ChangesetNodeVm cn)
            WriteToDiskRequested?.Invoke(this, new ProjectItemEventArgs { Item = cn.OwnerItem, Project = cn.Project });
    }

    private void OnDiscardChangeset(object sender, RoutedEventArgs e)
    {
        if (_contextMenuTarget is ChangesetNodeVm cn)
            DiscardChangesetRequested?.Invoke(this, new ProjectItemEventArgs { Item = cn.OwnerItem, Project = cn.Project });
    }

    /// <inheritdoc/>
    public void RefreshChangesetNode(IProjectItem item)
    {
        // Must be called on the UI thread — Dispatcher.Invoke from the host if needed
        var fileNode = FindFileNodeVm(_vm.Roots, item);
        fileNode?.RefreshChangesetChild();
    }

    private static FileNodeVm? FindFileNodeVm(IEnumerable<SolutionExplorerNodeVm> nodes, IProjectItem item)
    {
        foreach (var node in nodes)
        {
            if (node is FileNodeVm fn && fn.Source.Id == item.Id) return fn;
            var found = FindFileNodeVm(node.Children, item);
            if (found is not null) return found;
        }
        return null;
    }
}

/// <summary>
/// Event args for "Set/Clear default TBL" requests from the Solution Explorer.
/// </summary>
public sealed class DefaultTblChangeEventArgs : EventArgs
{
    public IProject     Project { get; init; } = null!;
    public IProjectItem? TblItem { get; init; } // null = clear
}

/// <summary>
/// Event args for "Apply TBL to document(s)" requests from the Solution Explorer context menu.
/// </summary>
public sealed class ApplyTblRequestedEventArgs : EventArgs
{
    /// <summary>Project that owns the TBL item.</summary>
    public IProject     Project    { get; init; } = null!;

    /// <summary>The .tbl / .tblx item to apply.</summary>
    public IProjectItem TblItem    { get; init; } = null!;

    /// <summary>
    /// <see langword="true"/> = apply to all open HexEditor documents;
    /// <see langword="false"/> = apply only to the currently active HexEditor.
    /// </summary>
    public bool         ApplyToAll { get; init; }
}
