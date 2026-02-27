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
/// Transparent overlay window that shows dock direction indicators (compass rose)
/// during a drag operation.
/// </summary>
public class DockOverlayWindow : Window
{
    private readonly Canvas _canvas;
    private readonly Dictionary<DockDirection, Path> _indicators = new();
    private DockDirection? _highlightedDirection;

    public DockDirection? HighlightedDirection
    {
        get => _highlightedDirection;
        set
        {
            _highlightedDirection = value;
            UpdateHighlights();
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
    /// Shows the overlay centered on the given target element.
    /// </summary>
    public void ShowOverTarget(UIElement target)
    {
        var targetPos = target.PointToScreen(new Point(0, 0));
        var targetSize = target.RenderSize;

        Left = targetPos.X;
        Top = targetPos.Y;
        Width = targetSize.Width;
        Height = targetSize.Height;

        BuildIndicators();
        Show();
    }

    /// <summary>
    /// Performs hit-testing to determine which dock direction the mouse is over.
    /// </summary>
    public DockDirection? HitTest(Point screenPoint)
    {
        var localPoint = new Point(screenPoint.X - Left, screenPoint.Y - Top);
        var centerX = Width / 2;
        var centerY = Height / 2;
        var indicatorSize = 32.0;
        var offset = 40.0;

        var zones = new Dictionary<DockDirection, Rect>
        {
            [DockDirection.Center] = new(centerX - indicatorSize / 2, centerY - indicatorSize / 2, indicatorSize, indicatorSize),
            [DockDirection.Left] = new(centerX - offset - indicatorSize, centerY - indicatorSize / 2, indicatorSize, indicatorSize),
            [DockDirection.Right] = new(centerX + offset, centerY - indicatorSize / 2, indicatorSize, indicatorSize),
            [DockDirection.Top] = new(centerX - indicatorSize / 2, centerY - offset - indicatorSize, indicatorSize, indicatorSize),
            [DockDirection.Bottom] = new(centerX - indicatorSize / 2, centerY + offset, indicatorSize, indicatorSize),
        };

        foreach (var (direction, zone) in zones)
        {
            if (zone.Contains(localPoint))
                return direction;
        }

        return null;
    }

    private void BuildIndicators()
    {
        _canvas.Children.Clear();
        _indicators.Clear();

        var centerX = Width / 2;
        var centerY = Height / 2;
        var size = 32.0;
        var offset = 40.0;

        AddIndicator(DockDirection.Center, centerX - size / 2, centerY - size / 2, size);
        AddIndicator(DockDirection.Left, centerX - offset - size, centerY - size / 2, size);
        AddIndicator(DockDirection.Right, centerX + offset, centerY - size / 2, size);
        AddIndicator(DockDirection.Top, centerX - size / 2, centerY - offset - size, size);
        AddIndicator(DockDirection.Bottom, centerX - size / 2, centerY + offset, size);
    }

    private void AddIndicator(DockDirection direction, double x, double y, double size)
    {
        var isHighlighted = _highlightedDirection == direction;
        var rect = new Rectangle
        {
            Width = size,
            Height = size,
            Fill = isHighlighted
                ? new SolidColorBrush(Color.FromArgb(240, 0, 150, 255))
                : new SolidColorBrush(Color.FromArgb(140, 0, 122, 204)),
            Stroke = isHighlighted ? Brushes.Yellow : Brushes.White,
            StrokeThickness = isHighlighted ? 3 : 2,
            RadiusX = 4,
            RadiusY = 4
        };

        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
        _canvas.Children.Add(rect);

        // Add direction arrow text
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
        Canvas.SetLeft(text, x + (size - text.DesiredSize.Width) / 2);
        Canvas.SetTop(text, y + (size - text.DesiredSize.Height) / 2);
        _canvas.Children.Add(text);
    }

    private void UpdateHighlights()
    {
        // Rebuild with highlight state
        BuildIndicators();
    }
}
