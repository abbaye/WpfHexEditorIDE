//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Docking.Core;
using WpfHexEditor.Docking.Core.Nodes;

namespace WpfHexEditor.Docking.Wpf;

/// <summary>
/// Manages drag and drop operations for dock tabs and floating windows using mouse capture.
/// Shows two overlays simultaneously (VS2022-style):
/// - Edge overlay: 4 indicators at the edges of the entire dock area (dock to MainDocumentHost)
/// - Panel compass: 5 indicators on the specific panel being hovered (dock relative to that panel)
/// Panel compass takes hit-test priority over edge overlay.
/// </summary>
public class DockDragManager
{
    private readonly DockControl _dockControl;
    private DockOverlayWindow? _panelOverlay;
    private DockEdgeOverlayWindow? _edgeOverlay;

    private DockItem? _draggedItem;
    private DockGroupNode? _originalGroup;
    private DockDirection? _lastDirection;
    private bool _isDragging;

    // Target group tracking
    private DockGroupNode? _targetGroup;
    private UIElement? _targetElement;

    // Floating window drag state
    private FloatingWindow? _sourceFloatingWindow;
    private DragPreviewWindow? _snapPreview;
    private Point _dragOffset;
    private Point _originalWindowPos;

    public DockDragManager(DockControl dockControl)
    {
        _dockControl = dockControl;
    }

    public bool IsDragging => _isDragging;

    /// <summary>
    /// Walks up the visual tree from the element at the given point to find the containing DockTabControl.
    /// Also checks sibling children of parent Panels: side panels use a DockPanel that holds
    /// a title bar Border alongside the DockTabControl, so when the cursor is over the title bar,
    /// the walk-up finds the parent DockPanel and locates the sibling DockTabControl.
    /// Returns null if no DockTabControl is found (e.g., over a splitter or empty area).
    /// </summary>
    private DockTabControl? FindTargetTabControl(Point localPosInCenterHost)
    {
        var hit = _dockControl.CenterHost.InputHitTest(localPosInCenterHost) as DependencyObject;
        while (hit != null)
        {
            if (hit is DockTabControl tabControl)
                return tabControl;

            // Side panels: DockPanel { Border(title bar), DockTabControl }.
            // When cursor is over the title bar, check sibling children for DockTabControl.
            if (hit is Panel panel)
            {
                foreach (UIElement child in panel.Children)
                {
                    if (child is DockTabControl tc)
                        return tc;
                }
            }

            hit = VisualTreeHelper.GetParent(hit);
        }
        return null;
    }

    /// <summary>
    /// Updates both overlays based on the cursor position.
    /// Panel compass: shown over the specific panel (hidden for self-drag).
    /// Edge overlay: always visible when cursor is over CenterHost.
    /// Priority: panel compass > edge overlay.
    /// </summary>
    private void UpdateOverlays(DockTabControl? targetTab, Point screenPos)
    {
        if (_panelOverlay is null || _edgeOverlay is null) return;

        var targetNode = targetTab?.Node;
        var isSelfDrag = targetNode != null && targetNode == _originalGroup;

        // --- Panel overlay ---
        if (!isSelfDrag && targetTab != null && targetNode != null)
        {
            _targetGroup = targetNode;

            // Only reposition when the target element changes
            if (_targetElement != targetTab)
            {
                _targetElement = targetTab;
                _panelOverlay.ShowOverTarget(targetTab);
            }
        }
        else
        {
            // Self-drag or no target: hide panel overlay
            _targetElement = null;
            _panelOverlay.HighlightedDirection = null;
            if (_panelOverlay.IsVisible) _panelOverlay.Hide();
        }

        // --- Hit test with priority: panel compass first, then edge ---

        // 1. Panel compass (if visible)
        DockDirection? panelDir = _panelOverlay.IsVisible ? _panelOverlay.HitTest(screenPos) : null;
        if (panelDir.HasValue && targetNode != null)
        {
            _lastDirection = panelDir;
            _targetGroup = targetNode;
            _panelOverlay.HighlightedDirection = panelDir;
            _edgeOverlay.HighlightedDirection = null;
            return;
        }

        // Clear panel highlight when not over a panel indicator
        if (_panelOverlay.IsVisible)
            _panelOverlay.HighlightedDirection = null;

        // 2. Edge overlay
        DockDirection? edgeDir = _edgeOverlay.HitTest(screenPos);
        if (edgeDir.HasValue && _dockControl.Layout != null)
        {
            _lastDirection = edgeDir;
            _targetGroup = _dockControl.Layout.MainDocumentHost;
            _edgeOverlay.HighlightedDirection = edgeDir;
            return;
        }

        // 3. Neither
        _lastDirection = null;
        _targetGroup = isSelfDrag ? null : targetNode; // keep for reference but no direction
        _edgeOverlay.HighlightedDirection = null;
    }

    /// <summary>
    /// Hides both overlays and clears tracking state.
    /// </summary>
    private void HideAllOverlays()
    {
        _lastDirection = null;
        _targetGroup = null;
        _targetElement = null;

        if (_panelOverlay != null)
        {
            _panelOverlay.HighlightedDirection = null;
            if (_panelOverlay.IsVisible) _panelOverlay.Hide();
        }
        if (_edgeOverlay != null)
        {
            _edgeOverlay.HighlightedDirection = null;
            if (_edgeOverlay.IsVisible) _edgeOverlay.Hide();
        }
    }

    /// <summary>
    /// Starts a drag operation for the given item by immediately floating it (VS2022-style).
    /// The item is detached from its dock group at drag start and becomes a FloatingWindow
    /// that follows the cursor. Overlays appear when hovering over the dock area.
    /// </summary>
    public void BeginDrag(DockItem item)
    {
        if (_isDragging) return;
        if (_dockControl.Engine is null || _dockControl.Layout is null) return;

        // Capture cursor position in DIPs before the layout changes
        var screenPos = _dockControl.PointToScreen(Mouse.GetPosition(_dockControl));
        var mouseDip  = DpiHelper.ScreenToDip(_dockControl, screenPos);

        // Capture the panel's rendered size BEFORE detaching (visual tree is still alive)
        _dockControl.CaptureDockedSizeForFloat(item);

        // Detach item from dock layout → fires OnItemFloated → FloatingWindowManager creates the window
        _dockControl.Engine.Float(item);
        _dockControl.RebuildVisualTree();

        // Retrieve the newly created floating window
        var window = _dockControl.FloatingManager?.FindWindowForItem(item);
        if (window is null) return;

        // Reposition so the cursor lands approximately in the title bar (DIPs)
        window.Left = mouseDip.X - 50;
        window.Top  = mouseDip.Y - 10;

        // Hand off to floating drag: the window follows the cursor and overlays appear on hover
        BeginFloatingDrag(item, window);
    }

    /// <summary>
    /// Starts a drag operation for an entire group (VS2026-style title-bar drag).
    /// All items in the group are instantly floated together as one FloatingWindow.
    /// </summary>
    public void BeginGroupDrag(DockGroupNode group, DockItem activeItem)
    {
        if (_isDragging) return;
        if (_dockControl.Engine is null || _dockControl.Layout is null) return;

        // Capture cursor position in DIPs before the layout changes
        var screenPos = _dockControl.PointToScreen(Mouse.GetPosition(_dockControl));
        var mouseDip  = DpiHelper.ScreenToDip(_dockControl, screenPos);

        // Capture the group's rendered size BEFORE detaching (visual tree is still alive)
        _dockControl.CaptureDockedSizeForFloat(group);

        // Float the entire group → fires GroupFloated → FloatingWindowManager.CreateFloatingWindowForGroup
        _dockControl.Engine.FloatGroup(group);
        _dockControl.RebuildVisualTree();

        // Retrieve the floating window via the active item reference (unchanged after FloatGroup)
        var window = _dockControl.FloatingManager?.FindWindowForItem(activeItem);
        if (window is null) return;

        // Position so the cursor lands approximately in the title bar (DIPs)
        window.Left = mouseDip.X - 50;
        window.Top  = mouseDip.Y - 10;

        // Hand off to floating drag: window follows cursor, overlays appear on hover
        BeginFloatingDrag(activeItem, window);
    }

    /// <summary>
    /// Starts a drag operation for a floating window. The window follows the cursor
    /// and overlays appear when over the dock area.
    /// </summary>
    public void BeginFloatingDrag(DockItem item, FloatingWindow sourceWindow)
    {
        if (_isDragging) return;
        if (_dockControl.Engine is null || _dockControl.Layout is null) return;

        _isDragging = true;
        _draggedItem = item;
        _originalGroup = item.Owner;
        _lastDirection = null;
        _targetGroup = null;
        _targetElement = null;
        _sourceFloatingWindow = sourceWindow;

        // Record offset from cursor to window corner using per-monitor DIPs for smooth cross-monitor drag
        var cursorScreen = sourceWindow.PointToScreen(Mouse.GetPosition(sourceWindow));
        var cursorDip = DpiHelper.ScreenToDipForPoint(cursorScreen);
        _dragOffset = new Point(cursorDip.X - sourceWindow.Left, cursorDip.Y - sourceWindow.Top);
        _originalWindowPos = new Point(sourceWindow.Left, sourceWindow.Top);

        _panelOverlay ??= new DockOverlayWindow();
        _edgeOverlay ??= new DockEdgeOverlayWindow();

        // Wire event handlers first, then capture mouse.
        // try-finally ensures capture is released if an exception occurs.
        sourceWindow.PreviewMouseMove += OnFloatingMouseMove;
        sourceWindow.PreviewMouseLeftButtonUp += OnFloatingMouseUp;
        sourceWindow.KeyDown += OnFloatingKeyDown;
        try
        {
            Mouse.Capture(sourceWindow, CaptureMode.SubTree);
        }
        catch
        {
            EndFloatingDrag();
            throw;
        }
    }

    #region Floating window drag handlers

    private void OnFloatingMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || _sourceFloatingWindow is null) return;

        // Get cursor in both physical pixels and per-monitor DIPs
        var screenPos = _sourceFloatingWindow.PointToScreen(e.GetPosition(_sourceFloatingWindow));
        var dipPos = DpiHelper.ScreenToDipForPoint(screenPos);

        // Move the floating window following cursor (DIPs)
        _sourceFloatingWindow.Left = dipPos.X - _dragOffset.X;
        _sourceFloatingWindow.Top = dipPos.Y - _dragOffset.Y;

        // Check if cursor is over the dock area using physical pixel bounds
        var topLeft = _dockControl.CenterHost.PointToScreen(new Point(0, 0));
        var bottomRight = _dockControl.CenterHost.PointToScreen(
            new Point(_dockControl.CenterHost.ActualWidth, _dockControl.CenterHost.ActualHeight));
        var dockBounds = new Rect(topLeft, bottomRight);

        _panelOverlay ??= new DockOverlayWindow();
        _edgeOverlay ??= new DockEdgeOverlayWindow();

        if (dockBounds.Contains(screenPos))
        {
            // Show edge overlay if not visible
            if (!_edgeOverlay.IsVisible)
                _edgeOverlay.ShowOverTarget(_dockControl.CenterHost);

            // Convert screen position to CenterHost local coords for hit testing
            var localInCenterHost = _dockControl.CenterHost.PointFromScreen(screenPos);
            var targetTab = FindTargetTabControl(localInCenterHost);
            UpdateOverlays(targetTab, screenPos);

            // Show snap preview when hovering over a dock indicator
            UpdateSnapPreview(screenPos);
        }
        else
        {
            HideAllOverlays();
            HideSnapPreview();
        }
    }

    private void OnFloatingMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging || _draggedItem is null || _sourceFloatingWindow is null) return;

        var engine = _dockControl.Engine;
        var layout = _dockControl.Layout;
        if (engine is null || layout is null)
        {
            EndFloatingDrag();
            return;
        }

        if (_lastDirection.HasValue && _targetGroup != null)
        {
            // Multi-item floating group: dock ALL items, not just the active one
            var floatingItems = _sourceFloatingWindow.Node?.Items.ToList();

            if (floatingItems is { Count: > 1 })
            {
                DockGroupNode? landingGroup = null;

                foreach (var item in floatingItems)
                {
                    if (landingGroup is null)
                    {
                        // First item creates the split (or joins Center)
                        engine.Dock(item, _targetGroup, _lastDirection.Value);
                        landingGroup = _lastDirection.Value == DockDirection.Center
                            ? _targetGroup
                            : item.Owner;
                    }
                    else
                    {
                        // Remaining items join the same new group
                        engine.Dock(item, landingGroup, DockDirection.Center);
                    }
                }
            }
            else
            {
                engine.Dock(_draggedItem, _targetGroup, _lastDirection.Value);
            }

            _dockControl.RebuildVisualTree();
            _sourceFloatingWindow.Close();
        }

        EndFloatingDrag();
    }

    private void OnFloatingKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _isDragging && _sourceFloatingWindow is not null)
        {
            _sourceFloatingWindow.Left = _originalWindowPos.X;
            _sourceFloatingWindow.Top = _originalWindowPos.Y;
            EndFloatingDrag();
            e.Handled = true;
        }
    }

    #endregion

    /// <summary>
    /// Shows a semi-transparent snap preview over the target zone where the item would dock.
    /// </summary>
    private void UpdateSnapPreview(Point screenPos)
    {
        if (!_lastDirection.HasValue || _targetElement is null)
        {
            HideSnapPreview();
            return;
        }

        // Determine the target element for snap zone calculation
        var target = _lastDirection.HasValue && _targetGroup == _dockControl.Layout?.MainDocumentHost
            ? (UIElement)_dockControl.CenterHost
            : _targetElement;

        var tl = target.PointToScreen(new Point(0, 0));
        var br = target.PointToScreen(new Point(target.RenderSize.Width, target.RenderSize.Height));
        var dipTl = DpiHelper.ScreenToDipForPoint(tl);
        var dipBr = DpiHelper.ScreenToDipForPoint(br);
        var tw = dipBr.X - dipTl.X;
        var th = dipBr.Y - dipTl.Y;

        var zone = _lastDirection.Value switch
        {
            DockDirection.Left   => new Rect(dipTl.X, dipTl.Y, tw * 0.25, th),
            DockDirection.Right  => new Rect(dipTl.X + tw * 0.75, dipTl.Y, tw * 0.25, th),
            DockDirection.Top    => new Rect(dipTl.X, dipTl.Y, tw, th * 0.25),
            DockDirection.Bottom => new Rect(dipTl.X, dipTl.Y + th * 0.75, tw, th * 0.25),
            DockDirection.Center => new Rect(dipTl.X, dipTl.Y, tw, th),
            _                    => Rect.Empty
        };

        if (zone == Rect.Empty || zone.Width < 1 || zone.Height < 1)
        {
            HideSnapPreview();
            return;
        }

        _snapPreview ??= new DragPreviewWindow(_draggedItem?.Title ?? "");
        _snapPreview.ShowSnapPreview(zone);
    }

    private void HideSnapPreview()
    {
        if (_snapPreview is { IsVisible: true })
        {
            _snapPreview.HideSnapPreview();
            _snapPreview.Hide();
        }
    }

    private void EndFloatingDrag()
    {
        var window = _sourceFloatingWindow;

        _isDragging = false;
        _draggedItem = null;
        _originalGroup = null;
        _lastDirection = null;
        _targetGroup = null;
        _targetElement = null;
        _sourceFloatingWindow = null;

        Mouse.Capture(null);

        if (window is not null)
        {
            window.PreviewMouseMove -= OnFloatingMouseMove;
            window.PreviewMouseLeftButtonUp -= OnFloatingMouseUp;
            window.KeyDown -= OnFloatingKeyDown;
        }

        _panelOverlay?.Hide();
        _edgeOverlay?.Hide();
        HideSnapPreview();
        _snapPreview?.Close();
        _snapPreview = null;
    }
}
