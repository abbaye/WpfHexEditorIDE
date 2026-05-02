// ==========================================================
// Project: WpfHexEditor.Shell
// File: DockControl.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     The central WPF control of the docking system. Renders the DockLayoutRoot
//     node tree as WPF visual elements, and coordinates drag & drop, floating
//     windows, auto-hide bars, keyboard navigation, and tab event wiring.
//
// Architecture Notes:
//     Implements IDockHost and IDisposable. Acts as the Facade over DockEngine,
//     DockDragManager, FloatingWindowManager, and DockKeyboardNavigation.
//     Content factory pattern: consumers register factories keyed by string IDs
//     to produce content for DockItem nodes on demand.
//
// ==========================================================

using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using WpfHexEditor.Docking.Core;
using WpfHexEditor.Docking.Core.Commands;
using WpfHexEditor.Docking.Core.Nodes;
using WpfHexEditor.Shell.Commands;
using WpfHexEditor.Shell.Controls;
using DCore = WpfHexEditor.Docking.Core;

namespace WpfHexEditor.Shell;

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
    private readonly List<DockTabControl>    _managedTabControls = [];
    private readonly Dictionary<string, object> _contentCache = new();

    // Batch-close support: suppresses intermediate rebuilds during multi-item close operations.
    private bool _suppressRebuild;
    private bool _rebuildPending;

    // M2.1 — incremental visual tree: tab controls cached by group node.
    private readonly Dictionary<DockGroupNode, DockTabControl> _tabControlCache = new();
    private bool _pendingIncrementalHandled;

    // Tab-switch dedup: prevents triple-fire from SelectionChanged + GotKeyboardFocus + GotFocus.
    private DockItem? _lastActivatedItem;

    // M2.4 — WeakEvent fields: stored so DetachEngine can unsubscribe.
    private Action? _weLayoutChanged;
    private Action<DockItem>? _weItemFloated;
    private Action<DockItem>? _weItemDocked;
    private Action<DockItem>? _weItemClosed;
    private Action<DockItem>? _weItemHidden;
    private Action<DockGroupNode>? _weGroupFloated;
    private Action<DockItem, DockGroupNode>? _weItemAddedToGroup;
    private Action<DockItem, DockGroupNode>? _weItemRemovedFromGroup;

    // M3.3 — undo/redo layout.
    public DockCommandStack CommandStack { get; } = new();
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

    // All overlay borders currently in the visual tree (cleared + repopulated on every RebuildVisualTree).
    private readonly List<Border> _panelBorders = [];
    // All outer Grids that carry a rounded clip (cleared + repopulated on every RebuildVisualTree).
    private readonly List<FrameworkElement> _panelClipElements = [];

    /// <summary>
    /// Controls the visual highlight style applied to the active panel container.
    /// Use <see cref="ApplyHighlightMode"/> for live updates (re-renders the current active panel).
    /// </summary>
    public ActivePanelHighlightMode PanelHighlightMode { get; set; } = ActivePanelHighlightMode.TopBar;

    /// <summary>Corner radius in px applied to every panel overlay border. Default 4.</summary>
    public double PanelCornerRadius { get; set; } = 4.0;

    // Bitmap snapshots captured when an auto-hide flyout is dismissed,
    // keyed by DockItem so AutoHideBarHoverPreview can display them.
    private readonly Dictionary<DockItem, System.Windows.Media.Imaging.BitmapSource> _autoHideBitmapCache = new();

    // Tab hover-preview settings shared across all TabHoverPreview instances.
    private readonly List<TabHoverPreview> _tabPreviews = [];

    /// <summary>
    /// Live settings for the tab hover-preview thumbnail popup.
    /// Mutate properties then call <see cref="RefreshTabPreviewSettings"/> to apply.
    /// </summary>
    public TabPreviewSettings TabPreviewSettings { get; } = new();

    /// <summary>
    /// Configurable timing for auto-hide flyout. Mutate then rebuild visual tree to apply.
    /// </summary>
    public AutoHideSettings AutoHideSettings { get; } = new();

    /// <summary>
    /// Optional workspace facade for profile quick-save/load commands.
    /// </summary>
    public DockWorkspace? Workspace { get; set; }

    /// <summary>
    /// Applies animation timing settings to the internal animation helper.
    /// Call after changing settings to take effect immediately.
    /// </summary>
    public void ApplyAnimationSettings(bool enabled, int overlayFadeInMs, int overlayFadeOutMs, int floatingFadeInMs)
    {
        DockAnimationHelper.AnimationsEnabled = enabled;
        DockAnimationHelper.OverlayFadeInMs   = overlayFadeInMs;
        DockAnimationHelper.OverlayFadeOutMs  = overlayFadeOutMs;
        DockAnimationHelper.FloatingFadeInMs  = floatingFadeInMs;
    }

    public static readonly DependencyProperty LayoutProperty =
        DependencyProperty.Register(
            nameof(Layout),
            typeof(DockLayoutRoot),
            typeof(DockControl),
            new PropertyMetadata(null, OnLayoutChanged));

    public static readonly DependencyProperty TabBarSettingsProperty =
        DependencyProperty.Register(
            nameof(TabBarSettings),
            typeof(DocumentTabBarSettings),
            typeof(DockControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ShowGroupNumberBadgeProperty =
        DependencyProperty.Register(
            nameof(ShowGroupNumberBadge),
            typeof(bool),
            typeof(DockControl),
            new PropertyMetadata(false, (d, _) => ((DockControl)d).AssignGroupBadges()));

    /// <summary>
    /// When <see langword="true"/>, a "Group N" badge is overlaid on each document tab bar
    /// while multiple tab groups are open. Default: <see langword="false"/>.
    /// </summary>
    public bool ShowGroupNumberBadge
    {
        get => (bool)GetValue(ShowGroupNumberBadgeProperty);
        set => SetValue(ShowGroupNumberBadgeProperty, value);
    }

    /// <summary>
    /// Settings for the document tab bar (placement, multi-row, colorization, etc.).
    /// Shared with the active <see cref="DocumentTabHost"/> and serialized in the layout.
    /// </summary>
    public DocumentTabBarSettings? TabBarSettings
    {
        get => (DocumentTabBarSettings?)GetValue(TabBarSettingsProperty);
        set => SetValue(TabBarSettingsProperty, value);
    }

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

    // --- MVVM Source Binding -----------------------------------------

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

    // --- Content Factory ---------------------------------------------

    /// <summary>
    /// Factory to create content for a DockItem. If not set, a default placeholder is shown.
    /// </summary>
    public Func<DockItem, object>? ContentFactory { get; set; }

    /// <summary>
    /// Optional factory injected by the application shell to add extra context-menu items
    /// to every tab (e.g. "Compare with…").  Forwarded to each <see cref="DockTabControl"/>
    /// when it is created.
    /// </summary>
    public Func<DockItem, IReadOnlyList<MenuItem>>? TabExtraMenuItemsFactory { get; set; }

    /// <summary>
    /// Optional async content factory. When set and <see cref="ContentFactory"/> is null,
    /// tabs show an indeterminate progress bar until the async factory completes.
    /// </summary>
    public Func<DockItem, Task<object>>? AsyncContentFactory { get; set; }

    // --- Layout Item Templates --------------------------------------

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

    // --- Cached Content Factory -------------------------------------

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
        _autoHideFlyout = new AutoHideFlyout { Settings = AutoHideSettings };
        _autoHideFlyout.SnapshotReady    += CaptureAutoHideSnapshot;  // primary: full-size, fully-painted panel
        _autoHideFlyout.Dismissing       += CaptureAutoHideSnapshot;  // fallback: covers quick dismiss before animation ends
        _autoHideFlyout.Dismissing       += () => ClearAllAutoHideBarHighlights();
        _autoHideFlyout.RestoreRequested += OnAutoHideRestoreRequested;
        _autoHideFlyout.CloseRequested   += OnAutoHideCloseRequested;
        _autoHideFlyout.FloatRequested   += OnAutoHideFloatRequested;
        _centerHost = new ContentControl();

        _autoHideLeft.GroupClicked   += OnAutoHideGroupClicked;
        _autoHideRight.GroupClicked  += OnAutoHideGroupClicked;
        _autoHideTop.GroupClicked    += OnAutoHideGroupClicked;
        _autoHideBottom.GroupClicked += OnAutoHideGroupClicked;

        _autoHideLeft.GroupFloatRequested   += OnAutoHideGroupFloatRequested;
        _autoHideRight.GroupFloatRequested  += OnAutoHideGroupFloatRequested;
        _autoHideTop.GroupFloatRequested    += OnAutoHideGroupFloatRequested;
        _autoHideBottom.GroupFloatRequested += OnAutoHideGroupFloatRequested;

        _autoHideLeft.GroupCloseRequested   += OnAutoHideGroupCloseRequested;
        _autoHideRight.GroupCloseRequested  += OnAutoHideGroupCloseRequested;
        _autoHideTop.GroupCloseRequested    += OnAutoHideGroupCloseRequested;
        _autoHideBottom.GroupCloseRequested += OnAutoHideGroupCloseRequested;

        // Thumbnail hover previews intentionally disabled (flash/activation issues).
        // AutoHideBarHoverPreview class and _autoHideBitmapCache kept for future re-enable.

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
                    DCore.DockSide.Left   => DockDirection.Left,
                    DCore.DockSide.Right  => DockDirection.Right,
                    DCore.DockSide.Top    => DockDirection.Top,
                    DCore.DockSide.Bottom => DockDirection.Bottom,
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
            (_, _) => ExecuteUndoable("Auto Hide All", () => { _engine?.AutoHideAll(); RebuildVisualTree(); }),
            (_, e) => e.CanExecute = _engine is not null));

        CommandBindings.Add(new CommandBinding(
            DockCommands.RestoreAll,
            (_, _) => ExecuteUndoable("Restore All", () => { _engine?.RestoreAllFromAutoHide(); RebuildVisualTree(); }),
            (_, e) => e.CanExecute = _engine is not null && Layout?.AutoHideItems.Any() == true));

        // M3.3 — Undo/Redo layout via Ctrl+Shift+Z / Ctrl+Shift+Y
        CommandBindings.Add(new CommandBinding(
            DockCommands.UndoLayout,
            (_, _) => { CommandStack.Undo(); RebuildVisualTree(); },
            (_, e) => e.CanExecute = CommandStack.CanUndo));

        CommandBindings.Add(new CommandBinding(
            DockCommands.RedoLayout,
            (_, _) => { CommandStack.Redo(); RebuildVisualTree(); },
            (_, e) => e.CanExecute = CommandStack.CanRedo));

        CommandBindings.Add(new CommandBinding(
            DockCommands.NewVerticalTabGroup,
            (_, _) =>
            {
                if (GetActiveDocumentItem() is { } item)
                    HandleNewTabGroup(item, DockDirection.Right);
            },
            (_, e) => e.CanExecute = GetActiveDocumentItem() is not null));

        CommandBindings.Add(new CommandBinding(
            DockCommands.NewHorizontalTabGroup,
            (_, _) =>
            {
                if (GetActiveDocumentItem() is { } item)
                    HandleNewTabGroup(item, DockDirection.Bottom);
            },
            (_, e) => e.CanExecute = GetActiveDocumentItem() is not null));

        CommandBindings.Add(new CommandBinding(
            DockCommands.MoveToNextTabGroup,
            (_, _) =>
            {
                if (GetActiveDocumentItem() is { } item)
                    HandleMoveToAdjacentGroup(item, forward: true);
            },
            (_, e) => e.CanExecute = GetActiveDocumentItem() is not null
                && (Layout?.GetAllDocumentHosts().Skip(1).Any() ?? false)));

        CommandBindings.Add(new CommandBinding(
            DockCommands.MoveToPreviousTabGroup,
            (_, _) =>
            {
                if (GetActiveDocumentItem() is { } item)
                    HandleMoveToAdjacentGroup(item, forward: false);
            },
            (_, e) => e.CanExecute = GetActiveDocumentItem() is not null
                && (Layout?.GetAllDocumentHosts().Skip(1).Any() ?? false)));

        CommandBindings.Add(new CommandBinding(
            DockCommands.CloseAllTabGroups,
            (_, _) => HandleCloseAllTabGroups(),
            (_, e) => e.CanExecute = Layout?.GetAllDocumentHosts().Skip(1).Any() ?? false));

        CommandBindings.Add(new CommandBinding(
            DockCommands.PinTab,
            (_, _) =>
            {
                if (GetActiveDocumentItem() is not { } item) return;
                item.IsPinned = !item.IsPinned;
                // Reorder pinned tabs first in the group
                if (item.Owner is { } group)
                {
                    var current = group.Items.ToList();
                    var ordered = current.Where(i => i.IsPinned)
                        .Concat(current.Where(i => !i.IsPinned)).ToList();
                    if (!current.SequenceEqual(ordered))
                    {
                        var active = group.ActiveItem;
                        foreach (var i in current) group.RemoveItem(i);
                        foreach (var i in ordered) group.AddItem(i);
                        if (active is not null) group.ActiveItem = active;
                    }
                }
                RebuildVisualTree();
            },
            (_, e) => e.CanExecute = GetActiveDocumentItem() is not null));

        CommandBindings.Add(new CommandBinding(
            DockCommands.QuickWindowSearch,
            (_, _) => ShowQuickWindowSearch(),
            (_, e) => e.CanExecute = Layout is not null));

        // Quick layout profile save/load (Ctrl+Shift+1..4 / Ctrl+Alt+1..4)
        var saveCommands = new[] { DockCommands.QuickSaveLayout1, DockCommands.QuickSaveLayout2, DockCommands.QuickSaveLayout3, DockCommands.QuickSaveLayout4 };
        var loadCommands = new[] { DockCommands.QuickLoadLayout1, DockCommands.QuickLoadLayout2, DockCommands.QuickLoadLayout3, DockCommands.QuickLoadLayout4 };
        for (int i = 0; i < 4; i++)
        {
            var slot = i + 1;
            CommandBindings.Add(new CommandBinding(
                saveCommands[i],
                (_, _) => Workspace?.SaveQuickProfile(slot),
                (_, e) => e.CanExecute = Layout is not null));
            CommandBindings.Add(new CommandBinding(
                loadCommands[i],
                (_, _) => { if (Workspace?.LoadQuickProfile(slot) == true) RebuildVisualTree(); },
                (_, e) => e.CanExecute = Layout is not null));
        }
    }

    /// <summary>
    /// Wraps an action in a <see cref="LayoutSnapshotCommand"/> and executes it through
    /// <see cref="CommandStack"/> so the operation is undoable via Ctrl+Shift+Z.
    /// </summary>
    private void ExecuteUndoable(string description, Action action)
    {
        if (Layout is null)
        {
            action();
            return;
        }

        var cmd = new LayoutSnapshotCommand(
            description,
            () => Layout,
            newLayout => { Layout = newLayout; RebuildVisualTree(); },
            action);

        CommandStack.Execute(cmd);
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
                // Sync tab-bar settings: ensure layout and control share the same instance.
                var settings = newLayout.TabBarSettings ?? new DocumentTabBarSettings();
                newLayout.TabBarSettings = settings;
                control.TabBarSettings = settings;

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
        DetachEngine();

        // M2.4 — WeakEvent: lambdas capture WeakReference<DockControl> so the engine
        // cannot prevent GC of this control. Stored in fields for later unsubscription.
        var weak = new WeakReference<DockControl>(this);

        _weLayoutChanged        = () => { if (weak.TryGetTarget(out var t)) t.OnLayoutTreeChanged(); };
        _weItemFloated          = i  => { if (weak.TryGetTarget(out var t)) t.OnItemFloated(i); };
        _weItemDocked           = i  => { if (weak.TryGetTarget(out var t)) t.OnItemDocked(i); };
        _weItemClosed           = i  => { if (weak.TryGetTarget(out var t)) t.OnItemClosed(i); };
        _weItemHidden           = i  => { if (weak.TryGetTarget(out var t)) t.OnItemHidden(i); };
        _weGroupFloated         = g  => { if (weak.TryGetTarget(out var t)) t.OnGroupFloated(g); };
        _weItemAddedToGroup     = (i, g) => { if (weak.TryGetTarget(out var t)) t.OnItemAddedToGroup(i, g); };
        _weItemRemovedFromGroup = (i, g) => { if (weak.TryGetTarget(out var t)) t.OnItemRemovedFromGroup(i, g); };

        _engine.LayoutChanged        += _weLayoutChanged;
        _engine.ItemFloated          += _weItemFloated;
        _engine.ItemDocked           += _weItemDocked;
        _engine.ItemClosed           += _weItemClosed;
        _engine.ItemHidden           += _weItemHidden;
        _engine.GroupFloated         += _weGroupFloated;
        _engine.ItemAddedToGroup     += _weItemAddedToGroup;
        _engine.ItemRemovedFromGroup += _weItemRemovedFromGroup;
    }

    /// <summary>
    /// Unsubscribes all engine event handlers to prevent leaks.
    /// </summary>
    private void DetachEngine()
    {
        if (_engine is null) return;
        _engine.LayoutChanged        -= _weLayoutChanged;
        _engine.ItemFloated          -= _weItemFloated;
        _engine.ItemDocked           -= _weItemDocked;
        _engine.ItemClosed           -= _weItemClosed;
        _engine.ItemHidden           -= _weItemHidden;
        _engine.GroupFloated         -= _weGroupFloated;
        _engine.ItemAddedToGroup     -= _weItemAddedToGroup;
        _engine.ItemRemovedFromGroup -= _weItemRemovedFromGroup;

        _weLayoutChanged = null;
        _weItemFloated = _weItemDocked = _weItemClosed = _weItemHidden = null;
        _weGroupFloated = null;
        _weItemAddedToGroup = _weItemRemovedFromGroup = null;
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
        _autoHideFlyout.SnapshotReady    -= CaptureAutoHideSnapshot;
        _autoHideFlyout.Dismissing       -= CaptureAutoHideSnapshot;
        _autoHideFlyout.RestoreRequested -= OnAutoHideRestoreRequested;
        _autoHideFlyout.CloseRequested   -= OnAutoHideCloseRequested;
        _autoHideFlyout.FloatRequested   -= OnAutoHideFloatRequested;
        GC.SuppressFinalize(this);
    }

    private void OnLayoutTreeChanged()
    {
        Dispatcher.Invoke(() =>
        {
            if (_pendingIncrementalHandled)
            {
                // M2.1: incremental handler already updated the tab strip — only refresh auto-hide bars.
                _pendingIncrementalHandled = false;
                UpdateAutoHideBars();
                AssignGroupBadges();
                return;
            }
            RebuildVisualTree();
        });
    }

    // M2.1 — incremental add: find cached DockTabControl, add one tab, no full rebuild.
    private void OnItemAddedToGroup(DockItem item, DockGroupNode group)
    {
        Dispatcher.Invoke(() =>
        {
            if (!_tabControlCache.TryGetValue(group, out var tabControl)) return;

            // Clear start placeholder in DocumentTabHost on first real document.
            if (tabControl is DocumentTabHost docHost)
                docHost.ClearEmptyPlaceholder();

            tabControl.AddTab(item);
            _pendingIncrementalHandled = true;
        });
    }

    // M2.1 — incremental remove: find cached DockTabControl, remove one tab, no full rebuild.
    private void OnItemRemovedFromGroup(DockItem item, DockGroupNode group)
    {
        Dispatcher.Invoke(() =>
        {
            if (!_tabControlCache.TryGetValue(group, out var tabControl)) return;
            tabControl.RemoveTab(item);
            _pendingIncrementalHandled = true;
        });
    }

    // --- Layout size sync -------------------------------------------

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
        if (size.HasValue) ApplySizeToFloatDimensions(item, size.Value);
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
        if (size.HasValue) ApplySizeToFloatDimensions(activeItem, size.Value);
    }

    /// <summary>
    /// Applies only the panel's "compact" dimension to FloatWidth/FloatHeight.
    /// Left/Right panels span the full window height — only Width is meaningful.
    /// Top/Bottom panels span the full window width — only Height is meaningful.
    /// </summary>
    private static void ApplySizeToFloatDimensions(DockItem item, Size size)
    {
        switch (item.LastDockSide)
        {
            case DCore.DockSide.Left:
            case DCore.DockSide.Right:
                if (size.Width  > 0) item.FloatWidth  = size.Width;
                break;
            case DCore.DockSide.Top:
            case DCore.DockSide.Bottom:
                if (size.Height > 0) item.FloatHeight = size.Height;
                break;
            default:
                if (size.Width  > 0) item.FloatWidth  = size.Width;
                if (size.Height > 0) item.FloatHeight = size.Height;
                break;
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
    /// Evicts a single item from the internal content cache so the next
    /// <see cref="RebuildVisualTree"/> call will invoke <see cref="ContentFactory"/> again
    /// for that item. Use this when a plugin replaces a previously-rendered placeholder.
    /// </summary>
    public void InvalidateContent(string contentId)
        => _contentCache.Remove(contentId);

    /// <summary>
    /// Pushes the current <see cref="TabPreviewSettings"/> values to all active
    /// <see cref="TabHoverPreview"/> instances. Call after mutating <see cref="TabPreviewSettings"/>.
    /// </summary>
    public void RefreshTabPreviewSettings()
    {
        foreach (var p in _tabPreviews)
            p.ApplySettings();
    }

    /// <summary>
    /// Enters batch-close mode: subsequent <see cref="RebuildVisualTree"/> calls are deferred
    /// until <see cref="EndBatchClose"/> is called, preventing mid-iteration wirer disposal.
    /// </summary>
    internal void BeginBatchClose() => _suppressRebuild = true;

    /// <summary>
    /// Exits batch-close mode and triggers a single deferred <see cref="RebuildVisualTree"/>
    /// if any rebuild was requested while suppressed.
    /// </summary>
    internal void EndBatchClose()
    {
        _suppressRebuild = false;
        if (!_rebuildPending) return;
        _rebuildPending = false;
        RebuildVisualTree();
    }

    // ── Document Tab Group Operations ──────────────────────────────────

    /// <summary>
    /// Splits the document area by moving <paramref name="item"/> into a new
    /// DocumentHostNode beside the current one.
    /// </summary>
    public void HandleNewTabGroup(DockItem item, DockDirection direction)
    {
        if (_engine is null || item.Owner is not DocumentHostNode docHost) return;
        _engine.SplitDocumentHost(item, docHost, direction);
        RebuildVisualTree();
    }

    /// <summary>
    /// Moves <paramref name="item"/> to the next (or previous) DocumentHostNode.
    /// </summary>
    public void HandleMoveToAdjacentGroup(DockItem item, bool forward)
    {
        if (_engine is null || Layout is null || item.Owner is not DocumentHostNode currentHost) return;
        var hosts = Layout.GetAllDocumentHosts().ToList();
        if (hosts.Count < 2) return;
        var idx = hosts.IndexOf(currentHost);
        if (idx < 0) return;
        var targetIdx = forward ? (idx + 1) % hosts.Count : (idx - 1 + hosts.Count) % hosts.Count;
        _engine.MoveItem(item, hosts[targetIdx]);
        RebuildVisualTree();
    }

    /// <summary>
    /// Closes the document tab group that contains <paramref name="item"/>
    /// by moving all its tabs to the main document host.
    /// </summary>
    public void HandleCloseTabGroup(DockItem item)
    {
        if (_engine is null || Layout is null || item.Owner is not DocumentHostNode docHost) return;
        if (docHost.IsMain) return; // Cannot close the main document host
        var mainHost = Layout.MainDocumentHost;
        _engine.BeginTransaction();
        foreach (var tabItem in docHost.Items.ToList())
            _engine.MoveItem(tabItem, mainHost);
        _engine.CommitTransaction();
        RebuildVisualTree();
    }

    /// <summary>
    /// Closes all non-main document tab groups, merging their tabs into the main host.
    /// </summary>
    public void HandleCloseAllTabGroups()
    {
        if (_engine is null || Layout is null) return;
        var nonMainHosts = Layout.GetAllDocumentHosts().Where(h => !h.IsMain).ToList();
        if (nonMainHosts.Count == 0) return;
        var mainHost = Layout.MainDocumentHost;
        _engine.BeginTransaction();
        foreach (var host in nonMainHosts)
            foreach (var tabItem in host.Items.ToList())
                _engine.MoveItem(tabItem, mainHost);
        _engine.CommitTransaction();
        RebuildVisualTree();
    }

    /// <summary>
    /// Gives keyboard focus to the tab control rendering <paramref name="host"/>.
    /// </summary>
    public void FocusDocumentHost(DocumentHostNode host)
    {
        if (_tabControlCache.TryGetValue(host, out var tabControl))
            tabControl.Focus();
    }

    /// <summary>
    /// Returns the active item in the focused document host, or falls back to any host.
    /// </summary>
    public DockItem? GetActiveDocumentItem() =>
        Layout?.MainDocumentHost.ActiveItem
        ?? Layout?.GetAllDocumentHosts().Select(h => h.ActiveItem).FirstOrDefault(i => i is not null);

    /// <summary>
    /// Opens the Quick Window Search popup (Ctrl+Shift+A) showing all known panels.
    /// </summary>
    private void ShowQuickWindowSearch()
    {
        if (Layout is null) return;

        // Collect all items: docked groups + floating + auto-hide + hidden
        var allItems = new List<DockItem>();
        foreach (var group in Layout.GetAllGroups())
            allItems.AddRange(group.Items);
        allItems.AddRange(Layout.FloatingItems);
        allItems.AddRange(Layout.AutoHideItems);
        allItems.AddRange(Layout.HiddenItems);

        // Deduplicate by Id
        var seen = new HashSet<Guid>();
        var deduped = new List<DockItem>();
        foreach (var item in allItems)
        {
            if (seen.Add(item.Id))
                deduped.Add(item);
        }

        var popup = new QuickWindowSearchWindow(deduped);

        // Center over the dock control
        var screenPos = PointToScreen(new Point(ActualWidth / 2 - 200, ActualHeight / 4));
        popup.Left = screenPos.X;
        popup.Top = screenPos.Y;
        popup.ShowDialog();

        if (popup.SelectedItem is { } selected)
        {
            // Activate the selected item
            if (selected.State == DockItemState.Hidden && _engine is not null)
            {
                _engine.Show(selected);
                RebuildVisualTree();
            }
            else if (selected.State == DockItemState.AutoHide && _engine is not null)
            {
                _engine.RestoreFromAutoHide(selected);
                RebuildVisualTree();
            }
            else if (selected.Owner is { } group)
            {
                group.ActiveItem = selected;
                RebuildVisualTree();
            }
            TrackActivation(selected);
        }
    }

    // ── End Document Tab Group Operations ───────────────────────────────

    /// <summary>
    /// Rebuilds the entire visual tree from the current Layout.
    /// </summary>
    public void RebuildVisualTree()
    {
        if (_suppressRebuild) { _rebuildPending = true; return; }

        _lastActivatedItem = null;  // reset dedup after layout change

        // Dispose previous tab wirers to prevent event leaks
        DisposeWirers();
        _tabControlCache.Clear();  // M2.1: clear stale group→tab mappings
        _panelBorders.Clear();
        _panelClipElements.Clear();
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

        // Restore focus to the previously active tab's content after visual tree rebuild
        if (ActivationHistory.Count > 0)
        {
            var lastActive = ActivationHistory[0];
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
            {
                if (lastActive.Owner is { } group && _tabControlCache.TryGetValue(group, out var tc))
                {
                    if (tc.SelectedItem is System.Windows.Controls.TabItem tab)
                        tab.Focus();
                }
            });
        }

        AssignGroupBadges();
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
        var host = new DocumentTabHost
        {
            Settings = TabBarSettings ?? new DocumentTabBarSettings()
        };

        host.Bind(docHost, CachedContentFactory);
        _managedTabControls.Add(host);
        host.ApplyHighlightMode(PanelHighlightMode);

        WireTabControlEvents(host);
        _tabPreviews.Add(TabHoverPreview.Attach(host, TabPreviewSettings));
        _tabControlCache[docHost] = host;  // M2.1: register for incremental updates

        // Group badge overlay — visible only when ShowGroupBadge == true
        var badgeBorder = new Border
        {
            CornerRadius        = new CornerRadius(8),
            Padding             = new Thickness(6, 1, 6, 1),
            Margin              = new Thickness(0, 4, 42, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment   = VerticalAlignment.Top,
            IsHitTestVisible    = true,
            Opacity             = 0.35,
            Cursor              = System.Windows.Input.Cursors.Arrow,
        };
        badgeBorder.SetResourceReference(Border.BackgroundProperty, "TG_BadgeBackgroundBrush");

        var fadeIn  = new System.Windows.Media.Animation.DoubleAnimation(0.85, TimeSpan.FromMilliseconds(120));
        var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(0.35, TimeSpan.FromMilliseconds(200));
        badgeBorder.MouseEnter += (_, _) => badgeBorder.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        badgeBorder.MouseLeave += (_, _) => badgeBorder.BeginAnimation(UIElement.OpacityProperty, fadeOut);

        badgeBorder.ContextMenu = BuildBadgeContextMenu(docHost);

        var badgeText = new TextBlock
        {
            FontSize   = 10,
            FontWeight = FontWeights.SemiBold,
        };
        badgeText.SetResourceReference(TextBlock.ForegroundProperty, "TG_BadgeForegroundBrush");
        badgeBorder.Child = badgeText;

        // Bind badge visibility and text to host DPs
        badgeBorder.SetBinding(UIElement.VisibilityProperty, new System.Windows.Data.Binding(nameof(DocumentTabHost.ShowGroupBadge))
        {
            Source    = host,
            Converter = new System.Windows.Controls.BooleanToVisibilityConverter(),
        });
        badgeText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(DocumentTabHost.GroupIndex))
        {
            Source      = host,
            StringFormat = "Group {0}",
        });

        var grid = new Grid();
        grid.Children.Add(host);
        grid.Children.Add(badgeBorder);

        var r = PanelCornerRadius;

        // Overlay border: covers the content area only (below the top tab strip).
        // CornerRadius applied directly so the highlight border itself is rounded.
        var overlayBorder = new Border
        {
            BorderThickness     = new Thickness(0),
            CornerRadius        = new CornerRadius(r),
            IsHitTestVisible    = false,
            SnapsToDevicePixels = true,
        };
        overlayBorder.SetResourceReference(Border.BorderBrushProperty, "DockTabActiveBrush");

        var outer = new Grid { Margin = new Thickness(2) };
        outer.Children.Add(grid);          // z=0
        outer.Children.Add(overlayBorder); // z=1

        host.Loaded += (_, _) =>
        {
            host.ApplyTemplate();
            if (host.Template?.FindName("PART_TabStrip", host) is not FrameworkElement tabStrip)
                return;

            const double gapH = 2.0;
            const double bt   = 1.0;

            void UpdateOverlay()
            {
                double tabH = tabStrip.ActualHeight;
                double w    = outer.ActualWidth;
                double h    = outer.ActualHeight - tabH;

                overlayBorder.Margin = new Thickness(0, tabH, 0, 0);

                if (w <= 0 || h <= 0) { overlayBorder.Clip = null; return; }

                // Overlay origin is at (0, tabH) in outer coords; clip rect is relative to overlay.
                var contentArea = new RectangleGeometry(new Rect(0, 0, w, h));

                int selIdx = host.SelectedIndex;
                if (selIdx >= 0 &&
                    host.ItemContainerGenerator.ContainerFromIndex(selIdx) is FrameworkElement activeTab)
                {
                    // Translate tab position to overlay coordinates (subtract tabH from Y).
                    var pos   = activeTab.TranslatePoint(new Point(0, 0), outer);
                    double gapX = Math.Max(0, pos.X);
                    double gapW = activeTab.ActualWidth;
                    if (gapW > 0)
                    {
                        double gX = gapX + bt;
                        double gW = Math.Max(0, gapW - bt * 2);
                        // Gap at y=0 (top of overlay = separator between tab strip and content).
                        var gap = new RectangleGeometry(new Rect(gX, -gapH, gW, gapH * 2));
                        overlayBorder.Clip = new CombinedGeometry(GeometryCombineMode.Exclude, contentArea, gap);
                        return;
                    }
                }

                overlayBorder.Clip = contentArea;
            }

            tabStrip.SizeChanged      += (_, _) => UpdateOverlay();
            outer.SizeChanged         += (_, _) => UpdateOverlay();
            host.SelectionChanged     += (_, _) => UpdateOverlay();
            host.LayoutUpdated        += (_, _) => UpdateOverlay();
            UpdateOverlay();
        };

        outer.AddHandler(
            UIElement.PreviewMouseDownEvent,
            new MouseButtonEventHandler((_, _) =>
            {
                SetActivePanel(overlayBorder);
                // Fire TrackActivation so ActiveItemChanged reaches all plugins
                // (ParsedFields, etc.) when the user clicks into a tab group without
                // switching tabs — SelectionChanged alone misses this cross-group focus.
                if (host.SelectedItem is TabItem { Tag: DockItem di })
                    TrackActivation(di);
            }),
            handledEventsToo: true);
        outer.AddHandler(
            UIElement.GotKeyboardFocusEvent,
            new KeyboardFocusChangedEventHandler((_, _) =>
            {
                SetActivePanel(overlayBorder);
                if (host.SelectedItem is TabItem { Tag: DockItem di })
                    TrackActivation(di);
            }),
            handledEventsToo: true);

        _panelBorders.Add(overlayBorder);
        return outer;
    }

    /// <summary>
    /// Assigns group number badges to all DocumentTabHost controls based on current layout.
    /// Called after RebuildVisualTree() and on LayoutChanged.
    /// Zero layout invalidation — only mutates DependencyProperty values.
    /// </summary>
    private void AssignGroupBadges()
    {
        if (Layout is null) return;
        var hosts = Layout.GetAllDocumentHosts().ToList();
        bool showBadge = ShowGroupNumberBadge && hosts.Count > 1;

        for (int i = 0; i < hosts.Count; i++)
        {
            if (_tabControlCache.TryGetValue(hosts[i], out var tabControl)
                && tabControl is DocumentTabHost docTabHost)
            {
                docTabHost.GroupIndex     = i + 1;
                docTabHost.ShowGroupBadge = showBadge;
            }
        }
    }

    private ContextMenu BuildBadgeContextMenu(DocumentHostNode docHost)
    {
        var menu = new ContextMenu();

        MenuItem Make(string header, Action execute, bool enabled = true)
        {
            var item = new MenuItem { Header = header, IsEnabled = enabled };
            item.Click += (_, _) => execute();
            return item;
        }

        DockItem? ActiveItem() => docHost.ActiveItem
            ?? docHost.Items.FirstOrDefault();

        menu.Opened += (_, _) =>
        {
            menu.Items.Clear();

            var active       = ActiveItem();
            var hosts        = Layout?.GetAllDocumentHosts().ToList() ?? [];
            var hostIndex    = hosts.IndexOf(docHost);
            bool hasNext     = hostIndex < hosts.Count - 1;
            bool hasPrev     = hostIndex > 0;
            bool multiGroup  = hosts.Count > 1;

            menu.Items.Add(Make("Move to Next Tab Group",     () => { if (active is not null) HandleMoveToAdjacentGroup(active, forward: true);  }, active is not null && hasNext));
            menu.Items.Add(Make("Move to Previous Tab Group", () => { if (active is not null) HandleMoveToAdjacentGroup(active, forward: false); }, active is not null && hasPrev));
            menu.Items.Add(new Separator());
            menu.Items.Add(Make("New Vertical Tab Group",   () => { if (active is not null) HandleNewTabGroup(active, DockDirection.Right);  }, active is not null));
            menu.Items.Add(Make("New Horizontal Tab Group", () => { if (active is not null) HandleNewTabGroup(active, DockDirection.Bottom); }, active is not null));
            menu.Items.Add(new Separator());
            menu.Items.Add(Make("Close This Tab Group",       () => { if (active is not null) HandleCloseTabGroup(active); }, active is not null && multiGroup));
            menu.Items.Add(Make("Close All Other Tab Groups", () => HandleCloseAllTabGroups(),                               multiGroup));
            menu.Items.Add(new Separator());
            menu.Items.Add(Make("Hide Group Badge", () => ShowGroupNumberBadge = false));
        };

        return menu;
    }

    private UIElement CreateTabControl(DockGroupNode group)
    {
        var tabControl = CreateTabControlForGroup(group);
        _managedTabControls.Add(tabControl);
        tabControl.ApplyHighlightMode(PanelHighlightMode);
        var titleBar   = CreateGroupTitleBar(group, tabControl);

        var layout = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(titleBar, Dock.Top);
        layout.Children.Add(titleBar);
        layout.Children.Add(tabControl);

        var r = PanelCornerRadius;

        // Overlay border: sits over the content area only (title bar + body).
        // CornerRadius applied directly so the highlight border itself is rounded.
        var overlayBorder = new Border
        {
            BorderThickness     = new Thickness(0),
            CornerRadius        = new CornerRadius(r),
            IsHitTestVisible    = false,
            SnapsToDevicePixels = true,
        };
        overlayBorder.SetResourceReference(Border.BorderBrushProperty, "DockTabActiveBrush");

        var outer = new Grid { Margin = new Thickness(2) };
        outer.Children.Add(layout);        // z=0
        outer.Children.Add(overlayBorder); // z=1

        // Keep overlay margin in sync with tab strip height, and punch a gap in the bottom
        // border above the active tab so the selected tab connects flush to the content area.
        tabControl.Loaded += (_, _) =>
        {
            tabControl.ApplyTemplate();
            if (tabControl.Template?.FindName("PART_TabStrip", tabControl) is not FrameworkElement tabStrip)
                return;

            const double gapH = 2.0;
            const double bt   = 1.0;

            void UpdateOverlay()
            {
                double tabH = tabStrip.ActualHeight;
                double w    = outer.ActualWidth;
                double h    = outer.ActualHeight - tabH;

                overlayBorder.Margin = new Thickness(0, 0, 0, tabH);

                if (w <= 0 || h <= 0) { overlayBorder.Clip = null; return; }

                var contentArea = new RectangleGeometry(new Rect(0, 0, w, h));

                int selIdx = tabControl.SelectedIndex;
                if (selIdx >= 0 &&
                    tabControl.ItemContainerGenerator.ContainerFromIndex(selIdx) is FrameworkElement activeTab)
                {
                    var pos  = activeTab.TranslatePoint(new Point(0, 0), outer);
                    double gapX = Math.Max(0, pos.X);
                    double gapW = activeTab.ActualWidth;
                    if (gapW > 0)
                    {
                        double gX = gapX + bt;
                        double gW = Math.Max(0, gapW - bt * 2);
                        var gap = new RectangleGeometry(new Rect(gX, h - gapH, gW, gapH * 2));
                        overlayBorder.Clip = new CombinedGeometry(GeometryCombineMode.Exclude, contentArea, gap);
                        return;
                    }
                }

                overlayBorder.Clip = contentArea;
            }

            tabStrip.SizeChanged        += (_, _) => UpdateOverlay();
            outer.SizeChanged           += (_, _) => UpdateOverlay();
            tabControl.SelectionChanged += (_, _) => UpdateOverlay();
            tabControl.LayoutUpdated    += (_, _) => UpdateOverlay();
            UpdateOverlay();
        };

        outer.AddHandler(
            UIElement.PreviewMouseDownEvent,
            new MouseButtonEventHandler((_, _) => SetActivePanel(overlayBorder)),
            handledEventsToo: true);
        outer.AddHandler(
            UIElement.GotKeyboardFocusEvent,
            new KeyboardFocusChangedEventHandler((_, _) => SetActivePanel(overlayBorder)),
            handledEventsToo: true);

        _panelBorders.Add(overlayBorder);
        return outer;
    }

    /// <summary>
    /// Creates and binds a <see cref="DockTabControl"/> for a side panel group
    /// with bottom tab strip placement (VS-style).
    /// </summary>
    private DockTabControl CreateTabControlForGroup(DockGroupNode group)
    {
        var tabControl = new DockTabControl();
        tabControl.TabStripPlacement     = Dock.Bottom;
        tabControl.ExtraMenuItemsFactory = TabExtraMenuItemsFactory;
        tabControl.HasMultipleDocumentHostsCheck = () =>
            Layout?.GetAllDocumentHosts().Skip(1).Any() ?? false;
        tabControl.Bind(group, CachedContentFactory);
        WireTabControlEvents(tabControl);
        _tabPreviews.Add(TabHoverPreview.Attach(tabControl, TabPreviewSettings));
        _tabControlCache[group] = tabControl;  // M2.1: register for incremental updates
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

        // Pin button (Pin MDL2) — sends entire group to auto-hide
        var pinButton = MakeTitleButton("\uE141", "Auto Hide");
        pinButton.Click += (_, _) =>
        {
            _engine?.AutoHideGroup(group);
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
            var dimFg      = new SolidColorBrush(Color.FromArgb(0x80, 0xCC, 0xCC, 0xCC));

            var menu = new ContextMenu
            {
                Background  = menuBg     ?? Brushes.DarkGray,
                BorderBrush = menuBorder ?? Brushes.Gray,
                Foreground  = menuFg     ?? Brushes.White
            };

            TextBlock MakeIcon(string glyph, bool enabled = true) => new()
            {
                Text       = glyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize   = 12,
                Foreground = enabled ? (menuFg ?? Brushes.White) : dimFg,
                VerticalAlignment = VerticalAlignment.Center
            };

            MenuItem MakeItem(string header, string glyph, Action action,
                              bool enabled = true, string? gesture = null)
            {
                var mi = new MenuItem
                {
                    Header     = header,
                    Icon       = MakeIcon(glyph, enabled),
                    IsEnabled  = enabled,
                    Foreground = enabled ? menuFg : dimFg
                };
                if (gesture is not null) mi.InputGestureText = gesture;
                if (enabled) mi.Click += (_, _) => action();
                return mi;
            }

            menu.Items.Add(MakeItem("Float",
                "\uE8A7", () => { CaptureDockedSizeForFloat(item); _engine?.Float(item); RebuildVisualTree(); },
                enabled: item.CanFloat));
            menu.Items.Add(MakeItem("Dock as Tabbed Document",
                "\uE8F4", () => { _engine?.Dock(item, Layout!.MainDocumentHost, DockDirection.Center); RebuildVisualTree(); }));
            menu.Items.Add(MakeItem("Auto Hide",
                "\uE77A", () => { _engine?.AutoHideGroup(group); RebuildVisualTree(); }));
            menu.Items.Add(new Separator());
            menu.Items.Add(MakeItem("Move to New Window",
                "\uE8A7", () => { }, enabled: false));
            menu.Items.Add(new Separator());
            menu.Items.Add(MakeItem("Close",
                "\uE8BB", () => { RaiseTabCloseRequested(item); _engine?.Close(item); RebuildVisualTree(); },
                enabled: item.CanClose, gesture: "Shift+Esc"));

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
    /// Activates a panel border and deactivates the previous one.
    /// The visual style is driven by <see cref="PanelHighlightMode"/>.
    /// </summary>
    private void SetActivePanel(Border panelBorder)
    {
        if (_activePanel == panelBorder) return;

        if (_activePanel is not null)
        {
            _activePanel.BorderThickness = new Thickness(0);
            _activePanel.Effect = null;
            // Dim the previously active panel border
            _activePanel.SetResourceReference(Border.BorderBrushProperty, "TG_InactiveGroupBorderBrush");
        }

        _activePanel = panelBorder;
        // Use the same accent as the tab selector's SelectionBorder for visual consistency.
        _activePanel.SetResourceReference(Border.BorderBrushProperty, "DockTabActiveBrush");
        ApplyHighlightToBorder(_activePanel);
    }

    /// <summary>
    /// Changes the highlight mode and immediately re-renders the active panel.
    /// Call this when the user changes the option (live preview).
    /// </summary>
    public void ApplyHighlightMode(ActivePanelHighlightMode mode)
    {
        PanelHighlightMode = mode;

        foreach (var tc in _managedTabControls)
            tc.ApplyHighlightMode(mode);

        if (_activePanel is null) return;

        _activePanel.BorderThickness = new Thickness(0);
        _activePanel.Effect = null;
        ApplyHighlightToBorder(_activePanel);
    }

    private void ApplyHighlightToBorder(Border border)
    {
        switch (PanelHighlightMode)
        {
            case ActivePanelHighlightMode.None:
                break;

            case ActivePanelHighlightMode.TopBar:
                border.BorderThickness = new Thickness(0, 2, 0, 0);
                break;

            case ActivePanelHighlightMode.FullBorder:
                border.BorderThickness = new Thickness(1);
                break;

            case ActivePanelHighlightMode.Glow:
                border.BorderThickness = new Thickness(2);
                border.Effect = new DropShadowEffect
                {
                    Color       = GetAccentColor(),
                    BlurRadius  = 12,
                    ShadowDepth = 0,
                    Opacity     = 0.8,
                };
                break;
        }
    }

    private static Color GetAccentColor()
    {
        if (Application.Current?.TryFindResource("DockTabActiveColor") is Color c)
            return c;
        return Colors.CornflowerBlue;
    }

    /// <summary>
    /// Updates the corner radius on all currently active panel borders and rebuilds their clip geometries.
    /// Call this after <see cref="PanelCornerRadius"/> is changed at runtime (e.g. from options page).
    /// </summary>
    public void UpdatePanelCornerRadius(double r)
    {
        PanelCornerRadius = r;
        foreach (var border in _panelBorders)
            border.CornerRadius = new CornerRadius(r);
        foreach (var element in _panelClipElements)
            UpdateRoundedClip(element, r);
        // Rebuild the active highlight border with the new shape.
        if (_activePanel is not null)
            ApplyHighlightToBorder(_activePanel);
    }

    /// <summary>
    /// Sets an explicit rounded <see cref="RectangleGeometry"/> clip on a border.
    /// WPF Border.ClipToBounds only clips to a rectangular box — CornerRadius is ignored for child clipping.
    /// </summary>
    private static void UpdateRoundedClip(FrameworkElement element, double radius)
    {
        double w = element.ActualWidth;
        double h = element.ActualHeight;
        if (w <= 0 || h <= 0 || radius <= 0)
        {
            element.Clip = null;
            return;
        }
        element.Clip = new RectangleGeometry(new Rect(0, 0, w, h), radius, radius);
    }

    /// <summary>
    /// Wraps any UIElement in a panel border with rounded corners and a small margin (VS2026-style).
    /// No visible border initially; a 2px top accent appears when the panel is activated.
    /// Children are clipped to the rounded shape via a dynamic RectangleGeometry clip.
    /// </summary>
    private Border CreatePanelBorder(UIElement content)
    {
        var radius = PanelCornerRadius;
        var border = new Border
        {
            Child               = content,
            BorderThickness     = new Thickness(0),
            CornerRadius        = new CornerRadius(radius),
            // Border.GetLayoutClip() returns a rounded geometry matching CornerRadius when
            // ClipToBounds=true. This is the correct WPF-native way to clip child content
            // (including the tab strip background) to the rounded panel shape.
            ClipToBounds        = true,
            Margin              = new Thickness(2),
            SnapsToDevicePixels = true
        };
        border.SetResourceReference(Border.BorderBrushProperty, "DockTabActiveBrush");
        _panelBorders.Add(border);

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
        if (item == _lastActivatedItem) return;
        _lastActivatedItem = item;

        ActivationHistory.Remove(item);
        ActivationHistory.Insert(0, item);
        // Sync model so layout serialization captures the last selected tab,
        // not just the last tab added via DockEngine.Dock().
        if (item.Owner is { } owner)
            owner.ActiveItem = item;
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

        _autoHideLeft.UpdateItems(Layout.AutoHideItems.Where(i => i.LastDockSide == DCore.DockSide.Left));
        _autoHideRight.UpdateItems(Layout.AutoHideItems.Where(i => i.LastDockSide == DCore.DockSide.Right));
        _autoHideTop.UpdateItems(Layout.AutoHideItems.Where(i => i.LastDockSide == DCore.DockSide.Top));
        _autoHideBottom.UpdateItems(Layout.AutoHideItems.Where(i => i.LastDockSide == DCore.DockSide.Bottom));
    }

    private void ClearAllAutoHideBarHighlights()
    {
        _autoHideLeft.SetActiveGroup(null);
        _autoHideRight.SetActiveGroup(null);
        _autoHideTop.SetActiveGroup(null);
        _autoHideBottom.SetActiveGroup(null);
    }

    private void OnAutoHideGroupClicked(IReadOnlyList<DockItem> items)
    {
        if (items.Count == 0) return;

        var representative = items[0];

        // Toggle off if the same group is already open
        if (_autoHideFlyout.IsOpen &&
            _autoHideFlyout.CurrentGroup.Count > 0 &&
            _autoHideFlyout.CurrentGroup[0] == representative)
        {
            // Toggle off — snapshot is captured by the Dismissing event inside Close()
            _autoHideFlyout.Close();
            return;
        }

        // Inset the flyout so it doesn't overlap sibling auto-hide bars.
        var insets = new Thickness(
            _autoHideLeft.ActualWidth,
            _autoHideTop.ActualHeight,
            _autoHideRight.ActualWidth,
            _autoHideBottom.ActualHeight);

        // Highlight the active bar button and show the flyout.
        var activeBar = representative.LastDockSide switch
        {
            DCore.DockSide.Left   => _autoHideLeft,
            DCore.DockSide.Right  => _autoHideRight,
            DCore.DockSide.Top    => _autoHideTop,
            _                    => _autoHideBottom
        };
        activeBar.SetActiveGroup(items);

        _autoHideFlyout.ShowForItems(items, CachedContentFactory, representative.LastDockSide, insets);
    }

    private void OnAutoHideRestoreRequested(DockItem item)
    {
        _autoHideFlyout.Close(); // Dismissing event captures snapshot

        if (_engine is null || Layout is null) return;

        // Restore the whole group if the item was auto-hidden as part of one
        if (item.AutoHideGroupId is { } groupId)
        {
            _engine.RestoreGroupFromAutoHide(groupId, Layout.MainDocumentHost);
        }
        else
        {
            var direction = item.LastDockSide switch
            {
                DCore.DockSide.Left   => DockDirection.Left,
                DCore.DockSide.Right  => DockDirection.Right,
                DCore.DockSide.Top    => DockDirection.Top,
                DCore.DockSide.Bottom => DockDirection.Bottom,
                _                    => DockDirection.Bottom
            };
            _engine.RestoreFromAutoHide(item, Layout.MainDocumentHost, direction);
        }

        RebuildVisualTree();
    }

    private void OnAutoHideCloseRequested(DockItem item)
    {
        _autoHideFlyout.Close(); // Dismissing event captures snapshot

        if (_engine is null) return;

        _engine.Close(item);
        RebuildVisualTree();
    }

    private void OnAutoHideFloatRequested(DockItem item)
    {
        _autoHideFlyout.Close(); // Dismissing event captures snapshot

        if (_engine is null || Layout is null) return;

        // Save IsDocument before RestoreFromAutoHide: docking into MainDocumentHost
        // (a DocumentHostNode) sets IsDocument=true via DockGroupNode.AddItem, which
        // corrupts DockDragManager routing and prevents re-docking to panel zones.
        // Mirrors the same preserve pattern used in DockEngine.Float for multi-item groups.
        bool wasDocument = item.IsDocument;
        _engine.RestoreFromAutoHide(item, Layout.MainDocumentHost, DockDirection.Center);
        _engine.Float(item);
        item.IsDocument = wasDocument;  // Restore panel identity after float
        RebuildVisualTree();
    }

    private void OnAutoHideGroupFloatRequested(IReadOnlyList<DockItem> items)
    {
        if (_engine is null || Layout is null) return;
        foreach (var item in items)
        {
            bool wasDocument = item.IsDocument;
            _engine.RestoreFromAutoHide(item, Layout.MainDocumentHost, DockDirection.Center);
            _engine.Float(item);
            item.IsDocument = wasDocument;
        }
        RebuildVisualTree();
    }

    private void OnAutoHideGroupCloseRequested(IReadOnlyList<DockItem> items)
    {
        if (_engine is null) return;
        foreach (var item in items)
            _engine.Close(item);
        RebuildVisualTree();
    }

    /// <summary>
    /// Captures a <see cref="System.Windows.Media.Imaging.RenderTargetBitmap"/> snapshot of the
    /// current flyout panel and stores it in <see cref="_autoHideBitmapCache"/>
    /// so the hover preview can display it after the flyout is dismissed.
    /// </summary>
    private void CaptureAutoHideSnapshot()
    {
        if (_autoHideFlyout.CurrentGroup.Count == 0) return;
        var item = _autoHideFlyout.CurrentGroup[0];
        var panel = _autoHideFlyout.PanelElement;
        if (!panel.IsVisible) return;

        // Skip if the panel is too small — it is likely still in the opening animation or
        // was closed before the animation completed. Storing a partial/blank snapshot here
        // would overwrite a previously good one, degrading the hover-preview quality.
        const double MinCaptureSize = 60.0;
        if (panel.RenderSize.Width < MinCaptureSize || panel.RenderSize.Height < MinCaptureSize) return;

        try
        {
            var dpi    = System.Windows.Media.VisualTreeHelper.GetDpi(panel);
            var width  = panel.RenderSize.Width;
            var height = panel.RenderSize.Height;

            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(
                (int)(width  * dpi.DpiScaleX),
                (int)(height * dpi.DpiScaleY),
                dpi.PixelsPerInchX,
                dpi.PixelsPerInchY,
                System.Windows.Media.PixelFormats.Pbgra32);

            rtb.Render(panel);
            rtb.Freeze();

            _autoHideBitmapCache[item] = rtb;
        }
        catch
        {
            // Swallow render errors (hardware-accelerated content not yet realised, etc.)
        }
    }
}
