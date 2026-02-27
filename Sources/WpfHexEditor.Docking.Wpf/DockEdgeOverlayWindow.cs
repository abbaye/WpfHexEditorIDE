//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using WpfHexEditor.Docking.Core;

namespace WpfHexEditor.Docking.Wpf;

/// <summary>
/// Transparent overlay window showing 4 dock indicators at the edges of the entire dock area.
/// These indicators allow docking to the window edges (relative to MainDocumentHost).
/// Works alongside <see cref="DockOverlayWindow"/> which shows the per-panel compass.
/// </summary>
public class DockEdgeOverlayWindow : Window
{
    private readonly Canvas _canvas;
    private DockDirection? _highlightedDirection;

    private const double IndicatorSize = 36.0;
    private const double EdgeMargin = 10.0;

    public DockDirection? HighlightedDirection
    {
        get => _highlightedDirection;
        set
        {
            if (_highlightedDirection != value)
            {
                _highlightedDirection = value;
                Rebuild();
            }
        }
    }

    public DockEdgeOverlayWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        IsHitTestVisible = false;

        _canvas = new Canvas();
        Content = _canvas;
    }

    /// <summary>
    /// Shows the overlay positioned over the given target element (the entire CenterHost).
    /// </summary>
    public void ShowOverTarget(UIElement target)
    {
        var targetPos = target.PointToScreen(new Point(0, 0));
        var targetSize = target.RenderSize;

        // PointToScreen returns physical pixels; Window.Left/Top need DIPs
        var source = PresentationSource.FromVisual(target);
        if (source?.CompositionTarget != null)
            targetPos = source.CompositionTarget.TransformFromDevice.Transform(targetPos);

        Left = targetPos.X;
        Top = targetPos.Y;
        Width = targetSize.Width;
        Height = targetSize.Height;

        Rebuild();
        Show();
    }

    /// <summary>
    /// Performs hit-testing for edge indicators. Returns Left/Right/Top/Bottom or null.
    /// Input screenPoint must be in physical screen pixels (from PointToScreen).
    /// </summary>
    public DockDirection? HitTest(Point screenPoint)
    {
        // Convert from physical pixels to DIPs
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget != null)
            screenPoint = source.CompositionTarget.TransformFromDevice.Transform(screenPoint);

        var localPoint = new Point(screenPoint.X - Left, screenPoint.Y - Top);

        var zones = new Dictionary<DockDirection, Rect>
        {
            [DockDirection.Left] = new(EdgeMargin, Height / 2 - IndicatorSize / 2, IndicatorSize, IndicatorSize),
            [DockDirection.Right] = new(Width - EdgeMargin - IndicatorSize, Height / 2 - IndicatorSize / 2, IndicatorSize, IndicatorSize),
            [DockDirection.Top] = new(Width / 2 - IndicatorSize / 2, EdgeMargin, IndicatorSize, IndicatorSize),
            [DockDirection.Bottom] = new(Width / 2 - IndicatorSize / 2, Height - EdgeMargin - IndicatorSize, IndicatorSize, IndicatorSize),
        };

        foreach (var (direction, zone) in zones)
        {
            if (zone.Contains(localPoint))
                return direction;
        }

        return null;
    }

    private void Rebuild()
    {
        _canvas.Children.Clear();

        // Draw preview zone if a direction is highlighted
        if (_highlightedDirection.HasValue)
            DrawPreviewZone(_highlightedDirection.Value);

        // Draw 4 edge indicators
        AddEdgeIndicator(DockDirection.Left, EdgeMargin, Height / 2 - IndicatorSize / 2);
        AddEdgeIndicator(DockDirection.Right, Width - EdgeMargin - IndicatorSize, Height / 2 - IndicatorSize / 2);
        AddEdgeIndicator(DockDirection.Top, Width / 2 - IndicatorSize / 2, EdgeMargin);
        AddEdgeIndicator(DockDirection.Bottom, Width / 2 - IndicatorSize / 2, Height - EdgeMargin - IndicatorSize);
    }

    /// <summary>
    /// Draws a semi-transparent rectangle showing where the panel will be placed (25% of the entire area).
    /// </summary>
    private void DrawPreviewZone(DockDirection direction)
    {
        double x = 0, y = 0, w = Width, h = Height;

        switch (direction)
        {
            case DockDirection.Left:
                w = Width * 0.25;
                break;
            case DockDirection.Right:
                x = Width * 0.75;
                w = Width * 0.25;
                break;
            case DockDirection.Top:
                h = Height * 0.25;
                break;
            case DockDirection.Bottom:
                y = Height * 0.75;
                h = Height * 0.25;
                break;
        }

        var preview = new Rectangle
        {
            Width = w,
            Height = h,
            Fill = new SolidColorBrush(Color.FromArgb(60, 0, 122, 204)),
            Stroke = new SolidColorBrush(Color.FromArgb(160, 0, 122, 204)),
            StrokeThickness = 2
        };

        Canvas.SetLeft(preview, x);
        Canvas.SetTop(preview, y);
        _canvas.Children.Add(preview);
    }

    /// <summary>
    /// Draws an edge indicator with a mini dock-icon showing a panel docked to that side.
    /// </summary>
    private void AddEdgeIndicator(DockDirection direction, double x, double y)
    {
        var isHighlighted = _highlightedDirection == direction;

        // Background rounded rectangle
        var rect = new Rectangle
        {
            Width = IndicatorSize,
            Height = IndicatorSize,
            Fill = isHighlighted
                ? new SolidColorBrush(Color.FromRgb(0, 122, 204))
                : new SolidColorBrush(Color.FromArgb(220, 45, 45, 48)),
            Stroke = isHighlighted
                ? Brushes.White
                : new SolidColorBrush(Color.FromArgb(200, 112, 112, 112)),
            StrokeThickness = isHighlighted ? 2 : 1,
            RadiusX = 4,
            RadiusY = 4
        };

        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
        _canvas.Children.Add(rect);

        // Draw mini dock-icon: a window frame with a highlighted strip on the dock side
        const double iconMargin = 6;
        const double iconSize = IndicatorSize - iconMargin * 2; // 24x24

        // Window frame (outer border)
        var frame = new Rectangle
        {
            Width = iconSize,
            Height = iconSize,
            Fill = Brushes.Transparent,
            Stroke = isHighlighted ? Brushes.White : new SolidColorBrush(Color.FromRgb(180, 180, 180)),
            StrokeThickness = 1
        };
        Canvas.SetLeft(frame, x + iconMargin);
        Canvas.SetTop(frame, y + iconMargin);
        _canvas.Children.Add(frame);

        // Dock strip (filled portion showing where the panel docks)
        double sx = x + iconMargin, sy = y + iconMargin, sw = iconSize, sh = iconSize;
        switch (direction)
        {
            case DockDirection.Left:
                sw = iconSize * 0.3;
                break;
            case DockDirection.Right:
                sx += iconSize * 0.7;
                sw = iconSize * 0.3;
                break;
            case DockDirection.Top:
                sh = iconSize * 0.3;
                break;
            case DockDirection.Bottom:
                sy += iconSize * 0.7;
                sh = iconSize * 0.3;
                break;
        }

        var strip = new Rectangle
        {
            Width = sw,
            Height = sh,
            Fill = isHighlighted
                ? new SolidColorBrush(Color.FromArgb(180, 255, 255, 255))
                : new SolidColorBrush(Color.FromArgb(160, 180, 180, 180))
        };
        Canvas.SetLeft(strip, sx);
        Canvas.SetTop(strip, sy);
        _canvas.Children.Add(strip);
    }
}
