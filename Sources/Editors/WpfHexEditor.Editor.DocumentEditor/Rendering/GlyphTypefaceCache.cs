// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Rendering/GlyphTypefaceCache.cs
// Description:
//     Thread-safe static cache: (family, bold, italic) → GlyphTypeface.
//     Avoids repeated Typeface + TryGetGlyphTypeface() per block/render.
// Architecture: Static, lock-free reads via ConcurrentDictionary.
// ==========================================================

using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Media;

namespace WpfHexEditor.Editor.DocumentEditor.Rendering;

internal static class GlyphTypefaceCache
{
    private static readonly ConcurrentDictionary<CacheKey, GlyphTypeface> _cache = new();

    // Fallback typefaces resolved once
    private static GlyphTypeface? _fallbackRegular;
    private static GlyphTypeface? _fallbackBold;
    private static GlyphTypeface? _fallbackItalic;
    private static GlyphTypeface? _fallbackBoldItalic;

    static GlyphTypefaceCache()
    {
        _fallbackRegular   = Resolve("Segoe UI", false, false);
        _fallbackBold      = Resolve("Segoe UI", true,  false) ?? _fallbackRegular;
        _fallbackItalic    = Resolve("Segoe UI", false, true)  ?? _fallbackRegular;
        _fallbackBoldItalic= Resolve("Segoe UI", true,  true)  ?? _fallbackBold;
    }

    /// <summary>
    /// Returns a <see cref="GlyphTypeface"/> for the given family/style, or the Segoe UI
    /// fallback if the font cannot be resolved.
    /// </summary>
    public static GlyphTypeface Get(string family, bool bold, bool italic)
    {
        var key = new CacheKey(family, bold, italic);
        return _cache.GetOrAdd(key, k =>
        {
            var gt = Resolve(k.Family, k.Bold, k.Italic);
            return gt ?? FallbackFor(k.Bold, k.Italic);
        });
    }

    /// <summary>Returns the Segoe UI fallback matching the requested bold/italic combination.</summary>
    public static GlyphTypeface Fallback(bool bold = false, bool italic = false) =>
        FallbackFor(bold, italic);

    // ── internals ────────────────────────────────────────────────────────────

    private static GlyphTypeface? Resolve(string family, bool bold, bool italic)
    {
        try
        {
            var weight  = bold   ? FontWeights.Bold    : FontWeights.Normal;
            var style   = italic ? FontStyles.Italic   : FontStyles.Normal;
            var typeface = new Typeface(new FontFamily(family), style, weight, FontStretches.Normal);
            return typeface.TryGetGlyphTypeface(out var gt) ? gt : null;
        }
        catch
        {
            return null;
        }
    }

    private static GlyphTypeface FallbackFor(bool bold, bool italic) =>
        (bold, italic) switch
        {
            (true,  true)  => _fallbackBoldItalic ?? _fallbackRegular!,
            (true,  false) => _fallbackBold       ?? _fallbackRegular!,
            (false, true)  => _fallbackItalic     ?? _fallbackRegular!,
            _              => _fallbackRegular!,
        };

    private readonly record struct CacheKey(string Family, bool Bold, bool Italic);
}
