//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using WpfHexEditor.Docking.Core;

namespace WpfHexEditor.Docking.Wpf;

/// <summary>
/// Transparent overlay window that shows a 5-indicator compass rose (Center, Left, Right, Top, Bottom)
/// and a preview zone during a drag operation. Positioned over the specific panel being hovered.
/// Works alongside <see cref="DockEdgeOverlayWindow"/> which shows edge indicators.
/// </summary>
public class DockOverlayWindow : Window
{
    private readonly Canvas _canvas;
    private DockDirection? _highlightedDirection;

    private const double IndicatorSize = 36.0;
    private const double IndicatorOffset = 44.0;

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

    public DockOverlayWindow()
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
    /// Shows the overlay positioned over the given target element (the entire dock area).
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
    /// Performs hit-testing to determine which dock direction the mouse is over.
    /// Input screenPoint must be in physical screen pixels (from PointToScreen).
    /// </summary>
    public DockDirection? HitTest(Point screenPoint)
    {
        // Convert from physical pixels to DIPs to match Left/Top/Width/Height
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget != null)
            screenPoint = source.CompositionTarget.TransformFromDevice.Transform(screenPoint);

        var localPoint = new Point(screenPoint.X - Left, screenPoint.Y - Top);
        var centerX = Width / 2;
        var centerY = Height / 2;

        var zones = new Dictionary<DockDirection, Rect>
        {
            [DockDirection.Center] = new(centerX - IndicatorSize / 2, centerY - IndicatorSize / 2, IndicatorSize, IndicatorSize),
            [DockDirection.Left] = new(centerX - IndicatorOffset - IndicatorSize, centerY - IndicatorSize / 2, IndicatorSize, IndicatorSize),
            [DockDirection.Right] = new(centerX + IndicatorOffset, centerY - IndicatorSize / 2, IndicatorSize, IndicatorSize),
            [DockDirection.Top] = new(centerX - IndicatorSize / 2, centerY - IndicatorOffset - IndicatorSize, IndicatorSize, IndicatorSize),
            [DockDirection.Bottom] = new(centerX - IndicatorSize / 2, centerY + IndicatorOffset, IndicatorSize, IndicatorSize),
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

        // Compute indicator positions
        var centerX = Width / 2;
        var centerY = Height / 2;

        var leftX = centerX - IndicatorOffset - IndicatorSize;
        var rightX = centerX + IndicatorOffset;
        var topY = centerY - IndicatorOffset - IndicatorSize;
        var bottomY = centerY + IndicatorOffset;

        // Draw cross-shaped background connecting the 5 indicators
        var crossFill = new SolidColorBrush(Color.FromArgb(220, 45, 45, 48));
        var crossStroke = new SolidColorBrush(Color.FromArgb(200, 63, 63, 70));

        // Horizontal bar (left indicator to right indicator)
        var hBar = new Rectangle
        {
            Width = rightX + IndicatorSize - leftX,
            Height = IndicatorSize,
            Fill = crossFill,
            Stroke = crossStroke,
            StrokeThickness = 1,
            RadiusX = 4,
            RadiusY = 4
        };
        Canvas.SetLeft(hBar, leftX);
        Canvas.SetTop(hBar, centerY - IndicatorSize / 2);
        _canvas.Children.Add(hBar);

        // Vertical bar (top indicator to bottom indicator)
        var vBar = new Rectangle
        {
            Width = IndicatorSize,
            Height = bottomY + IndicatorSize - topY,
            Fill = crossFill,
            Stroke = crossStroke,
            StrokeThickness = 1,
            RadiusX = 4,
            RadiusY = 4
        };
        Canvas.SetLeft(vBar, centerX - IndicatorSize / 2);
        Canvas.SetTop(vBar, topY);
        _canvas.Children.Add(vBar);

        // Draw 5 compass indicators on top of the cross
        AddIndicator(DockDirection.Center, centerX - IndicatorSize / 2, centerY - IndicatorSize / 2);
        AddIndicator(DockDirection.Left, leftX, centerY - IndicatorSize / 2);
        AddIndicator(DockDirection.Right, rightX, centerY - IndicatorSize / 2);
        AddIndicator(DockDirection.Top, centerX - IndicatorSize / 2, topY);
        AddIndicator(DockDirection.Bottom, centerX - IndicatorSize / 2, bottomY);
    }

    /// <summary>
    /// Draws a semi-transparent rectangle showing where the panel will be placed.
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
            case DockDirection.Center:
                // Full area with lighter overlay
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

    private void AddIndicator(DockDirection direction, double x, double y)
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

        // Arrow symbol
        var arrow = direction switch
        {
            DockDirection.Left => "\u25C0",
            DockDirection.Right => "\u25B6",
            DockDirection.Top => "\u25B2",
            DockDirection.Bottom => "\u25BC",
            DockDirection.Center => "\u25A0",
            _ => ""
        };

        var text = new TextBlock
        {
            Text = arrow,
            Foreground = Brushes.White,
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(text, x + (IndicatorSize - text.DesiredSize.Width) / 2);
        Canvas.SetTop(text, y + (IndicatorSize - text.DesiredSize.Height) / 2);
        _canvas.Children.Add(text);
    }
}
