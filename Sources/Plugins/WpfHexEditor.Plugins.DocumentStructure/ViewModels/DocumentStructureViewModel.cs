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

namespace WpfHexEditor.Plugins.DocumentStructure.ViewModels;

public enum SortMode { SourceOrder, Alphabetical, ByKind }

/// <summary>
/// ViewModel for the Document Structure panel.
/// </summary>
public sealed class DocumentStructureViewModel : INotifyPropertyChanged
{
    private readonly DocumentStructureProviderResolver _resolver;
    private readonly Dispatcher _dispatcher;
    private CancellationTokenSource? _refreshCts;
    private DateTime _lastCaretUpdate;

    private string _filterText = string.Empty;
    private SortMode _currentSort = SortMode.SourceOrder;
    private bool _isTreeMode = true;
    private bool _isLoading;
    private string _statusText = "No document";
    private string? _activeProviderName;
    private int _autoExpandDepth = 2;
    private StructureNodeVm? _highlightedNode;

    // ── Backing data (unfiltered) ─────────────────────────────────────────
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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SortModeIndex)));
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

    /// <summary>Raised when a node is activated (clicked) and the editor should navigate to it.</summary>
    public event EventHandler<StructureNodeVm>? NavigateRequested;

    public DocumentStructureViewModel(DocumentStructureProviderResolver resolver)
    {
        _resolver = resolver;
        _dispatcher = Dispatcher.CurrentDispatcher;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Debounced Refresh
    // ══════════════════════════════════════════════════════════════════════════

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

        var provider = _resolver.Resolve(filePath, documentType, language);
        if (provider is null)
        {
            _dispatcher.Invoke(() => ClearUI("No structure available"));
            return;
        }

        _dispatcher.Invoke(() => IsLoading = true);

        try
        {
            var result = await provider.GetStructureAsync(filePath, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();

            if (result is null || result.Nodes.Count == 0)
            {
                _dispatcher.Invoke(() => ClearUI("Empty structure"));
                return;
            }

            // Build VMs on background thread
            var vms = result.Nodes.Select(n => new StructureNodeVm(n)).ToList();
            var totalCount = CountNodes(result.Nodes);

            _dispatcher.Invoke(() =>
            {
                _allRootNodes = vms;
                ActiveProviderName = provider.DisplayName;
                StatusText = $"{totalCount} symbols";
                IsLoading = false;

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
        ActiveProviderName = null;
        IsLoading = false;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Filtering
    // ══════════════════════════════════════════════════════════════════════════

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

    // ══════════════════════════════════════════════════════════════════════════
    // Sorting
    // ══════════════════════════════════════════════════════════════════════════

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

    // ══════════════════════════════════════════════════════════════════════════
    // Tree / Flat
    // ══════════════════════════════════════════════════════════════════════════

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

    // ══════════════════════════════════════════════════════════════════════════
    // Caret Tracking
    // ══════════════════════════════════════════════════════════════════════════

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

        // No range match — find closest by StartLine only
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

    // ══════════════════════════════════════════════════════════════════════════
    // Auto-Expand
    // ══════════════════════════════════════════════════════════════════════════

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

    // ══════════════════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════════════════

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

    private static void SetVisibilityRecursive(StructureNodeVm node, System.Windows.Visibility vis)
    {
        node.Visibility = vis;
        foreach (var child in node.Children)
            SetVisibilityRecursive(child, vis);
    }

    // ── INPC ────────────────────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}
