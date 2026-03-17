// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: CodeScrollMarkerPanel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-17
// Description:
//     Lightweight overlay panel that renders colored tick marks on top of
//     the vertical scrollbar to indicate positions of interest (e.g. all
//     occurrences of the word under the caret).
//     Fully click-through (IsHitTestVisible = false, Background = null).
//
// Architecture Notes:
//     Pattern: Canvas-overlay / Observer
//     Positioned by the host CodeEditor to exactly cover _vScrollBar.
//     Proportional mapping: lineIndex / totalLines * drawableHeight.
//     Multiple marker types are supported via typed lists; rendered in
//     a fixed z-order (word highlights drawn last = topmost).
// ==========================================================

using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace WpfHexEditor.Editor.CodeEditor.Controls;

/// <summary>
/// Renders proportional tick marks over the vertical scrollbar to show the
/// positions of word-highlight occurrences (and future marker types).
/// Must be arranged by the host <see cref="CodeEditor"/> on top of the scrollbar.
/// </summary>
internal sealed class CodeScrollMarkerPanel : FrameworkElement
{
    #region Constants

    // Vertical margins matching the scrollbar arrow-button height (pixels).
    private const double TopMargin    = 17.0;
    private const double BottomMargin = 17.0;

    // Tick dimensions.
    private const double TickWidth  = 4.0;
    private const double TickHeight = 3.0;

    #endregion

    #region Static brushes

    private static readonly Brush s_wordHighlightTick = MakeFrozenBrush(Color.FromArgb(220, 86, 156, 214));

    #endregion

    #region Fields

    private IReadOnlyList<int> _wordHighlightLines = [];
    private int                _totalLines         = 1;

    #endregion

    #region Constructor

    public CodeScrollMarkerPanel()
    {
        IsHitTestVisible = false; // click-through to the scrollbar beneath
        // Background stays null — avoids capturing mouse events
    }

    #endregion

    #region Public API

    /// <summary>
    /// Updates the word-highlight tick marks.
    /// </summary>
    /// <param name="lines">Distinct line indices (0-based) that have an occurrence.</param>
    /// <param name="totalLines">Total line count of the document (used for proportional mapping).</param>
    public void UpdateWordMarkers(IReadOnlyList<int> lines, int totalLines)
    {
        _wordHighlightLines = lines;
        _totalLines         = Math.Max(1, totalLines);
        InvalidateVisual();
    }

    /// <summary>Removes all word-highlight tick marks.</summary>
    public void ClearWordMarkers()
    {
        _wordHighlightLines = [];
        InvalidateVisual();
    }

    #endregion

    #region Rendering

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        if (ActualHeight <= TopMargin + BottomMargin)
            return;

        double drawableH = ActualHeight - TopMargin - BottomMargin;
        double tickX     = (ActualWidth - TickWidth) / 2.0;

        foreach (int line in _wordHighlightLines)
        {
            double y = TopMargin + (line / (double)_totalLines) * drawableH;
            dc.DrawRectangle(s_wordHighlightTick, null,
                new Rect(tickX, y, TickWidth, TickHeight));
        }
    }

    #endregion

    #region Helpers

    private static Brush MakeFrozenBrush(Color color)
    {
        var b = new SolidColorBrush(color);
        b.Freeze();
        return b;
    }

    #endregion
}
