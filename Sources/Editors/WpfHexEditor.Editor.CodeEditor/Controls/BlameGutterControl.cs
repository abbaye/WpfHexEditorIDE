// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Controls/BlameGutterControl.cs
// Description:
//     Generic blame gutter — 6px color-bar strip left of the breakpoint gutter.
//     Receives BlameEntry[] from the plugin layer (knows nothing about git).
//     Color encodes commit age: recent = #4EC9B0, old = #808080.
//     Hover shows a tooltip popup with Author / Date / Message.
// Architecture Notes:
//     Pattern: identical to BreakpointGutterControl (FrameworkElement + OnRender).
//     Data injection: SetBlame(IReadOnlyList<BlameEntry>?) — no SDK dep from plugin.
//     Visibility: controlled by ShowBlameGutter DP on CodeEditor (default false).
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using WpfHexEditor.Editor.Core.LSP;

namespace WpfHexEditor.Editor.CodeEditor.Controls;

/// <summary>
/// Renders a 6-pixel blame color bar in the CodeEditor left gutter area.
/// Each visible line is tinted by the commit age of the line's last blame entry.
/// </summary>
internal sealed class BlameGutterControl : FrameworkElement
{
    // ── Constants ─────────────────────────────────────────────────────────────

    internal const double BlameGutterWidth = 6.0;

    private static readonly Color RecentColor = Color.FromRgb(0x4E, 0xC9, 0xB0);  // teal
    private static readonly Color OldColor    = Color.FromRgb(0x80, 0x80, 0x80);  // gray

    // ── State ─────────────────────────────────────────────────────────────────

    private IReadOnlyList<BlameEntry>?            _entries;
    private double                                _lineHeight;
    private int                                   _firstVisibleLine;
    private int                                   _lastVisibleLine;
    private double                                _topMargin;
    private IReadOnlyDictionary<int, double>      _lineYLookup = new Dictionary<int, double>();

    // Tooltip popup
    private readonly Popup     _tooltip       = new() { StaysOpen = false, AllowsTransparency = true };
    private readonly TextBlock _tooltipContent = new()
    {
        Padding    = new Thickness(6, 4, 6, 4),
        FontFamily = new FontFamily("Segoe UI"),
        FontSize   = 11,
        Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x25)),
        Foreground = Brushes.White
    };

    // Age bounds for colour interpolation (computed from _entries)
    private DateTime _newestDate = DateTime.Now;
    private DateTime _oldestDate = DateTime.Now.AddYears(-3);

    // ── Constructor ───────────────────────────────────────────────────────────

    internal BlameGutterControl()
    {
        _tooltip.Child = _tooltipContent;
        MouseMove  += OnMouseMove;
        MouseLeave += OnMouseLeave;
    }

    // ── Data injection ────────────────────────────────────────────────────────

    internal void SetBlame(IReadOnlyList<BlameEntry>? entries)
    {
        _entries = entries;
        if (entries is { Count: > 0 })
        {
            _newestDate = entries.Max(e => e.Date);
            _oldestDate = entries.Min(e => e.Date);
            if (_newestDate == _oldestDate)
                _oldestDate = _newestDate.AddYears(-1);
        }
        InvalidateVisual();
    }

    // ── Layout update (called by CodeEditor on scroll / resize) ──────────────

    internal void Update(
        double lineHeight,
        int    firstVisible,
        int    lastVisible,
        double topMargin,
        IReadOnlyDictionary<int, double> lineYLookup)
    {
        _lineHeight       = lineHeight;
        _firstVisibleLine = firstVisible;
        _lastVisibleLine  = lastVisible;
        _topMargin        = topMargin;
        _lineYLookup      = lineYLookup;
        InvalidateVisual();
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        if (_entries is null || _entries.Count == 0 || _lineHeight <= 0) return;

        for (var line = _firstVisibleLine; line <= _lastVisibleLine; line++)
        {
            if (line < 1 || line > _entries.Count) continue;
            if (!_lineYLookup.TryGetValue(line, out var y)) continue;

            var entry = _entries[line - 1];
            var brush = new SolidColorBrush(LerpColor(entry.Date));
            brush.Freeze();
            dc.DrawRectangle(brush, null, new Rect(0, y, BlameGutterWidth, _lineHeight));
        }
    }

    // ── Mouse interaction ─────────────────────────────────────────────────────

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_entries is null || _lineHeight <= 0) return;

        var pos = e.GetPosition(this);
        var line = HitTestLine(pos.Y);
        if (line < 1 || line > _entries.Count) { _tooltip.IsOpen = false; return; }

        var entry = _entries[line - 1];
        _tooltipContent.Text =
            $"{entry.AuthorName}  {entry.Date:yyyy-MM-dd}\n{TruncateMessage(entry.Message)}";

        _tooltip.Placement         = PlacementMode.Mouse;
        _tooltip.PlacementTarget   = this;
        _tooltip.IsOpen            = true;
    }

    private void OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        => _tooltip.IsOpen = false;

    // ── Helpers ───────────────────────────────────────────────────────────────

    private int HitTestLine(double y)
    {
        if (_lineHeight <= 0) return -1;
        var approx = _firstVisibleLine + (int)((y - _topMargin) / _lineHeight);
        // Verify against lookup for folded-line accuracy
        foreach (var (ln, ly) in _lineYLookup)
            if (y >= ly && y < ly + _lineHeight) return ln;
        return approx;
    }

    private Color LerpColor(DateTime date)
    {
        var range  = (_newestDate - _oldestDate).TotalSeconds;
        var offset = (date - _oldestDate).TotalSeconds;
        var t      = range > 0 ? Math.Clamp(offset / range, 0.0, 1.0) : 1.0;
        return Color.FromRgb(
            (byte)(OldColor.R + (RecentColor.R - OldColor.R) * t),
            (byte)(OldColor.G + (RecentColor.G - OldColor.G) * t),
            (byte)(OldColor.B + (RecentColor.B - OldColor.B) * t));
    }

    private static string TruncateMessage(string msg)
        => msg.Length > 72 ? msg[..69] + "…" : msg;
}
