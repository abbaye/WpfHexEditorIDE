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

    private Func<DockItem, object>? _contentFactory;

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

        if (_contentFactory is null) return;

        foreach (var added in e.AddedItems)
        {
            if (added is TabItem { Tag: DockItem item } tab && tab.Content is LazyContentPlaceholder)
                tab.Content = _contentFactory.Invoke(item);
        }
    }

    private TabItem CreateTabItem(DockItem item, Func<DockItem, object>? contentFactory, bool isActive)
    {
        var header = new DockTabHeader(item);
        header.CloseClicked += () => TabCloseRequested?.Invoke(item);
        header.DragStarted += () => TabDragStarted?.Invoke(item);
        header.FloatRequested += () => TabFloatRequested?.Invoke(item);
        header.AutoHideRequested += () => TabAutoHideRequested?.Invoke(item);
        header.HideRequested += () => TabHideRequested?.Invoke(item);
        header.DockAsDocumentRequested += () => TabDockAsDocumentRequested?.Invoke(item);
        header.CloseAllRequested += () => CloseAllItems();
        header.CloseAllButThisRequested += () => CloseAllButItem(item);
        header.PinToggleRequested += () => TabPinToggleRequested?.Invoke(item);
        header.CloseAllButPinnedRequested += () => CloseAllButPinnedItems();

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
    private Button? _closeButton;
    private Button? _pinButton;
    private Point _dragStartPoint;
    private bool _isDragging;

    public event Action? CloseClicked;
    public event Action? DragStarted;
    public event Action? FloatRequested;
    public event Action? AutoHideRequested;
    public event Action? HideRequested;
    public event Action? DockAsDocumentRequested;
    public event Action? CloseAllRequested;
    public event Action? CloseAllButThisRequested;
    public event Action? PinToggleRequested;
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

        var titleBlock = new TextBlock
        {
            Text = item.Title,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0)
        };
        Children.Add(titleBlock);

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
                BorderThickness = new Thickness(0),
                Background      = Brushes.Transparent,
                Foreground      = Brushes.Transparent, // inherit from tab style via binding below
                Cursor          = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip         = "Auto-Hide"
            };
            pinButton.SetResourceReference(Button.ForegroundProperty, "DockTabTextBrush");
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
                BorderThickness = new Thickness(0),
                Background      = Brushes.Transparent,
                Cursor          = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip         = item.IsPinned ? "Unpin Tab" : "Pin Tab",
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(item.IsPinned ? 0 : 90),
                Opacity         = item.IsPinned ? 1 : 0
            };
            _pinButton.SetResourceReference(Button.ForegroundProperty, "DockTabTextBrush");
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
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Close",
                Opacity = 0 // VS2026: hidden by default, shown on hover or when active
            };
            _closeButton.SetResourceReference(Button.ForegroundProperty, "DockTabTextBrush");
            AutomationProperties.SetName(_closeButton, $"Close {item.Title}");
            _closeButton.Click += (_, _) => CloseClicked?.Invoke();
            Children.Add(_closeButton);
        }

        // Context menu
        ContextMenu = BuildContextMenu(item);

        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;

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

        // Pin/Unpin — only for document tabs
        if (item.Owner is DocumentHostNode)
        {
            var pinMenuItem = new MenuItem { Header = item.IsPinned ? "Unpin Tab" : "Pin Tab" };
            pinMenuItem.Click += (_, _) => PinToggleRequested?.Invoke();
            menu.Items.Add(pinMenuItem);
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
        CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _isDragging) return;

        var currentPos = e.GetPosition(this);
        var diff = currentPos - _dragStartPoint;

        if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
        {
            _isDragging = true;
            ReleaseMouseCapture();
            DragStarted?.Invoke();
        }
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        ReleaseMouseCapture();
    }
}
