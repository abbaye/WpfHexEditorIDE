//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using WpfHexEditor.Docking.Core;
using WpfHexEditor.Docking.Core.Nodes;

namespace WpfHexEditor.Docking.Wpf;

/// <summary>
/// Main WPF control for the docking system.
/// Renders the <see cref="DockLayoutRoot"/> tree as WPF visual elements,
/// and integrates drag &amp; drop, floating windows, and auto-hide bars.
/// </summary>
public class DockControl : ContentControl
{
    private DockEngine? _engine;
    private DockDragManager? _dragManager;
    private FloatingWindowManager? _floatingManager;

    private readonly DockPanel _rootPanel;
    private readonly AutoHideBar _autoHideLeft;
    private readonly AutoHideBar _autoHideRight;
    private readonly AutoHideBar _autoHideTop;
    private readonly AutoHideBar _autoHideBottom;
    private readonly AutoHidePopup _autoHidePopup;
    private readonly ContentControl _centerHost;

    public static readonly DependencyProperty LayoutProperty =
        DependencyProperty.Register(
            nameof(Layout),
            typeof(DockLayoutRoot),
            typeof(DockControl),
            new PropertyMetadata(null, OnLayoutChanged));

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

    /// <summary>
    /// Factory to create content for a DockItem. If not set, a default placeholder is shown.
    /// </summary>
    public Func<DockItem, object>? ContentFactory { get; set; }

    /// <summary>
    /// Raised when a tab close is requested.
    /// </summary>
    public event Action<DockItem>? TabCloseRequested;

    public DockControl()
    {
        _autoHideLeft = new AutoHideBar(Dock.Left);
        _autoHideRight = new AutoHideBar(Dock.Right);
        _autoHideTop = new AutoHideBar(Dock.Top);
        _autoHideBottom = new AutoHideBar(Dock.Bottom);
        _autoHidePopup = new AutoHidePopup();
        _centerHost = new ContentControl();

        _autoHideLeft.ItemClicked += OnAutoHideItemClicked;
        _autoHideRight.ItemClicked += OnAutoHideItemClicked;
        _autoHideTop.ItemClicked += OnAutoHideItemClicked;
        _autoHideBottom.ItemClicked += OnAutoHideItemClicked;

        // Build the root structure: auto-hide bars on edges, content in center
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

        Content = _rootPanel;
    }

    private static void OnLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DockControl control)
        {
            // Unsubscribe from old engine
            if (control._engine is not null)
                control._engine.LayoutChanged -= control.OnLayoutTreeChanged;

            if (e.NewValue is DockLayoutRoot newLayout)
            {
                control._engine = new DockEngine(newLayout);
                control._engine.LayoutChanged += control.OnLayoutTreeChanged;

                // Wire engine events for float and dock
                control._engine.ItemFloated += control.OnItemFloated;
                control._engine.ItemDocked += control.OnItemDocked;
                control._engine.ItemClosed += control.OnItemClosed;

                control._dragManager = new DockDragManager(control);
                control._floatingManager = new FloatingWindowManager(control);

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

    private void OnLayoutTreeChanged()
    {
        Dispatcher.Invoke(RebuildVisualTree);
    }

    /// <summary>
    /// Rebuilds the entire visual tree from the current Layout.
    /// </summary>
    public void RebuildVisualTree()
    {
        if (Layout is null)
        {
            _centerHost.Content = null;
            return;
        }

        _centerHost.Content = CreateVisualForNode(Layout.RootNode);
        UpdateAutoHideBars();
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

    private DocumentTabHost CreateDocumentHost(DocumentHostNode docHost)
    {
        var host = new DocumentTabHost();

        if (docHost.IsEmpty)
        {
            host.ShowEmptyPlaceholder();
        }
        else
        {
            host.Bind(docHost, ContentFactory);
        }

        WireTabControlEvents(host);
        return host;
    }

    private DockTabControl CreateTabControl(DockGroupNode group)
    {
        var tabControl = new DockTabControl();
        tabControl.Bind(group, ContentFactory);
        WireTabControlEvents(tabControl);
        return tabControl;
    }

    /// <summary>
    /// Wires all events and drag-drop handlers on a tab control.
    /// </summary>
    private void WireTabControlEvents(DockTabControl tabControl)
    {
        // Tab events
        tabControl.TabCloseRequested += OnTabCloseRequested;

        tabControl.TabDragStarted += item =>
        {
            _dragManager?.BeginDrag(item);
        };

        tabControl.TabFloatRequested += item =>
        {
            if (_engine is null) return;
            _engine.Float(item);
            RebuildVisualTree();
        };

        tabControl.TabAutoHideRequested += item =>
        {
            if (_engine is null) return;
            _engine.AutoHide(item);
            RebuildVisualTree();
        };

        // Drag-drop handlers for receiving drops
        tabControl.AllowDrop = true;

        tabControl.DragEnter += (sender, e) =>
        {
            if (sender is DockTabControl target)
                _dragManager?.OnDragEnter(target, e);
        };

        tabControl.DragOver += (sender, e) =>
        {
            if (sender is DockTabControl target)
                _dragManager?.OnDragOver(target, e);
        };

        tabControl.DragLeave += (sender, e) =>
        {
            if (sender is DockTabControl target)
                _dragManager?.OnDragLeave(target, e);
        };

        tabControl.Drop += (sender, e) =>
        {
            if (sender is DockTabControl target)
                _dragManager?.OnDrop(target, e);
        };
    }

    private void OnTabCloseRequested(DockItem item)
    {
        TabCloseRequested?.Invoke(item);
    }

    private void OnItemFloated(DockItem item)
    {
        // Create a floating window for the item
        var mousePos = System.Windows.Input.Mouse.GetPosition(this);
        var screenPos = PointToScreen(mousePos);
        _floatingManager?.CreateFloatingWindow(item, new Point(screenPos.X - 50, screenPos.Y - 20));
    }

    private void OnItemDocked(DockItem item)
    {
        // Close the floating window if the item was re-docked
        _floatingManager?.CloseWindowForItem(item);
    }

    private void OnItemClosed(DockItem item)
    {
        // Close the floating window if the item was closed
        _floatingManager?.CloseWindowForItem(item);
    }

    /// <summary>
    /// Updates the auto-hide bars with current auto-hide items, distributed by LastDockSide.
    /// </summary>
    private void UpdateAutoHideBars()
    {
        if (Layout is null) return;

        _autoHideLeft.UpdateItems(Layout.AutoHideItems.Where(i => i.LastDockSide == Core.DockSide.Left));
        _autoHideRight.UpdateItems(Layout.AutoHideItems.Where(i => i.LastDockSide == Core.DockSide.Right));
        _autoHideTop.UpdateItems(Layout.AutoHideItems.Where(i => i.LastDockSide == Core.DockSide.Top));
        _autoHideBottom.UpdateItems(Layout.AutoHideItems.Where(i => i.LastDockSide == Core.DockSide.Bottom));
    }

    private void OnAutoHideItemClicked(DockItem item)
    {
        if (_autoHidePopup.IsOpen && _autoHidePopup.CurrentItem == item)
        {
            // Toggle off
            _autoHidePopup.IsOpen = false;
            return;
        }

        // Find the button and its bar to determine placement
        UIElement? placementTarget = FindAutoHideButton(item);
        if (placementTarget is null) return;

        _autoHidePopup.ShowForItem(item, placementTarget, ContentFactory, item.LastDockSide);
    }

    private UIElement? FindAutoHideButton(DockItem item)
    {
        foreach (AutoHideBar bar in new[] { _autoHideLeft, _autoHideRight, _autoHideTop, _autoHideBottom })
        {
            foreach (UIElement child in bar.Children)
            {
                if (child is Button button && button.Tag == item)
                    return button;
            }
        }
        return null;
    }
}
