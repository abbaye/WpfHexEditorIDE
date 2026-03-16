//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WpfHexEditor.Editor.TextEditor.Highlighting;
using WpfHexEditor.Editor.TextEditor.ViewModels;

namespace WpfHexEditor.Editor.TextEditor.Controls;

/// <summary>
/// Custom WPF element that renders lines of text with syntax highlighting via
/// three <see cref="DrawingVisual"/> layers:
/// <list type="number">
///   <item>Layer 0 (<c>_backgroundVisual</c>) — background, line-number gutter,
///         current-line highlight and selection rectangles.</item>
///   <item>Layer 1 (<c>_textContentVisual</c>) — line-number labels and
///         syntax-highlighted code text.</item>
///   <item>Layer 2 (<c>_cursorOverlay</c>) — blinking caret.</item>
/// </list>
/// During mouse-drag selection only layer 0 is redrawn (~10 µs).
/// Layer 1 is only redrawn when text content changes.
/// A per-line <see cref="FormattedText"/> segment cache prevents heap allocations
/// on every render frame when text is unchanged.
/// </summary>
internal sealed class TextViewport : FrameworkElement
{
    // -----------------------------------------------------------------------
    // Constants
    // -----------------------------------------------------------------------

    private const double DefaultFontSize = 13.0;
    private const double LineNumberPadding = 4.0;
    private const double LeftMargin = 6.0;
    private const double SelectionCornerRadius = 3.0;

    // -----------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------

    private TextEditorViewModel? _vm;

    // Font / layout metrics
    private double _lineHeight;
    private double _charWidth;
    private double _lineNumberWidth;
    private int _firstVisibleLine;
    private int _visibleLineCount;
    private double _horizontalOffset;
    private Typeface? _typeface;
    private double _emSize;
    private double _cachedFontSize = -1;
    private Typeface? _cachedTypeface;
    private DpiScale _dpi;

    // Mouse drag selection
    private bool _isDragging;

    // Set to true while OnMouseMove updates the caret directly so that
    // OnVmPropertyChanged does not queue a redundant background render.
    private bool _suppressVmNotify;

    // Cursor blink
    private readonly DispatcherTimer _cursorBlinkTimer;
    private bool _cursorVisible = true;

    // Brush cache — keyed by theme resource key
    private readonly Dictionary<string, Brush> _brushCache = new();

    // Per-line FormattedText segment cache.
    // Key = line index; valid when LineRenderCache.Text equals the current line string.
    // Cleared on content change, theme change, or font change.
    private readonly Dictionary<int, LineRenderCache> _lineRenderCache = new();

    // Render coalescing — prevent dispatcher queue flooding
    private bool _fullRenderPending;
    private bool _backgroundRenderPending;

    // -----------------------------------------------------------------------
    // DrawingVisual layers
    // -----------------------------------------------------------------------

    private readonly DrawingVisual _backgroundVisual  = new(); // layer 0
    private readonly DrawingVisual _textContentVisual = new(); // layer 1
    private readonly DrawingVisual _cursorOverlay     = new(); // layer 2

    private readonly VisualCollection _visuals;

    protected override int VisualChildrenCount => _visuals.Count;
    protected override Visual GetVisualChild(int index) => _visuals[index];

    // -----------------------------------------------------------------------
    // Per-line render cache entry
    // -----------------------------------------------------------------------

    /// <summary>
    /// Cached segments for one rendered line.
    /// <see cref="XPositions"/> are relative to the code-area origin (before adding <c>codeX</c>).
    /// </summary>
    private readonly record struct LineRenderCache(
        string Text,
        FormattedText[] Segments,
        double[] XPositions);

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------

    public TextViewport()
    {
        _visuals = new VisualCollection(this);
        _visuals.Add(_backgroundVisual);   // z = 0 (bottom — below text)
        _visuals.Add(_textContentVisual);  // z = 1
        _visuals.Add(_cursorOverlay);      // z = 2 (top)

        _cursorBlinkTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(530)
        };
        _cursorBlinkTimer.Tick += OnCursorBlink;

        Focusable = true;
        ClipToBounds = true;
        SnapsToDevicePixels = true;

        // Watch TE_Background via DynamicResource so that any theme swap
        // (Application.Resources.MergedDictionaries replacement) triggers
        // OnThemeWatcherChanged → brush/text cache flush + re-render.
        SetResourceReference(ThemeWatcherProperty, "TE_Background");
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    public void Attach(TextEditorViewModel vm)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnVmPropertyChanged;

        _vm = vm;
        vm.PropertyChanged += OnVmPropertyChanged;
        _lineRenderCache.Clear();
        InvalidateMeasure();
        InvalidateVisual(); // triggers OnRender → full render
    }

    public void Detach()
    {
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm = null;
        }
        _lineRenderCache.Clear();
        InvalidateVisual();
    }

    public int FirstVisibleLine
    {
        get => _firstVisibleLine;
        set
        {
            if (_firstVisibleLine != value)
            {
                _firstVisibleLine = Math.Max(0, value);
                QueueFullRender();
            }
        }
    }

    public double HorizontalOffset
    {
        get => _horizontalOffset;
        set
        {
            if (Math.Abs(_horizontalOffset - value) > 0.01)
            {
                _horizontalOffset = Math.Max(0, value);
                QueueFullRender();
            }
        }
    }

    /// <summary>Total document height in device-independent units.</summary>
    public double TotalHeight => (_vm?.LineCount ?? 0) * LineHeight;

    /// <summary>Estimated max line width (for horizontal scrollbar).</summary>
    public double EstimatedMaxWidth => (_vm?.Lines.Max(l => l.Length) ?? 0) * CharWidth + LineNumberColumnWidth + LeftMargin + 20;

    public double LineHeight            => _lineHeight > 0 ? _lineHeight : 18;
    public double CharWidth             => _charWidth  > 0 ? _charWidth  : 7.2;
    public double LineNumberColumnWidth => _lineNumberWidth + LineNumberPadding * 2;

    public void ScrollIntoView(int lineIndex)
    {
        if (_visibleLineCount == 0) return;
        if (lineIndex < _firstVisibleLine)
            FirstVisibleLine = lineIndex;
        else if (lineIndex >= _firstVisibleLine + _visibleLineCount)
            FirstVisibleLine = lineIndex - _visibleLineCount + 1;
    }

    public void StartCursorBlink()
    {
        _cursorVisible = true;
        _cursorBlinkTimer.Start();
        DrawCursor();
    }

    public void StopCursorBlink()
    {
        _cursorBlinkTimer.Stop();
        _cursorVisible = false;
        DrawCursor();
    }

    // -----------------------------------------------------------------------
    // Layout
    // -----------------------------------------------------------------------

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureFontMetrics();

        double desiredWidth = double.IsInfinity(availableSize.Width)
            ? EstimatedMaxWidth
            : availableSize.Width;

        double desiredHeight = double.IsInfinity(availableSize.Height)
            ? TotalHeight
            : availableSize.Height;

        return new Size(Math.Max(0, desiredWidth), Math.Max(0, desiredHeight));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _visibleLineCount = _lineHeight > 0 ? (int)Math.Ceiling(finalSize.Height / _lineHeight) + 1 : 0;
        return finalSize;
    }

    // -----------------------------------------------------------------------
    // OnRender — triggered by WPF for layout/size/theme/InvalidateVisual changes.
    // Performs a full synchronous render and cancels any pending async renders.
    // -----------------------------------------------------------------------

    protected override void OnRender(DrawingContext dc)
    {
        // Reserve hit-test area. Actual rendering is in DrawingVisual children.
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));

        // Cancel any queued async renders — we're doing a full render now.
        _fullRenderPending       = false;
        _backgroundRenderPending = false;

        EnsureFontMetrics();
        EnsureDpi();

        UpdateBackground();
        UpdateTextContent();
        DrawCursor();
    }

    // -----------------------------------------------------------------------
    // Layer 0 — background, gutter, current-line highlight, selection
    // -----------------------------------------------------------------------

    /// <summary>
    /// Redraws layer 0: background, line-number gutter, current-line highlight,
    /// and selection rectangles.  No <see cref="FormattedText"/> is created here —
    /// this is the fast path called on every mouse move during drag (~10–50 µs).
    /// </summary>
    private void UpdateBackground()
    {
        using var dc = _backgroundVisual.RenderOpen();

        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        dc.DrawRectangle(GetBrush("TE_Background"), null, bounds);

        if (_vm is null || _lineHeight <= 0) return;

        // Line number column background + separator
        dc.DrawRectangle(GetBrush("TE_LineNumberBackground"), null,
            new Rect(0, 0, LineNumberColumnWidth, ActualHeight));
        dc.DrawRectangle(GetBrush("TE_LineNumberForeground"), null,
            new Rect(LineNumberColumnWidth - 1, 0, 1, ActualHeight));

        int firstLine = Math.Max(0, _firstVisibleLine);
        int lastLine  = Math.Min(_vm.Lines.Count - 1, firstLine + _visibleLineCount);
        double codeX  = LineNumberColumnWidth + LeftMargin - _horizontalOffset;

        // Current-line highlight (drawn before text so text renders on top)
        if (_vm.CaretLine >= firstLine && _vm.CaretLine <= lastLine)
        {
            double y = (_vm.CaretLine - firstLine) * _lineHeight;
            dc.DrawRectangle(GetBrush("TE_CurrentLineBrush"), null,
                new Rect(0, y, ActualWidth, _lineHeight));
        }

        // Selection rectangles
        if (_vm.HasSelection)
            DrawSelectionRects(dc, firstLine, lastLine, codeX);
    }

    private void DrawSelectionRects(DrawingContext dc, int firstVisLine, int lastVisLine, double codeX)
    {
        if (_vm is null || !_vm.HasSelection) return;

        int ancLine = _vm.SelectionAnchorLine;
        int ancCol  = _vm.SelectionAnchorColumn;
        int carLine = _vm.CaretLine;
        int carCol  = _vm.CaretColumn;

        bool anchorFirst = ancLine < carLine || (ancLine == carLine && ancCol <= carCol);
        int startLine = anchorFirst ? ancLine : carLine;
        int startCol  = anchorFirst ? ancCol  : carCol;
        int endLine   = anchorFirst ? carLine : ancLine;
        int endCol    = anchorFirst ? carCol  : ancCol;

        var selBrush = GetBrush("TE_SelectionBackground");

        if (startLine == endLine)
        {
            if (startLine < firstVisLine || startLine > lastVisLine) return;
            double y  = (startLine - firstVisLine) * _lineHeight;
            double x1 = codeX + startCol * _charWidth;
            double x2 = codeX + endCol   * _charWidth;
            if (x2 > x1) dc.DrawRoundedRectangle(selBrush, null, new Rect(x1, y, x2 - x1, _lineHeight), SelectionCornerRadius, SelectionCornerRadius);
            return;
        }

        // Multi-line: collect overlapping segments then union them so the brush is applied once,
        // preventing double-alpha darkening at junctions with semi-transparent selection brushes.
        var segments = new List<Geometry>();

        // First (partial) line — extend bottom by CornerRadius so the rounded tail merges with next segment
        if (startLine >= firstVisLine && startLine <= lastVisLine)
        {
            double y     = (startLine - firstVisLine) * _lineHeight;
            double x1    = codeX + startCol * _charWidth;
            double lineW = (_vm.GetLine(startLine).Length - startCol) * _charWidth;
            double width = Math.Max(lineW, _charWidth);
            segments.Add(new RectangleGeometry(new Rect(x1, y, width, _lineHeight + SelectionCornerRadius), SelectionCornerRadius, SelectionCornerRadius));
        }

        // Middle (full) lines — extend top and bottom by CornerRadius to merge with neighbours
        for (int li = startLine + 1; li < endLine; li++)
        {
            if (li < firstVisLine || li > lastVisLine) continue;
            double y     = (li - firstVisLine) * _lineHeight - SelectionCornerRadius;
            double width = Math.Max(_vm.GetLine(li).Length * _charWidth, _charWidth);
            segments.Add(new RectangleGeometry(new Rect(codeX, y, width, _lineHeight + SelectionCornerRadius * 2), SelectionCornerRadius, SelectionCornerRadius));
        }

        // Last (partial) line — extend top by CornerRadius so the rounded head merges with previous segment
        if (endLine >= firstVisLine && endLine <= lastVisLine)
        {
            double y     = (endLine - firstVisLine) * _lineHeight - SelectionCornerRadius;
            double width = Math.Max(endCol * _charWidth, _charWidth);
            segments.Add(new RectangleGeometry(new Rect(codeX, y, width, _lineHeight + SelectionCornerRadius), SelectionCornerRadius, SelectionCornerRadius));
        }

        if (segments.Count == 0) return;
        Geometry combined = segments[0];
        for (int i = 1; i < segments.Count; i++)
            combined = Geometry.Combine(combined, segments[i], GeometryCombineMode.Union, null);
        combined.Freeze();
        dc.DrawGeometry(selBrush, null, combined);
    }

    // -----------------------------------------------------------------------
    // Layer 1 — line-number labels + syntax-highlighted code text
    // -----------------------------------------------------------------------

    /// <summary>
    /// Redraws layer 1: line-number text labels and syntax-highlighted code.
    /// Uses a per-line <see cref="FormattedText"/> segment cache — no allocations
    /// for lines whose text has not changed since the last render.
    /// </summary>
    private void UpdateTextContent()
    {
        using var dc = _textContentVisual.RenderOpen();

        if (_vm is null || _lineHeight <= 0) return;

        var lines     = _vm.Lines;
        int firstLine = Math.Max(0, _firstVisibleLine);
        int lastLine  = Math.Min(lines.Count - 1, firstLine + _visibleLineCount);
        double codeX  = LineNumberColumnWidth + LeftMargin - _horizontalOffset;

        for (int li = firstLine; li <= lastLine; li++)
        {
            double y    = (li - firstLine) * _lineHeight;
            var    line = lines[li];

            // Line number label
            var lnText = BuildFormattedText((li + 1).ToString(), GetBrush("TE_LineNumberForeground"));
            double lnX = LineNumberColumnWidth - lnText.Width - LineNumberPadding;
            dc.DrawText(lnText, new Point(lnX, y + (_lineHeight - lnText.Height) / 2));

            if (string.IsNullOrEmpty(line)) continue;

            var spans = _vm.GetHighlightedSpans(li);
            if (spans.Count > 0)
                RenderHighlightedLineCached(dc, li, line, spans, codeX, y);
            else
                RenderPlainLineCached(dc, li, line, codeX, y);
        }
    }

    /// <summary>
    /// Renders a plain (unstyled) line, reusing the cached <see cref="FormattedText"/>
    /// when the line text is unchanged.
    /// </summary>
    private void RenderPlainLineCached(DrawingContext dc, int lineIndex, string line, double codeX, double y)
    {
        if (_lineRenderCache.TryGetValue(lineIndex, out var cache) && cache.Text == line)
        {
            // Cache hit — draw segments at the new Y position; codeX may have changed (h-scroll).
            for (int i = 0; i < cache.Segments.Length; i++)
                dc.DrawText(cache.Segments[i], new Point(codeX + cache.XPositions[i], y));
            return;
        }

        // Cache miss — build FormattedText and populate cache.
        var ft = BuildFormattedText(line, GetBrush("TE_Foreground"));
        dc.DrawText(ft, new Point(codeX, y));
        _lineRenderCache[lineIndex] = new LineRenderCache(line, [ft], [0.0]);
    }

    /// <summary>
    /// Renders a syntax-highlighted line, reusing cached <see cref="FormattedText"/>
    /// segments when the line text is unchanged.
    /// </summary>
    private void RenderHighlightedLineCached(DrawingContext dc, int lineIndex, string line,
        IReadOnlyList<ColoredSpan> spans, double codeX, double y)
    {
        if (_lineRenderCache.TryGetValue(lineIndex, out var cache) && cache.Text == line)
        {
            for (int i = 0; i < cache.Segments.Length; i++)
                dc.DrawText(cache.Segments[i], new Point(codeX + cache.XPositions[i], y));
            return;
        }

        // Cache miss — rebuild all FormattedText segments for this line.
        var segments   = new List<FormattedText>(spans.Count * 2 + 1);
        var xPositions = new List<double>(spans.Count * 2 + 1);
        var defaultBrush = GetBrush("TE_Foreground");
        int pos = 0;

        foreach (var span in spans)
        {
            // Guard: stale span positions must not exceed current line length.
            if (span.Start >= line.Length) break;

            // Unstyled text before this span
            if (span.Start > pos)
            {
                int safeEnd = Math.Min(span.Start, line.Length);
                int safePos = Math.Min(pos, safeEnd);
                var raw = line[safePos..safeEnd];
                if (!string.IsNullOrEmpty(raw))
                {
                    segments.Add(BuildFormattedText(raw, defaultBrush));
                    xPositions.Add(safePos * _charWidth);
                }
            }

            // Styled span
            var spanText = span.Start < line.Length
                ? line.Substring(span.Start, Math.Min(span.Length, line.Length - span.Start))
                : string.Empty;

            if (!string.IsNullOrEmpty(spanText))
            {
                segments.Add(BuildFormattedText(spanText, GetBrush(span.ColorKey, defaultBrush)));
                xPositions.Add(span.Start * _charWidth);
            }

            pos = span.Start + span.Length;
        }

        // Remaining unstyled text after last span
        if (pos < line.Length)
        {
            var tail = line[pos..];
            if (!string.IsNullOrEmpty(tail))
            {
                segments.Add(BuildFormattedText(tail, defaultBrush));
                xPositions.Add(pos * _charWidth);
            }
        }

        var segsArr = segments.ToArray();
        var xArr    = xPositions.ToArray();
        _lineRenderCache[lineIndex] = new LineRenderCache(line, segsArr, xArr);

        for (int i = 0; i < segsArr.Length; i++)
            dc.DrawText(segsArr[i], new Point(codeX + xArr[i], y));
    }

    // -----------------------------------------------------------------------
    // Layer 2 — cursor overlay
    // -----------------------------------------------------------------------

    private void DrawCursor()
    {
        using var dc = _cursorOverlay.RenderOpen();

        if (_vm is null || !_cursorVisible || !IsKeyboardFocusWithin) return;

        int caretLine = _vm.CaretLine;
        int caretCol  = _vm.CaretColumn;

        if (caretLine < _firstVisibleLine || caretLine > _firstVisibleLine + _visibleLineCount)
            return;

        double y   = (caretLine - _firstVisibleLine) * _lineHeight;
        double x   = LineNumberColumnWidth + LeftMargin + caretCol * _charWidth - _horizontalOffset;
        var    pen = new Pen(GetBrush("TE_Foreground"), 1.5);
        dc.DrawLine(pen, new Point(x, y + 1), new Point(x, y + _lineHeight - 1));
    }

    private void OnCursorBlink(object? sender, EventArgs e)
    {
        _cursorVisible = !_cursorVisible;
        DrawCursor();
    }

    // -----------------------------------------------------------------------
    // Render scheduling
    // -----------------------------------------------------------------------

    /// <summary>
    /// Queues a full render (background + text content + cursor) at Render priority.
    /// Coalesces multiple calls into a single dispatch item.
    /// </summary>
    private void QueueFullRender()
    {
        if (_fullRenderPending) return;
        _fullRenderPending = true;
        Dispatcher.InvokeAsync(DoFullRender, DispatcherPriority.Render);
    }

    /// <summary>
    /// Queues a background-only render (selection + current-line highlight + cursor)
    /// at Render priority.  Skipped if a full render is already pending.
    /// </summary>
    private void QueueBackgroundRender()
    {
        if (_fullRenderPending || _backgroundRenderPending) return;
        _backgroundRenderPending = true;
        Dispatcher.InvokeAsync(DoBackgroundRender, DispatcherPriority.Render);
    }

    private void DoFullRender()
    {
        _fullRenderPending       = false;
        _backgroundRenderPending = false;
        EnsureFontMetrics();
        EnsureDpi();
        UpdateBackground();
        UpdateTextContent();
        DrawCursor();
    }

    private void DoBackgroundRender()
    {
        _backgroundRenderPending = false;
        EnsureFontMetrics();
        EnsureDpi();
        UpdateBackground();
        DrawCursor();
    }

    // -----------------------------------------------------------------------
    // Keyboard / Mouse
    // -----------------------------------------------------------------------

    protected override void OnGotFocus(RoutedEventArgs e)
    {
        base.OnGotFocus(e);
        StartCursorBlink();
        UpdateBackground(); // redraw separator/highlight with focus-aware styling
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        StopCursorBlink();
        UpdateBackground();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_vm is null) return;
        base.OnKeyDown(e);

        var ctrl  = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

        switch (e.Key)
        {
            // -- Clipboard / Edit shortcuts ------------------------------
            case Key.A when ctrl:
                ViewportSelectAll();
                e.Handled = true; break;
            case Key.C when ctrl:
                ViewportCopy();
                e.Handled = true; break;
            case Key.X when ctrl:
                ViewportCut();
                e.Handled = true; break;
            case Key.V when ctrl:
                ViewportPaste(); ScrollIntoView(_vm.CaretLine);
                e.Handled = true; break;

            // -- Navigation (with optional Shift selection) --------------
            case Key.Left:
                BeginSelectionIfShift(shift);
                if (_vm.CaretColumn > 0) _vm.CaretColumn--;
                else if (_vm.CaretLine > 0) { _vm.CaretLine--; _vm.CaretColumn = _vm.GetLine(_vm.CaretLine).Length; }
                if (!shift) _vm.ClearSelection();
                e.Handled = true; break;
            case Key.Right:
                BeginSelectionIfShift(shift);
                var curLine = _vm.GetLine(_vm.CaretLine);
                if (_vm.CaretColumn < curLine.Length) _vm.CaretColumn++;
                else if (_vm.CaretLine < _vm.LineCount - 1) { _vm.CaretLine++; _vm.CaretColumn = 0; }
                if (!shift) _vm.ClearSelection();
                e.Handled = true; break;
            case Key.Up:
                BeginSelectionIfShift(shift);
                if (_vm.CaretLine > 0) _vm.CaretLine--;
                if (!shift) _vm.ClearSelection();
                e.Handled = true; break;
            case Key.Down:
                BeginSelectionIfShift(shift);
                if (_vm.CaretLine < _vm.LineCount - 1) _vm.CaretLine++;
                if (!shift) _vm.ClearSelection();
                e.Handled = true; break;
            case Key.Home:
                BeginSelectionIfShift(shift);
                _vm.CaretColumn = ctrl ? 0 : GetFirstNonWhiteSpace(_vm.GetLine(_vm.CaretLine));
                if (ctrl) _vm.CaretLine = 0;
                if (!shift) _vm.ClearSelection();
                e.Handled = true; break;
            case Key.End:
                BeginSelectionIfShift(shift);
                _vm.CaretColumn = _vm.GetLine(_vm.CaretLine).Length;
                if (ctrl) _vm.CaretLine = _vm.LineCount - 1;
                if (!shift) _vm.ClearSelection();
                e.Handled = true; break;
            case Key.PageUp:
                BeginSelectionIfShift(shift);
                _vm.CaretLine = Math.Max(0, _vm.CaretLine - Math.Max(1, _visibleLineCount - 1));
                if (!shift) _vm.ClearSelection();
                ScrollIntoView(_vm.CaretLine);
                e.Handled = true; break;
            case Key.PageDown:
                BeginSelectionIfShift(shift);
                _vm.CaretLine = Math.Min(_vm.LineCount - 1, _vm.CaretLine + Math.Max(1, _visibleLineCount - 1));
                if (!shift) _vm.ClearSelection();
                ScrollIntoView(_vm.CaretLine);
                e.Handled = true; break;

            // -- Edit operations -----------------------------------------
            case Key.Back:
                if (_vm.HasSelection) _vm.DeleteSelectedText();
                else _vm.Backspace();
                ScrollIntoView(_vm.CaretLine);
                e.Handled = true; break;
            case Key.Delete:
                if (_vm.HasSelection) _vm.DeleteSelectedText();
                else _vm.DeleteForward();
                ScrollIntoView(_vm.CaretLine);
                e.Handled = true; break;
            case Key.Return:
                if (!_vm.IsReadOnly)
                {
                    if (_vm.HasSelection) _vm.DeleteSelectedText();
                    _vm.InsertChar('\n');
                    ScrollIntoView(_vm.CaretLine);
                }
                e.Handled = true; break;
            case Key.Tab:
                if (!_vm.IsReadOnly)
                {
                    if (_vm.HasSelection) _vm.DeleteSelectedText();
                    foreach (var c in "    ") _vm.InsertChar(c);
                    ScrollIntoView(_vm.CaretLine);
                }
                e.Handled = true; break;
            case Key.Z when ctrl:
                _vm.Undo(); ScrollIntoView(_vm.CaretLine);
                e.Handled = true; break;
            case Key.Y when ctrl:
                _vm.Redo(); ScrollIntoView(_vm.CaretLine);
                e.Handled = true; break;
        }

        // Content edits fire Lines/LineCount PropertyChanged → QueueFullRender().
        // Navigation fires CaretLine/CaretColumn PropertyChanged → QueueBackgroundRender().
        // Draw cursor immediately for instant visual feedback regardless.
        _cursorVisible = true;
        DrawCursor();
    }

    // Sets the selection anchor to the current caret position if there is no selection yet.
    private void BeginSelectionIfShift(bool shift)
    {
        if (!shift || _vm is null) return;
        if (!_vm.HasSelection)
        {
            _vm.SelectionAnchorLine   = _vm.CaretLine;
            _vm.SelectionAnchorColumn = _vm.CaretColumn;
        }
    }

    protected override void OnTextInput(TextCompositionEventArgs e)
    {
        if (_vm is null || _vm.IsReadOnly) return;
        base.OnTextInput(e);

        if (_vm.HasSelection) _vm.DeleteSelectedText();

        foreach (var c in e.Text)
        {
            if (c == '\r' || c == '\n') continue; // handled in OnKeyDown
            if (c < 0x20) continue;               // control chars
            _vm.InsertChar(c);
        }
        ScrollIntoView(_vm.CaretLine);
        _cursorVisible = true;
        DrawCursor();
        e.Handled = true;
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        // Right-click must never clear the selection — the context menu
        // should open with the current selection intact.
        if (e.ChangedButton == MouseButton.Right)
            return;

        base.OnMouseDown(e);
        Focus();

        if (_vm is null) return;

        var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

        // Save caret before move — needed for Shift+click anchor
        int oldLine = _vm.CaretLine;
        int oldCol  = _vm.CaretColumn;

        var pos  = e.GetPosition(this);
        int line = Math.Clamp((int)(pos.Y / _lineHeight) + _firstVisibleLine, 0, _vm.LineCount - 1);
        int col  = Math.Clamp((int)((pos.X - LineNumberColumnWidth - LeftMargin + _horizontalOffset) / _charWidth), 0, _vm.GetLine(line).Length);

        // Double-click: select word
        if (e.ClickCount == 2 && e.ChangedButton == MouseButton.Left)
        {
            _vm.CaretLine   = line;
            _vm.CaretColumn = col;
            SelectWordAtCaret();
            e.Handled = true;
            return;
        }

        if (shift)
        {
            // Shift+click: extend existing selection or start one from old caret
            if (!_vm.HasSelection)
            {
                _vm.SelectionAnchorLine   = oldLine;
                _vm.SelectionAnchorColumn = oldCol;
            }
        }
        else
        {
            _vm.ClearSelection();
        }

        _vm.CaretLine   = line;
        _vm.CaretColumn = col;

        if (e.ChangedButton == MouseButton.Left)
        {
            _isDragging = true;
            CaptureMouse();
        }

        _cursorVisible = true;
        DrawCursor();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (!_isDragging || _vm is null || e.LeftButton != MouseButtonState.Pressed) return;

        // Set anchor on first movement after button-down
        if (!_vm.HasSelection)
        {
            _vm.SelectionAnchorLine   = _vm.CaretLine;
            _vm.SelectionAnchorColumn = _vm.CaretColumn;
        }

        var pos  = e.GetPosition(this);
        int line = Math.Clamp((int)(pos.Y / _lineHeight) + _firstVisibleLine, 0, _vm.LineCount - 1);
        int col  = Math.Clamp((int)((pos.X - LineNumberColumnWidth - LeftMargin + _horizontalOffset) / _charWidth), 0, _vm.GetLine(line).Length);

        // Guard: skip if caret cell is unchanged (mouse within same character).
        if (line == _vm.CaretLine && col == _vm.CaretColumn) return;

        // Suppress VM PropertyChanged so OnVmPropertyChanged does not queue
        // a redundant background render — we update layer 0 directly below.
        _suppressVmNotify = true;
        _vm.CaretLine   = line;
        _vm.CaretColumn = col;
        _suppressVmNotify = false;

        // Redraw only layer 0 (selection + current-line highlight).
        // Layer 1 (text content) is unchanged during drag — not redrawn.
        UpdateBackground();
        _cursorVisible = true;
        DrawCursor();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        if (!_isDragging) return;
        _isDragging = false;
        ReleaseMouseCapture();

        // Simple click (no movement): clear the selection
        if (_vm is not null && _vm.HasSelection
            && _vm.SelectionAnchorLine   == _vm.CaretLine
            && _vm.SelectionAnchorColumn == _vm.CaretColumn)
        {
            _vm.ClearSelection();
        }

        UpdateBackground();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        int delta = e.Delta / 40;
        FirstVisibleLine = Math.Max(0, _firstVisibleLine - delta);
        e.Handled = true;
    }

    // -----------------------------------------------------------------------
    // Font metrics
    // -----------------------------------------------------------------------

    private void EnsureFontMetrics()
    {
        var font = TryFindResource("TE_FontFamily") as FontFamily
                   ?? new FontFamily("Cascadia Code, Consolas, Courier New");
        var size = TryFindResource("TE_FontSize") is double fs ? fs : DefaultFontSize;

        if (_typeface is not null && _emSize == size &&
            _cachedFontSize == size && Equals(_cachedTypeface, _typeface))
            return;

        _typeface       = new Typeface(font, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        _emSize         = size;
        _cachedFontSize = size;
        _cachedTypeface = _typeface;
        _brushCache.Clear();
        _lineRenderCache.Clear(); // font changed — all cached FormattedText is stale

        EnsureDpi();

        // Measure 'W' for character width (monospace assumption)
        var ft = new FormattedText("W",
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            _typeface, _emSize, Brushes.Black, _dpi.PixelsPerDip);

        _charWidth  = ft.Width;
        _lineHeight = ft.Height + 2; // +2 px line spacing

        var lnSample = BuildFormattedText("999999", Brushes.Gray);
        _lineNumberWidth = lnSample.Width;
    }

    private void EnsureDpi()
    {
        var src = PresentationSource.FromVisual(this);
        _dpi = src is not null
            ? new DpiScale(src.CompositionTarget!.TransformToDevice.M11,
                           src.CompositionTarget!.TransformToDevice.M22)
            : new DpiScale(1, 1);
    }

    // -----------------------------------------------------------------------
    // Brush / text helpers
    // -----------------------------------------------------------------------

    private Brush GetBrush(string key, Brush? fallback = null)
    {
        if (_brushCache.TryGetValue(key, out var cached)) return cached;

        var resource = TryFindResource(key);
        Brush brush;

        if (resource is Brush b) brush = b;
        else if (resource is Color c) brush = new SolidColorBrush(c);
        else brush = fallback ?? Brushes.White;

        brush.Freeze();
        _brushCache[key] = brush;
        return brush;
    }

    private FormattedText BuildFormattedText(string text, Brush foreground)
    {
        EnsureFontMetrics();
        return new FormattedText(
            text,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            _typeface!,
            _emSize,
            foreground,
            _dpi.PixelsPerDip);
    }

    // -----------------------------------------------------------------------
    // Clipboard helpers (called from OnKeyDown Ctrl+C/X/V/A)
    // -----------------------------------------------------------------------

    private void ViewportSelectAll()
    {
        if (_vm is null) return;
        _vm.SelectionAnchorLine   = 0;
        _vm.SelectionAnchorColumn = 0;
        _vm.CaretLine   = _vm.LineCount - 1;
        _vm.CaretColumn = _vm.GetLine(_vm.CaretLine).Length;
        // Selection/caret PropertyChanged → QueueBackgroundRender()
    }

    private void ViewportCopy()
    {
        if (_vm is null || !_vm.HasSelection) return;
        var text = _vm.GetSelectedText();
        if (!string.IsNullOrEmpty(text)) Clipboard.SetText(text);
    }

    private void ViewportCut()
    {
        if (_vm is null || !_vm.HasSelection || _vm.IsReadOnly) return;
        ViewportCopy();
        _vm.DeleteSelectedText();
        // Lines/LineCount PropertyChanged → QueueFullRender()
    }

    private void ViewportPaste()
    {
        if (_vm is null || _vm.IsReadOnly || !Clipboard.ContainsText()) return;
        _vm.InsertText(Clipboard.GetText());
        // Lines/LineCount PropertyChanged → QueueFullRender()
    }

    // -----------------------------------------------------------------------
    // Word selection (double-click)
    // -----------------------------------------------------------------------

    private void SelectWordAtCaret()
    {
        if (_vm is null) return;

        var line = _vm.GetLine(_vm.CaretLine);
        int col  = Math.Clamp(_vm.CaretColumn, 0, line.Length);

        if (col >= line.Length)
        {
            _vm.ClearSelection();
            return;
        }

        bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

        int start = col;
        if (IsWordChar(line[col]))
            while (start > 0 && IsWordChar(line[start - 1])) start--;

        int end = col;
        if (IsWordChar(line[col]))
            while (end < line.Length && IsWordChar(line[end])) end++;

        if (start == end) { _vm.ClearSelection(); return; }

        _vm.SelectionAnchorLine   = _vm.CaretLine;
        _vm.SelectionAnchorColumn = start;
        _vm.CaretColumn           = end;
        // CaretColumn/SelectionAnchor PropertyChanged → QueueBackgroundRender()
    }

    private static int GetFirstNonWhiteSpace(string line)
    {
        for (int i = 0; i < line.Length; i++)
            if (!char.IsWhiteSpace(line[i])) return i;
        return 0;
    }

    // -----------------------------------------------------------------------
    // Theme change detection
    // -----------------------------------------------------------------------

    // Sentinel DependencyProperty bound to TE_Background via SetResourceReference.
    // When the application theme swaps MergedDictionaries, WPF re-resolves every
    // DynamicResource binding — this triggers OnThemeWatcherChanged, which flushes
    // both brush and FormattedText caches and forces a full re-render.
    private static readonly DependencyProperty ThemeWatcherProperty =
        DependencyProperty.Register(
            nameof(ThemeWatcher),
            typeof(Brush),
            typeof(TextViewport),
            new PropertyMetadata(null, OnThemeWatcherChanged));

    private Brush? ThemeWatcher
    {
        get => (Brush?)GetValue(ThemeWatcherProperty);
        set => SetValue(ThemeWatcherProperty, value);
    }

    private static void OnThemeWatcherChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var vp = (TextViewport)d;
        vp._brushCache.Clear();
        vp._lineRenderCache.Clear(); // brushes changed — cached FormattedText has stale colors
        vp._cachedFontSize = -1;     // force TE_FontFamily / TE_FontSize re-read
        vp.InvalidateVisual();       // triggers OnRender → full render
    }

    // -----------------------------------------------------------------------
    // VM property changes
    // -----------------------------------------------------------------------

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Suppressed during OnMouseMove's direct caret update to avoid a
        // redundant queued render on top of the immediate UpdateBackground() call.
        if (_suppressVmNotify) return;

        switch (e.PropertyName)
        {
            case nameof(TextEditorViewModel.Lines):
            case nameof(TextEditorViewModel.LineCount):
                // Text content changed — evict entire FormattedText cache and do a full render.
                _lineRenderCache.Clear();
                QueueFullRender();
                break;

            case nameof(TextEditorViewModel.CaretLine):
            case nameof(TextEditorViewModel.CaretColumn):
            case nameof(TextEditorViewModel.HasSelection):
                // Only the background layer needs updating:
                // current-line highlight moves with the caret; selection geometry changes.
                // Text layer is unchanged — skip the expensive UpdateTextContent().
                QueueBackgroundRender();
                break;
        }
    }
}
