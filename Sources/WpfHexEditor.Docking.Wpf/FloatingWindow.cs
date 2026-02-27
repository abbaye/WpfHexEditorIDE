//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using WpfHexEditor.Docking.Core;
using WpfHexEditor.Docking.Core.Nodes;

namespace WpfHexEditor.Docking.Wpf;

/// <summary>
/// A floating window that contains a dock group (one or more tabbed items).
/// Custom dark title bar with chevron dropdown, pin, and close buttons (VS-style).
/// </summary>
public class FloatingWindow : Window
{
    private readonly DockTabControl _tabControl;
    private readonly TextBlock _titleBlock;

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
                Content = content,
                FontSize = 12,
                Padding = new Thickness(4, 2, 4, 2),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = tooltip
            };
            btn.SetResourceReference(Control.ForegroundProperty, "DockTabTextBrush");
            return btn;
        }

        // Close button
        var closeButton = MakeTitleButton("\u2715", "Close");
        closeButton.Click += (_, _) =>
        {
            if (Item is not null) TabCloseRequested?.Invoke(Item);
        };

        // Pin button (dock)
        var pinButton = MakeTitleButton("\uD83D\uDCCC", "Dock");
        pinButton.Click += (_, _) =>
        {
            if (Item is not null) ReDockRequested?.Invoke(Item);
        };

        // Chevron dropdown button
        var chevronButton = MakeTitleButton("\u25BC", "Options");
        chevronButton.FontSize = 9;
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

        // Title bar layout
        var titleContent = new DockPanel();
        DockPanel.SetDock(closeButton, Dock.Right);
        DockPanel.SetDock(pinButton, Dock.Right);
        DockPanel.SetDock(chevronButton, Dock.Right);
        titleContent.Children.Add(closeButton);
        titleContent.Children.Add(pinButton);
        titleContent.Children.Add(chevronButton);
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

        Content = outerBorder;
    }

    public void Bind(DockGroupNode node, DockItem item, Func<DockItem, object>? contentFactory = null)
    {
        Node = node;
        Item = item;
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
        }
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
    /// </summary>
    public FloatingWindow CreateFloatingWindow(DockItem item, Point? position = null)
    {
        var group = new DockGroupNode();
        group.AddItem(item);

        var window = new FloatingWindow();
        window.Bind(group, item, _dockControl.ContentFactory);

        if (position.HasValue)
        {
            window.Left = position.Value.X;
            window.Top = position.Value.Y;
        }
        else
        {
            // Center relative to the main window
            var owner = Window.GetWindow(_dockControl);
            if (owner is not null)
            {
                window.Left = owner.Left + (owner.Width - window.Width) / 2;
                window.Top = owner.Top + (owner.Height - window.Height) / 2;
            }
        }

        window.Closed += (_, _) =>
        {
            _windows.Remove(window);
            // Re-activate the main window so it doesn't fall behind other apps
            Window.GetWindow(_dockControl)?.Activate();
        };

        window.TabCloseRequested += i =>
        {
            _dockControl.Engine?.Close(i);
            if (window.Node?.IsEmpty == true)
                window.Close();
        };

        window.TabDragStarted += i =>
        {
            // Re-dock: dock back to MainDocumentHost and close the floating window
            if (_dockControl.Engine is not null)
            {
                _dockControl.Engine.Dock(i, _dockControl.Layout!.MainDocumentHost, DockDirection.Center);
                _dockControl.RebuildVisualTree();
                window.Close();
            }
        };

        window.ReDockRequested += i =>
        {
            if (_dockControl.Engine is not null)
            {
                // Re-dock to the item's previous side, not always Center
                var direction = i.LastDockSide switch
                {
                    Core.DockSide.Left => DockDirection.Left,
                    Core.DockSide.Right => DockDirection.Right,
                    Core.DockSide.Top => DockDirection.Top,
                    Core.DockSide.Bottom => DockDirection.Bottom,
                    _ => DockDirection.Center
                };
                _dockControl.Engine.Dock(i, _dockControl.Layout!.MainDocumentHost, direction);
                _dockControl.RebuildVisualTree();
                window.Close();
            }
        };

        window.WindowDragStarted += i =>
        {
            _dockControl.DragManager?.BeginFloatingDrag(i, window);
        };

        window.TabAutoHideRequested += i =>
        {
            _dockControl.Engine?.AutoHide(i);
            _dockControl.RebuildVisualTree();
            if (window.Node?.IsEmpty == true)
                window.Close();
        };

        _windows.Add(window);
        window.Owner = Window.GetWindow(_dockControl);
        window.Show();

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

        var window = new FloatingWindow();
        window.Bind(group, item, _dockControl.ContentFactory);

        if (position.HasValue)
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

        window.Closed += (_, _) =>
        {
            _windows.Remove(window);
            Window.GetWindow(_dockControl)?.Activate();
        };

        window.TabCloseRequested += i =>
        {
            _dockControl.Engine?.Close(i);
            if (window.Node?.IsEmpty == true) window.Close();
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
            if (window.Node?.IsEmpty == true) window.Close();
        };

        window.WindowDragStarted += i =>
        {
            _dockControl.DragManager?.BeginFloatingDrag(i, window);
        };

        window.TabAutoHideRequested += i =>
        {
            _dockControl.Engine?.AutoHide(i);
            _dockControl.RebuildVisualTree();
            if (window.Node?.IsEmpty == true) window.Close();
        };

        _windows.Add(window);
        window.Owner = Window.GetWindow(_dockControl);
        window.Show();

        return window;
    }

    /// <summary>
    /// Finds a floating window containing the given item.
    /// </summary>
    public FloatingWindow? FindWindowForItem(DockItem item)
    {
        return _windows.FirstOrDefault(w => w.Item == item);
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
        Background = System.Windows.Media.Brushes.Transparent;

        _titleBlock = new TextBlock
        {
            Text = title,
            Foreground = System.Windows.Media.Brushes.White,
            FontSize = 12,
            Padding = new Thickness(10, 5, 10, 5)
        };

        var border = new Border
        {
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(192, 37, 37, 38)),
            BorderBrush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0, 122, 204)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(3),
            Child = _titleBlock
        };

        Content = border;
    }

    public void MoveTo(Point screenPoint)
    {
        Left = screenPoint.X + 12;
        Top = screenPoint.Y + 12;
    }
}
