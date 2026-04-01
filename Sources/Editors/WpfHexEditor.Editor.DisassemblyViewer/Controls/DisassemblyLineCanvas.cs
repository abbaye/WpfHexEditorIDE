//////////////////////////////////////////////
// Project: WpfHexEditor.Editor.DisassemblyViewer
// File: Controls/DisassemblyLineCanvas.cs
// Description:
//     High-performance GlyphRun-based renderer for structured disassembly lines.
//     Each line is coloured by token kind:
//       Address  → gray       Bytes     → blue-gray
//       Mnemonic → keyword    Operand   → foreground
//       Comment  → green      Arrow     → orange
//       Label    → yellow
//     Click → fires OffsetRequested(fileOffset).
//     Hover highlight → single line tint.
// Architecture:
//     FrameworkElement + IScrollInfo.  DrawingContext OnRender.
//     Reuses DiffGlyphHelper pattern (GlyphTypeface cached per family/size).
//     Line height = FontSize * 1.4.  Horizontal scroll not implemented.
//////////////////////////////////////////////

using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Core.Decompiler;

namespace WpfHexEditor.Editor.DisassemblyViewer.Controls;

/// <summary>
/// GlyphRun-based canvas that renders structured <see cref="DisassemblyLine"/> records
/// with per-token syntax colouring.  Fires <see cref="OffsetRequested"/> on click.
/// </summary>
public sealed class DisassemblyLineCanvas : FrameworkElement
{
    // ── Shared frozen brushes ─────────────────────────────────────────────────

    private static readonly Brush _brushAddress;
    private static readonly Brush _brushBytes;
    private static readonly Brush _brushMnemonic;
    private static readonly Brush _brushOperand;
    private static readonly Brush _brushComment;
    private static readonly Brush _brushArrow;
    private static readonly Brush _brushLabel;
    private static readonly Brush _brushHover;
    private static readonly Brush _brushJumpTarget;

    static DisassemblyLineCanvas()
    {
        static Brush F(byte r, byte g, byte b, byte a = 255)
        {
            var br = new SolidColorBrush(Color.FromArgb(a, r, g, b));
            br.Freeze();
            return br;
        }
        _brushAddress    = F(128, 128, 128);         // gray
        _brushBytes      = F(100, 149, 237);         // cornflower blue
        _brushMnemonic   = F(86,  156, 214);         // VS keyword blue
        _brushOperand    = F(220, 220, 220);         // light foreground
        _brushComment    = F(106, 153, 85);          // VS comment green
        _brushArrow      = F(255, 165, 0);           // orange
        _brushLabel      = F(220, 220, 100);         // yellow-ish
        _brushHover      = F(255, 255, 255, 18);     // subtle white tint
        _brushJumpTarget = F(255, 100, 100, 40);     // subtle red tint for JMP lines
    }

    private Brush BrushFor(DisassemblyTokenKind kind) => kind switch
    {
        DisassemblyTokenKind.Address  => _brushAddress,
        DisassemblyTokenKind.Bytes    => _brushBytes,
        DisassemblyTokenKind.Mnemonic => _brushMnemonic,
        DisassemblyTokenKind.Operand  => _brushOperand,
        DisassemblyTokenKind.Comment  => _brushComment,
        DisassemblyTokenKind.Arrow    => _brushArrow,
        DisassemblyTokenKind.Label    => _brushLabel,
        _                             => _brushOperand,
    };

    // ── GlyphTypeface cache ───────────────────────────────────────────────────

    private GlyphTypeface? _glyphTypeface;
    private double         _cachedFontSize;

    private GlyphTypeface GetGlyphTypeface()
    {
        if (_glyphTypeface is null || _cachedFontSize != FontSize)
        {
            var tf = new Typeface(new FontFamily("Consolas, Courier New"),
                FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            tf.TryGetGlyphTypeface(out _glyphTypeface);
            _cachedFontSize = FontSize;
        }
        return _glyphTypeface!;
    }

    // ── State ─────────────────────────────────────────────────────────────────

    private IReadOnlyList<DisassemblyLine>? _lines;
    private int     _hoverLine      = -1;
    private int     _selectedLine   = -1;
    private double  _lineHeight;

    // ── Properties ────────────────────────────────────────────────────────────

    public new double FontSize { get; set; } = 12.5;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fired when the user clicks a line. Argument is the file offset.</summary>
    public event EventHandler<long>? OffsetRequested;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Sets line data and triggers a full redraw.</summary>
    public void SetLines(IReadOnlyList<DisassemblyLine>? lines)
    {
        _lines        = lines;
        _hoverLine    = -1;
        _selectedLine = -1;
        _lineHeight   = FontSize * 1.4;
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Scrolls to and selects the line nearest to <paramref name="fileOffset"/>.</summary>
    public void NavigateToOffset(long fileOffset)
    {
        if (_lines is null) return;
        int best = -1;
        long bestDelta = long.MaxValue;
        for (int i = 0; i < _lines.Count; i++)
        {
            long delta = Math.Abs(_lines[i].FileOffset - fileOffset);
            if (delta < bestDelta) { bestDelta = delta; best = i; }
        }
        if (best < 0) return;
        _selectedLine = best;
        InvalidateVisual();
        // Scroll into view via parent ScrollViewer
        BringLineIntoView(best);
    }

    // ── Layout ────────────────────────────────────────────────────────────────

    protected override Size MeasureOverride(Size availableSize)
    {
        _lineHeight = FontSize * 1.4;
        double h = (_lines?.Count ?? 0) * _lineHeight;
        return new Size(availableSize.Width == double.PositiveInfinity ? 0 : availableSize.Width, h);
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        if (_lines is null || _lines.Count == 0) return;

        _lineHeight = FontSize * 1.4;
        double w    = ActualWidth;
        double h    = ActualHeight;
        var    gtf  = GetGlyphTypeface();
        if (gtf is null) return;

        // Only render visible lines
        var clip    = VisualClip ?? new RectangleGeometry(new Rect(0, 0, w, h));
        var clipBounds = clip.Bounds;
        int firstLine = Math.Max(0, (int)(clipBounds.Top / _lineHeight));
        int lastLine  = Math.Min(_lines.Count - 1,
                            (int)((clipBounds.Bottom + _lineHeight) / _lineHeight));

        double baselineOffset = _lineHeight * 0.78; // Consolas baseline ~78%

        for (int i = firstLine; i <= lastLine; i++)
        {
            double y = i * _lineHeight;
            var line = _lines[i];

            // Line background
            if (i == _selectedLine)
                dc.DrawRectangle(_brushHover, null, new Rect(0, y, w, _lineHeight));
            else if (i == _hoverLine)
                dc.DrawRectangle(_brushHover, null, new Rect(0, y, w, _lineHeight));
            if (line.IsJump || line.IsCall)
                dc.DrawRectangle(_brushJumpTarget, null, new Rect(0, y, w, _lineHeight));

            // Tokens
            double x = 8;
            foreach (var token in line.Tokens)
            {
                if (string.IsNullOrEmpty(token.Text)) continue;
                double tokenW = MeasureText(token.Text, gtf);
                DrawText(dc, token.Text, BrushFor(token.Kind), gtf, new Point(x, y + baselineOffset));
                x += tokenW;
            }
        }
    }

    private void DrawText(DrawingContext dc, string text, Brush brush,
        GlyphTypeface gtf, Point origin)
    {
        if (string.IsNullOrEmpty(text) || gtf is null) return;

        var glyphIndexes  = new ushort[text.Length];
        var advanceWidths = new double[text.Length];
        double totalW = 0;

        for (int i = 0; i < text.Length; i++)
        {
            if (!gtf.CharacterToGlyphMap.TryGetValue(text[i], out ushort gi))
                gi = gtf.CharacterToGlyphMap.TryGetValue(' ', out ushort spGi) ? spGi : (ushort)0;
            glyphIndexes[i]  = gi;
            double adv = gtf.AdvanceWidths[gi] * FontSize;
            advanceWidths[i] = adv;
            totalW += adv;
        }

        var run = new GlyphRun(gtf, 0, false, FontSize, 1.0f,
            glyphIndexes, origin, advanceWidths,
            null, null, null, null, null, null);
        dc.DrawGlyphRun(brush, run);
    }

    private double MeasureText(string text, GlyphTypeface gtf)
    {
        double w = 0;
        foreach (var ch in text)
        {
            if (!gtf.CharacterToGlyphMap.TryGetValue(ch, out ushort gi))
                gi = gtf.CharacterToGlyphMap.TryGetValue(' ', out ushort spGi) ? spGi : (ushort)0;
            w += gtf.AdvanceWidths[gi] * FontSize;
        }
        return w;
    }

    // ── Mouse ─────────────────────────────────────────────────────────────────

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int idx = LineAt(e.GetPosition(this).Y);
        if (idx != _hoverLine) { _hoverLine = idx; InvalidateVisual(); }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoverLine >= 0) { _hoverLine = -1; InvalidateVisual(); }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        int idx = LineAt(e.GetPosition(this).Y);
        if (idx >= 0 && _lines is not null && idx < _lines.Count)
        {
            _selectedLine = idx;
            InvalidateVisual();
            OffsetRequested?.Invoke(this, _lines[idx].FileOffset);
            e.Handled = true;
        }
    }

    private int LineAt(double y)
    {
        if (_lines is null || _lineHeight <= 0) return -1;
        int idx = (int)(y / _lineHeight);
        return idx >= 0 && idx < _lines.Count ? idx : -1;
    }

    private void BringLineIntoView(int lineIndex)
    {
        // Walk up to find ScrollViewer and scroll
        var sv = FindScrollViewer(this);
        if (sv is null) return;
        double top    = lineIndex * _lineHeight;
        double bottom = top + _lineHeight;
        if (top < sv.VerticalOffset)
            sv.ScrollToVerticalOffset(top);
        else if (bottom > sv.VerticalOffset + sv.ViewportHeight)
            sv.ScrollToVerticalOffset(bottom - sv.ViewportHeight);
    }

    private static System.Windows.Controls.ScrollViewer? FindScrollViewer(DependencyObject d)
    {
        var parent = System.Windows.Media.VisualTreeHelper.GetParent(d);
        while (parent is not null)
        {
            if (parent is System.Windows.Controls.ScrollViewer sv) return sv;
            parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
        }
        return null;
    }
}
