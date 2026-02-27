//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System.Windows;
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
    private DragPreviewWindow? _previewWindow;

    private DockItem? _draggedItem;
    private DockGroupNode? _originalGroup;
    private DockDirection? _lastDirection;
    private bool _isDragging;

    // Target group tracking
    private DockGroupNode? _targetGroup;
    private UIElement? _targetElement;

    // Floating window drag state
    private FloatingWindow? _sourceFloatingWindow;
    private Point _dragOffset;
    private Point _originalWindowPos;

    public DockDragManager(DockControl dockControl)
    {
        _dockControl = dockControl;
    }

    public bool IsDragging => _isDragging;

    /// <summary>
    /// Converts a physical screen pixel point to WPF DIPs using the given visual's DPI.
    /// </summary>
    private static Point ScreenToDip(Visual visual, Point screenPoint)
    {
        var source = PresentationSource.FromVisual(visual);
        if (source?.CompositionTarget != null)
            return source.CompositionTarget.TransformFromDevice.Transform(screenPoint);
        return screenPoint;
    }

    /// <summary>
    /// Walks up the visual tree from the element at the given point to find the containing DockTabControl.
    /// Returns null if no DockTabControl is found (e.g., over a splitter or empty area).
    /// </summary>
    private DockTabControl? FindTargetTabControl(Point localPosInCenterHost)
    {
        var hit = _dockControl.CenterHost.InputHitTest(localPosInCenterHost) as DependencyObject;
        while (hit != null)
        {
            if (hit is DockTabControl tabControl)
                return tabControl;
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
    /// Starts a drag operation for the given item using mouse capture on the DockControl.
    /// Used when dragging a tab from a docked panel.
    /// </summary>
    public void BeginDrag(DockItem item)
    {
        if (_isDragging) return;
        if (_dockControl.Engine is null || _dockControl.Layout is null) return;

        _isDragging = true;
        _draggedItem = item;
        _originalGroup = item.Owner;
        _lastDirection = null;
        _targetGroup = null;
        _targetElement = null;
        _sourceFloatingWindow = null;

        // Create the floating preview window
        _previewWindow = new DragPreviewWindow(item.Title);
        var dipPos = ScreenToDip(_dockControl, _dockControl.PointToScreen(Mouse.GetPosition(_dockControl)));
        _previewWindow.MoveTo(dipPos);
        _previewWindow.Show();

        // Create both overlays (edge overlay shown immediately, panel overlay on hover)
        _panelOverlay ??= new DockOverlayWindow();
        _edgeOverlay ??= new DockEdgeOverlayWindow();
        _edgeOverlay.ShowOverTarget(_dockControl.CenterHost);

        // Capture mouse on the DockControl to receive all mouse events
        _dockControl.PreviewMouseMove += OnPreviewMouseMove;
        _dockControl.PreviewMouseLeftButtonUp += OnPreviewMouseUp;
        _dockControl.KeyDown += OnKeyDown;
        Mouse.Capture(_dockControl, CaptureMode.SubTree);
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

        // Record offset from cursor to window corner in DIPs for smooth drag
        var cursorDip = ScreenToDip(sourceWindow, sourceWindow.PointToScreen(Mouse.GetPosition(sourceWindow)));
        _dragOffset = new Point(cursorDip.X - sourceWindow.Left, cursorDip.Y - sourceWindow.Top);
        _originalWindowPos = new Point(sourceWindow.Left, sourceWindow.Top);

        _panelOverlay ??= new DockOverlayWindow();
        _edgeOverlay ??= new DockEdgeOverlayWindow();

        // Capture mouse on the floating window
        sourceWindow.PreviewMouseMove += OnFloatingMouseMove;
        sourceWindow.PreviewMouseLeftButtonUp += OnFloatingMouseUp;
        sourceWindow.KeyDown += OnFloatingKeyDown;
        Mouse.Capture(sourceWindow, CaptureMode.SubTree);
    }

    #region Tab drag handlers

    private void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;

        // Get cursor position in physical pixels (for overlay HitTest) and DIPs (for preview window)
        var screenPos = _dockControl.PointToScreen(e.GetPosition(_dockControl));
        var dipPos = ScreenToDip(_dockControl, screenPos);

        // Move preview window to follow cursor (DIPs)
        _previewWindow?.MoveTo(dipPos);

        // Check if mouse is within the CenterHost bounds
        var localPos = e.GetPosition(_dockControl.CenterHost);
        var bounds = new Rect(0, 0, _dockControl.CenterHost.ActualWidth, _dockControl.CenterHost.ActualHeight);

        if (bounds.Contains(localPos))
        {
            // Show edge overlay if not visible
            if (_edgeOverlay != null && !_edgeOverlay.IsVisible)
                _edgeOverlay.ShowOverTarget(_dockControl.CenterHost);

            // Find the specific group under the cursor and update both overlays
            var targetTab = FindTargetTabControl(localPos);
            UpdateOverlays(targetTab, screenPos);
        }
        else
        {
            HideAllOverlays();
        }
    }

    private void OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging || _draggedItem is null) return;

        var engine = _dockControl.Engine;
        var layout = _dockControl.Layout;
        if (engine is null || layout is null)
        {
            EndDrag();
            return;
        }

        if (_lastDirection.HasValue && _targetGroup != null)
        {
            engine.Dock(_draggedItem, _targetGroup, _lastDirection.Value);
            EndDrag();
            _dockControl.RebuildVisualTree();
        }
        else
        {
            // No compass direction or no valid target → cancel drag
            EndDrag();
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _isDragging)
        {
            EndDrag();
            e.Handled = true;
        }
    }

    #endregion

    #region Floating window drag handlers

    private void OnFloatingMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || _sourceFloatingWindow is null) return;

        // Get cursor in both physical pixels and DIPs
        var screenPos = _sourceFloatingWindow.PointToScreen(e.GetPosition(_sourceFloatingWindow));
        var dipPos = ScreenToDip(_sourceFloatingWindow, screenPos);

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
        }
        else
        {
            HideAllOverlays();
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
            engine.Dock(_draggedItem, _targetGroup, _lastDirection.Value);
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

    private void EndDrag()
    {
        _isDragging = false;
        _draggedItem = null;
        _originalGroup = null;
        _lastDirection = null;
        _targetGroup = null;
        _targetElement = null;
        _sourceFloatingWindow = null;

        Mouse.Capture(null);

        _dockControl.PreviewMouseMove -= OnPreviewMouseMove;
        _dockControl.PreviewMouseLeftButtonUp -= OnPreviewMouseUp;
        _dockControl.KeyDown -= OnKeyDown;

        _previewWindow?.Close();
        _previewWindow = null;

        _panelOverlay?.Hide();
        _edgeOverlay?.Hide();
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
    }
}
