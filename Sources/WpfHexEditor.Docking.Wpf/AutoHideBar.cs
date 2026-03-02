//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Docking.Core;
using WpfHexEditor.Docking.Core.Nodes;
using WpfHexEditor.Docking.Wpf.Automation;

namespace WpfHexEditor.Docking.Wpf;

/// <summary>
/// A bar on the edge of the dock area that shows buttons for auto-hidden panels.
/// </summary>
public class AutoHideBar : StackPanel
{
    public Dock Position { get; }

    /// <summary>
    /// Raised when a panel button is clicked to show/toggle its popup.
    /// </summary>
    public event Action<DockItem>? ItemClicked;

    public AutoHideBar(Dock position)
    {
        Position = position;
        Orientation = position is Dock.Top or Dock.Bottom
            ? Orientation.Horizontal
            : Orientation.Vertical;
        SetResourceReference(BackgroundProperty, "DockMenuBackgroundBrush");
    }

    protected override AutomationPeer OnCreateAutomationPeer() =>
        new AutoHideBarAutomationPeer(this);

    /// <summary>
    /// Updates the bar with the given auto-hide items.
    /// </summary>
    public void UpdateItems(IEnumerable<DockItem> items)
    {
        Children.Clear();

        foreach (var item in items)
        {
            var button = new Button
            {
                Content = new TextBlock
                {
                    Text = item.Title,
                    LayoutTransform = Position is Dock.Left or Dock.Right
                        ? new RotateTransform(Position == Dock.Left ? -90 : 90)
                        : Transform.Identity
                },
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(1),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Tag = item
            };

            button.SetResourceReference(Control.ForegroundProperty, "DockTabTextBrush");
            AutomationProperties.SetName(button, $"Show {item.Title}");
            button.Click += (_, _) => ItemClicked?.Invoke(item);
            Children.Add(button);
        }

        Visibility = Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}

/// <summary>
/// An overlay panel that slides in from the edge to show an auto-hidden panel's content.
/// Supports mouse-drag resize on the inner edge and drag-to-float from the title bar.
/// </summary>
public class AutoHideFlyout : Grid
{
    private readonly Border         _panel;
    private readonly ContentControl _contentHost;
    private readonly Border         _clickCatcher;
    private readonly TextBlock      _titleBlock;
    private readonly Border         _resizeHandle;
    private readonly Grid           _panelContainer;

    private DockSide _currentSide;
    private bool     _resizing;
    private Point    _resizeStart;
    private double   _resizeStartSize;

    private const double DefaultSideSize      = 300;
    private const double DefaultTopBottomSize = 250;
    private const double MinSize              = 80;
    private const double ResizeThickness      = 6;

    public DockItem? CurrentItem { get; private set; }
    public bool IsOpen => _panelContainer.Visibility == Visibility.Visible;

    /// <summary>
    /// Raised when the user clicks the pin button or double-clicks the title bar to re-dock.
    /// </summary>
    public event Action<DockItem>? RestoreRequested;

    /// <summary>
    /// Raised when the user clicks close.
    /// </summary>
    public event Action<DockItem>? CloseRequested;

    /// <summary>
    /// Raised when the user chooses "Float" in the menu or drags the title bar.
    /// </summary>
    public event Action<DockItem>? FloatRequested;

    public AutoHideFlyout()
    {
        // Full overlay — covers the entire DockControl area
        Visibility = Visibility.Collapsed;

        // Click-catcher: transparent background dismisses the flyout on outside click
        _clickCatcher = new Border { Background = Brushes.Transparent };
        _clickCatcher.MouseLeftButtonDown += (_, _) => Close();
        Children.Add(_clickCatcher);

        // ── Title bar ────────────────────────────────────────────────────────────────
        _titleBlock = new TextBlock
        {
            FontWeight        = FontWeights.SemiBold,
            FontSize          = 12,
            Margin            = new Thickness(8, 6, 8, 6),
            VerticalAlignment = VerticalAlignment.Center
        };
        _titleBlock.SetResourceReference(TextBlock.ForegroundProperty, "DockMenuForegroundBrush");

        Button MakeTitleButton(string content, string tooltip)
        {
            var btn = new Button
            {
                Content    = content,
                FontSize   = 10,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                ToolTip    = tooltip
            };
            var titleStyle = Application.Current?.TryFindResource("DockTitleButtonStyle") as Style;
            if (titleStyle is not null)
                btn.Style = titleStyle;
            return btn;
        }

        var closeButton = MakeTitleButton("\uE8BB", "Close");
        closeButton.Click += (_, _) => { if (CurrentItem is not null) CloseRequested?.Invoke(CurrentItem); };

        var pinButton = MakeTitleButton("\uE141", "Dock");
        pinButton.Click += (_, _) => { if (CurrentItem is not null) RestoreRequested?.Invoke(CurrentItem); };

        var chevronButton = MakeTitleButton("\uE70D", "Options");
        chevronButton.Click += (sender, _) =>
        {
            if (CurrentItem is null || sender is not Button btn) return;
            var item = CurrentItem;

            var menuBg     = TryFindResource("DockMenuBackgroundBrush") as Brush ?? Brushes.DarkGray;
            var menuFg     = TryFindResource("DockMenuForegroundBrush") as Brush ?? Brushes.White;
            var menuBorder = TryFindResource("DockMenuBorderBrush") as Brush ?? Brushes.Gray;

            var menu = new ContextMenu
            {
                Background  = menuBg,
                BorderBrush = menuBorder,
                Foreground  = menuFg
            };

            var dockItem = new MenuItem { Header = "Dock", Foreground = menuFg };
            dockItem.Click += (_, _) => RestoreRequested?.Invoke(item);
            menu.Items.Add(dockItem);

            var autoHideItem = new MenuItem
            {
                Header     = "Auto Hide",
                Foreground = Brushes.Gray,
                IsEnabled  = false
            };
            menu.Items.Add(autoHideItem);

            var floatItem = new MenuItem { Header = "Float", Foreground = menuFg };
            floatItem.Click += (_, _) => FloatRequested?.Invoke(item);
            menu.Items.Add(floatItem);

            menu.Items.Add(new Separator());

            var closeItem = new MenuItem { Header = "Close", Foreground = menuFg };
            closeItem.Click += (_, _) => CloseRequested?.Invoke(item);
            menu.Items.Add(closeItem);

            menu.PlacementTarget = btn;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        };

        var titleContent = new DockPanel();
        DockPanel.SetDock(closeButton,   Dock.Right);
        DockPanel.SetDock(pinButton,     Dock.Right);
        DockPanel.SetDock(chevronButton, Dock.Right);
        titleContent.Children.Add(closeButton);
        titleContent.Children.Add(pinButton);
        titleContent.Children.Add(chevronButton);
        titleContent.Children.Add(_titleBlock);

        var titleBar = new Border
        {
            Child  = titleContent,
            Cursor = Cursors.SizeAll
        };
        titleBar.SetResourceReference(Border.BackgroundProperty, "DockMenuBackgroundBrush");

        // Drag-to-float: drag the title bar beyond the system threshold to float the panel
        var titleDragStart   = new Point();
        var titleDragPending = false;

        titleBar.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount >= 2) return;
            titleDragStart   = e.GetPosition(titleBar);
            titleDragPending = true;
            titleBar.CaptureMouse();
            e.Handled = true;
        };

        titleBar.MouseMove += (_, e) =>
        {
            if (!titleDragPending || e.LeftButton != MouseButtonState.Pressed) return;
            var diff = e.GetPosition(titleBar) - titleDragStart;
            if (Math.Abs(diff.X) <= SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(diff.Y) <= SystemParameters.MinimumVerticalDragDistance) return;

            titleDragPending = false;
            titleBar.ReleaseMouseCapture();
            if (CurrentItem is not null) FloatRequested?.Invoke(CurrentItem);
        };

        titleBar.MouseLeftButtonUp += (_, _) =>
        {
            titleDragPending = false;
            titleBar.ReleaseMouseCapture();
        };

        // ── Content ──────────────────────────────────────────────────────────────────
        _contentHost = new ContentControl();

        var innerStack = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(titleBar, Dock.Top);
        innerStack.Children.Add(titleBar);
        innerStack.Children.Add(_contentHost);

        _panel = new Border
        {
            BorderThickness = new Thickness(1),
            Child           = innerStack
        };
        _panel.SetResourceReference(Border.BackgroundProperty, "DockBackgroundBrush");
        _panel.SetResourceReference(Border.BorderBrushProperty, "DockBorderBrush");

        // ── Resize handle ─────────────────────────────────────────────────────────
        // Transparent strip placed on the inner edge; position/cursor configured in ShowForItem.
        _resizeHandle = new Border { Background = Brushes.Transparent };
        _resizeHandle.MouseLeftButtonDown += OnResizeStart;
        _resizeHandle.MouseMove           += OnResizeMove;
        _resizeHandle.MouseLeftButtonUp   += (_, _) => OnResizeEnd();
        _resizeHandle.LostMouseCapture    += (_, _) => OnResizeEnd();

        // ── Panel container: _panel + resize handle overlaid ─────────────────────
        _panelContainer = new Grid();
        _panelContainer.Children.Add(_panel);
        _panelContainer.Children.Add(_resizeHandle);

        Children.Add(_panelContainer);
    }

    /// <summary>
    /// Shows the flyout with content for the given item on the specified side.
    /// </summary>
    public void ShowForItem(DockItem item, Func<DockItem, object>? contentFactory = null, DockSide side = DockSide.Bottom)
    {
        CurrentItem  = item;
        _currentSide = side;
        _titleBlock.Text = item.Title;

        // Position the container against the correct edge
        _panelContainer.HorizontalAlignment = side switch
        {
            DockSide.Left  => HorizontalAlignment.Left,
            DockSide.Right => HorizontalAlignment.Right,
            _              => HorizontalAlignment.Stretch
        };

        _panelContainer.VerticalAlignment = side switch
        {
            DockSide.Top    => VerticalAlignment.Top,
            DockSide.Bottom => VerticalAlignment.Bottom,
            _               => VerticalAlignment.Stretch
        };

        if (side is DockSide.Left or DockSide.Right)
        {
            _panelContainer.Width  = DefaultSideSize;
            _panelContainer.Height = double.NaN;

            // Resize handle: thin strip on the inner edge (right for Left panel, left for Right panel)
            _resizeHandle.Width             = ResizeThickness;
            _resizeHandle.Height            = double.NaN;
            _resizeHandle.Cursor            = Cursors.SizeWE;
            _resizeHandle.HorizontalAlignment = side == DockSide.Left
                ? HorizontalAlignment.Right
                : HorizontalAlignment.Left;
            _resizeHandle.VerticalAlignment = VerticalAlignment.Stretch;
        }
        else
        {
            _panelContainer.Height = DefaultTopBottomSize;
            _panelContainer.Width  = double.NaN;

            // Resize handle: thin strip on the inner edge (bottom for Top panel, top for Bottom panel)
            _resizeHandle.Height            = ResizeThickness;
            _resizeHandle.Width             = double.NaN;
            _resizeHandle.Cursor            = Cursors.SizeNS;
            _resizeHandle.VerticalAlignment   = side == DockSide.Top
                ? VerticalAlignment.Bottom
                : VerticalAlignment.Top;
            _resizeHandle.HorizontalAlignment = HorizontalAlignment.Stretch;
        }

        _contentHost.Content = contentFactory?.Invoke(item) ?? new TextBlock
        {
            Text       = $"Auto-hide: {item.Title}",
            Foreground = Brushes.White,
            Margin     = new Thickness(8)
        };

        Visibility = Visibility.Visible;
    }

    public void Close()
    {
        Visibility           = Visibility.Collapsed;
        _contentHost.Content = null;
        CurrentItem          = null;
    }

    // ── Resize ───────────────────────────────────────────────────────────────────

    private void OnResizeStart(object sender, MouseButtonEventArgs e)
    {
        _resizing        = true;
        _resizeStart     = e.GetPosition(this);
        _resizeStartSize = _currentSide is DockSide.Left or DockSide.Right
            ? _panelContainer.Width
            : _panelContainer.Height;
        _resizeHandle.CaptureMouse();
        e.Handled = true;
    }

    private void OnResizeMove(object sender, MouseEventArgs e)
    {
        if (!_resizing || e.LeftButton != MouseButtonState.Pressed) return;

        var current = e.GetPosition(this);
        var delta   = current - _resizeStart;

        switch (_currentSide)
        {
            case DockSide.Left:
                _panelContainer.Width  = Math.Max(MinSize, _resizeStartSize + delta.X);
                break;
            case DockSide.Right:
                _panelContainer.Width  = Math.Max(MinSize, _resizeStartSize - delta.X);
                break;
            case DockSide.Top:
                _panelContainer.Height = Math.Max(MinSize, _resizeStartSize + delta.Y);
                break;
            case DockSide.Bottom:
                _panelContainer.Height = Math.Max(MinSize, _resizeStartSize - delta.Y);
                break;
        }

        e.Handled = true;
    }

    private void OnResizeEnd()
    {
        if (!_resizing) return;
        _resizing = false;
        _resizeHandle.ReleaseMouseCapture();
    }
}
