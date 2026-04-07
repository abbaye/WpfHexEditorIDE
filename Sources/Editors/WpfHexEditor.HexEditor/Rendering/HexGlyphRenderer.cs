// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: Rendering/HexGlyphRenderer.cs
// Description:
//     High-performance text renderer for HexViewport using WPF GlyphRun
//     instead of FormattedText, eliminating per-draw layout overhead.
//
// Architecture Notes:
//     Flyweight: GlyphTypeface instances cached statically by Typeface.
//     Fallback: fonts that do not expose a GlyphTypeface transparently
//               fall back to null — callers must use FormattedText in that case.
//     One instance per (typeface, fontSize, pixelsPerDip) combination.
//     Recreate when any of those change (font, DPI).
// ==========================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace WpfHexEditor.HexEditor.Rendering;

/// <summary>
/// Renders short text strings (hex bytes, ASCII chars, offset labels) onto a
/// <see cref="DrawingContext"/> using <see cref="GlyphRun"/> for maximum throughput.
/// When the font does not expose a <see cref="GlyphTypeface"/>, falls back gracefully
/// (renderer is null-safe — callers detect <c>IsAvailable == false</c> and use FormattedText).
/// </summary>
internal sealed class HexGlyphRenderer
{
    #region Static GlyphTypeface cache

    private static readonly Dictionary<Typeface, GlyphTypeface?> _gtCache = new();

    private static GlyphTypeface? ResolveGlyphTypeface(Typeface tf)
    {
        if (_gtCache.TryGetValue(tf, out var cached))
            return cached;

        GlyphTypeface? gt = tf.TryGetGlyphTypeface(out var resolved) ? resolved : null;
        _gtCache[tf] = gt;
        return gt;
    }

    #endregion

    #region State

    private readonly double _fontSize;
    private readonly double _pixelsPerDip;
    private readonly GlyphTypeface? _gt;

    /// <summary>
    /// True when GlyphRun rendering is available.
    /// False when the font does not expose a GlyphTypeface — callers must fall back to FormattedText.
    /// </summary>
    public bool IsAvailable => _gt != null;

    /// <summary>Ascender offset from line top to GlyphRun baseline.</summary>
    public double Baseline { get; }

    /// <summary>Em-height of the font at the configured size.</summary>
    public double CharHeight { get; }

    /// <summary>Advance width of a single 'M' glyph — typical monospace char width.</summary>
    public double CharWidth { get; }

    #endregion

    #region Constructor

    public HexGlyphRenderer(Typeface typeface, double fontSize, double pixelsPerDip)
    {
        _fontSize      = fontSize;
        _pixelsPerDip  = pixelsPerDip;
        _gt            = ResolveGlyphTypeface(typeface);

        if (_gt != null)
        {
            CharWidth  = MeasureAdvance(_gt, 'M', fontSize);
            CharHeight = _gt.Height * fontSize;
            Baseline   = _gt.Baseline * fontSize;
        }
        else
        {
            // Fallback heuristics — only used when IsAvailable == false
            var ft = new FormattedText("M", CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, typeface, fontSize, Brushes.Black, pixelsPerDip);
            CharWidth  = ft.Width;
            CharHeight = ft.Height;
            Baseline   = CharHeight * 0.8;
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Draws <paramref name="text"/> at the given position using a GlyphRun.
    /// Caller must ensure <see cref="IsAvailable"/> is true before calling.
    /// </summary>
    /// <param name="dc">Active drawing context.</param>
    /// <param name="text">Text to render (non-null, non-empty).</param>
    /// <param name="x">Left edge in canvas coordinates.</param>
    /// <param name="baselineY">Pre-computed baseline Y (<c>lineTopY + Baseline</c>).</param>
    /// <param name="brush">Foreground brush.</param>
    public void RenderText(DrawingContext dc, string text, double x, double baselineY, Brush brush)
    {
        if (_gt == null || string.IsNullOrEmpty(text))
            return;

        RenderWithGlyphRun(dc, text, x, baselineY, _gt, brush);
    }

    /// <summary>
    /// Measures the advance width of <paramref name="text"/> using the GlyphTypeface.
    /// Falls back to a simple character-count × CharWidth estimate when unavailable.
    /// </summary>
    public double MeasureString(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        if (_gt == null)
            return text.Length * CharWidth;

        var charMap = _gt.CharacterToGlyphMap;
        double total = 0;
        foreach (char ch in text)
        {
            charMap.TryGetValue(ch, out ushort gi);
            total += _gt.AdvanceWidths[gi] * _fontSize;
        }
        return total;
    }

    #endregion

    #region Private helpers

    private void RenderWithGlyphRun(DrawingContext dc, string text,
                                    double x, double baselineY,
                                    GlyphTypeface gt, Brush brush)
    {
        var glyphIndices  = new List<ushort>(text.Length);
        var advanceWidths = new List<double>(text.Length);
        var charMap       = gt.CharacterToGlyphMap;

        foreach (char ch in text)
        {
            if (!charMap.TryGetValue(ch, out ushort gi))
                charMap.TryGetValue('\uFFFD', out gi);

            glyphIndices.Add(gi);
            advanceWidths.Add(gt.AdvanceWidths[gi] * _fontSize);
        }

        var glyphRun = new GlyphRun(
            gt,
            bidiLevel:        0,
            isSideways:       false,
            renderingEmSize:  _fontSize,
            pixelsPerDip:     (float)_pixelsPerDip,
            glyphIndices:     glyphIndices,
            baselineOrigin:   new Point(x, baselineY),
            advanceWidths:    advanceWidths,
            glyphOffsets:     null,
            characters:       null,
            deviceFontName:   null,
            clusterMap:       null,
            caretStops:       null,
            language:         null);

        dc.DrawGlyphRun(brush, glyphRun);
    }

    private static double MeasureAdvance(GlyphTypeface gt, char ch, double fontSize)
    {
        gt.CharacterToGlyphMap.TryGetValue(ch, out ushort gi);
        return gt.AdvanceWidths[gi] * fontSize;
    }

    #endregion
}
