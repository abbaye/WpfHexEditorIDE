// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Rendering/WhitespaceRenderer.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Draws visible whitespace markers over a rendered line when
//     ShowWhitespace is enabled in CodeEditorOptions.
//     Spaces → middle dot (·, U+00B7).
//     Tabs    → right arrow (→, U+2192) followed by a horizontal rule.
//     Line endings are drawn by the caller (outside scope here).
//
// Architecture Notes:
//     Pattern: Decorator / Post-render pass
//     - Stateless; called once per visible line after the main GlyphRunRenderer pass.
//     - Uses a pre-built Brush and Typeface passed at construction for performance.
//     - CharWidth / CharHeight are set from the editor's current typeface metrics
//       so the markers align precisely with regular glyphs.
//     - Only active when ShowWhitespace == true; CodeEditor gates the call.
// ==========================================================

using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace WpfHexEditor.Editor.CodeEditor.Rendering;

/// <summary>
/// Renders visible representations of space and tab characters over a line.
/// Call <see cref="RenderLine"/> in your <c>OnRender</c> override after the
/// main text pass to overlay whitespace glyphs.
/// </summary>
public sealed class WhitespaceRenderer
{
    private static readonly char SpaceMarker = '\u00B7'; // middle dot ·
    private static readonly char TabMarker   = '\u2192'; // right arrow →

    private readonly Brush    _markerBrush;
    private readonly Typeface _typeface;
    private readonly double   _emSize;

    /// <summary>Horizontal advance (pixels) of one character in the editor font.</summary>
    public double CharWidth  { get; set; } = 8.0;

    /// <summary>Line height (pixels) of the editor font.</summary>
    public double CharHeight { get; set; } = 16.0;

    /// <param name="markerBrush">Brush used to draw whitespace glyphs (typically semi-transparent).</param>
    /// <param name="typeface">Typeface matching the editor font.</param>
    /// <param name="emSize">Font em size (pixels) of the editor font.</param>
    public WhitespaceRenderer(Brush markerBrush, Typeface typeface, double emSize)
    {
        _markerBrush = markerBrush ?? throw new ArgumentNullException(nameof(markerBrush));
        _typeface    = typeface    ?? throw new ArgumentNullException(nameof(typeface));
        _emSize      = emSize;
    }

    /// <summary>
    /// Draws whitespace markers for a single line of <paramref name="lineText"/>.
    /// </summary>
    /// <param name="dc">WPF drawing context (already in the correct transform for this line).</param>
    /// <param name="lineText">Raw text of the line (without trailing newline).</param>
    /// <param name="originX">X-origin of the first character (pixels).</param>
    /// <param name="originY">Y-origin (baseline) of the line (pixels).</param>
    /// <param name="tabSize">Number of spaces a tab represents (used for the horizontal rule length).</param>
    public void RenderLine(
        DrawingContext dc,
        string         lineText,
        double         originX,
        double         originY,
        int            tabSize = 4)
    {
        if (string.IsNullOrEmpty(lineText)) return;

        double x = originX;

        for (int i = 0; i < lineText.Length; i++)
        {
            char c = lineText[i];

            if (c == ' ')
            {
                DrawGlyph(dc, SpaceMarker, x, originY);
                x += CharWidth;
            }
            else if (c == '\t')
            {
                DrawGlyph(dc, TabMarker, x, originY);
                // Draw the horizontal rule up to the next tab stop.
                double ruleEnd = x + CharWidth * tabSize;
                double ruleY   = originY - CharHeight * 0.35; // mid-line
                dc.DrawLine(new Pen(_markerBrush, 0.5), new Point(x + CharWidth, ruleY), new Point(ruleEnd, ruleY));
                x = ruleEnd;
            }
            else
            {
                x += CharWidth;
            }
        }
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private void DrawGlyph(DrawingContext dc, char glyph, double x, double y)
    {
        var text = new FormattedText(
            glyph.ToString(),
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            _typeface,
            _emSize,
            _markerBrush,
            pixelsPerDip: 1.0);

        dc.DrawText(text, new Point(x, y - _emSize));
    }
}
