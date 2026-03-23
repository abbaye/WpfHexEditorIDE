// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: CodeScrollMarkerPanel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-17
// Description:
//     Lightweight overlay panel that renders colored tick marks on top of
//     the vertical scrollbar to indicate positions of interest (e.g. all
//     occurrences of the word under the caret, the current caret position,
//     and the active selection range).
//     Fully click-through (IsHitTestVisible = false, Background = null).
//
// Architecture Notes:
//     Pattern: Canvas-overlay / Observer
//     Positioned by the host CodeEditor to exactly cover _vScrollBar.
//     Proportional mapping: lineIndex / totalLines * drawableHeight.
//     Multiple marker types are supported via typed lists; rendered in
//     a fixed z-order (selection block → word highlights → caret tick).
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
    private static readonly Brush s_caretTick         = MakeFrozenBrush(Color.FromArgb(220, 255, 255, 100)); // bright yellow
    private static readonly Brush s_selectionBlock    = MakeFrozenBrush(Color.FromArgb(70,   86, 156, 214)); // semi-transparent blue
    private static readonly Brush s_errorTick         = MakeFrozenBrush(Color.FromArgb(200, 244, 71,  71));  // red — diagnostic errors
    private static readonly Brush s_warningTick       = MakeFrozenBrush(Color.FromArgb(200, 255, 168,  0));  // amber — diagnostic warnings

    #endregion

    #region Fields

    private IReadOnlyList<int> _wordHighlightLines = [];
    private IReadOnlyList<int> _errorLines         = [];
    private IReadOnlyList<int> _warningLines       = [];
    private int                _totalLines         = 1;
    private int                _caretLine          = -1;
    private int                _selectionStart     = -1;
    private int                _selectionEnd       = -1;

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

    /// <summary>
    /// Updates the diagnostic error and warning tick marks.
    /// </summary>
    /// <param name="errorLines">0-based line indices that have at least one error.</param>
    /// <param name="warningLines">0-based line indices that have at least one warning (and no error).</param>
    /// <param name="totalLines">Total line count of the document.</param>
    public void UpdateDiagnosticMarkers(IReadOnlyList<int> errorLines, IReadOnlyList<int> warningLines, int totalLines)
    {
        _errorLines   = errorLines;
        _warningLines = warningLines;
        _totalLines   = Math.Max(1, totalLines);
        InvalidateVisual();
    }

    /// <summary>Removes all diagnostic tick marks.</summary>
    public void ClearDiagnosticMarkers()
    {
        _errorLines   = [];
        _warningLines = [];
        InvalidateVisual();
    }

    /// <summary>Removes all word-highlight tick marks.</summary>
    public void ClearWordMarkers()
    {
        _wordHighlightLines = [];
        InvalidateVisual();
    }

    /// <summary>
    /// Updates the caret position and optional selection range markers.
    /// Pass <paramref name="selStart"/> == -1 (or selStart >= selEnd) for no selection block.
    /// </summary>
    /// <param name="caretLine">0-based line of the caret (-1 to hide).</param>
    /// <param name="selStart">0-based first selected line (-1 for no selection).</param>
    /// <param name="selEnd">0-based last selected line (exclusive upper bound).</param>
    /// <param name="totalLines">Total visible line count (denominator for proportional mapping).</param>
    public void UpdateCaretAndSelection(int caretLine, int selStart, int selEnd, int totalLines)
    {
        _caretLine      = caretLine;
        _selectionStart = selStart;
        _selectionEnd   = selEnd;
        _totalLines     = Math.Max(1, totalLines);
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

        // Selection block — rendered first so word-highlight ticks and caret sit on top.
        if (_selectionStart >= 0 && _selectionEnd > _selectionStart)
        {
            double yTop = TopMargin + (_selectionStart / (double)_totalLines) * drawableH;
            double yBot = TopMargin + (_selectionEnd   / (double)_totalLines) * drawableH;
            double h    = Math.Max(2, yBot - yTop);
            dc.DrawRectangle(s_selectionBlock, null, new Rect(tickX, yTop, TickWidth, h));
        }

        // Warning ticks — rendered before error ticks so errors always sit on top.
        foreach (int line in _warningLines)
        {
            double y = TopMargin + (line / (double)_totalLines) * drawableH;
            dc.DrawRectangle(s_warningTick, null,
                new Rect(tickX, y, TickWidth, TickHeight));
        }

        // Error ticks.
        foreach (int line in _errorLines)
        {
            double y = TopMargin + (line / (double)_totalLines) * drawableH;
            dc.DrawRectangle(s_errorTick, null,
                new Rect(tickX, y, TickWidth, TickHeight));
        }

        // Word-highlight ticks.
        foreach (int line in _wordHighlightLines)
        {
            double y = TopMargin + (line / (double)_totalLines) * drawableH;
            dc.DrawRectangle(s_wordHighlightTick, null,
                new Rect(tickX, y, TickWidth, TickHeight));
        }

        // Caret tick — rendered last so it is always visible on top.
        if (_caretLine >= 0)
        {
            double y = TopMargin + (_caretLine / (double)_totalLines) * drawableH;
            dc.DrawRectangle(s_caretTick, null, new Rect(tickX - 1, y, TickWidth + 2, 2));
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
