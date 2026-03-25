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
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
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
/// Controls how the search query is matched against tree nodes.
/// </summary>
public enum SearchMode
{
    FileName,       // substring match on display name only (default)
    NameAndContent, // substring match on name OR file content
    ContentOnly,    // substring match on file content only
    Regex           // regex match on file name
}

/// <summary>
/// Root view-model for <see cref="SolutionExplorerPanel"/>.
/// Builds and synchronises the tree from an <see cref="ISolution"/>.
/// </summary>
public sealed class SolutionExplorerViewModel : INotifyPropertyChanged
{
    // -- Flat-cache search node (pre-lowercased for zero-alloc matching) --------

    private readonly struct FlatNode
    {
        public readonly SolutionExplorerNodeVm  Node;
        public readonly SolutionExplorerNodeVm? Parent;
        public readonly string                  SearchKey;  // DisplayName.ToLowerInvariant(), built once
        public readonly string?                 FilePath;   // absolute path; null for non-file nodes
        public readonly bool                    IsTextFile; // false for Binary/Image/Tile

        public FlatNode(SolutionExplorerNodeVm node, SolutionExplorerNodeVm? parent,
                        string searchKey, string? filePath, bool isTextFile)
        {
            Node       = node;
            Parent     = parent;
            SearchKey  = searchKey;
            FilePath   = filePath;
            IsTextFile = isTextFile;
        }
    }

    private ISolution? _solution;
    private string     _searchText   = "";
    private SearchMode _searchMode   = SearchMode.FileName;
    private bool       _searchActive;   // true while a search (≥2 chars) is in effect
    private bool       _showAllFiles;
    private SortMode   _currentSort   = SortMode.None;
    private FilterMode _currentFilter = FilterMode.All;

    // -- Search async pipeline fields -----------------------------------------

    // Captured at construction (UI thread) — used to dispatch back from Task.Run.
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;

    // Flat ordered list of all tree nodes; rebuilt after every Rebuild().
    // volatile: background thread reads the reference; UI thread swaps it atomically.
    private volatile List<FlatNode> _flatNodeCache = [];

    private DispatcherTimer?         _searchDebounceTimer;
    private CancellationTokenSource? _searchCts;

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
            ScheduleSearch();
        }
    }

    public SearchMode CurrentSearchMode
    {
        get => _searchMode;
        set
        {
            if (_searchMode == value) return;
            _searchMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SearchModeTooltip));
            ScheduleSearch();
        }
    }

    public string SearchModeTooltip => _searchMode switch
    {
        SearchMode.FileName       => "Search: File Name",
        SearchMode.NameAndContent => "Search: Name + Content",
        SearchMode.ContentOnly    => "Search: Content Only",
        SearchMode.Regex          => "Search: Regex (file name)",
        _                         => "Search"
    };

    // -- Debounce + async search pipeline -------------------------------------

    /// <summary>
    /// Cancels any in-flight background search and stops the debounce timer.
    /// Safe to call from any UI-thread context (Rebuild, ScheduleSearch, Dispose).
    /// </summary>
    private void CancelSearch()
    {
        _searchDebounceTimer?.Stop();
        _searchDebounceTimer = null;
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = null;
    }

    /// <summary>
    /// Entry point called on every <see cref="SearchText"/> change.
    /// Empty queries are resolved immediately (fast path).
    /// Non-empty queries are debounced at 200 ms to avoid per-keystroke O(n) walks.
    /// </summary>
    private void ScheduleSearch()
    {
        CancelSearch();

        if (string.IsNullOrWhiteSpace(_searchText) || _searchText.Trim().Length < 2)
        {
            // Only reset the tree if a search was previously active.
            // If no search was running, the tree is already in its normal state — touching it
            // here would cause an unwanted full expansion when the user types the first char.
            if (_searchActive)
            {
                _searchActive = false;
                ResetToStructuralExpansion(Roots);
            }
            return;
        }

        _searchActive = true;

        // Adaptive debounce: 400 ms for content modes (disk I/O), 200 ms for name/regex.
        var debounceMs = _searchMode is SearchMode.NameAndContent or SearchMode.ContentOnly ? 400 : 200;
        _searchDebounceTimer = new DispatcherTimer(DispatcherPriority.Background)
            { Interval = TimeSpan.FromMilliseconds(debounceMs) };
        _searchDebounceTimer.Tick += OnSearchDebounceTimerTick;
        _searchDebounceTimer.Start();
    }

    /// <summary>
    /// Fires 200 ms after the last keystroke.  Captures a stable query snapshot
    /// and launches the background match task.
    /// </summary>
    private void OnSearchDebounceTimerTick(object? sender, EventArgs e)
    {
        _searchDebounceTimer?.Stop();
        _searchDebounceTimer = null;

        // Capture immutable locals before entering async context.
        var query = _searchText.ToLowerInvariant(); // single allocation per completed search
        var mode  = _searchMode;                    // snapshot — mode may change while searching
        var cache = _flatNodeCache;                  // volatile read — gets latest complete list

        var cts = new CancellationTokenSource();
        _searchCts = cts;

        _ = ExecuteSearchAsync(query, mode, cache, cts.Token);
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

    /// <summary>
    /// Updates <see cref="ProjectNodeVm.IsStartup"/> on every project node in the
    /// current tree without triggering a full rebuild.
    /// Call after <see cref="ISolutionManager.SetStartupProject"/> to refresh the bold indicator.
    /// </summary>
    public void UpdateStartupProject(string? projectId)
    {
        foreach (var node in EnumerateAllNodes(Roots))
            if (node is ProjectNodeVm pn)
                pn.IsStartup = pn.Source.Id == projectId;
    }

    private static IEnumerable<SolutionExplorerNodeVm> EnumerateAllNodes(
        IEnumerable<SolutionExplorerNodeVm> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in EnumerateAllNodes(node.Children))
                yield return child;
        }
    }

    public void Rebuild()
    {
        // Track whether this is a fresh load (no previously-remembered expansion state).
        bool isFreshLoad = _expandedState.Count == 0;

        // Capture current IsExpanded state before clearing the tree
        CaptureExpandedState(Roots);

        Roots.Clear();

        if (_solution is null) return;

        var solutionNode = new SolutionNodeVm(_solution);
        Roots.Add(solutionNode);

        // Solution Folders come first; collect which project names are inside a folder.
        var folderedNames = CollectFolderedProjectNames(_solution.RootFolders);

        foreach (var folder in _solution.RootFolders
                     .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
            solutionNode.Children.Add(BuildSolutionFolderNode(folder, _solution));

        // Unfoldered projects follow directly under the solution node — alphabetical.
        var startupId = _solution.StartupProject?.Id;
        foreach (var project in _solution.Projects
                     .Where(p => !folderedNames.Contains(p.Name))
                     .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            var projNode = _showAllFiles
                ? BuildProjectNodePhysical(project)
                : BuildProjectNode(project);
            projNode.IsStartup = project.Id == startupId;
            solutionNode.Children.Add(projNode);
        }

        // Apply sort and filter after building the tree.
        ApplySort(solutionNode.Children);
        if (_currentFilter != FilterMode.All)
            ApplyFilterVisibility(solutionNode.Children);

        // Restore remembered IsExpanded state (first Rebuild after loading will use defaults)
        RestoreExpandedState(Roots);

        // On a fresh load (no prior expanded state) collapse the entire tree so the user
        // starts from a clean slate — VS-like behaviour.  The active document's ancestors
        // will be expanded automatically via SyncWithFile once a document is focused.
        if (isFreshLoad)
            CollapseAllExceptRoot(Roots);

        // Cancel any in-flight search — the cache is about to be replaced.
        CancelSearch();

        // Build the flat search cache from the fully-populated tree.
        RebuildFlatCache();

        // Re-apply a pending query against the new cache.
        if (!string.IsNullOrWhiteSpace(_searchText))
            ScheduleSearch();
    }

    // -- Flat-cache construction -----------------------------------------------

    /// <summary>
    /// Rebuilds <see cref="_flatNodeCache"/> from the live tree.
    /// Must be called on the UI thread immediately after the tree is fully populated.
    /// The resulting list is immutable once assigned; the background search thread
    /// reads it via a volatile reference swap with no locking required.
    /// </summary>
    private void RebuildFlatCache()
    {
        var cache = new List<FlatNode>(256);
        foreach (var root in Roots)
            PopulateCacheRecursive(root, parent: null, cache);
        _flatNodeCache = cache; // volatile write — visible to background thread
    }

    /// <summary>
    /// Depth-first traversal that appends one <see cref="FlatNode"/> per node.
    /// <see cref="LoadingNodeVm"/> sentinels are skipped: they have no stable
    /// display name and are not user-searchable.
    /// </summary>
    private static void PopulateCacheRecursive(
        SolutionExplorerNodeVm  node,
        SolutionExplorerNodeVm? parent,
        List<FlatNode>          cache)
    {
        if (node is LoadingNodeVm) return;

        string? filePath   = null;
        bool    isTextFile = false;
        if (node is FileNodeVm fn)
        {
            filePath   = fn.Source.AbsolutePath;
            isTextFile = fn.Source.ItemType is not (
                ProjectItemType.Binary or ProjectItemType.Image or ProjectItemType.Tile);
        }

        cache.Add(new FlatNode(node, parent, node.DisplayName.ToLowerInvariant(), filePath, isTextFile));

        foreach (var child in node.Children)
            PopulateCacheRecursive(child, node, cache);
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
        var node      = new SolutionFolderNodeVm(folder, solution) { IsExpanded = true };
        var startupId = solution.StartupProject?.Id;

        // Nested solution folders first (recursive) — alphabetical.
        foreach (var child in folder.Children.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
            node.Children.Add(BuildSolutionFolderNode(child, solution));

        // Projects inside this folder — alphabetical.
        foreach (var projectName in folder.ProjectIds.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            var project = solution.Projects.FirstOrDefault(p => p.Name == projectName);
            if (project is null) continue;

            var projNode = _showAllFiles
                ? BuildProjectNodePhysical(project)
                : BuildProjectNode(project);
            projNode.IsStartup = project.Id == startupId;
            node.Children.Add(projNode);
        }

        return node;
    }

    private static ProjectNodeVm BuildProjectNode(IProject project)
    {
        var node = new ProjectNodeVm(project);

        // References section — only for projects that expose typed references (e.g. VS projects).
        if (project is IProjectWithReferences refs)
        {
            var refsNode = BuildReferencesNode(refs);
            if (refsNode is not null)
                node.Children.Add(refsNode);
        }

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
                    fileNode.Children.Insert(0, MakeDependentFileNode(dep, project));

            node.Children.Add(fileNode);
        }

        return node;
    }

    private static ReferencesContainerNodeVm? BuildReferencesNode(IProjectWithReferences refs)
    {
        if (refs.ProjectReferences.Count  == 0 &&
            refs.PackageReferences.Count  == 0 &&
            refs.AssemblyReferences.Count == 0 &&
            refs.AnalyzerReferences.Count == 0)
            return null;

        var container = new ReferencesContainerNodeVm { IsExpanded = false, Project = refs as IProject };

        // 1. Analyzers sub-folder (top, like VS).
        if (refs.AnalyzerReferences.Count > 0)
        {
            var analyzers = new AnalyzersContainerNodeVm { IsExpanded = false };
            foreach (var a in refs.AnalyzerReferences.OrderBy(r => System.IO.Path.GetFileName(r.HintPath), StringComparer.OrdinalIgnoreCase))
                analyzers.Children.Add(new AnalyzerNodeVm(a));
            container.Children.Add(analyzers);
        }

        // 2. Project-to-project references — sorted alphabetically.
        foreach (var path in refs.ProjectReferences
            .OrderBy(p => System.IO.Path.GetFileNameWithoutExtension(p), StringComparer.OrdinalIgnoreCase))
            container.Children.Add(new ProjectReferenceNodeVm(path));

        // 3. NuGet / package references — sorted alphabetically by Id.
        foreach (var pkg in refs.PackageReferences
            .OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase))
            container.Children.Add(new PackageReferenceNodeVm(pkg));

        // 4. Assembly references — BCL/framework first, then external DLLs, all alphabetical.
        foreach (var asm in refs.AssemblyReferences
            .OrderByDescending(r => r.IsFrameworkRef)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
            container.Children.Add(new AssemblyReferenceNodeVm(asm));

        return container;
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
                    fileNode.Children.Insert(0, MakeDependentFileNode(dep, project));

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
            Project           = project,
            SupportsExpansion = canExpand,
        };

        // Inject a LoadingNodeVm sentinel so WPF shows the expand arrow.
        // Start collapsed so the outline is loaded lazily on first user expand,
        // not eagerly when the solution is opened.
        if (canExpand)
        {
            node.IsExpanded = false;
            node.Children.Add(new LoadingNodeVm());
        }

        return node;
    }

    private static DependentFileNodeVm MakeDependentFileNode(IProjectItem item, IProject project)
    {
        var ext          = Path.GetExtension(item.Name);
        var canExpand    = string.Equals(ext, ".cs", StringComparison.OrdinalIgnoreCase);
        var node         = new DependentFileNodeVm(item, project) { SupportsExpansion = canExpand };

        // Inject a LoadingNodeVm sentinel so the dep file also shows an expand arrow.
        // Start collapsed — outline loads lazily on first expand.
        if (canExpand)
        {
            node.IsExpanded = false;
            node.Children.Add(new LoadingNodeVm());
        }

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
        // Only FileNodeVm carries the IsModifiedExternally flag.
        if (FindFileNode(Roots, fullPath) is FileNodeVm fn)
            fn.IsModifiedExternally = modified;
    }

    private static SolutionExplorerNodeVm? FindFileNode(IEnumerable<SolutionExplorerNodeVm> nodes, string fullPath)
    {
        foreach (var node in nodes)
        {
            // Match FileNodeVm or DependentFileNodeVm — allows SyncWithFile to highlight
            // code-behind files (e.g. App.xaml.cs) that are nested under their parent XAML node.
            string? nodePath = node switch
            {
                FileNodeVm      fn  => fn.Source.AbsolutePath,
                DependentFileNodeVm dep => dep.Source.AbsolutePath,
                _                   => null,
            };

            if (nodePath is not null &&
                string.Equals(nodePath, fullPath, StringComparison.OrdinalIgnoreCase))
                return node;

            var hit = FindFileNode(node.Children, fullPath);
            if (hit is not null) return hit;
        }
        return null;
    }

    // -- Active document tracking (D5) ----------------------------------------

    /// <summary>
    /// Finds the <see cref="FileNodeVm"/> or <see cref="DependentFileNodeVm"/> matching
    /// <paramref name="filePath"/>, expands all ancestors so it is visible, and sets
    /// <see cref="SolutionExplorerNodeVm.IsSelected"/>.
    /// </summary>
    public SolutionExplorerNodeVm? SyncWithFile(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return null;

        var node = FindFileNode(Roots, filePath);
        if (node is null) return null;

        node.IsSelected = true;

        // Expand ancestors so the node is visible (but NOT the node itself —
        // file-level expansion is always user-initiated).
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

    // -- Async search pipeline -------------------------------------------------

    /// <summary>
    /// Runs <see cref="ComputeMatchSet"/> on a background thread and applies the
    /// result on the UI thread via a single batched <see cref="ApplySearchResultBatch"/> call.
    /// Silently exits on cancellation so a superseding search can take over.
    /// </summary>
    private async Task ExecuteSearchAsync(
        string             lowerQuery,
        SearchMode         mode,
        List<FlatNode>     cache,
        CancellationToken  cancellationToken)
    {
        HashSet<SolutionExplorerNodeVm>? matches;
        try
        {
            matches = await Task.Run(
                () => ComputeMatchSet(lowerQuery, mode, cache, cancellationToken),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return; // superseded by a newer search — do nothing
        }

        // Return to UI thread; pass the token so a Rebuild() cancellation skips the apply.
        await _dispatcher.InvokeAsync(
            () => ApplySearchResultBatch(matches),
            DispatcherPriority.Background,
            cancellationToken);
    }

    /// <summary>
    /// Runs entirely on a background thread.
    /// Iterates the flat cache linearly and builds the set of nodes that must be
    /// expanded: every node whose <see cref="FlatNode.SearchKey"/> contains
    /// <paramref name="lowerQuery"/>, plus all their ancestors.
    /// </summary>
    private const long ContentSearchMaxBytes = 512 * 1024; // 512 KB guard

    private static HashSet<SolutionExplorerNodeVm> ComputeMatchSet(
        string            lowerQuery,
        SearchMode        mode,
        List<FlatNode>    cache,
        CancellationToken cancellationToken)
    {
        // For Regex mode compile the pattern once; invalid pattern yields empty results.
        System.Text.RegularExpressions.Regex? regex = null;
        if (mode == SearchMode.Regex)
        {
            try
            {
                regex = new System.Text.RegularExpressions.Regex(
                    lowerQuery,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase |
                    System.Text.RegularExpressions.RegexOptions.Compiled);
            }
            catch
            {
                return new HashSet<SolutionExplorerNodeVm>(ReferenceEqualityComparer.Instance);
            }
        }

        // Glob auto-detection: FileName queries containing * or ? are converted to regex.
        System.Text.RegularExpressions.Regex? globRegex = null;
        if (mode == SearchMode.FileName &&
            (lowerQuery.Contains('*') || lowerQuery.Contains('?')))
        {
            globRegex = GlobToRegex(lowerQuery);
        }

        // Build parent-lookup once — O(n).  ReferenceEqualityComparer avoids
        // string allocations and is correct because each node is a unique object.
        var parentMap = new Dictionary<SolutionExplorerNodeVm, SolutionExplorerNodeVm?>(
            cache.Count,
            ReferenceEqualityComparer.Instance);
        foreach (var flat in cache)
            parentMap[flat.Node] = flat.Parent;

        var result = new HashSet<SolutionExplorerNodeVm>(ReferenceEqualityComparer.Instance);

        foreach (var flat in cache)
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool isMatch = mode switch
            {
                SearchMode.Regex          => regex!.IsMatch(flat.Node.DisplayName),
                SearchMode.ContentOnly    => MatchesContent(flat, lowerQuery),
                SearchMode.NameAndContent => flat.SearchKey.Contains(lowerQuery, StringComparison.Ordinal)
                                             || MatchesContent(flat, lowerQuery),
                // FileName: use glob regex when * or ? detected, otherwise fast Contains.
                _ => globRegex is not null
                    ? globRegex.IsMatch(flat.Node.DisplayName)
                    : flat.SearchKey.Contains(lowerQuery, StringComparison.Ordinal)
            };

            if (!isMatch) continue;

            result.Add(flat.Node);

            // Walk up the parent chain; short-circuit once an ancestor is already in the set
            // (all its ancestors are too, because we added them when we first processed a
            //  match below it).
            var current = flat.Parent;
            while (current is not null)
            {
                if (!result.Add(current)) break;
                parentMap.TryGetValue(current, out current);
            }
        }

        return result;
    }

    /// <summary>
    /// Reads up to <see cref="ContentSearchMaxBytes"/> of a text file and checks
    /// whether it contains <paramref name="lowerQuery"/> (case-insensitive).
    /// Returns false for binary/image files, files over the size limit, or on any I/O error.
    /// Runs on a background thread; synchronous I/O is acceptable here.
    /// </summary>
    private static bool MatchesContent(in FlatNode flat, string lowerQuery)
    {
        if (flat.FilePath is null || !flat.IsTextFile) return false;
        try
        {
            var info = new FileInfo(flat.FilePath);
            if (!info.Exists || info.Length > ContentSearchMaxBytes) return false;
            return File.ReadAllText(flat.FilePath)
                       .Contains(lowerQuery, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    /// <summary>
    /// Converts a glob pattern (<c>*</c> = any chars, <c>?</c> = one char) into a
    /// case-insensitive anchored regex. All other regex metacharacters are escaped.
    /// </summary>
    private static System.Text.RegularExpressions.Regex GlobToRegex(string glob)
    {
        var sb = new System.Text.StringBuilder("^");
        foreach (char c in glob)
        {
            sb.Append(c switch
            {
                '*' => ".*",
                '?' => ".",
                _   => System.Text.RegularExpressions.Regex.Escape(c.ToString())
            });
        }
        sb.Append('$');
        return new System.Text.RegularExpressions.Regex(
            sb.ToString(),
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.Compiled);
    }

    /// <summary>
    /// UI-thread finaliser: applies all <see cref="SolutionExplorerNodeVm.IsExpanded"/>
    /// changes in a single tight loop, replacing the per-node <c>PropertyChanged</c> storm
    /// of the old recursive approach.
    /// <para>
    /// <b>Critical guard:</b> <see cref="FileNodeVm"/> and <see cref="DependentFileNodeVm"/>
    /// must never be expanded by search — doing so triggers
    /// <c>OnTreeItemExpanded</c> → <c>LoadSourceOutlineAsync</c>.
    /// These nodes are visible when their parent folder is expanded; they need no
    /// <c>IsExpanded</c> change of their own.
    /// </para>
    /// </summary>
    private void ApplySearchResultBatch(HashSet<SolutionExplorerNodeVm> matchedAndAncestors)
    {
        var cache = _flatNodeCache; // volatile read — use the same list that was searched
        foreach (var flat in cache)
        {
            var inSet = matchedAndAncestors.Contains(flat.Node);

            // Visibility: only fire PropertyChanged for nodes whose parent is already visible.
            // If the parent is NOT in the set, IsExpanded=false already hides all its children
            // via the ControlTemplate trigger — firing PropertyChanged on those descendants is
            // a wasted WPF DataTrigger evaluation for each live TreeViewItem container.
            var parentVisible = flat.Parent is null || matchedAndAncestors.Contains(flat.Parent);
            if (parentVisible && flat.Node.IsSearchVisible != inSet)
                flat.Node.IsSearchVisible = inSet;

            // Expansion: containers only — never set IsExpanded on file nodes (triggers lazy outline load).
            if (flat.Node is FileNodeVm or DependentFileNodeVm) continue;
            if (flat.Node.IsExpanded != inSet)
                flat.Node.IsExpanded = inSet;
        }
    }

    // -- Collapse helpers ------------------------------------------------------

    /// <summary>
    /// Collapses every node in the tree except the solution root(s), which stay expanded
    /// so the user can see the top-level structure.  Called on a fresh solution load to
    /// replicate VS behaviour (start collapsed; reveal nodes as documents are opened).
    /// </summary>
    private static void CollapseAllExceptRoot(IEnumerable<SolutionExplorerNodeVm> roots)
    {
        foreach (var root in roots)
        {
            root.IsExpanded = true;
            foreach (var child in root.Children)
                CollapseNodeRecursive(child);
        }
    }

    private static void CollapseNodeRecursive(SolutionExplorerNodeVm node)
    {
        node.IsExpanded = false;
        foreach (var child in node.Children)
            CollapseNodeRecursive(child);
    }

    // -- Search reset ----------------------------------------------------------

    /// <summary>
    /// Restores the tree to its "natural" post-search state: structural nodes (solution,
    /// project, folder) are expanded so items are visible; file-level nodes are collapsed
    /// so outline members are only loaded on explicit user expansion.
    /// </summary>
    private static void ResetToStructuralExpansion(IEnumerable<SolutionExplorerNodeVm> nodes)
    {
        foreach (var n in nodes)
        {
            // Restore visibility unconditionally — nodes may have been hidden during search.
            n.IsSearchVisible = true;

            // File and dependent-file nodes must stay collapsed — expanding them triggers
            // lazy outline loading which must remain user-initiated.
            if (n is FileNodeVm or DependentFileNodeVm)
            {
                n.IsExpanded = false;
            }
            else
            {
                // Structural nodes (solution, project, folder) are expanded so their
                // contents remain browsable after the search is cleared.
                n.IsExpanded = true;
            }

            ResetToStructuralExpansion(n.Children);
        }
    }

    // -- Incremental build dirty tracking -------------------------------------

    /// <summary>
    /// Updates the <see cref="ProjectNodeVm.IsBuildDirty"/> flag for the matching project node.
    /// Called from <c>MainWindow.Build.cs</c> when the IncrementalBuildTracker fires.
    /// Must be called on the UI thread.
    /// </summary>
    public void SetProjectDirty(string projectId, bool isDirty)
    {
        foreach (var node in Roots.OfType<SolutionNodeVm>()
                                   .SelectMany(s => s.Children)
                                   .OfType<ProjectNodeVm>())
        {
            if (node.Source.Id.Equals(projectId, StringComparison.OrdinalIgnoreCase))
            {
                node.IsBuildDirty = isDirty;
                return;
            }
        }
    }

    /// <summary>
    /// Sets <see cref="ProjectNodeVm.IsBuilding"/> for the project whose file path matches
    /// <paramref name="projectFilePath"/>. Must be called on the UI thread.
    /// </summary>
    public void SetProjectBuilding(string projectFilePath, bool isBuilding)
    {
        if (string.IsNullOrEmpty(projectFilePath)) return;
        var normalizedPath = Path.GetFullPath(projectFilePath);
        foreach (var node in Roots.OfType<SolutionNodeVm>()
                                   .SelectMany(s => s.Children)
                                   .OfType<ProjectNodeVm>())
        {
            var nodePath = node.Source.ProjectFilePath;
            if (!string.IsNullOrEmpty(nodePath) &&
                string.Equals(Path.GetFullPath(nodePath), normalizedPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                node.IsBuilding = isBuilding;
                return;
            }
        }
    }

    /// <summary>Clears <see cref="ProjectNodeVm.IsBuilding"/> on all project nodes (build cancelled).</summary>
    public void ClearAllBuilding()
    {
        foreach (var node in Roots.OfType<SolutionNodeVm>()
                                   .SelectMany(s => s.Children)
                                   .OfType<ProjectNodeVm>())
            node.IsBuilding = false;
    }

    // -- INPC -----------------------------------------------------------------

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
