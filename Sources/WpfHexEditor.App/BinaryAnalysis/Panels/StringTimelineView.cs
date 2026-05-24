// Project     : WpfHexEditor.App
// File        : StringTimelineView.cs
// Description : Custom FrameworkElement rendering string runs as horizontal offset bands.
//               X-axis = file offset; each run = colored rectangle proportional to its length.
//               Colors: encoding palette fallback; Kind overrides when Kind != None.
//               Opacity: proportional to string length (longer = more opaque).
//               Density heatmap overlay drawn below runs.
//               Viewport-culled per paint — binary search on offset-sorted _drawList.
// Architecture: Pure DrawingContext render; zoom via Slider; scroll via ScrollViewer wrapping this element.
//
// Perf contract:
//   RebuildLayout — O(n log n), runs ONLY when data changes.
//   Zoom/Scroll   — O(1): only InvalidateMeasure/Visual. No rebuild, no allocs.
//   _rowMap stores normalized coords; pixel coords computed at render time.
//   _drawList merges adjacent/overlapping runs with the same brush into single rects,
//   reducing DrawRectangle calls from O(runs) to O(distinct color bands) per frame.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.App.BinaryAnalysis.Services;
using WpfHexEditor.App.BinaryAnalysis.ViewModels;

namespace WpfHexEditor.App.BinaryAnalysis.Panels;

internal sealed class StringTimelineView : FrameworkElement
{
    private static readonly SolidColorBrush BackBrush      = FreezeB(new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)));
    private static readonly SolidColorBrush RulerBrush     = FreezeB(new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)));
    private static readonly SolidColorBrush RulerTextBrush = FreezeB(new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)));
    private static readonly Typeface        RulerTypeface  = new("Consolas");
    private static readonly Pen             RulerPen       = FreezePen(new Pen(RulerTextBrush, 0.5));

    // Pre-built heatmap brush pool keyed by quantized alpha.
    private const byte HeatMaxAlpha    = 0x72;
    private const int  HeatAlphaLevels = 64;
    private static readonly SolidColorBrush[] HeatBrushes = BuildHeatBrushes();
    private static SolidColorBrush[] BuildHeatBrushes()
    {
        var arr = new SolidColorBrush[HeatAlphaLevels];
        for (int i = 0; i < HeatAlphaLevels; i++)
        {
            byte alpha = (byte)(HeatMaxAlpha * i / (HeatAlphaLevels - 1));
            arr[i] = FreezeB(new SolidColorBrush(Color.FromArgb(alpha, 0xFF, 0xA0, 0x00)));
        }
        return arr;
    }

    // Brush palette indexed by small integer for fast lookup in draw list.
    // Indices 0..N match EncodingPalette entries; index N+1..M match KindBrushes.
    private static readonly SolidColorBrush[] BrushById;
    private static readonly int FallbackBrushId;
    private static readonly Dictionary<(StringEncoding, StringKind), int> BrushIndex;
    static StringTimelineView()
    {
        var list = new List<SolidColorBrush>();
        // Encoding brushes (indices 0+)
        foreach (StringEncoding enc in Enum.GetValues<StringEncoding>())
            if (EncodingPalette.Brushes.TryGetValue(enc, out var b)) list.Add(b);
        // Kind brushes follow
        foreach (StringKind kind in Enum.GetValues<StringKind>())
            if (kind != StringKind.None && EncodingPalette.KindBrushes.TryGetValue(kind, out var b)) list.Add(b);
        FallbackBrushId = list.Count;
        list.Add(EncodingPalette.FallbackBrush);
        BrushById = [.. list];

        // BrushIndex must be built AFTER BrushById is populated — do not use a field initializer.
        BrushIndex = BuildBrushIndex();
    }

    // Maps (encoding, kind) → brush index — computed once at startup.
    private static Dictionary<(StringEncoding, StringKind), int> BuildBrushIndex()
    {
        var d   = new Dictionary<(StringEncoding, StringKind), int>();
        var idx = 0;
        foreach (StringEncoding enc in Enum.GetValues<StringEncoding>())
        {
            if (!EncodingPalette.Brushes.ContainsKey(enc)) continue;
            foreach (StringKind kind in Enum.GetValues<StringKind>())
            {
                if (kind != StringKind.None && EncodingPalette.KindBrushes.ContainsKey(kind))
                {
                    // Kind overrides encoding — find the kind brush index
                    int ki = BrushById.Length - 1; // fallback default
                    int search = 0;
                    foreach (StringKind k2 in Enum.GetValues<StringKind>())
                    {
                        if (k2 == StringKind.None) continue;
                        if (!EncodingPalette.KindBrushes.ContainsKey(k2)) continue;
                        if (k2 == kind) { ki = search + CountEncodingBrushes(); break; }
                        search++;
                    }
                    d.TryAdd((enc, kind), ki);
                }
                else
                {
                    d.TryAdd((enc, kind), idx);
                }
            }
            idx++;
        }
        return d;
    }
    private static int CountEncodingBrushes()
    {
        int c = 0;
        foreach (StringEncoding enc in Enum.GetValues<StringEncoding>())
            if (EncodingPalette.Brushes.ContainsKey(enc)) c++;
        return c;
    }

    private static SolidColorBrush FreezeB(SolidColorBrush b) { b.Freeze(); return b; }
    private static Pen FreezePen(Pen p) { p.Freeze(); return p; }

    private const double RowHeight   = 12.0;
    private const double RulerHeight = 18.0;
    private const int    HeatBuckets = 512;
    private const double MinOpacity  = 0.35;
    private const double OpacityRange = 0.65;
    private const double MinRunNorm  = 0.0008;
    private const int    MaxRows     = 120;
    private const double MinRunPx    = 2.0;
    // Merge gap: adjacent runs within this normalized distance get merged into one rect.
    private const double MergeGapNorm = 0.0005;

    // ── Zoom ─────────────────────────────────────────────────────────────────────

    private double _zoom = 1.0;
    public double Zoom
    {
        get => _zoom;
        set
        {
            _zoom = Math.Clamp(value, 1.0, 200.0);
            _zoomDebounce.Stop();
            _zoomDebounce.Start();
        }
    }

    private readonly System.Windows.Threading.DispatcherTimer _zoomDebounce;
    private readonly System.Windows.Threading.DispatcherTimer _refreshDebounce;

    internal double ViewportOffsetX { get; set; }
    internal double ViewportWidth   { get; set; } = double.MaxValue;

    private StringExtractionViewModel? _vm;
    private long _bufferLength;

    // Normalized layout cache: xNorm/rwNorm in [0,1].
    private readonly List<(StringRun run, int row, double xNorm, double rwNorm)> _rowMap = [];
    private int _rowCount;

    // Merged draw list: sorted by (row, brushId, xNorm) then merged.
    // Each entry: (row, brushId, x1Norm, x2Norm, opacity).
    // DrawRuns iterates this instead of _rowMap — far fewer DrawRectangle calls.
    private readonly List<(int row, int brushId, double x1, double x2, float opacity)> _drawList = [];

    private float[] _densityMap = [];

    private readonly ToolTip _tooltip;
    private long _hoveredOffset = long.MinValue;

    public Action<StringRun>? RunSelected { get; set; }

    // ── Constructor ──────────────────────────────────────────────────────────────

    public StringTimelineView()
    {
        _zoomDebounce = new System.Windows.Threading.DispatcherTimer
            { Interval = TimeSpan.FromMilliseconds(16) };  // one frame — smooth without double-render
        _zoomDebounce.Tick += (_, _) =>
        {
            _zoomDebounce.Stop();
            InvalidateMeasure();
            InvalidateVisual();
        };

        _refreshDebounce = new System.Windows.Threading.DispatcherTimer
            { Interval = TimeSpan.FromMilliseconds(250) };
        _refreshDebounce.Tick += (_, _) => { _refreshDebounce.Stop(); DoRebuildAndRender(); };

        _tooltip = new ToolTip { Placement = PlacementMode.Mouse, HasDropShadow = true };
        ToolTip = _tooltip;
    }

    // ── Attach / Refresh ─────────────────────────────────────────────────────────

    private void DoRebuildAndRender()
    {
        _bufferLength = _vm?.LastBufferLength ?? 0;
        RebuildLayout(_vm?.GetAllRuns());
        InvalidateMeasure();
        InvalidateVisual();
    }

    public void Attach(StringExtractionViewModel vm)
    {
        if (_vm is not null) _vm.PropertyChanged -= OnVmChanged;
        _vm = vm;
        _vm.PropertyChanged += OnVmChanged;
        SizeChanged -= OnSizeChanged;
        SizeChanged += OnSizeChanged;
        Refresh();
    }

    private void OnVmChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(StringExtractionViewModel.TotalCount))
        {
            _refreshDebounce.Stop();
            _refreshDebounce.Start();
        }
    }

    private void OnSizeChanged(object _, SizeChangedEventArgs e)
    {
        if (e.WidthChanged && e.NewSize.Width > 0)
            Refresh();
    }

    public void Refresh()
    {
        _refreshDebounce.Stop();
        DoRebuildAndRender();
    }

    // ── RebuildLayout: O(n log n) — normalized coords, merged draw list ───────────

    private void RebuildLayout(IEnumerable<StringRun>? runs)
    {
        _rowMap.Clear();
        _drawList.Clear();
        _rowCount = 0;
        _densityMap = [];
        if (runs is null || _bufferLength <= 0) return;

        double invBuffer = 1.0 / _bufferLength;
        var pq      = new PriorityQueue<int, double>();
        int nextRow = 0;
        var density = new float[HeatBuckets];

        foreach (var run in runs)
        {
            double xNorm  = run.Offset * invBuffer;
            double rwNorm = Math.Max(MinRunNorm, run.Length * invBuffer);
            double xEnd   = xNorm + rwNorm;

            int row;
            if (pq.Count > 0 && pq.TryPeek(out int r, out double end) && end <= xNorm)
            {
                pq.Dequeue();
                row = r;
            }
            else if (nextRow < MaxRows)
            {
                row = nextRow++;
            }
            else
            {
                pq.TryDequeue(out row, out _);
            }
            pq.Enqueue(row, xEnd);
            _rowMap.Add((run, row, xNorm, rwNorm));

            density[Math.Clamp((int)(xNorm * HeatBuckets), 0, HeatBuckets - 1)] += 1f;
        }

        _rowCount = Math.Max(1, Math.Min(nextRow, MaxRows));

        float dmax = 0f;
        foreach (var v in density) if (v > dmax) dmax = v;
        if (dmax > 0f)
        {
            _densityMap = density;
            for (int i = 0; i < _densityMap.Length; i++) _densityMap[i] /= dmax;
        }

        BuildDrawList();
    }

    // Merges _rowMap entries into _drawList by sorting (row, brushId, xNorm) then
    // merging overlapping/adjacent runs with the same brush into single draw rects.
    // This reduces draw calls from O(runs) to O(distinct color bands).
    private void BuildDrawList()
    {
        if (_rowMap.Count == 0) return;

        // Temp list with brush pre-resolved.
        var tmp = new List<(int row, int brushId, double x1, double x2, float opacity)>(_rowMap.Count);
        foreach (var (run, row, xNorm, rwNorm) in _rowMap)
        {
            int brushId = BrushIndex.TryGetValue((run.Encoding, run.Kind), out var bi) ? bi : FallbackBrushId;
            float opacity = (float)(MinOpacity + OpacityRange * Math.Clamp((run.Length - 4) / 56.0, 0.0, 1.0));
            tmp.Add((row, brushId, xNorm, xNorm + rwNorm, opacity));
        }

        // Sort by (row, brushId, x1) for efficient merge sweep.
        tmp.Sort(static (a, b) =>
        {
            int c = a.row.CompareTo(b.row);
            if (c != 0) return c;
            c = a.brushId.CompareTo(b.brushId);
            if (c != 0) return c;
            return a.x1.CompareTo(b.x1);
        });

        // Merge adjacent/overlapping same-brush, same-row spans.
        var cur = tmp[0];
        for (int i = 1; i < tmp.Count; i++)
        {
            var next = tmp[i];
            if (next.row == cur.row && next.brushId == cur.brushId && next.x1 <= cur.x2 + MergeGapNorm)
            {
                // Extend current span; take the higher opacity of the two.
                cur = cur with { x2 = Math.Max(cur.x2, next.x2), opacity = Math.Max(cur.opacity, next.opacity) };
            }
            else
            {
                _drawList.Add(cur);
                cur = next;
            }
        }
        _drawList.Add(cur);
    }

    // ── Layout ───────────────────────────────────────────────────────────────────

    protected override Size MeasureOverride(Size availableSize)
    {
        double baseW = double.IsInfinity(availableSize.Width) || double.IsNaN(availableSize.Width)
            ? (ActualWidth > 0 ? ActualWidth : 200)
            : availableSize.Width;
        double w = Math.Max(baseW, 1) * _zoom;
        double h = RulerHeight + _rowCount * RowHeight;
        if (double.IsInfinity(w) || double.IsNaN(w)) w = baseW;
        if (double.IsInfinity(h) || double.IsNaN(h)) h = RulerHeight + MaxRows * RowHeight;
        return new Size(w, h);
    }

    // ── Render ───────────────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        double w = ActualWidth;
        double h = ActualHeight;
        dc.DrawRectangle(BackBrush, null, new Rect(0, 0, w, h));
        if (_drawList.Count == 0 || _bufferLength <= 0 || w <= 0) return;

        double pixelW = w * _zoom;
        DrawRuler(dc, w, pixelW, pixelsPerDip);
        DrawDensityHeatmap(dc, h, pixelW);
        DrawRuns(dc, h, pixelW);
    }

    private void DrawDensityHeatmap(DrawingContext dc, double h, double pixelW)
    {
        if (_densityMap.Length == 0) return;
        double bucketW  = pixelW / HeatBuckets;
        double contentH = h - RulerHeight;
        double vpEnd    = ViewportOffsetX + ViewportWidth;
        for (int i = 0; i < _densityMap.Length; i++)
        {
            float v = _densityMap[i];
            if (v < 0.05f) continue;
            double bx = i * bucketW;
            if (bx + bucketW < ViewportOffsetX || bx > vpEnd) continue;
            int brushIdx = (int)Math.Clamp(v * (HeatAlphaLevels - 1), 0, HeatAlphaLevels - 1);
            dc.DrawRectangle(HeatBrushes[brushIdx], null, new Rect(bx, RulerHeight, bucketW, contentH));
        }
    }

    private void DrawRuns(DrawingContext dc, double h, double pixelW)
    {
        // _drawList is sorted by (row, brushId, x1Norm) — binary search for first visible entry.
        int startIdx = BinarySearchDrawStart(pixelW);
        double vpEnd = ViewportOffsetX + ViewportWidth;

        for (int i = startIdx; i < _drawList.Count; i++)
        {
            var (row, brushId, x1n, x2n, opacity) = _drawList[i];
            double x  = x1n * pixelW;
            double x2 = Math.Max(x + MinRunPx, x2n * pixelW);

            // _drawList is sorted by x1 within each (row,brush) group but rows interleave.
            // Can only break early within a contiguous run of same-row entries — skip if past viewport.
            if (x > vpEnd) continue;   // not break: next entry might be in a different row/brush
            if (x2 < ViewportOffsetX) continue;

            double y = RulerHeight + row * RowHeight;
            if (y > h) continue;

            var brush = (uint)brushId < (uint)BrushById.Length ? BrushById[brushId] : EncodingPalette.FallbackBrush;

            if (opacity >= 1f)
            {
                dc.DrawRectangle(brush, null, new Rect(x, y + 1, x2 - x, RowHeight - 2));
            }
            else
            {
                dc.PushOpacity(opacity);
                dc.DrawRectangle(brush, null, new Rect(x, y + 1, x2 - x, RowHeight - 2));
                dc.Pop();
            }
        }
    }

    // Binary search on _drawList by x1Norm (approximate — list is sorted within (row,brush) groups).
    // Falls back to 0 on miss — viewport culling in DrawRuns handles the rest.
    private int BinarySearchDrawStart(double pixelW)
    {
        if (_drawList.Count == 0 || pixelW <= 0) return 0;
        double targetNorm = (ViewportOffsetX - MinRunPx) / pixelW;
        // Search by x1 only — entries are sorted by (row, brushId, x1) so this under-estimates.
        int lo = 0, hi = _drawList.Count - 1;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (_drawList[mid].x1 < targetNorm - 0.05) lo = mid + 1;
            else hi = mid;
        }
        return Math.Max(0, lo - 4);  // small backstep to avoid missing cross-brush entries
    }

    private const int MaxRulerTicks = 200;

    private void DrawRuler(DrawingContext dc, double w, double pixelW, double pixelsPerDip)
    {
        dc.DrawRectangle(RulerBrush, null, new Rect(0, 0, w, RulerHeight));
        if (_bufferLength <= 0) return;

        double scale = pixelW / _bufferLength;
        if (scale <= 0) return;
        long tickStep = (long)Math.Pow(2, Math.Ceiling(Math.Log2(Math.Max(1.0, 100.0 / scale))));
        tickStep = Math.Max(1, tickStep);

        long estimatedTicks = (long)(pixelW / (tickStep * scale)) + 1;
        if (estimatedTicks > MaxRulerTicks)
            tickStep = (long)Math.Ceiling(pixelW / (MaxRulerTicks * scale));

        double vpEnd = ViewportOffsetX + ViewportWidth;
        for (long off = 0; off * scale <= pixelW; off += tickStep)
        {
            double x = off * scale;
            if (x + 60 < ViewportOffsetX || x > vpEnd) continue;
            dc.DrawLine(RulerPen, new Point(x, 0), new Point(x, RulerHeight));
            var ft = new FormattedText($"0x{off:X}",
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, RulerTypeface, 8, RulerTextBrush, pixelsPerDip);
            dc.DrawText(ft, new Point(x + 2, 3));
        }
    }

    // ── Mouse ─────────────────────────────────────────────────────────────────────

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var pos = e.GetPosition(this);
        var hit = HitTestRun(pos);
        if (hit is not null)
        {
            if (hit.Offset != _hoveredOffset)
            {
                _hoveredOffset   = hit.Offset;
                _tooltip.Content = $"0x{hit.Offset:X8}  [{hit.Encoding}]  {TruncateValue(hit.Value, 60)}";
            }
            _tooltip.IsOpen = true;
        }
        else
        {
            _hoveredOffset  = long.MinValue;
            _tooltip.IsOpen = false;
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hoveredOffset  = long.MinValue;
        _tooltip.IsOpen = false;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var run = HitTestRun(e.GetPosition(this));
        if (run is not null) RunSelected?.Invoke(run);
    }

    private StringRun? HitTestRun(Point pos)
    {
        if (_bufferLength <= 0 || ActualWidth <= 0 || _rowMap.Count == 0) return null;
        if (pos.Y < RulerHeight) return null;

        double pixelW     = ActualWidth * _zoom;
        double targetNorm = pos.X / pixelW;
        StringRun? best   = null;
        double bestDist   = double.MaxValue;

        int lo = 0, hi = _rowMap.Count - 1;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (_rowMap[mid].xNorm < targetNorm - 200.0 / pixelW) lo = mid + 1;
            else hi = mid;
        }

        for (int i = Math.Max(0, lo - 1); i < _rowMap.Count; i++)
        {
            var (run, _, xNorm, rwNorm) = _rowMap[i];
            double x  = xNorm  * pixelW;
            double rw = Math.Max(MinRunPx, rwNorm * pixelW);
            if (x > pos.X + 4) break;
            if (pos.X >= x && pos.X <= x + rw)
            {
                double dist = Math.Abs(pos.X - (x + rw / 2));
                if (dist < bestDist) { best = run; bestDist = dist; }
            }
        }
        return best;
    }

    private static string TruncateValue(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}

// ── Legend ────────────────────────────────────────────────────────────────────────

/// <summary>Draws a compact color legend: encoding swatches + Kind swatches.</summary>
internal sealed class StringTimelineLegend : FrameworkElement
{
    private static readonly Typeface        LegendTypeface  = new("Segoe UI");
    private static readonly SolidColorBrush LegendTextBrush = FreezeLegendBrush();
    private static SolidColorBrush FreezeLegendBrush()
    {
        var b = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));
        b.Freeze();
        return b;
    }

    private static readonly (string label, SolidColorBrush brush)[] _items =
    [
        ("ASCII",   EncodingPalette.Brushes[StringEncoding.Ascii]),
        ("UTF-8",   EncodingPalette.Brushes[StringEncoding.Utf8]),
        ("UTF-16",  EncodingPalette.Brushes[StringEncoding.Utf16Le]),
        ("EBCDIC",  EncodingPalette.Brushes[StringEncoding.Ebcdic]),
        ("Latin-1", EncodingPalette.Brushes[StringEncoding.Latin1]),
        ("TBL",     EncodingPalette.Brushes[StringEncoding.Tbl]),
        ("Email",   (SolidColorBrush)EncodingPalette.KindBrushes[StringKind.Email]),
        ("URL",     (SolidColorBrush)EncodingPalette.KindBrushes[StringKind.Url]),
        ("Path",    (SolidColorBrush)EncodingPalette.KindBrushes[StringKind.PathWin]),
        ("GUID",    (SolidColorBrush)EncodingPalette.KindBrushes[StringKind.Guid]),
        ("Version", (SolidColorBrush)EncodingPalette.KindBrushes[StringKind.Version]),
        ("IP",      (SolidColorBrush)EncodingPalette.KindBrushes[StringKind.IpV4]),
        ("Hash",    (SolidColorBrush)EncodingPalette.KindBrushes[StringKind.HexHash]),
    ];

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
        return new Size(w, 18);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        double x = 4;
        foreach (var (label, brush) in _items)
        {
            dc.DrawRectangle(brush, null, new Rect(x, 4, 10, 10));
            x += 12;
            var ft = new FormattedText(label,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, LegendTypeface, 9, LegendTextBrush, pixelsPerDip);
            dc.DrawText(ft, new Point(x, 4));
            x += ft.Width + 10;
        }
    }
}

// ── Host panel ────────────────────────────────────────────────────────────────────

/// <summary>Host panel for the timeline: wraps canvas in ScrollViewer + zoom slider + legend.</summary>
internal sealed class StringTimelinePanel : Border
{
    private readonly StringTimelineView   _view   = new() { UseLayoutRounding = true };
    private readonly StringTimelineLegend _legend = new();
    private readonly ScrollViewer         _scroll;
    private          Slider               _zoomSlider = null!;

    public StringTimelinePanel()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var zoomRow = new DockPanel { LastChildFill = false, Margin = new Thickness(4, 2, 4, 2) };
        zoomRow.SetResourceReference(DockPanel.BackgroundProperty, "Panel_ToolbarBrush");

        var zoomLbl = new TextBlock
        {
            Text = "Zoom:", FontSize = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0),
        };
        zoomLbl.SetResourceReference(TextBlock.ForegroundProperty, "Panel_ToolbarForegroundBrush");

        _zoomSlider = new Slider
        {
            Minimum = 1, Maximum = 100, Value = 1,
            Width = 140, Height = 16,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _zoomSlider.ValueChanged += OnZoomSliderChanged;

        var resetBtn = new Button
        {
            Content = "1:1", FontSize = 10,
            Padding = new Thickness(4, 1, 4, 1), Margin = new Thickness(4, 0, 0, 0),
            FocusVisualStyle = null,
        };
        resetBtn.SetResourceReference(StyleProperty,              "PanelIconButtonStyle");
        resetBtn.SetResourceReference(Control.ForegroundProperty, "Panel_ToolbarForegroundBrush");
        resetBtn.Click += (_, _) => _zoomSlider.Value = 1;

        DockPanel.SetDock(zoomLbl,     Dock.Left);
        DockPanel.SetDock(_zoomSlider, Dock.Left);
        DockPanel.SetDock(resetBtn,    Dock.Left);
        zoomRow.Children.Add(zoomLbl);
        zoomRow.Children.Add(_zoomSlider);
        zoomRow.Children.Add(resetBtn);

        _scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            Content = _view,
        };
        _scroll.ScrollChanged += OnScrollChanged;
        _view.MouseWheel      += OnViewMouseWheel;

        _legend.SetResourceReference(BackgroundProperty, "Panel_ToolbarBrush");

        Grid.SetRow(zoomRow, 0);
        Grid.SetRow(_scroll,  1);
        Grid.SetRow(_legend,  2);
        root.Children.Add(zoomRow);
        root.Children.Add(_scroll);
        root.Children.Add(_legend);
        Child = root;
    }

    private void OnZoomSliderChanged(object _, RoutedPropertyChangedEventArgs<double> e)
        => _view.Zoom = e.NewValue;

    private void OnScrollChanged(object _, ScrollChangedEventArgs _2)
    {
        const double Eps = 0.5;
        if (Math.Abs(_view.ViewportOffsetX - _scroll.HorizontalOffset) < Eps &&
            Math.Abs(_view.ViewportWidth   - _scroll.ViewportWidth)    < Eps) return;
        _view.ViewportOffsetX = _scroll.HorizontalOffset;
        _view.ViewportWidth   = _scroll.ViewportWidth;
        _view.InvalidateVisual();
    }

    private void OnViewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;

        double anchorRatio = _view.ActualWidth > 0
            ? ((MouseWheelEventArgs)e).GetPosition(_view).X / _view.ActualWidth
            : 0.0;

        _zoomSlider.Value = Math.Clamp(_zoomSlider.Value + e.Delta / 120.0 * 2, 1, 100);

        _view.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, () =>
        {
            double targetX = anchorRatio * _scroll.ExtentWidth - _scroll.ViewportWidth / 2;
            _scroll.ScrollToHorizontalOffset(Math.Max(0, targetX));
        });

        e.Handled = true;
    }

    public void Attach(StringExtractionViewModel vm, Action<StringRun> onSelected)
    {
        _view.Attach(vm);
        _view.RunSelected = onSelected;
    }

    public void Refresh() => _view.Refresh();
}
