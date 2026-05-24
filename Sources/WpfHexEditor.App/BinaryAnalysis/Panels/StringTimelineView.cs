// Project     : WpfHexEditor.App
// File        : StringTimelineView.cs
// Description : Custom FrameworkElement rendering string runs as horizontal offset bands.
//               X-axis = file offset; each run = colored rectangle proportional to its length.
//               Colors: encoding palette fallback; Kind overrides when Kind != None.
//               Opacity: proportional to string length (longer = more opaque).
//               Density heatmap overlay drawn below runs.
//               Viewport-culled per paint — binary search on offset-sorted _drawList.
// Architecture: Pure DrawingContext render; zoom via Slider; scroll via ScrollViewer wrapping this element.
//               Tooltip uses a Popup (not ToolTip) repositioned on every MouseMove so it
//               tracks the cursor continuously. WPF ToolTip with PlacementMode.Mouse only
//               positions itself at the moment IsOpen transitions false→true and stays frozen.
//
// Perf contract:
//   RebuildLayout — O(n log n), offloaded to Task.Run; UI thread blocked only for the final
//                   _rowMap/_drawList swap via Dispatcher.InvokeAsync.
//   Zoom/Scroll   — O(1): only InvalidateMeasure/Visual. No rebuild, no allocs.
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
    private static readonly Typeface        RulerTypeface    = new("Consolas");
    private static readonly Pen             RulerPen         = FreezePen(new Pen(RulerTextBrush, 0.5));

    // Tooltip chrome — static frozen so they are shared across all StringTimelineView instances.
    private static readonly FontFamily      TooltipMonoFamily  = new("Consolas");
    private static readonly SolidColorBrush TooltipBgBrush     = FreezeB(new SolidColorBrush(Color.FromArgb(0xF2, 0x1E, 0x1E, 0x1E)));
    private static readonly SolidColorBrush TooltipBorderBrush = FreezeB(new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)));
    private static readonly SolidColorBrush TooltipOffsetFg    = FreezeB(new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00)));
    private static readonly SolidColorBrush TooltipValueFg     = FreezeB(new SolidColorBrush(Color.FromRgb(0xCE, 0xCE, 0xCE)));
    private static readonly SolidColorBrush TooltipMetaFg      = FreezeB(new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0x77)));

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
        foreach (StringEncoding enc in Enum.GetValues<StringEncoding>())
            if (EncodingPalette.Brushes.TryGetValue(enc, out var b)) list.Add(b);
        foreach (StringKind kind in Enum.GetValues<StringKind>())
            if (kind != StringKind.None && EncodingPalette.KindBrushes.TryGetValue(kind, out var b)) list.Add(b);
        FallbackBrushId = list.Count;
        list.Add(EncodingPalette.FallbackBrush);
        BrushById = [.. list];

        BrushIndex = BuildBrushIndex();
    }

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
                    int ki     = BrushById.Length - 1;
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

    private const double RowHeight    = 12.0;
    private const double RulerHeight  = 18.0;
    private const int    HeatBuckets  = 512;
    private const double MinOpacity   = 0.35;
    private const double OpacityRange = 0.65;
    private const double MinRunNorm   = 0.0008;
    private const int    MaxRows      = 120;
    private const double MinRunPx     = 2.0;
    private const double MergeGapNorm = 0.0005;

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

    private readonly System.Windows.Threading.DispatcherTimer _zoomDebounce;
    private readonly System.Windows.Threading.DispatcherTimer _refreshDebounce;

    internal double ViewportOffsetX { get; set; }
    internal double ViewportWidth   { get; set; } = double.MaxValue;

    private StringExtractionViewModel? _vm;
    private long _bufferLength;

    // Not readonly: swapped atomically from Dispatcher.InvokeAsync after each background rebuild.
    private List<(StringRun run, int row, double xNorm, double rwNorm)> _rowMap  = [];
    private List<(int row, int brushId, double x1, double x2, float opacity)>    _drawList = [];
    private float[] _densityMap = [];
    private int _rowCount;

    // Not readonly: replaced on every rebuild to cancel the previous in-flight Task.Run.
    private CancellationTokenSource _rebuildCts = new(); // CS0649 suppressed — intentionally reassigned

    // ── Tooltip — Popup tracked on MouseMove ──────────────────────────────────
    // We use a Popup instead of ToolTip: ToolTip.PlacementMode.Mouse only positions
    // itself at the instant IsOpen flips to true and stays frozen until closed.
    private readonly Popup      _tooltipPopup;
    private readonly TextBlock  _tooltipOffset;
    private readonly TextBlock  _tooltipEncoding;
    private readonly TextBlock  _tooltipKind;
    private readonly TextBlock  _tooltipValue;
    private readonly TextBlock  _tooltipLength;
    private readonly Border     _tooltipEncodingBadge;
    private readonly Border     _tooltipKindBadge;
    private StringRun?          _tooltipRun;        // last run shown — avoids rebuild on same run

    public Action<StringRun>? RunSelected { get; set; }

    // ── Constructor ───────────────────────────────────────────────────────────

    public StringTimelineView()
    {
        _zoomDebounce = new System.Windows.Threading.DispatcherTimer
            { Interval = TimeSpan.FromMilliseconds(16) };
        _zoomDebounce.Tick += (_, _) =>
        {
            _zoomDebounce.Stop();
            InvalidateMeasure();
            InvalidateVisual();
        };

        _refreshDebounce = new System.Windows.Threading.DispatcherTimer
            { Interval = TimeSpan.FromMilliseconds(250) };
        _refreshDebounce.Tick += (_, _) => { _refreshDebounce.Stop(); DoRebuildAndRender(); };

        (_tooltipPopup, _tooltipOffset, _tooltipEncoding,
         _tooltipKind, _tooltipValue, _tooltipLength,
         _tooltipEncodingBadge, _tooltipKindBadge) = BuildTooltipPopup();
    }

    // ── Rich tooltip construction ─────────────────────────────────────────────

    private static (Popup popup,
                    TextBlock offsetTb,
                    TextBlock encodingTb,
                    TextBlock kindTb,
                    TextBlock valueTb,
                    TextBlock lengthTb,
                    Border encodingBadge,
                    Border kindBadge)
        BuildTooltipPopup()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (int i = 0; i < 5; i++)
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var offsetTb = new TextBlock
        {
            FontFamily = TooltipMonoFamily,
            FontSize   = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = TooltipOffsetFg,
            Margin     = new Thickness(0, 0, 0, 4),
        };
        Grid.SetRow(offsetTb, 0);
        Grid.SetColumnSpan(offsetTb, 2);
        grid.Children.Add(offsetTb);

        var encodingBadge = MakeTooltipBadge(out var encodingTb);
        Grid.SetRow(encodingBadge, 1);
        Grid.SetColumn(encodingBadge, 0);
        grid.Children.Add(encodingBadge);

        var kindBadge = MakeTooltipBadge(out var kindTb);
        kindBadge.Margin = new Thickness(0, 2, 0, 0);
        Grid.SetRow(kindBadge, 2);
        Grid.SetColumn(kindBadge, 0);
        grid.Children.Add(kindBadge);

        var valueTb = new TextBlock
        {
            FontFamily   = TooltipMonoFamily,
            FontSize     = 10,
            Foreground   = TooltipValueFg,
            Margin       = new Thickness(0, 4, 0, 0),
            MaxWidth     = 360,
            TextWrapping = TextWrapping.Wrap,
        };
        Grid.SetRow(valueTb, 3);
        Grid.SetColumnSpan(valueTb, 2);
        grid.Children.Add(valueTb);

        var lengthTb = new TextBlock
        {
            FontSize   = 9,
            Foreground = TooltipMetaFg,
            Margin     = new Thickness(0, 3, 0, 0),
        };
        Grid.SetRow(lengthTb, 4);
        Grid.SetColumnSpan(lengthTb, 2);
        grid.Children.Add(lengthTb);

        var chrome = new Border
        {
            Background      = TooltipBgBrush,
            BorderBrush     = TooltipBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(4),
            Padding         = new Thickness(8, 6, 8, 7),
            Effect          = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color       = Colors.Black,
                Opacity     = 0.55,
                BlurRadius  = 6,
                ShadowDepth = 2,
            },
            Child = grid,
        };

        var popup = new Popup
        {
            AllowsTransparency = true,
            Placement          = PlacementMode.AbsolutePoint,
            StaysOpen          = false,
            IsHitTestVisible   = false,
            PopupAnimation     = PopupAnimation.None,
            Child              = chrome,
        };

        return (popup, offsetTb, encodingTb, kindTb, valueTb, lengthTb, encodingBadge, kindBadge);
    }

    private static Border MakeTooltipBadge(out TextBlock label)
    {
        label = new TextBlock
        {
            FontSize   = 9,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
        };
        return new Border
        {
            CornerRadius = new CornerRadius(3),
            Padding      = new Thickness(5, 1, 5, 1),
            Child        = label,
        };
    }

    // Updates the tooltip content for the given run (skips rebuild if same run).
    private void UpdateTooltipContent(StringRun run)
    {
        if (ReferenceEquals(_tooltipRun, run)) return;
        _tooltipRun = run;

        _tooltipOffset.Text = $"Offset  0x{run.Offset:X8}  ({run.Offset:N0})";

        var encBrush = EncodingPalette.Brushes.TryGetValue(run.Encoding, out var eb)
            ? eb : EncodingPalette.FallbackBrush;
        _tooltipEncodingBadge.Background = encBrush;
        _tooltipEncoding.Text            = run.Encoding.ToString();

        bool hasKind = run.Kind != StringKind.None;
        _tooltipKindBadge.Visibility = hasKind ? Visibility.Visible : Visibility.Collapsed;
        if (hasKind)
        {
            var kindBrush = EncodingPalette.KindBrushes.TryGetValue(run.Kind, out var kb)
                ? kb : EncodingPalette.FallbackBrush;
            _tooltipKindBadge.Background = kindBrush;
            _tooltipKind.Text            = run.Kind.ToString();
        }

        _tooltipValue.Text = TruncateValue(run.Value, 120);

        _tooltipLength.Text = run.ReadabilityScore > 0f
            ? $"Length {run.Length}  ·  Readability {run.ReadabilityScore:P0}"
            : $"Length {run.Length}";
    }

    // Positions the popup 14px below+right of the screen cursor.
    private void PositionTooltip(MouseEventArgs e)
    {
        var screenPos = PointToScreen(e.GetPosition(this));
        _tooltipPopup.HorizontalOffset = screenPos.X + 14;
        _tooltipPopup.VerticalOffset   = screenPos.Y + 14;
    }

    // ── Attach / Refresh ──────────────────────────────────────────────────────

    private void DoRebuildAndRender()
    {
        // Cancel any in-flight rebuild — its result would be stale.
        _rebuildCts.Cancel();
        _rebuildCts.Dispose();
        _rebuildCts = new CancellationTokenSource();
        var cts = _rebuildCts;

        // Capture inputs on the UI thread before handing off to the ThreadPool.
        var snapshot     = _vm?.GetAllRunsSnapshot() ?? [];
        var bufferLength = _vm?.LastBufferLength ?? 0;

        if (snapshot.Length == 0 || bufferLength <= 0)
        {
            _bufferLength = bufferLength;
            _rowMap.Clear();
            _drawList.Clear();
            _densityMap = [];
            _rowCount   = 0;
            InvalidateMeasure();
            InvalidateVisual();
            return;
        }

        _bufferLength = bufferLength;

        Task.Run(() => RebuildLayout(snapshot, bufferLength, cts.Token), cts.Token)
            .ContinueWith(t =>
            {
                if (t.IsCanceled || t.IsFaulted || cts.IsCancellationRequested) return;
                Dispatcher.InvokeAsync(() =>
                {
                    if (cts.IsCancellationRequested) return;
                    (_rowMap, _drawList, _densityMap, _rowCount) = t.Result;
                    InvalidateMeasure();
                    InvalidateVisual();
                }, System.Windows.Threading.DispatcherPriority.Render);
            }, TaskScheduler.Default);
    }

    public void Attach(StringExtractionViewModel vm)
    {
        if (_vm is { } prev) prev.PropertyChanged -= OnVmChanged;
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

    // ── RebuildLayout: O(n log n), pure — safe on ThreadPool ──────────────────

    private static (List<(StringRun run, int row, double xNorm, double rwNorm)> rowMap,
                    List<(int row, int brushId, double x1, double x2, float opacity)> drawList,
                    float[] densityMap,
                    int rowCount)
        RebuildLayout(StringRun[] runs, long bufferLength, CancellationToken ct)
    {
        var rowMap  = new List<(StringRun run, int row, double xNorm, double rwNorm)>(runs.Length);
        var density = new float[HeatBuckets];

        double invBuffer = 1.0 / bufferLength;
        var pq      = new PriorityQueue<int, double>();
        int nextRow = 0;

        foreach (var run in runs)
        {
            if (ct.IsCancellationRequested) return ([], [], [], 0);

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
            rowMap.Add((run, row, xNorm, rwNorm));
            density[Math.Clamp((int)(xNorm * HeatBuckets), 0, HeatBuckets - 1)] += 1f;
        }

        int rowCount = Math.Max(1, Math.Min(nextRow, MaxRows));

        float dmax = 0f;
        foreach (var v in density) if (v > dmax) dmax = v;
        float[] densityMap = [];
        if (dmax > 0f)
        {
            densityMap = new float[HeatBuckets];
            for (int i = 0; i < HeatBuckets; i++) densityMap[i] = density[i] / dmax;
        }

        var drawList = BuildDrawList(rowMap, ct);
        return (rowMap, drawList, densityMap, rowCount);
    }

    private static List<(int row, int brushId, double x1, double x2, float opacity)>
        BuildDrawList(List<(StringRun run, int row, double xNorm, double rwNorm)> rowMap, CancellationToken ct)
    {
        var result = new List<(int row, int brushId, double x1, double x2, float opacity)>(rowMap.Count);
        if (rowMap.Count == 0) return result;

        var tmp = new List<(int row, int brushId, double x1, double x2, float opacity)>(rowMap.Count);
        foreach (var (run, row, xNorm, rwNorm) in rowMap)
        {
            int brushId   = BrushIndex.TryGetValue((run.Encoding, run.Kind), out var bi) ? bi : FallbackBrushId;
            float opacity = (float)(MinOpacity + OpacityRange * Math.Clamp((run.Length - 4) / 56.0, 0.0, 1.0));
            tmp.Add((row, brushId, xNorm, xNorm + rwNorm, opacity));
        }

        tmp.Sort(static (a, b) =>
        {
            int c = a.row.CompareTo(b.row);
            if (c != 0) return c;
            c = a.brushId.CompareTo(b.brushId);
            if (c != 0) return c;
            return a.x1.CompareTo(b.x1);
        });

        var cur = tmp[0];
        for (int i = 1; i < tmp.Count; i++)
        {
            if (ct.IsCancellationRequested) return [];
            var next = tmp[i];
            if (next.row == cur.row && next.brushId == cur.brushId && next.x1 <= cur.x2 + MergeGapNorm)
                cur = cur with { x2 = Math.Max(cur.x2, next.x2), opacity = Math.Max(cur.opacity, next.opacity) };
            else
            {
                result.Add(cur);
                cur = next;
            }
        }
        result.Add(cur);
        return result;
    }

    // ── Layout ────────────────────────────────────────────────────────────────

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

    // ── Render ────────────────────────────────────────────────────────────────

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
        int startIdx = BinarySearchDrawStart(pixelW);
        double vpEnd = ViewportOffsetX + ViewportWidth;

        for (int i = startIdx; i < _drawList.Count; i++)
        {
            var (row, brushId, x1n, x2n, opacity) = _drawList[i];
            double x  = x1n * pixelW;
            double x2 = Math.Max(x + MinRunPx, x2n * pixelW);

            if (x > vpEnd) continue;
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

    private int BinarySearchDrawStart(double pixelW)
    {
        if (_drawList.Count == 0 || pixelW <= 0) return 0;
        double targetNorm = (ViewportOffsetX - MinRunPx) / pixelW;
        int lo = 0, hi = _drawList.Count - 1;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (_drawList[mid].x1 < targetNorm - 0.05) lo = mid + 1;
            else hi = mid;
        }
        return Math.Max(0, lo - 4);
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

    // ── Mouse ─────────────────────────────────────────────────────────────────

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hit = HitTestRun(e.GetPosition(this));
        if (hit is not null)
        {
            UpdateTooltipContent(hit);
            PositionTooltip(e);
            if (!_tooltipPopup.IsOpen)
            {
                _tooltipPopup.PlacementTarget = this;
                _tooltipPopup.IsOpen          = true;
            }
        }
        else
        {
            _tooltipRun          = null;
            _tooltipPopup.IsOpen = false;
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _tooltipRun          = null;
        _tooltipPopup.IsOpen = false;
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

// ── Legend ────────────────────────────────────────────────────────────────────

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

// ── Host panel ────────────────────────────────────────────────────────────────

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
            Text              = "Zoom:",
            FontSize          = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(0, 0, 4, 0),
        };
        zoomLbl.SetResourceReference(TextBlock.ForegroundProperty, "Panel_ToolbarForegroundBrush");

        _zoomSlider = new Slider
        {
            Minimum           = 1,
            Maximum           = 100,
            Value             = 1,
            Width             = 140,
            Height            = 16,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _zoomSlider.ValueChanged += OnZoomSliderChanged;

        var resetBtn = new Button
        {
            Content          = "1:1",
            FontSize         = 10,
            Padding          = new Thickness(4, 1, 4, 1),
            Margin           = new Thickness(4, 0, 0, 0),
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
