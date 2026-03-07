//////////////////////////////////////////////
// Apache 2.0  - 2026
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
using WpfHexEditor.Docking.Core.Nodes;
using WpfHexEditor.Docking.Wpf.Automation;

namespace WpfHexEditor.Docking.Wpf;

/// <summary>
/// WPF projection of <see cref="DockGroupNode"/>: a TabControl with draggable tabs.
/// </summary>
public class DockTabControl : TabControl
{
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
    public event Action<DockItem>? TabFloatRequested;
    public event Action<DockItem>? TabAutoHideRequested;
    public event Action<DockItem>? TabHideRequested;
    public event Action<DockItem>? TabDockAsDocumentRequested;
    public event Action<DockItem>? TabPinToggleRequested;
    public event Action<DockItem>? TabStickyToggleRequested;
    public event Action<DockItem, int>? TabReorderRequested;

    private Func<DockItem, object>? _contentFactory;
    private int  _dragOriginalModelIndex = -1;
    private int  _currentInsertionIdx    = -1;
    private Popup?                   _reorderGhost;
    private ReorderInsertionAdorner? _reorderAdorner;

    public void Bind(DockGroupNode node, Func<DockItem, object>? contentFactory = null)
    {
        Node = node;
        _contentFactory = contentFactory;
        Items.Clear();

        foreach (var item in node.Items)
        {
            var isActive = item == node.ActiveItem;
            var tabItem = CreateTabItem(item, contentFactory, isActive);
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
                    tab.Content = _contentFactory.Invoke(item);
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
        var header = new DockTabHeader(item);
        header.CloseClicked             += () => TabCloseRequested?.Invoke(item);
        header.DragStarted              += () => TabDragStarted?.Invoke(item);
        header.FloatRequested           += () => TabFloatRequested?.Invoke(item);
        header.AutoHideRequested        += () => TabAutoHideRequested?.Invoke(item);
        header.HideRequested            += () => TabHideRequested?.Invoke(item);
        header.DockAsDocumentRequested  += () => TabDockAsDocumentRequested?.Invoke(item);
        header.CloseAllRequested        += () => CloseAllItems();
        header.CloseAllButThisRequested += () => CloseAllButItem(item);
        header.PinToggleRequested       += () => TabPinToggleRequested?.Invoke(item);
        header.StickyToggleRequested    += () => TabStickyToggleRequested?.Invoke(item);
        header.CloseAllButPinnedRequested += () => CloseAllButPinnedItems();
        header.ReorderDragging          += pos => OnHeaderReorderDragging(item, pos);
        header.ReorderDropped           += pos => OnHeaderReorderDropped(item, pos);
        header.ReorderCancelled         += () => OnHeaderReorderCancelled(item);

        var tabItem = new TabItem
        {
            Header = header,
            Tag = item,
            Content = isActive || contentFactory is null
                ? (contentFactory?.Invoke(item) ?? DefaultContent(item))
                : new LazyContentPlaceholder(item)
        };
        tabItem.SetResourceReference(StyleProperty, "DockTabItemStyle");

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
        foreach (var item in Node.Items.ToList())
            if (item.CanClose && !item.IsPinned)
                TabCloseRequested?.Invoke(item);
    }

    private void CloseAllButItem(DockItem keep)
    {
        if (Node is null) return;
        foreach (var item in Node.Items.ToList())
            if (item != keep && item.CanClose)
                TabCloseRequested?.Invoke(item);
    }

    private void CloseAllButPinnedItems()
    {
        if (Node is null) return;
        foreach (var item in Node.Items.ToList())
            if (!item.IsPinned && item.CanClose)
                TabCloseRequested?.Invoke(item);
    }
}

/// <summary>
/// Tab header with title, close button, context menu, and drag support.
/// </summary>
public class DockTabHeader : StackPanel
{
    private readonly DockItem _item;
    private Button?    _closeButton;
    private Button?    _pinButton;
    private TextBlock? _titleBlock;
    private Point _dragStartPoint;
    private bool _isDragging;
    private bool _isReordering;
    private const double FloatThresholdY = 20.0;

    public event Action? CloseClicked;
    public event Action? DragStarted;
    public event Action? FloatRequested;
    public event Action<Point>? ReorderDragging;
    public event Action<Point>? ReorderDropped;
    public event Action? ReorderCancelled;
    public event Action? AutoHideRequested;
    public event Action? HideRequested;
    public event Action? DockAsDocumentRequested;
    public event Action? CloseAllRequested;
    public event Action? CloseAllButThisRequested;
    public event Action? PinToggleRequested;
    public event Action? StickyToggleRequested;
    public event Action? CloseAllButPinnedRequested;

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

        _titleBlock = new TextBlock
        {
            Text             = item.Title,
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

        // React to title changes (e.g. "file *" dirty flag)
        item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DockItem.Title) && _titleBlock is not null)
                _titleBlock.Text = item.Title;
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

        // Context menu
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

    private ContextMenu BuildContextMenu(DockItem item)
    {
        var menu = new ContextMenu();

        // Pin/Unpin + Keep Tab Visible — only for document tabs
        if (item.Owner is DocumentHostNode)
        {
            var pinMenuItem = new MenuItem { Header = item.IsPinned ? "Unpin Tab" : "Pin Tab" };
            pinMenuItem.Click += (_, _) => PinToggleRequested?.Invoke();
            menu.Items.Add(pinMenuItem);

            // IsSticky: keeps the tab permanently in the tab strip (never overflowed).
            var stickyMenuItem = new MenuItem
            {
                Header      = item.IsSticky ? "Remove from Tab Strip Pin" : "Keep Tab Visible",
                IsCheckable = true,
                IsChecked   = item.IsSticky
            };
            stickyMenuItem.Click += (_, _) => StickyToggleRequested?.Invoke();
            menu.Items.Add(stickyMenuItem);

            menu.Items.Add(new Separator());
        }

        if (item.CanFloat)
        {
            var floatItem = new MenuItem { Header = "Float" };
            floatItem.Click += (_, _) => FloatRequested?.Invoke();
            menu.Items.Add(floatItem);
        }

        var autoHideItem = new MenuItem { Header = "Auto-Hide" };
        autoHideItem.Click += (_, _) => AutoHideRequested?.Invoke();
        menu.Items.Add(autoHideItem);

        if (item.Owner is not DocumentHostNode)
        {
            var dockAsDocItem = new MenuItem { Header = "Dock as Tabbed Document" };
            dockAsDocItem.Click += (_, _) => DockAsDocumentRequested?.Invoke();
            menu.Items.Add(dockAsDocItem);
        }

        var hideItem = new MenuItem { Header = "Hide" };
        hideItem.Click += (_, _) => HideRequested?.Invoke();
        menu.Items.Add(hideItem);

        menu.Items.Add(new Separator());

        if (item.CanClose)
        {
            var closeItem = new MenuItem { Header = "Close" };
            closeItem.Click += (_, _) => CloseClicked?.Invoke();
            menu.Items.Add(closeItem);
        }

        var closeAllItem = new MenuItem { Header = "Close All" };
        closeAllItem.Click += (_, _) => CloseAllRequested?.Invoke();
        menu.Items.Add(closeAllItem);

        var closeAllButItem = new MenuItem { Header = "Close All But This" };
        closeAllButItem.Click += (_, _) => CloseAllButThisRequested?.Invoke();
        menu.Items.Add(closeAllButItem);

        if (item.Owner is DocumentHostNode)
        {
            var closeAllButPinnedItem = new MenuItem { Header = "Close All But Pinned" };
            closeAllButPinnedItem.Click += (_, _) => CloseAllButPinnedRequested?.Invoke();
            menu.Items.Add(closeAllButPinnedItem);
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

        if (_item.Owner is DocumentHostNode)
        {
            if (_isDragging) return;

            var diff = e.GetPosition(this) - _dragStartPoint;

            // Float takes priority: a large vertical move cancels any pending reorder and floats the tab.
            // Checking this BEFORE _isReordering prevents the reorder state from permanently blocking float.
            if (Math.Abs(diff.Y) > FloatThresholdY)
            {
                _isReordering = false;
                _isDragging   = true;
                ReleaseMouseCapture();
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
            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
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
            ReorderDropped?.Invoke(PointToScreen(e.GetPosition(this)));
            _isReordering = false;
        }
        _isDragging = false;
        ReleaseMouseCapture();
    }
}
