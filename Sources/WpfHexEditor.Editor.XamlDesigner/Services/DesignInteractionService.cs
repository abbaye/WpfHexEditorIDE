// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: DesignInteractionService.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     Orchestrates drag-move and resize interactions on the XAML design canvas.
//     Tracks drag state, computes attribute deltas, and produces DesignOperations
//     that are applied via DesignToXamlSyncService.
//
// Architecture Notes:
//     Service pattern (stateful per-session, one instance per split host).
//     Strategy pattern — move vs resize handled by distinct code paths.
//     Raises OperationCommitted event; the split host applies the XAML patch
//     and pushes the DesignOperation onto the undo stack.
// ==========================================================

using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfHexEditor.Editor.XamlDesigner.Models;

namespace WpfHexEditor.Editor.XamlDesigner.Services;

/// <summary>
/// Manages interactive drag-move and resize on the XAML design canvas.
/// </summary>
public sealed class DesignInteractionService
{
    // ── State ─────────────────────────────────────────────────────────────────

    private Point  _moveStartPoint;
    private Thickness _startMargin;
    private double _startWidth;
    private double _startHeight;
    private double _startCanvasLeft;
    private double _startCanvasTop;
    private bool   _isInMove;
    private int    _activeUid = -1;

    // Rotation state
    private bool   _isInRotate;
    private double _rotateStartAngle;
    private int    _rotateUid = -1;

    // Multi-element move state
    private Point _multiMoveStart;
    private readonly Dictionary<int, (Thickness StartMargin, double StartLeft, double StartTop)> _multiMoveStates = new();

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Raised when a design operation is completed and ready to be applied.
    /// The handler should call DesignToXamlSyncService.PatchElement and
    /// push the operation onto the undo stack.
    /// </summary>
    public event EventHandler<DesignOperationCommittedEventArgs>? OperationCommitted;

    // ── Move API ──────────────────────────────────────────────────────────────

    /// <summary>Called when the user starts dragging an element to move it.</summary>
    public void OnMoveStart(FrameworkElement element, Point canvasPosition, int elementUid)
    {
        _moveStartPoint = canvasPosition;
        _startMargin    = element.Margin;
        _startWidth     = element.ActualWidth;
        _startHeight    = element.ActualHeight;
        _startCanvasLeft = Canvas.GetLeft(element);
        _startCanvasTop  = Canvas.GetTop(element);
        _isInMove        = true;
        _activeUid       = elementUid;
    }

    /// <summary>Called on each mouse move during a drag-move operation.</summary>
    public void OnMoveDelta(FrameworkElement element, Point canvasPosition)
    {
        if (!_isInMove) return;

        double dx = canvasPosition.X - _moveStartPoint.X;
        double dy = canvasPosition.Y - _moveStartPoint.Y;

        // Apply position change depending on parent panel type.
        if (element.Parent is Canvas)
        {
            double newLeft = (_startCanvasLeft is double.NaN ? 0 : _startCanvasLeft) + dx;
            double newTop  = (_startCanvasTop  is double.NaN ? 0 : _startCanvasTop)  + dy;
            Canvas.SetLeft(element, newLeft);
            Canvas.SetTop(element, newTop);
        }
        else
        {
            // Margin-based move (works for most panels).
            element.Margin = new Thickness(
                _startMargin.Left + dx,
                _startMargin.Top  + dy,
                _startMargin.Right,
                _startMargin.Bottom);
        }
    }

    /// <summary>Called when the user releases the mouse after a drag-move.</summary>
    public void OnMoveCompleted(FrameworkElement element)
    {
        if (!_isInMove) return;
        _isInMove = false;

        var before = new Dictionary<string, string?>();
        var after  = new Dictionary<string, string?>();

        if (element.Parent is Canvas)
        {
            double newLeft = Canvas.GetLeft(element);
            double newTop  = Canvas.GetTop(element);
            before["Canvas.Left"] = _startCanvasLeft.ToString(System.Globalization.CultureInfo.InvariantCulture);
            before["Canvas.Top"]  = _startCanvasTop.ToString(System.Globalization.CultureInfo.InvariantCulture);
            after["Canvas.Left"]  = newLeft.ToString(System.Globalization.CultureInfo.InvariantCulture);
            after["Canvas.Top"]   = newTop.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        else
        {
            before["Margin"] = ThicknessToString(_startMargin);
            after["Margin"]  = ThicknessToString(element.Margin);
        }

        if (IsSameState(before, after)) return;

        var op = DesignOperation.CreateMove(_activeUid, before, after);
        OperationCommitted?.Invoke(this, new DesignOperationCommittedEventArgs(op, element));
    }

    // ── Multi-element Move API ────────────────────────────────────────────────

    /// <summary>Starts a coordinated move for multiple elements.</summary>
    public void OnMultiMoveStart(IReadOnlyList<FrameworkElement> elements, Point canvasPosition)
    {
        _multiMoveStart = canvasPosition;
        _multiMoveStates.Clear();
        foreach (var el in elements)
        {
            int uid = GetUidFromTag(el);
            if (uid >= 0)
                _multiMoveStates[uid] = CaptureElementState(el);
        }
    }

    /// <summary>Called on each mouse move during a multi-element drag-move operation.</summary>
    public void OnMultiMoveDelta(IReadOnlyList<FrameworkElement> elements, Point canvasPosition)
    {
        if (_multiMoveStates.Count == 0) return;

        double dx = canvasPosition.X - _multiMoveStart.X;
        double dy = canvasPosition.Y - _multiMoveStart.Y;

        foreach (var el in elements)
        {
            int uid = GetUidFromTag(el);
            if (!_multiMoveStates.TryGetValue(uid, out var state)) continue;

            if (el.Parent is Canvas)
            {
                Canvas.SetLeft(el, (double.IsNaN(state.StartLeft)  ? 0 : state.StartLeft)  + dx);
                Canvas.SetTop(el,  (double.IsNaN(state.StartTop)   ? 0 : state.StartTop)   + dy);
            }
            else
            {
                el.Margin = new Thickness(
                    state.StartMargin.Left + dx,
                    state.StartMargin.Top  + dy,
                    state.StartMargin.Right,
                    state.StartMargin.Bottom);
            }
        }
    }

    /// <summary>Called when the user releases the mouse after a multi-element drag-move.</summary>
    public void OnMultiMoveCompleted(IReadOnlyList<FrameworkElement> elements)
    {
        if (_multiMoveStates.Count == 0) return;

        foreach (var el in elements)
        {
            int uid = GetUidFromTag(el);
            if (!_multiMoveStates.TryGetValue(uid, out var state)) continue;

            var before = new Dictionary<string, string?>();
            var after  = new Dictionary<string, string?>();

            if (el.Parent is Canvas)
            {
                before["Canvas.Left"] = DoubleToXaml(state.StartLeft);
                before["Canvas.Top"]  = DoubleToXaml(state.StartTop);
                after["Canvas.Left"]  = DoubleToXaml(Canvas.GetLeft(el));
                after["Canvas.Top"]   = DoubleToXaml(Canvas.GetTop(el));
            }
            else
            {
                before["Margin"] = ThicknessToString(state.StartMargin);
                after["Margin"]  = ThicknessToString(el.Margin);
            }

            if (IsSameState(before, after)) continue;

            var op = DesignOperation.CreateMove(uid, before, after);
            OperationCommitted?.Invoke(this, new DesignOperationCommittedEventArgs(op, el));
        }

        _multiMoveStates.Clear();
    }

    // ── Resize API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called when the user drags a resize handle (index 0–7, NW→N→NE→E→SE→S→SW→W).
    /// </summary>
    public void OnResizeDelta(FrameworkElement element, int handleIndex, double dx, double dy, int elementUid)
    {
        if (_activeUid < 0) _activeUid = elementUid;

        double w = element.ActualWidth;
        double h = element.ActualHeight;
        var    m = element.Margin;

        // Handle indices: 0=NW, 1=N, 2=NE, 3=E, 4=SE, 5=S, 6=SW, 7=W
        switch (handleIndex)
        {
            case 0: // NW — shrink/grow from top-left
                element.Width  = Math.Max(8, w - dx);
                element.Height = Math.Max(8, h - dy);
                element.Margin = new Thickness(m.Left + dx, m.Top + dy, m.Right, m.Bottom);
                break;
            case 1: // N — resize height from top
                element.Height = Math.Max(8, h - dy);
                element.Margin = new Thickness(m.Left, m.Top + dy, m.Right, m.Bottom);
                break;
            case 2: // NE — grow right, shrink/grow top
                element.Width  = Math.Max(8, w + dx);
                element.Height = Math.Max(8, h - dy);
                element.Margin = new Thickness(m.Left, m.Top + dy, m.Right, m.Bottom);
                break;
            case 3: // E — resize width from right
                element.Width  = Math.Max(8, w + dx);
                break;
            case 4: // SE — grow right and down
                element.Width  = Math.Max(8, w + dx);
                element.Height = Math.Max(8, h + dy);
                break;
            case 5: // S — resize height from bottom
                element.Height = Math.Max(8, h + dy);
                break;
            case 6: // SW — grow left and down
                element.Width  = Math.Max(8, w - dx);
                element.Height = Math.Max(8, h + dy);
                element.Margin = new Thickness(m.Left + dx, m.Top, m.Right, m.Bottom);
                break;
            case 7: // W — resize width from left
                element.Width  = Math.Max(8, w - dx);
                element.Margin = new Thickness(m.Left + dx, m.Top, m.Right, m.Bottom);
                break;
        }
    }

    /// <summary>Called when the user releases a resize handle.</summary>
    public void OnResizeStarted(FrameworkElement element, int elementUid)
    {
        _startWidth  = element.ActualWidth;
        _startHeight = element.ActualHeight;
        _startMargin = element.Margin;
        _activeUid   = elementUid;
    }

    /// <summary>Called when a resize drag is completed.</summary>
    public void OnResizeCompleted(FrameworkElement element)
    {
        if (_activeUid < 0) return;

        var before = new Dictionary<string, string?>();
        var after  = new Dictionary<string, string?>();

        before["Width"]  = DoubleToXaml(_startWidth);
        before["Height"] = DoubleToXaml(_startHeight);
        before["Margin"] = ThicknessToString(_startMargin);
        after["Width"]   = DoubleToXaml(element.Width);
        after["Height"]  = DoubleToXaml(element.Height);
        after["Margin"]  = ThicknessToString(element.Margin);

        if (IsSameState(before, after)) return;

        var op = DesignOperation.CreateResize(_activeUid, before, after);
        OperationCommitted?.Invoke(this, new DesignOperationCommittedEventArgs(op, element));
        _activeUid = -1;
    }

    // ── Rotation API ──────────────────────────────────────────────────────────

    /// <summary>Called when the user starts dragging the rotation handle.</summary>
    public void OnRotateStarted(FrameworkElement element, int elementUid)
    {
        _rotateStartAngle = GetCurrentRotation(element);
        _rotateUid        = elementUid;
        _isInRotate       = true;
    }

    /// <summary>Called on each mouse move during a rotation drag.</summary>
    public void OnRotateDelta(FrameworkElement element, double newAngle)
    {
        if (!_isInRotate) return;

        element.RenderTransformOrigin = new Point(0.5, 0.5);

        if (element.RenderTransform is RotateTransform rt)
            rt.Angle = newAngle;
        else
            element.RenderTransform = new RotateTransform(newAngle);
    }

    /// <summary>Called when the rotation drag is completed.</summary>
    public void OnRotateCompleted(FrameworkElement element)
    {
        if (!_isInRotate) return;
        _isInRotate = false;

        // Guard: if OnRotateStarted never ran (e.g. DragStarted lost to event routing),
        // _rotateUid is still -1 → PatchRotation would receive uid=-1 and return unchanged XAML.
        if (_rotateUid < 0) return;

        double after = GetCurrentRotation(element);
        if (Math.Abs(after - _rotateStartAngle) < 0.01) return; // no-op

        var op = DesignOperation.CreateRotate(
            _rotateUid,
            new Dictionary<string, string?> { ["Angle"] = _rotateStartAngle.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) },
            new Dictionary<string, string?> { ["Angle"] = after.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) });

        OperationCommitted?.Invoke(this, new DesignOperationCommittedEventArgs(op, element));
        _rotateUid = -1;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>Reads the current rotation angle from a FrameworkElement's RenderTransform.</summary>
    private static double GetCurrentRotation(FrameworkElement fe)
    {
        if (fe.RenderTransform is RotateTransform rt) return rt.Angle;
        if (fe.RenderTransform is TransformGroup tg)
            return tg.Children.OfType<RotateTransform>().FirstOrDefault()?.Angle ?? 0.0;
        return 0.0;
    }

    /// <summary>
    /// Extracts the numeric UID from an element's Tag string (format: "xd_N").
    /// Returns -1 when the tag is absent or malformed.
    /// </summary>
    private static int GetUidFromTag(FrameworkElement el)
    {
        if (el.Tag is not string tag) return -1;
        const string prefix = "xd_";
        if (!tag.StartsWith(prefix, StringComparison.Ordinal)) return -1;
        return int.TryParse(tag.AsSpan(prefix.Length), out int uid) ? uid : -1;
    }

    /// <summary>
    /// Snapshots the current position state of an element for later delta computation.
    /// </summary>
    private static (Thickness StartMargin, double StartLeft, double StartTop) CaptureElementState(
        FrameworkElement el)
        => (el.Margin, Canvas.GetLeft(el), Canvas.GetTop(el));

    private static string ThicknessToString(Thickness t) =>
        t.Left  == t.Right  && t.Left == t.Top && t.Left == t.Bottom
            ? t.Left.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : $"{t.Left},{t.Top},{t.Right},{t.Bottom}";

    private static string DoubleToXaml(double v) =>
        double.IsNaN(v) ? "Auto"
        : v.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static bool IsSameState(
        Dictionary<string, string?> before,
        Dictionary<string, string?> after)
    {
        foreach (var key in before.Keys)
        {
            if (!after.TryGetValue(key, out var afterVal)) return false;
            if (before[key] != afterVal) return false;
        }
        return true;
    }
}

/// <summary>
/// Event arguments for a committed design operation.
/// </summary>
public sealed class DesignOperationCommittedEventArgs(
    DesignOperation operation,
    FrameworkElement element) : EventArgs
{
    public DesignOperation   Operation { get; } = operation;
    public FrameworkElement  Element   { get; } = element;
}
