// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: GlyphRunRenderer.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-05
// Description:
//     High-performance token renderer that uses WPF GlyphRun instead of
//     FormattedText, eliminating per-token layout overhead during OnRender.
//     Measured improvement: ~2× throughput on documents > 5 000 lines.
//
// Architecture Notes:
//     Flyweight Pattern  — GlyphTypeface instances are shared via a static
//                          dictionary keyed on Typeface (font family + style +
//                          weight + stretch).  Only resolved once per unique font.
//     Fallback Strategy  — Fonts that do not expose a GlyphTypeface (symbol
//                          fonts, some open-type features) transparently fall
//                          back to FormattedText so rendering never breaks.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using WpfHexEditor.Editor.CodeEditor.Helpers;
// IEnumerable<T> in BuildLineGlyphRuns comes from System.Collections.Generic — already included.

namespace WpfHexEditor.Editor.CodeEditor.Rendering;

/// <summary>
/// Renders <see cref="SyntaxHighlightToken"/> sequences onto a <see cref="DrawingContext"/>
/// using <see cref="GlyphRun"/> for maximum throughput.
/// One instance per (regular typeface, bold typeface, font size, DPI) combination.
/// Recreate when any of those change.
/// </summary>
public sealed class GlyphRunRenderer
{
    #region Static GlyphTypeface cache — shared across all GlyphRunRenderer instances

    // Maps a Typeface (value type) → resolved GlyphTypeface (null = fallback needed).
    private static readonly Dictionary<Typeface, GlyphTypeface?> _gtCache
        = new(TypefaceEqualityComparer.Instance);

    private static GlyphTypeface? ResolveGlyphTypeface(Typeface typeface)
    {
        if (_gtCache.TryGetValue(typeface, out var cached))
            return cached;

        GlyphTypeface? gt = typeface.TryGetGlyphTypeface(out var resolved) ? resolved : null;
        _gtCache[typeface] = gt;
        return gt;
    }

    #endregion

    #region Constants

    // Number of space-widths used to render a single tab character.
    private const int TabSize = 4;

    #endregion

    #region Instance state

    private readonly Typeface       _regularTypeface;
    private readonly Typeface       _boldTypeface;
    private readonly double         _fontSize;
    private readonly double         _pixelsPerDip;

    private readonly GlyphTypeface? _regularGt;
    private readonly GlyphTypeface? _boldGt;

    /// <summary>
    /// Width of a single character in the regular monospace font.
    /// Computed from the advance width of the glyph for 'M'.
    /// </summary>
    public double CharWidth  { get; }

    /// <summary>
    /// Total em-height of the regular font at the current size.
    /// Does not include external leading; callers add their own padding.
    /// </summary>
    public double CharHeight { get; }

    /// <summary>
    /// Pre-computed ascender offset from the top of a line to the GlyphRun baseline.
    /// Usage: <c>baselineY = lineTopY + Baseline</c>
    /// </summary>
    public double Baseline { get; }

    /// <summary>
    /// Computes the visual pixel X offset for a given character column in
    /// <paramref name="lineText"/>, expanding tab characters to
    /// <see cref="TabSize"/> character-widths each.
    /// </summary>
    /// <param name="lineText">The raw text of the line (may contain tab characters).</param>
    /// <param name="charColumn">The zero-based character index to measure up to.</param>
    /// <returns>Visual X offset in device-independent pixels.</returns>
    public double ComputeVisualX(string lineText, int charColumn)
    {
        // Fast path: no tabs — O(1)
        if (lineText.IndexOf('\t') < 0)
            return Math.Min(charColumn, lineText.Length) * CharWidth;

        double visualX = 0;
        int limit = Math.Min(charColumn, lineText.Length);
        for (int i = 0; i < limit; i++)
            visualX += lineText[i] == '\t' ? CharWidth * TabSize : CharWidth;
        return visualX;
    }

    /// <summary>
    /// Reverse of <see cref="ComputeVisualX"/>: given a target pixel offset in the text area,
    /// returns the zero-based character column whose left edge is closest to that offset.
    /// Tab characters expand to <see cref="TabSize"/> character-widths.
    /// </summary>
    public int ComputeColumnFromVisualX(string lineText, double targetVisualX)
    {
        // Fast path: no tabs — O(1)
        if (lineText.IndexOf('\t') < 0)
            return Math.Max(0, Math.Min(lineText.Length, (int)((targetVisualX + CharWidth / 2) / CharWidth)));

        double visualX = 0;
        for (int i = 0; i < lineText.Length; i++)
        {
            double charW = lineText[i] == '\t' ? CharWidth * TabSize : CharWidth;
            if (targetVisualX < visualX + charW / 2)
                return i;
            visualX += charW;
        }
        return lineText.Length;
    }

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a renderer for the given font configuration.
    /// </summary>
    /// <param name="regular">Regular-weight typeface (used for most tokens).</param>
    /// <param name="bold">Bold-weight typeface (used for keyword tokens etc.).</param>
    /// <param name="fontSize">Font size in WPF device-independent pixels.</param>
    /// <param name="pixelsPerDip">DPI scale from <see cref="VisualTreeHelper.GetDpi"/>.</param>
    public GlyphRunRenderer(Typeface regular, Typeface bold, double fontSize, double pixelsPerDip)
    {
        _regularTypeface = regular;
        _boldTypeface    = bold;
        _fontSize        = fontSize;
        _pixelsPerDip    = pixelsPerDip;

        _regularGt = ResolveGlyphTypeface(regular);
        _boldGt    = ResolveGlyphTypeface(bold);

        if (_regularGt != null)
        {
            CharWidth  = MeasureGlyphAdvanceWidth(_regularGt, 'M', fontSize);
            CharHeight = _regularGt.Height * fontSize;
            Baseline   = _regularGt.Baseline * fontSize;
        }
        else
        {
            // GlyphTypeface unavailable — measure via FormattedText as last resort.
            var ft = MakeFormattedText("M", regular, fontSize, Brushes.Black, pixelsPerDip);
            CharWidth  = ft.Width;
            CharHeight = ft.Height;
            Baseline   = CharHeight * 0.8; // heuristic ascender fraction
        }
    }

    #endregion

    #region Public rendering API

    /// <summary>
    /// Renders a single <see cref="SyntaxHighlightToken"/> onto <paramref name="dc"/>.
    /// Uses GlyphRun when possible; silently falls back to FormattedText otherwise.
    /// </summary>
    /// <param name="dc">Active WPF drawing context (inside an OnRender call).</param>
    /// <param name="token">Token to render (text, color, bold/italic flags).</param>
    /// <param name="tokenX">Left pixel edge of the token in canvas coordinates.</param>
    /// <param name="lineTopY">Top pixel edge of the current line in canvas coordinates.</param>
    /// <param name="baselineY">Pre-computed baseline Y (<c>lineTopY + Baseline</c>).</param>
    public void RenderToken(DrawingContext dc, in SyntaxHighlightToken token,
                            double tokenX, double lineTopY, double baselineY)
    {
        if (string.IsNullOrEmpty(token.Text))
            return;

        var gt = token.IsBold ? _boldGt : _regularGt;

        if (gt != null)
        {
            RenderWithGlyphRun(dc, token.Text, tokenX, baselineY, gt, token.Foreground);
        }
        else
        {
            // Fallback: FormattedText (handles special / symbol fonts).
            var tf = token.IsBold ? _boldTypeface : _regularTypeface;
            var ft = MakeFormattedText(token.Text, tf, _fontSize, token.Foreground, _pixelsPerDip);
            if (token.IsItalic)
                ft.SetFontStyle(FontStyles.Italic);
            dc.DrawText(ft, new Point(tokenX, lineTopY));
        }
    }

    #endregion

    #region Private helpers

    /// <summary>
    /// Rounds <paramref name="value"/> to the nearest physical pixel boundary.
    /// Prevents sub-pixel glyph positioning that causes blurry ClearType rendering.
    /// </summary>
    private double SnapToPixel(double value)
        => Math.Round(value * _pixelsPerDip) / _pixelsPerDip;

    /// <summary>Builds and draws a GlyphRun for the given text string.</summary>
    private void RenderWithGlyphRun(DrawingContext dc, string text,
                                    double x, double baselineY,
                                    GlyphTypeface gt, Brush brush)
    {
        var glyphIndices  = new List<ushort>(text.Length);
        var advanceWidths = new List<double>(text.Length);
        var charMap       = gt.CharacterToGlyphMap;

        foreach (char ch in text)
        {
            if (ch == '\t')
            {
                // Tab has no glyph in most monospace fonts — use space glyph with TabSize advance.
                charMap.TryGetValue(' ', out ushort spaceGi);
                glyphIndices.Add(spaceGi);
                advanceWidths.Add(SnapToPixel(gt.AdvanceWidths[spaceGi] * _fontSize * TabSize));
                continue;
            }

            if (!charMap.TryGetValue(ch, out ushort gi))
            {
                // Try replacement-character glyph; fall back to glyph index 0.
                charMap.TryGetValue('\uFFFD', out gi);
            }

            glyphIndices.Add(gi);
            advanceWidths.Add(SnapToPixel(gt.AdvanceWidths[gi] * _fontSize));
        }

        var origin   = new Point(SnapToPixel(x), SnapToPixel(baselineY));
        var glyphRun = new GlyphRun(
            gt,
            bidiLevel:     0,
            isSideways:    false,
            renderingEmSize: _fontSize,
            pixelsPerDip:  (float)_pixelsPerDip,
            glyphIndices:  glyphIndices,
            baselineOrigin: origin,
            advanceWidths: advanceWidths,
            glyphOffsets:  null,
            characters:    null,
            deviceFontName: null,
            clusterMap:    null,
            caretStops:    null,
            language:      null);

        dc.DrawGlyphRun(brush, glyphRun);
    }

    private static double MeasureGlyphAdvanceWidth(GlyphTypeface gt, char ch, double fontSize)
    {
        gt.CharacterToGlyphMap.TryGetValue(ch, out ushort gi);
        return gt.AdvanceWidths[gi] * fontSize;
    }

    private static FormattedText MakeFormattedText(string text, Typeface typeface,
                                                    double fontSize, Brush brush,
                                                    double pixelsPerDip)
        => new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
               typeface, fontSize, brush, pixelsPerDip);

    #endregion

    #region TypefaceEqualityComparer — Dictionary key for Typeface (struct)

    private sealed class TypefaceEqualityComparer : IEqualityComparer<Typeface>
    {
        public static readonly TypefaceEqualityComparer Instance = new();

        private TypefaceEqualityComparer() { }

        public bool Equals(Typeface? x, Typeface? y)
        {
            if (x is null && y is null) return true;
            if (x is null || y is null) return false;

            return x.FontFamily.Source == y.FontFamily.Source
                && x.Style   == y.Style
                && x.Weight  == y.Weight
                && x.Stretch == y.Stretch;
        }

        public int GetHashCode(Typeface obj)
            => HashCode.Combine(obj.FontFamily.Source, obj.Style, obj.Weight, obj.Stretch);
    }

    #endregion

    #region P1-CE-05: GlyphRun segment cache builder

    // Thread-local scratch list: built once, re-used across calls on the same thread.
    // After building, contents are copied to a right-sized List<GlyphRunEntry> for storage,
    // avoiding the internal capacity-doubling of List<T> on every cache-miss line.
    [ThreadStatic] private static List<GlyphRunEntry>? _scratchEntries;

    /// <summary>
    /// Builds a list of <see cref="GlyphRunEntry"/> records from the given token sequence.
    /// Each entry's <c>GlyphRun.BaselineOrigin</c> is positioned at
    /// <c>(token.StartColumn * CharWidth, Baseline)</c> — caller must apply
    /// <c>dc.PushTransform(new TranslateTransform(textAreaX, lineTopY))</c> before drawing.
    /// Tokens backed by the fallback font path (null GlyphTypeface) are skipped; the
    /// caller must handle those via the normal <see cref="RenderToken"/> path.
    /// </summary>
    /// <param name="tokens">Token sequence for a single line.</param>
    /// <param name="urlBrush">
    /// The brush used to identify URL tokens; used to set <see cref="GlyphRunEntry.IsUrlToken"/>.
    /// </param>
    public List<GlyphRunEntry> BuildLineGlyphRuns(
        IEnumerable<SyntaxHighlightToken> tokens,
        Brush                             urlBrush,
        string                            lineText = "")
    {
        var scratch = _scratchEntries ??= new List<GlyphRunEntry>(16);
        scratch.Clear();

        foreach (var token in tokens)
        {
            if (string.IsNullOrEmpty(token.Text))
                continue;

            var gt = token.IsBold ? _boldGt : _regularGt;
            if (gt is null)
                continue; // fallback-font token — not cacheable as GlyphRun

            // Use tab-aware X so tokens on tab-indented lines land at the correct column.
            double tokenX = ComputeVisualX(lineText, token.StartColumn);

            var run = BuildGlyphRunAtOffset(
                token.Text,
                tokenX,
                Baseline,
                gt);

            scratch.Add(new GlyphRunEntry(
                run,
                token.Foreground,
                ReferenceEquals(token.Foreground, urlBrush),
                token.StartColumn,
                token.Length));
        }

        // Copy to a right-sized list for storage in CodeLine.GlyphRunCache.
        // This avoids retaining the oversized scratch capacity in the cache.
        return new List<GlyphRunEntry>(scratch);
    }

    /// <summary>
    /// Builds a <see cref="GlyphRun"/> for <paramref name="text"/> placed at the given
    /// <paramref name="x"/> / <paramref name="baselineY"/> coordinates.
    /// </summary>
    private GlyphRun BuildGlyphRunAtOffset(string text, double x, double baselineY, GlyphTypeface gt)
    {
        var idxList = new List<ushort>(text.Length);
        var advList = new List<double>(text.Length);
        var charMap = gt.CharacterToGlyphMap;

        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (ch == '\t')
            {
                // Tab has no glyph in most monospace fonts — use space glyph with TabSize advance.
                charMap.TryGetValue(' ', out ushort spaceGi);
                idxList.Add(spaceGi);
                advList.Add(gt.AdvanceWidths[spaceGi] * _fontSize * TabSize);
                continue;
            }

            if (!charMap.TryGetValue(ch, out ushort gi))
                charMap.TryGetValue('\uFFFD', out gi);

            idxList.Add(gi);
            advList.Add(gt.AdvanceWidths[gi] * _fontSize);
        }

        return new GlyphRun(
            gt,
            bidiLevel:        0,
            isSideways:       false,
            renderingEmSize:  _fontSize,
            pixelsPerDip:     (float)_pixelsPerDip,
            glyphIndices:     idxList,
            baselineOrigin:   new Point(x, baselineY),
            advanceWidths:    advList,
            glyphOffsets:     null,
            characters:       null,
            deviceFontName:   null,
            clusterMap:       null,
            caretStops:       null,
            language:         null);
    }

    #endregion
}

// ──────────────────────────────────────────────────────────────────────────────
// P1-CE-05: Per-line GlyphRun cache entry
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>
/// A pre-built GlyphRun entry stored in <see cref="Models.CodeLine.GlyphRunCache"/>.
/// The <see cref="Run"/>'s <c>BaselineOrigin</c> is relative to the text area origin:
/// <c>X = StartColumn * charWidth</c>, <c>Y = Baseline</c>.
/// At render time, one <c>DrawingContext.PushTransform(x, lineTopY)</c> translates
/// the entire cache to screen coordinates — zero per-token allocations.
/// </summary>
/// <param name="Run">Pre-built WPF GlyphRun — zero allocation to draw.</param>
/// <param name="Foreground">Brush for this token segment.</param>
/// <param name="IsUrlToken">True when this token should receive a hover underline.</param>
/// <param name="StartColumn">0-based column for underline X calculation.</param>
/// <param name="TokenLength">Character count for underline width calculation.</param>
public readonly record struct GlyphRunEntry(
    GlyphRun Run,
    Brush    Foreground,
    bool     IsUrlToken,
    int      StartColumn,
    int      TokenLength);
