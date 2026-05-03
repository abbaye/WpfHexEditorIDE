// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Layers/DebugValueHintsLayer.cs
// Description:
//     DrawingVisual overlay that renders inline debug-value annotations
//     (" = <value>") next to identifiers when the debugger is paused.
//     The layer is a no-op until an IDebugValueProvider is set by the
//     DAP integration layer (Debugger feature #44).
//
// Architecture:
//     Same pattern as LspInlayHintsLayer — FrameworkElement with a single
//     DrawingVisual child, IsHitTestVisible=false, 500ms debounce.
//     Theme tokens: CE_InlayHintForeground, CE_InlayHintBackground (reused).
// ==========================================================

using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using WpfHexEditor.Editor.Core.Debugging;

namespace WpfHexEditor.Editor.CodeEditor.Layers;

/// <summary>
/// Renders inline debug-value hints as a <see cref="DrawingVisual"/> overlay.
/// Active only while a <see cref="IDebugValueProvider"/> is set (debugger paused).
/// </summary>
public sealed class DebugValueHintsLayer : FrameworkElement
{
    // ── State ─────────────────────────────────────────────────────────────────
    private IDebugValueProvider?              _provider;
    private string?                           _filePath;
    private int                               _firstVisibleLine;
    private int                               _lastVisibleLine;
    private double                            _charWidth;
    private double                            _lineHeight;
    private double                            _horizontalScrollOffset;
    private IReadOnlyList<DebugValueHint>     _hints = Array.Empty<DebugValueHint>();

    private readonly DrawingVisual            _visual = new();
    private readonly DispatcherTimer          _debounce;

    // ── Constructor ───────────────────────────────────────────────────────────

    public DebugValueHintsLayer()
    {
        IsHitTestVisible = false;
        ClipToBounds     = true;
        AddVisualChild(_visual);
        AddLogicalChild(_visual);

        _debounce = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _debounce.Tick += OnDebounce;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets the active debug-value provider (called by DAP integration on pause).
    /// Pass <see langword="null"/> to clear the layer when the session resumes or stops.
    /// </summary>
    public void SetDebugValueProvider(IDebugValueProvider? provider)
    {
        _provider = provider;
        if (provider is null)
            ClearHints();
        else
            RequestRefresh();
    }

    /// <summary>Updates viewport context and schedules a refresh.</summary>
    public void SetContext(string? filePath, int firstLine, int lastLine,
                           double charWidth, double lineHeight,
                           double horizontalScrollOffset = 0.0)
    {
        bool viewportMoved = _firstVisibleLine != firstLine || _lastVisibleLine != lastLine;
        _filePath               = filePath;
        _firstVisibleLine       = firstLine;
        _lastVisibleLine        = lastLine;
        _charWidth              = charWidth;
        _lineHeight             = lineHeight;
        _horizontalScrollOffset = horizontalScrollOffset;

        if (viewportMoved && _hints.Count > 0)
            ClearHints();

        RequestRefresh();
    }

    // ── Visual tree ───────────────────────────────────────────────────────────

    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) => _visual;

    protected override Size ArrangeOverride(Size finalSize) => finalSize;

    // ── Refresh logic ─────────────────────────────────────────────────────────

    private void RequestRefresh()
    {
        _debounce.Stop();
        _debounce.Start();
    }

    private void OnDebounce(object? sender, EventArgs e)
    {
        _debounce.Stop();

        if (_provider is null || _filePath is null)
        {
            ClearHints();
            return;
        }

        try
        {
            _hints = _provider.GetValues(_filePath, _firstVisibleLine, _lastVisibleLine);
            RenderHints();
        }
        catch
        {
            ClearHints();
        }
    }

    private void ClearHints()
    {
        _hints = Array.Empty<DebugValueHint>();
        using var dc = _visual.RenderOpen();
        // empty — clears previous drawing
    }

    private void RenderHints()
    {
        using var dc = _visual.RenderOpen();

        if (_hints.Count == 0 || _charWidth <= 0 || _lineHeight <= 0) return;

        var fg = TryFindResource("CE_InlayHintForeground") as Brush
                 ?? new SolidColorBrush(Color.FromArgb(160, 100, 180, 100));
        var bg = TryFindResource("CE_InlayHintBackground") as Brush
                 ?? new SolidColorBrush(Color.FromArgb(30, 100, 180, 100));

        var typeface = new Typeface(
            new FontFamily("Consolas, Courier New"),
            FontStyles.Italic, FontWeights.Normal, FontStretches.Normal);

        double ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        foreach (var hint in _hints)
        {
            if (hint.Line < _firstVisibleLine || hint.Line > _lastVisibleLine) continue;

            var text = $" = {hint.Value}";

            var ft = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                8.0,
                fg,
                ppd);

            var x = hint.Column * _charWidth - _horizontalScrollOffset;
            var y = (hint.Line - _firstVisibleLine) * _lineHeight
                    + (_lineHeight - ft.Height) / 2;

            var rect = new Rect(x - 1, y, ft.Width + 2, ft.Height);
            dc.DrawRectangle(bg, null, rect);
            dc.DrawText(ft, new Point(x, y));
        }
    }
}
