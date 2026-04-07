// ==========================================================
// Project: WpfHexEditor.Plugins.DocumentStructure
// File: ViewModels/DocumentStructureViewModel.cs
// Created: 2026-04-05
// Description:
//     Main ViewModel for the Document Structure panel.
//     Manages provider resolution, debounced refresh, filtering,
//     sorting, caret tracking, and flat/tree mode switching.
//
// Architecture Notes:
//     Debounce: 300ms CTS-swap for refresh, 100ms throttle for caret.
//     Filtering: recursive ancestor-preserving walk.
//     Sort: re-sorts children at each level of the tree.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using WpfHexEditor.Core.DocumentStructure;
using WpfHexEditor.SDK.ExtensionPoints.DocumentStructure;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Plugins.DocumentStructure.ViewModels;

public enum SortMode { SourceOrder, Alphabetical, ByKind }

/// <summary>
/// ViewModel for the Document Structure panel.
/// </summary>
public sealed class DocumentStructureViewModel : ViewModelBase
{
    private readonly DocumentStructureProviderResolver _resolver;
    private readonly Dispatcher _dispatcher;
    private CancellationTokenSource? _refreshCts;
    private DateTime _lastCaretUpdate;

    private string _filterText = string.Empty;
    private SortMode _currentSort = SortMode.SourceOrder;
    private bool _isTreeMode = true;
    private bool _isLoading;
    private bool _isDocumentOpen;
    private bool _isPinned;
    private bool _scrollSyncEnabled;
    private string _statusText = "No document";
    private string? _activeProviderName;
    private string _breadcrumbText = string.Empty;
    private int _autoExpandDepth = 2;
    private int _maxDepthIndex;
    private StructureNodeVm? _highlightedNode;

    // ── Depth-limiting ────────────────────────────────────────────────────
    // Maps dropdown index → max nesting depth (int.MaxValue = unlimited).
    private static readonly int[] s_depthValues = [int.MaxValue, 2, 3, 5, 10];

    // ── Backing data (unfiltered, full depth) ─────────────────────────────
    private IReadOnlyList<DocumentStructureNode> _rawNodes = [];
    private IReadOnlyList<StructureNodeVm> _allRootNodes = [];

    public ObservableCollection<StructureNodeVm> RootNodes { get; } = [];
    public ObservableCollection<StructureNodeVm> FlatNodes { get; } = [];

    public string FilterText
    {
        get => _filterText;
        set
        {
            if (SetField(ref _filterText, value))
                ApplyFilter(value);
        }
    }

    public SortMode CurrentSort
    {
        get => _currentSort;
        set
        {
            if (SetField(ref _currentSort, value))
            {
                ApplySort(value);
                OnPropertyChanged(nameof(SortModeIndex));
            }
        }
    }

    public int SortModeIndex
    {
        get => (int)_currentSort;
        set { CurrentSort = (SortMode)value; }
    }

    public bool IsTreeMode
    {
        get => _isTreeMode;
        set => SetField(ref _isTreeMode, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetField(ref _isLoading, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    public string? ActiveProviderName
    {
        get => _activeProviderName;
        set => SetField(ref _activeProviderName, value);
    }

    public int AutoExpandDepth
    {
        get => _autoExpandDepth;
        set => SetField(ref _autoExpandDepth, value);
    }

    /// <summary>
    /// Index into the max-depth dropdown (0 = ∞, 1 = 2, 2 = 3, 3 = 5, 4 = 10).
    /// Changing this rebuilds the visible tree from the last fetched raw nodes.
    /// </summary>
    public int MaxDepthIndex
    {
        get => _maxDepthIndex;
        set
        {
            if (!SetField(ref _maxDepthIndex, Math.Clamp(value, 0, s_depthValues.Length - 1))) return;
            if (_rawNodes.Count > 0)
                _dispatcher.Invoke(RebuildWithCurrentDepth);
            MaxDepthChanged?.Invoke(this, _maxDepthIndex);
        }
    }

    /// <summary>Fired when the user changes the max-depth dropdown so the plugin can persist it.</summary>
    public event EventHandler<int>? MaxDepthChanged;

    public bool IsDocumentOpen
    {
        get => _isDocumentOpen;
        private set => SetField(ref _isDocumentOpen, value);
    }

    public bool IsPinned
    {
        get => _isPinned;
        set => SetField(ref _isPinned, value);
    }

    public bool ScrollSyncEnabled
    {
        get => _scrollSyncEnabled;
        set => SetField(ref _scrollSyncEnabled, value);
    }

    public string BreadcrumbText
    {
        get => _breadcrumbText;
        private set => SetField(ref _breadcrumbText, value);
    }

    /// <summary>Raised when scroll sync is active and the panel should scroll to a node.</summary>
    public event EventHandler<StructureNodeVm>? ScrollToNodeRequested;

    /// <summary>Raised when a node is activated (clicked) and the editor should navigate to it.</summary>
    public event EventHandler<StructureNodeVm>? NavigateRequested;

    /// <summary>Optional logger for diagnostics — set by plugin to route to Output panel.</summary>
    public Action<string>? Logger { get; set; }

    public DocumentStructureViewModel(DocumentStructureProviderResolver resolver)
    {
        _resolver = resolver;
        _dispatcher = Dispatcher.CurrentDispatcher;
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // Debounced Refresh
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    /// <summary>Queues a debounced refresh (300ms).</summary>
    public void QueueRefresh(string? filePath, string? documentType, string? language)
    {
        _refreshCts?.Cancel();
        _refreshCts = new CancellationTokenSource();
        var ct = _refreshCts.Token;
        _ = RefreshAfterDelay(filePath, documentType, language, ct);
    }

    private async Task RefreshAfterDelay(string? filePath, string? documentType, string? language, CancellationToken ct)
    {
        try
        {
            await Task.Delay(300, ct).ConfigureAwait(false);
            await RefreshAsync(filePath, documentType, language, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>Performs the actual structure query and UI update.</summary>
    public async Task RefreshAsync(string? filePath, string? documentType, string? language, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            _dispatcher.Invoke(() => ClearUI("No document"));
            return;
        }

        var candidates = _resolver.ResolveAll(filePath, documentType, language);
        Logger?.Invoke($"[RefreshAsync] fp={filePath} docType={documentType} lang={language} candidates={candidates.Count} [{string.Join(",", candidates.Select(c => c.DisplayName))}]");

        if (candidates.Count == 0)
        {
            Logger?.Invoke("[RefreshAsync] No provider matched — No structure available");
            _dispatcher.Invoke(() => ClearUI("No structure available"));
            return;
        }

        _dispatcher.Invoke(() => IsLoading = true);

        try
        {
            // Try providers in priority order — fall through to next if one returns empty
            DocumentStructureResult? result = null;
            IDocumentStructureProvider? usedProvider = null;
            foreach (var candidate in candidates)
            {
                Logger?.Invoke($"[RefreshAsync] Trying provider: {candidate.DisplayName}");
                result = await candidate.GetStructureAsync(filePath, ct).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();
                Logger?.Invoke($"[RefreshAsync] Provider {candidate.DisplayName} returned {result?.Nodes.Count ?? -1} nodes");
                if (result is not null && result.Nodes.Count > 0) { usedProvider = candidate; break; }
            }

            if (result is null || result.Nodes.Count == 0 || usedProvider is null)
            {
                Logger?.Invoke("[RefreshAsync] All providers returned empty — Empty structure");
                _dispatcher.Invoke(() => ClearUI("Empty structure"));
                return;
            }

            Logger?.Invoke($"[RefreshAsync] SUCCESS via {usedProvider.DisplayName} — {result.Nodes.Count} root nodes");

            // Store raw nodes; build VMs with current depth limit on background thread.
            var rawNodes = result.Nodes;
            var effective = ApplyDepthLimit(rawNodes, s_depthValues[_maxDepthIndex]);
            var vms = effective.Select(n => new StructureNodeVm(n)).ToList();
            var totalCount = CountNodesVm(vms);

            _dispatcher.Invoke(() =>
            {
                _rawNodes = rawNodes;
                _allRootNodes = vms;
                ActiveProviderName = usedProvider.DisplayName;
                StatusText = $"{totalCount} symbols";
                IsLoading = false;
                IsDocumentOpen = true;

                RebuildTreeFromSource(vms);
                BuildFlatList(vms);
                AutoExpand(vms, 0);
            });
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            _dispatcher.Invoke(() => ClearUI("Parse error"));
        }
    }

    private void ClearUI(string status)
    {
        _allRootNodes = [];
        RootNodes.Clear();
        FlatNodes.Clear();
        StatusText = status;
        BreadcrumbText = string.Empty;
        ActiveProviderName = null;
        IsLoading = false;
        IsDocumentOpen = false;
    }

    /// <summary>Clears the panel when no document is open (distinct from "no structure available").</summary>
    public void ClearForNoDocument()
    {
        _dispatcher.Invoke(() =>
        {
            ClearUI(string.Empty);
        });
    }

    public void CollapseAll()
    {
        foreach (var root in RootNodes)
            SetExpandedRecursive(root, false);
    }

    public void ExpandAll()
    {
        foreach (var root in RootNodes)
            SetExpandedRecursive(root, true);
    }

    private static void SetExpandedRecursive(StructureNodeVm node, bool expanded)
    {
        node.IsExpanded = expanded;
        foreach (var child in node.Children)
            SetExpandedRecursive(child, expanded);
    }

    public string BuildQualifiedName(StructureNodeVm node)
    {
        var path = new List<string>();
        if (FindPath(_allRootNodes, node, path))
            return string.Join(".", path);
        return node.Name;
    }

    private static bool FindPath(IReadOnlyList<StructureNodeVm> nodes, StructureNodeVm target, List<string> path)
    {
        foreach (var n in nodes)
        {
            path.Add(n.Name);
            if (ReferenceEquals(n, target)) return true;
            if (FindPath(n.Children, target, path)) return true;
            path.RemoveAt(path.Count - 1);
        }
        return false;
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // Filtering
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    private void ApplyFilter(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            // Show all
            foreach (var node in RootNodes)
                SetVisibilityRecursive(node, System.Windows.Visibility.Visible);

            var total = CountNodesVm(_allRootNodes);
            StatusText = $"{total} symbols";
            return;
        }

        var matchCount = 0;
        var totalCount = 0;
        foreach (var node in RootNodes)
        {
            var (matched, total) = ApplyFilterNode(node, text);
            matchCount += matched;
            totalCount += total;
        }
        StatusText = $"{matchCount} of {totalCount} symbols";
    }

    private static (int matched, int total) ApplyFilterNode(StructureNodeVm node, string text)
    {
        var matchCount = 0;
        var totalCount = 1;
        var selfMatch = node.Name.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                        (node.Detail?.Contains(text, StringComparison.OrdinalIgnoreCase) == true);

        var anyChildMatch = false;
        foreach (var child in node.Children)
        {
            var (cm, ct) = ApplyFilterNode(child, text);
            matchCount += cm;
            totalCount += ct;
            if (cm > 0) anyChildMatch = true;
        }

        if (selfMatch) matchCount++;

        node.Visibility = (selfMatch || anyChildMatch)
            ? System.Windows.Visibility.Visible
            : System.Windows.Visibility.Collapsed;

        if (anyChildMatch) node.IsExpanded = true;

        return (matchCount, totalCount);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // Sorting
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    private void ApplySort(SortMode mode)
    {
        if (_allRootNodes.Count == 0) return;

        var sorted = SortNodes(_allRootNodes.ToList(), mode);
        RebuildTreeFromSource(sorted);
        BuildFlatList(sorted);
    }

    private static List<StructureNodeVm> SortNodes(List<StructureNodeVm> nodes, SortMode mode)
    {
        var sorted = mode switch
        {
            SortMode.Alphabetical => nodes.OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            SortMode.ByKind       => nodes.OrderBy(n => KindOrder(n.Kind)).ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            _                     => nodes.OrderBy(n => n.StartLine >= 0 ? n.StartLine : n.ByteOffset).ToList(),
        };

        foreach (var node in sorted)
        {
            if (node.Children.Count > 0)
            {
                var sortedChildren = SortNodes(node.Children.ToList(), mode);
                node.Children.Clear();
                foreach (var c in sortedChildren)
                    node.Children.Add(c);
            }
        }

        return sorted;
    }

    private static int KindOrder(string kind) => kind switch
    {
        "namespace" => 0,
        "module"    => 1,
        "class"     => 2,
        "struct"    => 3,
        "record"    => 3,
        "interface" => 4,
        "enum"      => 5,
        "method"    => 6,
        "function"  => 6,
        "constructor" => 7,
        "property"  => 8,
        "field"     => 9,
        "event"     => 10,
        "constant"  => 11,
        _           => 20,
    };

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // Tree / Flat
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    private void RebuildTreeFromSource(IReadOnlyList<StructureNodeVm> source)
    {
        RootNodes.Clear();
        foreach (var node in source)
            RootNodes.Add(node);
    }

    private void BuildFlatList(IReadOnlyList<StructureNodeVm> source)
    {
        FlatNodes.Clear();
        FlattenDfs(source, 0);
    }

    private void FlattenDfs(IReadOnlyList<StructureNodeVm> nodes, int depth)
    {
        foreach (var node in nodes)
        {
            FlatNodes.Add(new StructureNodeVm(node, depth));
            FlattenDfs(node.Children, depth + 1);
        }
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // Caret Tracking
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    /// <summary>Updates the highlighted node to match the caret line. Throttled to 100ms.</summary>
    public void UpdateCaretHighlight(int line)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastCaretUpdate).TotalMilliseconds < 100) return;
        _lastCaretUpdate = now;

        _dispatcher.InvokeAsync(() =>
        {
            if (_highlightedNode is not null)
            {
                _highlightedNode.IsHighlighted = false;
                _highlightedNode = null;
            }

            var deepest = FindDeepestContaining(RootNodes, line);
            if (deepest is not null)
            {
                deepest.IsHighlighted = true;
                _highlightedNode = deepest;
                UpdateBreadcrumb(deepest);
                if (ScrollSyncEnabled)
                    ScrollToNodeRequested?.Invoke(this, deepest);
            }
            else
            {
                BreadcrumbText = string.Empty;
            }
        }, DispatcherPriority.Background);
    }

    private static StructureNodeVm? FindDeepestContaining(IReadOnlyList<StructureNodeVm> nodes, int line)
    {
        foreach (var node in nodes)
        {
            if (node.StartLine <= 0 || node.EndLine <= 0) continue;
            if (line >= node.StartLine && line <= node.EndLine)
            {
                // Try to find a deeper match in children
                var deeper = FindDeepestContaining(node.Children, line);
                return deeper ?? node;
            }
        }

        // No range match â€” find closest by StartLine only
        StructureNodeVm? closest = null;
        foreach (var node in nodes)
        {
            if (node.StartLine > 0 && node.StartLine <= line)
            {
                if (closest is null || node.StartLine > closest.StartLine)
                    closest = node;
            }
        }
        return closest;
    }

    /// <summary>Raises NavigateRequested for the given node.</summary>
    public void OnNodeActivated(StructureNodeVm node)
        => NavigateRequested?.Invoke(this, node);

    /// <summary>
    /// Highlights the node whose Tag is the given designer element UID.
    /// Called by DocumentStructurePlugin when the XAML Designer canvas selection changes.
    /// </summary>
    public void HighlightNodeByUid(int uid)
    {
        _dispatcher.Invoke(() =>
        {
            foreach (var root in RootNodes)
                SetUidHighlightRecursive(root, uid);
        });
    }

    private static void SetUidHighlightRecursive(StructureNodeVm node, int uid)
    {
        node.IsHighlighted = node.Tag is int nodeUid && nodeUid == uid;
        foreach (var child in node.Children)
            SetUidHighlightRecursive(child, uid);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // Auto-Expand
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    private void AutoExpand(IReadOnlyList<StructureNodeVm> nodes, int depth)
    {
        foreach (var node in nodes)
        {
            if (depth < _autoExpandDepth && node.Children.Count > 0)
            {
                node.IsExpanded = true;
                AutoExpand(node.Children, depth + 1);
            }
        }
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // Helpers
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    // ── Depth limiting ────────────────────────────────────────────────────────────

    private void RebuildWithCurrentDepth()
    {
        var effective = ApplyDepthLimit(_rawNodes, s_depthValues[_maxDepthIndex]);
        var vms = effective.Select(n => new StructureNodeVm(n)).ToList();
        _allRootNodes = vms;
        RebuildTreeFromSource(vms);
        BuildFlatList(vms);
        AutoExpand(vms, 0);
        StatusText = $"{CountNodesVm(vms)} symbols";
    }

    private static IReadOnlyList<DocumentStructureNode> ApplyDepthLimit(
        IReadOnlyList<DocumentStructureNode> nodes, int maxDepth)
    {
        if (maxDepth == int.MaxValue) return nodes;
        return nodes.Select(n => TrimDepth(n, maxDepth)).ToList();
    }

    private static DocumentStructureNode TrimDepth(DocumentStructureNode node, int remaining)
    {
        var children = remaining <= 0
            ? (IReadOnlyList<DocumentStructureNode>)[]
            : node.Children.Select(c => TrimDepth(c, remaining - 1)).ToList();

        return new DocumentStructureNode
        {
            Name        = node.Name,
            Kind        = node.Kind,
            IconGlyph   = node.IconGlyph,
            Detail      = node.Detail,
            StartLine   = node.StartLine,
            StartColumn = node.StartColumn,
            EndLine     = node.EndLine,
            ByteOffset  = node.ByteOffset,
            ByteLength  = node.ByteLength,
            Tag         = node.Tag,
            Children    = children,
        };
    }

    private static int CountNodes(IReadOnlyList<DocumentStructureNode> nodes)
    {
        var count = 0;
        foreach (var n in nodes)
        {
            count++;
            count += CountNodes(n.Children);
        }
        return count;
    }

    private static int CountNodesVm(IReadOnlyList<StructureNodeVm> nodes)
    {
        var count = 0;
        foreach (var n in nodes)
        {
            count++;
            count += CountNodesVm(n.Children);
        }
        return count;
    }

    private void UpdateBreadcrumb(StructureNodeVm? node)
    {
        if (node is null) { BreadcrumbText = string.Empty; return; }
        var path = new List<string>();
        if (FindPath(_allRootNodes, node, path))
            BreadcrumbText = string.Join(" › ", path);
    }

    private static void SetVisibilityRecursive(StructureNodeVm node, System.Windows.Visibility vis)
    {
        node.Visibility = vis;
        foreach (var child in node.Children)
            SetVisibilityRecursive(child, vis);
    }

    // ── INPC ────────────────────────────────────────────────────────────────


    private new bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
