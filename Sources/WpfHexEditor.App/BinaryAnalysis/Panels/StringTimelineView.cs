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
    private static readonly Typeface        RulerTypeface  = new("Consolas");
    private static readonly Pen             RulerPen       = FreezePen(new Pen(RulerTextBrush, 0.5));

    // Pre-built heatmap brush pool: 64 quantized alpha levels avoid per-bucket PushOpacity/Pop.
    // Alpha range: HeatBrush base color (R=FF, G=A0, B=00) with alpha = [0..0x72] (HeatMaxAlpha).
    private const byte     HeatMaxAlpha   = 0x72;
    private const int      HeatAlphaLevels = 64;
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

    private static SolidColorBrush FreezeB(SolidColorBrush b) { b.Freeze(); return b; }
    private static Pen FreezePen(Pen p) { p.Freeze(); return p; }

    private const double RowHeight    = 12.0;
    private const double RulerHeight  = 18.0;
    private const double MinRunWidth  = 2.0;
    private const int    HeatBuckets  = 512;
    private const double MinOpacity   = 0.35;  // opacity floor: shortest strings (≤4 chars)
    private const double OpacityRange = 0.65;  // ramp over chars 4–60

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
    // Coalesces TotalCount-triggered refreshes during streaming scan — avoids O(n²) rebuild per run found.
    private readonly System.Windows.Threading.DispatcherTimer _refreshDebounce;

    // Set by StringTimelinePanel on scroll change — enables X-axis culling in OnRender.

    internal double ViewportOffsetX { get; set; }
    internal double ViewportWidth   { get; set; } = double.MaxValue;

    private StringExtractionViewModel? _vm;
    private long _bufferLength;

    // Layout cache — rebuilt in Refresh(), consumed by MeasureOverride/OnRender/HitTestRun.
    private List<(StringRun run, int row, double x, double rw)> _rowMap = [];
    private int _rowCount;

    // Density heatmap: one float per bucket = run-count density, normalized to [0,1].
    private float[] _densityMap = [];

    // WPF ToolTip — updated on hover; _hoveredOffset guards against redundant string-format per pixel.
    private readonly ToolTip _tooltip;
    private long _hoveredOffset = long.MinValue;

    public Action<StringRun>? RunSelected { get; set; }

    public StringTimelineView()
    {
        _zoomDebounce = new System.Windows.Threading.DispatcherTimer
            { Interval = TimeSpan.FromMilliseconds(30) };
        _zoomDebounce.Tick += (_, _) => { _zoomDebounce.Stop(); DoRebuildAndRender(); };

        _refreshDebounce = new System.Windows.Threading.DispatcherTimer
            { Interval = TimeSpan.FromMilliseconds(250) };
        _refreshDebounce.Tick += (_, _) => { _refreshDebounce.Stop(); DoRebuildAndRender(); };

        _tooltip = new ToolTip { Placement = PlacementMode.Mouse, HasDropShadow = true };
        ToolTip = _tooltip;
    }

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
        SizeChanged -= OnSizeChanged;  // guard: safe no-op on first attach
        SizeChanged += OnSizeChanged;
        Refresh();
    }

    private void OnVmChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(StringExtractionViewModel.TotalCount))
        {
            // Debounce: TotalCount fires on every run found during streaming scan.
            // Without this, RebuildLayout runs O(n) times during a 100k-run scan = O(n²) total.
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

    // O(n log n) greedy row-packing: min-heap keyed by row-end X.
    private void RebuildLayout(IEnumerable<StringRun>? runs)
    {
        _rowMap.Clear();
        _rowCount = 0;
        _densityMap = [];
        if (runs is null || _bufferLength <= 0) return;

        double w = Math.Max(ActualWidth > 0 ? ActualWidth : DesiredSize.Width, 1) * _zoom;
        if (w <= 0) return;
        double scale = w / _bufferLength;

        var pq      = new PriorityQueue<int, double>();
        int nextRow = 0;
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

            int bucket = Math.Clamp((int)(x / w * HeatBuckets), 0, HeatBuckets - 1);
            density[bucket] += 1f;
        }

        _rowCount = Math.Max(1, nextRow);

        float max = 0f;
        foreach (var v in density) if (v > max) max = v;
        if (max > 0f)
        {
            _densityMap = density;
            for (int i = 0; i < _densityMap.Length; i++) _densityMap[i] /= max;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // availableSize.Width is PositiveInfinity inside a ScrollViewer with Auto horizontal scroll.
        double baseW = double.IsInfinity(availableSize.Width)
            ? (ActualWidth > 0 ? ActualWidth : 200)
            : availableSize.Width;
        double w = Math.Max(baseW, 1) * _zoom;
        double h = RulerHeight + _rowCount * RowHeight;
        return new Size(w, h);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        double w = ActualWidth;
        double h = ActualHeight;
        dc.DrawRectangle(BackBrush, null, new Rect(0, 0, w, h));

        if (_rowMap.Count == 0 || _bufferLength <= 0 || w <= 0) return;

        double scale = w / _bufferLength;
        DrawRuler(dc, w, scale, pixelsPerDip);
        DrawDensityHeatmap(dc, w, h);
        DrawRuns(dc, w, h, scale);
    }

    private void DrawDensityHeatmap(DrawingContext dc, double w, double h)
    {
        if (_densityMap.Length == 0) return;
        double bucketW  = w / HeatBuckets;
        double contentH = h - RulerHeight;
        for (int i = 0; i < _densityMap.Length; i++)
        {
            float v = _densityMap[i];
            if (v < 0.05f) continue;
            double bx = i * bucketW;
            if (bx + bucketW < ViewportOffsetX || bx > ViewportOffsetX + ViewportWidth) continue;

            // Map normalized density to a pre-built frozen brush — avoids PushOpacity/Pop stack overhead.
            int brushIdx = (int)Math.Clamp(v * (HeatAlphaLevels - 1), 0, HeatAlphaLevels - 1);
            dc.DrawRectangle(HeatBrushes[brushIdx], null, new Rect(bx, RulerHeight, bucketW, contentH));
        }
    }

    private void DrawRuns(DrawingContext dc, double w, double h, double scale)
    {
        int startIdx = BinarySearchVisibleStart();

        for (int i = startIdx; i < _rowMap.Count; i++)
        {
            var (run, row, x, rw) = _rowMap[i];

            if (x > ViewportOffsetX + ViewportWidth) break;
            if (x + rw < ViewportOffsetX) continue;

            double y = RulerHeight + row * RowHeight;
            if (y > h) continue;

            SolidColorBrush brush;
            if (run.Kind != StringKind.None && EncodingPalette.KindBrushes.TryGetValue(run.Kind, out var kb))
                brush = kb;
            else if (EncodingPalette.Brushes.TryGetValue(run.Encoding, out var eb))
                brush = eb;
            else
                brush = EncodingPalette.FallbackBrush;

            double opacity = MinOpacity + OpacityRange * Math.Clamp((run.Length - 4) / 56.0, 0.0, 1.0);

            // Skip push/pop for fully opaque runs (max-length strings) to avoid drawing context stack cost.
            if (opacity >= 1.0)
            {
                dc.DrawRectangle(brush, null, new Rect(x, y + 1, rw, RowHeight - 2));
            }
            else
            {
                dc.PushOpacity(opacity);
                dc.DrawRectangle(brush, null, new Rect(x, y + 1, rw, RowHeight - 2));
                dc.Pop();
            }
        }
    }

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

    private void DrawRuler(DrawingContext dc, double w, double scale, double pixelsPerDip)
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
                FlowDirection.LeftToRight, RulerTypeface, 8, RulerTextBrush, pixelsPerDip);
            dc.DrawText(ft, new Point(x + 2, 3));
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var pos = e.GetPosition(this);
        var hit = HitTestRun(pos);
        if (hit is not null)
        {
            // Skip string allocation when still hovering the same run.
            if (hit.Offset != _hoveredOffset)
            {
                _hoveredOffset    = hit.Offset;
                _tooltip.Content  = $"0x{hit.Offset:X8}  [{hit.Encoding}]  {TruncateValue(hit.Value, 60)}";
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

    protected override Size MeasureOverride(Size availableSize) => new(availableSize.Width, 18);

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
    {
        _view.Zoom = e.NewValue;
    }

    private void OnScrollChanged(object _, ScrollChangedEventArgs _2)
    {
        // Guard: avoid redundant InvalidateVisual when scroll values haven't changed
        // (e.g. elastic overscroll bounce-back events that fire with the same offset).
        if (_view.ViewportOffsetX == _scroll.HorizontalOffset &&
            _view.ViewportWidth   == _scroll.ViewportWidth) return;

        _view.ViewportOffsetX = _scroll.HorizontalOffset;
        _view.ViewportWidth   = _scroll.ViewportWidth;
        _view.InvalidateVisual();
    }

    private void OnViewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;

        // Compute anchor ratio at the point under the mouse before zoom changes extent.
        double anchorRatio = _view.ActualWidth > 0
            ? ((MouseWheelEventArgs)e).GetPosition(_view).X / _view.ActualWidth
            : 0.0;

        _zoomSlider.Value = Math.Clamp(_zoomSlider.Value + e.Delta / 120.0 * 2, 1, 100);

        // After debounce fires and element re-measures, restore scroll to keep anchor stationary.
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
