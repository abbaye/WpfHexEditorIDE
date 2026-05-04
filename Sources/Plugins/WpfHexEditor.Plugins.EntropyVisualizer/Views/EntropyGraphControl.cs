// ==========================================================
// Project: WpfHexEditor.Plugins.EntropyVisualizer
// File: Views/EntropyGraphControl.cs
// Description: Pure-WPF FrameworkElement that renders the entropy polyline
//              graph using DrawingVisual. No third-party dependencies.
// Architecture Notes:
//     StreamGeometry is cached and rebuilt only when Chunks changes, not on
//     every resize — resize re-draws the cached geometry in the new plotW/plotH.
//     ArrangeOverride does NOT call Render to avoid double-render on first layout;
//     OnRenderSizeChanged is the single trigger for size-driven redraws.
// ==========================================================

using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Plugins.EntropyVisualizer.Models;

namespace WpfHexEditor.Plugins.EntropyVisualizer.Views;

internal sealed class EntropyGraphControl : FrameworkElement
{
    // ── Constants ────────────────────────────────────────────────────────────

    private const double PaddingLeft   = 36.0; // Y-axis label space
    private const double PaddingRight  = 8.0;
    private const double PaddingTop    = 8.0;
    private const double PaddingBottom = 24.0; // X-axis label space
    private const double MaxEntropy    = 8.0;
    private const double HighThreshold = 7.2;

    // ── Static resources ─────────────────────────────────────────────────────

    private static readonly Typeface s_labelTypeface  = new("Segoe UI");
    private static readonly Brush    s_axisBrush       = MakeFrozen(Color.FromArgb(120, 180, 180, 180));
    private static readonly Brush    s_highEntropyBand = MakeFrozen(Color.FromArgb(18,  244,  71,  71));
    private static readonly Brush    s_gridBrush       = MakeFrozen(Color.FromArgb( 40, 180, 180, 180));
    private static readonly Brush    s_thresholdBrush  = MakeFrozen(Color.FromArgb( 80, 244,  71,  71));
    private static readonly Pen      s_axisPen         = MakeFrozenPen(s_axisBrush,      1.0);
    private static readonly Pen      s_gridPen         = MakeFrozenPen(s_gridBrush,      1.0);
    private static readonly Pen      s_thresholdPen    = MakeFrozenPen(s_thresholdBrush, 1.0);
    private static readonly Pen      s_polylinePen     = MakeFrozenPen(MakeFrozen(Color.FromArgb(200, 86, 213, 119)), 1.5);

    // ── Dependency properties ─────────────────────────────────────────────────

    public static readonly DependencyProperty ChunksProperty =
        DependencyProperty.Register(nameof(Chunks), typeof(IReadOnlyList<EntropyChunk>),
            typeof(EntropyGraphControl),
            new FrameworkPropertyMetadata(null,
                FrameworkPropertyMetadataOptions.AffectsRender,
                (d, _) => ((EntropyGraphControl)d).OnChunksChanged()));

    public IReadOnlyList<EntropyChunk>? Chunks
    {
        get => (IReadOnlyList<EntropyChunk>?)GetValue(ChunksProperty);
        set => SetValue(ChunksProperty, value);
    }

    // ── Events ────────────────────────────────────────────────────────────────

    public event Action<EntropyChunk>?  NavigateRequested;
    public event Action<EntropyChunk?>? HoverChanged;

    // ── State ─────────────────────────────────────────────────────────────────

    private readonly DrawingVisual  _visual = new();
    private          StreamGeometry? _cachedPolyline;   // invalidated on Chunks change

    // ── Constructor ──────────────────────────────────────────────────────────

    public EntropyGraphControl()
    {
        AddVisualChild(_visual);
        ClipToBounds = true;
    }

    // ── Visual children ──────────────────────────────────────────────────────

    protected override int    VisualChildrenCount     => 1;
    protected override Visual GetVisualChild(int index) => _visual;

    // ── Layout ────────────────────────────────────────────────────────────────

    protected override Size MeasureOverride(Size availableSize)
    {
        // Guard against Infinity when placed in a StackPanel or ScrollViewer.
        double w = double.IsInfinity(availableSize.Width)  ? 200 : availableSize.Width;
        double h = double.IsInfinity(availableSize.Height) ? 100 : availableSize.Height;
        return new Size(w, h);
    }

    // ArrangeOverride intentionally does NOT call Render — OnRenderSizeChanged
    // fires immediately after and is the single size-driven render trigger.
    protected override Size ArrangeOverride(Size finalSize) => finalSize;

    protected override void OnRenderSizeChanged(SizeChangedInfo info)
    {
        base.OnRenderSizeChanged(info);
        Render(RenderSize);
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    private void OnChunksChanged()
    {
        _cachedPolyline = null;   // geometry is plot-size-dependent; invalidate for rebuild
        Render(RenderSize);
    }

    private void Render(Size size)
    {
        using var dc = _visual.RenderOpen();

        if (size.Width < 20 || size.Height < 20) return;

        double plotW = size.Width  - PaddingLeft - PaddingRight;
        double plotH = size.Height - PaddingTop  - PaddingBottom;

        if (plotW <= 0 || plotH <= 0) return;

        DrawAxesAndGrid(dc, plotW, plotH);

        var chunks = Chunks;
        if (chunks is null || chunks.Count == 0) return;

        DrawHighEntropyBackground(dc, chunks, plotW, plotH);
        DrawPolyline(dc, chunks, plotW, plotH);
    }

    private void DrawAxesAndGrid(DrawingContext dc, double plotW, double plotH)
    {
        for (int e = 0; e <= 8; e += 2)
        {
            double y = PaddingTop + plotH - (e / MaxEntropy) * plotH;
            dc.DrawLine(s_gridPen, new Point(PaddingLeft, y), new Point(PaddingLeft + plotW, y));

            var ft = MakeLabel(e.ToString());
            dc.DrawText(ft, new Point(PaddingLeft - ft.Width - 4, y - ft.Height / 2));
        }

        double threshY = PaddingTop + plotH - (HighThreshold / MaxEntropy) * plotH;
        dc.DrawLine(s_thresholdPen,
            new Point(PaddingLeft, threshY),
            new Point(PaddingLeft + plotW, threshY));

        dc.DrawLine(s_axisPen, new Point(PaddingLeft, PaddingTop),
                               new Point(PaddingLeft, PaddingTop + plotH));
        dc.DrawLine(s_axisPen, new Point(PaddingLeft, PaddingTop + plotH),
                               new Point(PaddingLeft + plotW, PaddingTop + plotH));
    }

    private void DrawHighEntropyBackground(
        DrawingContext dc, IReadOnlyList<EntropyChunk> chunks, double plotW, double plotH)
    {
        int n = chunks.Count;
        int? runStart = null;

        for (int i = 0; i <= n; i++)
        {
            bool isHigh = i < n && chunks[i].IsHighEntropy;
            if (isHigh && runStart is null)
                runStart = i;
            else if (!isHigh && runStart is not null)
            {
                double x1 = PaddingLeft + (runStart.Value / (double)n) * plotW;
                double x2 = PaddingLeft + (i            / (double)n) * plotW;
                dc.DrawRectangle(s_highEntropyBand, null,
                    new Rect(x1, PaddingTop, x2 - x1, plotH));
                runStart = null;
            }
        }
    }

    private void DrawPolyline(
        DrawingContext dc, IReadOnlyList<EntropyChunk> chunks, double plotW, double plotH)
    {
        // Rebuild geometry only when Chunks changed; reuse on resize redraws.
        if (_cachedPolyline is null)
        {
            int n   = chunks.Count;
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                for (int i = 0; i < n; i++)
                {
                    double x = PaddingLeft + ((i + 0.5) / n) * plotW;
                    double y = PaddingTop  + plotH - (chunks[i].Entropy / MaxEntropy) * plotH;
                    if (i == 0) ctx.BeginFigure(new Point(x, y), false, false);
                    else        ctx.LineTo(new Point(x, y), true, false);
                }
            }
            geo.Freeze();
            _cachedPolyline = geo;
        }

        dc.DrawGeometry(null, s_polylinePen, _cachedPolyline);
    }

    // ── Mouse interaction ─────────────────────────────────────────────────────

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        HoverChanged?.Invoke(HitTestChunk(e.GetPosition(this)));
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        HoverChanged?.Invoke(null);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var chunk = HitTestChunk(e.GetPosition(this));
        if (chunk is not null) NavigateRequested?.Invoke(chunk);
    }

    private EntropyChunk? HitTestChunk(Point pos)
    {
        var chunks = Chunks;
        if (chunks is null || chunks.Count == 0) return null;

        double plotW = RenderSize.Width - PaddingLeft - PaddingRight;
        if (plotW <= 0) return null;

        double relX = pos.X - PaddingLeft;
        if (relX < 0 || relX > plotW) return null;

        int idx = (int)(relX / plotW * chunks.Count);
        return chunks[Math.Clamp(idx, 0, chunks.Count - 1)];
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // PresentationSource used instead of Application.Current to be standalone-safe.
    private double PixelsPerDip =>
        PresentationSource.FromVisual(this) is { } src
            ? src.CompositionTarget.TransformToDevice.M11
            : 1.0;

    private FormattedText MakeLabel(string text) =>
        new(text, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, s_labelTypeface, 10.0,
            Brushes.Gray, PixelsPerDip);

    private static SolidColorBrush MakeFrozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    private static Pen MakeFrozenPen(Brush brush, double thickness)
    {
        var p = new Pen(brush, thickness);
        p.Freeze();
        return p;
    }
}
