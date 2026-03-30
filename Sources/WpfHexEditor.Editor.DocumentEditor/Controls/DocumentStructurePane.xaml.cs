// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Controls/DocumentStructurePane.xaml.cs
// Description:
//     TreeView pane showing the hierarchical DocumentBlock structure.
//     Each node shows kind, offset, and a forensic alert badge.
//     Double-click navigates the text and hex panes to the block.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfHexEditor.Editor.DocumentEditor.Core.Forensic;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Editor.DocumentEditor.Controls;

/// <summary>
/// Displays the document block hierarchy with forensic badges.
/// </summary>
public partial class DocumentStructurePane : UserControl
{
    private DocumentModel? _model;

    /// <summary>Raised when the user double-clicks a node.</summary>
    public event EventHandler<DocumentBlock>? BlockNavigated;

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
        var nodes = _model.Blocks
            .Select(b => BuildNode(b, alertMap))
            .ToList();

        PART_Tree.ItemsSource = nodes;
    }

    private static DocumentBlockNode BuildNode(
        DocumentBlock                            block,
        Dictionary<DocumentBlock, ForensicAlert> alertMap)
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
            var block = _model.BinaryMap.BlockAt(alert.Offset);
            if (block is not null && !map.ContainsKey(block))
                map[block] = alert;
        }
        return map;
    }

    // ── Events ────────────────────────────────────────────────────────────────

    private void OnTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is DocumentBlockNode node)
            BlockNavigated?.Invoke(this, node.Block);
    }

    private void OnBlocksChanged(object? sender, EventArgs e) =>
        Dispatcher.InvokeAsync(RebuildTree);

    private void OnAlertsChanged(object? sender, EventArgs e) =>
        Dispatcher.InvokeAsync(RebuildTree);
}

// ── Node view model ───────────────────────────────────────────────────────────

/// <summary>
/// TreeView node wrapping a <see cref="DocumentBlock"/> with forensic overlay.
/// </summary>
public sealed class DocumentBlockNode : INotifyPropertyChanged
{
    private ForensicAlert? _alert;

    public DocumentBlockNode(DocumentBlock block)
    {
        Block = block;
    }

    public DocumentBlock Block { get; }

    public ForensicAlert? Alert
    {
        get => _alert;
        set { _alert = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasAlert)); OnPropertyChanged(nameof(AlertMessage)); OnPropertyChanged(nameof(AlertBrushKey)); }
    }

    public ObservableCollection<DocumentBlockNode> Children { get; } = [];

    public string KindLabel   => Block.Kind.ToUpperInvariant()[..Math.Min(3, Block.Kind.Length)];
    public string Preview     => Block.Text.Length > 40 ? Block.Text[..40] + "…" : Block.Text;
    public string OffsetText  => $"0x{Block.RawOffset:X}";
    public bool   HasAlert    => _alert is not null;
    public string AlertMessage => _alert?.Message ?? string.Empty;

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
