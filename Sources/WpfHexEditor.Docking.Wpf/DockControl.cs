//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Docking.Core;
using WpfHexEditor.Docking.Core.Nodes;
using WpfHexEditor.Docking.Wpf.Commands;

namespace WpfHexEditor.Docking.Wpf;

/// <summary>
/// Main WPF control for the docking system.
/// Renders the <see cref="DockLayoutRoot"/> tree as WPF visual elements,
/// and integrates drag &amp; drop, floating windows, and auto-hide bars.
/// </summary>
public class DockControl : ContentControl, IDockHost, IDisposable
{
    private DockEngine? _engine;
    private DockDragManager? _dragManager;
    private FloatingWindowManager? _floatingManager;
    private DockKeyboardNavigation? _keyboardNav;
    private readonly List<DockTabEventWirer> _tabWirers = [];
    private readonly Dictionary<string, object> _contentCache = new();
    internal readonly List<DockItem> ActivationHistory = [];

    private readonly Grid _rootGrid;
    private readonly DockPanel _rootPanel;
    private readonly AutoHideBar _autoHideLeft;
    private readonly AutoHideBar _autoHideRight;
    private readonly AutoHideBar _autoHideTop;
    private readonly AutoHideBar _autoHideBottom;
    private readonly AutoHideFlyout _autoHideFlyout;
    private readonly ContentControl _centerHost;
    private Border? _activePanel;

    public static readonly DependencyProperty LayoutProperty =
        DependencyProperty.Register(
            nameof(Layout),
            typeof(DockLayoutRoot),
            typeof(DockControl),
            new PropertyMetadata(null, OnLayoutChanged));

    /// <summary>
    /// The dock layout root to render.
    /// </summary>
    public DockLayoutRoot? Layout
    {
        get => (DockLayoutRoot?)GetValue(LayoutProperty);
        set => SetValue(LayoutProperty, value);
    }

    /// <summary>
    /// The engine managing the layout. Created automatically when Layout is set.
    /// </summary>
    public DockEngine? Engine => _engine;

    // ─── MVVM Source Binding ─────────────────────────────────────────

    public static readonly DependencyProperty DocumentsSourceProperty =
        DependencyProperty.Register(nameof(DocumentsSource), typeof(IEnumerable), typeof(DockControl),
            new PropertyMetadata(null, OnDocumentsSourceChanged));

    public static readonly DependencyProperty AnchorablesSourceProperty =
        DependencyProperty.Register(nameof(AnchorablesSource), typeof(IEnumerable), typeof(DockControl),
            new PropertyMetadata(null, OnAnchorablesSourceChanged));

    /// <summary>
    /// Collection of view-model objects to display as document tabs.
    /// Requires <see cref="ItemMapper"/> to convert VMs to <see cref="DockItem"/>s.
    /// Supports <see cref="INotifyCollectionChanged"/> for live sync.
    /// </summary>
    public IEnumerable? DocumentsSource
    {
        get => (IEnumerable?)GetValue(DocumentsSourceProperty);
        set => SetValue(DocumentsSourceProperty, value);
    }

    /// <summary>
    /// Collection of view-model objects to display as anchorable tool panels.
    /// </summary>
    public IEnumerable? AnchorablesSource
    {
        get => (IEnumerable?)GetValue(AnchorablesSourceProperty);
        set => SetValue(AnchorablesSourceProperty, value);
    }

    /// <summary>
    /// Maps a view-model object to a <see cref="DockItem"/>.
    /// Required when using <see cref="DocumentsSource"/> or <see cref="AnchorablesSource"/>.
    /// </summary>
    public Func<object, DockItem>? ItemMapper { get; set; }

    /// <summary>
    /// Optional strategy to customize how items are inserted into the layout.
    /// Consulted by the source synchronizers before default insertion logic.
    /// </summary>
    public ILayoutUpdateStrategy? LayoutUpdateStrategy { get; set; }

    private DockItemSourceSynchronizer? _documentsSynchronizer;
    private DockItemSourceSynchronizer? _anchorablesSynchronizer;

    private static void OnDocumentsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DockControl dc) dc.ResetDocumentsSynchronizer();
    }

    private static void OnAnchorablesSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DockControl dc) dc.ResetAnchorablesSynchronizer();
    }

    private void ResetDocumentsSynchronizer()
    {
        _documentsSynchronizer?.Dispose();
        _documentsSynchronizer = null;

        if (DocumentsSource is not null && ItemMapper is not null && _engine is not null)
            _documentsSynchronizer = new DockItemSourceSynchronizer(this, DocumentsSource, ItemMapper, isDocument: true);
    }

    private void ResetAnchorablesSynchronizer()
    {
        _anchorablesSynchronizer?.Dispose();
        _anchorablesSynchronizer = null;

        if (AnchorablesSource is not null && ItemMapper is not null && _engine is not null)
            _anchorablesSynchronizer = new DockItemSourceSynchronizer(this, AnchorablesSource, ItemMapper, isDocument: false);
    }

    // ─── Content Factory ─────────────────────────────────────────────

    /// <summary>
    /// Factory to create content for a DockItem. If not set, a default placeholder is shown.
    /// </summary>
    public Func<DockItem, object>? ContentFactory { get; set; }

    /// <summary>
    /// Optional async content factory. When set and <see cref="ContentFactory"/> is null,
    /// tabs show an indeterminate progress bar until the async factory completes.
    /// </summary>
    public Func<DockItem, Task<object>>? AsyncContentFactory { get; set; }

    // ─── Layout Item Templates ──────────────────────────────────────

    /// <summary>
    /// Default <see cref="DataTemplate"/> used to present document view-model content.
    /// Applied when <see cref="ContentFactory"/> is null and the item is in a <see cref="DocumentHostNode"/>.
    /// </summary>
    public DataTemplate? DocumentTemplate { get; set; }

    /// <summary>
    /// Selects a <see cref="DataTemplate"/> per document item. Takes precedence over <see cref="DocumentTemplate"/>.
    /// </summary>
    public DataTemplateSelector? DocumentTemplateSelector { get; set; }

    /// <summary>
    /// Default <see cref="DataTemplate"/> used to present anchorable (tool) view-model content.
    /// Applied when <see cref="ContentFactory"/> is null and the item is in a <see cref="DockGroupNode"/>.
    /// </summary>
    public DataTemplate? AnchorableTemplate { get; set; }

    /// <summary>
    /// Selects a <see cref="DataTemplate"/> per anchorable item. Takes precedence over <see cref="AnchorableTemplate"/>.
    /// </summary>
    public DataTemplateSelector? AnchorableTemplateSelector { get; set; }

    // ─── Cached Content Factory ─────────────────────────────────────

    /// <summary>
    /// Returns a wrapped content factory that caches results by <see cref="DockItem.ContentId"/>.
    /// Supports sync factories, async factories, and DataTemplate-based content.
    /// </summary>
    internal Func<DockItem, object>? CachedContentFactory =>
        ContentFactory is not null || AsyncContentFactory is not null || HasTemplateSupport
            ? GetOrCreateContent
            : null;

    private bool HasTemplateSupport =>
        DocumentTemplate is not null || DocumentTemplateSelector is not null ||
        AnchorableTemplate is not null || AnchorableTemplateSelector is not null;

    private object GetOrCreateContent(DockItem item)
    {
        if (_contentCache.TryGetValue(item.ContentId, out var cached))
            return cached;

        if (ContentFactory is not null)
        {
            var content = ContentFactory(item);
            _contentCache[item.ContentId] = content;
            return content;
        }

        if (AsyncContentFactory is not null)
        {
            var placeholder = new System.Windows.Controls.ProgressBar
            {
                IsIndeterminate = true,
                Height = 4,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0)
            };
            _contentCache[item.ContentId] = placeholder;

            var factory = AsyncContentFactory;
            _ = LoadAsyncContent(item, factory, placeholder);
            return placeholder;
        }

        // Template-based content: wrap the VM (item.Tag) in a ContentPresenter
        if (item.Tag is not null && HasTemplateSupport)
        {
            var isDocument = item.Owner is DocumentHostNode;
            var selector = isDocument ? DocumentTemplateSelector : AnchorableTemplateSelector;
            var template = isDocument ? DocumentTemplate : AnchorableTemplate;

            var presenter = new ContentPresenter { Content = item.Tag };
            if (selector is not null)
                presenter.ContentTemplateSelector = selector;
            else if (template is not null)
                presenter.ContentTemplate = template;

            _contentCache[item.ContentId] = presenter;
            return presenter;
        }

        return new System.Windows.Controls.TextBlock { Text = $"Content: {item.Title}" };
    }

    private async Task LoadAsyncContent(DockItem item, Func<DockItem, Task<object>> factory,
        System.Windows.Controls.ProgressBar placeholder)
    {
        var content = await factory(item);
        await Dispatcher.InvokeAsync(() =>
        {
            _contentCache[item.ContentId] = content;

            // Find and replace the placeholder in all tab controls
            ReplaceContent(placeholder, content);
        });
    }

    private void ReplaceContent(object oldContent, object newContent)
    {
        if (_centerHost.Content == oldContent)
        {
            _centerHost.Content = newContent;
            return;
        }

        // Walk visual tree to find ContentControls/TabItems holding the placeholder
        ReplaceInVisualTree(_rootGrid, oldContent, newContent);
    }

    private static void ReplaceInVisualTree(DependencyObject parent, object oldContent, object newContent)
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is System.Windows.Controls.ContentControl cc && cc.Content == oldContent)
            {
                cc.Content = newContent;
                return;
            }
            if (child is System.Windows.Controls.TabItem ti && ti.Content == oldContent)
            {
                ti.Content = newContent;
                return;
            }
            ReplaceInVisualTree(child, oldContent, newContent);
        }
    }

    /// <summary>
    /// The center content host, used by DockDragManager for overlay positioning.
    /// </summary>
    internal ContentControl CenterHost => _centerHost;

    /// <summary>
    /// The drag manager, used by FloatingWindowManager for drag-to-dock operations.
    /// </summary>
    internal DockDragManager? DragManager => _dragManager;

    /// <summary>
    /// The floating window manager, used by DockDragManager to retrieve a newly created window after Float().
    /// </summary>
    internal FloatingWindowManager? FloatingManager => _floatingManager;

    /// <summary>
    /// Configurable drag threshold in DIPs. Defaults to system minimum.
    /// </summary>
    public double DragThreshold { get; set; } = SystemParameters.MinimumHorizontalDragDistance;

    /// <summary>
    /// Optional callback invoked before closing an item. Return false to veto the close.
    /// </summary>
    public Func<DockItem, bool>? BeforeCloseCallback { get; set; }

    /// <summary>
    /// Raised when a tab close is requested.
    /// </summary>
    public event Action<DockItem>? TabCloseRequested;

    /// <summary>
    /// Raised when the active (focused) item changes.
    /// </summary>
    public event Action<DockItem>? ActiveItemChanged;

    public DockControl()
    {
        _autoHideLeft = new AutoHideBar(Dock.Left);
        _autoHideRight = new AutoHideBar(Dock.Right);
        _autoHideTop = new AutoHideBar(Dock.Top);
        _autoHideBottom = new AutoHideBar(Dock.Bottom);
        _autoHideFlyout = new AutoHideFlyout();
        _autoHideFlyout.RestoreRequested += OnAutoHideRestoreRequested;
        _autoHideFlyout.CloseRequested += OnAutoHideCloseRequested;
        _autoHideFlyout.FloatRequested += OnAutoHideFloatRequested;
        _centerHost = new ContentControl();

        _autoHideLeft.ItemClicked += OnAutoHideItemClicked;
        _autoHideRight.ItemClicked += OnAutoHideItemClicked;
        _autoHideTop.ItemClicked += OnAutoHideItemClicked;
        _autoHideBottom.ItemClicked += OnAutoHideItemClicked;

        // Build the root structure: DockPanel with bars + center, then flyout overlay
        _rootPanel = new DockPanel { LastChildFill = true };

        DockPanel.SetDock(_autoHideLeft, Dock.Left);
        DockPanel.SetDock(_autoHideRight, Dock.Right);
        DockPanel.SetDock(_autoHideTop, Dock.Top);
        DockPanel.SetDock(_autoHideBottom, Dock.Bottom);

        _rootPanel.Children.Add(_autoHideLeft);
        _rootPanel.Children.Add(_autoHideRight);
        _rootPanel.Children.Add(_autoHideTop);
        _rootPanel.Children.Add(_autoHideBottom);
        _rootPanel.Children.Add(_centerHost);

        // Grid with two layers: content + flyout overlay
        // DockWindowBackgroundBrush is darker than DockBackgroundBrush so the inter-panel
        // gaps (from panel Margin) appear as a distinct darker background (VS2026-style).
        _rootGrid = new Grid();
        _rootGrid.SetResourceReference(Panel.BackgroundProperty, "DockWindowBackgroundBrush");
        _rootGrid.Children.Add(_rootPanel);
        _rootGrid.Children.Add(_autoHideFlyout);

        Content = _rootGrid;

        // Break strong reference cycle: unsubscribe engine events when control leaves the visual tree
        Unloaded += (_, _) => DetachEngine();
        Loaded += (_, _) => AttachEngine();

        // Register routed commands (MVVM-friendly)
        RegisterDockCommands();
    }

    private void RegisterDockCommands()
    {
        CommandBindings.Add(new CommandBinding(
            DockCommands.Close,
            (_, e) => { if (GetCommandItem(e) is { } item) { RaiseTabCloseRequested(item); _engine?.Close(item); RebuildVisualTree(); } },
            (_, e) => { var item = GetCommandItem(e); e.CanExecute = item is { CanClose: true }; }));

        CommandBindings.Add(new CommandBinding(
            DockCommands.Float,
            (_, e) => { if (GetCommandItem(e) is { } item) { CaptureDockedSizeForFloat(item); _engine?.Float(item); RebuildVisualTree(); } },
            (_, e) => { var item = GetCommandItem(e); e.CanExecute = item is { CanFloat: true }; }));

        CommandBindings.Add(new CommandBinding(
            DockCommands.AutoHide,
            (_, e) => { if (GetCommandItem(e) is { } item) { _engine?.AutoHide(item); RebuildVisualTree(); } },
            (_, e) => e.CanExecute = GetCommandItem(e) is not null));

        CommandBindings.Add(new CommandBinding(
            DockCommands.Dock,
            (_, e) =>
            {
                if (GetCommandItem(e) is not { } item || _engine is null || Layout is null) return;
                var dir = item.LastDockSide switch
                {
                    Core.DockSide.Left   => DockDirection.Left,
                    Core.DockSide.Right  => DockDirection.Right,
                    Core.DockSide.Top    => DockDirection.Top,
                    Core.DockSide.Bottom => DockDirection.Bottom,
                    _                    => DockDirection.Center
                };
                _engine.Dock(item, Layout.MainDocumentHost, dir);
                RebuildVisualTree();
            },
            (_, e) => e.CanExecute = GetCommandItem(e) is not null));

        CommandBindings.Add(new CommandBinding(
            DockCommands.CloseAll,
            (_, _) => { CloseAllClosableItems(); RebuildVisualTree(); },
            (_, e) => e.CanExecute = _engine is not null));

        CommandBindings.Add(new CommandBinding(
            DockCommands.AutoHideAll,
            (_, _) => { _engine?.AutoHideAll(); RebuildVisualTree(); },
            (_, e) => e.CanExecute = _engine is not null));

        CommandBindings.Add(new CommandBinding(
            DockCommands.RestoreAll,
            (_, _) => { _engine?.RestoreAllFromAutoHide(); RebuildVisualTree(); },
            (_, e) => e.CanExecute = _engine is not null && Layout?.AutoHideItems.Any() == true));
    }

    private void CloseAllClosableItems()
    {
        if (_engine is null || Layout is null) return;
        var items = CollectItems(Layout.RootNode);
        foreach (var item in items)
            if (item.CanClose) _engine.Close(item);
    }

    private static List<DockItem> CollectItems(DockNode node) => node switch
    {
        DockSplitNode s => s.Children.SelectMany(CollectItems).ToList(),
        DockGroupNode g => g.Items.ToList(),
        _ => []
    };

    /// <summary>
    /// Resolves the <see cref="DockItem"/> for a command — from explicit parameter or from keyboard focus.
    /// </summary>
    private static DockItem? GetCommandItem(ExecutedRoutedEventArgs e) =>
        e.Parameter as DockItem ?? GetFocusedDockItem();

    private static DockItem? GetCommandItem(CanExecuteRoutedEventArgs e) =>
        e.Parameter as DockItem ?? GetFocusedDockItem();

    private static DockItem? GetFocusedDockItem()
    {
        var focused = Keyboard.FocusedElement as DependencyObject;
        while (focused is not null)
        {
            if (focused is TabItem { Tag: DockItem item })
                return item;
            if (focused is DockTabControl tc && tc.SelectedItem is TabItem { Tag: DockItem active })
                return active;
            focused = VisualTreeHelper.GetParent(focused);
        }
        return null;
    }

    private static void OnLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DockControl control)
        {
            // Fully detach from old engine (all 5 events)
            control.DetachEngine();
            control._contentCache.Clear();

            if (e.NewValue is DockLayoutRoot newLayout)
            {
                control._engine = new DockEngine(newLayout);
                control.AttachEngine();

                control._dragManager = new DockDragManager(control);
                control._floatingManager = new FloatingWindowManager(control);
                control._keyboardNav = new DockKeyboardNavigation(control);

                // Notify strategy that a layout was loaded (e.g. after deserialization)
                control.LayoutUpdateStrategy?.AfterLayoutDeserialized(newLayout);

                control.RebuildVisualTree();
            }
            else
            {
                control._engine = null;
                control._dragManager = null;
                control._floatingManager?.CloseAll();
                control._floatingManager = null;
                control._centerHost.Content = null;
            }
        }
    }

    /// <summary>
    /// Subscribes to all engine event handlers. Safe to call multiple times (idempotent).
    /// </summary>
    private void AttachEngine()
    {
        if (_engine is null) return;
        // Detach first to avoid double-subscription
        DetachEngine();
        _engine.LayoutChanged += OnLayoutTreeChanged;
        _engine.ItemFloated   += OnItemFloated;
        _engine.ItemDocked    += OnItemDocked;
        _engine.ItemClosed    += OnItemClosed;
        _engine.ItemHidden    += OnItemHidden;
        _engine.GroupFloated  += OnGroupFloated;
    }

    /// <summary>
    /// Unsubscribes all engine event handlers to prevent leaks.
    /// </summary>
    private void DetachEngine()
    {
        if (_engine is null) return;
        _engine.LayoutChanged -= OnLayoutTreeChanged;
        _engine.ItemFloated   -= OnItemFloated;
        _engine.ItemDocked    -= OnItemDocked;
        _engine.ItemClosed    -= OnItemClosed;
        _engine.ItemHidden    -= OnItemHidden;
        _engine.GroupFloated  -= OnGroupFloated;
    }

    /// <summary>
    /// Disposes all tab event wirers from the previous visual tree.
    /// </summary>
    private void DisposeWirers()
    {
        foreach (var w in _tabWirers) w.Dispose();
        _tabWirers.Clear();
    }

    public void Dispose()
    {
        DetachEngine();
        DisposeWirers();
        _documentsSynchronizer?.Dispose();
        _anchorablesSynchronizer?.Dispose();
        _keyboardNav?.Detach();
        _floatingManager?.CloseAll();
        _autoHideFlyout.RestoreRequested -= OnAutoHideRestoreRequested;
        _autoHideFlyout.CloseRequested   -= OnAutoHideCloseRequested;
        _autoHideFlyout.FloatRequested   -= OnAutoHideFloatRequested;
        GC.SuppressFinalize(this);
    }

    private void OnLayoutTreeChanged()
    {
        Dispatcher.Invoke(RebuildVisualTree);
    }

    // ─── Layout size sync ───────────────────────────────────────────

    /// <summary>
    /// Synchronizes the in-memory <see cref="DockSplitNode.Ratios"/> with the actual
    /// rendered column/row sizes of every <see cref="DockSplitPanel"/> in the visual tree.
    /// Call this before serializing the layout to persist user-resized panels.
    /// </summary>
    public void SyncLayoutSizes()
    {
        SyncSplitPanels(_centerHost);

        static void SyncSplitPanels(DependencyObject parent)
        {
            var count = VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is DockSplitPanel panel)
                    panel.SyncRatiosFromVisual();
                SyncSplitPanels(child);
            }
        }
    }

    /// <summary>
    /// Returns the rendered size of the visual element hosting the given group node.
    /// Walks up from the <see cref="DockTabControl"/> to find the direct child of
    /// a <see cref="DockSplitPanel"/> (the outermost wrapper), which reflects the cell size.
    /// Returns null if the group has no visual representation.
    /// </summary>
    internal Size? GetRenderedSizeForGroup(DockGroupNode group)
    {
        var tabControl = FindDescendant<DockTabControl>(_centerHost, tc => tc.Node == group);
        if (tabControl is null) return null;

        // Walk up to find the direct child of a DockSplitPanel
        FrameworkElement element = tabControl;
        DependencyObject parent = VisualTreeHelper.GetParent(tabControl);
        while (parent is not null and not DockSplitPanel)
        {
            if (parent is FrameworkElement fe)
                element = fe;
            parent = VisualTreeHelper.GetParent(parent);
        }

        return parent is DockSplitPanel ? element.RenderSize : tabControl.RenderSize;
    }

    /// <summary>
    /// Captures the current rendered size of a docked item's panel and stores it as
    /// <see cref="DockItem.FloatWidth"/>/<see cref="DockItem.FloatHeight"/>.
    /// Must be called BEFORE <see cref="DockEngine.Float"/> (which destroys the owner reference).
    /// </summary>
    internal void CaptureDockedSizeForFloat(DockItem item)
    {
        if (item.Owner is not { } group) return;
        var size = GetRenderedSizeForGroup(group);
        if (size is { Width: > 0, Height: > 0 })
        {
            item.FloatWidth = size.Value.Width;
            item.FloatHeight = size.Value.Height;
        }
    }

    /// <summary>
    /// Captures the current rendered size of a group and stores it on its active item.
    /// Must be called BEFORE <see cref="DockEngine.FloatGroup"/>.
    /// </summary>
    internal void CaptureDockedSizeForFloat(DockGroupNode group)
    {
        var activeItem = group.ActiveItem ?? group.Items.FirstOrDefault();
        if (activeItem is null) return;
        var size = GetRenderedSizeForGroup(group);
        if (size is { Width: > 0, Height: > 0 })
        {
            activeItem.FloatWidth = size.Value.Width;
            activeItem.FloatHeight = size.Value.Height;
        }
    }

    private static T? FindDescendant<T>(DependencyObject parent, Func<T, bool> predicate) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t && predicate(t))
                return t;
            var found = FindDescendant(child, predicate);
            if (found is not null)
                return found;
        }
        return null;
    }

    /// <summary>
    /// Rebuilds the entire visual tree from the current Layout.
    /// </summary>
    public void RebuildVisualTree()
    {
        // Dispose previous tab wirers to prevent event leaks
        DisposeWirers();
        _activePanel = null;

        if (Layout is null)
        {
            _centerHost.Content = null;
            return;
        }

        _centerHost.Content = CreateVisualForNode(Layout.RootNode);
        UpdateAutoHideBars();
        RestoreFloatingWindows();

        // Fire ActiveItemChanged for the main document host's active item on first load so that
        // the host app can initialize ActiveDocumentEditor correctly after layout deserialization.
        // Only done when no prior user activation has occurred (ActivationHistory is empty),
        // which prevents this from firing on every rebuild triggered by dock/undock operations.
        if (ActivationHistory.Count == 0)
        {
            var mainActiveItem = Layout.MainDocumentHost.ActiveItem;
            if (mainActiveItem is not null)
                Dispatcher.BeginInvoke(() => TrackActivation(mainActiveItem));
        }
    }

    /// <summary>
    /// Ensures every item in <see cref="DockLayoutRoot.FloatingItems"/> has a visible
    /// <see cref="FloatingWindow"/>. Called after each visual tree rebuild so that
    /// floating items persisted in a saved layout are shown on restore.
    /// Items that already have a window (floated interactively) are skipped.
    /// </summary>
    private void RestoreFloatingWindows()
    {
        if (Layout is null || _floatingManager is null) return;

        foreach (var item in Layout.FloatingItems)
        {
            if (_floatingManager.FindWindowForItem(item) is null)
                _floatingManager.CreateFloatingWindow(item);
        }
    }

    private UIElement CreateVisualForNode(DockNode node)
    {
        return node switch
        {
            DockSplitNode split => CreateSplitPanel(split),
            DocumentHostNode docHost => CreateDocumentHost(docHost),
            DockGroupNode group => CreateTabControl(group),
            _ => new TextBlock { Text = $"Unknown node: {node.GetType().Name}" }
        };
    }

    private DockSplitPanel CreateSplitPanel(DockSplitNode split)
    {
        var panel = new DockSplitPanel();
        panel.Bind(split, CreateVisualForNode);
        return panel;
    }

    private UIElement CreateDocumentHost(DocumentHostNode docHost)
    {
        var host = new DocumentTabHost();

        if (docHost.IsEmpty)
        {
            host.ShowEmptyPlaceholder();
        }
        else
        {
            host.Bind(docHost, CachedContentFactory);
        }

        WireTabControlEvents(host);
        return CreatePanelBorder(host);
    }

    private UIElement CreateTabControl(DockGroupNode group)
    {
        var tabControl = CreateTabControlForGroup(group);
        var titleBar   = CreateGroupTitleBar(group, tabControl);

        var layout = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(titleBar, Dock.Top);
        layout.Children.Add(titleBar);
        layout.Children.Add(tabControl);

        return CreatePanelBorder(layout);
    }

    /// <summary>
    /// Creates and binds a <see cref="DockTabControl"/> for a side panel group
    /// with bottom tab strip placement (VS-style).
    /// </summary>
    private DockTabControl CreateTabControlForGroup(DockGroupNode group)
    {
        var tabControl = new DockTabControl();
        tabControl.TabStripPlacement = Dock.Bottom;
        tabControl.Bind(group, CachedContentFactory);
        WireTabControlEvents(tabControl);
        return tabControl;
    }

    /// <summary>
    /// Creates a draggable title bar for a side panel group with VS-style action buttons
    /// (chevron ▼, pin 📌, close ✕). Supports drag-to-float via <see cref="DockDragManager.BeginGroupDrag"/>.
    /// </summary>
    private Border CreateGroupTitleBar(DockGroupNode group, DockTabControl tabControl)
    {
        // Icon before title (updates with active item)
        var titleIcon = new ContentPresenter
        {
            Width = 16, Height = 16,
            Margin = new Thickness(4, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed
        };
        UpdateTitleIcon(titleIcon, group.ActiveItem);

        var titleBlock = new TextBlock
        {
            Text              = group.ActiveItem?.Title ?? "",
            FontWeight        = FontWeights.SemiBold,
            FontSize          = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(4, 2, 8, 2)
        };
        titleBlock.SetResourceReference(TextBlock.ForegroundProperty, "DockTabTextBrush");

        // Update title and icon when the active tab changes, and track MRU
        tabControl.SelectionChanged += (_, _) =>
        {
            if (tabControl.SelectedItem is TabItem tab && tab.Tag is DockItem di)
            {
                titleBlock.Text = di.Title;
                UpdateTitleIcon(titleIcon, di);
                TrackActivation(di);
            }
        };

        // --- VS-style title bar buttons ---

        Button MakeTitleButton(string content, string tooltip)
        {
            var btn = new Button
            {
                Content    = content,
                FontSize   = 10,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                ToolTip    = tooltip
            };
            if (TryFindResource("DockTitleButtonStyle") is Style titleStyle)
                btn.Style = titleStyle;
            return btn;
        }

        // Close button (ChromeClose MDL2)
        var closeButton = MakeTitleButton("\uE8BB", "Close");
        closeButton.Click += (_, _) =>
        {
            var item = group.ActiveItem;
            if (item is null || !item.CanClose) return;
            RaiseTabCloseRequested(item);
            _engine?.Close(item);
            RebuildVisualTree();
        };

        // Pin button (Pin MDL2) — sends to auto-hide
        var pinButton = MakeTitleButton("\uE141", "Auto Hide");
        pinButton.Click += (_, _) =>
        {
            var item = group.ActiveItem;
            if (item is null) return;
            _engine?.AutoHide(item);
            RebuildVisualTree();
        };

        // Chevron dropdown (ChevronDown MDL2)
        var chevronButton = MakeTitleButton("\uE70D", "Options");
        chevronButton.Click += (sender, _) =>
        {
            var item = group.ActiveItem;
            if (item is null || sender is not Button btn) return;

            var menuBg     = TryFindResource("DockMenuBackgroundBrush") as Brush;
            var menuFg     = TryFindResource("DockMenuForegroundBrush") as Brush;
            var menuBorder = TryFindResource("DockMenuBorderBrush") as Brush;

            var menu = new ContextMenu
            {
                Background  = menuBg ?? Brushes.DarkGray,
                BorderBrush = menuBorder ?? Brushes.Gray,
                Foreground  = menuFg ?? Brushes.White
            };

            var floatMenuItem = new MenuItem { Header = "Float", Foreground = menuFg };
            floatMenuItem.Click += (_, _) => { CaptureDockedSizeForFloat(item); _engine?.Float(item); RebuildVisualTree(); };
            menu.Items.Add(floatMenuItem);

            var autoHideMenuItem = new MenuItem { Header = "Auto Hide", Foreground = menuFg };
            autoHideMenuItem.Click += (_, _) => { _engine?.AutoHide(item); RebuildVisualTree(); };
            menu.Items.Add(autoHideMenuItem);

            menu.Items.Add(new Separator());

            var closeMenuItem = new MenuItem
            {
                Header = "Close",
                Foreground = menuFg,
                IsEnabled = item.CanClose
            };
            closeMenuItem.Click += (_, _) =>
            {
                RaiseTabCloseRequested(item);
                _engine?.Close(item);
                RebuildVisualTree();
            };
            menu.Items.Add(closeMenuItem);

            menu.PlacementTarget = btn;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        };

        // Title bar layout: buttons right-aligned, title fills remaining space
        var titleContent = new DockPanel();
        DockPanel.SetDock(closeButton, Dock.Right);
        DockPanel.SetDock(pinButton, Dock.Right);
        DockPanel.SetDock(chevronButton, Dock.Right);
        titleContent.Children.Add(closeButton);
        titleContent.Children.Add(pinButton);
        titleContent.Children.Add(chevronButton);
        titleContent.Children.Add(titleIcon);
        titleContent.Children.Add(titleBlock);

        var titleBar = new Border
        {
            Child           = titleContent,
            Cursor          = Cursors.SizeAll,
            MinHeight       = 26,
            BorderThickness = new Thickness(0, 0, 0, 1)
        };
        titleBar.SetResourceReference(Border.BackgroundProperty, "DockMenuBackgroundBrush");
        titleBar.SetResourceReference(Border.BorderBrushProperty, "DockBorderBrush");

        // Drag threshold state (local to this title bar instance)
        var titleDragStart   = new Point();
        var titleDragPending = false;

        titleBar.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount >= 2)
            {
                // Double-click title bar: float the entire group (VS-style)
                var activeItem = group.ActiveItem;
                if (activeItem is not null && activeItem.CanFloat)
                {
                    CaptureDockedSizeForFloat(group);
                    _engine?.FloatGroup(group);
                    RebuildVisualTree();
                }
                return;
            }
            titleDragStart   = e.GetPosition(titleBar);
            titleDragPending = true;
            titleBar.CaptureMouse();
        };

        titleBar.MouseMove += (_, e) =>
        {
            if (!titleDragPending || e.LeftButton != MouseButtonState.Pressed) return;
            var diff = e.GetPosition(titleBar) - titleDragStart;
            if (Math.Abs(diff.X) <= DragThreshold &&
                Math.Abs(diff.Y) <= DragThreshold) return;

            titleDragPending = false;
            titleBar.ReleaseMouseCapture();
            var activeItem = group.ActiveItem;
            if (activeItem is null) return;
            _dragManager?.BeginGroupDrag(group, activeItem);
        };

        titleBar.MouseLeftButtonUp += (_, _) =>
        {
            titleDragPending = false;
            titleBar.ReleaseMouseCapture();
        };

        return titleBar;
    }

    /// <summary>
    /// Updates a title bar icon ContentPresenter to reflect the given item's Icon.
    /// </summary>
    private static void UpdateTitleIcon(ContentPresenter iconHost, DockItem? item)
    {
        if (item?.Icon is null)
        {
            iconHost.Visibility = Visibility.Collapsed;
            iconHost.Content = null;
            return;
        }
        iconHost.Content = item.Icon is ImageSource img
            ? new Image { Source = img, Width = 16, Height = 16, Stretch = Stretch.Uniform }
            : item.Icon;
        iconHost.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Activates a panel border (VS2026-style: 2px top accent only) and deactivates the previous one.
    /// Inactive panels have no visible border. Only the top edge of the active panel turns accent-colored.
    /// </summary>
    private void SetActivePanel(Border panelBorder)
    {
        if (_activePanel == panelBorder) return;

        if (_activePanel is not null)
            _activePanel.BorderThickness = new Thickness(0);

        _activePanel = panelBorder;
        _activePanel.BorderThickness = new Thickness(0, 2, 0, 0);
    }

    /// <summary>
    /// Wraps any UIElement in a panel border with rounded corners and a small margin (VS2026-style).
    /// No visible border initially; a 2px top accent appears when the panel is activated.
    /// Children are clipped to the rounded shape via a dynamic RectangleGeometry clip.
    /// </summary>
    private Border CreatePanelBorder(UIElement content)
    {
        const double radius = 6.0;
        var border = new Border
        {
            Child               = content,
            BorderThickness     = new Thickness(0),
            CornerRadius        = new CornerRadius(radius),
            Margin              = new Thickness(2),
            SnapsToDevicePixels = true
        };
        border.SetResourceReference(Border.BorderBrushProperty, "DockTabActiveBrush");

        // Clip children to the rounded rectangle so title bar / tab strip corners are clean
        border.SizeChanged += (_, e) =>
            border.Clip = new RectangleGeometry(new Rect(e.NewSize), radius, radius);

        // Activate on ANY mouse click inside the panel (tunneling catches handled events)
        border.AddHandler(
            UIElement.PreviewMouseDownEvent,
            new MouseButtonEventHandler((_, _) => SetActivePanel(border)),
            handledEventsToo: true);

        // Also activate on keyboard focus (e.g. Tab navigation)
        border.AddHandler(
            UIElement.GotKeyboardFocusEvent,
            new KeyboardFocusChangedEventHandler((_, _) => SetActivePanel(border)),
            handledEventsToo: true);

        return border;
    }

    /// <summary>
    /// Wires all events on a tab control via a disposable wirer (prevents leaks on rebuild).
    /// </summary>
    private void WireTabControlEvents(DockTabControl tabControl)
    {
        _tabWirers.Add(new DockTabEventWirer(tabControl, this));

        // Track MRU for document tabs (NavigatorWindow)
        tabControl.SelectionChanged += (_, _) =>
        {
            if (tabControl.SelectedItem is TabItem { Tag: DockItem di })
                TrackActivation(di);
        };
    }

    /// <summary>
    /// Moves the item to the front of the MRU activation history.
    /// </summary>
    internal void TrackActivation(DockItem item)
    {
        ActivationHistory.Remove(item);
        ActivationHistory.Insert(0, item);
        ActiveItemChanged?.Invoke(item);
    }

    /// <summary>
    /// Raises the <see cref="TabCloseRequested"/> event. Called by <see cref="DockTabEventWirer"/>.
    /// </summary>
    internal void RaiseTabCloseRequested(DockItem item)
    {
        if (BeforeCloseCallback is not null && !BeforeCloseCallback(item))
            return;
        TabCloseRequested?.Invoke(item);
    }

    private void OnItemFloated(DockItem item)
    {
        // Create a floating window for the item, positioned near the cursor (DIPs)
        var mousePos  = Mouse.GetPosition(this);
        var screenPos = PointToScreen(mousePos);
        var dipPos    = DpiHelper.ScreenToDip(this, screenPos);
        _floatingManager?.CreateFloatingWindow(item, new Point(dipPos.X - 50, dipPos.Y - 20));
    }

    private void OnItemDocked(DockItem item)
    {
        // Close the floating window if the item was re-docked
        _floatingManager?.CloseWindowForItem(item);
    }

    private void OnItemClosed(DockItem item)
    {
        // Evict from internal content cache so a future item with the same ContentId
        // gets a fresh content instance instead of a stale (potentially Unloaded) one.
        _contentCache.Remove(item.ContentId);

        // Close the floating window if the item was closed
        _floatingManager?.CloseWindowForItem(item);
    }

    private void OnItemHidden(DockItem item)
    {
        // Close the floating window if the item was hidden
        _floatingManager?.CloseWindowForItem(item);
    }

    private void OnGroupFloated(DockGroupNode floatingGroup)
    {
        var mousePos  = Mouse.GetPosition(this);
        var screenPos = PointToScreen(mousePos);
        var dipPos    = DpiHelper.ScreenToDip(this, screenPos);
        _floatingManager?.CreateFloatingWindowForGroup(floatingGroup,
            new Point(dipPos.X - 50, dipPos.Y - 20));
    }

    /// <summary>
    /// Updates the auto-hide bars with current auto-hide items, distributed by LastDockSide.
    /// </summary>
    private void UpdateAutoHideBars()
    {
        if (Layout is null) return;

        _autoHideLeft.UpdateItems(Layout.AutoHideItems.Where(i => i.LastDockSide == Core.DockSide.Left));
        _autoHideRight.UpdateItems(Layout.AutoHideItems.Where(i => i.LastDockSide == Core.DockSide.Right));
        _autoHideTop.UpdateItems(Layout.AutoHideItems.Where(i => i.LastDockSide == Core.DockSide.Top));
        _autoHideBottom.UpdateItems(Layout.AutoHideItems.Where(i => i.LastDockSide == Core.DockSide.Bottom));
    }

    private void OnAutoHideItemClicked(DockItem item)
    {
        if (_autoHideFlyout.IsOpen && _autoHideFlyout.CurrentItem == item)
        {
            // Toggle off
            _autoHideFlyout.Close();
            return;
        }

        _autoHideFlyout.ShowForItem(item, CachedContentFactory, item.LastDockSide);
    }

    private void OnAutoHideRestoreRequested(DockItem item)
    {
        _autoHideFlyout.Close();

        if (_engine is null || Layout is null) return;

        // Re-dock to original side
        var direction = item.LastDockSide switch
        {
            Core.DockSide.Left => DockDirection.Left,
            Core.DockSide.Right => DockDirection.Right,
            Core.DockSide.Top => DockDirection.Top,
            Core.DockSide.Bottom => DockDirection.Bottom,
            _ => DockDirection.Bottom
        };

        _engine.RestoreFromAutoHide(item, Layout.MainDocumentHost, direction);
        RebuildVisualTree();
    }

    private void OnAutoHideCloseRequested(DockItem item)
    {
        _autoHideFlyout.Close();

        if (_engine is null) return;

        _engine.Close(item);
        RebuildVisualTree();
    }

    private void OnAutoHideFloatRequested(DockItem item)
    {
        _autoHideFlyout.Close();

        if (_engine is null || Layout is null) return;

        // Remove from auto-hide, then float
        _engine.RestoreFromAutoHide(item, Layout.MainDocumentHost, DockDirection.Center);
        _engine.Float(item);
        RebuildVisualTree();
    }
}
