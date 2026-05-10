// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Controls/DocumentStructurePane.xaml.cs
// Description:
//     TreeView pane showing the hierarchical DocumentBlock structure.
//     v2: search/filter, Expand/Collapse all, context menu (navigate,
//         jump-to-hex, copy text/offset), block count in header.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using WpfHexEditor.Editor.DocumentEditor.Core.Forensic;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Editor.DocumentEditor.Controls;

/// <summary>
/// Displays the document block hierarchy with forensic badges, search,
/// and context-menu navigation actions.
/// </summary>
public partial class DocumentStructurePane : UserControl
{
    // ── Fields ────────────────────────────────────────────────────────────────

    private DocumentModel? _model;
    private ObservableCollection<DocumentBlockNode> _allNodes = [];
    private string _filterText = string.Empty;
    private bool   _showRuns   = false; // hide run leaves by default — they duplicate paragraph text
    private string _kindFilter = "all"; // "all" | "heading" | "list-item" | "table" | "image"
    private bool   _outlineMode;        // true = headings-only, hierarchically grouped by level
    private bool   _suppressNavigateEvent;       // re-entrancy guard for SyncToBlock

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Raised when the user selects or double-clicks a node.</summary>
    public event EventHandler<DocumentBlock>? BlockNavigated;

    /// <summary>Raised when the user clicks "Jump to hex offset" in context menu.</summary>
    public event EventHandler<DocumentBlock>? JumpHexRequested;

    // ── Constructor ──────────────────────────────────────────────────────────

    public DocumentStructurePane()
    {
        InitializeComponent();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public void BindModel(DocumentModel model)
    {
        if (_model is not null)
        {
            _model.BlocksChanged         -= OnBlocksChanged;
            _model.ForensicAlertsChanged -= OnAlertsChanged;
        }

        _model = model;
        _model.BlocksChanged         += OnBlocksChanged;
        _model.ForensicAlertsChanged += OnAlertsChanged;

        RebuildTree();
    }

    /// <summary>
    /// Selects, expands ancestors of, and scrolls into view the tree node whose
    /// underlying <see cref="DocumentBlock"/> matches <paramref name="block"/>.
    /// Called from the host when the renderer's caret moves into a new block —
    /// keeps the structure tree in sync with the cursor like VS's outline pane.
    /// </summary>
    public void SyncToBlock(DocumentBlock? block)
    {
        if (block is null) return;
        var node = FindNode(_allNodes, block);
        if (node is null) return;

        // Expand chain: walk up by re-finding parents in the visual tree, since
        // DocumentBlockNode doesn't carry a parent pointer. Re-running the search
        // top-down with a path-collecting visitor keeps SyncToBlock allocation-light
        // and stays correct under filtered ItemsSources.
        var path = new List<DocumentBlockNode>();
        if (CollectPath(_allNodes, block, path))
        {
            for (int i = 0; i < path.Count - 1; i++)
            {
                if (PART_Tree.ItemContainerGenerator.ContainerFromItem(path[i]) is TreeViewItem tvi)
                    tvi.IsExpanded = true;
            }
            // Defer container realization for the leaf — the item generator
            // populates lazily on expansion, so the container we need may not
            // exist yet on the first dispatcher pass.
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () =>
            {
                if (PART_Tree.ItemContainerGenerator.ContainerFromItem(node) is TreeViewItem leaf)
                {
                    _suppressNavigateEvent = true;
                    leaf.IsSelected = true;
                    _suppressNavigateEvent = false;
                    leaf.BringIntoView();
                }
            });
        }
    }

    private static DocumentBlockNode? FindNode(IEnumerable<DocumentBlockNode> nodes, DocumentBlock target)
    {
        foreach (var n in nodes)
        {
            if (ReferenceEquals(n.Block, target)) return n;
            var hit = FindNode(n.Children, target);
            if (hit is not null) return hit;
        }
        return null;
    }

    private static bool CollectPath(IEnumerable<DocumentBlockNode> nodes,
        DocumentBlock target, List<DocumentBlockNode> path)
    {
        foreach (var n in nodes)
        {
            path.Add(n);
            if (ReferenceEquals(n.Block, target)) return true;
            if (CollectPath(n.Children, target, path)) return true;
            path.RemoveAt(path.Count - 1);
        }
        return false;
    }

    // ── Tree building ────────────────────────────────────────────────────────

    private void RebuildTree()
    {
        if (_model is null) return;

        var alertMap = BuildAlertMap();
        _allNodes = _outlineMode
            ? BuildOutlineNodes(_model.Blocks, alertMap)
            : new ObservableCollection<DocumentBlockNode>(
                _model.Blocks
                      .Where(b => _showRuns || b.Kind != "run")
                      .Select(b => BuildNode(b, alertMap, _showRuns)));

        ApplyFilter();
        UpdateCountLabel();
    }

    /// <summary>
    /// Builds a heading-only tree where each heading owns the deeper-level headings
    /// that follow it until a same-or-shallower heading breaks the chain. Mirrors
    /// the document outline panes in VS Code / Word.
    /// </summary>
    private static ObservableCollection<DocumentBlockNode> BuildOutlineNodes(
        IEnumerable<DocumentBlock> blocks, Dictionary<DocumentBlock, ForensicAlert> alertMap)
    {
        var roots = new ObservableCollection<DocumentBlockNode>();
        var stack = new Stack<(int Level, DocumentBlockNode Node)>();

        foreach (var block in blocks.SelectMany(Flatten).Where(b => b.Kind == "heading"))
        {
            int level = ReadHeadingLevel(block);
            var node  = new DocumentBlockNode(block)
            {
                Alert = alertMap.TryGetValue(block, out var a) ? a : null
            };

            // Pop until we find a stricter ancestor.
            while (stack.Count > 0 && stack.Peek().Level >= level)
                stack.Pop();

            if (stack.Count == 0)
                roots.Add(node);
            else
                stack.Peek().Node.Children.Add(node);

            stack.Push((level, node));
        }

        return roots;
    }

    private static IEnumerable<DocumentBlock> Flatten(DocumentBlock b)
    {
        yield return b;
        foreach (var c in b.Children)
            foreach (var d in Flatten(c)) yield return d;
    }

    private static int ReadHeadingLevel(DocumentBlock b) =>
        b.Attributes.TryGetValue("level", out var lv) &&
        lv is string s && int.TryParse(s, out int n) ? n : 1;

    private void ApplyFilter()
    {
        // Guard: ComboBox<KindFilter SelectedIndex="0"> fires SelectionChanged
        // during InitializeComponent() — before PART_Tree's x:Name field is
        // assigned. The next RebuildTree() (triggered from BindModel) will
        // re-run ApplyFilter once everything is realized.
        if (PART_Tree is null) return;

        bool hasText = !string.IsNullOrWhiteSpace(_filterText);
        bool hasKind = _kindFilter != "all";

        if (!hasText && !hasKind)
        {
            PART_Tree.ItemsSource = _allNodes;
            UpdateCountLabel();
            return;
        }

        var lower = hasText ? _filterText.ToLowerInvariant() : string.Empty;
        var filtered = _allNodes
            .Where(n => Matches(n, lower, _kindFilter))
            .ToList();

        PART_Tree.ItemsSource = filtered;
        UpdateCountLabel(filtered);
    }

    /// <summary>Tree-aware filter: a node passes if it matches OR any descendant matches.</summary>
    private static bool Matches(DocumentBlockNode node, string text, string kind)
    {
        bool kindOk = kind == "all" || node.Block.Kind == kind;
        bool textOk = string.IsNullOrEmpty(text) ||
                      node.KindLabel.ToLowerInvariant().Contains(text) ||
                      node.Preview.ToLowerInvariant().Contains(text)   ||
                      node.OffsetText.ToLowerInvariant().Contains(text);
        if (kindOk && textOk) return true;
        return node.Children.Any(c => Matches(c, text, kind));
    }

    private void UpdateCountLabel(IEnumerable<DocumentBlockNode>? filtered = null)
    {
        if (PART_CountLabel is null) return;
        int total = CountNodes(_allNodes);
        if (filtered is null)
        {
            PART_CountLabel.Text = $"({total})";
            return;
        }
        int shown = CountNodes(filtered);
        PART_CountLabel.Text = shown == total ? $"({total})" : $"({shown}/{total})";
    }

    private static int CountNodes(IEnumerable<DocumentBlockNode> nodes) =>
        nodes.Sum(n => 1 + CountNodes(n.Children));

    private static DocumentBlockNode BuildNode(
        DocumentBlock block, Dictionary<DocumentBlock, ForensicAlert> alertMap, bool showRuns)
    {
        var node = new DocumentBlockNode(block)
        {
            Alert = alertMap.TryGetValue(block, out var a) ? a : null
        };
        foreach (var child in block.Children)
        {
            // Run leaves duplicate the parent paragraph's flat text — hiding them
            // by default cuts the tree size 2-3× without losing information.
            if (!showRuns && child.Kind == "run") continue;
            node.Children.Add(BuildNode(child, alertMap, showRuns));
        }
        return node;
    }

    private Dictionary<DocumentBlock, ForensicAlert> BuildAlertMap()
    {
        var map = new Dictionary<DocumentBlock, ForensicAlert>();
        if (_model is null) return map;

        foreach (var alert in _model.ForensicAlerts)
        {
            var block = alert.Offset.HasValue ? _model.BinaryMap.BlockAt(alert.Offset.Value) : null;
            if (block is not null && !map.ContainsKey(block))
                map[block] = alert;
        }
        return map;
    }

    // ── Tree events ──────────────────────────────────────────────────────────

    private void OnTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        // SyncToBlock toggles the selection programmatically when the caret
        // moves — we must not re-fire BlockNavigated, or the renderer will
        // fight the user every time they move the cursor.
        if (_suppressNavigateEvent) return;
        if (e.NewValue is DocumentBlockNode node)
            BlockNavigated?.Invoke(this, node.Block);
    }

    // ── Search ───────────────────────────────────────────────────────────────

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        _filterText = PART_SearchBox.Text;
        ApplyFilter();
    }

    private void OnClearSearchClicked(object sender, RoutedEventArgs e)
    {
        PART_SearchBox.Clear();
    }

    private void OnKindFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PART_KindFilter?.SelectedItem is ComboBoxItem item &&
            item.Tag is string tag)
        {
            _kindFilter = tag;
            ApplyFilter();
        }
    }

    // ── Expand / Collapse all ─────────────────────────────────────────────────

    private void OnExpandAllClicked(object sender, RoutedEventArgs e)  => SetExpanded(PART_Tree, true);
    private void OnCollapseAllClicked(object sender, RoutedEventArgs e) => SetExpanded(PART_Tree, false);

    private void OnShowRunsToggled(object sender, RoutedEventArgs e)
    {
        _showRuns = PART_ShowRunsBtn.IsChecked == true;
        RebuildTree();
    }

    private void OnOutlineModeToggled(object sender, RoutedEventArgs e)
    {
        _outlineMode = PART_OutlineBtn.IsChecked == true;
        // Outline mode is heading-only; the "show runs" and "kind filter" controls
        // become noise in that view. Disable them visually so the user understands
        // why their settings have no effect.
        PART_ShowRunsBtn.IsEnabled = !_outlineMode;
        PART_KindFilter.IsEnabled  = !_outlineMode;
        RebuildTree();
    }

    private static void SetExpanded(ItemsControl parent, bool expand)
    {
        foreach (var item in parent.Items)
        {
            var container = parent.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
            if (container is null) continue;
            container.IsExpanded = expand;
            SetExpanded(container, expand);
        }
    }

    // ── Context menu ─────────────────────────────────────────────────────────

    private DocumentBlockNode? GetSelectedNode() =>
        PART_Tree.SelectedItem as DocumentBlockNode;

    private void OnMenuNavigateClicked(object sender, RoutedEventArgs e)
    {
        if (GetSelectedNode() is { } node)
            BlockNavigated?.Invoke(this, node.Block);
    }

    private void OnMenuJumpHexClicked(object sender, RoutedEventArgs e)
    {
        if (GetSelectedNode() is { } node)
            JumpHexRequested?.Invoke(this, node.Block);
    }

    private void OnMenuCopyTextClicked(object sender, RoutedEventArgs e)
    {
        if (GetSelectedNode() is { } node && !string.IsNullOrEmpty(node.Block.Text))
            System.Windows.Clipboard.SetText(node.Block.Text);
    }

    private void OnMenuCopyOffsetClicked(object sender, RoutedEventArgs e)
    {
        if (GetSelectedNode() is { } node)
            System.Windows.Clipboard.SetText($"0x{node.Block.RawOffset:X8}");
    }

    // ── Model events ─────────────────────────────────────────────────────────

    private void OnBlocksChanged(object? sender, EventArgs e)  => Dispatcher.InvokeAsync(RebuildTree);
    private void OnAlertsChanged(object? sender, EventArgs e)  => Dispatcher.InvokeAsync(RebuildTree);
}

// ── Node view model ───────────────────────────────────────────────────────────

/// <summary>TreeView node wrapping a <see cref="DocumentBlock"/> with forensic overlay.</summary>
public sealed class DocumentBlockNode : INotifyPropertyChanged
{
    private ForensicAlert? _alert;

    public DocumentBlockNode(DocumentBlock block) => Block = block;

    public DocumentBlock Block { get; }

    public ForensicAlert? Alert
    {
        get => _alert;
        set
        {
            _alert = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasAlert));
            OnPropertyChanged(nameof(AlertMessage));
            OnPropertyChanged(nameof(AlertBrushKey));
        }
    }

    public ObservableCollection<DocumentBlockNode> Children { get; } = [];

    /// <summary>
    /// Highest severity present on this node OR any descendant. Lets a parent
    /// signal that some child carries an alert even when the parent itself is
    /// clean — e.g. an H1 with one buried error paragraph still shows the dot.
    /// </summary>
    public ForensicSeverity? AggregateSeverity
    {
        get
        {
            ForensicSeverity? max = _alert?.Severity;
            foreach (var c in Children)
            {
                var ch = c.AggregateSeverity;
                if (ch is null) continue;
                if (max is null || ch > max) max = ch;
            }
            return max;
        }
    }

    public bool   HasAggregateAlert    => AggregateSeverity is not null;
    public string AggregateAlertBrushKey => AggregateSeverity switch
    {
        ForensicSeverity.Error   => "DE_ForensicErrorBrush",
        ForensicSeverity.Warning => "DE_ForensicWarnBrush",
        ForensicSeverity.Info    => "DE_ForensicOkBrush",
        _                        => "DE_ForensicOkBrush",
    };

    public string KindLabel
    {
        get
        {
            // Heading paragraphs surface their OOXML level (H1..H9) so the user
            // can distinguish hierarchy at a glance instead of reading them all
            // as "HEA". Falls back to the generic 3-letter prefix for everything
            // else (PAR, LIS, TAB, IMG, …).
            if (Block.Kind == "heading"
                && Block.Attributes.TryGetValue("level", out var lvObj)
                && lvObj is string lv && !string.IsNullOrEmpty(lv))
            {
                return $"H{lv}";
            }
            return Block.Kind.ToUpperInvariant()[..Math.Min(3, Block.Kind.Length)];
        }
    }
    public string Preview     => BuildPreview(Block.Text);
    /// <summary>
    /// 8-digit zero-padded hex so all offsets render with the same character width
    /// and stay visually aligned across siblings (Consolas in the chip).
    /// </summary>
    public string OffsetText  => $"0x{Block.RawOffset:X8}";

    /// <summary>
    /// Segoe MDL2 glyph picked per <see cref="Block"/>.Kind for the chip prefix.
    /// Codepoints expressed via \uXXXX escapes to keep the source ASCII-clean.
    /// </summary>
    public string KindGlyph => Block.Kind switch
    {
        "heading"        => "\uE8FD", // Header
        "paragraph"      => "\uE7C3", // Page
        "list-item"      => "\uEA37", // BulletedList
        "table"          => "\uF532", // GridView
        "table-row"      => "\uE9D9", // RowGrid
        "image"          => "\uEB9F", // Photo2
        "hyperlink"      => "\uE71B", // Link
        "page-break"     => "\uE7AD", // NewPage
        "structured-tag" => "\uE8EC", // Tag
        "run"            => "\uE8C1", // Font
        _                => "\uE8A5", // PageHash
    };

    /// <summary>Resource key resolved by ResourceKeyToBrushConverter to color the chip background.</summary>
    public string KindBrushKey => Block.Kind switch
    {
        "heading"        => "DE_StructureChipHeadingBrush",
        "list-item"      => "DE_StructureChipListBrush",
        "table"          => "DE_StructureChipTableBrush",
        "table-row"      => "DE_StructureChipTableBrush",
        "image"          => "DE_StructureChipImageBrush",
        "hyperlink"      => "DE_StructureChipLinkBrush",
        "structured-tag" => "DE_StructureChipTagBrush",
        _                => "DE_StructureChipDefaultBrush",
    };

    /// <summary>
    /// Trims and collapses runs of whitespace (incl. tabs) into single spaces so
    /// LibreOffice-style text alignment via tabs/multi-spaces (common in CV docs)
    /// doesn't render as fragmented previews. Truncates to 60 chars with ellipsis.
    /// </summary>
    private static string BuildPreview(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        var collapsed = WhitespaceRun.Replace(raw.Trim(), " ");
        return collapsed.Length > 60 ? collapsed[..60] + "…" : collapsed;
    }

    private static readonly Regex WhitespaceRun = new(@"\s+", RegexOptions.Compiled);
    public bool   HasAlert    => _alert is not null;
    public string AlertMessage => _alert?.Description ?? string.Empty;

    public string AlertBrushKey => _alert?.Severity switch
    {
        ForensicSeverity.Error   => "DE_ForensicErrorBrush",
        ForensicSeverity.Warning => "DE_ForensicWarnBrush",
        _                        => "DE_ForensicOkBrush"
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Resolves a resource-key string to the matching Brush in Application.Current.Resources.</summary>
[ValueConversion(typeof(string), typeof(Brush))]
internal sealed class ResourceKeyToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string key && Application.Current.Resources.Contains(key))
            return Application.Current.Resources[key];
        return Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
