// ==========================================================
// Project: WpfHexEditor.Shell
// File: DockEdgeOverlayWindow.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     Transparent overlay window showing dock indicators at the edges of the
//     entire dock area (relative to the CenterHost). Supports two sets of
//     indicators (VS2022-style):
//       - Outer indicators: dock at the layout root level (full-width / full-height)
//       - Inner indicators: dock adjacent to the main document host (between side panels)
//     Inner indicators are shown only when InnerBounds differs from the full overlay
//     (i.e., side panels are present). HitTestEx() distinguishes inner vs outer hits.
//
// Architecture Notes:
//     All visual elements are pre-created in the constructor; Rebuild() only updates
//     positions and appearance for better performance during drag operations.
//     Works alongside DockOverlayWindow (per-panel compass) managed by DockDragManager.
//
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using WpfHexEditor.Docking.Core;

namespace WpfHexEditor.Shell;

/// <summary>
/// Transparent overlay window showing dock indicators at the edges of the entire dock area.
/// Supports two indicator sets (VS2022-style): outer (full-width/height) and inner (between
/// side panels). <see cref="HitTestEx"/> distinguishes which set was hit.
/// </summary>
public class DockEdgeOverlayWindow : Window
{
    // --- Constants -----------------------------------------------------------

    /// Size of the outer (full-width / full-height) edge indicators in DIPs.
    private const double OuterSize = 36.0;

    /// Size of the inner (between-side-panels) edge indicators in DIPs.
    private const double InnerSize = 28.0;

    private const double EdgeMargin = 10.0;

    /// Minimum pixel difference between inner and full bounds to show inner indicators.
    private const double InnerThreshold = 20.0;

    // --- Visual state --------------------------------------------------------

    private readonly Canvas _canvas;

    /// Preview zone rectangle drawn inside the overlay.
    private readonly Rectangle _previewZone;

    // Outer indicator visuals (one set of 4 directions)
    private readonly Dictionary<DockDirection, Rectangle> _outerBgs    = new();
    private readonly Dictionary<DockDirection, Rectangle> _outerFrames = new();
    private readonly Dictionary<DockDirection, Rectangle> _outerStrips = new();

    // Inner indicator visuals (one set of 4 directions, shown only when InnerBounds is set)
    private readonly Dictionary<DockDirection, Rectangle> _innerBgs    = new();
    private readonly Dictionary<DockDirection, Rectangle> _innerFrames = new();
    private readonly Dictionary<DockDirection, Rectangle> _innerStrips = new();

    // --- State ---------------------------------------------------------------

    private DockDirection? _highlightedDirection;
    private bool           _highlightIsOuter;
    private Rect           _innerBounds = Rect.Empty;

    // --- Shared frozen brushes -----------------------------------------------

    private static readonly Brush HighlightFill      = Freeze(new SolidColorBrush(Color.FromRgb(0, 122, 204)));
    private static readonly Brush NormalFill          = Freeze(new SolidColorBrush(Color.FromArgb(220, 45, 45, 48)));
    private static readonly Brush HighlightStroke     = Brushes.White;
    private static readonly Brush NormalStroke        = Freeze(new SolidColorBrush(Color.FromArgb(200, 112, 112, 112)));
    private static readonly Brush PreviewFill         = Freeze(new SolidColorBrush(Color.FromArgb(60, 0, 122, 204)));
    private static readonly Brush PreviewStroke       = Freeze(new SolidColorBrush(Color.FromArgb(160, 0, 122, 204)));
    private static readonly Brush FrameStrokeNormal   = Freeze(new SolidColorBrush(Color.FromRgb(180, 180, 180)));
    private static readonly Brush StripFillNormal     = Freeze(new SolidColorBrush(Color.FromArgb(160, 180, 180, 180)));
    private static readonly Brush StripFillHighlight  = Freeze(new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)));

    private static Brush Freeze(SolidColorBrush b) { b.Freeze(); return b; }

    // --- Properties ----------------------------------------------------------

    /// <summary>
    /// The direction that is currently highlighted (outer or inner, depending on <see cref="IsHighlightOuter"/>).
    /// Setting triggers a <see cref="Rebuild"/>.
    /// </summary>
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

    /// <summary>
    /// Whether the current highlight belongs to the outer indicator set.
    /// Must be set together with <see cref="HighlightedDirection"/>.
    /// </summary>
    public bool IsHighlightOuter
    {
        get => _highlightIsOuter;
        set
        {
            if (_highlightIsOuter != value)
            {
                _highlightIsOuter = value;
                Rebuild();
            }
        }
    }

    /// <summary>
    /// Bounds of the inner document zone in overlay-local DIPs.
    /// When set (and meaningfully smaller than the full overlay), a second set of inner
    /// indicators is shown at the inner-zone edges. Set to <see cref="Rect.Empty"/> to hide them.
    /// </summary>
    public Rect InnerBounds
    {
        get => _innerBounds;
        set
        {
            if (_innerBounds != value)
            {
                _innerBounds = value;
                Rebuild();
            }
        }
    }

    // --- Constructor ---------------------------------------------------------

    public DockEdgeOverlayWindow()
    {
        WindowStyle      = WindowStyle.None;
        AllowsTransparency = true;
        Background       = Brushes.Transparent;
        ShowInTaskbar    = false;
        Topmost          = true;
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

        // Pre-create outer indicators (4 directions)
        CreateIndicatorSet(OuterSize, _outerBgs, _outerFrames, _outerStrips);

        // Pre-create inner indicators (4 directions, hidden by default)
        CreateIndicatorSet(InnerSize, _innerBgs, _innerFrames, _innerStrips);
        foreach (var bg in _innerBgs.Values)  bg.Visibility = Visibility.Collapsed;
        foreach (var f  in _innerFrames.Values) f.Visibility = Visibility.Collapsed;
        foreach (var s  in _innerStrips.Values) s.Visibility = Visibility.Collapsed;
    }

    private void CreateIndicatorSet(
        double size,
        Dictionary<DockDirection, Rectangle> bgs,
        Dictionary<DockDirection, Rectangle> frames,
        Dictionary<DockDirection, Rectangle> strips)
    {
        var directions = new[] { DockDirection.Left, DockDirection.Right, DockDirection.Top, DockDirection.Bottom };

        foreach (var dir in directions)
        {
            var bg = new Rectangle
            {
                Width           = size,
                Height          = size,
                Fill            = NormalFill,
                Stroke          = NormalStroke,
                StrokeThickness = 1,
                RadiusX         = 4,
                RadiusY         = 4
            };
            bgs[dir] = bg;
            _canvas.Children.Add(bg);

            const double iconMargin = 6;
            var iconSize = size - iconMargin * 2;

            var frame = new Rectangle
            {
                Width           = iconSize,
                Height          = iconSize,
                Fill            = Brushes.Transparent,
                Stroke          = FrameStrokeNormal,
                StrokeThickness = 1
            };
            frames[dir] = frame;
            _canvas.Children.Add(frame);

            var strip = new Rectangle { Fill = StripFillNormal };
            strips[dir] = strip;
            _canvas.Children.Add(strip);
        }
    }

    // --- Public API ----------------------------------------------------------

    /// <summary>
    /// Shows the overlay positioned over the given target element (the entire CenterHost).
    /// </summary>
    public void ShowOverTarget(UIElement target)
    {
        var targetPos  = target.PointToScreen(new Point(0, 0));
        var targetSize = target.RenderSize;
        var dipPos     = DpiHelper.ScreenToDipForPoint(targetPos);

        Left   = dipPos.X;
        Top    = dipPos.Y;
        Width  = targetSize.Width;
        Height = targetSize.Height;

        Rebuild();
        Show();
        DockAnimationHelper.FadeIn(this, DockAnimationHelper.OverlayFadeInMs);
    }

    /// <summary>
    /// Performs hit-testing. Returns the direction and whether it is an outer or inner hit.
    /// <paramref name="screenPoint"/> must be in physical screen pixels.
    /// </summary>
    public (DockDirection? Direction, bool IsOuter) HitTestEx(Point screenPoint)
    {
        var pt = DpiHelper.ScreenToDipForPoint(screenPoint);
        var lp = new Point(pt.X - Left, pt.Y - Top);

        // Inner indicators take hit-test priority when visible (they are inside the outer zone)
        if (HasInnerIndicators())
        {
            var innerZones = GetInnerZones();
            foreach (var (dir, zone) in innerZones)
            {
                if (zone.Contains(lp))
                    return (dir, false);
            }
        }

        // Outer indicators
        var outerZones = GetOuterZones();
        foreach (var (dir, zone) in outerZones)
        {
            if (zone.Contains(lp))
                return (dir, true);
        }

        return (null, false);
    }

    /// <summary>
    /// Legacy hit-test that always returns only outer hits (backward compatibility).
    /// </summary>
    public DockDirection? HitTest(Point screenPoint) => HitTestEx(screenPoint).Direction;

    // --- Private helpers -----------------------------------------------------

    private bool HasInnerIndicators()
    {
        if (_innerBounds == Rect.Empty) return false;
        // Only show inner indicators if the inner zone is meaningfully smaller than the full overlay
        return _innerBounds.Width < Width - InnerThreshold ||
               _innerBounds.Height < Height - InnerThreshold;
    }

    private Dictionary<DockDirection, Rect> GetOuterZones() => new()
    {
        [DockDirection.Left]   = new(EdgeMargin, Height / 2 - OuterSize / 2, OuterSize, OuterSize),
        [DockDirection.Right]  = new(Width - EdgeMargin - OuterSize, Height / 2 - OuterSize / 2, OuterSize, OuterSize),
        [DockDirection.Top]    = new(Width / 2 - OuterSize / 2, EdgeMargin, OuterSize, OuterSize),
        [DockDirection.Bottom] = new(Width / 2 - OuterSize / 2, Height - EdgeMargin - OuterSize, OuterSize, OuterSize),
    };

    private Dictionary<DockDirection, Rect> GetInnerZones() => new()
    {
        [DockDirection.Left]   = new(_innerBounds.Left + EdgeMargin,
                                     _innerBounds.Y + _innerBounds.Height / 2 - InnerSize / 2,
                                     InnerSize, InnerSize),
        [DockDirection.Right]  = new(_innerBounds.Right - EdgeMargin - InnerSize,
                                     _innerBounds.Y + _innerBounds.Height / 2 - InnerSize / 2,
                                     InnerSize, InnerSize),
        [DockDirection.Top]    = new(_innerBounds.X + _innerBounds.Width / 2 - InnerSize / 2,
                                     _innerBounds.Top + EdgeMargin,
                                     InnerSize, InnerSize),
        [DockDirection.Bottom] = new(_innerBounds.X + _innerBounds.Width / 2 - InnerSize / 2,
                                     _innerBounds.Bottom - EdgeMargin - InnerSize,
                                     InnerSize, InnerSize),
    };

    // --- Rebuild -------------------------------------------------------------

    /// <summary>
    /// Updates positions and visual properties of all pre-created elements.
    /// No elements are created or destroyed — only repositioned and restyled.
    /// </summary>
    private void Rebuild()
    {
        RebuildPreviewZone();
        RebuildOuterIndicators();
        RebuildInnerIndicators();
    }

    private void RebuildPreviewZone()
    {
        if (!_highlightedDirection.HasValue)
        {
            _previewZone.Visibility = Visibility.Collapsed;
            return;
        }

        Rect zone;
        if (_highlightIsOuter)
        {
            // Outer preview: full-width / full-height quarter
            zone = _highlightedDirection.Value switch
            {
                DockDirection.Left   => new Rect(0, 0, Width * 0.25, Height),
                DockDirection.Right  => new Rect(Width * 0.75, 0, Width * 0.25, Height),
                DockDirection.Top    => new Rect(0, 0, Width, Height * 0.25),
                DockDirection.Bottom => new Rect(0, Height * 0.75, Width, Height * 0.25),
                _                    => Rect.Empty
            };
        }
        else
        {
            // Inner preview: bounded by InnerBounds
            if (_innerBounds == Rect.Empty)
            {
                _previewZone.Visibility = Visibility.Collapsed;
                return;
            }

            var ib = _innerBounds;
            zone = _highlightedDirection.Value switch
            {
                DockDirection.Left   => new Rect(ib.X, ib.Y, ib.Width * 0.25, ib.Height),
                DockDirection.Right  => new Rect(ib.X + ib.Width * 0.75, ib.Y, ib.Width * 0.25, ib.Height),
                DockDirection.Top    => new Rect(ib.X, ib.Y, ib.Width, ib.Height * 0.25),
                DockDirection.Bottom => new Rect(ib.X, ib.Y + ib.Height * 0.75, ib.Width, ib.Height * 0.25),
                _                    => Rect.Empty
            };
        }

        if (zone == Rect.Empty)
        {
            _previewZone.Visibility = Visibility.Collapsed;
            return;
        }

        _previewZone.Width  = zone.Width;
        _previewZone.Height = zone.Height;
        Canvas.SetLeft(_previewZone, zone.X);
        Canvas.SetTop(_previewZone, zone.Y);
        _previewZone.Visibility = Visibility.Visible;
    }

    private void RebuildOuterIndicators()
    {
        var positions = GetOuterZones();

        foreach (var (dir, zone) in positions)
        {
            var isHighlighted = _highlightedDirection == dir && _highlightIsOuter;
            PositionIndicator(dir, zone, isHighlighted, OuterSize,
                _outerBgs, _outerFrames, _outerStrips, Visibility.Visible);
        }
    }

    private void RebuildInnerIndicators()
    {
        if (!HasInnerIndicators())
        {
            // Hide all inner indicators
            foreach (var r in _innerBgs.Values)    r.Visibility = Visibility.Collapsed;
            foreach (var r in _innerFrames.Values)  r.Visibility = Visibility.Collapsed;
            foreach (var r in _innerStrips.Values)  r.Visibility = Visibility.Collapsed;
            return;
        }

        var positions = GetInnerZones();

        foreach (var (dir, zone) in positions)
        {
            var isHighlighted = _highlightedDirection == dir && !_highlightIsOuter;
            PositionIndicator(dir, zone, isHighlighted, InnerSize,
                _innerBgs, _innerFrames, _innerStrips, Visibility.Visible);
        }
    }

    private void PositionIndicator(
        DockDirection dir,
        Rect zone,
        bool isHighlighted,
        double size,
        Dictionary<DockDirection, Rectangle> bgs,
        Dictionary<DockDirection, Rectangle> frames,
        Dictionary<DockDirection, Rectangle> strips,
        Visibility defaultVisibility)
    {
        const double iconMargin = 6;
        var iconSize = size - iconMargin * 2;

        // Background
        var bg = bgs[dir];
        bg.Fill            = isHighlighted ? HighlightFill : NormalFill;
        bg.Stroke          = isHighlighted ? HighlightStroke : NormalStroke;
        bg.StrokeThickness = isHighlighted ? 2 : 1;
        bg.Visibility      = defaultVisibility;
        Canvas.SetLeft(bg, zone.X);
        Canvas.SetTop(bg, zone.Y);

        // Icon frame
        var frame = frames[dir];
        frame.Stroke     = isHighlighted ? HighlightStroke : FrameStrokeNormal;
        frame.Visibility = defaultVisibility;
        Canvas.SetLeft(frame, zone.X + iconMargin);
        Canvas.SetTop(frame, zone.Y + iconMargin);

        // Dock strip
        double sx = zone.X + iconMargin, sy = zone.Y + iconMargin, sw = iconSize, sh = iconSize;
        switch (dir)
        {
            case DockDirection.Left:   sw = iconSize * 0.3; break;
            case DockDirection.Right:  sx += iconSize * 0.7; sw = iconSize * 0.3; break;
            case DockDirection.Top:    sh = iconSize * 0.3; break;
            case DockDirection.Bottom: sy += iconSize * 0.7; sh = iconSize * 0.3; break;
        }

        var strip = strips[dir];
        strip.Width      = sw;
        strip.Height     = sh;
        strip.Fill       = isHighlighted ? StripFillHighlight : StripFillNormal;
        strip.Visibility = defaultVisibility;
        Canvas.SetLeft(strip, sx);
        Canvas.SetTop(strip, sy);
    }
}
