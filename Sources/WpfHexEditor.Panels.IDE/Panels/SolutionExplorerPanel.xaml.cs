//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

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

    public SolutionExplorerPanel()
    {
        InitializeComponent();
        DataContext = _vm;
    }

    // ── ISolutionExplorerPanel ────────────────────────────────────────────────

    public void SetSolution(ISolution? solution)
        => _vm.SetSolution(solution);

    public void SyncWithFile(string absolutePath)
    {
        // Walk tree and select the FileNodeVm matching the path
        SelectNodeByPath(absolutePath, _vm.Roots);
    }

    public event EventHandler<ProjectItemActivatedEventArgs>? ItemActivated;
    public event EventHandler<ProjectItemEventArgs>?          ItemSelected;
    public event EventHandler<ProjectItemEventArgs>?          ItemRenameRequested;
    public event EventHandler<ProjectItemEventArgs>?          ItemDeleteRequested;
    public event EventHandler<ItemMoveRequestedEventArgs>?    ItemMoveRequested;

    // ── Tree events ───────────────────────────────────────────────────────────

    private void OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is FileNodeVm fn && fn.Project is not null)
            ItemSelected?.Invoke(this, new ProjectItemEventArgs { Item = fn.Source, Project = fn.Project });
    }

    private void OnItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
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

        var  file      = node as FileNodeVm;
        bool isProject = node is ProjectNodeVm;
        bool isFolder  = node is FolderNodeVm;
        bool isFile    = file is not null;
        bool isTbl     = file?.Source.ItemType == ProjectItemType.Tbl;
        bool isDefault = file?.IsDefaultTbl == true;
        // "Convert to TBLX" only for plain .tbl files; .tblx is already the advanced format
        bool isThingyTbl = isTbl && string.Equals(
            Path.GetExtension(file?.Source.Name ?? string.Empty), ".tbl", StringComparison.OrdinalIgnoreCase);

        bool canAdd = isProject || isFolder;

        // Add New / Existing — project or folder only; Import Format — project only
        AddNewItemMenuItem        .Visibility = canAdd     ? Visibility.Visible : Visibility.Collapsed;
        AddExistingItemMenuItem   .Visibility = canAdd     ? Visibility.Visible : Visibility.Collapsed;
        ImportFormatMenuItem      .Visibility = isProject  ? Visibility.Visible : Visibility.Collapsed;
        NewFolderMenuItem         .Visibility = canAdd     ? Visibility.Visible : Visibility.Collapsed;
        NewPhysicalFolderMenuItem .Visibility = canAdd     ? Visibility.Visible : Visibility.Collapsed;
        AddFolderFromDiskMenuItem .Visibility = canAdd     ? Visibility.Visible : Visibility.Collapsed;
        AddSeparator              .Visibility = canAdd     ? Visibility.Visible : Visibility.Collapsed;

        // TBL — Set and Clear are mutually exclusive; Convert only for plain .tbl (not .tblx)
        SetDefaultTblMenuItem  .Visibility = (isTbl && !isDefault) ? Visibility.Visible : Visibility.Collapsed;
        ClearDefaultTblMenuItem.Visibility = (isTbl &&  isDefault) ? Visibility.Visible : Visibility.Collapsed;
        ConvertToTblxMenuItem  .Visibility = isThingyTbl           ? Visibility.Visible : Visibility.Collapsed;
        TblSeparator           .Visibility = isTbl                 ? Visibility.Visible : Visibility.Collapsed;

        // Rename / Remove — file or folder
        RenameMenuItem.Visibility = (isFile || isFolder) ? Visibility.Visible : Visibility.Collapsed;
        RemoveMenuItem.Visibility = (isFile || isFolder) ? Visibility.Visible : Visibility.Collapsed;

        // Properties — file or project
        bool hasProp = isFile || isProject;
        PropertiesSeparator.Visibility = hasProp ? Visibility.Visible : Visibility.Collapsed;
        PropertiesMenuItem .Visibility = hasProp ? Visibility.Visible : Visibility.Collapsed;

        return canAdd || isTbl || isFile || isFolder || isProject;
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
            CreatePhysical = false,
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
            CreatePhysical = true,
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

    private void OnRename(object sender, RoutedEventArgs e)
    {
        if (_contextMenuTarget is FileNodeVm fn)        StartInlineEdit(fn);
        else if (_contextMenuTarget is FolderNodeVm fv) StartInlineFolderEdit(fv);
    }

    private void OnRemove(object sender, RoutedEventArgs e)
    {
        if (_contextMenuTarget is FileNodeVm fn && fn.Project is not null)
            ItemDeleteRequested?.Invoke(this, new ProjectItemEventArgs { Item = fn.Source, Project = fn.Project });
        else if (_contextMenuTarget is FolderNodeVm fv && fv.Project is not null)
            FolderDeleteRequested?.Invoke(this, new FolderDeleteEventArgs { Folder = fv.Folder, Project = fv.Project });
    }

    private void OnProperties(object sender, RoutedEventArgs e)
    {
        // Raised to host
    }

    // ── Additional public events ───────────────────────────────────────────────

    /// <summary>Raised when the user requests a change to the project default TBL.</summary>
    public event EventHandler<DefaultTblChangeEventArgs>? DefaultTblChangeRequested;

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
            if      (SolutionTree.SelectedItem is FileNodeVm fn)   { StartInlineEdit(fn);       e.Handled = true; }
            else if (SolutionTree.SelectedItem is FolderNodeVm fv) { StartInlineFolderEdit(fv); e.Handled = true; }
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
        }

        SolutionTree.Focus();
    }

    // ── DragDrop ──────────────────────────────────────────────────────────────

    private const string DragDataFormat = "SolutionExplorerFileNode";
    private Point       _dragStartPoint;
    private FileNodeVm? _draggedNode;

    private void OnTreeMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        _draggedNode    = null;

        if (e.OriginalSource is DependencyObject src)
        {
            var tvi = FindAncestor<TreeViewItem>(src);
            if (tvi?.DataContext is FileNodeVm fn)
                _draggedNode = fn;
        }
    }

    private void OnTreeMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _draggedNode is null) return;

        var pos  = e.GetPosition(null);
        var diff = _dragStartPoint - pos;
        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance) return;

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
            if (SelectNodeByPath(path, node.Children)) return true;
        }
        return false;
    }
}

/// <summary>Event args for "Set/Clear default TBL" requests from the Solution Explorer.</summary>
public sealed class DefaultTblChangeEventArgs : EventArgs
{
    public IProject     Project { get; init; } = null!;
    public IProjectItem? TblItem { get; init; } // null = clear
}
