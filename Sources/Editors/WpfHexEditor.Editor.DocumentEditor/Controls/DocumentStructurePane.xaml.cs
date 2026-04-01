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

    // ── Tree building ────────────────────────────────────────────────────────

    private void RebuildTree()
    {
        if (_model is null) return;

        var alertMap = BuildAlertMap();
        _allNodes = new ObservableCollection<DocumentBlockNode>(
            _model.Blocks.Select(b => BuildNode(b, alertMap)));

        ApplyFilter();
        UpdateCountLabel();
    }

    private void ApplyFilter()
    {
        if (string.IsNullOrWhiteSpace(_filterText))
        {
            PART_Tree.ItemsSource = _allNodes;
            return;
        }

        var lower = _filterText.ToLowerInvariant();
        var filtered = _allNodes
            .Where(n => MatchesFilter(n, lower))
            .ToList();

        PART_Tree.ItemsSource = filtered;
    }

    private static bool MatchesFilter(DocumentBlockNode node, string filter) =>
        node.KindLabel.ToLowerInvariant().Contains(filter) ||
        node.Preview.ToLowerInvariant().Contains(filter)   ||
        node.OffsetText.ToLowerInvariant().Contains(filter)||
        node.Children.Any(c => MatchesFilter(c, filter));

    private void UpdateCountLabel()
    {
        int total = CountNodes(_allNodes);
        PART_CountLabel.Text = $"({total})";
    }

    private static int CountNodes(IEnumerable<DocumentBlockNode> nodes) =>
        nodes.Sum(n => 1 + CountNodes(n.Children));

    private static DocumentBlockNode BuildNode(
        DocumentBlock block, Dictionary<DocumentBlock, ForensicAlert> alertMap)
    {
        var node = new DocumentBlockNode(block)
        {
            Alert = alertMap.TryGetValue(block, out var a) ? a : null
        };
        foreach (var child in block.Children)
            node.Children.Add(BuildNode(child, alertMap));
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

    // ── Expand / Collapse all ─────────────────────────────────────────────────

    private void OnExpandAllClicked(object sender, RoutedEventArgs e)  => SetExpanded(PART_Tree, true);
    private void OnCollapseAllClicked(object sender, RoutedEventArgs e) => SetExpanded(PART_Tree, false);

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

    public string KindLabel   => Block.Kind.ToUpperInvariant()[..Math.Min(3, Block.Kind.Length)];
    public string Preview     => Block.Text.Length > 40 ? Block.Text[..40] + "…" : Block.Text;
    public string OffsetText  => $"0x{Block.RawOffset:X}";
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
