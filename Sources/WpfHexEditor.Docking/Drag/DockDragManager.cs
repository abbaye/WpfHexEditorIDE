// Apache 2.0 - 2026
// WpfHexEditor.Docking - VS-Like Docking Framework

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using WpfHexEditor.Docking.Controls;
using WpfHexEditor.Docking.Model;

namespace WpfHexEditor.Docking.Drag;

/// <summary>
/// State machine managing the full lifecycle of a drag-and-dock operation.
/// States: Idle → DragStarting → Dragging → Dropping → Idle
///
/// DragStarting phase uses WPF mouse capture (no extra windows shown yet).
/// Dragging phase switches to Win32 message-level tracking via ComponentDispatcher,
/// which survives showing overlay windows that would otherwise disrupt WPF capture.
/// </summary>
internal class DockDragManager
{
    private const double DragThreshold = 5.0; // Pixels before drag activates

    #region Win32 interop

    [DllImport("user32.dll")]
    private static extern IntPtr SetCapture(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    private const int WM_MOUSEMOVE = 0x0200;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_KEYDOWN = 0x0100;
    private const int VK_ESCAPE = 0x1B;

    #endregion

    private readonly DockManager _manager;
    private DragPreviewWindow? _previewWindow;
    private DockingGuideOverlay? _guideOverlay;
    private DockDropTarget? _currentTarget;

    private DragState _state = DragState.Idle;
    private LayoutContent? _draggedContent;
    private Point _dragStartScreenPoint;
    private FrameworkElement? _dragSource; // Element with WPF capture (DragStarting phase only)
    private bool _win32Hooked;

    // Cache last known pane model and bounds, so compass rose guides still work
    // even if the cursor moves slightly off the pane between moves
    private LayoutElement? _lastPaneModel;
    private Rect? _lastPaneBounds;

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

        // Use WPF capture during DragStarting phase (no extra windows shown yet)
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

    #region WPF event handlers (DragStarting phase only)

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_state != DragState.DragStarting) return;

        var screenPoint = GetScreenPoint(e, sender as FrameworkElement);
        var delta = screenPoint - _dragStartScreenPoint;
        if (Math.Abs(delta.X) > DragThreshold || Math.Abs(delta.Y) > DragThreshold)
        {
            ActivateDrag(screenPoint);
        }
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_state == DragState.DragStarting)
        {
            // Mouse released before threshold - just a click, not a drag
            CleanupDrag();
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
        // Lost WPF capture during DragStarting - cancel
        if (_state == DragState.DragStarting)
        {
            CleanupDrag();
        }
        // Note: During Dragging phase, we don't use WPF capture, so this won't fire
    }

    #endregion

    #region Win32 message handler (Dragging phase)

    private void OnThreadFilterMessage(ref MSG msg, ref bool handled)
    {
        if (_state != DragState.Dragging) return;

        switch (msg.message)
        {
            case WM_MOUSEMOVE:
            {
                GetCursorPos(out var pt);
                ProcessDragOver(new Point(pt.X, pt.Y));
                handled = true;
                break;
            }

            case WM_LBUTTONUP:
            {
                GetCursorPos(out var pt);
                ExecuteDrop(new Point(pt.X, pt.Y));
                handled = true;
                break;
            }

            case WM_KEYDOWN:
            {
                if ((int)msg.wParam == VK_ESCAPE)
                {
                    CancelDrag();
                    handled = true;
                }
                break;
            }
        }
    }

    #endregion

    #region Core drag logic

    /// <summary>
    /// Transition from DragStarting to Dragging.
    /// Switches from WPF mouse capture to Win32 message-level tracking,
    /// then creates the overlay windows.
    /// </summary>
    private void ActivateDrag(Point screenPoint)
    {
        _state = DragState.Dragging;

        // Detach WPF event handlers and release WPF capture
        DetachFromDragSource();

        // Hook Win32 messages BEFORE showing any new windows.
        // ComponentDispatcher.ThreadFilterMessage intercepts all Win32 messages
        // on the UI thread before WPF dispatches them - immune to WPF capture loss.
        ComponentDispatcher.ThreadFilterMessage += OnThreadFilterMessage;
        _win32Hooked = true;

        // Set Win32 capture on the main window HWND so mouse messages
        // reach our thread even when cursor is outside all our windows
        var hwndSource = PresentationSource.FromVisual(_manager) as HwndSource;
        if (hwndSource != null)
            SetCapture(hwndSource.Handle);

        // Now safe to create and show overlay windows - they won't disrupt
        // our Win32 message hook (unlike WPF CaptureMouse)
        _previewWindow = new DragPreviewWindow();
        _guideOverlay = new DockingGuideOverlay();

        // Initial position and hit-test
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
            // Cache the pane so we can use it if the cursor drifts slightly off
            _lastPaneModel = paneModel;
            _lastPaneBounds = paneBounds;
        }

        // Show/update the guide overlay (uses current pane bounds, or keeps previous compass rose visible)
        _guideOverlay?.ShowOverlay(managerBounds, paneBounds ?? _lastPaneBounds);

        // Hit-test the guides
        var (side, isRoot, hitGuide) = _guideOverlay?.HitTestGuides(screenPoint) ?? (DockSide.None, false, false);

        // Determine drop target
        _currentTarget = null;

        // Use cached pane if current pane not found but a guide is hit
        var effectivePaneModel = paneModel ?? _lastPaneModel;
        var effectivePaneBounds = paneBounds ?? _lastPaneBounds;

        if (hitGuide)
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
                        effectivePaneBounds ?? managerBounds, side, true, managerBounds);
                }
            }
            else if (effectivePaneModel != null && effectivePaneBounds.HasValue)
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
                var isSamePane = _draggedContent.Parent == effectivePaneModel;
                if (!(dropType == DropType.TabInto && isSamePane))
                {
                    _currentTarget = new DockDropTarget(effectivePaneModel, side, dropType);
                    _currentTarget.PreviewBounds = DockingGuideOverlay.CalculatePreviewBounds(
                        effectivePaneBounds.Value, side, false, managerBounds);
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

        // Final hit-test at exact drop position
        ProcessDragOver(screenPoint);

        if (_currentTarget != null && _draggedContent != null)
        {
            _currentTarget.Execute(_draggedContent);

            // Rebuild the layout controls after the model change
            _manager.RebuildLayoutControls();
        }

        CleanupDrag();
    }

    /// <summary>
    /// Detach WPF event handlers from the drag source and release WPF capture.
    /// Used during transition from DragStarting to Dragging, and during cleanup.
    /// </summary>
    private void DetachFromDragSource()
    {
        if (_dragSource == null) return;

        _dragSource.MouseMove -= OnMouseMove;
        _dragSource.MouseUp -= OnMouseUp;
        _dragSource.LostMouseCapture -= OnLostCapture;
        _dragSource.KeyDown -= OnKeyDown;

        if (_dragSource.IsMouseCaptured)
            _dragSource.ReleaseMouseCapture();

        _dragSource = null;
    }

    /// <summary>
    /// Clean up all drag state, windows, and event handlers.
    /// </summary>
    private void CleanupDrag()
    {
        _state = DragState.Idle;

        // Unhook Win32 message filter and release Win32 capture
        if (_win32Hooked)
        {
            ComponentDispatcher.ThreadFilterMessage -= OnThreadFilterMessage;
            _win32Hooked = false;
            ReleaseCapture();
        }

        // Detach WPF handlers (for DragStarting phase, if still attached)
        DetachFromDragSource();

        // Close windows
        _previewWindow?.Close();
        _previewWindow = null;

        _guideOverlay?.HideOverlay();
        _guideOverlay?.Close();
        _guideOverlay = null;

        _currentTarget = null;
        _lastPaneModel = null;
        _lastPaneBounds = null;
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
