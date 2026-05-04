// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Rendering/InlineLineBreaker.cs
// Description:
//     Converts InlineSegment[] into InlineVisualLine[] using GlyphRun
//     composition. Word-wraps at maxW using per-glyph advance widths.
//     Produces frozen PlacedSegment/GlyphRun objects ready for
//     DrawingContext.DrawGlyphRun — zero FormattedText overhead.
// Architecture:
//     Pure static; no WPF element dependency. Thread-safe (no shared
//     mutable state). Segments are iterated once; lines emitted lazily.
// ==========================================================

using System.Collections.Immutable;
using System.Windows;
using System.Windows.Media;

namespace WpfHexEditor.Editor.DocumentEditor.Rendering;

internal static class InlineLineBreaker
{
    private const double TabStopWidth  = 48.0;  // pixels per tab stop
    private const double MinLineHeight = 4.0;   // guard against zero-height empty lines
    private const double PixelsPerDip  = 1.0;   // overridden per-call where DPI is known

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Breaks <paramref name="segments"/> into <see cref="InlineVisualLine"/> objects that fit
    /// within <paramref name="maxWidth"/> device-independent pixels.
    /// </summary>
    /// <param name="segments">Ordered inline segments for one paragraph/block.</param>
    /// <param name="maxWidth">Available content width in WPF DIPs.</param>
    /// <param name="pixelsPerDip">DPI scaling factor (use <c>VisualTreeHelper.GetDpi</c>).</param>
    public static IReadOnlyList<InlineVisualLine> Break(
        IReadOnlyList<InlineSegment> segments,
        double maxWidth,
        double pixelsPerDip = 1.0)
    {
        if (segments.Count == 0 || maxWidth <= 0)
            return [];

        var lines   = new List<InlineVisualLine>();
        var pending = new List<PendingSegment>(); // segments accumulated on current line
        double lineX = 0;                         // current X pen position on the line
        int    lineCharStart = 0;                 // block-relative char index of line start
        int    blockCharPos  = 0;                 // running block-relative char cursor

        // Metrics for the current line (updated per segment)
        double lineAscent  = 0;
        double lineDescent = 0;
        double lineLeading = 0;

        // ── helpers ──────────────────────────────────────────────────────────

        void FlushLine()
        {
            if (pending.Count == 0) return;

            double ascent  = Math.Max(MinLineHeight, lineAscent);
            double descent = lineDescent;
            double leading = lineLeading;

            var placed = ImmutableArray.CreateBuilder<PlacedSegment>(pending.Count);
            double x = 0;
            int lineCharEnd = lineCharStart;
            foreach (var ps in pending)
            {
                var gt   = ps.Seg.GlyphTypeface;
                double uOff = -gt.UnderlinePosition      * ps.Seg.Size; // positive = below baseline
                double sOff =  gt.StrikethroughPosition  * ps.Seg.Size; // positive = above baseline

                placed.Add(new PlacedSegment(
                    offsetX:             x,
                    glyphTypeface:       gt,
                    emSize:              ps.Seg.Size,
                    glyphIndices:        ps.Glyphs,
                    advanceWidths:       ps.Advances,
                    width:               ps.TotalWidth,
                    foreground:          ps.Seg.Foreground,
                    underline:           ps.Seg.Underline,
                    strikethrough:       ps.Seg.Strikethrough,
                    underlineOffset:     uOff,
                    strikethroughOffset: sOff,
                    charStart:           ps.CharStart));

                x += ps.TotalWidth;
                lineCharEnd = ps.CharStart + ps.CharCount;
            }

            lines.Add(new InlineVisualLine(placed.MoveToImmutable(), ascent, descent, leading,
                                           lineCharStart, lineCharEnd));
            pending.Clear();
            lineX         = 0;
            lineCharStart = lineCharEnd;
            lineAscent    = lineDescent = lineLeading = 0;
        }

        void UpdateLineMetrics(InlineSegment seg)
        {
            var gt   = seg.GlyphTypeface;
            double a = gt.Baseline              * seg.Size;
            double d = (gt.Height - gt.Baseline) * seg.Size;
            // Leading = extra gap suggested by the font designer (Height > Baseline + descent for many fonts)
            double l = Math.Max(0, (gt.Height - 1.0) * seg.Size * 0.12);
            if (a > lineAscent)  lineAscent  = a;
            if (d > lineDescent) lineDescent = d;
            if (l > lineLeading) lineLeading = l;
        }

        // ── main loop — iterate segments ─────────────────────────────────────

        foreach (var seg in segments)
        {
            if (seg.IsEmpty) continue;

            var gt     = seg.GlyphTypeface;
            double size = seg.Size;

            // Decompose segment text into word tokens to allow clean line breaks
            // Token = sequence of non-whitespace OR a single whitespace char
            int i = 0;
            while (i < seg.Text.Length)
            {
                int tokenCharStart = blockCharPos + i;

                // Collect a "word" token (non-space run) + optional trailing spaces
                var (glyphs, advances, tokenW, nextI) = MeasureToken(seg, i, gt, size);
                int tokenCharCount = nextI - i;
                i = nextI;

                // Handle explicit newline embedded in token
                if (glyphs.Count == 0 && advances.Count == 0 && tokenW < 0)
                {
                    // Sentinel: explicit \n — flush and start new line
                    FlushLine();
                    continue;
                }

                // If the token alone is wider than maxWidth we must force-break mid-token
                if (tokenW > maxWidth && pending.Count == 0)
                {
                    // Force-split: chunk glyph-by-glyph
                    var splitLines = ForceSplit(seg, glyphs, advances, maxWidth, pixelsPerDip,
                                                ref lineX, ref lineAscent, ref lineDescent, ref lineLeading,
                                                pending, ref lineCharStart, tokenCharStart);
                    foreach (var sl in splitLines)
                        lines.Add(sl);
                    continue;
                }

                // If adding this token would overflow, flush the current line first
                if (lineX + tokenW > maxWidth && pending.Count > 0)
                    FlushLine();

                // Accumulate token onto current line
                UpdateLineMetrics(seg);
                pending.Add(new PendingSegment(seg, glyphs, advances, tokenW, tokenCharStart, tokenCharCount));
                lineX += tokenW;
            }

            blockCharPos += seg.Text.Length;
        }

        // Flush last line
        FlushLine();

        return lines;
    }

    // ── Token measurement ─────────────────────────────────────────────────────

    /// <summary>
    /// Measures one "word" token starting at <paramref name="start"/> in <paramref name="seg"/>.
    /// A token is a non-space run followed by any trailing spaces (they wrap together).
    /// Returns sentinel (empty, empty, -1, start+1) for an explicit newline character.
    /// </summary>
    private static (List<ushort> Glyphs, List<double> Advances, double Width, int NextIndex)
        MeasureToken(InlineSegment seg, int start, GlyphTypeface gt, double size)
    {
        var   text    = seg.Text;
        var   glyphs  = new List<ushort>();
        var   advances= new List<double>();
        double width  = 0;
        int   i       = start;

        if (text[i] == '\n')
            return ([], [], -1.0, i + 1); // explicit newline sentinel

        if (text[i] == '\t')
        {
            // Tab → advance to next tab stop (relative to a 0-origin line — caller owns tab stop logic)
            ushort spaceGlyph = GetGlyphIndex(gt, ' ');
            double adv        = TabStopWidth;
            glyphs.Add(spaceGlyph);
            advances.Add(adv);
            return (glyphs, advances, adv, i + 1);
        }

        // Collect non-whitespace characters
        while (i < text.Length && text[i] != ' ' && text[i] != '\t' && text[i] != '\n')
        {
            var (g, a) = MeasureChar(gt, text[i], size);
            glyphs.Add(g);
            advances.Add(a);
            width += a;
            i++;
        }

        // Collect trailing spaces (they belong to the same token for break purposes)
        while (i < text.Length && text[i] == ' ')
        {
            var (g, a) = MeasureChar(gt, ' ', size);
            glyphs.Add(g);
            advances.Add(a);
            width += a;
            i++;
        }

        return (glyphs, advances, width, i);
    }

    private static (ushort GlyphIndex, double Advance) MeasureChar(GlyphTypeface gt, char c, double size)
    {
        ushort gi  = GetGlyphIndex(gt, c);
        double adv = gt.AdvanceWidths[gi] * size;
        return (gi, adv);
    }

    private static ushort GetGlyphIndex(GlyphTypeface gt, char c)
    {
        if (gt.CharacterToGlyphMap.TryGetValue(c, out ushort gi))
            return gi;
        // Replacement glyph fallback
        gt.CharacterToGlyphMap.TryGetValue('�', out gi);
        return gi;
    }

    // ── Force-split for tokens wider than maxWidth ────────────────────────────

    /// <summary>
    /// Splits a single oversized token (word longer than <paramref name="maxWidth"/>) across
    /// multiple lines, appending fully-split lines to <paramref name="lines"/>.
    /// Partial last chunk is pushed into <paramref name="pending"/> for the caller to continue.
    /// </summary>
    private static IEnumerable<InlineVisualLine> ForceSplit(
        InlineSegment   seg,
        List<ushort>    glyphs,
        List<double>    advances,
        double          maxWidth,
        double          pixelsPerDip,
        ref double      lineX,
        ref double      lineAscent,
        ref double      lineDescent,
        ref double      lineLeading,
        List<PendingSegment> pending,
        ref int         lineCharStart,
        int             tokenCharStart)
    {
        var   gt       = seg.GlyphTypeface;
        double ascent   = gt.Baseline              * seg.Size;
        double descent  = (gt.Height - gt.Baseline)  * seg.Size;
        double leading  = Math.Max(0, (gt.Height - 1.0) * seg.Size * 0.12);

        var chunkGlyphs   = new List<ushort>();
        var chunkAdvances = new List<double>();
        double chunkW     = 0;
        int    chunkCharStart = tokenCharStart;

        var result = new List<InlineVisualLine>();

        for (int k = 0; k < glyphs.Count; k++)
        {
            double a = advances[k];
            if (chunkW + a > maxWidth && chunkGlyphs.Count > 0)
            {
                // Emit a full line for the chunk so far
                double uOff = -gt.UnderlinePosition     * seg.Size;
                double sOff =  gt.StrikethroughPosition * seg.Size;
                int chunkCount = chunkGlyphs.Count;
                var ps = new PlacedSegment(
                    offsetX: 0, glyphTypeface: gt, emSize: seg.Size,
                    glyphIndices: new List<ushort>(chunkGlyphs),
                    advanceWidths: new List<double>(chunkAdvances),
                    width: chunkW, foreground: seg.Foreground,
                    underline: seg.Underline, strikethrough: seg.Strikethrough,
                    underlineOffset: uOff, strikethroughOffset: sOff,
                    charStart: chunkCharStart);
                result.Add(new InlineVisualLine(ImmutableArray.Create(ps),
                                          Math.Max(MinLineHeight, ascent), descent, leading,
                                          lineCharStart, chunkCharStart + chunkCount));
                lineCharStart = chunkCharStart + chunkCount;
                chunkCharStart = lineCharStart;
                chunkGlyphs.Clear();
                chunkAdvances.Clear();
                chunkW = 0;
            }

            chunkGlyphs.Add(glyphs[k]);
            chunkAdvances.Add(a);
            chunkW += a;
        }

        // Remaining chunk goes into pending (caller will continue accumulating)
        if (chunkGlyphs.Count > 0)
        {
            if (ascent > lineAscent)  lineAscent  = ascent;
            if (descent > lineDescent) lineDescent = descent;
            if (leading > lineLeading) lineLeading = leading;
            pending.Add(new PendingSegment(seg, chunkGlyphs, chunkAdvances, chunkW,
                                           chunkCharStart, chunkGlyphs.Count));
            lineX += chunkW;
        }

        return result;
    }

    // ── Internal accumulation type ────────────────────────────────────────────

    private readonly struct PendingSegment(
        InlineSegment seg,
        List<ushort>  glyphs,
        List<double>  advances,
        double        totalWidth,
        int           charStart  = 0,
        int           charCount  = 0)
    {
        public readonly InlineSegment Seg        = seg;
        public readonly List<ushort>  Glyphs     = glyphs;
        public readonly List<double>  Advances   = advances;
        public readonly double        TotalWidth = totalWidth;
        public readonly int           CharStart  = charStart;
        public readonly int           CharCount  = charCount;
    }
}
