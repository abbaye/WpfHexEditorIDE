// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Services/ColorSwatchRenderer.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-30
// Description:
//     Renders 12×12 color swatches to the left of color literals on visible lines,
//     and tracks hit areas so CodeEditor can map a mouse click to a swatch.
//
//     Rendering is performed inside the H-translate DrawingContext scope (same as
//     text content), so swatches scroll horizontally with the document text.
//     Hit areas are stored in SCREEN coordinates (corrected for H-scroll) so that
//     OnMouseDown comparison with e.GetPosition(this) is always accurate.
//
// Architecture Notes:
//     Stateful — stores LastHitAreas per render frame; one instance per CodeEditor.
//     Call Render() once per OnRender pass (inside H-translate scope).
// ==========================================================

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using WpfHexEditor.Editor.CodeEditor.Models;

namespace WpfHexEditor.Editor.CodeEditor.Services;

/// <summary>A clickable swatch hit area in screen coordinates.</summary>
internal sealed record SwatchHitArea(
    /// <summary>Bounds in screen coordinates (relative to CodeEditor element).</summary>
    Rect Bounds,
    /// <summary>Colour represented by the swatch.</summary>
    Color Color,
    /// <summary>0-based document line index.</summary>
    int Line);

/// <summary>
/// Renders colour preview swatches alongside colour literals and
/// exposes the hit areas for click-to-picker interaction.
/// </summary>
internal sealed class ColorSwatchRenderer
{
    // Swatch dimensions
    private const double SwatchSize    = 12.0;
    private const double SwatchOffset  = 15.0; // pixels to the left of the literal start
    private const double BorderWidth   = 1.0;

    private static readonly Pen s_borderPen;

    static ColorSwatchRenderer()
    {
        s_borderPen = new Pen(Brushes.Black, BorderWidth);
        s_borderPen.Freeze();
    }

    private readonly List<SwatchHitArea> _hitAreas = new();

    /// <summary>
    /// Swatch hit areas computed during the most recent <see cref="Render"/> call,
    /// in screen coordinates relative to the CodeEditor element.
    /// </summary>
    public IReadOnlyList<SwatchHitArea> LastHitAreas => _hitAreas;

    /// <summary>
    /// Renders color swatches for all visible lines.
    /// Must be called inside the H-translate DrawingContext scope.
    /// </summary>
    /// <param name="dc">DrawingContext (inside H-translate transform).</param>
    /// <param name="lines">All document lines.</param>
    /// <param name="firstVisible">Index of first visible line (0-based).</param>
    /// <param name="lastVisible">Index of last visible line (0-based, inclusive).</param>
    /// <param name="charWidth">Width of a single character in the editor font.</param>
    /// <param name="lineHeight">Height of a single line.</param>
    /// <param name="textAreaLeft">Left edge of the text area (inside H-translate).</param>
    /// <param name="horizontalScroll">Current horizontal scroll offset (for screen-coord hit areas).</param>
    /// <param name="patterns">Pre-compiled regex patterns from LanguageDefinition.ColorLiteralPatterns.</param>
    public void Render(
        DrawingContext             dc,
        IReadOnlyList<CodeLine>    lines,
        int                        firstVisible,
        int                        lastVisible,
        double                     charWidth,
        double                     lineHeight,
        double                     textAreaLeft,
        double                     horizontalScroll,
        IReadOnlyList<Regex>?      patterns)
    {
        _hitAreas.Clear();

        if (patterns is null || patterns.Count == 0 || charWidth <= 0) return;

        double swatchCy = (lineHeight - SwatchSize) / 2.0; // vertical centre offset

        for (int li = firstVisible; li <= lastVisible && li < lines.Count; li++)
        {
            string text = lines[li].Text;
            if (string.IsNullOrEmpty(text)) continue;

            var matches = ColorLiteralDetector.Detect(text, patterns);
            if (matches.Count == 0) continue;

            // Vertical position inside H-translate (same space as text content).
            double lineY = (li - firstVisible) * lineHeight;

            foreach (var match in matches)
            {
                double drawX = textAreaLeft + match.StartColumn * charWidth - SwatchOffset;
                double drawY = lineY + swatchCy;
                var    rect  = new Rect(drawX, drawY, SwatchSize, SwatchSize);

                // Draw the swatch (filled colour + 1px black border).
                var brush = new SolidColorBrush(match.Color);
                brush.Freeze();
                dc.DrawRectangle(brush, s_borderPen, rect);

                // Record hit area in screen coords (subtract H-scroll so click test is accurate).
                var screenRect = new Rect(
                    drawX - horizontalScroll,
                    drawY,
                    SwatchSize,
                    SwatchSize);
                _hitAreas.Add(new SwatchHitArea(screenRect, match.Color, li));
            }
        }
    }
}
