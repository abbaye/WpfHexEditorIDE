// ==========================================================
// Project: WpfHexEditor.Shell
// File: AutoHideBar.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     A WPF StackPanel-based bar displayed at the edges of the dock area that
//     renders buttons for auto-hidden panels. Clicking a button raises ItemClicked
//     to toggle the panel's flyout. Integrates with AutoHideBarHoverPreview for
//     thumbnail previews on hover.
//
// Architecture Notes:
//     Inherits StackPanel for automatic horizontal/vertical layout based on Dock position.
//     Uses AutoHideBarAutomationPeer for full UI Automation (MSAA/UIA) accessibility support.
//     DynamicResource binding to DockMenuBackgroundBrush ensures theme compliance.
//
// ==========================================================

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WpfHexEditor.Docking.Core;
using WpfHexEditor.Docking.Core.Nodes;
using WpfHexEditor.Shell.Automation;

namespace WpfHexEditor.Shell;

/// <summary>
/// A bar on the edge of the dock area that shows buttons for auto-hidden panels.
/// </summary>
public class AutoHideBar : StackPanel
{
    public Dock Position { get; }

    /// <summary>
    /// Raised when a panel button (or group button) is clicked to show/toggle its flyout.
    /// The list contains one item for a solo panel, or all group members for a grouped entry.
    /// </summary>
    public event Action<IReadOnlyList<DockItem>>? GroupClicked;

    /// <summary>
    /// Raised after <see cref="UpdateItems"/> rebuilds the button list,
    /// so attached hover-preview helpers can re-wire mouse events.
    /// </summary>
    public event Action? ItemsUpdated;

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
    /// Items that share an <see cref="DockItem.AutoHideGroupId"/> are merged into a single button.
    /// </summary>
    public void UpdateItems(IEnumerable<DockItem> items)
    {
        Children.Clear();

        // Group by AutoHideGroupId; items without an ID each form their own single-item group.
        var groups = items
            .GroupBy(i => (object?)i.AutoHideGroupId ?? (object)i)
            .Select(g => g.ToList())
            .ToList();

        foreach (var groupItems in groups)
        {
            var label = groupItems.Count == 1
                ? groupItems[0].Title
                : string.Join(" \u00b7 ", groupItems.Select(i => i.Title));  // U+00B7 = middle dot

            var button = new Button
            {
                Content = new TextBlock
                {
                    Text = label,
                    LayoutTransform = Position is Dock.Left or Dock.Right
                        ? new RotateTransform(Position == Dock.Left ? -90 : 90)
                        : Transform.Identity
                },
                Padding = new Thickness(6, 4, 6, 4),
                Margin  = new Thickness(1),
                Tag     = groupItems
            };

            // Apply themed style so IsMouseOver uses DockTabHoverBrush instead of default WPF chrome.
            button.SetResourceReference(FrameworkElement.StyleProperty, "DockTitleButtonStyle");
            AutomationProperties.SetName(button, $"Show {label}");
            var captured = (IReadOnlyList<DockItem>)groupItems;
            button.Click += (_, _) => GroupClicked?.Invoke(captured);
            Children.Add(button);
        }

        Visibility = Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ItemsUpdated?.Invoke();
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

    private IReadOnlyList<DockItem> _currentGroup = [];
    private StackPanel? _tabStrip;
    private Func<DockItem, object>? _contentFactory;

    public DockItem? CurrentItem { get; private set; }

    /// <summary>
    /// All items currently shown in the flyout (one item for solo panels, multiple for grouped).
    /// </summary>
    public IReadOnlyList<DockItem> CurrentGroup => _currentGroup;

    public bool IsOpen => _panelContainer.Visibility == Visibility.Visible;

    /// <summary>
    /// Returns the panel container element so the host can capture a snapshot before closing.
    /// </summary>
    public UIElement PanelElement => _panelContainer;

    /// <summary>
    /// Raised when the user clicks the pin button or double-clicks the title bar to re-dock.
    /// </summary>
    public event Action<DockItem>? RestoreRequested;

    /// <summary>
    /// Raised at the very start of <see cref="Close"/>, before the hide animation begins,
    /// so the host can capture a snapshot while the panel content is still fully visible.
    /// Covers all dismiss paths including the click-catcher (outside click).
    /// </summary>
    public event Action? Dismissing;

    /// <summary>
    /// Raised after the open animation completes and after <see cref="DispatcherPriority.Render"/>
    /// so the host can capture a high-quality snapshot when the panel is at full size and fully painted.
    /// This is the preferred capture point; <see cref="Dismissing"/> is the fallback.
    /// </summary>
    public event Action? SnapshotReady;

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

        // -- Title bar ----------------------------------------------------------------
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

        // -- Content ------------------------------------------------------------------
        _contentHost = new ContentControl();

        // Tab strip: shown only when multiple grouped items are in the flyout
        _tabStrip = new StackPanel { Orientation = Orientation.Horizontal, Visibility = Visibility.Collapsed };
        _tabStrip.SetResourceReference(BackgroundProperty, "DockMenuBackgroundBrush");

        var innerStack = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(titleBar,  Dock.Top);
        DockPanel.SetDock(_tabStrip, Dock.Top);
        innerStack.Children.Add(titleBar);
        innerStack.Children.Add(_tabStrip);
        innerStack.Children.Add(_contentHost);

        _panel = new Border
        {
            BorderThickness = new Thickness(1),
            Child           = innerStack
        };
        _panel.SetResourceReference(Border.BackgroundProperty, "DockBackgroundBrush");
        _panel.SetResourceReference(Border.BorderBrushProperty, "DockBorderBrush");

        // -- Resize handle ---------------------------------------------------------
        // Transparent strip placed on the inner edge; position/cursor configured in ShowForItem.
        _resizeHandle = new Border { Background = Brushes.Transparent };
        _resizeHandle.MouseLeftButtonDown += OnResizeStart;
        _resizeHandle.MouseMove           += OnResizeMove;
        _resizeHandle.MouseLeftButtonUp   += (_, _) => OnResizeEnd();
        _resizeHandle.LostMouseCapture    += (_, _) => OnResizeEnd();

        // -- Panel container: _panel + resize handle overlaid ---------------------
        _panelContainer = new Grid();
        _panelContainer.Children.Add(_panel);
        _panelContainer.Children.Add(_resizeHandle);

        Children.Add(_panelContainer);
    }

    /// <summary>
    /// Shows the flyout for a group of items (one or more). When multiple items are present,
    /// a tab strip is rendered below the title bar so the user can switch between them.
    /// </summary>
    public void ShowForItems(IReadOnlyList<DockItem> items, Func<DockItem, object>? contentFactory = null, DockSide side = DockSide.Bottom)
    {
        if (items.Count == 0) return;
        _currentGroup   = items;
        _contentFactory = contentFactory;
        ShowForItem(items[0], contentFactory, side);

        // Build tab strip when there are multiple items in the group
        if (_tabStrip is null) return;
        _tabStrip.Children.Clear();
        if (items.Count > 1)
        {
            foreach (var tabItem in items)
            {
                var captured = tabItem;
                var tabBtn = new Button
                {
                    Content = tabItem.Title,
                    Padding = new Thickness(8, 3, 8, 3),
                    Margin  = new Thickness(1, 0, 0, 0)
                };
                tabBtn.SetResourceReference(StyleProperty, "DockTitleButtonStyle");
                tabBtn.Click += (_, _) => SelectGroupTab(captured);
                _tabStrip.Children.Add(tabBtn);
            }
            _tabStrip.Visibility = Visibility.Visible;
            UpdateTabStripSelection();
        }
        else
        {
            _tabStrip.Visibility = Visibility.Collapsed;
        }
    }

    private void SelectGroupTab(DockItem item)
    {
        CurrentItem = item;
        _titleBlock.Text = item.Title;
        _contentHost.Content = _contentFactory?.Invoke(item) ?? new TextBlock
        {
            Text       = $"Auto-hide: {item.Title}",
            Foreground = Brushes.White,
            Margin     = new Thickness(8)
        };
        UpdateTabStripSelection();
    }

    private void UpdateTabStripSelection()
    {
        if (_tabStrip is null) return;
        foreach (Button btn in _tabStrip.Children.OfType<Button>())
        {
            bool isActive = btn.Content as string == CurrentItem?.Title;
            btn.FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal;
        }
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

        // Animate slide-in from the edge.
        bool isHorizontal = side is DockSide.Left or DockSide.Right;
        double targetSize  = isHorizontal ? DefaultSideSize : DefaultTopBottomSize;

        // Stop any ongoing close animation on the container.
        if (isHorizontal)
            _panelContainer.BeginAnimation(WidthProperty, null);
        else
            _panelContainer.BeginAnimation(HeightProperty, null);

        // Start with size 0 then expand to target.
        if (isHorizontal)
            _panelContainer.Width  = 0;
        else
            _panelContainer.Height = 0;

        Visibility = Visibility.Visible;

        var showAnim = new DoubleAnimation(targetSize,
            new Duration(TimeSpan.FromMilliseconds(120)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        // Notify the host AFTER the open animation finishes AND after the WPF render pipeline
        // has committed a frame (DispatcherPriority.Render). This is the ideal moment to capture
        // a snapshot: panel is at full size and content is fully painted.
        showAnim.Completed += (_, _) =>
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render,
                (System.Action)(() => SnapshotReady?.Invoke()));

        showAnim.Freeze();

        if (isHorizontal)
            _panelContainer.BeginAnimation(WidthProperty, showAnim);
        else
            _panelContainer.BeginAnimation(HeightProperty, showAnim);
    }

    public void Close()
    {
        // Notify the host BEFORE animation starts so it can capture a snapshot
        // while the panel content is still fully rendered and visible.
        Dismissing?.Invoke();

        bool isHorizontal = _currentSide is DockSide.Left or DockSide.Right;

        var hideAnim = new DoubleAnimation(0,
            new Duration(TimeSpan.FromMilliseconds(100)));
        hideAnim.Completed += (_, _) =>
        {
            Visibility           = Visibility.Collapsed;
            _contentHost.Content = null;
            CurrentItem          = null;
            _currentGroup        = [];
            if (_tabStrip is not null)
            {
                _tabStrip.Children.Clear();
                _tabStrip.Visibility = Visibility.Collapsed;
            }
        };
        hideAnim.Freeze();

        if (isHorizontal)
            _panelContainer.BeginAnimation(WidthProperty, hideAnim);
        else
            _panelContainer.BeginAnimation(HeightProperty, hideAnim);
    }

    // -- Resize -------------------------------------------------------------------

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
