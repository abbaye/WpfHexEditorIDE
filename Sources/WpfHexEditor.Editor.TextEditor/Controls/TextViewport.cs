//////////////////////////////////////////////
// Apache 2.0  - 2026
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
/// <see cref="OnRender(DrawingContext)"/>.  No external dependency — pure WPF.
/// <para>
/// Pattern: same architecture as HexViewport (FrameworkElement + DrawingContext +
/// FormattedText caching + DrawingVisual cursor overlay).
/// </para>
/// </summary>
internal sealed class TextViewport : FrameworkElement
{
    // -----------------------------------------------------------------------
    // Constants
    // -----------------------------------------------------------------------

    private const double DefaultFontSize = 13.0;
    private const double LineNumberPadding = 4.0;
    private const double LeftMargin = 6.0;

    // -----------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------

    private TextEditorViewModel? _vm;
    private double _lineHeight;
    private double _charWidth;
    private double _lineNumberWidth;
    private int _firstVisibleLine;
    private int _visibleLineCount;
    private double _horizontalOffset;
    private readonly DispatcherTimer _cursorBlinkTimer;
    private bool _cursorVisible = true;
    private readonly DrawingVisual _cursorOverlay;
    private DpiScale _dpi;

    // Typeface / font (recached when theme resource changes)
    private Typeface? _typeface;
    private double _emSize;

    // Mouse drag selection
    private bool _isDragging;

    // Brush cache — keyed by theme resource key
    private readonly Dictionary<string, Brush> _brushCache = new();

    // FormattedText measurement cache (single char 'W' for width, 'g' for height)
    private double _cachedFontSize = -1;
    private Typeface? _cachedTypeface;

    // -----------------------------------------------------------------------
    // Visual children (cursor overlay)
    // -----------------------------------------------------------------------

    private readonly VisualCollection _visuals;

    protected override int VisualChildrenCount => _visuals.Count;
    protected override Visual GetVisualChild(int index) => _visuals[index];

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------

    public TextViewport()
    {
        _visuals = new VisualCollection(this);
        _cursorOverlay = new DrawingVisual();
        _visuals.Add(_cursorOverlay);

        _cursorBlinkTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(530)
        };
        _cursorBlinkTimer.Tick += OnCursorBlink;

        Focusable = true;
        ClipToBounds = true;
        SnapsToDevicePixels = true;
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
        InvalidateMeasure();
        InvalidateVisual();
    }

    public void Detach()
    {
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm = null;
        }
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
                InvalidateVisual();
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
                InvalidateVisual();
            }
        }
    }

    /// <summary>
    /// Total document height in device-independent units.
    /// </summary>
    public double TotalHeight => (_vm?.LineCount ?? 0) * LineHeight;

    /// <summary>
    /// Estimated max line width (for horizontal scrollbar).
    /// </summary>
    public double EstimatedMaxWidth => (_vm?.Lines.Max(l => l.Length) ?? 0) * CharWidth + LineNumberColumnWidth + LeftMargin + 20;

    public double LineHeight    => _lineHeight > 0 ? _lineHeight : 18;
    public double CharWidth     => _charWidth > 0 ? _charWidth : 7.2;
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

        // WPF forbids returning PositiveInfinity from MeasureOverride.
        // When hosted inside a ScrollViewer, availableSize may be infinite on one or both axes.
        // Return a finite desired size: actual document dimensions clamped to available space.
        double desiredWidth  = double.IsInfinity(availableSize.Width)
            ? EstimatedMaxWidth
            : availableSize.Width;

        double desiredHeight = double.IsInfinity(availableSize.Height)
            ? TotalHeight
            : availableSize.Height;

        return new Size(
            Math.Max(0, desiredWidth),
            Math.Max(0, desiredHeight));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _visibleLineCount = _lineHeight > 0 ? (int)Math.Ceiling(finalSize.Height / _lineHeight) + 1 : 0;
        return finalSize;
    }

    // -----------------------------------------------------------------------
    // Main render
    // -----------------------------------------------------------------------

    protected override void OnRender(DrawingContext dc)
    {
        if (_vm is null) return;

        EnsureFontMetrics();
        EnsureDpi();

        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);

        // Background
        dc.DrawRectangle(GetBrush("TE_Background"), null, bounds);

        if (_lineHeight <= 0) return;

        var lines        = _vm.Lines;
        int totalLines   = lines.Count;
        int firstLine    = Math.Max(0, _firstVisibleLine);
        int lastLine     = Math.Min(totalLines - 1, firstLine + _visibleLineCount);

        // Line number column background
        var lnBg = GetBrush("TE_LineNumberBackground");
        dc.DrawRectangle(lnBg, null, new Rect(0, 0, LineNumberColumnWidth, ActualHeight));

        // Separator line between line numbers and code
        var sep = GetBrush("TE_LineNumberForeground");
        dc.DrawRectangle(sep, null, new Rect(LineNumberColumnWidth - 1, 0, 1, ActualHeight));

        double codeX = LineNumberColumnWidth + LeftMargin - _horizontalOffset;

        // Selection highlight (drawn before text so text renders on top)
        if (_vm.HasSelection)
            RenderSelection(dc, firstLine, lastLine, codeX);

        for (int li = firstLine; li <= lastLine; li++)
        {
            double y    = (li - firstLine) * _lineHeight;
            var    line = lines[li];

            // --- Current-line highlight ---
            if (li == _vm.CaretLine)
            {
                dc.DrawRectangle(GetBrush("TE_CurrentLineBrush"), null,
                    new Rect(0, y, ActualWidth, _lineHeight));
            }

            // --- Line number ---
            var lnText = BuildFormattedText((li + 1).ToString(), GetBrush("TE_LineNumberForeground"));
            double lnX = LineNumberColumnWidth - lnText.Width - LineNumberPadding;
            dc.DrawText(lnText, new Point(lnX, y + (_lineHeight - lnText.Height) / 2));

            if (string.IsNullOrEmpty(line)) continue;

            // --- Syntax-highlighted spans ---
            var spans = _vm.GetHighlightedSpans(li);
            if (spans.Count > 0)
                RenderHighlightedLine(dc, line, spans, codeX, y);
            else
            {
                // Plain text
                var ft = BuildFormattedText(line, GetBrush("TE_Foreground"));
                dc.DrawText(ft, new Point(codeX, y));
            }
        }

        // Cursor is drawn in the overlay visual, not here.
        DrawCursor();
    }

    private void RenderSelection(DrawingContext dc, int firstVisLine, int lastVisLine, double codeX)
    {
        if (_vm is null || !_vm.HasSelection) return;

        int ancLine = _vm.SelectionAnchorLine;
        int ancCol  = _vm.SelectionAnchorColumn;
        int carLine = _vm.CaretLine;
        int carCol  = _vm.CaretColumn;

        // Normalise: startLine <= endLine
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
            if (x2 > x1) dc.DrawRectangle(selBrush, null, new Rect(x1, y, x2 - x1, _lineHeight));
            return;
        }

        // First (partial) line
        if (startLine >= firstVisLine && startLine <= lastVisLine)
        {
            double y      = (startLine - firstVisLine) * _lineHeight;
            double x1     = codeX + startCol * _charWidth;
            double lineW  = (_vm.GetLine(startLine).Length - startCol) * _charWidth;
            double width  = Math.Max(lineW, _charWidth);
            dc.DrawRectangle(selBrush, null, new Rect(x1, y, width, _lineHeight));
        }

        // Middle (full) lines
        for (int li = startLine + 1; li < endLine; li++)
        {
            if (li < firstVisLine || li > lastVisLine) continue;
            double y     = (li - firstVisLine) * _lineHeight;
            double width = Math.Max(_vm.GetLine(li).Length * _charWidth, _charWidth);
            dc.DrawRectangle(selBrush, null, new Rect(codeX, y, width, _lineHeight));
        }

        // Last (partial) line
        if (endLine >= firstVisLine && endLine <= lastVisLine)
        {
            double y     = (endLine - firstVisLine) * _lineHeight;
            double width = Math.Max(endCol * _charWidth, _charWidth);
            dc.DrawRectangle(selBrush, null, new Rect(codeX, y, width, _lineHeight));
        }
    }

    private void RenderHighlightedLine(DrawingContext dc, string line,
        IReadOnlyList<ColoredSpan> spans, double codeX, double y)
    {
        int pos = 0;
        var defaultBrush = GetBrush("TE_Foreground");

        foreach (var span in spans)
        {
            // Guard: spans computed on a stale/different line version must not
            // reference positions beyond the current line length.
            if (span.Start >= line.Length) break;

            // Render unstyled text before this span
            if (span.Start > pos)
            {
                int safeEnd = Math.Min(span.Start, line.Length);
                int safePos = Math.Min(pos, safeEnd);
                var raw = line[safePos..safeEnd];
                if (!string.IsNullOrEmpty(raw))
                {
                    var ft = BuildFormattedText(raw, defaultBrush);
                    dc.DrawText(ft, new Point(codeX + safePos * _charWidth, y));
                }
            }

            // Render styled span
            var spanText = span.Start < line.Length
                ? line.Substring(span.Start, Math.Min(span.Length, line.Length - span.Start))
                : string.Empty;

            if (!string.IsNullOrEmpty(spanText))
            {
                var brush = GetBrush(span.ColorKey, defaultBrush);
                var ft    = BuildFormattedText(spanText, brush);
                dc.DrawText(ft, new Point(codeX + span.Start * _charWidth, y));
            }

            pos = span.Start + span.Length;
        }

        // Remaining text after last span
        if (pos < line.Length)
        {
            var tail = line[pos..];
            if (!string.IsNullOrEmpty(tail))
            {
                var ft = BuildFormattedText(tail, defaultBrush);
                dc.DrawText(ft, new Point(codeX + pos * _charWidth, y));
            }
        }
    }

    // -----------------------------------------------------------------------
    // Cursor overlay
    // -----------------------------------------------------------------------

    private void DrawCursor()
    {
        using var dc = _cursorOverlay.RenderOpen();

        if (_vm is null || !_cursorVisible || !IsKeyboardFocusWithin) return;

        int caretLine = _vm.CaretLine;
        int caretCol  = _vm.CaretColumn;

        if (caretLine < _firstVisibleLine || caretLine > _firstVisibleLine + _visibleLineCount)
            return;

        double y    = (caretLine - _firstVisibleLine) * _lineHeight;
        double x    = LineNumberColumnWidth + LeftMargin + caretCol * _charWidth - _horizontalOffset;
        var    pen  = new Pen(GetBrush("TE_Foreground"), 1.5);
        dc.DrawLine(pen, new Point(x, y + 1), new Point(x, y + _lineHeight - 1));
    }

    private void OnCursorBlink(object? sender, EventArgs e)
    {
        _cursorVisible = !_cursorVisible;
        DrawCursor();
    }

    // -----------------------------------------------------------------------
    // Keyboard / Mouse
    // -----------------------------------------------------------------------

    protected override void OnGotFocus(RoutedEventArgs e)
    {
        base.OnGotFocus(e);
        StartCursorBlink();
        InvalidateVisual();
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        StopCursorBlink();
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_vm is null) return;
        base.OnKeyDown(e);

        var ctrl  = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

        switch (e.Key)
        {
            // ── Clipboard / Edit shortcuts ──────────────────────────────
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

            // ── Navigation (with optional Shift selection) ──────────────
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

            // ── Edit operations ─────────────────────────────────────────
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

        InvalidateVisual();
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

        // Replace selection with the typed text
        if (_vm.HasSelection) _vm.DeleteSelectedText();

        foreach (var c in e.Text)
        {
            if (c == '\r' || c == '\n') continue; // handled in OnKeyDown
            if (c < 0x20) continue;               // control chars
            _vm.InsertChar(c);
        }
        ScrollIntoView(_vm.CaretLine);
        InvalidateVisual();
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
            InvalidateVisual();
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

        InvalidateVisual();
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

        _vm.CaretLine   = line;
        _vm.CaretColumn = col;

        InvalidateVisual();
        _cursorVisible = true;
        DrawCursor();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        if (!_isDragging) return;
        _isDragging = false;
        ReleaseMouseCapture();

        // Simple click (no movement): clear the selection that may have been set
        if (_vm is not null && _vm.HasSelection
            && _vm.SelectionAnchorLine   == _vm.CaretLine
            && _vm.SelectionAnchorColumn == _vm.CaretColumn)
        {
            _vm.ClearSelection();
        }

        InvalidateVisual();
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

        _typeface = new Typeface(font, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        _emSize   = size;
        _cachedFontSize  = size;
        _cachedTypeface  = _typeface;
        _brushCache.Clear();

        EnsureDpi();

        // Measure 'W' for character width (monospace assumption)
        var ft = new FormattedText("W",
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            _typeface, _emSize, Brushes.Black, _dpi.PixelsPerDip);

        _charWidth  = ft.Width;
        _lineHeight = ft.Height + 2; // +2px line spacing

        // Measure the widest possible line number (9 digits)
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
    // Helpers
    // -----------------------------------------------------------------------

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
        InvalidateVisual();
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
        InvalidateVisual();
    }

    private void ViewportPaste()
    {
        if (_vm is null || _vm.IsReadOnly || !Clipboard.ContainsText()) return;
        _vm.InsertText(Clipboard.GetText());
        InvalidateVisual();
    }

    // -----------------------------------------------------------------------
    // Word selection (double-click)
    // -----------------------------------------------------------------------

    private void SelectWordAtCaret()
    {
        if (_vm is null) return;

        var line = _vm.GetLine(_vm.CaretLine);
        int col  = Math.Clamp(_vm.CaretColumn, 0, line.Length);

        // Boundary condition: empty line or caret past end
        if (col >= line.Length)
        {
            _vm.ClearSelection();
            return;
        }

        bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

        // Find word start
        int start = col;
        if (IsWordChar(line[col]))
            while (start > 0 && IsWordChar(line[start - 1])) start--;

        // Find word end
        int end = col;
        if (IsWordChar(line[col]))
            while (end < line.Length && IsWordChar(line[end])) end++;

        if (start == end) { _vm.ClearSelection(); return; }

        _vm.SelectionAnchorLine   = _vm.CaretLine;
        _vm.SelectionAnchorColumn = start;
        _vm.CaretColumn           = end;
        InvalidateVisual();
    }

    private static int GetFirstNonWhiteSpace(string line)
    {
        for (int i = 0; i < line.Length; i++)
            if (!char.IsWhiteSpace(line[i])) return i;
        return 0;
    }

    // -----------------------------------------------------------------------
    // VM event
    // -----------------------------------------------------------------------

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(TextEditorViewModel.Lines):
            case nameof(TextEditorViewModel.LineCount):
            case nameof(TextEditorViewModel.CaretLine):
            case nameof(TextEditorViewModel.CaretColumn):
                Dispatcher.InvokeAsync(InvalidateVisual, DispatcherPriority.Render);
                break;
        }
    }
}
