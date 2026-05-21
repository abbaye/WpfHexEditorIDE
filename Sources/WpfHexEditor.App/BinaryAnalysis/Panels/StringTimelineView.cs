// Project     : WpfHexEditor.App
// File        : StringTimelineView.cs
// Description : Custom FrameworkElement rendering string runs as horizontal offset bands.
//               X-axis = file offset; each run = colored rectangle proportional to its length.
//               Clip-culled per paint — only draws runs inside the visible offset window.
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

    private static SolidColorBrush FreezeB(SolidColorBrush b) { b.Freeze(); return b; }

    private const double RowHeight   = 12.0;
    private const double RulerHeight = 18.0;
    private const double MinRunWidth = 2.0;

    // Zoom: 1.0 = whole file fits in ActualWidth; higher = expanded.
    private double _zoom = 1.0;
    public double Zoom
    {
        get => _zoom;
        set { _zoom = Math.Clamp(value, 1.0, 200.0); InvalidateMeasure(); InvalidateVisual(); }
    }

    private StringExtractionViewModel? _vm;
    private long _bufferLength;

    // Layout cache — rebuilt in Refresh(), consumed by MeasureOverride/OnRender/HitTestRun.
    private List<(StringRun run, int row, double x, double rw)> _rowMap = [];
    private int _rowCount;

    // Ruler pen — cached to avoid allocation per render.
    private static readonly Pen RulerPen = FreezePen(new Pen(RulerTextBrush, 0.5));
    private static Pen FreezePen(Pen p) { p.Freeze(); return p; }

    // Hover state for tooltip
    private StringRun? _hovered;
    private Point _hoveredPos;

    public Action<StringRun>? RunSelected { get; set; }

    public void Attach(StringExtractionViewModel vm)
    {
        if (_vm is not null) _vm.PropertyChanged -= OnVmChanged;
        _vm = vm;
        _vm.PropertyChanged += OnVmChanged;
        Refresh();
    }

    private void OnVmChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(StringExtractionViewModel.TotalCount))
            Refresh();
    }

    public void Refresh()
    {
        _bufferLength = _vm?.LastBufferLength ?? 0;
        RebuildLayout(_vm?.GetAllRuns());
        InvalidateMeasure();
        InvalidateVisual();
    }

    // Greedy row-packing — runs once per data change, result cached for render + hit-test.
    private void RebuildLayout(IEnumerable<StringRun>? runs)
    {
        _rowMap.Clear();
        _rowCount = 0;
        if (runs is null || _bufferLength <= 0) return;

        double w = Math.Max(ActualWidth > 0 ? ActualWidth : DesiredSize.Width, 1) * _zoom;
        if (w <= 0) return;
        double scale = w / _bufferLength;

        var rowEnds = new List<double>();
        foreach (var run in runs)
        {
            double x    = run.Offset * scale;
            double rw   = Math.Max(MinRunWidth, run.Length * scale);
            double xEnd = x + rw;
            int row = -1;
            for (int r = 0; r < rowEnds.Count; r++)
            {
                if (rowEnds[r] <= x) { row = r; rowEnds[r] = xEnd + 1; break; }
            }
            if (row < 0) { row = rowEnds.Count; rowEnds.Add(xEnd + 1); }
            _rowMap.Add((run, row, x, rw));
        }
        _rowCount = Math.Max(1, rowEnds.Count);
    }

    // ── Layout ────────────────────────────────────────────────────────────────

    protected override Size MeasureOverride(Size availableSize)
    {
        // availableSize.Width is PositiveInfinity when inside a ScrollViewer with Auto horizontal scroll.
        // Fall back to ActualWidth (already arranged) or a reasonable minimum so WPF doesn't throw.
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
        double w = ActualWidth;
        double h = ActualHeight;
        dc.DrawRectangle(BackBrush, null, new Rect(0, 0, w, h));

        if (_rowMap.Count == 0 || _bufferLength <= 0 || w <= 0) return;

        double scale = w / _bufferLength;
        DrawRuler(dc, w, scale);

        foreach (var (run, row, x, rw) in _rowMap)
        {
            double y = RulerHeight + row * RowHeight;
            if (y > h) continue;
            // Rescale x/rw from cached layout width to current ActualWidth
            double cx  = run.Offset * scale;
            double crw = Math.Max(MinRunWidth, run.Length * scale);
            var brush = EncodingPalette.Brushes.TryGetValue(run.Encoding, out var b) ? b : EncodingPalette.FallbackBrush;
            dc.DrawRectangle(brush, null, new Rect(cx, y + 1, crw, RowHeight - 2));
        }

        if (_hovered is not null)
        {
            var text = new FormattedText(
                $"0x{_hovered.Offset:X8}  [{_hovered.Encoding}]  {TruncateValue(_hovered.Value, 60)}",
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, RulerTypeface, 10, RulerTextBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            double tx = Math.Min(_hoveredPos.X + 4, w - text.Width - 4);
            double ty = Math.Max(RulerHeight, _hoveredPos.Y - 16);
            dc.DrawRectangle(RulerBrush, null, new Rect(tx - 2, ty - 1, text.Width + 4, text.Height + 2));
            dc.DrawText(text, new Point(tx, ty));
        }
    }

    private void DrawRuler(DrawingContext dc, double w, double scale)
    {
        dc.DrawRectangle(RulerBrush, null, new Rect(0, 0, w, RulerHeight));
        long tickStep = (long)Math.Pow(2, Math.Ceiling(Math.Log2(100.0 / scale)));
        tickStep = Math.Max(1, tickStep);
        for (long off = 0; off * scale <= w; off += tickStep)
        {
            double x = off * scale;
            dc.DrawLine(RulerPen, new Point(x, 0), new Point(x, RulerHeight));
            var ft = new FormattedText($"0x{off:X}", System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, RulerTypeface, 8, RulerTextBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(ft, new Point(x + 2, 3));
        }
    }

    // ── Mouse ─────────────────────────────────────────────────────────────────

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var pos = e.GetPosition(this);
        var hit = HitTestRun(pos);
        if (!ReferenceEquals(hit, _hovered))
        {
            _hovered    = hit;
            _hoveredPos = pos;
            InvalidateVisual();
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hovered is not null) { _hovered = null; InvalidateVisual(); }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var run = HitTestRun(e.GetPosition(this));
        if (run is not null) RunSelected?.Invoke(run);
    }

    // O(log n) column search: binary search by offset, then check only candidates in that X band.
    private StringRun? HitTestRun(Point pos)
    {
        if (_bufferLength <= 0 || ActualWidth <= 0 || _rowMap.Count == 0) return null;
        double scale = ActualWidth / _bufferLength;
        if (pos.Y < RulerHeight) return null;

        long targetOffset = (long)(pos.X / scale);
        StringRun? best   = null;
        double bestDist   = double.MaxValue;

        // _rowMap is insertion-ordered by ascending offset — binary-search for window
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

/// <summary>Host panel for the timeline: wraps the canvas in a ScrollViewer + zoom slider.</summary>
internal sealed class StringTimelinePanel : Border
{
    private readonly StringTimelineView _view = new() { UseLayoutRounding = true };
    private readonly ScrollViewer       _scroll;

    public StringTimelinePanel()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Zoom control row
        var zoomRow = new DockPanel { LastChildFill = false, Margin = new Thickness(4, 2, 4, 2) };
        zoomRow.SetResourceReference(DockPanel.BackgroundProperty, "Panel_ToolbarBrush");
        var zoomLbl = new TextBlock { Text = "Zoom:", FontSize = 10, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };
        zoomLbl.SetResourceReference(TextBlock.ForegroundProperty, "Panel_ToolbarForegroundBrush");
        var zoomSlider = new Slider
        {
            Minimum = 1, Maximum = 100, Value = 1,
            Width   = 140, Height = 16,
            VerticalAlignment = VerticalAlignment.Center,
        };
        zoomSlider.ValueChanged += (_, e) => _view.Zoom = e.NewValue;
        var resetBtn = new Button
        {
            Content = "1:1", FontSize = 10,
            Padding = new Thickness(4, 1, 4, 1), Margin = new Thickness(4, 0, 0, 0),
            FocusVisualStyle = null,
        };
        resetBtn.SetResourceReference(StyleProperty,                  "PanelIconButtonStyle");
        resetBtn.SetResourceReference(Control.ForegroundProperty, "Panel_ToolbarForegroundBrush");
        resetBtn.Click += (_, _) => { zoomSlider.Value = 1; };
        DockPanel.SetDock(zoomLbl,    Dock.Left);
        DockPanel.SetDock(zoomSlider, Dock.Left);
        DockPanel.SetDock(resetBtn,   Dock.Left);
        zoomRow.Children.Add(zoomLbl);
        zoomRow.Children.Add(zoomSlider);
        zoomRow.Children.Add(resetBtn);

        _scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            Content = _view,
        };
        _view.MouseWheel += (_, e) =>
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                zoomSlider.Value = Math.Clamp(zoomSlider.Value + e.Delta / 120.0 * 2, 1, 100);
                e.Handled = true;
            }
        };

        Grid.SetRow(zoomRow,  0);
        Grid.SetRow(_scroll,  1);
        root.Children.Add(zoomRow);
        root.Children.Add(_scroll);
        Child = root;
    }

    public void Attach(StringExtractionViewModel vm, Action<StringRun> onSelected)
    {
        _view.Attach(vm);
        _view.RunSelected = onSelected;
    }

    public void Refresh() => _view.Refresh();
}
