//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.WindowPanels.Panels.ViewModels;

namespace WpfHexEditor.WindowPanels.Panels;

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

    // ── Tree events ──────────────────────────────────────────────────────────

    private void OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        UpdateContextMenu(e.NewValue as SolutionExplorerNodeVm);

        if (e.NewValue is FileNodeVm fn && fn.Project is not null)
            ItemSelected?.Invoke(this, new ProjectItemEventArgs { Item = fn.Source, Project = fn.Project });
    }

    private void OnItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SolutionTree.SelectedItem is FileNodeVm fn && fn.Project is not null)
            ItemActivated?.Invoke(this, new ProjectItemActivatedEventArgs { Item = fn.Source, Project = fn.Project });
    }

    // ── Toolbar ──────────────────────────────────────────────────────────────

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

    // ── Search box ───────────────────────────────────────────────────────────

    private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        => SearchPlaceholder.Visibility = Visibility.Collapsed;

    private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        => SearchPlaceholder.Visibility =
            string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;

    // ── Context menu ─────────────────────────────────────────────────────────

    private void UpdateContextMenu(SolutionExplorerNodeVm? node)
    {
        _contextMenuTarget = node;
        bool isFile = node is FileNodeVm;
        bool isTbl  = node is FileNodeVm fn && fn.Source.ItemType == ProjectItemType.Tbl;

        SetDefaultTblMenuItem.Visibility   = isTbl ? Visibility.Visible : Visibility.Collapsed;
        ClearDefaultTblMenuItem.Visibility = isTbl ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnAddNewItem(object sender, RoutedEventArgs e)
    {
        // Raised to host; implemented in App layer
    }

    private void OnAddExistingItem(object sender, RoutedEventArgs e)
    {
        // Raised to host; implemented in App layer
    }

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
        if (_contextMenuTarget is FileNodeVm fn && fn.Project is not null)
            ItemRenameRequested?.Invoke(this, new ProjectItemEventArgs { Item = fn.Source, Project = fn.Project });
    }

    private void OnRemove(object sender, RoutedEventArgs e)
    {
        if (_contextMenuTarget is FileNodeVm fn && fn.Project is not null)
            ItemDeleteRequested?.Invoke(this, new ProjectItemEventArgs { Item = fn.Source, Project = fn.Project });
    }

    private void OnProperties(object sender, RoutedEventArgs e)
    {
        // Raised to host
    }

    // ── Additional public events ──────────────────────────────────────────────

    /// <summary>Raised when the user requests a change to the project default TBL.</summary>
    public event EventHandler<DefaultTblChangeEventArgs>? DefaultTblChangeRequested;

    // ── Helpers ──────────────────────────────────────────────────────────────

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
