// Apache 2.0 - 2026
// WpfHexEditor.Docking - VS-Like Docking Framework

using System.Windows;
using System.Windows.Input;
using WpfHexEditor.Docking.Controls;
using WpfHexEditor.Docking.Model;

namespace WpfHexEditor.Docking.Drag;

/// <summary>
/// State machine managing the full lifecycle of a drag-and-dock operation.
/// States: Idle → DragStarting → Dragging → Dropping → Idle
/// </summary>
internal class DockDragManager
{
    private const double DragThreshold = 5.0; // Pixels before drag activates

    private readonly DockManager _manager;
    private DragPreviewWindow? _previewWindow;
    private DockingGuideOverlay? _guideOverlay;
    private DockDropTarget? _currentTarget;

    private DragState _state = DragState.Idle;
    private LayoutContent? _draggedContent;
    private Point _dragStartScreenPoint;
    private FrameworkElement? _dragSource; // The element that initiated the drag (for mouse capture)

    public DockDragManager(DockManager manager)
    {
        _manager = manager;
    }

    /// <summary>Current drag state.</summary>
    public DragState State => _state;

    /// <summary>The content being dragged, if any.</summary>
    public LayoutContent? DraggedContent => _draggedContent;

    /// <summary>
    /// Begin tracking a potential drag from a tab header or pane header.
    /// Call this on MouseDown. The drag won't actually start until the mouse
    /// moves past the threshold (to prevent accidental drags).
    /// </summary>
    public void BeginDragTracking(LayoutContent content, Point screenPoint, FrameworkElement source)
    {
        if (_state != DragState.Idle) return;

        _draggedContent = content;
        _dragStartScreenPoint = screenPoint;
        _dragSource = source;
        _state = DragState.DragStarting;

        // Capture mouse on the source element
        source.CaptureMouse();
        source.MouseMove += OnMouseMove;
        source.MouseUp += OnMouseUp;
        source.LostMouseCapture += OnLostCapture;
        source.KeyDown += OnKeyDown;
    }

    /// <summary>Cancel the current drag operation.</summary>
    public void CancelDrag()
    {
        if (_state == DragState.Idle) return;
        CleanupDrag();
    }

    #region Mouse event handlers

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var screenPoint = GetScreenPoint(e, sender as FrameworkElement);

        switch (_state)
        {
            case DragState.DragStarting:
                // Check if we've exceeded the drag threshold
                var delta = screenPoint - _dragStartScreenPoint;
                if (Math.Abs(delta.X) > DragThreshold || Math.Abs(delta.Y) > DragThreshold)
                {
                    ActivateDrag(screenPoint);
                }
                break;

            case DragState.Dragging:
                ProcessDragOver(screenPoint);
                break;
        }
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        var screenPoint = GetScreenPoint(e, sender as FrameworkElement);

        switch (_state)
        {
            case DragState.DragStarting:
                // Mouse released before threshold reached - this was just a click, not a drag
                CleanupDrag();
                break;

            case DragState.Dragging:
                ExecuteDrop(screenPoint);
                break;
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CancelDrag();
            e.Handled = true;
        }
    }

    private void OnLostCapture(object sender, MouseEventArgs e)
    {
        // If we lose capture unexpectedly during a drag, cancel it
        if (_state == DragState.Dragging || _state == DragState.DragStarting)
        {
            CleanupDrag();
        }
    }

    #endregion

    #region Core drag logic

    /// <summary>
    /// Transition from DragStarting to Dragging. Creates the preview and overlay windows.
    /// </summary>
    private void ActivateDrag(Point screenPoint)
    {
        _state = DragState.Dragging;

        // Create the translucent preview window
        _previewWindow = new DragPreviewWindow();

        // Create the guide overlay
        _guideOverlay = new DockingGuideOverlay();

        // Update position immediately
        ProcessDragOver(screenPoint);
    }

    /// <summary>
    /// Called repeatedly as the mouse moves during a drag.
    /// Updates the preview window, hit-tests guides, and determines the drop target.
    /// </summary>
    private void ProcessDragOver(Point screenPoint)
    {
        if (_draggedContent == null) return;

        // Get manager screen bounds
        Rect managerBounds;
        try
        {
            var topLeft = _manager.PointToScreen(new Point(0, 0));
            managerBounds = new Rect(topLeft, new Size(_manager.ActualWidth, _manager.ActualHeight));
        }
        catch
        {
            return; // Manager not in visual tree
        }

        // Find which pane control is under the cursor
        var paneControl = HitTestHelper.FindPaneControlAtPoint(_manager, screenPoint);
        var paneModel = paneControl != null ? HitTestHelper.GetModelFromControl(paneControl) : null;

        Rect? paneBounds = null;
        if (paneControl != null)
        {
            paneBounds = HitTestHelper.GetScreenBounds(paneControl);
        }

        // Show/update the guide overlay
        _guideOverlay?.ShowOverlay(managerBounds, paneBounds);

        // Hit-test the guides
        var (side, isRoot) = _guideOverlay?.HitTestGuides(screenPoint) ?? (DockSide.None, false);

        // Determine drop target
        _currentTarget = null;

        if (side != DockSide.None || (side == DockSide.None && paneBounds.HasValue && !isRoot))
        {
            if (isRoot)
            {
                // Root-level docking
                var dropType = side switch
                {
                    DockSide.Left => DropType.RootLeft,
                    DockSide.Right => DropType.RootRight,
                    DockSide.Top => DropType.RootTop,
                    DockSide.Bottom => DropType.RootBottom,
                    _ => (DropType?)null
                };

                if (dropType.HasValue && _manager.Layout?.RootPanel != null)
                {
                    _currentTarget = new DockDropTarget(_manager.Layout.RootPanel, side, dropType.Value);
                    _currentTarget.PreviewBounds = DockingGuideOverlay.CalculatePreviewBounds(
                        paneBounds ?? managerBounds, side, true, managerBounds);
                }
            }
            else if (paneModel != null)
            {
                // Pane-level docking
                DropType dropType;
                if (side == DockSide.None)
                {
                    // Center guide = tab into
                    dropType = DropType.TabInto;
                }
                else
                {
                    dropType = side switch
                    {
                        DockSide.Left => DropType.SplitLeft,
                        DockSide.Right => DropType.SplitRight,
                        DockSide.Top => DropType.SplitTop,
                        DockSide.Bottom => DropType.SplitBottom,
                        _ => DropType.TabInto
                    };
                }

                // Don't allow dropping onto the same pane if it's a tab-into and it's the source
                var isSamePane = _draggedContent.Parent == paneModel;
                if (!(dropType == DropType.TabInto && isSamePane))
                {
                    _currentTarget = new DockDropTarget(paneModel, side, dropType);
                    _currentTarget.PreviewBounds = DockingGuideOverlay.CalculatePreviewBounds(
                        paneBounds!.Value, side, false, managerBounds);
                }
            }
        }

        // Update preview window
        if (_currentTarget != null)
        {
            _previewWindow?.ShowPreview(_currentTarget.PreviewBounds);
        }
        else
        {
            _previewWindow?.HidePreview();
        }
    }

    /// <summary>
    /// Execute the drop at the current target.
    /// </summary>
    private void ExecuteDrop(Point screenPoint)
    {
        _state = DragState.Dropping;

        if (_currentTarget != null && _draggedContent != null)
        {
            _currentTarget.Execute(_draggedContent);

            // Rebuild the layout controls after the model change
            _manager.RebuildLayoutControls();
        }

        CleanupDrag();
    }

    /// <summary>
    /// Clean up all drag state, windows, and event handlers.
    /// </summary>
    private void CleanupDrag()
    {
        _state = DragState.Idle;

        // Close windows
        _previewWindow?.Close();
        _previewWindow = null;

        _guideOverlay?.HideOverlay();
        _guideOverlay?.Close();
        _guideOverlay = null;

        _currentTarget = null;

        // Unhook events from source
        if (_dragSource != null)
        {
            _dragSource.MouseMove -= OnMouseMove;
            _dragSource.MouseUp -= OnMouseUp;
            _dragSource.LostMouseCapture -= OnLostCapture;
            _dragSource.KeyDown -= OnKeyDown;

            if (_dragSource.IsMouseCaptured)
                _dragSource.ReleaseMouseCapture();

            _dragSource = null;
        }

        _draggedContent = null;
    }

    #endregion

    #region Helpers

    private static Point GetScreenPoint(MouseEventArgs e, FrameworkElement? relativeTo)
    {
        if (relativeTo != null)
        {
            try
            {
                return relativeTo.PointToScreen(e.GetPosition(relativeTo));
            }
            catch
            {
                // Fallback if not in visual tree
            }
        }
        return default;
    }

    #endregion
}

/// <summary>
/// States of the drag state machine.
/// </summary>
internal enum DragState
{
    /// <summary>No drag in progress.</summary>
    Idle,

    /// <summary>Mouse is down, tracking movement to see if threshold is exceeded.</summary>
    DragStarting,

    /// <summary>Actively dragging with preview and guides visible.</summary>
    Dragging,

    /// <summary>Executing the drop operation.</summary>
    Dropping
}
