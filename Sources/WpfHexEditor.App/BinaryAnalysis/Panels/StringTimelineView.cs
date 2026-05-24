// Project     : WpfHexEditor.App
// File        : StringTimelineView.cs
// Description : Custom FrameworkElement rendering string runs as horizontal offset bands.
//               X-axis = file offset; each run = colored rectangle proportional to its length.
//               Colors: encoding palette fallback; Kind overrides when Kind != None.
//               Opacity: proportional to string length (longer = more opaque).
//               Density heatmap overlay drawn below runs.
//               Viewport-culled per paint — binary search on offset-sorted _rowMap.
// Architecture: Pure DrawingContext render; zoom via Slider; scroll via ScrollViewer wrapping this element.

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
    private static readonly SolidColorBrush HeatBrush      = FreezeB(new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xA0, 0x00)));
    private static readonly Typeface        RulerTypeface  = new("Consolas");
    private static readonly Pen             RulerPen       = FreezePen(new Pen(RulerTextBrush, 0.5));

    private static SolidColorBrush FreezeB(SolidColorBrush b) { b.Freeze(); return b; }
    private static Pen FreezePen(Pen p) { p.Freeze(); return p; }

    private const double RowHeight    = 12.0;
    private const double RulerHeight  = 18.0;
    private const double MinRunWidth  = 2.0;
    private const int    HeatBuckets  = 512;   // density heatmap resolution
    private const double MinOpacity   = 0.35;  // shortest strings at this opacity
    private const double OpacityRange = 0.65;  // added as length grows toward 60 chars

    // ── Zoom ──────────────────────────────────────────────────────────────────

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

    // Anchor used when zooming with Ctrl+Scroll: keeps the offset under the mouse stationary.
    private double _zoomAnchorRatio = 0.0;  // [0,1] fraction of total width
    private readonly System.Windows.Threading.DispatcherTimer _zoomDebounce;

    // ── Viewport ──────────────────────────────────────────────────────────────

    // Set by StringTimelinePanel on scroll change — enables X-axis culling in OnRender.
    internal double ViewportOffsetX { get; set; }
    internal double ViewportWidth   { get; set; } = double.MaxValue;

    // ── State ─────────────────────────────────────────────────────────────────

    private StringExtractionViewModel? _vm;
    private long _bufferLength;

    // Layout cache — rebuilt in Refresh(), consumed by MeasureOverride/OnRender/HitTestRun.
    private List<(StringRun run, int row, double x, double rw)> _rowMap = [];
    private int _rowCount;

    // Density heatmap: one float per bucket = run-count density, normalized to [0,1].
    private float[] _densityMap = [];

    // PixelsPerDip cached on first render — stable for the element lifetime.
    private double _pixelsPerDip = 1.0;

    // WPF ToolTip — updated on hover, avoids InvalidateVisual per mouse-move.
    private readonly ToolTip _tooltip;

    public Action<StringRun>? RunSelected { get; set; }

    // ── Constructor ───────────────────────────────────────────────────────────

    public StringTimelineView()
    {
        _zoomDebounce = new System.Windows.Threading.DispatcherTimer
            { Interval = TimeSpan.FromMilliseconds(30) };
        _zoomDebounce.Tick += OnZoomDebounced;

        _tooltip = new ToolTip { Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse, HasDropShadow = true };
        ToolTip = _tooltip;
    }

    private void OnZoomDebounced(object? s, EventArgs e)
    {
        _zoomDebounce.Stop();
        RebuildLayout(_vm?.GetAllRuns());
        InvalidateMeasure();
        InvalidateVisual();
    }

    // ── Attach / Refresh ──────────────────────────────────────────────────────

    public void Attach(StringExtractionViewModel vm)
    {
        if (_vm is not null) _vm.PropertyChanged -= OnVmChanged;
        _vm = vm;
        _vm.PropertyChanged += OnVmChanged;
        SizeChanged += OnSizeChanged;
        Refresh();
    }

    private void OnVmChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(StringExtractionViewModel.TotalCount))
            Refresh();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged && e.NewSize.Width > 0)
            Refresh();
    }

    public void Refresh()
    {
        _bufferLength = _vm?.LastBufferLength ?? 0;
        RebuildLayout(_vm?.GetAllRuns());
        InvalidateMeasure();
        InvalidateVisual();
    }

    // ── Layout: O(n log n) greedy row-packing via min-heap ────────────────────

    private void RebuildLayout(IEnumerable<StringRun>? runs)
    {
        _rowMap.Clear();
        _rowCount = 0;
        _densityMap = [];
        if (runs is null || _bufferLength <= 0) return;

        double w = Math.Max(ActualWidth > 0 ? ActualWidth : DesiredSize.Width, 1) * _zoom;
        if (w <= 0) return;
        double scale = w / _bufferLength;

        // Min-heap keyed by row-end X — O(n log n) instead of O(n²) linear scan.
        var pq       = new PriorityQueue<int, double>();
        int nextRow  = 0;

        // Density buckets — accumulated before normalisation.
        var density = new float[HeatBuckets];

        foreach (var run in runs)
        {
            double x    = run.Offset * scale;
            double rw   = Math.Max(MinRunWidth, run.Length * scale);
            double xEnd = x + rw + 1;

            int row;
            if (pq.Count > 0 && pq.TryPeek(out int r, out double end) && end <= x)
            {
                pq.Dequeue();
                row = r;
            }
            else
            {
                row = nextRow++;
            }
            pq.Enqueue(row, xEnd);
            _rowMap.Add((run, row, x, rw));

            // Accumulate density in the bucket spanning the run center.
            int bucket = (int)Math.Clamp(x / w * HeatBuckets, 0, HeatBuckets - 1);
            density[bucket] += 1f;
        }

        _rowCount = Math.Max(1, nextRow);

        // Normalise density to [0,1].
        float max = 0f;
        foreach (var v in density) if (v > max) max = v;
        if (max > 0f)
        {
            _densityMap = density;
            for (int i = 0; i < _densityMap.Length; i++) _densityMap[i] /= max;
        }
    }

    // ── Layout override ───────────────────────────────────────────────────────

    protected override Size MeasureOverride(Size availableSize)
    {
        double baseW = double.IsInfinity(availableSize.Width)
            ? (ActualWidth > 0 ? ActualWidth : 200)
            : availableSize.Width;
        double w = Math.Max(baseW, 1) * _zoom;
        double h = RulerHeight + _rowCount * RowHeight;
        return new Size(w, h);
    }

    // ── Render ────────────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        _pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        double w = ActualWidth;
        double h = ActualHeight;
        dc.DrawRectangle(BackBrush, null, new Rect(0, 0, w, h));

        if (_rowMap.Count == 0 || _bufferLength <= 0 || w <= 0) return;

        double scale = w / _bufferLength;
        DrawRuler(dc, w, scale);
        DrawDensityHeatmap(dc, w, h, scale);
        DrawRuns(dc, w, h, scale);
    }

    private void DrawDensityHeatmap(DrawingContext dc, double w, double h, double scale)
    {
        if (_densityMap.Length == 0) return;
        double bucketW = w / HeatBuckets;
        double contentH = h - RulerHeight;
        for (int i = 0; i < _densityMap.Length; i++)
        {
            float v = _densityMap[i];
            if (v < 0.05f) continue;
            double bx = i * bucketW;
            // Cull buckets outside viewport.
            if (bx + bucketW < ViewportOffsetX || bx > ViewportOffsetX + ViewportWidth) continue;
            dc.PushOpacity(v * 0.45);
            dc.DrawRectangle(HeatBrush, null, new Rect(bx, RulerHeight, bucketW, contentH));
            dc.Pop();
        }
    }

    private void DrawRuns(DrawingContext dc, double w, double h, double scale)
    {
        // Binary-search first run that could be visible (x + minWidth >= ViewportOffsetX).
        int startIdx = BinarySearchVisibleStart();

        for (int i = startIdx; i < _rowMap.Count; i++)
        {
            var (run, row, x, rw) = _rowMap[i];

            // X-axis viewport cull — runs are offset-sorted so we can break early.
            if (x > ViewportOffsetX + ViewportWidth) break;
            if (x + rw < ViewportOffsetX) continue;

            double y = RulerHeight + row * RowHeight;
            if (y > h) continue;

            // Color: Kind overrides encoding color when Kind != None.
            SolidColorBrush brush;
            if (run.Kind != StringKind.None && EncodingPalette.KindBrushes.TryGetValue(run.Kind, out var kb))
                brush = kb;
            else if (EncodingPalette.Brushes.TryGetValue(run.Encoding, out var eb))
                brush = eb;
            else
                brush = EncodingPalette.FallbackBrush;

            // Opacity: proportional to string length; longer = more opaque.
            double opacity = MinOpacity + OpacityRange * Math.Clamp((run.Length - 4) / 56.0, 0.0, 1.0);

            dc.PushOpacity(opacity);
            dc.DrawRectangle(brush, null, new Rect(x, y + 1, rw, RowHeight - 2));
            dc.Pop();
        }
    }

    // Returns first _rowMap index whose run.Offset*scale could reach ViewportOffsetX.
    private int BinarySearchVisibleStart()
    {
        if (_bufferLength <= 0 || ActualWidth <= 0 || _rowMap.Count == 0) return 0;
        double scale        = ActualWidth / _bufferLength;
        long   targetOffset = (long)((ViewportOffsetX - MinRunWidth) / scale);
        int lo = 0, hi = _rowMap.Count - 1;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (_rowMap[mid].run.Offset < targetOffset) lo = mid + 1;
            else hi = mid;
        }
        return Math.Max(0, lo - 1);
    }

    private const int MaxRulerTicks = 200;

    private void DrawRuler(DrawingContext dc, double w, double scale)
    {
        dc.DrawRectangle(RulerBrush, null, new Rect(0, 0, w, RulerHeight));
        long tickStep = (long)Math.Pow(2, Math.Ceiling(Math.Log2(100.0 / scale)));
        tickStep = Math.Max(1, tickStep);

        long estimatedTicks = scale > 0 ? (long)(w / (tickStep * scale)) + 1 : 0;
        if (estimatedTicks > MaxRulerTicks)
            tickStep = (long)Math.Ceiling(w / (MaxRulerTicks * scale));

        for (long off = 0; off * scale <= w; off += tickStep)
        {
            double x = off * scale;
            if (x + 60 < ViewportOffsetX || x > ViewportOffsetX + ViewportWidth) continue;
            dc.DrawLine(RulerPen, new Point(x, 0), new Point(x, RulerHeight));
            var ft = new FormattedText($"0x{off:X}",
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, RulerTypeface, 8, RulerTextBrush, _pixelsPerDip);
            dc.DrawText(ft, new Point(x + 2, 3));
        }
    }

    // ── Mouse ─────────────────────────────────────────────────────────────────

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var pos = e.GetPosition(this);
        var hit = HitTestRun(pos);
        if (hit is not null)
        {
            _tooltip.Content   = $"0x{hit.Offset:X8}  [{hit.Encoding}]  {TruncateValue(hit.Value, 60)}";
            _tooltip.IsOpen    = true;
        }
        else
        {
            _tooltip.IsOpen = false;
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _tooltip.IsOpen = false;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var run = HitTestRun(e.GetPosition(this));
        if (run is not null) RunSelected?.Invoke(run);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        // Ctrl+Scroll zooms centred on mouse X — handled by StringTimelinePanel.
        // Store the ratio here so the panel can restore scroll position.
        if (Keyboard.Modifiers == ModifierKeys.Control && ActualWidth > 0)
            _zoomAnchorRatio = e.GetPosition(this).X / ActualWidth;
    }

    internal double ZoomAnchorRatio => _zoomAnchorRatio;

    private StringRun? HitTestRun(Point pos)
    {
        if (_bufferLength <= 0 || ActualWidth <= 0 || _rowMap.Count == 0) return null;
        double scale = ActualWidth / _bufferLength;
        if (pos.Y < RulerHeight) return null;

        long targetOffset = (long)(pos.X / scale);
        StringRun? best   = null;
        double bestDist   = double.MaxValue;

        int lo = 0, hi = _rowMap.Count - 1;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (_rowMap[mid].run.Offset < targetOffset - (long)(200 / scale)) lo = mid + 1;
            else hi = mid;
        }

        for (int i = lo; i < _rowMap.Count; i++)
        {
            var (run, _, x, rw) = _rowMap[i];
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

// ── Legend ────────────────────────────────────────────────────────────────────

/// <summary>Draws a compact color legend: encoding swatches + known Kind swatches.</summary>
internal sealed class StringTimelineLegend : FrameworkElement
{
    private static readonly Typeface LegendTypeface = new("Segoe UI");
    private double _pixelsPerDip = 1.0;

    private static readonly (string label, SolidColorBrush brush)[] _items =
    [
        ("ASCII",   EncodingPalette.Brushes[StringEncoding.Ascii]),
        ("UTF-8",   EncodingPalette.Brushes[StringEncoding.Utf8]),
        ("UTF-16",  EncodingPalette.Brushes[StringEncoding.Utf16Le]),
        ("EBCDIC",  EncodingPalette.Brushes[StringEncoding.Ebcdic]),
        ("Latin-1", EncodingPalette.Brushes[StringEncoding.Latin1]),
        ("TBL",     EncodingPalette.Brushes[StringEncoding.Tbl]),
        // Kind overrides
        ("Email",   (SolidColorBrush)EncodingPalette.KindBrushes[StringKind.Email]),
        ("URL",     (SolidColorBrush)EncodingPalette.KindBrushes[StringKind.Url]),
        ("Path",    (SolidColorBrush)EncodingPalette.KindBrushes[StringKind.PathWin]),
        ("GUID",    (SolidColorBrush)EncodingPalette.KindBrushes[StringKind.Guid]),
        ("Version", (SolidColorBrush)EncodingPalette.KindBrushes[StringKind.Version]),
        ("IP",      (SolidColorBrush)EncodingPalette.KindBrushes[StringKind.IpV4]),
        ("Hash",    (SolidColorBrush)EncodingPalette.KindBrushes[StringKind.HexHash]),
    ];

    protected override Size MeasureOverride(Size availableSize) => new(availableSize.Width, 18);

    protected override void OnRender(DrawingContext dc)
    {
        _pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        double x = 4;
        foreach (var (label, brush) in _items)
        {
            dc.DrawRectangle(brush, null, new Rect(x, 4, 10, 10));
            x += 12;
            var ft = new FormattedText(label,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, LegendTypeface, 9,
                new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)), _pixelsPerDip);
            dc.DrawText(ft, new Point(x, 4));
            x += ft.Width + 10;
        }
    }
}

// ── Host panel ────────────────────────────────────────────────────────────────

/// <summary>Host panel for the timeline: wraps the canvas in a ScrollViewer + zoom slider + legend.</summary>
internal sealed class StringTimelinePanel : Border
{
    private readonly StringTimelineView   _view   = new() { UseLayoutRounding = true };
    private readonly StringTimelineLegend _legend = new();
    private readonly ScrollViewer         _scroll;
    private          Slider               _zoomSlider = null!;

    public StringTimelinePanel()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });          // toolbar
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // scroll
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });          // legend

        // ── Toolbar ──
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
        resetBtn.SetResourceReference(StyleProperty,                 "PanelIconButtonStyle");
        resetBtn.SetResourceReference(Control.ForegroundProperty,    "Panel_ToolbarForegroundBrush");
        resetBtn.Click += (_, _) => _zoomSlider.Value = 1;

        DockPanel.SetDock(zoomLbl,    Dock.Left);
        DockPanel.SetDock(_zoomSlider, Dock.Left);
        DockPanel.SetDock(resetBtn,   Dock.Left);
        zoomRow.Children.Add(zoomLbl);
        zoomRow.Children.Add(_zoomSlider);
        zoomRow.Children.Add(resetBtn);

        // ── ScrollViewer ──
        _scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            Content = _view,
        };
        _scroll.ScrollChanged += OnScrollChanged;

        // Ctrl+Scroll → zoom centred on mouse
        _view.MouseWheel += OnViewMouseWheel;

        // ── Legend ──
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
    {
        _view.Zoom = e.NewValue;
    }

    private void OnScrollChanged(object _, ScrollChangedEventArgs _2)
    {
        _view.ViewportOffsetX = _scroll.HorizontalOffset;
        _view.ViewportWidth   = _scroll.ViewportWidth;
        _view.InvalidateVisual();
    }

    private void OnViewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;

        double anchorRatio = _view.ZoomAnchorRatio;
        double oldZoom     = _zoomSlider.Value;
        double newZoom     = Math.Clamp(oldZoom + e.Delta / 120.0 * 2, 1, 100);
        _zoomSlider.Value  = newZoom;

        // Restore scroll so the point under the mouse stays stationary.
        // After the debounce fires and the element re-measures, scroll accordingly.
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
