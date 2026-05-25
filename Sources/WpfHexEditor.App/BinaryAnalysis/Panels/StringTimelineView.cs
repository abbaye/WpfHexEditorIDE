// Project     : WpfHexEditor.App
// File        : StringTimelineView.cs
// Description : Custom FrameworkElement rendering string runs as horizontal offset bands.
//               X-axis = file offset; each run maps to a (row, col) cell in a fixed
//               MaxRows×GridCols grid. Colors: encoding palette; Kind overrides.
//               Opacity: proportional to string length. Density heatmap underlay.
// Architecture: Pure DrawingContext render; zoom via Slider; scroll via ScrollViewer.
//               Tooltip: Popup(PlacementMode.Relative, StaysOpen=true) — offsets are
//               element-local logical px; updating them on an open popup repositions
//               it without close/reopen (no flash, no DPI math).
//
// Perf contract:
//   BuildGrid  — O(n) scatter into GridCell[MaxRows×GridCols]; offloaded to Task.Run.
//   OnRender   — O(MaxRows×GridCols) = O(61 440) fixed, independent of run count.
//   Zoom/Scroll — O(1): only InvalidateMeasure/Visual, no rebuild.

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
        var list  = new List<SolidColorBrush>();
        var index = new Dictionary<(StringEncoding, StringKind), int>();

        // Phase 1: one entry per encoding; any (enc, kind) without a Kind override maps here.
        foreach (StringEncoding enc in Enum.GetValues<StringEncoding>())
        {
            if (!EncodingPalette.Brushes.TryGetValue(enc, out var b)) continue;
            int id = list.Count;
            list.Add(b);
            foreach (StringKind kind in Enum.GetValues<StringKind>())
                index.TryAdd((enc, kind), id);
        }

        // Phase 2: Kind overrides win — overwrite the encoding default for matching pairs.
        foreach (StringKind kind in Enum.GetValues<StringKind>())
        {
            if (kind == StringKind.None) continue;
            if (!EncodingPalette.KindBrushes.TryGetValue(kind, out var b)) continue;
            int id = list.Count;
            list.Add(b);
            foreach (StringEncoding enc in Enum.GetValues<StringEncoding>())
                index[(enc, kind)] = id;
        }

        FallbackBrushId = list.Count;
        list.Add(EncodingPalette.FallbackBrush);
        BrushById  = [.. list];
        BrushIndex = index;
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

    // ── Grid constants for O(n) low-cost layout ───────────────────────────────
    // Grid is MaxRows × GridCols cells. Each cell stores the dominant brushId and
    // max opacity of all runs that map to it. Rebuild = O(n) scatter; render =
    // O(MaxRows × GridCols) = O(61 440) fixed regardless of run count.
    private const int GridCols = 512;   // same as HeatBuckets — one cell per heat bucket per row

    private static GridCell[] InitGrid()
    {
        var g = new GridCell[MaxRows * GridCols];
        Array.Fill(g, GridCell.Empty);
        return g;
    }

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

    // The unzoomed viewport width, set by StringTimelinePanel.OnScrollChanged.
    // MeasureOverride uses this as the stable base so zoom = 1 ↔ one screen width.
    // Never multiplied by _zoom — avoids the ActualWidth feedback loop.
    internal double ViewportBaseWidth { get; set; } = 200;

    private StringExtractionViewModel? _vm;
    private long _bufferLength;

    // ── Grid cell: dominant brush + max opacity for one (row, col) slot ─────────
    // Empty sentinel: BrushId = -1 (default int = 0 is valid brush index 0, so we
    // cannot rely on zero-init; Array.Fill with Empty is required after allocation).
    private readonly struct GridCell
    {
        public static readonly GridCell Empty = new(-1, 0f);
        public int   BrushId { get; }
        public float Opacity { get; }
        public bool  HasRun  => BrushId >= 0;
        public GridCell(int brushId, float opacity) { BrushId = brushId; Opacity = opacity; }
    }

    // Swapped atomically from Dispatcher.InvokeAsync after each background rebuild.
    // Grid is flat [row * GridCols + col] for cache-friendly row-major access.
    private GridCell[]  _grid       = InitGrid();
    private float[]     _densityMap = [];
    private int         _rowCount;
    // Sparse hit-map: col index → list of runs that landed in that column.
    // Used for click/hover hit-testing (O(MaxRows) per column, not O(n)).
    private List<(StringRun run, int row, double xNorm, double rwNorm)>[] _hitCols
        = new List<(StringRun, int, double, double)>[GridCols];

    // Not readonly: replaced on every rebuild to cancel the previous in-flight Task.Run.
    private CancellationTokenSource _rebuildCts = new();

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

    private (Popup popup,
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
            Placement          = PlacementMode.Relative,  // offsets are element-local logical px; no DPI math needed
            StaysOpen          = true,                     // OnMouseLeave closes; StaysOpen=false flashes with IsHitTestVisible=false
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

    // Positions the popup relative to the element's top-left corner (PlacementMode.Relative).
    // Changing HorizontalOffset/VerticalOffset on an already-open Relative popup repositions
    // it immediately without closing/reopening — no flash, no DPI math needed.
    private void PositionTooltip(MouseEventArgs e)
    {
        var pos = e.GetPosition(this);
        _tooltipPopup.HorizontalOffset = pos.X + 14;
        _tooltipPopup.VerticalOffset   = pos.Y + 14;
    }

    // ── Attach / Refresh ──────────────────────────────────────────────────────

    private void DoRebuildAndRender()
    {
        _rebuildCts.Cancel();
        _rebuildCts.Dispose();
        _rebuildCts = new CancellationTokenSource();
        var cts = _rebuildCts;

        var snapshot     = _vm?.GetAllRunsSnapshot() ?? [];
        var bufferLength = _vm?.LastBufferLength ?? 0;

        if (snapshot.Length == 0 || bufferLength <= 0)
        {
            _bufferLength = bufferLength;
            Array.Fill(_grid, GridCell.Empty);
            _densityMap = [];
            _rowCount   = 0;
            Array.Clear(_hitCols);
            InvalidateMeasure();
            InvalidateVisual();
            return;
        }

        _bufferLength = bufferLength;

        Task.Run(() => BuildGrid(snapshot, bufferLength, cts.Token), cts.Token)
            .ContinueWith(t =>
            {
                if (t.IsCanceled || t.IsFaulted || cts.IsCancellationRequested) return;
                Dispatcher.InvokeAsync(() =>
                {
                    if (cts.IsCancellationRequested) return;
                    var r = t.Result;
                    _grid       = r.Grid;
                    _densityMap = r.DensityMap;
                    _rowCount   = r.RowCount;
                    _hitCols    = r.HitCols;
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
        IsVisibleChanged -= OnVisibleChanged;
        IsVisibleChanged += OnVisibleChanged;
        Refresh();
    }

    // When the timeline tab becomes visible (user switches to it), the element may have
    // missed an InvalidateVisual that fired while it was hidden (ActualWidth=0 at that time).
    // Force a redraw so the latest grid data is always shown on first activation.
    private void OnVisibleChanged(object _, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true && _rowCount > 0)
            InvalidateVisual();
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
        // Coordinates are normalized — size change only affects pixel rendering, not layout.
        // A simple InvalidateVisual is sufficient; no rebuild needed.
        if (e.WidthChanged && e.NewSize.Width > 0)
            InvalidateVisual();
    }

    public void Refresh()
    {
        _refreshDebounce.Stop();
        DoRebuildAndRender();
    }

    // ── BuildGrid: O(n) scatter — pure, safe on ThreadPool ───────────────────
    // Each run maps to exactly one (row, col) cell via deterministic hash.
    // Row  = brushId % MaxRows  — spreads encoding/kind types across rows.
    // Col  = (int)(xNorm * GridCols) clamped — x position bucket.
    // Cell stores dominant brush (last write wins for speed) + max opacity.
    // Total rebuild cost: O(n) scatter + O(MaxRows×GridCols) density norm.
    // OnRender cost: O(MaxRows×GridCols) = O(61 440) fixed, independent of n.

    private sealed class GridResult(
        GridCell[] grid,
        float[] densityMap,
        int rowCount,
        List<(StringRun run, int row, double xNorm, double rwNorm)>[] hitCols)
    {
        public GridCell[]  Grid       { get; } = grid;
        public float[]     DensityMap { get; } = densityMap;
        public int         RowCount   { get; } = rowCount;
        public List<(StringRun run, int row, double xNorm, double rwNorm)>[] HitCols { get; } = hitCols;
    }

    private static GridResult BuildGrid(StringRun[] runs, long bufferLength, CancellationToken ct)
    {
        var grid    = new GridCell[MaxRows * GridCols];
        Array.Fill(grid, GridCell.Empty);
        var density = new float[HeatBuckets];
        var hitCols = new List<(StringRun, int, double, double)>[GridCols];

        double invBuffer = 1.0 / bufferLength;
        int usedRows = 0;

        foreach (var run in runs)
        {
            if (ct.IsCancellationRequested)
                return new GridResult(grid, [], 0, hitCols);

            double xNorm  = run.Offset * invBuffer;
            double rwNorm = Math.Max(MinRunNorm, run.Length * invBuffer);

            int brushId = BrushIndex.TryGetValue((run.Encoding, run.Kind), out var bi) ? bi : FallbackBrushId;
            // Spread rows by brush so different encodings/kinds land on different rows.
            int row = brushId % MaxRows;
            int col = Math.Clamp((int)(xNorm * GridCols), 0, GridCols - 1);

            float opacity = (float)(MinOpacity + OpacityRange * Math.Clamp((run.Length - 4) / 56.0, 0.0, 1.0));

            ref var cell = ref grid[row * GridCols + col];
            // Brush: last writer wins (most recent run per cell). Opacity: keep max.
            cell = new GridCell(brushId, Math.Max(cell.Opacity, opacity));

            density[col % HeatBuckets] += 1f;

            // Hit-map: store in column bucket for fast hover lookup.
            (hitCols[col] ??= new List<(StringRun, int, double, double)>(4))
                .Add((run, row, xNorm, rwNorm));

            if (row >= usedRows) usedRows = row + 1;
        }

        int rowCount = Math.Min(usedRows, MaxRows);

        float dmax = 0f;
        foreach (var v in density) if (v > dmax) dmax = v;
        float[] densityMap = [];
        if (dmax > 0f)
        {
            densityMap = new float[HeatBuckets];
            for (int i = 0; i < HeatBuckets; i++) densityMap[i] = density[i] / dmax;
        }

        return new GridResult(grid, densityMap, rowCount, hitCols);
    }

    // ── Layout ────────────────────────────────────────────────────────────────

    protected override Size MeasureOverride(Size availableSize)
    {
        // Use the stable viewport base width (set by ScrollViewer's ViewportWidth on
        // each ScrollChanged), never ActualWidth — ActualWidth = baseW * zoom and would
        // cause MeasureOverride to apply zoom a second time (zoom²) on the next pass.
        double baseW = ViewportBaseWidth > 0 ? ViewportBaseWidth : 200;
        double w     = Math.Max(baseW, 1) * _zoom;
        double h     = RulerHeight + _rowCount * RowHeight;
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
        if (_rowCount == 0 || _bufferLength <= 0 || w <= 0) return;

        // ActualWidth already reflects the zoom factor (MeasureOverride returns baseW * _zoom).
        // Drawing coordinates are element-local, so pixelW == ActualWidth — no second zoom multiply.
        double pixelW = w;
        DrawRuler(dc, w, pixelW, pixelsPerDip);
        DrawDensityHeatmap(dc, h, pixelW);
        DrawGrid(dc, h, pixelW);
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

    // O(MaxRows × GridCols) = O(61 440) — fixed cost regardless of run count.
    // Viewport-culled by column: skip cols outside [ViewportOffsetX, vpEnd].
    private void DrawGrid(DrawingContext dc, double h, double pixelW)
    {
        double cellW  = pixelW / GridCols;
        double rw     = Math.Max(MinRunPx, cellW);   // constant for this frame
        double vpEnd  = ViewportOffsetX + ViewportWidth;
        int    colMin = Math.Max(0,          (int)((ViewportOffsetX - cellW) / cellW));
        int    colMax = Math.Min(GridCols - 1, (int)((vpEnd + cellW)         / cellW));
        var    grid   = _grid;   // local snapshot — safe on UI thread after atomic swap

        for (int row = 0; row < _rowCount; row++)
        {
            double y     = RulerHeight + row * RowHeight + 1;
            double rectH = RowHeight - 2;
            int    rowOff = row * GridCols;

            for (int col = colMin; col <= colMax; col++)
            {
                ref readonly var cell = ref grid[rowOff + col];
                if (!cell.HasRun) continue;

                double x = col * cellW;
                var brush = (uint)cell.BrushId < (uint)BrushById.Length
                    ? BrushById[cell.BrushId] : EncodingPalette.FallbackBrush;

                if (cell.Opacity >= 1f)
                    dc.DrawRectangle(brush, null, new Rect(x, y, rw, rectH));
                else
                {
                    dc.PushOpacity(cell.Opacity);
                    dc.DrawRectangle(brush, null, new Rect(x, y, rw, rectH));
                    dc.Pop();
                }
            }
        }
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
        if (_bufferLength <= 0 || ActualWidth <= 0 || _rowCount == 0) return null;
        if (pos.Y < RulerHeight) return null;

        double pixelW  = ActualWidth * _zoom;
        double cellW   = pixelW / GridCols;
        // Check the clicked column and its immediate neighbours for tolerance.
        int    colHit  = Math.Clamp((int)(pos.X / cellW), 0, GridCols - 1);

        StringRun? best     = null;
        double     bestDist = double.MaxValue;
        var        hitCols  = _hitCols;

        for (int col = Math.Max(0, colHit - 1); col <= Math.Min(GridCols - 1, colHit + 1); col++)
        {
            var list = hitCols[col];
            if (list is null) continue;
            foreach (var (run, _, xNorm, rwNorm) in list)
            {
                double x  = xNorm * pixelW;
                double rw = Math.Max(MinRunPx, rwNorm * pixelW);
                if (pos.X >= x - 2 && pos.X <= x + rw + 2)
                {
                    double dist = Math.Abs(pos.X - (x + rw / 2));
                    if (dist < bestDist) { best = run; bestDist = dist; }
                }
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
        Loaded += (_, _) =>
        {
            // Bootstrap ViewportBaseWidth before the first ScrollChanged fires.
            if (_scroll.ViewportWidth > 0)
                _view.ViewportBaseWidth = _scroll.ViewportWidth;
        };

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

    private void OnScrollChanged(object _, ScrollChangedEventArgs e)
    {
        const double Eps = 0.5;
        if (Math.Abs(_view.ViewportOffsetX - _scroll.HorizontalOffset) < Eps &&
            Math.Abs(_view.ViewportWidth   - _scroll.ViewportWidth)    < Eps) return;

        _view.ViewportOffsetX = _scroll.HorizontalOffset;
        _view.ViewportWidth   = _scroll.ViewportWidth;

        // ViewportBaseWidth is the unzoomed reference width for MeasureOverride.
        // Update it only when the viewport physically resizes (not on horizontal scroll),
        // so the base stays stable while the user pans across the zoomed content.
        if (_scroll.ViewportWidth > 0 && Math.Abs(e.ViewportWidthChange) > Eps)
            _view.ViewportBaseWidth = _scroll.ViewportWidth;

        _view.InvalidateVisual();
    }

    private void OnViewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;

        double cursorX = e.GetPosition(_view).X;

        // Anchor: normalised [0,1] position within the current content (not pixel).
        double anchorNorm = _view.ViewportBaseWidth > 0 && _view.Zoom > 0
            ? (_scroll.HorizontalOffset + cursorX) / (_view.ViewportBaseWidth * _view.Zoom)
            : 0.0;

        _zoomSlider.Value = Math.Clamp(_zoomSlider.Value + e.Delta / 120.0 * 2, 1, 100);

        // After the slider sets the new zoom, the measure hasn't run yet.
        // Compute the target scroll from the known new zoom so the cursor stays fixed.
        double newZoom = _view.Zoom;   // already clamped by the Zoom setter
        _view.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
        {
            double newExtent = _view.ViewportBaseWidth * newZoom;
            double targetX   = anchorNorm * newExtent - cursorX;
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
