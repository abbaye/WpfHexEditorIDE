// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Rendering/InlineSegment.cs
// Description:
//     Logical unit of styled text before line-breaking.
//     One segment = one contiguous run of identical style.
//     Consumed by InlineLineBreaker to produce VisualLine[].
// Architecture: Immutable value type; no WPF objects held here.
// ==========================================================

using System.Windows.Media;

namespace WpfHexEditor.Editor.DocumentEditor.Rendering;

/// <summary>
/// A contiguous span of text sharing a single style, before line-breaking.
/// </summary>
internal readonly struct InlineSegment
{
    public readonly string        Text;
    public readonly GlyphTypeface GlyphTypeface;
    public readonly double        Size;          // em size in WPF device-independent pixels
    public readonly Color         Foreground;
    public readonly bool          Underline;
    public readonly bool          Strikethrough;
    /// <summary>
    /// Vertical baseline offset in pixels. Negative = up (superscript), positive = down (subscript).
    /// Zero for normal text.
    /// </summary>
    public readonly double        VerticalOffset;

    public InlineSegment(
        string        text,
        GlyphTypeface glyphTypeface,
        double        size,
        Color         foreground,
        bool          underline      = false,
        bool          strikethrough  = false,
        double        verticalOffset = 0)
    {
        Text           = text;
        GlyphTypeface  = glyphTypeface;
        Size           = size;
        Foreground     = foreground;
        Underline      = underline;
        Strikethrough  = strikethrough;
        VerticalOffset = verticalOffset;
    }

    public bool IsEmpty => string.IsNullOrEmpty(Text);
}
