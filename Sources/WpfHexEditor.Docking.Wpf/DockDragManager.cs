// ==========================================================
// Project: WpfHexEditor.Docking.Wpf
// File: DockDragManager.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Manages drag and drop operations for dock tabs and floating windows using
//     WPF mouse capture. Shows two simultaneous overlays in Visual Studio 2022
//     style: an edge overlay with 4 zone indicators and a panel compass with
//     5 directional indicators over the hovered panel.
//
// Architecture Notes:
//     Coordinates DockOverlayWindow (panel compass) and DockEdgeOverlayWindow
//     (edge indicators). Panel compass takes hit-test priority over edge overlay.
//     DragPreviewWindow shows a semi-transparent snap preview during drags.
//     Uses Win32 mouse capture for reliable cross-window drag tracking.
//
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
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

            hit = hit is Visual or Visual3D
                ? VisualTreeHelper.GetParent(hit)
                : (hit as FrameworkContentElement)?.Parent as DependencyObject;
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
        // DocumentTabHost can be unbound (Node = null) when it is empty (ShowEmptyPlaceholder path).
        // Fall back to MainDocumentHost so the compass still appears and documents can be re-docked.
        if (targetNode is null && targetTab is DocumentTabHost)
            targetNode = _dockControl.Layout?.MainDocumentHost;
        var isSelfDrag = targetNode != null && targetNode == _originalGroup;

        // Documents: compass only over DocumentTabHost.
        // Panels: compass only over regular DockTabControl (NOT DocumentTabHost).
        // Allowing panels to see the compass over DocumentTabHost would let the user drop a tool
        // panel into the document zone (Center), which corrupts item.IsDocument.
        bool isDocumentDrag = _draggedItem?.IsDocument == true;
        bool showCompass = !isSelfDrag && targetTab != null && targetNode != null
            && (isDocumentDrag ? targetTab is DocumentTabHost : targetTab is not DocumentTabHost);

        // --- Panel overlay ---
        if (showCompass)
        {
            _targetGroup = targetNode;

            // Documents get equal 50/50 split; tool panels use the default 25/75
            _panelOverlay.SplitRatio = isDocumentDrag ? 0.5 : 0.25;

            // Only reposition when the target element changes
            if (_targetElement != targetTab)
            {
                _targetElement = targetTab;
                _panelOverlay.ShowOverTarget(targetTab);
            }
        }
        else
        {
            // Self-drag, no target, or document dragged over a panel: hide panel overlay
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
            // Show edge overlay only for panel drags (documents use the panel compass on DocumentTabHost)
            if (_draggedItem?.IsDocument == true)
            {
                if (_edgeOverlay.IsVisible) { _edgeOverlay.HighlightedDirection = null; _edgeOverlay.Hide(); }
            }
            else if (!_edgeOverlay.IsVisible)
            {
                _edgeOverlay.ShowOverTarget(_dockControl.CenterHost);
            }

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
            bool isDocumentDrop = _draggedItem?.IsDocument == true;

            // Multi-item floating group: dock ALL items, not just the active one
            var floatingItems = _sourceFloatingWindow.Node?.Items.ToList();

            // Defense-in-depth: never dock a tool panel into DocumentHostNode via Center.
            // Fix B prevents the compass from appearing there, but guard here as a safety net.
            if (!isDocumentDrop
                && _lastDirection.Value == DockDirection.Center
                && _targetGroup is DocumentHostNode)
            {
                EndFloatingDrag();
                return;
            }

            if (floatingItems is { Count: > 1 })
            {
                DockGroupNode? landingGroup = null;

                foreach (var item in floatingItems)
                {
                    if (landingGroup is null)
                    {
                        // First item creates the split (or joins Center)
                        if (isDocumentDrop && _lastDirection.Value != DockDirection.Center
                            && _targetGroup is DocumentHostNode docHostMulti)
                        {
                            engine.SplitDocumentHost(item, docHostMulti, _lastDirection.Value);
                            landingGroup = item.Owner;
                        }
                        else
                        {
                            engine.Dock(item, _targetGroup, _lastDirection.Value);
                            landingGroup = _lastDirection.Value == DockDirection.Center
                                ? _targetGroup
                                : item.Owner;
                        }
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
                if (isDocumentDrop && _lastDirection.Value != DockDirection.Center
                    && _targetGroup is DocumentHostNode docHost)
                    engine.SplitDocumentHost(_draggedItem, docHost, _lastDirection.Value);
                else
                    engine.Dock(_draggedItem, _targetGroup, _lastDirection.Value);
            }

            _dockControl.RebuildVisualTree();
            // OnItemDocked already closed the floating window via CloseWindowForItem.
            // Guard against double-close (calling Close() on a disposed WPF Window throws ObjectDisposedException).
            if (_sourceFloatingWindow.IsLoaded)
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
        if (!_lastDirection.HasValue)
        {
            HideSnapPreview();
            return;
        }

        // The panel compass already draws its own _previewZone rectangle inside the overlay.
        // Showing a separate snap-preview window on top would create a duplicate rectangle.
        if (_panelOverlay is { IsVisible: true } && _panelOverlay.HighlightedDirection.HasValue)
        {
            HideSnapPreview();
            return;
        }

        // Determine the target element for snap zone calculation.
        // Edge indicators target MainDocumentHost → use CenterHost as the visual reference.
        // Panel compass indicators target a specific group → use the hovered DockTabControl.
        var target = _targetGroup == _dockControl.Layout?.MainDocumentHost
            ? (UIElement)_dockControl.CenterHost
            : _targetElement;

        if (target is null)
        {
            HideSnapPreview();
            return;
        }

        var tl = target.PointToScreen(new Point(0, 0));
        var br = target.PointToScreen(new Point(target.RenderSize.Width, target.RenderSize.Height));
        var dipTl = DpiHelper.ScreenToDipForPoint(tl);
        var dipBr = DpiHelper.ScreenToDipForPoint(br);
        var tw = dipBr.X - dipTl.X;
        var th = dipBr.Y - dipTl.Y;

        // Documents use 50/50 split ratio; tool panels use the default 25/75
        var r = _draggedItem?.IsDocument == true ? 0.5 : 0.25;
        var zone = _lastDirection.Value switch
        {
            DockDirection.Left   => new Rect(dipTl.X, dipTl.Y, tw * r, th),
            DockDirection.Right  => new Rect(dipTl.X + tw * (1 - r), dipTl.Y, tw * r, th),
            DockDirection.Top    => new Rect(dipTl.X, dipTl.Y, tw, th * r),
            DockDirection.Bottom => new Rect(dipTl.X, dipTl.Y + th * (1 - r), tw, th * r),
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
