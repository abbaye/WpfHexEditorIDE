// ==========================================================
// Project: WpfHexEditor.Plugins.XamlDesigner
//          2026-03-22 — Moved to plugin project (WpfHexEditor.Plugins.XamlDesigner.Panels).
// File: LiveVisualTreePanel.xaml.cs
// Author: Derek Tremblay
// Created: 2026-03-18
// Updated: 2026-03-19 — Phase 1: Auto-populate fix (RefreshRequested on IsVisibleChanged +
//                        InitializeAsync seed in plugin). Phase 2: type-specific icons.
//                        Phase 3: rich hover tooltips. Phase 4: NodeHovered → canvas overlay.
//                        Phase 5: AutoRefresh toggle. Phase 6: Expand-to-selection.
//                        Phase 7: Context menu (copy/navigate/expand). Phase 8: Breadcrumb bar.
//                        Phase 9: Node count status bar. Phase 10: Logical tree toggle.
//          2026-03-19 — Toolbar improvements: Expand All (A), Search bar / Ctrl+F / F3 (B),
//                        Set as Root / inspect subtree (C), Pick Element toggle (D),
//                        Track Selection (E), Filter presets + clear X (F),
//                        Show/Hide Dimensions toggle (G), Copy XAML Snippet (H).
// Description:
//     Dockable panel showing the actual rendered WPF visual tree of the
//     design canvas, as opposed to the XAML source outline tree.
//     Bidirectional: selecting a node highlights the element on the canvas;
//     selecting an element on the canvas highlights the corresponding node.
//
// Architecture Notes:
//     Observer — wired to XamlDesignerSplitHost.SelectedElementChanged.
//     Uses LiveVisualTreeService to walk VisualTree or LogicalTree (pure read service).
//     VS-Like Panel Pattern — 26px toolbar + breadcrumb bar + TreeView + status bar.
//     MEMORY.md rule: _vm is never nulled on OnUnloaded; OnLoaded re-subscribes.
//
// Theme: Global theme via XD_* and DockBackgroundBrush tokens (DynamicResource)
// ResourceDictionaries: WpfHexEditor.Shell/Themes/{Theme}/Colors.xaml
// ==========================================================

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WpfHexEditor.Editor.XamlDesigner.Services;
using WpfHexEditor.Editor.XamlDesigner.Models;
using WpfHexEditor.SDK.UI;

namespace WpfHexEditor.Plugins.XamlDesigner.Panels;

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

    /// <summary>
    /// Raised when the panel becomes visible (tab shown, auto-hide expanded, etc.).
    /// The plugin subscribes and re-seeds the tree from the current canvas DesignRoot
    /// so the panel is never empty when revealed without a document switch.
    /// </summary>
    public event EventHandler? RefreshRequested;

    /// <summary>
    /// Raised when the user hovers a tree node.
    /// Carries the backing <see cref="UIElement"/> so the plugin can draw a
    /// non-selecting hover highlight on the canvas.
    /// </summary>
    public event EventHandler<UIElement?>? NodeHovered;

    /// <summary>
    /// Raised when "Navigate to XAML" is chosen from the context menu.
    /// Carries the element's x:Name (may be null) for the outline panel to sync.
    /// </summary>
    public event EventHandler<string?>? NavigateToXamlRequested;

    /// <summary>
    /// Raised when the user toggles the Pick Element mode button.
    /// The plugin subscribes: true = every canvas click auto-scrolls the tree, false = normal.
    /// </summary>
    public event EventHandler<bool>? PickModeChanged;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Safe re-subscribe on every load (panel lifecycle rule from MEMORY.md).
        BtnRefresh.Click -= OnRefreshClick;
        BtnRefresh.Click += OnRefreshClick;

        BtnCollapseAll.Click -= OnCollapseAll;
        BtnCollapseAll.Click += OnCollapseAll;

        BtnExpandToSelected.Click -= OnExpandToSelectedClick;
        BtnExpandToSelected.Click += OnExpandToSelectedClick;

        // A: Expand All
        BtnExpandAll.Click -= OnExpandAll;
        BtnExpandAll.Click += OnExpandAll;

        // B: Search bar toggle + navigation
        BtnSearch.Click           -= OnToggleSearch;
        BtnSearch.Click           += OnToggleSearch;
        BtnSearchPrev.Click       -= OnSearchPrev;
        BtnSearchPrev.Click       += OnSearchPrev;
        BtnSearchNext.Click       -= OnSearchNext;
        BtnSearchNext.Click       += OnSearchNext;
        BtnSearchClose.Click      -= OnSearchClose;
        BtnSearchClose.Click      += OnSearchClose;
        TbxSearch.TextChanged     -= OnSearchTextChanged;
        TbxSearch.TextChanged     += OnSearchTextChanged;
        TbxSearch.PreviewKeyDown  -= OnSearchKeyDown;
        TbxSearch.PreviewKeyDown  += OnSearchKeyDown;

        // C: Set as Root / Reset Root
        BtnSetAsRoot.Click  -= OnSetAsRoot;
        BtnSetAsRoot.Click  += OnSetAsRoot;
        BtnResetRoot.Click  -= OnResetRoot;
        BtnResetRoot.Click  += OnResetRoot;

        // F: Filter preset + clear
        BtnFilterPreset.Click -= OnFilterPresetClick;
        BtnFilterPreset.Click += OnFilterPresetClick;
        BtnClearFilter.Click  -= OnClearFilter;
        BtnClearFilter.Click  += OnClearFilter;

        LiveTree.SelectedItemChanged -= OnTreeSelectedItemChanged;
        LiveTree.SelectedItemChanged += OnTreeSelectedItemChanged;

        LiveTree.MouseMove           -= OnTreeMouseMove;
        LiveTree.MouseMove           += OnTreeMouseMove;

        // B: Ctrl+F / F3 keyboard shortcut on the tree control
        LiveTree.PreviewKeyDown      -= OnTreePreviewKeyDown;
        LiveTree.PreviewKeyDown      += OnTreePreviewKeyDown;

        TbxFilter.TextChanged -= OnFilterChanged;
        TbxFilter.TextChanged += OnFilterChanged;

        // Re-seed whenever the panel becomes visible (e.g. auto-hide expanded, tab shown).
        IsVisibleChanged -= OnIsVisibleChanged;
        IsVisibleChanged += OnIsVisibleChanged;

        // VM events
        _vm.SearchCurrentMatchChanged -= OnSearchCurrentMatchChanged;
        _vm.SearchCurrentMatchChanged += OnSearchCurrentMatchChanged;
        _vm.TrackSelectionRequested   -= OnTrackSelectionRequested;
        _vm.TrackSelectionRequested   += OnTrackSelectionRequested;
        _vm.PickModeChanged           -= OnPickModeChangedFromVm;
        _vm.PickModeChanged           += OnPickModeChangedFromVm;

        _overflowManager ??= new ToolbarOverflowManager(
            ToolbarBorder,
            ToolbarRightPanel,
            ToolbarOverflowButton,
            null,
            new FrameworkElement[] { TbgNavigation },
            leftFixedElements: null);

        // Background priority gives WPF extra layout passes so TbgNavigation.ActualWidth
        // is correctly measured before CaptureNaturalWidths reads it.
        Dispatcher.InvokeAsync(
            () =>
            {
                TbgNavigation.UpdateLayout();
                _overflowManager.CaptureNaturalWidths();
            },
            DispatcherPriority.Background);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Per MEMORY.md rule: never null _vm on unload — OnLoaded re-subscribes.
        BtnRefresh.Click             -= OnRefreshClick;
        BtnCollapseAll.Click         -= OnCollapseAll;
        BtnExpandToSelected.Click    -= OnExpandToSelectedClick;
        BtnExpandAll.Click           -= OnExpandAll;
        BtnSearch.Click              -= OnToggleSearch;
        BtnSearchPrev.Click          -= OnSearchPrev;
        BtnSearchNext.Click          -= OnSearchNext;
        BtnSearchClose.Click         -= OnSearchClose;
        TbxSearch.TextChanged        -= OnSearchTextChanged;
        TbxSearch.PreviewKeyDown     -= OnSearchKeyDown;
        BtnSetAsRoot.Click           -= OnSetAsRoot;
        BtnResetRoot.Click           -= OnResetRoot;
        BtnFilterPreset.Click        -= OnFilterPresetClick;
        BtnClearFilter.Click         -= OnClearFilter;
        LiveTree.SelectedItemChanged -= OnTreeSelectedItemChanged;
        LiveTree.MouseMove           -= OnTreeMouseMove;
        LiveTree.PreviewKeyDown      -= OnTreePreviewKeyDown;
        TbxFilter.TextChanged        -= OnFilterChanged;
        IsVisibleChanged             -= OnIsVisibleChanged;
        _vm.SearchCurrentMatchChanged -= OnSearchCurrentMatchChanged;
        _vm.TrackSelectionRequested   -= OnTrackSelectionRequested;
        _vm.PickModeChanged           -= OnPickModeChangedFromVm;
    }

    // ── Toolbar handlers ──────────────────────────────────────────────────────

    private void OnRefreshClick(object sender, RoutedEventArgs e)
        => _vm.Refresh();

    private void OnCollapseAll(object sender, RoutedEventArgs e)
        => SetAllExpanded(_vm.RootNodes, false);

    private void OnFilterChanged(object sender, TextChangedEventArgs e)
        => _vm.FilterText = TbxFilter.Text;

    // ── Visibility re-seed ────────────────────────────────────────────────────

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // Fire RefreshRequested whenever the panel becomes visible so the plugin
        // can reseed from the current wired host — covers the case where the panel
        // was hidden when the last DesignRendered / FocusChanged event fired.
        if (!(bool)e.NewValue) return;

        RefreshRequested?.Invoke(this, EventArgs.Empty);

        // Re-capture toolbar natural widths every time the panel becomes visible.
        // CaptureNaturalWidths() guards ActualWidth>0, so it silently skips when
        // the panel loads in a hidden tab. Invalidating here + scheduling at
        // Background priority gives WPF time to do a full layout pass with the
        // panel actually visible before we try to measure TbgNavigation.
        _overflowManager?.InvalidateWidths();
        Dispatcher.InvokeAsync(
            () =>
            {
                TbgNavigation.Visibility = Visibility.Visible;
                TbgNavigation.UpdateLayout();
                _overflowManager?.CaptureNaturalWidths();
            },
            DispatcherPriority.Background);
    }

    // ── Tree selection ────────────────────────────────────────────────────────

    private void OnTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not LiveTreeNode node) return;

        _vm.SelectedNode = node;

        // Fire NodeSelected so the plugin can highlight the element on the canvas.
        var uiElement = node.Source as UIElement;
        NodeSelected?.Invoke(this, uiElement);
    }

    // ── Hover (NodeHovered event) ─────────────────────────────────────────────

    private void OnTreeMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        // Walk the visual tree under the cursor to find the hovered LiveTreeNode.
        var element = e.OriginalSource as DependencyObject;
        while (element is not null and not TreeViewItem)
        {
            // VisualTreeHelper.GetParent throws on non-Visual nodes (e.g. Run, FlowDocument).
            // Guard: use VisualTreeHelper only for Visual/Visual3D, fall back to LogicalTreeHelper.
            element = element is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                ? System.Windows.Media.VisualTreeHelper.GetParent(element)
                : System.Windows.LogicalTreeHelper.GetParent(element);
        }

        var uiElement = element is TreeViewItem { DataContext: LiveTreeNode n }
            ? n.Source as UIElement
            : null;

        NodeHovered?.Invoke(this, uiElement);
    }

    // ── Size changes ──────────────────────────────────────────────────────────

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (sizeInfo.WidthChanged)
            _overflowManager?.Update();
    }

    // ── Context menu handlers ─────────────────────────────────────────────────

    /// <summary>Context menu "Select on Canvas" — fires NodeSelected for the right-clicked node.</summary>
    private void OnContextSelectOnCanvas(object sender, RoutedEventArgs e)
    {
        if (GetContextNode(sender) is { } node)
        {
            _vm.SelectedNode = node;
            NodeSelected?.Invoke(this, node.Source as UIElement);
        }
    }

    /// <summary>Context menu "Navigate to XAML" — fires NavigateToXamlRequested.</summary>
    private void OnContextNavigateToXaml(object sender, RoutedEventArgs e)
    {
        if (GetContextNode(sender) is { } node)
            NavigateToXamlRequested?.Invoke(this, node.ElementName);
    }

    /// <summary>Context menu "Expand All Children" — recursively expands the right-clicked node.</summary>
    private void OnContextExpandAllChildren(object sender, RoutedEventArgs e)
    {
        if (GetContextNode(sender) is { } node)
            SetAllExpanded(node.Children, true);
    }

    /// <summary>Extracts the <see cref="LiveTreeNode"/> from a MenuItem's context menu DataContext.</summary>
    private static LiveTreeNode? GetContextNode(object sender)
        => (sender as FrameworkElement)?.DataContext as LiveTreeNode
           ?? ((sender as MenuItem)?.Parent as ContextMenu)?.DataContext as LiveTreeNode;

    // ── Breadcrumb click ──────────────────────────────────────────────────────

    /// <summary>Breadcrumb button click — selects the node and fires NodeSelected.</summary>
    private void OnBreadcrumbNodeClick(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is not LiveTreeNode node) return;

        _vm.SelectedNode = node;
        node.IsSelected  = true;

        // Expand ancestor chain so the TreeView selection is visible.
        ExpandAncestors(node);

        NodeSelected?.Invoke(this, node.Source as UIElement);
    }

    // ── Expand to selected ────────────────────────────────────────────────────

    private void OnExpandToSelectedClick(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedNode is not { } node) return;
        ExpandAncestors(node);

        // Bring the selected TreeViewItem into view after layout update.
        LiveTree.UpdateLayout();
        BringNodeIntoView(node);
    }

    private static void ExpandAncestors(LiveTreeNode node)
    {
        var current = node.Parent;
        while (current is not null)
        {
            current.IsExpanded = true;
            current = current.Parent;
        }
    }

    private void BringNodeIntoView(LiveTreeNode node)
    {
        // Walk the TreeView's ItemContainerGenerator to find and scroll to the item.
        var container = FindTreeViewItem(LiveTree, node);
        container?.BringIntoView();
    }

    private static TreeViewItem? FindTreeViewItem(ItemsControl parent, LiveTreeNode target)
    {
        foreach (var item in parent.Items)
        {
            if (parent.ItemContainerGenerator.ContainerFromItem(item) is not TreeViewItem tvi) continue;

            if (ReferenceEquals(item, target)) return tvi;

            var found = FindTreeViewItem(tvi, target);
            if (found is not null) return found;
        }
        return null;
    }

    // ── Feature A: Expand All ─────────────────────────────────────────────────

    private void OnExpandAll(object sender, RoutedEventArgs e)
        => SetAllExpanded(_vm.RootNodes, true);

    // ── Feature B: Search bar ─────────────────────────────────────────────────

    private void OnToggleSearch(object sender, RoutedEventArgs e)
    {
        if (SearchBarRow.Height.Value == 0)
            ShowSearchBar();
        else
            HideSearchBar();
    }

    private void OnTreePreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            ShowSearchBar();
            e.Handled = true;
        }
        else if (e.Key == Key.F3 && SearchBarRow.Height.Value > 0)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
                _vm.NavigateToPrevMatch();
            else
                _vm.NavigateToNextMatch();
            e.Handled = true;
        }
    }

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                HideSearchBar();
                e.Handled = true;
                break;
            case Key.Return:
                _vm.NavigateToNextMatch();
                e.Handled = true;
                break;
            case Key.F3:
                if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
                    _vm.NavigateToPrevMatch();
                else
                    _vm.NavigateToNextMatch();
                e.Handled = true;
                break;
        }
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        => _vm.SearchText = TbxSearch.Text;

    private void OnSearchPrev(object sender, RoutedEventArgs e)
        => _vm.NavigateToPrevMatch();

    private void OnSearchNext(object sender, RoutedEventArgs e)
        => _vm.NavigateToNextMatch();

    private void OnSearchClose(object sender, RoutedEventArgs e)
        => HideSearchBar();

    private void OnSearchCurrentMatchChanged(object? sender, LiveTreeNode? node)
    {
        if (node is null) return;
        Dispatcher.InvokeAsync(() =>
        {
            LiveTree.UpdateLayout();
            BringNodeIntoView(node);
        }, DispatcherPriority.Loaded);
    }

    private void ShowSearchBar()
    {
        SearchBarRow.Height = new GridLength(26);
        TbxSearch.Focus();
        TbxSearch.SelectAll();
    }

    private void HideSearchBar()
    {
        SearchBarRow.Height = new GridLength(0);
        _vm.SearchText      = string.Empty;
        TbxSearch.Text      = string.Empty;
        LiveTree.Focus();
    }

    // ── Feature C: Set as Root / Reset Root ──────────────────────────────────

    private void OnSetAsRoot(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedNode is { } node)
            _vm.SetInspectRoot(node);
    }

    private void OnResetRoot(object sender, RoutedEventArgs e)
        => _vm.ResetInspectRoot();

    private void OnContextSetAsRoot(object sender, RoutedEventArgs e)
    {
        if (GetContextNode(sender) is { } node)
            _vm.SetInspectRoot(node);
    }

    // ── Feature D: Pick Mode relay ────────────────────────────────────────────

    private void OnPickModeChangedFromVm(object? sender, bool isActive)
        => PickModeChanged?.Invoke(this, isActive);

    // ── Feature E: Track Selection ────────────────────────────────────────────

    private void OnTrackSelectionRequested(object? sender, LiveTreeNode? node)
    {
        if (node is null) return;
        ExpandAncestors(node);
        Dispatcher.InvokeAsync(() =>
        {
            LiveTree.UpdateLayout();
            BringNodeIntoView(node);
        }, DispatcherPriority.Loaded);
    }

    // ── Feature F: Filter preset + clear ─────────────────────────────────────

    private void OnClearFilter(object sender, RoutedEventArgs e)
    {
        TbxFilter.Text  = string.Empty;
        _vm.FilterText  = string.Empty;
    }

    private void OnFilterPresetClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu is { } menu)
            menu.IsOpen = true;
    }

    private void OnFilterPresetItem(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is string tag)
        {
            _vm.FilterPreset = tag switch
            {
                "HiddenOnly"   => FilterPreset.HiddenOnly,
                "DisabledOnly" => FilterPreset.DisabledOnly,
                "NamedOnly"    => FilterPreset.NamedOnly,
                _              => FilterPreset.All
            };
        }
    }

    // ── Feature H: Copy XAML Snippet context menu ─────────────────────────────

    private void OnContextCopyXamlSnippet(object sender, RoutedEventArgs e)
    {
        if (GetContextNode(sender) is { } node)
            System.Windows.Clipboard.SetText(node.GenerateXamlSnippet());
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
// FilterPreset enum
// ==========================================================

/// <summary>Preset filter modes for the Live Visual Tree panel.</summary>
public enum FilterPreset
{
    All,
    HiddenOnly,
    DisabledOnly,
    NamedOnly
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
    private string         _filterText      = string.Empty;
    private bool           _autoRefresh     = true;
    private bool           _useLogicalTree;
    private int            _totalNodeCount;

    // B: Search
    private string         _searchText        = string.Empty;
    private int            _searchCurrentIdx  = -1;
    private readonly List<LiveTreeNode> _searchMatches = new();

    // C: Inspect subtree root
    private LiveTreeNode?  _inspectRoot;

    // D: Pick mode
    private bool           _isPickMode;

    // E: Track Selection
    private bool           _trackSelection    = true;

    // F: Filter preset
    private FilterPreset   _filterPreset      = FilterPreset.All;

    // G: Show/Hide Dimensions
    private bool           _showDimensions    = true;

    // ── Properties ────────────────────────────────────────────────────────────

    /// <summary>Root nodes of the live visual tree (0 or 1 entries at top level).</summary>
    public ObservableCollection<LiveTreeNode> RootNodes { get; } = new();

    /// <summary>Ancestor path of the selected node — drives the breadcrumb bar.</summary>
    public ObservableCollection<LiveTreeNode> BreadcrumbPath { get; } = new();

    /// <summary>Currently selected node; null when nothing is selected.</summary>
    public LiveTreeNode? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (ReferenceEquals(_selectedNode, value)) return;
            _selectedNode = value;
            OnPropertyChanged();
            RebuildBreadcrumb(value);
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
            UpdateStatusText();
        }
    }

    /// <summary>
    /// When true (default), the tree is refreshed automatically on every
    /// <c>DesignRendered</c> event. Toggle off to freeze the snapshot.
    /// </summary>
    public bool AutoRefresh
    {
        get => _autoRefresh;
        set { if (_autoRefresh == value) return; _autoRefresh = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// When true, the panel shows the WPF <b>logical</b> tree instead of the visual tree.
    /// Logical tree has fewer nodes and is closer to the XAML source structure.
    /// </summary>
    public bool UseLogicalTree
    {
        get => _useLogicalTree;
        set
        {
            if (_useLogicalTree == value) return;
            _useLogicalTree = value;
            OnPropertyChanged();
            Rebuild();     // Recompute immediately on toggle.
        }
    }

    /// <summary>Status bar text — shows total node count (respects active filter).</summary>
    public string StatusText { get; private set; } = string.Empty;

    // ── B: Search properties ──────────────────────────────────────────────────

    /// <summary>Current search term; updating this triggers ExecuteSearch().</summary>
    public string SearchText
    {
        get => _searchText;
        set { if (_searchText == value) return; _searchText = value; OnPropertyChanged(); ExecuteSearch(); }
    }

    /// <summary>Total number of nodes matching the current search term.</summary>
    public int SearchMatchCount => _searchMatches.Count;

    /// <summary>1-based index of the active match (0 when no matches).</summary>
    public int SearchCurrentIndex => _searchCurrentIdx < 0 ? 0 : _searchCurrentIdx + 1;

    // ── C: Inspect Root properties ────────────────────────────────────────────

    /// <summary>True when the tree is rooted at a subtree node rather than the full canvas root.</summary>
    public bool IsInspectMode => _inspectRoot is not null;

    /// <summary>Label for the inspect-mode banner, e.g. "Subtree: Button".</summary>
    public string InspectRootLabel => _inspectRoot is null ? string.Empty : $"Subtree: {_inspectRoot.TypeName}";

    // ── D: Pick Mode ──────────────────────────────────────────────────────────

    /// <summary>When true, every canvas click auto-scrolls the tree to the selected element.</summary>
    public bool IsPickMode
    {
        get => _isPickMode;
        set
        {
            if (_isPickMode == value) return;
            _isPickMode = value;
            OnPropertyChanged();
            PickModeChanged?.Invoke(this, value);
        }
    }

    // ── E: Track Selection ────────────────────────────────────────────────────

    /// <summary>When true, canvas selection automatically expands and scrolls the tree.</summary>
    public bool TrackSelection
    {
        get => _trackSelection;
        set { if (_trackSelection == value) return; _trackSelection = value; OnPropertyChanged(); }
    }

    // ── F: Filter Preset ─────────────────────────────────────────────────────

    /// <summary>Preset filter mode (All / HiddenOnly / DisabledOnly / NamedOnly).</summary>
    public FilterPreset FilterPreset
    {
        get => _filterPreset;
        set
        {
            if (_filterPreset == value) return;
            _filterPreset = value;
            OnPropertyChanged();
            ApplyFilter(RootNodes);
            UpdateStatusText();
        }
    }

    // ── G: Show/Hide Dimensions ───────────────────────────────────────────────

    /// <summary>Controls whether element dimension labels are shown on tree nodes.</summary>
    public bool ShowDimensions
    {
        get => _showDimensions;
        set
        {
            if (_showDimensions == value) return;
            _showDimensions = value;
            OnPropertyChanged();
            PropagateShowDimensions(RootNodes, value);
        }
    }

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fired when the selected node changes.</summary>
    public event EventHandler<LiveTreeNode?>? SelectedNodeChanged;

    /// <summary>Fired when IsPickMode changes — panel relays to host as PickModeChanged.</summary>
    public event EventHandler<bool>? PickModeChanged;

    /// <summary>Fired when the search current match changes — code-behind scrolls the TreeView.</summary>
    public event EventHandler<LiveTreeNode?>? SearchCurrentMatchChanged;

    /// <summary>Fired when TrackSelection is on and canvas selection changes — code-behind scrolls.</summary>
    public event EventHandler<LiveTreeNode?>? TrackSelectionRequested;

    // ── INPC ──────────────────────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Rebuilds the tree from the provided root element.
    /// Pass null to clear the panel when no canvas root is available.
    /// </summary>
    public void Refresh(UIElement? root)
    {
        _root = root;
        Rebuild();
    }

    /// <summary>Re-reads the tree from the last-known root element.</summary>
    public void Refresh() => Rebuild();

    /// <summary>
    /// Programmatically selects the node whose backing source matches
    /// <paramref name="element"/> — used for canvas → tree synchronisation.
    /// </summary>
    public void SelectNodeByElement(UIElement? element)
    {
        if (element is null || RootNodes.Count == 0) return;

        var found = FindNodeBySource(RootNodes[0], element);
        if (found is null) return;

        SelectedNode = found;

        // E: If TrackSelection is on, notify the code-behind to expand + scroll.
        if (_trackSelection)
            TrackSelectionRequested?.Invoke(this, found);
    }

    // ── B: Search public API ──────────────────────────────────────────────────

    /// <summary>Executes a search for <see cref="SearchText"/> across all tree nodes.</summary>
    public void ExecuteSearch()
    {
        ClearSearchHighlights(RootNodes);
        _searchMatches.Clear();
        _searchCurrentIdx = -1;

        var term = _searchText.Trim();
        if (!string.IsNullOrEmpty(term))
            CollectSearchMatches(RootNodes, term, _searchMatches);

        foreach (var match in _searchMatches)
            match.IsSearchMatch = true;

        if (_searchMatches.Count > 0)
            NavigateToMatchByIndex(0);

        OnPropertyChanged(nameof(SearchMatchCount));
        OnPropertyChanged(nameof(SearchCurrentIndex));
        UpdateStatusText();
    }

    /// <summary>Advances to the next search match (wraps around).</summary>
    public void NavigateToNextMatch()
    {
        if (_searchMatches.Count == 0) return;
        NavigateToMatchByIndex((_searchCurrentIdx + 1) % _searchMatches.Count);
    }

    /// <summary>Moves to the previous search match (wraps around).</summary>
    public void NavigateToPrevMatch()
    {
        if (_searchMatches.Count == 0) return;
        NavigateToMatchByIndex((_searchCurrentIdx - 1 + _searchMatches.Count) % _searchMatches.Count);
    }

    // ── C: Inspect Root public API ────────────────────────────────────────────

    /// <summary>Re-roots the displayed tree at <paramref name="node"/> (inspect subtree mode).</summary>
    public void SetInspectRoot(LiveTreeNode node)
    {
        _inspectRoot = node;
        OnPropertyChanged(nameof(IsInspectMode));
        OnPropertyChanged(nameof(InspectRootLabel));
        RebuildFromInspectRoot();
    }

    /// <summary>Returns to the full tree view from inspect subtree mode.</summary>
    public void ResetInspectRoot()
    {
        _inspectRoot = null;
        OnPropertyChanged(nameof(IsInspectMode));
        OnPropertyChanged(nameof(InspectRootLabel));
        Rebuild();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void Rebuild()
    {
        RootNodes.Clear();
        BreadcrumbPath.Clear();
        _selectedNode = null;
        OnPropertyChanged(nameof(SelectedNode));

        if (_root is null)
        {
            _totalNodeCount = 0;
            UpdateStatusText();
            return;
        }

        var service  = new LiveVisualTreeService();
        var rootNode = _useLogicalTree
            ? service.BuildLogicalTree(_root)
            : service.BuildTree(_root);

        if (rootNode is not null)
        {
            RootNodes.Add(rootNode);
            rootNode.IsExpanded = true;    // Auto-expand root for discoverability.
        }

        // G: Propagate current ShowDimensions state to all newly built nodes.
        PropagateShowDimensions(RootNodes, _showDimensions);

        // Count all nodes for the status bar.
        _totalNodeCount = 0;
        CountNodes(RootNodes, ref _totalNodeCount);
        UpdateStatusText();
    }

    private void RebuildBreadcrumb(LiveTreeNode? node)
    {
        BreadcrumbPath.Clear();
        if (node is null) return;

        var path    = new System.Collections.Generic.List<LiveTreeNode>();
        var current = node;
        while (current is not null) { path.Add(current); current = current.Parent; }
        path.Reverse();
        foreach (var n in path) BreadcrumbPath.Add(n);
    }

    private bool ApplyFilter(ObservableCollection<LiveTreeNode> nodes)
    {
        var  filter   = _filterText.Trim();
        bool anyMatch = false;

        foreach (var node in nodes)
        {
            bool childMatch = ApplyFilter(node.Children);

            bool textMatch = string.IsNullOrEmpty(filter)
                             || node.DisplayLabel.Contains(filter, StringComparison.OrdinalIgnoreCase);

            // F: Apply preset filter in addition to text filter.
            bool presetMatch = _filterPreset switch
            {
                FilterPreset.HiddenOnly   => node.VisibilityBadgeText is not null,
                FilterPreset.DisabledOnly => node.IsDisabled,
                FilterPreset.NamedOnly    => node.HasName,
                _                         => true
            };

            bool selfMatch = textMatch && presetMatch;
            bool visible   = selfMatch || childMatch;
            anyMatch      |= visible;

            bool filterActive = !string.IsNullOrEmpty(filter) || _filterPreset != FilterPreset.All;
            if (childMatch && filterActive)
                node.IsExpanded = true;
        }

        return anyMatch;
    }

    private static void CountNodes(ObservableCollection<LiveTreeNode> nodes, ref int total)
    {
        foreach (var n in nodes)
        {
            total++;
            CountNodes(n.Children, ref total);
        }
    }

    private void UpdateStatusText()
    {
        var baseText = _totalNodeCount == 0 ? "No elements" : $"{_totalNodeCount} elements";

        StatusText = _searchMatches.Count > 0
            ? $"{baseText} — {_searchMatches.Count} match{(_searchMatches.Count == 1 ? "" : "es")}"
            : baseText;

        OnPropertyChanged(nameof(StatusText));
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

    // ── B: Search helpers ─────────────────────────────────────────────────────

    private static void CollectSearchMatches(
        ObservableCollection<LiveTreeNode> nodes,
        string term,
        List<LiveTreeNode> matches)
    {
        foreach (var node in nodes)
        {
            if (node.TypeName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || node.DisplayLabel.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (node.ElementName?.Contains(term, StringComparison.OrdinalIgnoreCase) == true))
            {
                matches.Add(node);
            }
            CollectSearchMatches(node.Children, term, matches);
        }
    }

    private static void ClearSearchHighlights(ObservableCollection<LiveTreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            node.IsSearchMatch        = false;
            node.IsCurrentSearchMatch = false;
            ClearSearchHighlights(node.Children);
        }
    }

    private void NavigateToMatchByIndex(int index)
    {
        if (_searchMatches.Count == 0) { _searchCurrentIdx = -1; return; }

        // Clear previous active flag.
        if (_searchCurrentIdx >= 0 && _searchCurrentIdx < _searchMatches.Count)
            _searchMatches[_searchCurrentIdx].IsCurrentSearchMatch = false;

        _searchCurrentIdx = index;
        var current = _searchMatches[index];
        current.IsCurrentSearchMatch = true;

        // Expand ancestors so the match is visible.
        ExpandAncestors(current);
        SearchCurrentMatchChanged?.Invoke(this, current);

        OnPropertyChanged(nameof(SearchCurrentIndex));
    }

    // C: Rebuild from inspect root node.
    private void RebuildFromInspectRoot()
    {
        RootNodes.Clear();
        BreadcrumbPath.Clear();
        _selectedNode = null;
        OnPropertyChanged(nameof(SelectedNode));

        if (_inspectRoot is null) return;
        RootNodes.Add(_inspectRoot);
        _inspectRoot.IsExpanded = true;

        PropagateShowDimensions(RootNodes, _showDimensions);

        _totalNodeCount = 0;
        CountNodes(RootNodes, ref _totalNodeCount);
        UpdateStatusText();
    }

    // G: Propagate ShowDimensions to all nodes recursively.
    private static void PropagateShowDimensions(ObservableCollection<LiveTreeNode> nodes, bool show)
    {
        foreach (var node in nodes)
        {
            node.ShowDimensions = show;
            PropagateShowDimensions(node.Children, show);
        }
    }

    // ViewModel-level ExpandAncestors — sets IsExpanded without a TreeView reference.
    private static void ExpandAncestors(LiveTreeNode node)
    {
        var current = node.Parent;
        while (current is not null)
        {
            current.IsExpanded = true;
            current = current.Parent;
        }
    }
}
