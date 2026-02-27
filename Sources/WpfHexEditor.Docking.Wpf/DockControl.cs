//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
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

    private readonly Grid _rootGrid;
    private readonly DockPanel _rootPanel;
    private readonly AutoHideBar _autoHideLeft;
    private readonly AutoHideBar _autoHideRight;
    private readonly AutoHideBar _autoHideTop;
    private readonly AutoHideBar _autoHideBottom;
    private readonly AutoHideFlyout _autoHideFlyout;
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
    /// The center content host, used by DockDragManager for overlay positioning.
    /// </summary>
    internal ContentControl CenterHost => _centerHost;

    /// <summary>
    /// The drag manager, used by FloatingWindowManager for drag-to-dock operations.
    /// </summary>
    internal DockDragManager? DragManager => _dragManager;

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
        _autoHideFlyout = new AutoHideFlyout();
        _autoHideFlyout.RestoreRequested += OnAutoHideRestoreRequested;
        _autoHideFlyout.CloseRequested += OnAutoHideCloseRequested;
        _autoHideFlyout.FloatRequested += OnAutoHideFloatRequested;
        _centerHost = new ContentControl();

        _autoHideLeft.ItemClicked += OnAutoHideItemClicked;
        _autoHideRight.ItemClicked += OnAutoHideItemClicked;
        _autoHideTop.ItemClicked += OnAutoHideItemClicked;
        _autoHideBottom.ItemClicked += OnAutoHideItemClicked;

        // Build the root structure: DockPanel with bars + center, then flyout overlay
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

        // Grid with two layers: content + flyout overlay
        _rootGrid = new Grid();
        _rootGrid.Children.Add(_rootPanel);
        _rootGrid.Children.Add(_autoHideFlyout);

        Content = _rootGrid;
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
    /// Wires all events on a tab control.
    /// </summary>
    private void WireTabControlEvents(DockTabControl tabControl)
    {
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
        if (_autoHideFlyout.IsOpen && _autoHideFlyout.CurrentItem == item)
        {
            // Toggle off
            _autoHideFlyout.Close();
            return;
        }

        _autoHideFlyout.ShowForItem(item, ContentFactory, item.LastDockSide);
    }

    private void OnAutoHideRestoreRequested(DockItem item)
    {
        _autoHideFlyout.Close();

        if (_engine is null || Layout is null) return;

        // Re-dock to original side
        var direction = item.LastDockSide switch
        {
            Core.DockSide.Left => DockDirection.Left,
            Core.DockSide.Right => DockDirection.Right,
            Core.DockSide.Top => DockDirection.Top,
            Core.DockSide.Bottom => DockDirection.Bottom,
            _ => DockDirection.Bottom
        };

        _engine.RestoreFromAutoHide(item, Layout.MainDocumentHost, direction);
        RebuildVisualTree();
    }

    private void OnAutoHideCloseRequested(DockItem item)
    {
        _autoHideFlyout.Close();

        if (_engine is null) return;

        _engine.Close(item);
        RebuildVisualTree();
    }

    private void OnAutoHideFloatRequested(DockItem item)
    {
        _autoHideFlyout.Close();

        if (_engine is null || Layout is null) return;

        // Remove from auto-hide, then float
        _engine.RestoreFromAutoHide(item, Layout.MainDocumentHost, DockDirection.Center);
        _engine.Float(item);
        RebuildVisualTree();
    }
}
