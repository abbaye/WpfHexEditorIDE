// ==========================================================
// Project: WpfHexEditor.Shell
// File: FloatingWindow.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     A WPF Window hosting a dock group (one or more tabbed items) in a floating
//     state. Implements a custom dark title bar with chevron dropdown, pin, and
//     close buttons in Visual Studio style.
//
// Architecture Notes:
//     WindowChrome is used to replace the native title bar while keeping native
//     resize/move. Drag detection raises TitleBarDragStarted for DockDragManager
//     to take over and show the overlay compass. ReDockRequested supports
//     re-integrating the floating window back into the dock area.
//
// ==========================================================

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using WpfHexEditor.Docking.Core;
using WpfHexEditor.Docking.Core.Nodes;
using Core = WpfHexEditor.Docking.Core;

namespace WpfHexEditor.Shell;

/// <summary>
/// A floating window that contains a dock group (one or more tabbed items).
/// Custom dark title bar with chevron dropdown, pin, and close buttons (VS-style).
/// </summary>
public class FloatingWindow : Window
{
    private readonly DockTabControl _tabControl;
    private readonly TextBlock _titleBlock;
    private readonly ContentPresenter _titleIcon;

    public DockGroupNode? Node { get; private set; }

    /// <summary>
    /// The main DockItem this floating window was created for.
    /// </summary>
    public DockItem? Item { get; private set; }

    public event Action<DockItem>? TabCloseRequested;
    public event Action<DockItem>? TabDragStarted;
    public event Action<DockItem>? TabFloatRequested;
    public event Action<DockItem>? TabAutoHideRequested;

    /// <summary>
    /// Raised when the user clicks pin or "Dock" in the context menu to re-dock.
    /// </summary>
    public event Action<DockItem>? ReDockRequested;

    /// <summary>
    /// Raised when the user starts dragging the title bar. The DockDragManager
    /// will handle the drag-to-dock overlay logic.
    /// </summary>
    public event Action<DockItem>? WindowDragStarted;

    public FloatingWindow()
    {
        WindowStyle = WindowStyle.None;
        ShowInTaskbar = false;
        Width = 400;
        Height = 300;
        ResizeMode = ResizeMode.CanResize;
        SetResourceReference(BackgroundProperty, "DockBackgroundBrush");

        // Remove the white DWM caption area that Windows adds even for WindowStyle.None.
        // CaptionHeight=0 keeps the client area full-height; ResizeBorderThickness keeps
        // all-edge resizing. Windows 11 DWM then handles drop shadow, rounded corners,
        // and the accent-color active border natively (VS2022-style look).
        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight           = 0,
            ResizeBorderThickness   = new Thickness(4),
            GlassFrameThickness     = new Thickness(0),
            UseAeroCaptionButtons   = false
        });

        // --- Title bar ---
        _titleBlock = new TextBlock
        {
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            Margin = new Thickness(8, 6, 8, 6),
            VerticalAlignment = VerticalAlignment.Center
        };
        _titleBlock.SetResourceReference(TextBlock.ForegroundProperty, "DockTabActiveTextBrush");

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
            if (Item is not null) TabCloseRequested?.Invoke(Item);
        };

        // Pin button (Pin MDL2 — dock)
        var pinButton = MakeTitleButton("\uE141", "Dock");
        pinButton.Click += (_, _) =>
        {
            if (Item is not null) ReDockRequested?.Invoke(Item);
        };

        // Chevron dropdown button (ChevronDown MDL2)
        var chevronButton = MakeTitleButton("\uE70D", "Options");
        chevronButton.Click += (sender, _) =>
        {
            if (Item is null || sender is not Button btn) return;
            var item = Item;

            var menuBg = TryFindResource("DockMenuBackgroundBrush") as Brush;
            var menuFg = TryFindResource("DockMenuForegroundBrush") as Brush;
            var menuBorder = TryFindResource("DockMenuBorderBrush") as Brush;

            var menu = new ContextMenu
            {
                Background = menuBg ?? Brushes.DarkGray,
                BorderBrush = menuBorder ?? Brushes.Gray,
                Foreground = menuFg ?? Brushes.White
            };

            var dockMenuItem = new MenuItem { Header = "Dock", Foreground = menuFg };
            dockMenuItem.Click += (_, _) => ReDockRequested?.Invoke(item);
            menu.Items.Add(dockMenuItem);

            var autoHideMenuItem = new MenuItem { Header = "Auto Hide", Foreground = menuFg };
            autoHideMenuItem.Click += (_, _) => TabAutoHideRequested?.Invoke(item);
            menu.Items.Add(autoHideMenuItem);

            var floatMenuItem = new MenuItem
            {
                Header = "Float",
                Foreground = Brushes.Gray,
                IsEnabled = false
            };
            menu.Items.Add(floatMenuItem);

            menu.Items.Add(new Separator());

            var closeMenuItem = new MenuItem { Header = "Close", Foreground = menuFg };
            closeMenuItem.Click += (_, _) => TabCloseRequested?.Invoke(item);
            menu.Items.Add(closeMenuItem);

            menu.PlacementTarget = btn;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        };

        // Icon before title (updated in Bind)
        _titleIcon = new ContentPresenter
        {
            Width = 16, Height = 16,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed
        };

        // Title bar layout
        var titleContent = new DockPanel();
        DockPanel.SetDock(closeButton, Dock.Right);
        DockPanel.SetDock(pinButton, Dock.Right);
        DockPanel.SetDock(chevronButton, Dock.Right);
        titleContent.Children.Add(closeButton);
        titleContent.Children.Add(pinButton);
        titleContent.Children.Add(chevronButton);
        titleContent.Children.Add(_titleIcon);
        titleContent.Children.Add(_titleBlock);

        var titleBar = new Border { Child = titleContent };
        titleBar.SetResourceReference(Border.BackgroundProperty, "DockMenuBackgroundBrush");

        // Title bar drag: raise WindowDragStarted instead of DragMove
        titleBar.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2)
            {
                if (Item is not null) ReDockRequested?.Invoke(Item);
            }
            else
            {
                if (Item is not null) WindowDragStarted?.Invoke(Item);
            }
        };

        // --- Tab control ---
        _tabControl = new DockTabControl();
        _tabControl.TabCloseRequested += item => TabCloseRequested?.Invoke(item);
        _tabControl.TabDragStarted += item => TabDragStarted?.Invoke(item);
        _tabControl.TabFloatRequested += item => TabFloatRequested?.Invoke(item);
        _tabControl.TabAutoHideRequested += item => TabAutoHideRequested?.Invoke(item);

        // --- Assemble layout ---
        var innerPanel = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(titleBar, Dock.Top);
        innerPanel.Children.Add(titleBar);
        innerPanel.Children.Add(_tabControl);

        var outerBorder = new Border
        {
            BorderThickness = new Thickness(1),
            Child = innerPanel
        };
        outerBorder.SetResourceReference(Border.BackgroundProperty, "DockBackgroundBrush");
        outerBorder.SetResourceReference(Border.BorderBrushProperty, "DockBorderBrush");

        // Propagate theme foreground dynamically to all text content inside the floating window
        outerBorder.SetResourceReference(TextElement.ForegroundProperty, "DockMenuForegroundBrush");

        // Active/inactive border accent (consistent on Windows 10 + 11)
        Activated += (_, _) =>
            outerBorder.SetResourceReference(Border.BorderBrushProperty, "DockTabActiveBrush");
        Deactivated += (_, _) =>
            outerBorder.SetResourceReference(Border.BorderBrushProperty, "DockBorderBrush");

        Content = outerBorder;
    }

    public void Bind(DockGroupNode node, DockItem item, Func<DockItem, object>? contentFactory = null)
    {
        Node = node;
        Item = item;

        // Floating windows always use bottom tab placement: the custom title bar
        // already occupies the top, so tabs at the bottom is the consistent choice
        // regardless of which side the panel was originally docked to.
        _tabControl.TabStripPlacement = Dock.Bottom;

        _tabControl.Bind(node, contentFactory);

        // Hide the tab strip completely when single item (title bar already shows the name)
        if (node.Items.Count() <= 1)
        {
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.ContentSourceProperty, "SelectedContent");
            _tabControl.Template = new ControlTemplate(typeof(TabControl)) { VisualTree = cp };
        }

        if (node.ActiveItem is not null)
        {
            Title = node.ActiveItem.Title;
            _titleBlock.Text = node.ActiveItem.Title;
            UpdateIcon(node.ActiveItem);

            // Keep title bar in sync when dockItem.Title changes (e.g. "file *" dirty flag)
            node.ActiveItem.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(DockItem.Title))
                {
                    Title = node.ActiveItem.Title;
                    _titleBlock.Text = node.ActiveItem.Title;
                }
            };
        }
    }

    private void UpdateIcon(DockItem? item)
    {
        if (item?.Icon is null)
        {
            _titleIcon.Visibility = Visibility.Collapsed;
            _titleIcon.Content = null;
            return;
        }
        _titleIcon.Content = item.Icon is ImageSource img
            ? new Image { Source = img, Width = 16, Height = 16, Stretch = Stretch.Uniform }
            : item.Icon;
        _titleIcon.Visibility = Visibility.Visible;
    }
}

/// <summary>
/// Manages creation and lifecycle of floating windows.
/// </summary>
public class FloatingWindowManager
{
    private readonly DockControl _dockControl;
    private readonly List<FloatingWindow> _windows = [];

    public IReadOnlyList<FloatingWindow> Windows => _windows;

    public FloatingWindowManager(DockControl dockControl)
    {
        _dockControl = dockControl;
    }

    /// <summary>
    /// Creates a floating window for the given item.
    /// Position priority: saved <see cref="DockItem.FloatLeft"/>/<see cref="DockItem.FloatTop"/>,
    /// then explicit <paramref name="position"/>, then centered relative to the owner window.
    /// </summary>
    public FloatingWindow CreateFloatingWindow(DockItem item, Point? position = null)
    {
        var group = new DockGroupNode();
        bool wasDocument = item.IsDocument; // Preserve: AddItem always syncs IsDocument to host type,
                                            // but a floating DockGroupNode is not DocumentHostNode.
                                            // IsDocument must survive the float so the drag manager
                                            // routes drops to the document zone, not a panel.
        group.AddItem(item);
        item.IsDocument = wasDocument;

        var window = ConfigureAndShow(group, item, position, trackPosition: true);

        // Single-item specific: tab drag re-docks to center and closes the window
        window.TabDragStarted += i =>
        {
            if (_dockControl.Engine is not null)
            {
                _dockControl.Engine.Dock(i, _dockControl.Layout!.MainDocumentHost, DockDirection.Center);
                _dockControl.RebuildVisualTree();
                // OnItemDocked already closed the window via CloseWindowForItem — guard against double-close.
                if (window.IsLoaded) window.Close();
            }
        };

        return window;
    }

    /// <summary>
    /// Creates a floating window for an existing group (e.g. from a group-drag).
    /// Unlike <see cref="CreateFloatingWindow"/>, uses the provided group directly.
    /// </summary>
    public FloatingWindow CreateFloatingWindowForGroup(DockGroupNode group, Point? position = null)
    {
        var item = group.ActiveItem ?? group.Items.FirstOrDefault();
        if (item is null) return null!;

        return ConfigureAndShow(group, item, position, trackPosition: false);
    }

    /// <summary>
    /// Shared setup for both <see cref="CreateFloatingWindow"/> and <see cref="CreateFloatingWindowForGroup"/>.
    /// Creates, positions, wires events, and shows the floating window.
    /// </summary>
    private FloatingWindow ConfigureAndShow(DockGroupNode group, DockItem activeItem, Point? position, bool trackPosition)
    {
        var window = new FloatingWindow();
        window.Bind(group, activeItem, _dockControl.CachedContentFactory);

        // Apply saved size if available (always, not just for trackPosition —
        // group drags also need the captured docked panel size)
        if (activeItem.FloatWidth.HasValue)  window.Width  = activeItem.FloatWidth.Value;
        if (activeItem.FloatHeight.HasValue) window.Height = activeItem.FloatHeight.Value;

        // Position: saved location > explicit position > centered relative to owner
        if (trackPosition && activeItem.FloatLeft.HasValue && activeItem.FloatTop.HasValue)
        {
            window.Left = activeItem.FloatLeft.Value;
            window.Top  = activeItem.FloatTop.Value;
        }
        else if (position.HasValue)
        {
            window.Left = position.Value.X;
            window.Top  = position.Value.Y;
        }
        else
        {
            var owner = Window.GetWindow(_dockControl);
            if (owner is not null)
            {
                window.Left = owner.Left + (owner.Width  - window.Width)  / 2;
                window.Top  = owner.Top  + (owner.Height - window.Height) / 2;
            }
        }

        ClampToVirtualScreen(window);

        // Throttle position persistence (only for individually floated items)
        DispatcherTimer? positionTimer = null;
        if (trackPosition)
        {
            positionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            positionTimer.Tick += (_, _) =>
            {
                positionTimer.Stop();
                activeItem.FloatLeft   = window.Left;
                activeItem.FloatTop    = window.Top;
                activeItem.FloatWidth  = window.Width;
                activeItem.FloatHeight = window.Height;
            };
            window.LocationChanged += (_, _) =>
            {
                positionTimer.Stop();
                positionTimer.Start();
            };
            window.SizeChanged += (_, _) =>
            {
                positionTimer.Stop();
                positionTimer.Start();
            };
        }

        window.Closed += (_, _) =>
        {
            positionTimer?.Stop();
            _windows.Remove(window);
            Window.GetWindow(_dockControl)?.Activate();
        };

        window.TabCloseRequested += i =>
        {
            // Route through RaiseTabCloseRequested so the host (MainWindow) can run its cleanup
            // (cache eviction, ParsedFields disconnect, dirty-close prompt, etc.).
            // RaiseTabCloseRequested → TabCloseRequested → host.OnTabCloseRequested
            // → engine.Close → ItemClosed → OnItemClosed → CloseWindowForItem → window.Close().
            _dockControl.RaiseTabCloseRequested(i);

            // Multi-item group fallback: if CloseWindowForItem didn't close this window
            // (because window.Item differs from i), close it now when the node is empty.
            if (window.IsLoaded && window.Node?.IsEmpty == true)
                window.Close();
        };

        window.ReDockRequested += i =>
        {
            if (_dockControl.Engine is null) return;
            var dir = i.LastDockSide switch
            {
                Core.DockSide.Left   => DockDirection.Left,
                Core.DockSide.Right  => DockDirection.Right,
                Core.DockSide.Top    => DockDirection.Top,
                Core.DockSide.Bottom => DockDirection.Bottom,
                _                    => DockDirection.Center
            };
            _dockControl.Engine.Dock(i, _dockControl.Layout!.MainDocumentHost, dir);
            _dockControl.RebuildVisualTree();
            // OnItemDocked already closed the window via CloseWindowForItem — guard against double-close.
            if (window.IsLoaded && window.Node?.IsEmpty == true) window.Close();
        };

        window.WindowDragStarted += i =>
        {
            _dockControl.DragManager?.BeginFloatingDrag(i, window);
        };

        window.TabAutoHideRequested += i =>
        {
            _dockControl.Engine?.AutoHide(i);
            _dockControl.RebuildVisualTree();
            // OnItemHidden already closed the window via CloseWindowForItem — guard against double-close.
            if (window.IsLoaded && window.Node?.IsEmpty == true) window.Close();
        };

        _windows.Add(window);
        window.Owner = Window.GetWindow(_dockControl);
        window.Show();

        return window;
    }

    /// <summary>
    /// Ensures at least <paramref name="minVisible"/> DIPs of the window remain inside the
    /// virtual screen bounds (all monitors combined). Prevents windows from being lost
    /// off-screen (e.g. after a monitor is disconnected).
    /// </summary>
    private static void ClampToVirtualScreen(Window window, double minVisible = 100)
    {
        var left = window.Left;
        var top  = window.Top;

        var maxLeft = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth  - minVisible;
        var maxTop  = SystemParameters.VirtualScreenTop  + SystemParameters.VirtualScreenHeight - minVisible;
        var minLeft = SystemParameters.VirtualScreenLeft - window.Width + minVisible;
        var minTop  = SystemParameters.VirtualScreenTop;

        window.Left = Math.Max(minLeft, Math.Min(left, maxLeft));
        window.Top  = Math.Max(minTop,  Math.Min(top,  maxTop));
    }

    /// <summary>
    /// Finds a floating window containing the given item.
    /// Checks both the primary <see cref="FloatingWindow.Item"/> and all items inside
    /// <see cref="FloatingWindow.Node"/>, so non-active items in a multi-tab group are found.
    /// </summary>
    public FloatingWindow? FindWindowForItem(DockItem item)
    {
        return _windows.FirstOrDefault(w =>
            w.Item == item ||
            (w.Node is not null && w.Node.Items.Contains(item)));
    }

    /// <summary>
    /// Closes the floating window for the given item if it exists.
    /// </summary>
    public void CloseWindowForItem(DockItem item)
    {
        var window = FindWindowForItem(item);
        window?.Close();
    }

    /// <summary>
    /// Closes all floating windows.
    /// </summary>
    public void CloseAll()
    {
        foreach (var window in _windows.ToList())
            window.Close();
    }
}

/// <summary>
/// Semi-transparent preview window shown during a drag operation, following the cursor.
/// </summary>
public class DragPreviewWindow : Window
{
    private readonly TextBlock _titleBlock;

    public DragPreviewWindow(string title)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        ShowInTaskbar = false;
        Topmost = true;
        IsHitTestVisible = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        Background = Brushes.Transparent;

        _titleBlock = new TextBlock
        {
            Text = title,
            FontSize = 12,
            Padding = new Thickness(10, 5, 10, 5)
        };
        _titleBlock.SetResourceReference(TextBlock.ForegroundProperty, "DockMenuForegroundBrush");

        var border = new Border
        {
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(3),
            Child = _titleBlock
        };
        border.SetResourceReference(Border.BackgroundProperty, "DockMenuBackgroundBrush");
        border.SetResourceReference(Border.BorderBrushProperty, "DockTabActiveBrush");
        border.Opacity = 0.85;

        Content = border;
    }

    public void MoveTo(Point screenPoint)
    {
        Left = screenPoint.X + 12;
        Top = screenPoint.Y + 12;
    }

    /// <summary>
    /// Positions and sizes the preview to cover the given snap target zone (in DIPs).
    /// </summary>
    public void ShowSnapPreview(Rect zone)
    {
        SizeToContent = SizeToContent.Manual;
        Left   = zone.X;
        Top    = zone.Y;
        Width  = zone.Width;
        Height = zone.Height;
        _titleBlock.Visibility = Visibility.Collapsed;
        if (!IsVisible) Show();
    }

    /// <summary>
    /// Returns to cursor-following mode (compact label visible).
    /// </summary>
    public void HideSnapPreview()
    {
        SizeToContent = SizeToContent.WidthAndHeight;
        _titleBlock.Visibility = Visibility.Visible;
    }
}
