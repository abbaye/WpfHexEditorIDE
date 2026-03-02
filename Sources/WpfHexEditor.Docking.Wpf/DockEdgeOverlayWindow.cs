//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6, Claude Sonnet 4.6
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
/// All visual elements are pre-created in the constructor; <see cref="Rebuild"/> only updates
/// positions and appearance properties for better performance during drag operations.
/// </summary>
public class DockEdgeOverlayWindow : Window
{
    private readonly Canvas _canvas;
    private DockDirection? _highlightedDirection;

    private const double IndicatorSize = 36.0;
    private const double EdgeMargin = 10.0;

    // Pre-created visual elements
    private readonly Rectangle _previewZone;
    private readonly Dictionary<DockDirection, Rectangle> _indicatorBgs   = new();
    private readonly Dictionary<DockDirection, Rectangle> _iconFrames     = new();
    private readonly Dictionary<DockDirection, Rectangle> _iconStrips     = new();

    // Shared frozen brushes
    private static readonly Brush HighlightFill     = Freeze(new SolidColorBrush(Color.FromRgb(0, 122, 204)));
    private static readonly Brush NormalFill         = Freeze(new SolidColorBrush(Color.FromArgb(220, 45, 45, 48)));
    private static readonly Brush HighlightStroke   = Brushes.White;
    private static readonly Brush NormalStroke       = Freeze(new SolidColorBrush(Color.FromArgb(200, 112, 112, 112)));
    private static readonly Brush PreviewFill       = Freeze(new SolidColorBrush(Color.FromArgb(60, 0, 122, 204)));
    private static readonly Brush PreviewStroke     = Freeze(new SolidColorBrush(Color.FromArgb(160, 0, 122, 204)));
    private static readonly Brush FrameStrokeNormal = Freeze(new SolidColorBrush(Color.FromRgb(180, 180, 180)));
    private static readonly Brush StripFillNormal   = Freeze(new SolidColorBrush(Color.FromArgb(160, 180, 180, 180)));
    private static readonly Brush StripFillHighlight = Freeze(new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)));

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

        // Pre-create preview zone
        _previewZone = new Rectangle
        {
            Fill            = PreviewFill,
            Stroke          = PreviewStroke,
            StrokeThickness = 2,
            Visibility      = Visibility.Collapsed
        };
        _canvas.Children.Add(_previewZone);

        // Pre-create 4 edge indicators (bg + frame + strip)
        var directions = new[] { DockDirection.Left, DockDirection.Right, DockDirection.Top, DockDirection.Bottom };

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

            const double iconMargin = 6;
            const double iconSize   = IndicatorSize - iconMargin * 2;

            var frame = new Rectangle
            {
                Width           = iconSize,
                Height          = iconSize,
                Fill            = Brushes.Transparent,
                Stroke          = FrameStrokeNormal,
                StrokeThickness = 1
            };
            _iconFrames[dir] = frame;
            _canvas.Children.Add(frame);

            var strip = new Rectangle { Fill = StripFillNormal };
            _iconStrips[dir] = strip;
            _canvas.Children.Add(strip);
        }
    }

    /// <summary>
    /// Shows the overlay positioned over the given target element (the entire CenterHost).
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
    /// Performs hit-testing for edge indicators. Returns Left/Right/Top/Bottom or null.
    /// Input screenPoint must be in physical screen pixels (from PointToScreen).
    /// </summary>
    public DockDirection? HitTest(Point screenPoint)
    {
        // Convert from physical pixels to DIPs using the monitor at the cursor position
        screenPoint = DpiHelper.ScreenToDipForPoint(screenPoint);

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

    /// <summary>
    /// Updates positions and visual properties of all pre-created elements.
    /// No elements are created or destroyed — only repositioned and restyled.
    /// </summary>
    private void Rebuild()
    {
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

        // --- Edge indicator positions ---
        var positions = new Dictionary<DockDirection, Point>
        {
            [DockDirection.Left]   = new(EdgeMargin, Height / 2 - IndicatorSize / 2),
            [DockDirection.Right]  = new(Width - EdgeMargin - IndicatorSize, Height / 2 - IndicatorSize / 2),
            [DockDirection.Top]    = new(Width / 2 - IndicatorSize / 2, EdgeMargin),
            [DockDirection.Bottom] = new(Width / 2 - IndicatorSize / 2, Height - EdgeMargin - IndicatorSize)
        };

        const double iconMargin = 6;
        const double iconSize   = IndicatorSize - iconMargin * 2;

        foreach (var (dir, pos) in positions)
        {
            var isHighlighted = _highlightedDirection == dir;

            // Background
            var bg = _indicatorBgs[dir];
            bg.Fill            = isHighlighted ? HighlightFill : NormalFill;
            bg.Stroke          = isHighlighted ? HighlightStroke : NormalStroke;
            bg.StrokeThickness = isHighlighted ? 2 : 1;
            Canvas.SetLeft(bg, pos.X);
            Canvas.SetTop(bg, pos.Y);

            // Icon frame
            var frame = _iconFrames[dir];
            frame.Stroke = isHighlighted ? HighlightStroke : FrameStrokeNormal;
            Canvas.SetLeft(frame, pos.X + iconMargin);
            Canvas.SetTop(frame, pos.Y + iconMargin);

            // Dock strip (filled portion showing where the panel docks)
            double sx = pos.X + iconMargin, sy = pos.Y + iconMargin, sw = iconSize, sh = iconSize;
            switch (dir)
            {
                case DockDirection.Left:   sw = iconSize * 0.3; break;
                case DockDirection.Right:  sx += iconSize * 0.7; sw = iconSize * 0.3; break;
                case DockDirection.Top:    sh = iconSize * 0.3; break;
                case DockDirection.Bottom: sy += iconSize * 0.7; sh = iconSize * 0.3; break;
            }

            var strip = _iconStrips[dir];
            strip.Width  = sw;
            strip.Height = sh;
            strip.Fill   = isHighlighted ? StripFillHighlight : StripFillNormal;
            Canvas.SetLeft(strip, sx);
            Canvas.SetTop(strip, sy);
        }
    }
}
