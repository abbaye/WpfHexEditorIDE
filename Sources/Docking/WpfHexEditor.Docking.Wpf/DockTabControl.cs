//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WpfHexEditor.Docking.Core;
using WpfHexEditor.Docking.Core.Nodes;
using WpfHexEditor.Shell.Automation;
using WpfHexEditor.Shell.Properties;

namespace WpfHexEditor.Shell;

/// <summary>
/// WPF projection of <see cref="DockGroupNode"/>: a TabControl with draggable tabs.
/// </summary>
public class DockTabControl : TabControl
{
    static DockTabControl()
    {
        // Re-apply selection border thickness whenever tab strip placement changes at runtime
        // (e.g. document area switched from top to bottom tabs).
        TabStripPlacementProperty.OverrideMetadata(
            typeof(DockTabControl),
            new FrameworkPropertyMetadata(Dock.Top, (d, _) =>
            {
                if (d is DockTabControl tc) tc.ApplyHighlightMode(tc._highlightMode);
            }));
    }

    public DockGroupNode? Node { get; private set; }

    // -- MaxTabWidth DP — caps the title text width so long filenames are ellipsis-truncated.
    public static readonly DependencyProperty MaxTabWidthProperty =
        DependencyProperty.Register(nameof(MaxTabWidth), typeof(double), typeof(DockTabControl),
            new PropertyMetadata(180.0));

    /// <summary>Maximum display width (in pixels) for a tab header's title text.</summary>
    public double MaxTabWidth
    {
        get => (double)GetValue(MaxTabWidthProperty);
        set => SetValue(MaxTabWidthProperty, value);
    }

    public DockTabControl()
    {
        SetResourceReference(BackgroundProperty, "DockBackgroundBrush");
        SetResourceReference(ForegroundProperty, "DockMenuForegroundBrush");
        SetResourceReference(BorderBrushProperty, "DockBorderBrush");
        SetResourceReference(StyleProperty, "DockTabControlStyle");
    }

    protected override AutomationPeer OnCreateAutomationPeer() =>
        new DockTabControlAutomationPeer(this);

    public event Action<DockItem>? TabDragStarted;
    public event Action<DockItem>? TabCloseRequested;

    /// <summary>
    /// Raised by Close All / Close All But This / Close All But Pinned so that the host can
    /// process all items in a single batch without triggering a visual-tree rebuild between items.
    /// </summary>
    public event Action<IReadOnlyList<DockItem>>? TabBatchCloseRequested;

    /// <summary>Programmatically requests closure of the given DockItem tab (used by overflow dropdown close button).</summary>
    public void RequestCloseTab(DockItem item) => TabCloseRequested?.Invoke(item);
    public event Action<DockItem>? TabFloatRequested;
    public event Action<DockItem>? TabAutoHideRequested;
    public event Action<DockItem>? TabHideRequested;
    public event Action<DockItem>? TabDockAsDocumentRequested;
    public event Action<DockItem>? TabRestoreToToolPanelRequested;
    public event Action<DockItem>? TabPinToggleRequested;
    public event Action<DockItem>? TabStickyToggleRequested;
    public event Action<DockItem, int>? TabReorderRequested;
    public event Action<DockItem>? TabNewVerticalGroupRequested;
    public event Action<DockItem>? TabNewHorizontalGroupRequested;
    public event Action<DockItem>? TabMoveToNextGroupRequested;
    public event Action<DockItem>? TabMoveToPreviousGroupRequested;
    public event Action<DockItem>? TabCloseGroupRequested;

    /// <summary>
    /// Optional factory that injects extra <see cref="MenuItem"/> entries at the bottom of
    /// a tab's context menu (after a separator).  Set by the application shell to provide
    /// "Compare with…" and similar extensibility items without coupling the docking library
    /// to application logic.
    /// </summary>
    public Func<DockItem, IReadOnlyList<MenuItem>>? ExtraMenuItemsFactory { get; set; }

    /// <summary>
    /// Callback used by tab headers to determine whether multiple document hosts exist.
    /// Set by <see cref="DockControl"/> when creating tab controls.
    /// </summary>
    public Func<bool>? HasMultipleDocumentHostsCheck { get; set; }

    /// <summary>
    /// Returns the rendered height of the tab strip (PART_TabStrip in the control template).
    /// Used by <see cref="DockControl"/> to clip the active-panel overlay border so it stops
    /// at the content/tab-strip boundary (VS-like: border does not wrap the tab strip).
    /// Returns 0 if the template has not been applied yet.
    /// </summary>
    internal double GetTabStripHeight()
    {
        ApplyTemplate();
        return (Template?.FindName("PART_TabStrip", this) as FrameworkElement)?.ActualHeight ?? 0;
    }

    // ── Highlight mode ─────────────────────────────────────────────────────────

    private ActivePanelHighlightMode _highlightMode = ActivePanelHighlightMode.FullBorder;

    /// <summary>
    /// Updates the SelectionBorder thickness on every tab item to match the active
    /// panel highlight mode (TopBar = bottom-only, FullBorder = U-shape 1px, Glow = U-shape 2px).
    /// </summary>
    internal void ApplyHighlightMode(ActivePanelHighlightMode mode)
    {
        _highlightMode = mode;
        foreach (var obj in Items)
        {
            if (ItemContainerGenerator.ContainerFromItem(obj) is TabItem tab)
                ApplySelectionBorderThickness(tab);
        }
    }

    private void ApplySelectionBorderThickness(TabItem tab)
    {
        tab.ApplyTemplate();
        if (tab.Template?.FindName("SelectionBorder", tab) is not Border sb) return;
        sb.BorderThickness = SelectionBorderThicknessFor(_highlightMode, TabStripPlacement);
    }

    /// <summary>
    /// Returns the SelectionBorder thickness matching the highlight mode and tab placement.
    /// Bottom tabs: U-shape open at top (left/bottom/right).
    /// Top tabs: inverted U open at bottom (left/top/right).
    /// </summary>
    private static Thickness SelectionBorderThicknessFor(ActivePanelHighlightMode mode, Dock placement)
    {
        bool isBottom = placement == Dock.Bottom;
        return (mode, isBottom) switch
        {
            (ActivePanelHighlightMode.TopBar, true)  => new Thickness(0, 0, 0, 2), // bottom only
            (ActivePanelHighlightMode.TopBar, false) => new Thickness(0, 2, 0, 0), // top only
            (ActivePanelHighlightMode.Glow,   true)  => new Thickness(2, 0, 2, 2), // U 2px
            (ActivePanelHighlightMode.Glow,   false) => new Thickness(2, 2, 2, 0), // inverted U 2px
            (_,                               true)  => new Thickness(1, 0, 1, 1), // U 1px (FullBorder)
            (_,                               false) => new Thickness(1, 1, 1, 0), // inverted U 1px
        };
    }

    private Func<DockItem, object>? _contentFactory;

    // Tracks DockItems whose plugin panel has already been created at least once.
    // "Already materialized" flag is stored in DockItem.Metadata so it survives
    // dock/undock, group moves, and Bind() rebuilds across any DockTabControl instance.
    private const string MaterializedKey = "_materialized";

    private int  _dragOriginalModelIndex = -1;
    private int  _currentInsertionIdx    = -1;
    private Popup?                   _reorderGhost;
    private ReorderInsertionAdorner? _reorderAdorner;

    /// <summary>
    /// Re-binds the current node, refreshing all tab items in the current template's items host.
    /// Used after a template/style change to guarantee items are hosted in the new panel.
    /// </summary>
    protected void Rebind()
    {
        if (Node is not null)
            Bind(Node, _contentFactory);
    }

    public void Bind(DockGroupNode node, Func<DockItem, object>? contentFactory = null)
    {
        Node = node;
        _contentFactory = contentFactory;
        Items.Clear();

        bool seenPinned = false;
        bool separatorPlaced = false;
        foreach (var item in node.Items)
        {
            var isActive = item == node.ActiveItem;
            var tabItem = CreateTabItem(item, contentFactory, isActive);
            // Pin separator: add left margin on first unpinned tab after pinned tabs
            if (item.IsPinned)
                seenPinned = true;
            else if (seenPinned && !separatorPlaced)
            {
                tabItem.Margin = new Thickness(6, 0, 0, 0);
                separatorPlaced = true;
            }
            Items.Add(tabItem);
        }

        if (node.ActiveItem is not null)
        {
            var activeIndex = node.Items.ToList().IndexOf(node.ActiveItem);
            if (activeIndex >= 0)
                SelectedIndex = activeIndex;
        }
    }

    /// <summary>
    /// Incrementally adds a tab for the given item without a full Bind rebuild.
    /// Called by DockControl when an item is added to an existing group (M2.1).
    /// </summary>
    public void AddTab(DockItem item)
    {
        var tabItem = CreateTabItem(item, _contentFactory, isActive: true);
        Items.Add(tabItem);
        SelectedItem = tabItem;
    }

    /// <summary>
    /// Incrementally removes the tab for the given item without a full Bind rebuild.
    /// Called by DockControl when an item is removed from an existing group (M2.1).
    /// </summary>
    public void RemoveTab(DockItem item)
    {
        for (var i = 0; i < Items.Count; i++)
        {
            if (Items[i] is TabItem ti && ti.Tag is DockItem d && d == item)
            {
                Items.RemoveAt(i);
                // Intentionally NOT clearing MaterializedKey from item.Metadata —
                // the flag must survive dock/undock so the loading placeholder never
                // re-appears for a panel that has already been shown once.
                return;
            }
        }
    }

    /// <summary>
    /// Materializes lazy content when a tab is first selected.
    /// </summary>
    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        base.OnSelectionChanged(e);

        if (_contentFactory is not null)
        {
            foreach (var added in e.AddedItems)
            {
                if (added is TabItem { Tag: DockItem item } tab && tab.Content is LazyContentPlaceholder)
                {
                    if (item.Metadata.ContainsKey("_pluginPanel"))
                    {
                        tab.Content = new PluginLoadingPlaceholder();
                        var factory = _contentFactory;
                        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
                        {
                            item.Metadata[MaterializedKey] = "1";
                            tab.Content = factory.Invoke(item);
                        });
                    }
                    else
                    {
                        tab.Content = _contentFactory.Invoke(item);
                    }
                }
            }
        }

        // C1: Scroll the newly selected tab into view so it is always visible
        // even when the tab strip overflows the available width.
        if (SelectedItem is TabItem selectedTab)
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,
                (System.Action)(() => selectedTab.BringIntoView()));
        }
    }

    private TabItem CreateTabItem(DockItem item, Func<DockItem, object>? contentFactory, bool isActive)
    {
        var header = new DockTabHeader(item)
        {
            ExtraMenuItemsFactory = ExtraMenuItemsFactory,
        };
        header.CloseClicked             += () => TabCloseRequested?.Invoke(item);
        header.DragStarted              += () => TabDragStarted?.Invoke(item);
        header.FloatRequested           += () => TabFloatRequested?.Invoke(item);
        header.AutoHideRequested        += () => TabAutoHideRequested?.Invoke(item);
        header.HideRequested            += () => TabHideRequested?.Invoke(item);
        header.DockAsDocumentRequested      += () => TabDockAsDocumentRequested?.Invoke(item);
        header.RestoreToToolPanelRequested  += () => TabRestoreToToolPanelRequested?.Invoke(item);
        header.CloseAllRequested            += () => CloseAllItems();
        header.CloseAllButThisRequested += () => CloseAllButItem(item);
        header.PinToggleRequested       += () => TabPinToggleRequested?.Invoke(item);
        header.StickyToggleRequested    += () => TabStickyToggleRequested?.Invoke(item);
        header.CloseAllButPinnedRequested += () => CloseAllButPinnedItems();
        header.NewVerticalGroupRequested  += () => TabNewVerticalGroupRequested?.Invoke(item);
        header.NewHorizontalGroupRequested += () => TabNewHorizontalGroupRequested?.Invoke(item);
        header.MoveToNextGroupRequested   += () => TabMoveToNextGroupRequested?.Invoke(item);
        header.MoveToPreviousGroupRequested += () => TabMoveToPreviousGroupRequested?.Invoke(item);
        header.CloseGroupRequested        += () => TabCloseGroupRequested?.Invoke(item);
        header.HasMultipleDocumentHostsCheck = HasMultipleDocumentHostsCheck;
        header.ReorderDragging          += pos => OnHeaderReorderDragging(item, pos);
        header.ReorderDropped           += pos => OnHeaderReorderDropped(item, pos);
        header.ReorderCancelled         += () => OnHeaderReorderCancelled(item);

        object tabContent;
        bool deferPluginContent = false;
        if (contentFactory is null)
        {
            tabContent = DefaultContent(item);
        }
        else if (isActive && item.Metadata.ContainsKey("_pluginPanel"))
        {
            if (item.Metadata.ContainsKey(MaterializedKey))
            {
                // Already built once — call factory directly (fast, no flash).
                tabContent = contentFactory.Invoke(item);
            }
            else
            {
                tabContent = new PluginLoadingPlaceholder();
                deferPluginContent = true;    // first load only
            }
        }
        else if (isActive)
        {
            tabContent = contentFactory.Invoke(item);
        }
        else
        {
            tabContent = new LazyContentPlaceholder(item);
        }

        var tabItem = new TabItem
        {
            Header = header,
            Tag = item,
            Content = tabContent
        };
        // DockTabControl is always bottom-placed: use inverted CornerRadius (0,0,4,4) style
        tabItem.SetResourceReference(StyleProperty, "DockTabItemBottomStyle");

        // Apply the current highlight mode once the template is available.
        tabItem.Loaded += (_, _) => ApplySelectionBorderThickness(tabItem);

        if (deferPluginContent)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
            {
                item.Metadata[MaterializedKey] = "1";
                tabItem.Content = contentFactory!.Invoke(item);
            });
        }

        return tabItem;
    }

    // -- Reorder — ghost-based, zero live Remove/Insert ----------------------
    //
    // During drag:
    //   • The dragged tab is dimmed in place (opacity 0.4).
    //   • A Popup ghost follows the cursor.
    //   • An adorner draws a thin vertical insertion indicator.
    //   • No Items.Remove/Insert and no UpdateLayout() are called.
    //
    // On drop: a single Remove/Insert fires TabReorderRequested which
    // triggers RebuildVisualTree() via DockTabEventWirer.

    private void OnHeaderReorderDragging(DockItem draggedItem, Point screenPos)
    {
        if (Node is null) return;

        // Initialize on first drag event
        if (_dragOriginalModelIndex < 0)
        {
            _dragOriginalModelIndex = Node.Items.ToList().IndexOf(draggedItem);
            SetDraggedTabOpacity(draggedItem, 0.4);
            ShowReorderGhost(draggedItem, screenPos);
            ShowInsertionIndicator();
        }

        MoveReorderGhost(screenPos);

        int idx = HitTestInsertionIndex(screenPos);
        if (idx != _currentInsertionIdx)
        {
            _currentInsertionIdx = idx;
            UpdateInsertionIndicator(idx);
        }
    }

    private void OnHeaderReorderDropped(DockItem draggedItem, Point screenPos)
    {
        int origModelIdx     = _dragOriginalModelIndex;
        _dragOriginalModelIndex = -1;
        _currentInsertionIdx    = -1;

        SetDraggedTabOpacity(draggedItem, 1.0);
        HideReorderGhost();
        RemoveInsertionIndicator();

        if (origModelIdx < 0) return;

        int insertionIdx    = HitTestInsertionIndex(screenPos);
        int targetVisualIdx = Math.Clamp(
            insertionIdx > origModelIdx ? insertionIdx - 1 : insertionIdx,
            0, Items.Count - 1);

        if (targetVisualIdx != origModelIdx)
            TabReorderRequested?.Invoke(draggedItem, targetVisualIdx);
    }

    private void OnHeaderReorderCancelled(DockItem draggedItem)
    {
        _dragOriginalModelIndex = -1;
        _currentInsertionIdx    = -1;

        SetDraggedTabOpacity(draggedItem, 1.0);
        HideReorderGhost();
        RemoveInsertionIndicator();
    }

    // -- Ghost helpers --------------------------------------------------------

    private void SetDraggedTabOpacity(DockItem draggedItem, double opacity)
    {
        for (int i = 0; i < Items.Count; i++)
            if (Items[i] is TabItem ti && ti.Tag is DockItem d && d == draggedItem)
            { ti.Opacity = opacity; return; }
    }

    private void ShowReorderGhost(DockItem item, Point screenPos)
    {
        var text = new TextBlock
        {
            Text = item.Title,
            VerticalAlignment = VerticalAlignment.Center
        };
        text.SetResourceReference(TextBlock.ForegroundProperty, "DockTabTextBrush");

        var border = new Border
        {
            Child           = text,
            Padding         = new Thickness(8, 3, 8, 3),
            BorderThickness = new Thickness(1),
            Opacity         = 0.85
        };
        border.SetResourceReference(Border.BackgroundProperty,   "DockTabActiveBrush");
        border.SetResourceReference(Border.BorderBrushProperty,  "DockBorderBrush");

        _reorderGhost = new Popup
        {
            Child              = border,
            AllowsTransparency = true,
            Placement          = PlacementMode.Absolute,
            HorizontalOffset   = screenPos.X + 8,
            VerticalOffset     = screenPos.Y - 24,
            IsOpen             = true
        };
    }

    private void MoveReorderGhost(Point screenPos)
    {
        if (_reorderGhost is null) return;
        _reorderGhost.HorizontalOffset = screenPos.X + 8;
        _reorderGhost.VerticalOffset   = screenPos.Y - 24;
    }

    private void HideReorderGhost()
    {
        if (_reorderGhost is null) return;
        _reorderGhost.IsOpen = false;
        _reorderGhost = null;
    }

    // -- Insertion-indicator helpers ------------------------------------------

    private void ShowInsertionIndicator()
    {
        var layer = AdornerLayer.GetAdornerLayer(this);
        if (layer is null) return;
        _reorderAdorner = new ReorderInsertionAdorner(this);
        layer.Add(_reorderAdorner);
    }

    private void UpdateInsertionIndicator(int insertionIdx)
    {
        if (_reorderAdorner is null) return;

        double x;
        if (insertionIdx >= Items.Count)
        {
            if (Items.Count == 0 ||
                Items[Items.Count - 1] is not TabItem last ||
                !last.IsArrangeValid) return;
            x = last.TranslatePoint(new Point(last.ActualWidth, 0), this).X;
        }
        else
        {
            if (Items[insertionIdx] is not TabItem ti || !ti.IsArrangeValid) return;
            x = ti.TranslatePoint(new Point(0, 0), this).X;
        }

        _reorderAdorner.SetX(x);
    }

    private void RemoveInsertionIndicator()
    {
        if (_reorderAdorner is null) return;
        AdornerLayer.GetAdornerLayer(this)?.Remove(_reorderAdorner);
        _reorderAdorner = null;
    }

    // -- Hit-test (screen coordinates) ---------------------------------------

    private int HitTestInsertionIndex(Point screenPos)
    {
        for (int i = 0; i < Items.Count; i++)
        {
            if (Items[i] is not TabItem ti || !ti.IsArrangeValid) continue;
            var tiScreen = ti.PointToScreen(new Point(0, 0));
            if (screenPos.X < tiScreen.X + ti.ActualWidth / 2)
                return i;
        }
        return Items.Count;
    }

    private static object DefaultContent(DockItem item) => new TextBlock
    {
        Text = $"Content: {item.Title}",
        Margin = new Thickness(8),
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center
    };

    /// <summary>
    /// Standalone placeholder shown while a plugin panel is being created.
    /// Displays a blue "+" icon, "Loading…" text, and an indeterminate progress bar
    /// on a dark scrim. Gets replaced directly via <c>tab.Content = realContent</c>.
    /// </summary>
    private sealed class PluginLoadingPlaceholder : Border
    {
        public PluginLoadingPlaceholder()
        {
            Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x1E, 0x1E, 0x1E));

            var icon = new TextBlock
            {
                Text = "\uE710",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 28,
                Foreground = new SolidColorBrush(Color.FromRgb(0x60, 0xA5, 0xFA)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            };

            var label = new TextBlock
            {
                Text = "Loading\u2026",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            };

            var progress = new System.Windows.Controls.ProgressBar
            {
                IsIndeterminate = true,
                Width = 140,
                Height = 4,
            };

            var stack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            stack.Children.Add(icon);
            stack.Children.Add(label);
            stack.Children.Add(progress);

            Child = stack;
        }
    }

    /// <summary>
    /// Lightweight placeholder shown for non-active tabs until they are first selected.
    /// </summary>
    private sealed class LazyContentPlaceholder : TextBlock
    {
        public LazyContentPlaceholder(DockItem item)
        {
            Text = $"Content: {item.Title}";
            Margin = new Thickness(8);
            VerticalAlignment = VerticalAlignment.Center;
            HorizontalAlignment = HorizontalAlignment.Center;
        }
    }

    /// <summary>
    /// Adorner that draws a thin vertical insertion-caret on the tab strip.
    /// </summary>
    private sealed class ReorderInsertionAdorner : Adorner
    {
        private double _x = -1;
        private static readonly Pen s_pen;

        static ReorderInsertionAdorner()
        {
            s_pen = new Pen(SystemColors.HighlightBrush, 2)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap   = PenLineCap.Round
            };
            s_pen.Freeze();
        }

        public ReorderInsertionAdorner(UIElement adornedElement) : base(adornedElement)
        {
            IsHitTestVisible = false;
        }

        public void SetX(double x) { _x = x; InvalidateVisual(); }
        public void Clear()         { _x = -1; InvalidateVisual(); }

        protected override void OnRender(DrawingContext dc)
        {
            if (_x < 0) return;
            double h = Math.Min(((FrameworkElement)AdornedElement).ActualHeight, 34);
            dc.DrawLine(s_pen, new Point(_x, 0), new Point(_x, h));
        }
    }

    private void CloseAllItems()
    {
        if (Node is null) return;
        var items = Node.Items.Where(i => i.CanClose && !i.IsPinned).ToList();
        if (items.Count > 0) TabBatchCloseRequested?.Invoke(items);
    }

    private void CloseAllButItem(DockItem keep)
    {
        if (Node is null) return;
        var items = Node.Items.Where(i => i != keep && i.CanClose).ToList();
        if (items.Count > 0) TabBatchCloseRequested?.Invoke(items);
    }

    private void CloseAllButPinnedItems()
    {
        if (Node is null) return;
        var items = Node.Items.Where(i => !i.IsPinned && i.CanClose).ToList();
        if (items.Count > 0) TabBatchCloseRequested?.Invoke(items);
    }
}

/// <summary>
/// Tab header with title, close button, context menu, and drag support.
/// </summary>
/// <summary>LSP server state for the document tab indicator dot.</summary>
public enum DockTabLspState { None, Connecting, Ready, Error }

public class DockTabHeader : StackPanel
{
    private readonly DockItem _item;
    private Button?    _closeButton;
    private Button?    _pinButton;
    private TextBlock? _titleBlock;
    private TextBlock? _lspDot;

    /// <summary>The DockItem this header represents (used for LSP state matching).</summary>
    public DockItem Item => _item;
    private Point _dragStartPoint;
    private bool _isDragging;
    private bool _isReordering;
    // Increased from 20 to 40 px to reduce accidental undock on small mouse jitter (VS-like feel).
    private const double FloatThresholdY    = 40.0;
    private const double FloatThresholdTool = 40.0;

    public event Action? CloseClicked;
    public event Action? DragStarted;

    /// <summary>
    /// Updates the LSP state dot visible on document tabs.
    /// Must be called on the UI thread.
    /// </summary>
    public void SetLspState(DockTabLspState state)
    {
        if (_lspDot is null) return;
        switch (state)
        {
            case DockTabLspState.None:
                _lspDot.Visibility = Visibility.Collapsed;
                break;
            case DockTabLspState.Connecting:
                _lspDot.Text       = "◌";
                _lspDot.Foreground = Application.Current?.TryFindResource("LSP_ConnectingDot") as System.Windows.Media.Brush
                                     ?? System.Windows.Media.Brushes.Gray;
                _lspDot.ToolTip    = DockingResources.Dock_LSP_Connecting;
                _lspDot.Visibility = Visibility.Visible;
                break;
            case DockTabLspState.Ready:
                _lspDot.Text       = "●";
                _lspDot.Foreground = Application.Current?.TryFindResource("LSP_ReadyDot") as System.Windows.Media.Brush
                                     ?? System.Windows.Media.Brushes.Green;
                _lspDot.ToolTip    = DockingResources.Dock_LSP_Ready;
                _lspDot.Visibility = Visibility.Visible;
                break;
            case DockTabLspState.Error:
                _lspDot.Text       = "✕";
                _lspDot.Foreground = Application.Current?.TryFindResource("LSP_ErrorDot") as System.Windows.Media.Brush
                                     ?? System.Windows.Media.Brushes.Red;
                _lspDot.ToolTip    = DockingResources.Dock_LSP_Error;
                _lspDot.Visibility = Visibility.Visible;
                break;
        }
    }
    public event Action? FloatRequested;
    public event Action<Point>? ReorderDragging;
    public event Action<Point>? ReorderDropped;
    public event Action? ReorderCancelled;
    public event Action? AutoHideRequested;
    public event Action? HideRequested;
    public event Action? DockAsDocumentRequested;
    public event Action? RestoreToToolPanelRequested;
    public event Action? CloseAllRequested;
    public event Action? CloseAllButThisRequested;
    public event Action? PinToggleRequested;
    public event Action? StickyToggleRequested;
    public event Action? CloseAllButPinnedRequested;
    public event Action? NewVerticalGroupRequested;
    public event Action? NewHorizontalGroupRequested;
    public event Action? MoveToNextGroupRequested;
    public event Action? MoveToPreviousGroupRequested;
    public event Action? CloseGroupRequested;

    /// <summary>
    /// Set by <see cref="DockTabControl.CreateTabItem"/> so the context menu can include
    /// application-injected items (e.g. "Compare with…").
    /// </summary>
    public Func<DockItem, IReadOnlyList<MenuItem>>? ExtraMenuItemsFactory { get; set; }

    /// <summary>
    /// Callback to check whether multiple document hosts exist.
    /// Set by the owning <see cref="DockTabControl"/>.
    /// </summary>
    public Func<bool>? HasMultipleDocumentHostsCheck { get; set; }

    private bool _hasMultipleDocumentHosts;

    public DockTabHeader(DockItem item)
    {
        _item = item;
        Orientation = Orientation.Horizontal;

        // Icon (if provided)
        if (item.Icon is not null)
        {
            var iconHost = new ContentPresenter
            {
                Content = item.Icon is ImageSource img
                    ? new Image { Source = img, Width = 16, Height = 16, Stretch = Stretch.Uniform }
                    : item.Icon,
                Width = 16,
                Height = 16,
                Margin = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Children.Add(iconHost);
        }

        // LSP state dot — document tabs only, hidden until SetLspState is called
        if (item.Owner is DocumentHostNode)
        {
            _lspDot = new TextBlock
            {
                Text              = "●",
                FontSize          = 8,
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(0, 0, 4, 0),
                Visibility        = Visibility.Collapsed,
                ToolTip           = "Language Server",
            };
            Children.Add(_lspDot);
        }

        _titleBlock = new TextBlock
        {
            Text             = item.IsDirty ? item.Title + " \u2022" : item.Title,
            VerticalAlignment = VerticalAlignment.Center,
            Margin           = new Thickness(0, 0, 4, 0),
            TextTrimming     = TextTrimming.CharacterEllipsis
        };
        // Bind MaxWidth to the nearest DockTabControl.MaxTabWidth so that
        // the host can configure tab name truncation centrally.
        _titleBlock.SetBinding(FrameworkElement.MaxWidthProperty,
            new System.Windows.Data.Binding(nameof(DockTabControl.MaxTabWidth))
            {
                RelativeSource = new System.Windows.Data.RelativeSource(
                    System.Windows.Data.RelativeSourceMode.FindAncestor,
                    typeof(DockTabControl), 1),
                FallbackValue = 180.0
            });
        Children.Add(_titleBlock);

        // React to title / dirty changes
        item.PropertyChanged += (_, e) =>
        {
            if (_titleBlock is null) return;
            if (e.PropertyName is nameof(DockItem.Title) or nameof(DockItem.IsDirty))
                _titleBlock.Text = item.IsDirty ? item.Title + " \u2022" : item.Title;
        };

        // Pin button (auto-hide toggle) — only for tool panels, not documents
        if (item.Owner is not DocumentHostNode)
        {
            var pinButton = new Button
            {
                Content         = "\uE141",
                FontSize        = 11,
                FontFamily      = new FontFamily("Segoe MDL2 Assets"),
                Padding         = new Thickness(2, 0, 2, 0),
                Margin          = new Thickness(0, 0, 1, 0),
                Cursor          = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip         = "Auto-Hide"
            };
            pinButton.SetResourceReference(StyleProperty, "DockTitleButtonStyle");
            AutomationProperties.SetName(pinButton, $"Auto-Hide {item.Title}");
            pinButton.Click += (_, _) => AutoHideRequested?.Invoke();
            Children.Add(pinButton);
        }

        // Pin button (pin/unpin toggle) — only for document tabs
        if (item.Owner is DocumentHostNode)
        {
            _pinButton = new Button
            {
                Content         = "\uE141",
                FontSize        = 11,
                FontFamily      = new FontFamily("Segoe MDL2 Assets"),
                Padding         = new Thickness(2, 0, 2, 0),
                Margin          = new Thickness(0, 0, 1, 0),
                Cursor          = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip         = item.IsPinned ? "Unpin Tab" : "Pin Tab",
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(item.IsPinned ? 0 : 90),
                Opacity         = item.IsPinned ? 1 : 0
            };
            _pinButton.SetResourceReference(StyleProperty, "DockTitleButtonStyle");
            AutomationProperties.SetName(_pinButton, $"Pin {item.Title}");
            _pinButton.Click += (_, _) => PinToggleRequested?.Invoke();
            Children.Add(_pinButton);
        }

        if (item.CanClose)
        {
            _closeButton = new Button
            {
                Content = "\u00D7",
                FontSize = 10,
                Padding = new Thickness(2, 0, 2, 0),
                Margin = new Thickness(0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Close",
                Opacity = 0 // VS2026: hidden by default, shown on hover or when active
            };
            _closeButton.SetResourceReference(StyleProperty, "DockTitleButtonStyle");
            AutomationProperties.SetName(_closeButton, $"Close {item.Title}");
            _closeButton.Click += (_, _) => CloseClicked?.Invoke();
            Children.Add(_closeButton);
        }

        // Context menu — rebuilt on each right-click so multi-host state is fresh
        ContextMenuOpening += (_, _) =>
        {
            _hasMultipleDocumentHosts = HasMultipleDocumentHostsCheck?.Invoke() ?? false;
            ContextMenu = BuildContextMenu(_item);
        };
        ContextMenu = BuildContextMenu(item);

        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove           += OnMouseMove;
        MouseLeftButtonUp   += OnMouseLeftButtonUp;

        // Cancel reorder if mouse capture is lost unexpectedly (Alt+Tab, window blur…)
        LostMouseCapture += (_, _) =>
        {
            if (_isReordering)
            {
                _isReordering = false;
                ReorderCancelled?.Invoke();
            }
        };

        // VS2026 Fluent: close/pin buttons visible on hover or when tab is selected
        MouseEnter += (_, _) =>
        {
            if (_closeButton is not null) _closeButton.Opacity = 1;
            if (_pinButton is not null && !_item.IsPinned) _pinButton.Opacity = 1;
        };
        MouseLeave += (_, _) => UpdateButtonVisibility();
        Loaded += (_, _) => WireParentTabItem();
    }

    private void WireParentTabItem()
    {
        if (_closeButton is null && _pinButton is null) return;

        // Walk up to find the owning TabItem
        DependencyObject? current = this;
        while (current is not null)
        {
            if (current is TabItem tabItem)
            {
                // Set initial state
                var show = tabItem.IsSelected ? 1.0 : 0.0;
                if (_closeButton is not null)
                    _closeButton.Opacity = show;
                if (_pinButton is not null && !_item.IsPinned)
                    _pinButton.Opacity = show;

                // Track selection changes via DependencyPropertyDescriptor
                var dpd = DependencyPropertyDescriptor.FromProperty(
                    Selector.IsSelectedProperty, typeof(TabItem));
                dpd?.AddValueChanged(tabItem, (_, _) => UpdateButtonVisibility());
                return;
            }
            current = VisualTreeHelper.GetParent(current);
        }
    }

    private void UpdateButtonVisibility()
    {
        // Keep visible if mouse is over header
        if (IsMouseOver) return;

        // Keep visible if parent tab is selected
        DependencyObject? current = this;
        while (current is not null)
        {
            if (current is TabItem tabItem)
            {
                var show = tabItem.IsSelected ? 1.0 : 0.0;
                if (_closeButton is not null) _closeButton.Opacity = show;
                if (_pinButton is not null && !_item.IsPinned) _pinButton.Opacity = show;
                return;
            }
            current = VisualTreeHelper.GetParent(current);
        }

        if (_closeButton is not null) _closeButton.Opacity = 0;
        if (_pinButton is not null && !_item.IsPinned) _pinButton.Opacity = 0;
    }

    /// <summary>
    /// Creates a 14-px Segoe MDL2 Assets glyph for use as a MenuItem.Icon.
    /// </summary>
    private static TextBlock MakeMenuIcon(string glyph) => new()
    {
        Text       = glyph,
        FontFamily = new FontFamily("Segoe MDL2 Assets"),
        FontSize   = 14,
        Width      = 16,
        Height     = 16,
        TextAlignment         = TextAlignment.Center,
        VerticalAlignment     = VerticalAlignment.Center,
        HorizontalAlignment   = HorizontalAlignment.Center,
    };

    private ContextMenu BuildContextMenu(DockItem item)
    {
        var menu = new ContextMenu();

        // Pin/Unpin + Keep Tab Visible — only for document tabs
        if (item.Owner is DocumentHostNode)
        {
            var pinIcon     = item.IsPinned ? "\uE196" : "\uE141"; // Unpin : Pin
            var pinMenuItem = new MenuItem
            {
                Header = item.IsPinned ? "Unpin Tab" : "Pin Tab",
                Icon   = MakeMenuIcon(pinIcon)
            };
            pinMenuItem.Click += (_, _) => PinToggleRequested?.Invoke();
            menu.Items.Add(pinMenuItem);

            // IsSticky: keeps the tab permanently in the tab strip (never overflowed).
            var stickyMenuItem = new MenuItem
            {
                Header      = item.IsSticky ? "Remove from Tab Strip Pin" : "Keep Tab Visible",
                Icon        = MakeMenuIcon(item.IsSticky ? "\uE77A" : "\uE718"), // UnLock : Lock
                IsCheckable = true,
                IsChecked   = item.IsSticky
            };
            stickyMenuItem.Click += (_, _) => StickyToggleRequested?.Invoke();
            menu.Items.Add(stickyMenuItem);

            menu.Items.Add(new Separator());
        }

        // Document tab group operations (VS2022 "New Vertical/Horizontal Tab Group")
        if (item.Owner is DocumentHostNode)
        {
            var newVertGroup = new MenuItem
            {
                Header = "New Vertical Tab Group",
                Icon   = MakeMenuIcon("\uE746") // SplitVertical
            };
            newVertGroup.Click += (_, _) => NewVerticalGroupRequested?.Invoke();
            menu.Items.Add(newVertGroup);

            var newHorizGroup = new MenuItem
            {
                Header = "New Horizontal Tab Group",
                Icon   = MakeMenuIcon("\uE748") // SplitHorizontal
            };
            newHorizGroup.Click += (_, _) => NewHorizontalGroupRequested?.Invoke();
            menu.Items.Add(newHorizGroup);

            // Move to Next/Previous only when multiple document hosts exist
            var moveNextGroup = new MenuItem
            {
                Header    = "Move to Next Tab Group",
                Icon      = MakeMenuIcon("\uE72A"), // Forward
                IsEnabled = _hasMultipleDocumentHosts
            };
            moveNextGroup.Click += (_, _) => MoveToNextGroupRequested?.Invoke();
            menu.Items.Add(moveNextGroup);

            var movePrevGroup = new MenuItem
            {
                Header    = "Move to Previous Tab Group",
                Icon      = MakeMenuIcon("\uE72B"), // Back
                IsEnabled = _hasMultipleDocumentHosts
            };
            movePrevGroup.Click += (_, _) => MoveToPreviousGroupRequested?.Invoke();
            menu.Items.Add(movePrevGroup);

            // Close tab group (only for non-main document hosts)
            if (_hasMultipleDocumentHosts)
            {
                var closeGroupItem = new MenuItem
                {
                    Header = "Close Tab Group",
                    Icon   = MakeMenuIcon("\uE8BB") // ChromeClose
                };
                closeGroupItem.Click += (_, _) => CloseGroupRequested?.Invoke();
                menu.Items.Add(closeGroupItem);
            }

            menu.Items.Add(new Separator());
        }

        if (item.CanFloat)
        {
            var floatItem = new MenuItem
            {
                Header = "Float",
                Icon   = MakeMenuIcon("\uE8A7")  // OpenInNewWindow
            };
            floatItem.Click += (_, _) => FloatRequested?.Invoke();
            menu.Items.Add(floatItem);
        }

        var autoHideItem = new MenuItem
        {
            Header = "Auto-Hide",
            Icon   = MakeMenuIcon("\uE141")  // Pin
        };
        autoHideItem.Click += (_, _) => AutoHideRequested?.Invoke();
        menu.Items.Add(autoHideItem);

        if (item.Owner is not DocumentHostNode)
        {
            var dockAsDocItem = new MenuItem
            {
                Header = "Dock as Tabbed Document",
                Icon   = MakeMenuIcon("\uE737")  // TabletMode / dock-to-doc
            };
            dockAsDocItem.Click += (_, _) => DockAsDocumentRequested?.Invoke();
            menu.Items.Add(dockAsDocItem);
        }
        else if (item.Metadata.TryGetValue("_promotedPanel", out _))
        {
            var restoreItem = new MenuItem
            {
                Header = "Dock as Tool Window",
                Icon   = MakeMenuIcon("\uE8A0")  // DockLeft
            };
            restoreItem.Click += (_, _) => RestoreToToolPanelRequested?.Invoke();
            menu.Items.Add(restoreItem);
        }

        var hideItem = new MenuItem
        {
            Header = "Hide",
            Icon   = MakeMenuIcon("\uED1A")  // Hide
        };
        hideItem.Click += (_, _) => HideRequested?.Invoke();
        menu.Items.Add(hideItem);

        menu.Items.Add(new Separator());

        var closeItem = new MenuItem
        {
            Header    = "Close",
            Icon      = MakeMenuIcon("\uE8BB"),  // ChromeClose
            IsEnabled = item.CanClose
        };
        closeItem.Click += (_, _) => CloseClicked?.Invoke();
        menu.Items.Add(closeItem);

        var closeAllItem = new MenuItem
        {
            Header = "Close All",
            Icon   = MakeMenuIcon("\uE74D")  // Delete (close all)
        };
        closeAllItem.Click += (_, _) => CloseAllRequested?.Invoke();
        menu.Items.Add(closeAllItem);

        var closeAllButItem = new MenuItem
        {
            Header = "Close All But This",
            Icon   = MakeMenuIcon("\uE8C6")  // RemoveFrom
        };
        closeAllButItem.Click += (_, _) => CloseAllButThisRequested?.Invoke();
        menu.Items.Add(closeAllButItem);

        if (item.Owner is DocumentHostNode)
        {
            var closeAllButPinnedItem = new MenuItem
            {
                Header = "Close All But Pinned",
                Icon   = MakeMenuIcon("\uE8F4")  // FilterError / pin-protected close
            };
            closeAllButPinnedItem.Click += (_, _) => CloseAllButPinnedRequested?.Invoke();
            menu.Items.Add(closeAllButPinnedItem);
        }

        // Extensibility: let the application shell inject extra items (e.g. "Compare with…")
        var extraItems = ExtraMenuItemsFactory?.Invoke(item);
        if (extraItems is { Count: > 0 })
        {
            menu.Items.Add(new Separator());
            foreach (var mi in extraItems)
                menu.Items.Add(mi);
        }

        return menu;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Double-click: float the item (VS-style)
        if (e.ClickCount == 2)
        {
            if (_item.CanFloat)
                FloatRequested?.Invoke();
            e.Handled = true;
            return;
        }

        _dragStartPoint = e.GetPosition(this);
        _isDragging = false;
        _isReordering = false;
        CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;

        // Guard: PointToScreen throws InvalidOperationException when the Visual is detached
        // from its PresentationSource (HWND) during a drag/float transition.
        if (PresentationSource.FromVisual(this) is null) return;

        if (_item.Owner is DocumentHostNode)
        {
            if (_isDragging) return;

            var diff = e.GetPosition(this) - _dragStartPoint;

            // Float takes priority: a large vertical move cancels any pending reorder and floats the tab.
            // Checking this BEFORE _isReordering prevents the reorder state from permanently blocking float.
            if (Math.Abs(diff.Y) > FloatThresholdY)
            {
                var wasReordering = _isReordering;
                _isReordering = false;
                _isDragging   = true;
                ReleaseMouseCapture();
                // Clean up the reorder ghost/adorner before starting the float.
                // Without this, the Popup stays IsOpen=true and gets orphaned by RebuildVisualTree().
                if (wasReordering)
                    ReorderCancelled?.Invoke();
                DragStarted?.Invoke();
                return;
            }

            // Reorder: horizontal drag within the document tab strip.
            if (_isReordering)
            {
                // Pass true screen coordinates for consistent hit-testing in DockTabControl
                ReorderDragging?.Invoke(PointToScreen(e.GetPosition(this)));
                return;
            }
            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance)
            {
                _isReordering = true;
                ReorderDragging?.Invoke(PointToScreen(e.GetPosition(this)));
            }
        }
        else
        {
            if (_isDragging) return;
            var diff = e.GetPosition(this) - _dragStartPoint;
            if (Math.Abs(diff.X) > FloatThresholdTool ||
                Math.Abs(diff.Y) > FloatThresholdTool)
            {
                _isDragging = true;
                ReleaseMouseCapture();
                DragStarted?.Invoke();
            }
        }
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isReordering)
        {
            _isReordering = false;
            // Guard: Visual may be detached during float transition — cancel reorder instead of drop.
            if (PresentationSource.FromVisual(this) is not null)
                ReorderDropped?.Invoke(PointToScreen(e.GetPosition(this)));
            else
                ReorderCancelled?.Invoke();
        }
        _isDragging = false;
        ReleaseMouseCapture();
    }
}
