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

    public InlineSegment(
        string        text,
        GlyphTypeface glyphTypeface,
        double        size,
        Color         foreground,
        bool          underline     = false,
        bool          strikethrough = false)
    {
        Text          = text;
        GlyphTypeface = glyphTypeface;
        Size          = size;
        Foreground    = foreground;
        Underline     = underline;
        Strikethrough = strikethrough;
    }

    public bool IsEmpty => string.IsNullOrEmpty(Text);
}
