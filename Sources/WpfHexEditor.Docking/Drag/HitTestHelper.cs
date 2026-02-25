// Apache 2.0 - 2026
// WpfHexEditor.Docking - VS-Like Docking Framework

using System.Windows;
using System.Windows.Media;
using WpfHexEditor.Docking.Controls;
using WpfHexEditor.Docking.Model;

namespace WpfHexEditor.Docking.Drag;

/// <summary>
/// Utility for finding dock target controls under a screen point.
/// </summary>
internal static class HitTestHelper
{
    /// <summary>
    /// Find the LayoutAnchorablePaneControl or LayoutDocumentPaneControl under a screen point.
    /// </summary>
    public static FrameworkElement? FindPaneControlAtPoint(DockManager manager, Point screenPoint)
    {
        var localPoint = manager.PointFromScreen(screenPoint);
        FrameworkElement? result = null;

        VisualTreeHelper.HitTest(manager, null, hitResult =>
        {
            if (hitResult.VisualHit is FrameworkElement fe)
            {
                // Walk up visual tree to find a pane control
                var current = fe;
                while (current != null)
                {
                    if (current is LayoutAnchorablePaneControl or LayoutDocumentPaneControl)
                    {
                        result = current;
                        return HitTestResultBehavior.Stop;
                    }
                    current = VisualTreeHelper.GetParent(current) as FrameworkElement;
                }
            }
            return HitTestResultBehavior.Continue;
        }, new PointHitTestParameters(localPoint));

        return result;
    }

    /// <summary>
    /// Get the model element from a pane control.
    /// </summary>
    public static LayoutElement? GetModelFromControl(FrameworkElement control) => control switch
    {
        LayoutAnchorablePaneControl apc => apc.Model,
        LayoutDocumentPaneControl dpc => dpc.Model,
        _ => null
    };

    /// <summary>
    /// Calculate the screen bounds of a framework element.
    /// </summary>
    public static Rect GetScreenBounds(FrameworkElement element)
    {
        var topLeft = element.PointToScreen(new Point(0, 0));
        return new Rect(topLeft, new Size(element.ActualWidth, element.ActualHeight));
    }

    /// <summary>
    /// Determine which dock side a point falls in relative to a rectangle.
    /// Divides the rect into 5 zones: center and 4 edges (each 25% of width/height).
    /// </summary>
    public static DockSide GetDockSideFromPoint(Point point, Rect bounds)
    {
        var relX = (point.X - bounds.X) / bounds.Width;
        var relY = (point.Y - bounds.Y) / bounds.Height;

        const double edgeThreshold = 0.25;

        if (relX < edgeThreshold) return DockSide.Left;
        if (relX > 1 - edgeThreshold) return DockSide.Right;
        if (relY < edgeThreshold) return DockSide.Top;
        if (relY > 1 - edgeThreshold) return DockSide.Bottom;
        return DockSide.None; // Center = tab into
    }
}
