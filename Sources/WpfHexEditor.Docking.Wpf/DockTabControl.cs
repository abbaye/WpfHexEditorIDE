//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Docking.Core.Nodes;

namespace WpfHexEditor.Docking.Wpf;

/// <summary>
/// WPF projection of <see cref="DockGroupNode"/>: a TabControl with draggable tabs.
/// </summary>
public class DockTabControl : TabControl
{
    public DockGroupNode? Node { get; private set; }

    public event Action<DockItem>? TabDragStarted;
    public event Action<DockItem>? TabCloseRequested;
    public event Action<DockItem>? TabFloatRequested;
    public event Action<DockItem>? TabAutoHideRequested;

    public void Bind(DockGroupNode node, Func<DockItem, object>? contentFactory = null)
    {
        Node = node;
        Items.Clear();

        foreach (var item in node.Items)
        {
            var tabItem = CreateTabItem(item, contentFactory);
            Items.Add(tabItem);
        }

        if (node.ActiveItem is not null)
        {
            var activeIndex = node.Items.ToList().IndexOf(node.ActiveItem);
            if (activeIndex >= 0)
                SelectedIndex = activeIndex;
        }
    }

    private TabItem CreateTabItem(DockItem item, Func<DockItem, object>? contentFactory)
    {
        var header = new DockTabHeader(item);
        header.CloseClicked += () => TabCloseRequested?.Invoke(item);
        header.DragStarted += () => TabDragStarted?.Invoke(item);
        header.FloatRequested += () => TabFloatRequested?.Invoke(item);
        header.AutoHideRequested += () => TabAutoHideRequested?.Invoke(item);
        header.CloseAllRequested += () => CloseAllItems();
        header.CloseAllButThisRequested += () => CloseAllButItem(item);

        var tabItem = new TabItem
        {
            Header = header,
            Content = contentFactory?.Invoke(item) ?? new TextBlock
            {
                Text = $"Content: {item.Title}",
                Margin = new Thickness(8),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            },
            Tag = item
        };

        return tabItem;
    }

    private void CloseAllItems()
    {
        if (Node is null) return;
        foreach (var item in Node.Items.ToList())
            if (item.CanClose)
                TabCloseRequested?.Invoke(item);
    }

    private void CloseAllButItem(DockItem keep)
    {
        if (Node is null) return;
        foreach (var item in Node.Items.ToList())
            if (item != keep && item.CanClose)
                TabCloseRequested?.Invoke(item);
    }
}

/// <summary>
/// Tab header with title, close button, context menu, and drag support.
/// </summary>
public class DockTabHeader : StackPanel
{
    private readonly DockItem _item;
    private Point _dragStartPoint;
    private bool _isDragging;

    public event Action? CloseClicked;
    public event Action? DragStarted;
    public event Action? FloatRequested;
    public event Action? AutoHideRequested;
    public event Action? CloseAllRequested;
    public event Action? CloseAllButThisRequested;

    public DockTabHeader(DockItem item)
    {
        _item = item;
        Orientation = Orientation.Horizontal;

        var titleBlock = new TextBlock
        {
            Text = item.Title,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0)
        };
        Children.Add(titleBlock);

        // Pin button (auto-hide toggle) - VS-style pushpin
        var pinButton = new Button
        {
            Content = "\uD83D\uDCCC",
            FontSize = 9,
            Padding = new Thickness(2, 0, 2, 0),
            Margin = new Thickness(0, 0, 1, 0),
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "Auto-Hide"
        };
        pinButton.Click += (_, _) => AutoHideRequested?.Invoke();
        Children.Add(pinButton);

        if (item.CanClose)
        {
            var closeButton = new Button
            {
                Content = "\u00D7",
                FontSize = 10,
                Padding = new Thickness(2, 0, 2, 0),
                Margin = new Thickness(0),
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Close"
            };
            closeButton.Click += (_, _) => CloseClicked?.Invoke();
            Children.Add(closeButton);
        }

        // Context menu
        ContextMenu = BuildContextMenu(item);

        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
    }

    private ContextMenu BuildContextMenu(DockItem item)
    {
        var menu = new ContextMenu();

        if (item.CanFloat)
        {
            var floatItem = new MenuItem { Header = "Float" };
            floatItem.Click += (_, _) => FloatRequested?.Invoke();
            menu.Items.Add(floatItem);
        }

        var autoHideItem = new MenuItem { Header = "Auto-Hide" };
        autoHideItem.Click += (_, _) => AutoHideRequested?.Invoke();
        menu.Items.Add(autoHideItem);

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

        return menu;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
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
