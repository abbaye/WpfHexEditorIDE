// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor.Core
// File: Model/DocumentBlockAttributes.cs
// Description:
//     Canonical attribute key names used by loaders, renderers
//     and forensic services on DocumentBlock.Attributes. Keeps
//     mappers and consumers from drifting on string spellings.
// ==========================================================

namespace WpfHexEditor.Editor.DocumentEditor.Core.Model;

/// <summary>Well-known string keys for <see cref="DocumentBlock.Attributes"/>.</summary>
public static class DocumentBlockAttributes
{
    public const string FontFamily    = "fontFamily";
    public const string FontSize      = "fontSize";
    public const string Bold          = "bold";
    public const string Italic        = "italic";
    public const string Underline     = "underline";
    public const string Color         = "color";
    public const string Style         = "style";
    public const string Level         = "level";
    public const string Align         = "align";
    public const string Indent        = "indent";

    /// <summary>The byte payload of an embedded image/binary blob.</summary>
    public const string BinaryData    = "binaryData";

    /// <summary>The declared size of an embedded blob (set when bytes aren't held inline).</summary>
    public const string BinarySize    = "binarySize";

    /// <summary>The archive-relative path to an embedded blob (e.g. <c>word/media/image1.png</c>).</summary>
    public const string ZipEntryName  = "zipEntryName";

    /// <summary>Natural pixel width of an image, as string (preserves loader CultureInfo formatting).</summary>
    public const string NaturalWidth  = "naturalWidth";

    /// <summary>Natural pixel height of an image.</summary>
    public const string NaturalHeight = "naturalHeight";
}
