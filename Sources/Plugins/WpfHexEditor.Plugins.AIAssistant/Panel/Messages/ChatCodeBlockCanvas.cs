// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: ChatCodeBlockCanvas.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-04-01
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     GlyphRun/DrawingContext renderer for syntax-highlighted code blocks.
//     Syntax rules driven by .whfmt via LanguageRegistry + SyntaxRuleHighlighter.
//     Never hardcodes language patterns — unknown languages render plain monospace.
// ==========================================================
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.Plugins.AIAssistant.Panel.Messages;

/// <summary>
/// FrameworkElement that renders syntax-highlighted code via DrawingContext/GlyphRun.
/// Language highlighting is driven by whfmt definitions via <see cref="ISyntaxColoringService"/>.
/// </summary>
internal sealed class ChatCodeBlockCanvas : FrameworkElement
{
    /// <summary>
    /// Set once during plugin init. All instances share the same service reference.
    /// </summary>
    internal static ISyntaxColoringService? SyntaxColoringService { get; set; }

    // ── Rendering state ──────────────────────────────────────────────────
    private static readonly Typeface s_typeface = new("Cascadia Code,Consolas,Courier New");
    private const double FontSize = 12.0;
    private const double LinePadding = 2.0;
    private const double LeftPadding = 10.0;
    private const double TopPadding = 4.0;

    private GlyphTypeface? _gt;
    private double _charW;
    private double _lineH;
    private double _baseline;
    private float _pixelsPerDip = 1f;

    private string _code = "";
    private string? _language;
    private string[]? _lines;
    private IReadOnlyList<IReadOnlyList<ColoredSpan>>? _cachedSpans;
    private Brush? _defaultFg;

    // ── Public properties ────────────────────────────────────────────────

    public string Code
    {
        get => _code;
        set
        {
            _code = value ?? "";
            _lines = _code.Split('\n');
            for (int i = 0; i < _lines.Length; i++)
                _lines[i] = _lines[i].TrimEnd('\r');
            RebuildCachedSpans();
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    public string? Language
    {
        get => _language;
        set
        {
            _language = value;
            RebuildCachedSpans();
            InvalidateVisual();
        }
    }

    private void RebuildCachedSpans()
    {
        _cachedSpans = null;
        if (_lines is null || _lines.Length == 0 || string.IsNullOrEmpty(_language)) return;

        try
        {
            _cachedSpans = SyntaxColoringService?.ColorizeLines(_lines, _language!);
        }
        catch
        {
            // graceful fallback — no highlighting
        }
    }

    // ── Measure / Arrange ────────────────────────────────────────────────

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureMetrics();
        var lineCount = _lines?.Length ?? 0;
        var height = TopPadding + lineCount * _lineH + TopPadding;
        return new Size(availableSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        return finalSize;
    }

    // ── OnRender (DrawingContext) ─────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        if (_lines is null || _lines.Length == 0) return;

        EnsureMetrics();

        _defaultFg = Application.Current?.TryFindResource("CA_MessageForegroundBrush") as Brush
                     ?? Brushes.WhiteSmoke;

        double y = TopPadding;

        for (int i = 0; i < _lines.Length; i++)
        {
            var line = _lines[i];
            double x = LeftPadding;
            double baselineY = y + _baseline;

            var spans = _cachedSpans is not null && i < _cachedSpans.Count ? _cachedSpans[i] : null;

            if (spans is not null && spans.Count > 0 && !string.IsNullOrEmpty(line))
            {
                int pos = 0;
                foreach (var span in spans)
                {
                    // Gap before span
                    if (span.Start > pos)
                    {
                        var gap = line[pos..span.Start];
                        RenderSegment(dc, gap, x, baselineY, y, _defaultFg);
                        x += MeasureWidth(gap);
                    }

                    // Span
                    RenderSegment(dc, span.Text, x, baselineY, y, span.Foreground);
                    x += MeasureWidth(span.Text);
                    pos = span.Start + span.Length;
                }

                // Remainder
                if (pos < line.Length)
                {
                    var rest = line[pos..];
                    RenderSegment(dc, rest, x, baselineY, y, _defaultFg);
                }
            }
            else if (!string.IsNullOrEmpty(line))
            {
                RenderSegment(dc, line, x, baselineY, y, _defaultFg);
            }

            y += _lineH;
        }
    }

    // ── Rendering helpers ────────────────────────────────────────────────

    private void RenderSegment(DrawingContext dc, string text, double x, double baselineY, double topY, Brush brush)
    {
        if (string.IsNullOrEmpty(text)) return;

        if (_gt != null)
        {
            RenderGlyphRun(dc, text, x, baselineY, brush);
        }
        else
        {
            var ft = new FormattedText(text, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, s_typeface, FontSize, brush, _pixelsPerDip);
            dc.DrawText(ft, new Point(x, topY));
        }
    }

    [ThreadStatic] private static List<ushort>? _glyphPool;
    [ThreadStatic] private static List<double>? _advancePool;

    private void RenderGlyphRun(DrawingContext dc, string text, double x, double baselineY, Brush brush)
    {
        var gt = _gt!;
        var glyphIndices = _glyphPool ??= new List<ushort>(256);
        var advanceWidths = _advancePool ??= new List<double>(256);
        glyphIndices.Clear();
        advanceWidths.Clear();

        var charMap = gt.CharacterToGlyphMap;
        foreach (char ch in text)
        {
            if (ch == '\t')
            {
                charMap.TryGetValue(' ', out ushort spaceGi);
                glyphIndices.Add(spaceGi);
                advanceWidths.Add(gt.AdvanceWidths[spaceGi] * FontSize * 4);
                continue;
            }

            if (!charMap.TryGetValue(ch, out ushort gi))
                charMap.TryGetValue('\uFFFD', out gi);

            glyphIndices.Add(gi);
            advanceWidths.Add(gt.AdvanceWidths[gi] * FontSize);
        }

        var glyphRun = new GlyphRun(
            gt, 0, false, FontSize, _pixelsPerDip,
            glyphIndices.ToArray(),
            new Point(x, baselineY),
            advanceWidths.ToArray(),
            null, null, null, null, null, null);

        dc.DrawGlyphRun(brush, glyphRun);
    }

    private double MeasureWidth(string text)
    {
        double w = 0;
        if (_gt != null)
        {
            var charMap = _gt.CharacterToGlyphMap;
            foreach (char ch in text)
            {
                if (ch == '\t')
                {
                    charMap.TryGetValue(' ', out ushort spaceGi);
                    w += _gt.AdvanceWidths[spaceGi] * FontSize * 4;
                }
                else
                {
                    if (!charMap.TryGetValue(ch, out ushort gi))
                        charMap.TryGetValue('M', out gi);
                    w += _gt.AdvanceWidths[gi] * FontSize;
                }
            }
        }
        else
        {
            w = _charW * text.Length;
        }
        return w;
    }

    private void EnsureMetrics()
    {
        if (_lineH > 0) return;

        var source = PresentationSource.FromVisual(this);
        _pixelsPerDip = source != null
            ? (float)(source.CompositionTarget?.TransformToDevice.M22 ?? 1.0)
            : 1f;

        _gt = s_typeface.TryGetGlyphTypeface(out var resolved) ? resolved : null;

        if (_gt != null)
        {
            _gt.CharacterToGlyphMap.TryGetValue('M', out ushort gi);
            _charW = _gt.AdvanceWidths[gi] * FontSize;
            _lineH = _gt.Height * FontSize + LinePadding;
            _baseline = _gt.Baseline * FontSize;
        }
        else
        {
            var ft = new FormattedText("M", CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, s_typeface, FontSize, Brushes.White, _pixelsPerDip);
            _charW = ft.Width;
            _lineH = ft.Height + LinePadding;
            _baseline = ft.Height * 0.8;
        }
    }
}
