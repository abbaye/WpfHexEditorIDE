// Project     : WpfHexEditor.App
// File        : EncodingPalette.cs
// Description : Single source of truth for per-encoding colors shared across
//               StringExtractionPanel, StringOffsetHeatmap, StringTimelineView, and exporters.
// Architecture: Static data-only class; no dependencies on UI framework types beyond Color/Brush.

using System.Windows.Media;

namespace WpfHexEditor.App.BinaryAnalysis.Services;

public static class EncodingPalette
{
    private static readonly (StringEncoding enc, byte r, byte g, byte b)[] _entries =
    [
        (StringEncoding.Tbl,          0x4C, 0xAF, 0x50),
        (StringEncoding.TblDte,       0x4C, 0xAF, 0x50),
        (StringEncoding.TblMte,       0x4C, 0xAF, 0x50),
        (StringEncoding.Ascii,        0x42, 0x8B, 0xCA),
        (StringEncoding.Utf8,         0x00, 0xBC, 0xD4),
        (StringEncoding.Utf16Le,      0x00, 0xBC, 0xD4),
        (StringEncoding.Utf16Be,      0x00, 0xBC, 0xD4),
        (StringEncoding.Ebcdic,       0xFF, 0x98, 0x00),
        (StringEncoding.EbcdicNoSpec, 0xFF, 0x98, 0x00),
        (StringEncoding.Latin1,       0xAB, 0x47, 0xBC),
    ];

    private static readonly (byte r, byte g, byte b) _fallback = (0x90, 0x90, 0x90);

    public static readonly IReadOnlyDictionary<StringEncoding, Color> Colors =
        _entries.ToDictionary(e => e.enc, e => Color.FromRgb(e.r, e.g, e.b));

    public static readonly IReadOnlyDictionary<StringEncoding, SolidColorBrush> Brushes =
        _entries.ToDictionary(
            e => e.enc,
            e => Freeze(new SolidColorBrush(Color.FromRgb(e.r, e.g, e.b))));

    public static readonly SolidColorBrush FallbackBrush =
        Freeze(new SolidColorBrush(Color.FromRgb(_fallback.r, _fallback.g, _fallback.b)));

    public static readonly Color FallbackColor =
        Color.FromRgb(_fallback.r, _fallback.g, _fallback.b);

    /// <summary>Returns the hex color string for a given encoding (e.g. "#428BCA").</summary>
    public static string HexColor(StringEncoding enc) =>
        Colors.TryGetValue(enc, out var c)
            ? $"#{c.R:X2}{c.G:X2}{c.B:X2}"
            : $"#{_fallback.r:X2}{_fallback.g:X2}{_fallback.b:X2}";

    private static SolidColorBrush Freeze(SolidColorBrush b) { b.Freeze(); return b; }
}
