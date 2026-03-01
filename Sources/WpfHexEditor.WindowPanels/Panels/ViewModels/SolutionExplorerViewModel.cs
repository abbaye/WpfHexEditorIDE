//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.WindowPanels.Panels.ViewModels;

/// <summary>
/// Root view-model for <see cref="SolutionExplorerPanel"/>.
/// Builds and synchronises the tree from an <see cref="ISolution"/>.
/// </summary>
public sealed class SolutionExplorerViewModel : INotifyPropertyChanged
{
    private ISolution? _solution;
    private string     _searchText = "";

    // ── Tree ─────────────────────────────────────────────────────────────────

    public ObservableCollection<SolutionExplorerNodeVm> Roots { get; } = [];

    // ── Search ───────────────────────────────────────────────────────────────

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value) return;
            _searchText = value;
            OnPropertyChanged();
            ApplySearch();
        }
    }

    // ── Solution binding ─────────────────────────────────────────────────────

    public void SetSolution(ISolution? solution)
    {
        _solution = solution;
        Rebuild();
    }

    public void Rebuild()
    {
        Roots.Clear();

        if (_solution is null) return;

        var solutionNode = new SolutionNodeVm(_solution);
        Roots.Add(solutionNode);

        foreach (var project in _solution.Projects)
        {
            var projNode = BuildProjectNode(project);
            solutionNode.Children.Add(projNode);
        }
    }

    /// <summary>Refreshes the default-TBL bold state for items in the given project.</summary>
    public void RefreshDefaultTbl(IProject project)
    {
        if (Roots.Count == 0) return;
        var solutionNode = Roots[0];
        foreach (var child in solutionNode.Children)
        {
            if (child is ProjectNodeVm pn && pn.Source == project)
            {
                MarkDefaultTbl(pn, project.DefaultTblItemId);
                return;
            }
        }
    }

    // ── Node construction ────────────────────────────────────────────────────

    private static ProjectNodeVm BuildProjectNode(IProject project)
    {
        var node = new ProjectNodeVm(project);

        // Items not in any virtual folder
        var inFolder = new HashSet<string>();
        CollectFolderItemIds(project.RootFolders, inFolder);

        // Virtual folders first
        foreach (var folder in project.RootFolders)
            node.Children.Add(BuildFolderNode(folder, project));

        // Loose items
        foreach (var item in project.Items)
        {
            if (!inFolder.Contains(item.Id))
                node.Children.Add(MakeFileNode(item, project));
        }

        return node;
    }

    private static FolderNodeVm BuildFolderNode(IVirtualFolder folder, IProject project)
    {
        var node = new FolderNodeVm(folder) { IsExpanded = false, Project = project };

        foreach (var childFolder in folder.Children)
            node.Children.Add(BuildFolderNode(childFolder, project));

        foreach (var id in folder.ItemIds)
        {
            var item = project.FindItem(id);
            if (item is not null)
                node.Children.Add(MakeFileNode(item, project));
        }

        return node;
    }

    private static FileNodeVm MakeFileNode(IProjectItem item, IProject project)
        => new(item, isDefaultTbl: item.Id == project.DefaultTblItemId)
        {
            Project = project,
        };

    private static void CollectFolderItemIds(IReadOnlyList<IVirtualFolder> folders, HashSet<string> set)
    {
        foreach (var f in folders)
        {
            foreach (var id in f.ItemIds) set.Add(id);
            CollectFolderItemIds(f.Children, set);
        }
    }

    private static void MarkDefaultTbl(SolutionExplorerNodeVm node, string? defaultId)
    {
        foreach (var child in node.Children)
        {
            if (child is FileNodeVm fn)
                fn.IsDefaultTbl = fn.Source.Id == defaultId;
            MarkDefaultTbl(child, defaultId);
        }
    }

    // ── Search filter ─────────────────────────────────────────────────────────

    private void ApplySearch()
    {
        // Simple visibility filter: collapse/expand based on text match
        // (full visibility filter would require a converter; this just expands matched paths)
        if (string.IsNullOrWhiteSpace(_searchText))
        {
            SetExpanded(Roots, true);
            return;
        }

        foreach (var root in Roots)
            ExpandIfMatch(root, _searchText.ToLowerInvariant());
    }

    private static bool ExpandIfMatch(SolutionExplorerNodeVm node, string query)
    {
        bool selfMatch = node.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase);
        bool childMatch = false;

        foreach (var child in node.Children)
            childMatch |= ExpandIfMatch(child, query);

        node.IsExpanded = selfMatch || childMatch;
        return selfMatch || childMatch;
    }

    private static void SetExpanded(IEnumerable<SolutionExplorerNodeVm> nodes, bool expanded)
    {
        foreach (var n in nodes)
        {
            n.IsExpanded = expanded;
            SetExpanded(n.Children, expanded);
        }
    }

    // ── INPC ─────────────────────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
