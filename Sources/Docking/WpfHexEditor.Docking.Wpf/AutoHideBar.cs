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
/// Uses a Border wrapper for a 1px content-facing separator (VS2022 style).
/// </summary>
public class AutoHideBar : Border
{
    private readonly StackPanel _itemsPanel;

    public Dock Position { get; }

    /// <summary>
    /// Exposes the items panel children for external iteration
    /// (e.g., <see cref="Controls.AutoHideBarHoverPreview"/>).
    /// </summary>
    public UIElementCollection ItemChildren => _itemsPanel.Children;

    /// <summary>
    /// Raised when a panel button (or group button) is clicked to show/toggle its flyout.
    /// The list contains one item for a solo panel, or all group members for a grouped entry.
    /// </summary>
    public event Action<IReadOnlyList<DockItem>>? GroupClicked;

    /// <summary>
    /// Raised when the user chooses "Float" from the bar button context menu.
    /// </summary>
    public event Action<IReadOnlyList<DockItem>>? GroupFloatRequested;

    /// <summary>
    /// Raised when the user chooses "Close" from the bar button context menu.
    /// </summary>
    public event Action<IReadOnlyList<DockItem>>? GroupCloseRequested;

    /// <summary>
    /// Raised after <see cref="UpdateItems"/> rebuilds the button list,
    /// so attached hover-preview helpers can re-wire mouse events.
    /// </summary>
    public event Action? ItemsUpdated;

    /// <summary>
    /// Holds references to the indicator border and label text block for each tab button.
    /// </summary>
    private sealed record AutoHideTabVisuals(Border Indicator, TextBlock Label);

    /// <summary>Tracks which wrapper is currently the active (flyout-open) tab, if any.</summary>
    private Grid? _activeWrapper;

    public AutoHideBar(Dock position)
    {
        Position = position;
        _itemsPanel = new StackPanel
        {
            Orientation = position is Dock.Top or Dock.Bottom
                ? Orientation.Horizontal
                : Orientation.Vertical
        };
        Child = _itemsPanel;

        SetResourceReference(BackgroundProperty, "DockMenuBackgroundBrush");
        SetResourceReference(BorderBrushProperty, "DockBorderBrush");

        // 1px separator on the outer edge (same side as dock position)
        BorderThickness = position switch
        {
            Dock.Left   => new Thickness(1, 0, 0, 0),
            Dock.Right  => new Thickness(0, 0, 1, 0),
            Dock.Top    => new Thickness(0, 1, 0, 0),
            Dock.Bottom => new Thickness(0, 0, 0, 1),
            _           => new Thickness(0)
        };
    }

    protected override AutomationPeer OnCreateAutomationPeer() =>
        new AutoHideBarAutomationPeer(this);

    /// <summary>
    /// Updates the bar with the given auto-hide items.
    /// Each item gets its own independent button (VS-like behavior), even when
    /// items share an <see cref="DockItem.AutoHideGroupId"/> for virtual group restore.
    /// </summary>
    public void UpdateItems(IEnumerable<DockItem> items)
    {
        _itemsPanel.Children.Clear();
        bool isVertical = Position is Dock.Left or Dock.Right;

        foreach (var item in items)
        {
            var textBlock = new TextBlock
            {
                Text = item.Title,
                LayoutTransform = isVertical
                    ? new RotateTransform(Position == Dock.Left ? -90 : 90)
                    : Transform.Identity
            };
            textBlock.SetResourceReference(TextBlock.ForegroundProperty, "DockTabTextBrush");

            var button = new Button
            {
                Content = textBlock,
                Padding = new Thickness(8, 6, 8, 6),
                Margin  = new Thickness(0),
                Tag     = new List<DockItem> { item }
            };

            // Apply themed style so IsMouseOver uses DockTabHoverBrush instead of default WPF chrome.
            button.SetResourceReference(FrameworkElement.StyleProperty, "DockTitleButtonStyle");
            AutomationProperties.SetName(button, $"Show {item.Title}");
            var captured = (IReadOnlyList<DockItem>)new List<DockItem> { item };
            button.Click += (_, _) => GroupClicked?.Invoke(captured);

            // VS-like right-click context menu: Show / Float / — / Close
            static TextBlock MakeIcon(string glyph) => new()
            {
                Text       = glyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize   = 12,
                Width      = 16,
                TextAlignment = TextAlignment.Center
            };

            var showItem  = new MenuItem { Header = "Show",  Icon = MakeIcon("\uE8BD") };
            var floatItem = new MenuItem { Header = "Float", Icon = MakeIcon("\uE78B") };
            var closeItem = new MenuItem { Header = "Close", Icon = MakeIcon("\uE8BB") };
            showItem.Click  += (_, _) => GroupClicked?.Invoke(captured);
            floatItem.Click += (_, _) => GroupFloatRequested?.Invoke(captured);
            closeItem.Click += (_, _) => GroupCloseRequested?.Invoke(captured);

            var ctxMenu = new ContextMenu();
            ctxMenu.Items.Add(showItem);
            ctxMenu.Items.Add(floatItem);
            ctxMenu.Items.Add(new Separator());
            ctxMenu.Items.Add(closeItem);
            ctxMenu.SetResourceReference(ContextMenu.BackgroundProperty,  "DockMenuBackgroundBrush");
            ctxMenu.SetResourceReference(ContextMenu.BorderBrushProperty, "DockMenuBorderBrush");
            foreach (MenuItem mi in ctxMenu.Items.OfType<MenuItem>())
                mi.SetResourceReference(MenuItem.ForegroundProperty, "DockMenuForegroundBrush");
            button.ContextMenu = ctxMenu;

            // VS2022 accent indicator strip (3px) on the content-facing edge,
            // placed OUTSIDE the button so it isn't affected by text rotation.
            // Always visible — active tab gets brighter accent color via SetActiveGroup.
            var indicator = new Border { Opacity = 0 };
            indicator.SetResourceReference(Border.BackgroundProperty, "DockAutoHideIndicatorBrush");

            var wrapper = new Grid();

            if (isVertical)
            {
                // Vertical bar: indicator is a 3px-wide strip on the outer edge
                indicator.Width = 3;
                indicator.HorizontalAlignment = Position == Dock.Left
                    ? HorizontalAlignment.Left
                    : HorizontalAlignment.Right;
                indicator.VerticalAlignment = VerticalAlignment.Stretch;
                indicator.Margin = new Thickness(0, 2, 0, 2);
            }
            else
            {
                // Horizontal bar: indicator is a 3px-tall strip on the outer edge
                indicator.Height = 3;
                indicator.VerticalAlignment = Position == Dock.Top
                    ? VerticalAlignment.Top
                    : VerticalAlignment.Bottom;
                indicator.HorizontalAlignment = HorizontalAlignment.Stretch;
                indicator.Margin = new Thickness(2, 0, 2, 0);
            }

            wrapper.Children.Add(button);
            wrapper.Children.Add(indicator);
            wrapper.Tag = new AutoHideTabVisuals(indicator, textBlock);

            // Hover: brighten indicator + text on mouse over (unless already active)
            wrapper.MouseEnter += (s, _) =>
            {
                if (s is not Grid w || w == _activeWrapper) return;
                if (w.Tag is not AutoHideTabVisuals v) return;
                v.Indicator.Opacity = 1.0;
                v.Label.SetResourceReference(TextBlock.ForegroundProperty, "DockTabActiveTextBrush");
            };
            wrapper.MouseLeave += (s, _) =>
            {
                if (s is not Grid w || w == _activeWrapper) return;
                if (w.Tag is not AutoHideTabVisuals v) return;
                v.Indicator.Opacity = 0;
                v.Label.SetResourceReference(TextBlock.ForegroundProperty, "DockTabTextBrush");
            };

            _itemsPanel.Children.Add(wrapper);
        }

        Visibility = _itemsPanel.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ItemsUpdated?.Invoke();
    }

    /// <summary>
    /// Highlights the bar button whose group contains any of the given items.
    /// Active tab gets bright accent indicator + white text (VS2022 style).
    /// Inactive tabs keep the indicator visible at a subdued opacity.
    /// Pass <c>null</c> to clear all highlights.
    /// </summary>
    public void SetActiveGroup(IReadOnlyList<DockItem>? items)
    {
        _activeWrapper = null;

        foreach (UIElement child in _itemsPanel.Children)
        {
            if (child is not Grid wrapper) continue;
            if (wrapper.Tag is not AutoHideTabVisuals visuals) continue;

            // Find the button inside the wrapper to check its Tag
            var btn = wrapper.Children.OfType<Button>().FirstOrDefault();
            if (btn is null) continue;

            var isActive = items is not null
                && btn.Tag is IReadOnlyList<DockItem> group
                && group.Any(i => items.Contains(i));

            if (isActive)
                _activeWrapper = wrapper;

            // Indicator: active = full opacity, inactive = hidden
            visuals.Indicator.Opacity = isActive ? 1.0 : 0;

            // Text color
            visuals.Label.SetResourceReference(TextBlock.ForegroundProperty,
                isActive ? "DockTabActiveTextBrush" : "DockTabTextBrush");
        }
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

    /// <summary>
    /// Optional configurable timing. When null, uses built-in defaults.
    /// </summary>
    public AutoHideSettings? Settings { get; set; }

    private int OpenAnimMs => Settings?.SlideAnimationMs ?? 120;
    private int CloseAnimMs => Settings?.SlideAnimationMs > 0 ? Math.Max(Settings.SlideAnimationMs / 2, 60) : 100;

    private IReadOnlyList<DockItem> _currentGroup = [];
    private StackPanel? _tabStrip;
    private Func<DockItem, object>? _contentFactory;
    private bool _isOpen;

    public DockItem? CurrentItem { get; private set; }

    /// <summary>
    /// All items currently shown in the flyout (one item for solo panels, multiple for grouped).
    /// </summary>
    public IReadOnlyList<DockItem> CurrentGroup => _currentGroup;

    public bool IsOpen => _isOpen;

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
    public void ShowForItems(IReadOnlyList<DockItem> items, Func<DockItem, object>? contentFactory = null, DockSide side = DockSide.Bottom, Thickness contentInsets = default)
    {
        if (items.Count == 0) return;
        _currentGroup   = items;
        _contentFactory = contentFactory;
        ShowForItem(items[0], contentFactory, side, contentInsets);

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
    public void ShowForItem(DockItem item, Func<DockItem, object>? contentFactory = null, DockSide side = DockSide.Bottom, Thickness contentInsets = default)
    {
        // Cancel BOTH axis animations before touching Width/Height.
        // If an animation holds NaN as the local value, BeginAnimation(prop, null) reverts to
        // that NaN → the next animation's origin becomes NaN → InvalidOperationException.
        _panelContainer.BeginAnimation(WidthProperty,  null);
        _panelContainer.BeginAnimation(HeightProperty, null);

        CurrentItem  = item;
        _currentSide = side;
        _titleBlock.Text = item.Title;

        // Apply bar-inset margins so the flyout doesn't overlap sibling auto-hide bars.
        _panelContainer.Margin = contentInsets;
        _clickCatcher.Margin   = contentInsets;

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
            _panelContainer.Width  = 0;          // animation starts from 0; NaN was cancelled above
            _panelContainer.Height = double.NaN; // safe: HeightProperty animation already cancelled

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
            _panelContainer.Height = 0;          // animation starts from 0; NaN was cancelled above
            _panelContainer.Width  = double.NaN; // safe: WidthProperty animation already cancelled

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

        bool isHorizontal = side is DockSide.Left or DockSide.Right;
        double targetSize  = isHorizontal ? DefaultSideSize : DefaultTopBottomSize;

        _isOpen = true;
        Visibility = Visibility.Visible;

        var showAnim = new DoubleAnimation(0, targetSize,
            new Duration(TimeSpan.FromMilliseconds(OpenAnimMs)))
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
        _isOpen = false;

        // Notify the host BEFORE animation starts so it can capture a snapshot
        // while the panel content is still fully rendered and visible.
        Dismissing?.Invoke();

        bool isHorizontal = _currentSide is DockSide.Left or DockSide.Right;

        var hideAnim = new DoubleAnimation(0,
            new Duration(TimeSpan.FromMilliseconds(CloseAnimMs)))
        {
            EasingFunction = DockAnimationHelper.GetFlyoutEase()
        };
        hideAnim.Completed += (_, _) =>
        {
            if (_isOpen) return;  // ShowForItem was called before hide animation completed
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
        // Release the open-animation hold so manual Width/Height assignments in OnResizeMove
        // are not silently overridden by WPF's animation clock.
        if (_currentSide is DockSide.Left or DockSide.Right)
            _panelContainer.BeginAnimation(WidthProperty, null);
        else
            _panelContainer.BeginAnimation(HeightProperty, null);

        _resizing        = true;
        _resizeStart     = e.GetPosition(this);
        _resizeStartSize = _currentSide is DockSide.Left or DockSide.Right
            ? _panelContainer.ActualWidth
            : _panelContainer.ActualHeight;
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
