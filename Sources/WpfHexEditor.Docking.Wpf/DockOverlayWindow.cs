//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Opus 4.6, Claude Sonnet 4.6
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
/// All visual elements are pre-created in the constructor; <see cref="Rebuild"/> only updates
/// positions and appearance properties for better performance during drag operations.
/// </summary>
public class DockOverlayWindow : Window
{
    private readonly Canvas _canvas;
    private DockDirection? _highlightedDirection;

    private const double IndicatorSize = 36.0;
    private const double IndicatorOffset = 44.0;

    // Pre-created visual elements (reused across Rebuild calls)
    private readonly Rectangle _previewZone;
    private readonly Rectangle _hBar;
    private readonly Rectangle _vBar;
    private readonly Dictionary<DockDirection, Rectangle> _indicatorBgs = new();
    private readonly Dictionary<DockDirection, TextBlock>  _indicatorTexts = new();

    // Shared frozen brushes (avoid per-frame allocations)
    private static readonly Brush HighlightFill  = Freeze(new SolidColorBrush(Color.FromRgb(0, 122, 204)));
    private static readonly Brush NormalFill      = Freeze(new SolidColorBrush(Color.FromArgb(220, 45, 45, 48)));
    private static readonly Brush HighlightStroke = Brushes.White;
    private static readonly Brush NormalStroke    = Freeze(new SolidColorBrush(Color.FromArgb(200, 112, 112, 112)));
    private static readonly Brush CrossFill      = Freeze(new SolidColorBrush(Color.FromArgb(220, 45, 45, 48)));
    private static readonly Brush CrossStroke    = Freeze(new SolidColorBrush(Color.FromArgb(200, 63, 63, 70)));
    private static readonly Brush PreviewFill    = Freeze(new SolidColorBrush(Color.FromArgb(60, 0, 122, 204)));
    private static readonly Brush PreviewStroke  = Freeze(new SolidColorBrush(Color.FromArgb(160, 0, 122, 204)));

    private static Brush Freeze(SolidColorBrush brush) { brush.Freeze(); return brush; }

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

        // Pre-create the preview zone (hidden by default)
        _previewZone = new Rectangle
        {
            Fill            = PreviewFill,
            Stroke          = PreviewStroke,
            StrokeThickness = 2,
            Visibility      = Visibility.Collapsed
        };
        _canvas.Children.Add(_previewZone);

        // Pre-create cross background bars
        _hBar = new Rectangle { Fill = CrossFill, Stroke = CrossStroke, StrokeThickness = 1, RadiusX = 4, RadiusY = 4 };
        _vBar = new Rectangle { Fill = CrossFill, Stroke = CrossStroke, StrokeThickness = 1, RadiusX = 4, RadiusY = 4 };
        _canvas.Children.Add(_hBar);
        _canvas.Children.Add(_vBar);

        // Pre-create 5 indicator backgrounds + text labels
        var directions = new[] { DockDirection.Center, DockDirection.Left, DockDirection.Right, DockDirection.Top, DockDirection.Bottom };
        var arrows = new Dictionary<DockDirection, string>
        {
            [DockDirection.Left]   = "\u25C0",
            [DockDirection.Right]  = "\u25B6",
            [DockDirection.Top]    = "\u25B2",
            [DockDirection.Bottom] = "\u25BC",
            [DockDirection.Center] = "\u25A0"
        };

        foreach (var dir in directions)
        {
            var bg = new Rectangle
            {
                Width           = IndicatorSize,
                Height          = IndicatorSize,
                Fill            = NormalFill,
                Stroke          = NormalStroke,
                StrokeThickness = 1,
                RadiusX         = 4,
                RadiusY         = 4
            };
            _indicatorBgs[dir] = bg;
            _canvas.Children.Add(bg);

            var text = new TextBlock
            {
                Text                = arrows[dir],
                Foreground          = Brushes.White,
                FontSize            = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            };
            text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            _indicatorTexts[dir] = text;
            _canvas.Children.Add(text);
        }
    }

    /// <summary>
    /// Shows the overlay positioned over the given target element (the entire dock area).
    /// </summary>
    public void ShowOverTarget(UIElement target)
    {
        var targetPos = target.PointToScreen(new Point(0, 0));
        var targetSize = target.RenderSize;

        // Use per-monitor DPI for the target's screen position (more accurate on multi-monitor)
        var dipPos = DpiHelper.ScreenToDipForPoint(targetPos);

        Left = dipPos.X;
        Top = dipPos.Y;
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
        // Convert from physical pixels to DIPs using the monitor at the cursor position
        screenPoint = DpiHelper.ScreenToDipForPoint(screenPoint);

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

    /// <summary>
    /// Updates positions and visual properties of all pre-created elements.
    /// No elements are created or destroyed — only repositioned and restyled.
    /// </summary>
    private void Rebuild()
    {
        var centerX = Width / 2;
        var centerY = Height / 2;

        // --- Preview zone ---
        if (_highlightedDirection.HasValue)
        {
            double x = 0, y = 0, w = Width, h = Height;
            switch (_highlightedDirection.Value)
            {
                case DockDirection.Left:   w = Width * 0.25; break;
                case DockDirection.Right:  x = Width * 0.75; w = Width * 0.25; break;
                case DockDirection.Top:    h = Height * 0.25; break;
                case DockDirection.Bottom: y = Height * 0.75; h = Height * 0.25; break;
            }
            _previewZone.Width  = w;
            _previewZone.Height = h;
            Canvas.SetLeft(_previewZone, x);
            Canvas.SetTop(_previewZone, y);
            _previewZone.Visibility = Visibility.Visible;
        }
        else
        {
            _previewZone.Visibility = Visibility.Collapsed;
        }

        // --- Cross background bars ---
        var leftX   = centerX - IndicatorOffset - IndicatorSize;
        var rightX  = centerX + IndicatorOffset;
        var topY    = centerY - IndicatorOffset - IndicatorSize;
        var bottomY = centerY + IndicatorOffset;

        _hBar.Width  = rightX + IndicatorSize - leftX;
        _hBar.Height = IndicatorSize;
        Canvas.SetLeft(_hBar, leftX);
        Canvas.SetTop(_hBar, centerY - IndicatorSize / 2);

        _vBar.Width  = IndicatorSize;
        _vBar.Height = bottomY + IndicatorSize - topY;
        Canvas.SetLeft(_vBar, centerX - IndicatorSize / 2);
        Canvas.SetTop(_vBar, topY);

        // --- Indicator positions ---
        var positions = new Dictionary<DockDirection, Point>
        {
            [DockDirection.Center] = new(centerX - IndicatorSize / 2, centerY - IndicatorSize / 2),
            [DockDirection.Left]   = new(leftX, centerY - IndicatorSize / 2),
            [DockDirection.Right]  = new(rightX, centerY - IndicatorSize / 2),
            [DockDirection.Top]    = new(centerX - IndicatorSize / 2, topY),
            [DockDirection.Bottom] = new(centerX - IndicatorSize / 2, bottomY)
        };

        foreach (var (dir, pos) in positions)
        {
            var isHighlighted = _highlightedDirection == dir;
            var bg = _indicatorBgs[dir];

            bg.Fill            = isHighlighted ? HighlightFill : NormalFill;
            bg.Stroke          = isHighlighted ? HighlightStroke : NormalStroke;
            bg.StrokeThickness = isHighlighted ? 2 : 1;
            Canvas.SetLeft(bg, pos.X);
            Canvas.SetTop(bg, pos.Y);

            var text = _indicatorTexts[dir];
            Canvas.SetLeft(text, pos.X + (IndicatorSize - text.DesiredSize.Width)  / 2);
            Canvas.SetTop(text,  pos.Y + (IndicatorSize - text.DesiredSize.Height) / 2);
        }
    }
}
