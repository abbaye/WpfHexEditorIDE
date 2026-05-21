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
    private static readonly SolidColorBrush BackBrush =
        Freeze(new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)));
    private static readonly SolidColorBrush RulerBrush =
        Freeze(new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)));
    private static readonly SolidColorBrush RulerTextBrush =
        Freeze(new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)));
    private static readonly Typeface RulerTypeface = new("Consolas");

    private static readonly IReadOnlyDictionary<StringEncoding, SolidColorBrush> EncodingBrushes =
        new Dictionary<StringEncoding, SolidColorBrush>
        {
            [StringEncoding.Tbl]          = Freeze(new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50))),
            [StringEncoding.TblDte]       = Freeze(new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50))),
            [StringEncoding.TblMte]       = Freeze(new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50))),
            [StringEncoding.Ascii]        = Freeze(new SolidColorBrush(Color.FromRgb(0x42, 0x8B, 0xCA))),
            [StringEncoding.Utf8]         = Freeze(new SolidColorBrush(Color.FromRgb(0x00, 0xBC, 0xD4))),
            [StringEncoding.Utf16Le]      = Freeze(new SolidColorBrush(Color.FromRgb(0x00, 0xBC, 0xD4))),
            [StringEncoding.Utf16Be]      = Freeze(new SolidColorBrush(Color.FromRgb(0x00, 0xBC, 0xD4))),
            [StringEncoding.Ebcdic]       = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00))),
            [StringEncoding.EbcdicNoSpec] = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00))),
            [StringEncoding.Latin1]       = Freeze(new SolidColorBrush(Color.FromRgb(0xAB, 0x47, 0xBC))),
        };
    private static readonly SolidColorBrush FallbackBrush =
        Freeze(new SolidColorBrush(Color.FromRgb(0x90, 0x90, 0x90)));

    private static SolidColorBrush Freeze(SolidColorBrush b) { b.Freeze(); return b; }

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
    private List<StringRun> _snapshot = [];
    private long _bufferLength;

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
        if (_vm is null) { _snapshot = []; _bufferLength = 0; InvalidateVisual(); return; }
        _snapshot    = _vm.GetAllRuns().ToList();
        _bufferLength = _vm.LastBufferLength;
        InvalidateMeasure();
        InvalidateVisual();
    }

    // ── Layout ────────────────────────────────────────────────────────────────

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = Math.Max(availableSize.Width, 1) * _zoom;
        int rows = CountRows();
        double h = RulerHeight + rows * RowHeight;
        return new Size(w, h);
    }

    private int CountRows()
    {
        if (_snapshot.Count == 0 || _bufferLength <= 0) return 0;
        // Pack rows greedily: each row tracks its rightmost pixel end.
        var rowEnds = new List<double>();
        double scale = DesiredSize.Width > 0 ? DesiredSize.Width / _bufferLength : 1.0;
        foreach (var run in _snapshot)
        {
            double x    = run.Offset   * scale;
            double xEnd = x + Math.Max(MinRunWidth, run.Length * scale);
            bool placed = false;
            for (int r = 0; r < rowEnds.Count; r++)
            {
                if (rowEnds[r] <= x) { rowEnds[r] = xEnd + 1; placed = true; break; }
            }
            if (!placed) rowEnds.Add(xEnd + 1);
        }
        return Math.Max(1, rowEnds.Count);
    }

    // ── Render ────────────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth;
        double h = ActualHeight;
        dc.DrawRectangle(BackBrush, null, new Rect(0, 0, w, h));

        if (_snapshot.Count == 0 || _bufferLength <= 0 || w <= 0) return;

        double scale = w / _bufferLength;

        DrawRuler(dc, w, scale);

        // Greedy row-packing — rowEnds tracks pixel end per row.
        var rowEnds = new List<double>();
        var rowMap  = new List<(StringRun run, int row, double x, double runW)>();

        foreach (var run in _snapshot)
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
            rowMap.Add((run, row, x, rw));
        }

        // Only draw rows inside the visible vertical clip
        double scrollTop    = 0; // parent ScrollViewer offset handled by WPF clipping
        double visibleBottom = h;

        foreach (var (run, row, x, rw) in rowMap)
        {
            double y = RulerHeight + row * RowHeight;
            if (y + RowHeight < scrollTop || y > visibleBottom) continue;

            var brush = EncodingBrushes.TryGetValue(run.Encoding, out var b) ? b : FallbackBrush;
            dc.DrawRectangle(brush, null, new Rect(x, y + 1, rw, RowHeight - 2));
        }

        // Draw hover tooltip inline
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
        if (_bufferLength <= 0) return;

        // Tick every ~100px, snapped to a power-of-2 offset step
        double pixelsPerTick = 100.0;
        long tickStep = (long)Math.Pow(2, Math.Ceiling(Math.Log2(pixelsPerTick / scale)));
        tickStep = Math.Max(1, tickStep);

        var pen = new Pen(RulerTextBrush, 0.5);
        for (long off = 0; off <= _bufferLength; off += tickStep)
        {
            double x = off * scale;
            if (x > w) break;
            dc.DrawLine(pen, new Point(x, 0), new Point(x, RulerHeight));
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
        var pos  = e.GetPosition(this);
        var hit  = HitTestRun(pos);
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

    private StringRun? HitTestRun(Point pos)
    {
        if (_bufferLength <= 0 || ActualWidth <= 0) return null;
        double scale = ActualWidth / _bufferLength;
        double y     = pos.Y - RulerHeight;
        if (y < 0) return null;
        int row = (int)(y / RowHeight);

        StringRun? best = null;
        double bestDist = double.MaxValue;
        foreach (var run in _snapshot)
        {
            double x  = run.Offset * scale;
            double rw = Math.Max(MinRunWidth, run.Length * scale);
            if (pos.X >= x && pos.X <= x + rw)
            {
                // approximate row match — return closest centre
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
        var zoomLbl = new TextBlock { Text = "Zoom:", FontSize = 10, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };
        zoomLbl.SetResourceReference(TextBlock.ForegroundProperty, "Panel_ToolbarForegroundBrush");
        var zoomSlider = new Slider
        {
            Minimum = 1, Maximum = 100, Value = 1,
            Width   = 140, Height = 16,
            VerticalAlignment = VerticalAlignment.Center,
        };
        zoomSlider.ValueChanged += (_, e) => _view.Zoom = e.NewValue;
        var resetBtn = new Button { Content = "1:1", FontSize = 10, Padding = new Thickness(4, 1, 4, 1), Margin = new Thickness(4, 0, 0, 0) };
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
