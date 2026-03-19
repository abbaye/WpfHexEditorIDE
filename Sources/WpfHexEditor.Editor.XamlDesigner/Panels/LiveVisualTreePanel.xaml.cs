// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: LiveVisualTreePanel.xaml.cs
// Author: Derek Tremblay
// Created: 2026-03-18
// Description:
//     Dockable panel showing the actual rendered WPF visual tree of the
//     design canvas, as opposed to the XAML source outline tree.
//     Bidirectional: selecting a node highlights the element on the canvas;
//     selecting an element on the canvas highlights the corresponding node.
//
// Architecture Notes:
//     Observer — wired to XamlDesignerSplitHost.SelectedElementChanged.
//     Uses LiveVisualTreeService to walk VisualTree (pure read service).
//     VS-Like Panel Pattern — 26px toolbar + TreeView + filter TextBox.
//     MEMORY.md rule: _vm is never nulled on OnUnloaded; OnLoaded re-subscribes.
//
// Theme: Global theme via XD_* and DockBackgroundBrush tokens (DynamicResource)
// ResourceDictionaries: WpfHexEditor.Shell/Themes/{Theme}/Colors.xaml
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WpfHexEditor.Editor.XamlDesigner.Services;
using WpfHexEditor.Editor.XamlDesigner.ViewModels;
using WpfHexEditor.SDK.UI;

namespace WpfHexEditor.Editor.XamlDesigner.Panels;

/// <summary>
/// Live Visual Tree dockable panel — shows the runtime WPF visual tree of the design canvas.
/// Selecting a node fires <see cref="NodeSelected"/> so the plugin can highlight
/// the corresponding element on the canvas.
/// </summary>
public partial class LiveVisualTreePanel : UserControl
{
    // ── State ─────────────────────────────────────────────────────────────────

    private readonly LiveVisualTreePanelViewModel _vm = new();
    private ToolbarOverflowManager?               _overflowManager;

    // ── Constructor ───────────────────────────────────────────────────────────

    public LiveVisualTreePanel()
    {
        InitializeComponent();
        DataContext = _vm;

        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Exposes the ViewModel for external wiring by the plugin.</summary>
    public LiveVisualTreePanelViewModel ViewModel => _vm;

    // ── Events exposed to the host ────────────────────────────────────────────

    /// <summary>
    /// Raised when the user selects a node in the tree.
    /// Carries the backing <see cref="UIElement"/> for canvas highlight.
    /// The plugin subscribes and calls <c>host.Canvas.SelectElement(e)</c>.
    /// </summary>
    public event EventHandler<UIElement?>? NodeSelected;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Safe re-subscribe on every load (panel lifecycle rule from MEMORY.md).
        BtnRefresh.Click -= OnRefreshClick;
        BtnRefresh.Click += OnRefreshClick;

        BtnCollapseAll.Click -= OnCollapseAll;
        BtnCollapseAll.Click += OnCollapseAll;

        LiveTree.SelectedItemChanged -= OnTreeSelectedItemChanged;
        LiveTree.SelectedItemChanged += OnTreeSelectedItemChanged;

        TbxFilter.TextChanged -= OnFilterChanged;
        TbxFilter.TextChanged += OnFilterChanged;

        _overflowManager ??= new ToolbarOverflowManager(
            ToolbarBorder,
            ToolbarRightPanel,
            ToolbarOverflowButton,
            null,
            new FrameworkElement[] { TbgNavigation },
            leftFixedElements: null);

        Dispatcher.InvokeAsync(
            () => _overflowManager.CaptureNaturalWidths(),
            DispatcherPriority.Loaded);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Per MEMORY.md rule: never null _vm on unload — OnLoaded re-subscribes.
        BtnRefresh.Click             -= OnRefreshClick;
        BtnCollapseAll.Click         -= OnCollapseAll;
        LiveTree.SelectedItemChanged -= OnTreeSelectedItemChanged;
        TbxFilter.TextChanged        -= OnFilterChanged;
    }

    // ── Toolbar handlers ──────────────────────────────────────────────────────

    private void OnRefreshClick(object sender, RoutedEventArgs e)
        => _vm.Refresh();

    private void OnCollapseAll(object sender, RoutedEventArgs e)
        => SetAllExpanded(_vm.RootNodes, false);

    private void OnFilterChanged(object sender, TextChangedEventArgs e)
        => _vm.FilterText = TbxFilter.Text;

    // ── Tree selection ────────────────────────────────────────────────────────

    private void OnTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not LiveTreeNode node) return;

        _vm.SelectedNode = node;

        // Fire NodeSelected so the plugin can highlight the element on the canvas.
        var uiElement = node.Source as UIElement;
        NodeSelected?.Invoke(this, uiElement);
    }

    // ── Size changes ──────────────────────────────────────────────────────────

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (sizeInfo.WidthChanged)
            _overflowManager?.Update();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static void SetAllExpanded(ObservableCollection<LiveTreeNode> nodes, bool expanded)
    {
        foreach (var n in nodes)
        {
            n.IsExpanded = expanded;
            SetAllExpanded(n.Children, expanded);
        }
    }
}

// ==========================================================
// LiveVisualTreePanelViewModel
// ==========================================================

/// <summary>
/// ViewModel for <see cref="LiveVisualTreePanel"/>.
/// Calls <see cref="LiveVisualTreeService"/> to build the live visual tree
/// and populates <see cref="RootNodes"/> for display.
/// </summary>
public sealed class LiveVisualTreePanelViewModel : INotifyPropertyChanged
{
    // ── State ─────────────────────────────────────────────────────────────────

    private UIElement?     _root;
    private LiveTreeNode?  _selectedNode;
    private string         _filterText = string.Empty;

    // ── Properties ────────────────────────────────────────────────────────────

    /// <summary>Root nodes of the live visual tree (0 or 1 entries at top level).</summary>
    public ObservableCollection<LiveTreeNode> RootNodes { get; } = new();

    /// <summary>Currently selected node; null when nothing is selected.</summary>
    public LiveTreeNode? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (ReferenceEquals(_selectedNode, value)) return;
            _selectedNode = value;
            OnPropertyChanged();
            SelectedNodeChanged?.Invoke(this, value);
        }
    }

    /// <summary>Text filter applied to DisplayLabel of tree nodes.</summary>
    public string FilterText
    {
        get => _filterText;
        set
        {
            if (_filterText == value) return;
            _filterText = value;
            OnPropertyChanged();
            ApplyFilter(RootNodes);
        }
    }

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fired when the selected node changes.</summary>
    public event EventHandler<LiveTreeNode?>? SelectedNodeChanged;

    // ── INPC ──────────────────────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Rebuilds the visual tree from the provided root element.
    /// Pass null to clear the panel when no canvas root is available.
    /// </summary>
    public void Refresh(UIElement? root)
    {
        _root = root;
        Rebuild();
    }

    /// <summary>Re-reads the visual tree from the last-known root element.</summary>
    public void Refresh() => Rebuild();

    /// <summary>
    /// Programmatically selects the node whose backing source matches
    /// <paramref name="element"/> — used for canvas → tree synchronisation.
    /// </summary>
    public void SelectNodeByElement(UIElement? element)
    {
        if (element is null || RootNodes.Count == 0) return;

        var found = FindNodeBySource(RootNodes[0], element);
        if (found is not null)
            SelectedNode = found;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void Rebuild()
    {
        RootNodes.Clear();
        _selectedNode = null;
        OnPropertyChanged(nameof(SelectedNode));

        if (_root is null) return;

        var service  = new LiveVisualTreeService();
        var rootNode = service.BuildTree(_root);

        if (rootNode is not null)
        {
            RootNodes.Add(rootNode);
            // Auto-expand the root node for discoverability.
            rootNode.IsExpanded = true;
        }
    }

    private bool ApplyFilter(ObservableCollection<LiveTreeNode> nodes)
    {
        var filter   = _filterText.Trim();
        bool anyMatch = false;

        foreach (var node in nodes)
        {
            bool childMatch = ApplyFilter(node.Children);
            bool selfMatch  = string.IsNullOrEmpty(filter)
                              || node.DisplayLabel.Contains(filter, StringComparison.OrdinalIgnoreCase);

            // A node is visible when it matches or any descendant matches.
            bool visible = selfMatch || childMatch;
            anyMatch    |= visible;

            // Expand nodes that contain a filter match so results are visible.
            if (childMatch && !string.IsNullOrEmpty(filter))
                node.IsExpanded = true;
        }

        return anyMatch;
    }

    private static LiveTreeNode? FindNodeBySource(LiveTreeNode node, UIElement target)
    {
        if (ReferenceEquals(node.Source, target)) return node;

        foreach (var child in node.Children)
        {
            var found = FindNodeBySource(child, target);
            if (found is not null) return found;
        }

        return null;
    }
}
