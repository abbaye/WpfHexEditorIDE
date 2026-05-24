// Project     : WpfHexEditor.App
// File        : EncodingPalette.cs
// Description : Single source of truth for per-encoding AND per-kind colors shared across
//               StringExtractionPanel, StringOffsetHeatmap, StringTimelineView, and exporters.
// Architecture: Static data-only class; no dependencies on UI framework types beyond Color/Brush.

using System.Windows.Media;

namespace WpfHexEditor.App.BinaryAnalysis.Services;

public static class EncodingPalette
{
    // ── Encoding palette ──────────────────────────────────────────────────────

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

    // ── Kind palette ─────────────────────────────────────────────────────────
    // Colors chosen for contrast on a dark background and colorblind accessibility.

    private static readonly (StringKind kind, byte r, byte g, byte b, string glyph)[] _kindEntries =
    [
        (StringKind.Email,       0x4F, 0xC3, 0xF7, "@"),    // sky blue
        (StringKind.Url,         0x81, 0xC7, 0x84, char.ConvertFromUtf32(0xE71B)), // green  — link glyph
        (StringKind.PathWin,     0xFF, 0xB7, 0x4D, char.ConvertFromUtf32(0xE8B7)), // amber  — folder
        (StringKind.PathUnix,    0xFF, 0xF1, 0x76, "/"),    // yellow
        (StringKind.Guid,        0xCE, 0x93, 0xD8, "{"),    // violet
        (StringKind.RegistryKey, 0xFF, 0x8A, 0x65, char.ConvertFromUtf32(0xE90F)), // orange — registry
        (StringKind.Version,     0xF4, 0x8F, 0xB1, "#"),    // pink
        (StringKind.IpV4,        0x80, 0xDE, 0xEA, char.ConvertFromUtf32(0xE839)), // cyan   — network
        (StringKind.IpV6,        0x79, 0x86, 0xCB, char.ConvertFromUtf32(0xE839)), // indigo — network
        (StringKind.HexHash,     0xEF, 0x9A, 0x9A, char.ConvertFromUtf32(0xE899)), // coral  — lock/hash
    ];

    public static readonly IReadOnlyDictionary<StringKind, SolidColorBrush> KindBrushes =
        _kindEntries.ToDictionary(
            e => e.kind,
            e => Freeze(new SolidColorBrush(Color.FromRgb(e.r, e.g, e.b))));

    public static readonly IReadOnlyDictionary<StringKind, Color> KindColors =
        _kindEntries.ToDictionary(e => e.kind, e => Color.FromRgb(e.r, e.g, e.b));

    /// <summary>Unicode glyph for each kind (Segoe MDL2 Assets or ASCII fallback).</summary>
    public static readonly IReadOnlyDictionary<StringKind, string> KindGlyphs =
        _kindEntries.ToDictionary(e => e.kind, e => e.glyph);

    private static SolidColorBrush Freeze(SolidColorBrush b) { b.Freeze(); return b; }
}
