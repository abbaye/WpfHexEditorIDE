// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Controls/BreakpointGutterControl.cs
// Description:
//     VS-style breakpoint gutter strip rendered in the CodeEditor line-number area.
//     Click to toggle breakpoints; renders red circles (active), outlined circles
//     (disabled), and a yellow arrow for the current execution line.
// Architecture:
//     FrameworkElement with DrawingContext rendering (same as GutterControl).
//     Decoupled from DebuggerService via IBreakpointSource interface —
//     the App layer injects the callback, no direct dep on Core.Debugger.
// ==========================================================

using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace WpfHexEditor.Editor.CodeEditor.Controls;

/// <summary>
/// Minimal interface allowing the App layer to inject breakpoint toggle
/// without creating a compile-time dependency on IDebuggerService.
/// </summary>
public interface IBreakpointSource
{
    /// <summary>Returns true when a breakpoint is set at the given file/line.</summary>
    bool HasBreakpoint(string filePath, int line);

    /// <summary>Toggle a breakpoint at the given location (async fire-and-forget).</summary>
    void Toggle(string filePath, int line);
}

/// <summary>
/// Renders breakpoint markers in the left gutter of the CodeEditor.
/// Positioned at x=0 with a fixed width of <see cref="GutterWidth"/> pixels.
/// </summary>
internal sealed class BreakpointGutterControl : FrameworkElement
{
    // ── Constants ─────────────────────────────────────────────────────────────

    internal const double GutterWidth = 16.0;

    private static readonly Brush BpActiveBrush    = new SolidColorBrush(Color.FromRgb(0xE5, 0x14, 0x00)); // DB_BreakpointActiveBrush
    private static readonly Brush BpDisabledBrush  = Brushes.DimGray;
    private static readonly Brush ExecutionBrush   = new SolidColorBrush(Color.FromRgb(0xFF, 0xDD, 0x00)); // DB_ExecutionLineBrush
    private static readonly Brush ExecutionLineBg  = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xDD, 0x00));
    private static readonly Pen   BpDisabledPen    = new(BpDisabledBrush, 1.5);

    private const double CircleRadius  = 5.5;
    private const double ArrowPadding  = 2.0;

    // ── State ─────────────────────────────────────────────────────────────────

    private IBreakpointSource? _source;
    private string             _filePath    = string.Empty;
    private int?               _executionLine;           // 1-based, null = none
    private double             _lineHeight;
    private int                _firstVisibleLine;        // 0-based
    private int                _lastVisibleLine;         // 0-based
    private double             _topMargin;
    private IReadOnlyDictionary<int, double> _lineYLookup = new Dictionary<int, double>();
    private Brush              _backgroundBrush = Brushes.Transparent;

    // ── Constructor ───────────────────────────────────────────────────────────

    static BreakpointGutterControl()
    {
        BpActiveBrush.Freeze();
        ExecutionBrush.Freeze();
        ExecutionLineBg.Freeze();
        BpDisabledPen.Freeze();
    }

    public BreakpointGutterControl()
    {
        Width              = GutterWidth;
        Cursor             = Cursors.Hand;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        ToolTip            = "Click to toggle breakpoint";
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Set the breakpoint data source (injected by App layer).</summary>
    public void SetBreakpointSource(IBreakpointSource? source)
    {
        _source = source;
        InvalidateVisual();
    }

    /// <summary>Set the file path of the current document.</summary>
    public void SetFilePath(string? path)
    {
        _filePath = path ?? string.Empty;
        InvalidateVisual();
    }

    /// <summary>Set the current execution line (1-based; null = no session paused).</summary>
    public void SetExecutionLine(int? line)
    {
        _executionLine = line;
        InvalidateVisual();
    }

    /// <summary>Called by CodeEditor after each layout pass to sync visible range.</summary>
    public void Update(
        double lineHeight, int firstVisible, int lastVisible,
        double topMargin, IReadOnlyDictionary<int, double> lineYLookup,
        Brush backgroundBrush)
    {
        _lineHeight       = lineHeight;
        _firstVisibleLine = firstVisible;
        _lastVisibleLine  = lastVisible;
        _topMargin        = topMargin;
        _lineYLookup      = lineYLookup;
        _backgroundBrush  = backgroundBrush;
        InvalidateVisual();
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        dc.DrawRectangle(_backgroundBrush, null, bounds);

        if (_lineHeight <= 0) return;

        for (int i = _firstVisibleLine; i <= _lastVisibleLine; i++)
        {
            if (!_lineYLookup.TryGetValue(i, out double y))
                y = _topMargin + (i - _firstVisibleLine) * _lineHeight;

            double cy    = y + _lineHeight / 2.0;
            double cx    = GutterWidth / 2.0;
            int    line1 = i + 1; // 1-based

            // Execution arrow (takes precedence over breakpoint circle)
            if (_executionLine.HasValue && _executionLine.Value == line1)
            {
                DrawExecutionArrow(dc, cx, cy);
                // Also tint the full row background
                dc.DrawRectangle(ExecutionLineBg, null, new Rect(0, y, ActualWidth, _lineHeight));
                continue;
            }

            // Breakpoint circle
            if (_source is not null && !string.IsNullOrEmpty(_filePath) && _source.HasBreakpoint(_filePath, line1))
                dc.DrawEllipse(BpActiveBrush, null, new Point(cx, cy), CircleRadius, CircleRadius);
        }
    }

    private static void DrawExecutionArrow(DrawingContext dc, double cx, double cy)
    {
        // Yellow right-pointing arrow (▶)
        var pts = new[]
        {
            new Point(cx - 4, cy - 5),
            new Point(cx + 4, cy),
            new Point(cx - 4, cy + 5),
        };
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(pts[0], isFilled: true, isClosed: true);
            ctx.LineTo(pts[1], isStroked: true, isSmoothJoin: false);
            ctx.LineTo(pts[2], isStroked: true, isSmoothJoin: false);
        }
        geo.Freeze();
        dc.DrawGeometry(ExecutionBrush, null, geo);
    }

    // ── Hit testing ───────────────────────────────────────────────────────────

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_source is null || string.IsNullOrEmpty(_filePath) || _lineHeight <= 0) return;

        var pos   = e.GetPosition(this);
        var line1 = HitTestLine(pos.Y);
        if (line1 > 0) _source.Toggle(_filePath, line1);
        e.Handled = true;
    }

    private int HitTestLine(double y)
    {
        // Walk visible range to find which 1-based line y falls in
        for (int i = _firstVisibleLine; i <= _lastVisibleLine; i++)
        {
            if (!_lineYLookup.TryGetValue(i, out double lineY))
                lineY = _topMargin + (i - _firstVisibleLine) * _lineHeight;

            if (y >= lineY && y < lineY + _lineHeight) return i + 1;
        }
        return -1;
    }
}
