// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Rendering/VisualLine.cs
// Description:
//     One rendered line produced by InlineLineBreaker.
//     Carries raw glyph data (indices + advances) per segment so that
//     GlyphRun objects are constructed on the fly at the final canvas
//     origin in DrawVisualLines — avoiding the need to mutate or
//     reconstruct frozen objects.
// Architecture: Immutable value carrier; no WPF layout dependency.
// ==========================================================

using System.Collections.Immutable;
using System.Windows;
using System.Windows.Media;

namespace WpfHexEditor.Editor.DocumentEditor.Rendering;

/// <summary>
/// A single rendered line produced by <see cref="InlineLineBreaker"/>:
/// one or more <see cref="PlacedSegment"/> sharing a common baseline.
/// Named <c>InlineVisualLine</c> to avoid collision with the navigation
/// <c>VisualLine</c> record used by <c>CaretNavHelper</c>.
/// </summary>
internal sealed class InlineVisualLine
{
    /// <summary>Segments positioned relative to the block's content-left at X=0, baseline Y=Ascent.</summary>
    public ImmutableArray<PlacedSegment> Segments { get; }

    /// <summary>Distance from line top to baseline (pixels).</summary>
    public double Ascent { get; }

    /// <summary>Distance from baseline to line bottom (pixels).</summary>
    public double Descent { get; }

    /// <summary>Extra inter-line spacing.</summary>
    public double Leading { get; }

    /// <summary>Total line height = Ascent + Descent + Leading.</summary>
    public double LineHeight => Ascent + Descent + Leading;

    /// <summary>Logical width (sum of segment advances).</summary>
    public double Width { get; }

    /// <summary>Block-relative char index of the first character on this line.</summary>
    public int CharStart { get; }

    /// <summary>Block-relative char index one past the last character on this line.</summary>
    public int CharEnd { get; }

    public InlineVisualLine(ImmutableArray<PlacedSegment> segments, double ascent, double descent, double leading,
                            int charStart = 0, int charEnd = 0)
    {
        Segments  = segments;
        Ascent    = ascent;
        Descent   = descent;
        Leading   = leading;
        Width     = segments.IsDefaultOrEmpty ? 0 : segments.Sum(s => s.Width);
        CharStart = charStart;
        CharEnd   = charEnd;
    }
}

/// <summary>
/// Raw glyph data for one styled span within an <see cref="InlineVisualLine"/>.
/// X offset is relative to content-left; GlyphRun is built at draw time with the
/// absolute canvas origin to avoid rebasing pre-frozen runs.
/// </summary>
internal sealed class PlacedSegment
{
    /// <summary>X offset from the block's content-left edge (block-relative, not canvas).</summary>
    public double          OffsetX        { get; }
    public GlyphTypeface   GlyphTypeface  { get; }
    public double          EmSize         { get; }
    public IList<ushort>   GlyphIndices   { get; }
    public IList<double>   AdvanceWidths  { get; }
    public double          Width          { get; }
    public Color           Foreground     { get; }
    public bool            Underline      { get; }
    public bool            Strikethrough  { get; }
    /// <summary>Block-relative char index of the first character in this segment.</summary>
    public int             CharStart      { get; }

    /// <summary>Underline offset below baseline (positive = below, font units scaled to pixels).</summary>
    public double UnderlineOffset     { get; }
    /// <summary>Strikethrough offset above baseline (positive = above, font units scaled to pixels).</summary>
    public double StrikethroughOffset { get; }

    public PlacedSegment(
        double         offsetX,
        GlyphTypeface  glyphTypeface,
        double         emSize,
        IList<ushort>  glyphIndices,
        IList<double>  advanceWidths,
        double         width,
        Color          foreground,
        bool           underline,
        bool           strikethrough,
        double         underlineOffset,
        double         strikethroughOffset,
        int            charStart = 0)
    {
        OffsetX             = offsetX;
        GlyphTypeface       = glyphTypeface;
        EmSize              = emSize;
        GlyphIndices        = glyphIndices;
        AdvanceWidths       = advanceWidths;
        Width               = width;
        Foreground          = foreground;
        Underline           = underline;
        Strikethrough       = strikethrough;
        UnderlineOffset     = underlineOffset;
        StrikethroughOffset = strikethroughOffset;
        CharStart           = charStart;
    }

    /// <summary>
    /// Builds a <see cref="GlyphRun"/> at the given absolute canvas baseline origin.
    /// </summary>
    public GlyphRun BuildGlyphRun(Point canvasBaseline, double pixelsPerDip) =>
        new(GlyphTypeface,
            bidiLevel:       0,
            isSideways:      false,
            renderingEmSize: EmSize,
            pixelsPerDip:    (float)pixelsPerDip,
            glyphIndices:    GlyphIndices,
            baselineOrigin:  canvasBaseline,
            advanceWidths:   AdvanceWidths,
            glyphOffsets:    null,
            characters:      null,
            deviceFontName:  null,
            clusterMap:      null,
            caretStops:      null,
            language:        System.Windows.Markup.XmlLanguage.GetLanguage("en-us"));
}
