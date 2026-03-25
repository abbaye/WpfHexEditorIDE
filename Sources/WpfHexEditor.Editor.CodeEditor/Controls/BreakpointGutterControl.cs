// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Controls/BreakpointGutterControl.cs
// Description:
//     VS-style breakpoint gutter strip rendered in the CodeEditor line-number area.
//     Left-click toggles breakpoints; right-click on an existing breakpoint fires
//     RightClickRequested so the App layer can show a BreakpointInfoPopup.
//     Validates line executable-status via the ValidateLine callback (set by CodeEditor
//     from the active language's NonExecutablePatterns).
// Architecture:
//     FrameworkElement with DrawingContext rendering (same as GutterControl).
//     Decoupled from DebuggerService via IBreakpointSource interface —
//     the App layer injects the callback, no direct dep on Core.Debugger.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace WpfHexEditor.Editor.CodeEditor.Controls;

// ── Minimal breakpoint data record (avoids SDK dependency on CodeEditor) ─────

/// <summary>Read-only snapshot of a breakpoint state used by BreakpointInfoPopup.</summary>
public sealed record BreakpointInfo(string? Condition, bool IsEnabled);

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

    /// <summary>Returns full breakpoint info, or null if none exists at that location.</summary>
    BreakpointInfo? GetBreakpoint(string filePath, int line);

    /// <summary>Update the condition of an existing breakpoint (fire-and-forget).</summary>
    void SetCondition(string filePath, int line, string? condition);

    /// <summary>Enable or disable an existing breakpoint (fire-and-forget).</summary>
    void SetEnabled(string filePath, int line, bool enabled);

    /// <summary>Delete an existing breakpoint (fire-and-forget).</summary>
    void Delete(string filePath, int line);
}

/// <summary>
/// Renders breakpoint markers in the left gutter of the CodeEditor.
/// Positioned at x=0 with a fixed width of <see cref="GutterWidth"/> pixels.
/// </summary>
internal sealed class BreakpointGutterControl : FrameworkElement
{
    // ── Constants ─────────────────────────────────────────────────────────────

    internal const double GutterWidth = 16.0;

    private static readonly Brush BpActiveBrush    = new SolidColorBrush(Color.FromRgb(0xE5, 0x14, 0x00));
    private static readonly Brush BpDisabledBrush  = Brushes.DimGray;
    private static readonly Brush ExecutionBrush   = new SolidColorBrush(Color.FromRgb(0xFF, 0xDD, 0x00));
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
    private int                _hoverLine = -1;          // 1-based, -1 = no hover

    // ── Extensibility ─────────────────────────────────────────────────────────

    /// <summary>
    /// Optional callback — returns <c>false</c> when the given 1-based line is
    /// non-executable (e.g. comment, blank, bare brace). Set by CodeEditor from
    /// the active language's <c>BreakpointRules.NonExecutablePatterns</c>.
    /// Null = all lines are valid.
    /// </summary>
    public Func<int, bool>? ValidateLine { get; set; }

    /// <summary>
    /// Fires when the user right-clicks a line that has an active breakpoint.
    /// Args: (filePath, 1-based line).
    /// The App layer should open a <c>BreakpointInfoPopup</c> in response.
    /// </summary>
    internal event Action<string, int>? RightClickRequested;

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
        Width               = GutterWidth;
        Cursor              = Cursors.Hand;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove           += OnMouseMove;
        MouseLeave          += OnMouseLeave;
        ToolTip             = "Click to toggle breakpoint";
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
                dc.DrawRectangle(ExecutionLineBg, null, new Rect(0, y, ActualWidth, _lineHeight));
                continue;
            }

            // Breakpoint circle
            if (_source is not null && !string.IsNullOrEmpty(_filePath) && _source.HasBreakpoint(_filePath, line1))
                dc.DrawEllipse(BpActiveBrush, null, new Point(cx, cy), CircleRadius, CircleRadius);
            // Ghost circle on hover (no existing breakpoint)
            else if (_hoverLine == line1)
                dc.DrawEllipse(null, BpDisabledPen, new Point(cx, cy), CircleRadius, CircleRadius);
        }
    }

    private static void DrawExecutionArrow(DrawingContext dc, double cx, double cy)
    {
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
        if (line1 < 1) return;

        // Reject non-executable lines according to the active language rules.
        if (ValidateLine != null && !ValidateLine(line1))
        {
            ShowInvalidLineFeedback();
            e.Handled = true;
            return;
        }

        _source.Toggle(_filePath, line1);
        e.Handled = true;
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonUp(e);

        if (_source is null || string.IsNullOrEmpty(_filePath) || _lineHeight <= 0) return;

        int line1 = HitTestLine(e.GetPosition(this).Y);
        if (line1 >= 1 && _source.HasBreakpoint(_filePath, line1))
            RightClickRequested?.Invoke(_filePath, line1);

        e.Handled = true;
    }

    private int HitTestLine(double y)
    {
        for (int i = _firstVisibleLine; i <= _lastVisibleLine; i++)
        {
            if (!_lineYLookup.TryGetValue(i, out double lineY))
                lineY = _topMargin + (i - _firstVisibleLine) * _lineHeight;

            if (y >= lineY && y < lineY + _lineHeight) return i + 1;
        }
        return -1;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        int newHover = _lineHeight > 0 ? HitTestLine(e.GetPosition(this).Y) : -1;
        if (newHover == _hoverLine) return;
        _hoverLine = newHover;
        InvalidateVisual();
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (_hoverLine == -1) return;
        _hoverLine = -1;
        InvalidateVisual();
    }

    // ── Validation feedback ───────────────────────────────────────────────────

    private void ShowInvalidLineFeedback()
    {
        // Close any previous tip first.
        if (ToolTip is ToolTip existing && existing.IsOpen)
            existing.IsOpen = false;

        // PlacementMode.Mouse positions near the cursor; StaysOpen keeps it
        // visible for the full 1.5s regardless of mouse movement.
        var tip = new ToolTip
        {
            Content    = "Cannot place breakpoint on this line",
            Placement  = System.Windows.Controls.Primitives.PlacementMode.Mouse,
            StaysOpen  = true
        };
        ToolTip    = tip;
        tip.IsOpen = true;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        timer.Tick += (_, _) =>
        {
            tip.IsOpen = false;
            timer.Stop();
            ToolTip = "Click to toggle breakpoint";
        };
        timer.Start();
    }
}
