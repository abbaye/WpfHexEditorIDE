//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Panels.IDE.ViewModels;

/// <summary>
/// Controls how items inside each project node are sorted.
/// </summary>
public enum SortMode
{
    None,
    Name,
    Type,
    DateModified,
    Size
}

/// <summary>
/// Controls which item types are visible in the tree.
/// </summary>
public enum FilterMode
{
    All,
    Binary,
    Text,
    Image,
    Language
}

/// <summary>
/// Root view-model for <see cref="SolutionExplorerPanel"/>.
/// Builds and synchronises the tree from an <see cref="ISolution"/>.
/// </summary>
public sealed class SolutionExplorerViewModel : INotifyPropertyChanged
{
    private ISolution? _solution;
    private string     _searchText = "";
    private bool       _showAllFiles;
    private SortMode   _currentSort   = SortMode.None;
    private FilterMode _currentFilter = FilterMode.All;

    // -- Collapse-state memory (session-level) ---------------------------------
    // Persists IsExpanded across Rebuild() calls so tree state is not lost on
    // sort/filter/file-watcher refreshes.  Keyed by a stable node identifier.

    private readonly Dictionary<string, bool> _expandedState = new(StringComparer.Ordinal);

    // -- Tree -----------------------------------------------------------------

    public ObservableCollection<SolutionExplorerNodeVm> Roots { get; } = [];

    // -- Multi-select (D3) ----------------------------------------------------

    /// <summary>
    /// All nodes that currently have <see cref="SolutionExplorerNodeVm.IsSelected"/> set.
    /// Updated by <see cref="SelectNode"/> / <see cref="ToggleNodeSelection"/> /
    /// <see cref="RangeSelectTo"/>.
    /// </summary>
    public IReadOnlyList<SolutionExplorerNodeVm> SelectedNodes
        => CollectSelected(Roots);

    /// <summary>
    /// Deselects all nodes without selecting a new one.
    /// Used by <see cref="SolutionExplorerPanel.SyncWithFile"/> before highlighting the tracked file
    /// so that auto-tracking never adds to an existing multi-selection.
    /// </summary>
    public void ClearSelection()
    {
        ClearAllSelected(Roots);
        _lastAnchor = null;
        OnPropertyChanged(nameof(SelectedNodes));
    }

    /// <summary>
    /// Clears all selections, then selects <paramref name="node"/> as the sole selection.
    /// Stores it as the anchor for future range selections.
    /// </summary>
    public void SelectNode(SolutionExplorerNodeVm node)
    {
        ClearAllSelected(Roots);
        node.IsSelected  = true;
        _lastAnchor      = node;
        OnPropertyChanged(nameof(SelectedNodes));
    }

    /// <summary>
    /// Toggles the selection state of <paramref name="node"/> (Ctrl+Click).
    /// </summary>
    public void ToggleNodeSelection(SolutionExplorerNodeVm node)
    {
        node.IsSelected = !node.IsSelected;
        if (node.IsSelected) _lastAnchor = node;
        OnPropertyChanged(nameof(SelectedNodes));
    }

    /// <summary>
    /// Selects all nodes between <see cref="_lastAnchor"/> and <paramref name="target"/>
    /// in document order (Shift+Click).
    /// </summary>
    public void RangeSelectTo(SolutionExplorerNodeVm target)
    {
        var flat    = FlattenVisible(Roots);
        var anchorIdx = _lastAnchor is null ? 0 : flat.IndexOf(_lastAnchor);
        var targetIdx = flat.IndexOf(target);

        if (anchorIdx < 0 || targetIdx < 0) { SelectNode(target); return; }

        int start = Math.Min(anchorIdx, targetIdx);
        int end   = Math.Max(anchorIdx, targetIdx);

        ClearAllSelected(Roots);
        for (int i = start; i <= end; i++)
            flat[i].IsSelected = true;

        OnPropertyChanged(nameof(SelectedNodes));
    }

    private SolutionExplorerNodeVm? _lastAnchor;

    private static IReadOnlyList<SolutionExplorerNodeVm> CollectSelected(
        IEnumerable<SolutionExplorerNodeVm> nodes)
    {
        var result = new List<SolutionExplorerNodeVm>();
        CollectSelectedCore(nodes, result);
        return result;
    }

    private static void CollectSelectedCore(
        IEnumerable<SolutionExplorerNodeVm> nodes,
        List<SolutionExplorerNodeVm> result)
    {
        foreach (var node in nodes)
        {
            if (node.IsSelected) result.Add(node);
            CollectSelectedCore(node.Children, result);
        }
    }

    private static void ClearAllSelected(IEnumerable<SolutionExplorerNodeVm> nodes)
    {
        foreach (var node in nodes)
        {
            node.IsSelected = false;
            ClearAllSelected(node.Children);
        }
    }

    private static List<SolutionExplorerNodeVm> FlattenVisible(
        IEnumerable<SolutionExplorerNodeVm> nodes)
    {
        var result = new List<SolutionExplorerNodeVm>();
        FlattenCore(nodes, result);
        return result;
    }

    private static void FlattenCore(
        IEnumerable<SolutionExplorerNodeVm> nodes,
        List<SolutionExplorerNodeVm> result)
    {
        foreach (var node in nodes)
        {
            result.Add(node);
            if (node.IsExpanded)
                FlattenCore(node.Children, result);
        }
    }

    // -- Search ---------------------------------------------------------------

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

    // -- Show All Files -------------------------------------------------------

    public bool ShowAllFiles
    {
        get => _showAllFiles;
        set
        {
            if (_showAllFiles == value) return;
            _showAllFiles = value;
            OnPropertyChanged();
            Rebuild();
        }
    }

    // -- Sort -----------------------------------------------------------------

    public SortMode CurrentSort
    {
        get => _currentSort;
        set
        {
            if (_currentSort == value) return;
            _currentSort = value;
            OnPropertyChanged();
            Rebuild();
        }
    }

    // -- Filter ---------------------------------------------------------------

    public FilterMode CurrentFilter
    {
        get => _currentFilter;
        set
        {
            if (_currentFilter == value) return;
            _currentFilter = value;
            OnPropertyChanged();
            Rebuild();
        }
    }

    // -- Solution binding -----------------------------------------------------

    public void SetSolution(ISolution? solution)
    {
        _solution = solution;
        Rebuild();
    }

    public void Rebuild()
    {
        // Capture current IsExpanded state before clearing the tree
        CaptureExpandedState(Roots);

        Roots.Clear();

        if (_solution is null) return;

        var solutionNode = new SolutionNodeVm(_solution);
        Roots.Add(solutionNode);

        // Solution Folders come first; collect which project names are inside a folder.
        var folderedNames = CollectFolderedProjectNames(_solution.RootFolders);

        foreach (var folder in _solution.RootFolders)
            solutionNode.Children.Add(BuildSolutionFolderNode(folder, _solution));

        // Unfoldered projects follow directly under the solution node.
        foreach (var project in _solution.Projects)
        {
            if (folderedNames.Contains(project.Name)) continue;

            var projNode = _showAllFiles
                ? BuildProjectNodePhysical(project)
                : BuildProjectNode(project);
            solutionNode.Children.Add(projNode);
        }

        // Apply sort and filter after building the tree.
        ApplySort(solutionNode.Children);
        if (_currentFilter != FilterMode.All)
            ApplyFilterVisibility(solutionNode.Children);

        // Restore remembered IsExpanded state (first Rebuild after loading will use defaults)
        RestoreExpandedState(Roots);
    }

    // -- Collapse-state cross-session persistence ------------------------------

    /// <summary>
    /// Snapshots the current live tree and returns all stable keys of expanded nodes.
    /// Intended for serialisation to the <c>.whsln.user</c> sidecar file.
    /// </summary>
    public IReadOnlyList<string> GetExpandedKeys()
    {
        CaptureExpandedState(Roots);
        return _expandedState
            .Where(kv => kv.Value)
            .Select(kv => kv.Key)
            .ToList();
    }

    /// <summary>
    /// Replaces the in-memory expand state with <paramref name="keys"/> and applies
    /// it to the live tree immediately. Nodes not in the list are collapsed.
    /// Call after <see cref="Rebuild"/> once the tree has been populated.
    /// </summary>
    public void ApplyExpandedKeys(IReadOnlyList<string> keys)
    {
        _expandedState.Clear();
        foreach (var key in keys)
            _expandedState[key] = true;
        RestoreExpandedState(Roots);
    }

    // -- Collapse-state helpers ------------------------------------------------

    /// <summary>
    /// Recursively walks <paramref name="nodes"/> and records each node's
    /// <see cref="SolutionExplorerNodeVm.IsExpanded"/> into <see cref="_expandedState"/>
    /// keyed by a stable identifier derived from the node type and its domain ID.
    /// </summary>
    private void CaptureExpandedState(IEnumerable<SolutionExplorerNodeVm> nodes)
    {
        foreach (var node in nodes)
        {
            var key = StableKeyFor(node);
            if (key is not null)
                _expandedState[key] = node.IsExpanded;
            CaptureExpandedState(node.Children);
        }
    }

    /// <summary>
    /// Recursively walks <paramref name="nodes"/> and restores each node's
    /// <see cref="SolutionExplorerNodeVm.IsExpanded"/> from <see cref="_expandedState"/>
    /// if a matching key exists; otherwise the node keeps its default value.
    /// </summary>
    private void RestoreExpandedState(IEnumerable<SolutionExplorerNodeVm> nodes)
    {
        foreach (var node in nodes)
        {
            var key = StableKeyFor(node);
            if (key is not null && _expandedState.TryGetValue(key, out bool expanded))
                node.IsExpanded = expanded;
            RestoreExpandedState(node.Children);
        }
    }

    /// <summary>
    /// Returns a stable string key for <paramref name="node"/> based on its
    /// concrete type and domain identifier (folder ID, project ID, path …).
    /// Returns <see langword="null"/> for node types that do not need state persistence.
    /// </summary>
    private static string? StableKeyFor(SolutionExplorerNodeVm node) => node switch
    {
        SolutionNodeVm       sn  => $"sol:{sn.Source.Name}",
        SolutionFolderNodeVm sfv => $"sfolder:{sfv.Folder.Id}",
        ProjectNodeVm        pv  => $"proj:{pv.Source.Id}",
        FolderNodeVm         fv  => $"vfolder:{fv.Folder.Id}",
        PhysicalFolderNodeVm pfv => $"phys:{pfv.PhysicalPath}",
        _                        => null,
    };

    private void ApplyFilterVisibility(ObservableCollection<SolutionExplorerNodeVm> children)
    {
        // Filter is a view concern: here we just collapse non-matching file nodes
        // by leaving them out. Since we rebuild, we can remove them.
        var toRemove = children
            .Where(n => n is FileNodeVm && !PassesFilter(n))
            .ToList();
        foreach (var n in toRemove) children.Remove(n);

        foreach (var child in children)
            ApplyFilterVisibility(child.Children);
    }

    /// <summary>
    /// Refreshes the default-TBL bold state for items in the given project.
    /// </summary>
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

    // -- Node construction ----------------------------------------------------

    private static HashSet<string> CollectFolderedProjectNames(IReadOnlyList<ISolutionFolder> folders)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in folders) CollectFolderedNamesCore(f, set);
        return set;
    }

    private static void CollectFolderedNamesCore(ISolutionFolder folder, HashSet<string> set)
    {
        foreach (var id in folder.ProjectIds) set.Add(id);
        foreach (var child in folder.Children) CollectFolderedNamesCore(child, set);
    }

    private SolutionFolderNodeVm BuildSolutionFolderNode(ISolutionFolder folder, ISolution solution)
    {
        var node = new SolutionFolderNodeVm(folder, solution) { IsExpanded = true };

        // Nested solution folders first (recursive).
        foreach (var child in folder.Children)
            node.Children.Add(BuildSolutionFolderNode(child, solution));

        // Projects inside this folder.
        foreach (var projectName in folder.ProjectIds)
        {
            var project = solution.Projects.FirstOrDefault(p => p.Name == projectName);
            if (project is null) continue;

            var projNode = _showAllFiles
                ? BuildProjectNodePhysical(project)
                : BuildProjectNode(project);
            node.Children.Add(projNode);
        }

        return node;
    }

    private static ProjectNodeVm BuildProjectNode(IProject project)
    {
        var node = new ProjectNodeVm(project);

        // Items not in any virtual folder
        var inFolder = new HashSet<string>();
        CollectFolderItemIds(project.RootFolders, inFolder);

        // Virtual folders first
        foreach (var folder in project.RootFolders)
            node.Children.Add(BuildFolderNode(folder, project));

        // Loose items — apply nesting within the loose-items scope
        var looseItems = project.Items
            .Where(i => !inFolder.Contains(i.Id))
            .ToList();

        var nesting = FileNestingDetector.Compute(looseItems);

        foreach (var item in looseItems)
        {
            if (nesting.DependentNames.Contains(item.Name)) continue;

            var fileNode = MakeFileNode(item, project);

            if (nesting.ParentToChildren.TryGetValue(item.Name, out var deps))
                foreach (var dep in deps)
                    fileNode.Children.Insert(0, new DependentFileNodeVm(dep, project));

            node.Children.Add(fileNode);
        }

        return node;
    }

    private static FolderNodeVm BuildFolderNode(IVirtualFolder folder, IProject project, string parentRelPath = "")
    {
        var relPath = parentRelPath.Length == 0 ? folder.Name : $"{parentRelPath}/{folder.Name}";
        var node = new FolderNodeVm(folder) { IsExpanded = false, Project = project, ComputedRelPath = relPath };

        foreach (var childFolder in folder.Children)
            node.Children.Add(BuildFolderNode(childFolder, project, relPath));

        // Collect items for this folder scope
        var scopeItems = new List<IProjectItem>();
        foreach (var id in folder.ItemIds)
        {
            var item = project.FindItem(id);
            if (item is not null) scopeItems.Add(item);
        }

        // Apply nesting: dependent files become children of their parent item
        var nesting = FileNestingDetector.Compute(scopeItems);

        foreach (var item in scopeItems)
        {
            if (nesting.DependentNames.Contains(item.Name)) continue;

            var fileNode = MakeFileNode(item, project);

            if (nesting.ParentToChildren.TryGetValue(item.Name, out var deps))
                foreach (var dep in deps)
                    fileNode.Children.Insert(0, new DependentFileNodeVm(dep, project));

            node.Children.Add(fileNode);
        }

        return node;
    }

    private static FileNodeVm MakeFileNode(IProjectItem item, IProject project)
    {
        var ext       = Path.GetExtension(item.Name);
        var canExpand = string.Equals(ext, ".cs",   StringComparison.OrdinalIgnoreCase)
                     || string.Equals(ext, ".xaml", StringComparison.OrdinalIgnoreCase);

        var node = new FileNodeVm(item, isDefaultTbl: item.Id == project.DefaultTblItemId)
        {
            Project          = project,
            SupportsExpansion = canExpand,
        };

        // Inject a LoadingNodeVm sentinel so WPF shows the expand arrow.
        // The code-behind will replace it with real member nodes on first expand.
        if (canExpand)
            node.Children.Add(new LoadingNodeVm());

        return node;
    }

    // -------------------------------------------------------------------------
    // FileNestingDetector — pure utility, no state
    // -------------------------------------------------------------------------

    /// <summary>
    /// Classifies a flat list of <see cref="IProjectItem"/>s within one scope
    /// (folder or loose-items list) into parent items and their dependent children
    /// by applying VS-like naming conventions.
    /// O(n) per scope — uses a name→item dictionary for O(1) parent lookups.
    /// </summary>
    private static class FileNestingDetector
    {
        internal sealed class NestingResult
        {
            /// <summary>Names of items that should be nested under a parent (excluded from main list).</summary>
            public HashSet<string> DependentNames { get; }
                = new(StringComparer.OrdinalIgnoreCase);

            /// <summary>Maps parent item name → ordered list of dependent items.</summary>
            public Dictionary<string, List<IProjectItem>> ParentToChildren { get; }
                = new(StringComparer.OrdinalIgnoreCase);
        }

        internal static NestingResult Compute(IReadOnlyList<IProjectItem> items)
        {
            var result  = new NestingResult();
            var nameMap = new Dictionary<string, IProjectItem>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in items)
                nameMap[item.Name] = item;

            foreach (var item in items)
            {
                var parentName = TryFindParent(item.Name, nameMap);
                if (parentName is null) continue;

                result.DependentNames.Add(item.Name);

                if (!result.ParentToChildren.TryGetValue(parentName, out var list))
                {
                    list = [];
                    result.ParentToChildren[parentName] = list;
                }
                list.Add(item);
            }

            return result;
        }

        /// <summary>
        /// Returns the name of the parent item for <paramref name="name"/>,
        /// or <see langword="null"/> if no nesting applies.
        /// </summary>
        private static string? TryFindParent(
            string name,
            Dictionary<string, IProjectItem> nameMap)
        {
            // Rule 1 — multi-extension: strip last extension and check
            // e.g. "Foo.xaml.cs" → try "Foo.xaml"; "Foo.xaml.vb" → try "Foo.xaml"
            var inner = StripLastExtension(name);
            if (!string.Equals(inner, name, StringComparison.OrdinalIgnoreCase)
                && nameMap.ContainsKey(inner))
                return inner;

            // Rule 2 — .Designer.cs / .designer.cs → check several candidate parents
            if (name.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase))
            {
                var baseName = name[..^".Designer.cs".Length];
                foreach (var ext in (ReadOnlySpan<string>)[".cs", ".resx", ".settings", ".xsd", ".wsdl"])
                {
                    var candidate = baseName + ext;
                    if (nameMap.ContainsKey(candidate)) return candidate;
                }
            }

            // Rule 3 — .settings.cs → .settings
            if (name.EndsWith(".settings.cs", StringComparison.OrdinalIgnoreCase))
            {
                var candidate = name[..^".cs".Length]; // "Foo.settings"
                if (nameMap.ContainsKey(candidate)) return candidate;
            }

            return null;
        }

        /// <summary>Strips the last extension: "Foo.xaml.cs" → "Foo.xaml".</summary>
        private static string StripLastExtension(string name)
        {
            var dot = name.LastIndexOf('.');
            return dot > 0 ? name[..dot] : name;
        }
    }

    private static void CollectFolderItemIds(IReadOnlyList<IVirtualFolder> folders, HashSet<string> set)
    {
        foreach (var f in folders)
        {
            foreach (var id in f.ItemIds) set.Add(id);
            CollectFolderItemIds(f.Children, set);
        }
    }

    // -- Physical tree (Show All Files) ----------------------------------------

    private static ProjectNodeVm BuildProjectNodePhysical(IProject project)
    {
        var node       = new ProjectNodeVm(project);
        var projectDir = Path.GetDirectoryName(project.ProjectFilePath);
        if (projectDir is null || !Directory.Exists(projectDir)) return node;

        var itemsByPath = project.Items
            .ToDictionary(i => i.AbsolutePath, StringComparer.OrdinalIgnoreCase);

        BuildPhysicalSubDir(node.Children, projectDir, project, itemsByPath, project.ProjectFilePath);
        return node;
    }

    private static void BuildPhysicalSubDir(
        ObservableCollection<SolutionExplorerNodeVm> children,
        string dir, IProject project,
        Dictionary<string, IProjectItem> itemsByPath,
        string projectFilePath)
    {
        foreach (var subDir in Directory.GetDirectories(dir))
        {
            var folderVm = new PhysicalFolderNodeVm(subDir) { Project = project, IsExpanded = false };
            BuildPhysicalSubDir(folderVm.Children, subDir, project, itemsByPath, projectFilePath);
            children.Add(folderVm);
        }

        foreach (var file in Directory.GetFiles(dir))
        {
            if (string.Equals(file, projectFilePath, StringComparison.OrdinalIgnoreCase))
                continue;
            itemsByPath.TryGetValue(file, out var linkedItem);
            children.Add(new PhysicalFileNodeVm(file) { Project = project, LinkedItem = linkedItem });
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

    // -- External modification (D1) -------------------------------------------

    /// <summary>
    /// Marks or clears the <see cref="FileNodeVm.IsModifiedExternally"/> flag for
    /// the node whose backing item has <paramref name="fullPath"/> as its absolute path.
    /// Safe to call from any thread — walks the tree on the calling thread.
    /// </summary>
    public void SetFileModifiedExternally(string fullPath, bool modified)
    {
        var node = FindFileNode(Roots, fullPath);
        if (node is not null)
            node.IsModifiedExternally = modified;
    }

    private static FileNodeVm? FindFileNode(IEnumerable<SolutionExplorerNodeVm> nodes, string fullPath)
    {
        foreach (var node in nodes)
        {
            if (node is FileNodeVm fn &&
                string.Equals(fn.Source.AbsolutePath, fullPath, StringComparison.OrdinalIgnoreCase))
                return fn;

            var hit = FindFileNode(node.Children, fullPath);
            if (hit is not null) return hit;
        }
        return null;
    }

    // -- Active document tracking (D5) ----------------------------------------

    /// <summary>
    /// Finds the <see cref="FileNodeVm"/> matching <paramref name="filePath"/>, expands
    /// all ancestors so it is visible, and sets <see cref="SolutionExplorerNodeVm.IsSelected"/>.
    /// </summary>
    public FileNodeVm? SyncWithFile(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return null;

        var node = FindFileNode(Roots, filePath);
        if (node is null) return null;

        node.IsSelected = true;

        // Expand ancestors so the node is visible.
        ExpandAncestors(Roots, node);

        return node;
    }

    private static bool ExpandAncestors(IEnumerable<SolutionExplorerNodeVm> nodes, SolutionExplorerNodeVm target)
    {
        foreach (var node in nodes)
        {
            if (node == target) return true;

            if (ExpandAncestors(node.Children, target))
            {
                node.IsExpanded = true;
                return true;
            }
        }
        return false;
    }

    // -- Sort helpers (D2) ----------------------------------------------------

    private void ApplySort(ObservableCollection<SolutionExplorerNodeVm> children)
    {
        if (_currentSort == SortMode.None) return;

        var sorted = _currentSort switch
        {
            SortMode.Name         => children.OrderBy(n => n.DisplayName, StringComparer.OrdinalIgnoreCase).ToList(),
            SortMode.Type         => children.OrderBy(n => n is FileNodeVm fn ? fn.Source.ItemType.ToString() : "")
                                             .ThenBy(n => n.DisplayName, StringComparer.OrdinalIgnoreCase).ToList(),
            SortMode.DateModified => children.OrderByDescending(n =>
                                        n is FileNodeVm fn && File.Exists(fn.Source.AbsolutePath)
                                            ? File.GetLastWriteTime(fn.Source.AbsolutePath)
                                            : DateTime.MinValue).ToList(),
            SortMode.Size         => children.OrderByDescending(n =>
                                        n is FileNodeVm fn && File.Exists(fn.Source.AbsolutePath)
                                            ? new FileInfo(fn.Source.AbsolutePath).Length
                                            : 0L).ToList(),
            _                     => null
        };

        if (sorted is null) return;

        for (int i = 0; i < sorted.Count; i++)
        {
            var current = children.IndexOf(sorted[i]);
            if (current != i) children.Move(current, i);
        }

        // Recurse into folders.
        foreach (var child in children)
            ApplySort(child.Children);
    }

    // -- Filter helpers (D2) --------------------------------------------------

    private bool PassesFilter(SolutionExplorerNodeVm node)
    {
        if (_currentFilter == FilterMode.All) return true;
        if (node is not FileNodeVm fn) return true;  // always show folders

        return _currentFilter switch
        {
            FilterMode.Binary   => fn.Source.ItemType is ProjectItemType.Binary,
            FilterMode.Text     => fn.Source.ItemType is ProjectItemType.Text or ProjectItemType.Tbl or ProjectItemType.Json,
            FilterMode.Image    => fn.Source.ItemType is ProjectItemType.Image or ProjectItemType.Tile,
            FilterMode.Language => fn.Source.ItemType is ProjectItemType.FormatDefinition or ProjectItemType.Script,
            _                   => true
        };
    }

    // -- Search filter ---------------------------------------------------------

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

    // -- INPC -----------------------------------------------------------------

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
