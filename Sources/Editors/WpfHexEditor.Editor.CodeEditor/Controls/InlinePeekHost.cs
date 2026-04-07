// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Controls/InlinePeekHost.cs
// Description:
//     Inline peek definition panel embedded directly between editor lines
//     (VS2026 style). Derives from FrameworkElement; all content rendered
//     via DrawingContext on a DrawingVisual — zero WPF controls, zero per-
//     frame allocations.
//
//     Hosts:
//       - Title bar (22px): symbol + file:line, nav arrows, go-to-def, close
//       - Content area: line numbers + syntax-colored source, target highlighted
//       - Resize handle (4px strip at bottom)
//
// Architecture:
//     Added to CodeEditor._scrollBarChildren (VisualCollection).
//     CodeEditor.Rendering.cs shifts all Y positions below _peekHostLine by
//     PeekHeight so lines flow around the panel naturally.
// ==========================================================

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Editor.CodeEditor.Helpers;

namespace WpfHexEditor.Editor.CodeEditor.Controls;

/// <summary>
/// Inline peek definition panel inserted between editor lines.
/// </summary>
internal sealed class InlinePeekHost : FrameworkElement
{
    // ── Constants ─────────────────────────────────────────────────────────────

    private const double TitleBarHeight  = 22.0;
    private const double SeparatorHeight =  1.0;
    private const double ResizeHandleH   =  4.0;
    private const double LineNumWidth    = 44.0;
    private const double LineNumSepWidth =  1.0;
    private const double ContentPadX     =  8.0;

    // ── Visual tree ──────────────────────────────────────────────────────────

    private readonly DrawingVisual _visual = new();

    // ── Content state ────────────────────────────────────────────────────────

    private ISyntaxHighlighter? _highlighter;
    private string[]            _sourceLines  = Array.Empty<string>();
    private int                 _targetLine;     // 1-based
    private string              _label        = string.Empty;
    private int                 _scrollOffset;   // lines scrolled in content
    private int                 _resultIndex;
    private int                 _resultCount;

    // ── Layout metrics (set by CodeEditor) ───────────────────────────────────

    private double   _lineHeight  = 18.0;
    private double   _charWidth   = 7.0;
    private Typeface _typeface    = new(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    private double   _fontSize    = 12.0;

    // ── Hit zones (rebuilt on each Render call) ───────────────────────────────

    private Rect _navPrevRect  = Rect.Empty;
    private Rect _navNextRect  = Rect.Empty;
    private Rect _gotoDefRect  = Rect.Empty;
    private Rect _closeRect    = Rect.Empty;

    // ── Resize drag state ────────────────────────────────────────────────────

    private bool   _isDragging;
    private double _dragStartY;
    private double _dragStartHeight;

    // ── Last rendered width (needed to rerender on resize) ───────────────────

    private double _lastWidth;

    // ── Public state ─────────────────────────────────────────────────────────

    internal double PeekHeight { get; set; } = 240.0;

    // ── Events ───────────────────────────────────────────────────────────────

    internal event Action?        CloseRequested;
    internal event Action?        GoToDefinitionRequested;
    internal event Action<int>?   ResultIndexChanged;
    internal event Action<double>? HeightChanged;

    // ── Constructor ───────────────────────────────────────────────────────────

    internal InlinePeekHost()
    {
        AddVisualChild(_visual);
        AddLogicalChild(_visual);
        ClipToBounds = true;
    }

    // ── Visual tree overrides ────────────────────────────────────────────────

    protected override int    VisualChildrenCount      => 1;
    protected override Visual GetVisualChild(int index) => _visual;

    protected override Size ArrangeOverride(Size finalSize)
    {
        Render(finalSize.Width);
        return finalSize;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    internal void SetContent(
        string              sourceText,
        int                 targetLine1Based,
        string              label,
        ISyntaxHighlighter? highlighter,
        Typeface            typeface,
        double              fontSize,
        double              lineHeight,
        double              charWidth,
        int                 resultIndex,
        int                 resultCount)
    {
        _sourceLines  = sourceText.Split('\n');
        _targetLine   = targetLine1Based;
        _label        = label;
        _highlighter  = highlighter;
        _typeface     = typeface;
        _fontSize     = fontSize;
        _lineHeight   = lineHeight;
        _charWidth    = charWidth;
        _resultIndex  = resultIndex;
        _resultCount  = resultCount;
        _scrollOffset = 0;

        ScrollToTarget();
    }

    internal void ScrollUp(int lines = 3)
    {
        _scrollOffset = Math.Max(0, _scrollOffset - lines);
        Render(_lastWidth);
    }

    internal void ScrollDown(int lines = 3)
    {
        int maxScroll = Math.Max(0, _sourceLines.Length - VisibleContentLines);
        _scrollOffset = Math.Min(maxScroll, _scrollOffset + lines);
        Render(_lastWidth);
    }

    internal void ScrollToTarget()
    {
        if (_targetLine <= 0 || _sourceLines.Length == 0) return;
        int targetIdx = _targetLine - 1;
        int half      = VisibleContentLines / 2;
        _scrollOffset = Math.Max(0, Math.Min(targetIdx - half, _sourceLines.Length - VisibleContentLines));
        Render(_lastWidth);
    }

    internal void NavigatePrev()
    {
        if (_resultIndex > 0)
            ResultIndexChanged?.Invoke(_resultIndex - 1);
    }

    internal void NavigateNext()
    {
        if (_resultIndex < _resultCount - 1)
            ResultIndexChanged?.Invoke(_resultIndex + 1);
    }

    // ── Rendering ────────────────────────────────────────────────────────────

    internal void Render(double width)
    {
        if (width <= 0) return;
        _lastWidth = width;

        using var dc = _visual.RenderOpen();

        var bg      = TryFindResource("CE_QuickInfo_Background") as Brush ?? Brushes.Black;
        var titleBg = TryFindResource("GD_PeekTitleBar")         as Brush ?? new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x26));
        var border  = TryFindResource("GD_PeekBorder")           as Brush ?? new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44));
        var titleFg = TryFindResource("GD_PeekTitleForeground")  as Brush ?? Brushes.White;
        var navFg   = TryFindResource("GD_PeekNavLinkForeground") as Brush ?? new SolidColorBrush(Color.FromRgb(0x56, 0x9C, 0xD6));
        var lineNumFg = TryFindResource("CE_LineNumber")          as Brush ?? Brushes.Gray;
        var targetBg  = TryFindResource("GD_PeekTargetLine")     as Brush ?? new SolidColorBrush(Color.FromArgb(80, 0x1E, 0x3A, 0x5F));
        var hoverBg   = TryFindResource("GD_PeekHoverLine")      as Brush ?? new SolidColorBrush(Color.FromArgb(40, 0xFF, 0xFF, 0xFF));
        var editorFg  = TryFindResource("CE_Foreground")         as Brush ?? Brushes.LightGray;

        double h = PeekHeight;

        // ── Full background ──────────────────────────────────────────────────
        dc.DrawRectangle(bg, null, new Rect(0, 0, width, h));

        // ── Title bar ────────────────────────────────────────────────────────
        dc.DrawRectangle(titleBg, null, new Rect(0, 0, width, TitleBarHeight));

        double ty = (TitleBarHeight - _fontSize) / 2.0;

        // Nav arrows (only when multiple results)
        double xCursor = 8.0;
        if (_resultCount > 1)
        {
            var prevFt = MakeText("◄", _fontSize - 1, _resultIndex > 0 ? navFg : Brushes.Gray);
            _navPrevRect = new Rect(xCursor, 0, prevFt.Width + 8, TitleBarHeight);
            dc.DrawText(prevFt, new Point(xCursor + 4, ty));
            xCursor += prevFt.Width + 8;

            var countFt = MakeText($" {_resultIndex + 1}/{_resultCount} ", _fontSize - 1, titleFg);
            dc.DrawText(countFt, new Point(xCursor, ty));
            xCursor += countFt.Width;

            var nextFt = MakeText("►", _fontSize - 1, _resultIndex < _resultCount - 1 ? navFg : Brushes.Gray);
            _navNextRect = new Rect(xCursor, 0, nextFt.Width + 8, TitleBarHeight);
            dc.DrawText(nextFt, new Point(xCursor + 2, ty));
            xCursor += nextFt.Width + 12;
        }
        else
        {
            _navPrevRect = _navNextRect = Rect.Empty;
        }

        // Label (symbol — file:line)
        var labelFt = MakeText(_label, _fontSize - 1, titleFg);
        dc.DrawText(labelFt, new Point(xCursor, ty));

        // Right side: go-to-def ↗ and close ✕
        double rx = width - 8;
        var closeFt  = MakeText("✕", _fontSize - 1, titleFg);
        rx -= closeFt.Width + 4;
        _closeRect = new Rect(rx - 2, 0, closeFt.Width + 8, TitleBarHeight);
        dc.DrawText(closeFt, new Point(rx, ty));

        rx -= 4;
        var gotoFt   = MakeText("↗", _fontSize - 1, navFg);
        rx -= gotoFt.Width + 4;
        _gotoDefRect = new Rect(rx - 2, 0, gotoFt.Width + 8, TitleBarHeight);
        dc.DrawText(gotoFt, new Point(rx, ty));

        // Separator line below title bar
        dc.DrawRectangle(border, null, new Rect(0, TitleBarHeight, width, SeparatorHeight));

        // ── Content area ─────────────────────────────────────────────────────
        double contentTop = TitleBarHeight + SeparatorHeight;
        double contentH   = h - contentTop - ResizeHandleH;

        // Line number column separator
        dc.DrawRectangle(border, null, new Rect(LineNumWidth, contentTop, LineNumSepWidth, contentH));

        double textX = LineNumWidth + LineNumSepWidth + ContentPadX;
        double textW = width - textX - ContentPadX;

        _highlighter?.Reset();

        int visLines = (int)(contentH / _lineHeight);
        int end      = Math.Min(_scrollOffset + visLines, _sourceLines.Length);

        for (int si = _scrollOffset; si < end; si++)
        {
            double rowY  = contentTop + (si - _scrollOffset) * _lineHeight;
            int    lineN = si + 1; // 1-based
            bool   isTarget = lineN == _targetLine;

            // Target / hover highlight
            if (isTarget)
                dc.DrawRectangle(targetBg, null, new Rect(0, rowY, width, _lineHeight));

            // Line number
            var numFt = MakeText(lineN.ToString(), _fontSize - 1, lineNumFg);
            double numX = LineNumWidth - numFt.Width - 4;
            dc.DrawText(numFt, new Point(Math.Max(2, numX), rowY + (_lineHeight - numFt.Height) / 2));

            // Source line text (syntax highlighted)
            string rawLine = _sourceLines[si].TrimEnd('\r');
            RenderSourceLine(dc, rawLine, si, textX, rowY, textW, editorFg);
        }

        // ── Resize handle ────────────────────────────────────────────────────
        dc.DrawRectangle(border, null, new Rect(0, h - ResizeHandleH, width, ResizeHandleH));

        // Outer border
        dc.DrawRectangle(null, new Pen(border, 1), new Rect(0.5, 0.5, width - 1, h - 1));
    }

    private void RenderSourceLine(
        DrawingContext dc,
        string         rawLine,
        int            lineIndex,
        double         x,
        double         rowY,
        double         maxW,
        Brush          defaultFg)
    {
        double midY = rowY + (_lineHeight - _fontSize * 1.2) / 2.0;

        if (_highlighter == null || rawLine.Length == 0)
        {
            var ft = MakeText(rawLine, _fontSize, defaultFg);
            dc.DrawText(ft, new Point(x, midY));
            return;
        }

        IReadOnlyList<SyntaxHighlightToken> tokens;
        try { tokens = _highlighter.Highlight(rawLine, lineIndex); }
        catch { tokens = Array.Empty<SyntaxHighlightToken>(); }

        if (tokens.Count == 0)
        {
            dc.DrawText(MakeText(rawLine, _fontSize, defaultFg), new Point(x, midY));
            return;
        }

        // Gap-fill rendering (same algorithm as FoldPeekPopup.Show)
        double cx = x;
        int    pos = 0;
        foreach (var token in tokens)
        {
            // Gap before token
            if (token.StartColumn > pos)
            {
                int gEnd = Math.Min(token.StartColumn, rawLine.Length);
                if (gEnd > pos)
                {
                    var gft = MakeText(rawLine[pos..gEnd], _fontSize, defaultFg);
                    dc.DrawText(gft, new Point(cx, midY));
                    cx += gft.WidthIncludingTrailingWhitespace;
                }
            }
            // Token
            if (token.StartColumn < rawLine.Length)
            {
                int    tEnd    = Math.Min(token.StartColumn + token.Length, rawLine.Length);
                string tText   = rawLine[token.StartColumn..tEnd];
                var    weight  = token.IsBold   ? FontWeights.Bold   : FontWeights.Normal;
                var    style   = token.IsItalic ? FontStyles.Italic  : FontStyles.Normal;
                var    tft     = MakeText(tText, _fontSize, token.Foreground, weight, style);
                dc.DrawText(tft, new Point(cx, midY));
                cx += tft.WidthIncludingTrailingWhitespace;
            }
            pos = token.StartColumn + token.Length;
        }
        // Tail
        if (pos < rawLine.Length)
        {
            dc.DrawText(MakeText(rawLine[pos..], _fontSize, defaultFg), new Point(cx, midY));
        }
    }

    // ── Mouse handling ────────────────────────────────────────────────────────

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        var pt = e.GetPosition(this);

        if (_closeRect.Contains(pt))   { CloseRequested?.Invoke();        e.Handled = true; return; }
        if (_gotoDefRect.Contains(pt)) { GoToDefinitionRequested?.Invoke(); e.Handled = true; return; }
        if (_navPrevRect.Contains(pt)) { NavigatePrev(); e.Handled = true; return; }
        if (_navNextRect.Contains(pt)) { NavigateNext(); e.Handled = true; return; }

        // Resize handle drag
        double resizeTop = PeekHeight - ResizeHandleH;
        if (pt.Y >= resizeTop)
        {
            _isDragging       = true;
            _dragStartY       = e.GetPosition(Parent as IInputElement).Y;
            _dragStartHeight  = PeekHeight;
            CaptureMouse();
            e.Handled = true;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var pt = e.GetPosition(this);

        if (_isDragging)
        {
            double parentY = e.GetPosition(Parent as IInputElement).Y;
            double newH    = Math.Max(80, _dragStartHeight + (parentY - _dragStartY));
            if (Math.Abs(newH - PeekHeight) > 0.5)
            {
                PeekHeight = newH;
                HeightChanged?.Invoke(newH);
            }
            e.Handled = true;
            return;
        }

        // Cursor change for resize zone
        double resizeTop = PeekHeight - ResizeHandleH;
        Cursor = pt.Y >= resizeTop ? Cursors.SizeNS : Cursors.Arrow;
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        if (e.Delta > 0) ScrollUp(3);
        else             ScrollDown(3);
        e.Handled = true;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private int VisibleContentLines
    {
        get
        {
            double contentH = PeekHeight - TitleBarHeight - SeparatorHeight - ResizeHandleH;
            return Math.Max(1, (int)(contentH / _lineHeight));
        }
    }

    private FormattedText MakeText(
        string text, double size, Brush fg,
        FontWeight? weight = null, FontStyle? style = null)
    {
        return new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            _typeface,
            size,
            fg,
            VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            MaxTextWidth = 4000,
        };
    }
}
