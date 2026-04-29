//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WpfHexEditor.Core;
using WpfHexEditor.Editor.Core.Helpers;
using WpfHexEditor.Editor.TextEditor.Highlighting;
using WpfHexEditor.Editor.TextEditor.ViewModels;
using WpfHexEditor.Editor.TextEditor.Selection;
using WpfHexEditor.Editor.TextEditor.Input;

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
    private int    _firstVisibleLine;
    private int    _visibleLineCount;
    private double _viewportVisibleHeight; // set by TextEditor from ScrollViewer.ViewportHeight
    private double _horizontalOffset;
    private Typeface? _typeface;
    private double _emSize;
    private double _cachedFontSize = -1;
    private double _cachedZoom     = -1; // P2-01: tracks last zoom applied to font metrics
    private Typeface? _cachedTypeface;
    private DpiScale _dpi;

    // Word wrap (ADR-049)
    private bool _isWordWrapEnabled;
    private int  _charsPerRow;
    private int[] _wrapHeights = Array.Empty<int>(); // visual rows per logical line
    private int[] _wrapOffsets = Array.Empty<int>(); // first visual row of logical line i (prefix sum)
    private int  _totalVisualRows;
    private double _lastArrangedWidth = -1;

    // Mouse drag selection
    private bool _isDragging;

    // 60 Hz throttle for drag selection redraws (P1-TE-02)
    private long _lastDragRenderTick;

    // Render-time measurement — reported via RefreshTimeUpdated to TextEditor for status bar.
    private readonly Stopwatch _refreshStopwatch = new();
    internal event EventHandler<long>? RefreshTimeUpdated;
    private static readonly long DragThrottleTicks
        = (long)(System.Diagnostics.Stopwatch.Frequency / 60.0); // ~16.7 ms

    // Set to true while OnMouseMove updates the caret directly so that
    // OnVmPropertyChanged does not queue a redundant background render.
    private bool _suppressVmNotify;

    // Whitespace markers — pale dots/arrows, configurable mode
    internal enum WhitespaceMode { None, Selection, Always }
    internal WhitespaceMode WhitespaceDisplayMode { get; set; } = WhitespaceMode.Selection;

    // Feature A — Rectangular (block/column) selection (Alt+LeftClick+drag)
    private readonly RectangularSelection _rectSelection = new();
    private bool _isRectSelecting;

    /// <summary>Exposes the rectangular selection for clipboard operations in TextEditor.</summary>
    internal RectangularSelection RectSelection => _rectSelection;

    // Feature B — Text drag-and-drop (move selection by dragging)
    private readonly DragDropState _textDragDrop = new();
    private bool _isRectDrag; // true when the active drag originates from a rect selection block

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
    private readonly DrawingVisual _whitespaceVisual  = new(); // layer 3 — whitespace markers
    private readonly DrawingVisual _panOverlay        = new(); // layer 4 — pan mode indicator (topmost)

    private readonly VisualCollection _visuals;

    // Middle-click auto-scroll (pan mode)
    private PanModeController _panMode = null!;

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
        _visuals.Add(_cursorOverlay);      // z = 2
        _visuals.Add(_whitespaceVisual);   // z = 3 (whitespace markers)
        _visuals.Add(_panOverlay);         // z = 4 (topmost — pan mode indicator)

        _panMode = new PanModeController(this, (_, dy) =>
        {
            if (_lineHeight <= 0) return;
            int delta = (int)Math.Round(dy / _lineHeight);
            if (delta != 0) FirstVisibleLine = Math.Max(0, FirstVisibleLine + delta);
        });

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
        {
            _vm.PropertyChanged    -= OnVmPropertyChanged;
            _vm.HighlightsComputed -= OnHighlightsComputed;
        }

        _vm = vm;
        vm.PropertyChanged    += OnVmPropertyChanged;
        vm.HighlightsComputed += OnHighlightsComputed;
        _lineRenderCache.Clear();
        InvalidateMeasure();
        InvalidateVisual(); // triggers OnRender → full render
    }

    public void Detach()
    {
        if (_vm is not null)
        {
            _vm.PropertyChanged    -= OnVmPropertyChanged;
            _vm.HighlightsComputed -= OnHighlightsComputed;
            _vm = null;
        }
        _lineRenderCache.Clear();
        InvalidateVisual();
    }

    /// <summary>
    /// The visible height of the ScrollViewer viewport (pixels). Set by TextEditor from
    /// ScrollViewer.ViewportHeight so ArrangeOverride can compute the correct _visibleLineCount
    /// without walking the visual tree. Defaults to 0 (ArrangeOverride falls back to finalSize).
    /// </summary>
    public double ViewportVisibleHeight
    {
        get => _viewportVisibleHeight;
        set => _viewportVisibleHeight = value;
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

    /// <summary>
    /// When true, long lines wrap at the viewport edge instead of scrolling horizontally.
    /// Rebuilds the visual-row wrap map on change. (ADR-049)
    /// </summary>
    public bool IsWordWrapEnabled
    {
        get => _isWordWrapEnabled;
        set
        {
            if (_isWordWrapEnabled == value) return;
            _isWordWrapEnabled = value;
            _horizontalOffset  = 0;
            RebuildWrapMap();
            InvalidateMeasure();
            QueueFullRender();
        }
    }

    /// <summary>Total document height in device-independent units.</summary>
    public double TotalHeight => _isWordWrapEnabled
        ? _totalVisualRows * LineHeight
        : (_vm?.LineCount ?? 0) * LineHeight;

    /// <summary>Estimated max line width (for horizontal scrollbar). O(1) — reads ViewModel incremental cache.</summary>
    public double EstimatedMaxWidth => _isWordWrapEnabled
        ? Math.Max(1, ActualWidth)
        : (_vm?.MaxLineLength ?? 0) * CharWidth + LineNumberColumnWidth + LeftMargin + 20;

    public double LineHeight            => _lineHeight > 0 ? _lineHeight : 18;
    public double CharWidth             => _charWidth  > 0 ? _charWidth  : 7.2;
    public double LineNumberColumnWidth => _lineNumberWidth + LineNumberPadding * 2;

    /// <summary>
    /// Returns the absolute X position of the caret at <paramref name="col"/>
    /// (before horizontal offset is subtracted). Word-wrap-aware.
    /// </summary>
    public double GetCaretAbsoluteX(int col)
    {
        if (_isWordWrapEnabled && _charsPerRow > 0 && _vm is not null)
        {
            string line    = _vm.GetLine(_vm.CaretLine);
            int visCol     = CharIndexToVisualCol(line, col) % _charsPerRow;
            return LineNumberColumnWidth + LeftMargin + visCol * CharWidth;
        }
        return LineNumberColumnWidth + LeftMargin + col * CharWidth;
    }

    public void ScrollIntoView(int lineIndex)
    {
        if (_visibleLineCount == 0) return;
        if (_isWordWrapEnabled && lineIndex < _wrapOffsets.Length)
        {
            int visFirst = _wrapOffsets[lineIndex];
            int visLast  = visFirst + _wrapHeights[lineIndex] - 1;
            if (visFirst < _firstVisibleLine)
                FirstVisibleLine = visFirst;
            else if (visLast >= _firstVisibleLine + _visibleLineCount)
                FirstVisibleLine = Math.Max(0, visLast - _visibleLineCount + 1);
            return;
        }
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

        double desiredWidth;
        if (_isWordWrapEnabled)
            // Word wrap: fill available width exactly — no horizontal extent needed.
            desiredWidth = double.IsInfinity(availableSize.Width) ? Math.Max(1, ActualWidth) : availableSize.Width;
        else
            desiredWidth = double.IsInfinity(availableSize.Width) ? EstimatedMaxWidth : availableSize.Width;

        // Always report TotalHeight regardless of the available size passed by the ScrollViewer.
        // TextViewport is a virtual renderer — it only paints visible lines, but it must report
        // the full document height so the ScrollViewer can compute the correct thumb size and
        // scroll extent. If we return availableSize.Height here, extent == viewport and the
        // ScrollViewer concludes there is nothing to scroll.
        double desiredHeight = Math.Max(TotalHeight, availableSize.Height > 0 && !double.IsInfinity(availableSize.Height)
            ? availableSize.Height
            : 0);

        return new Size(Math.Max(0, desiredWidth), Math.Max(0, desiredHeight));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // Word wrap: rebuild map when the available width changes (window resize).
        if (_isWordWrapEnabled && Math.Abs(finalSize.Width - _lastArrangedWidth) > 0.5)
        {
            _lastArrangedWidth = finalSize.Width;
            RebuildWrapMap();
        }
        // finalSize.Height equals TotalHeight (the desired size we reported to give the ScrollViewer
        // the correct scroll extent). Use ViewportVisibleHeight (set by TextEditor from the
        // ScrollViewer's ViewportHeight) so _visibleLineCount reflects physically visible lines only.
        double visibleHeight = _viewportVisibleHeight > 0 ? _viewportVisibleHeight : finalSize.Height;
        _visibleLineCount = _lineHeight > 0 ? (int)Math.Ceiling(visibleHeight / _lineHeight) + 1 : 0;
        return finalSize;
    }

    // -----------------------------------------------------------------------
    // Word wrap helpers (ADR-049)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Rebuilds the per-line visual-row arrays used when <see cref="IsWordWrapEnabled"/> is true.
    /// O(n) over logical line count. Safe to call multiple times — always produces a consistent map.
    /// </summary>
    private void RebuildWrapMap()
    {
        if (!_isWordWrapEnabled || _vm is null || _charWidth <= 0)
        {
            _wrapHeights     = Array.Empty<int>();
            _wrapOffsets     = Array.Empty<int>();
            _totalVisualRows = 0;
            return;
        }

        double baseW  = _lastArrangedWidth > 0 ? _lastArrangedWidth : ActualWidth;
        double availW = baseW - LineNumberColumnWidth - LeftMargin;
        _charsPerRow  = Math.Max(1, (int)(availW / _charWidth));

        var lines = _vm.Lines;
        int n     = lines.Count;
        _wrapHeights = new int[n];
        _wrapOffsets = new int[n];
        int total = 0;
        for (int i = 0; i < n; i++)
        {
            _wrapOffsets[i] = total;
            int len          = VisualLength(lines[i]);
            int h            = len == 0 ? 1 : (int)Math.Ceiling((double)len / _charsPerRow);
            _wrapHeights[i]  = h;
            total           += h;
        }
        _totalVisualRows = total;
    }

    /// <summary>
    /// Binary-searches <see cref="_wrapOffsets"/> to find the logical line that owns
    /// <paramref name="visualRow"/>. Returns (logLine, subRow) where subRow is the
    /// 0-based index within that logical line's visual rows.
    /// </summary>
    private (int logLine, int subRow) VisualRowToLogical(int visualRow)
    {
        if (_wrapOffsets.Length == 0) return (Math.Max(0, visualRow), 0);
        int lo = 0, hi = _wrapOffsets.Length - 1;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (_wrapOffsets[mid] <= visualRow) lo = mid;
            else hi = mid - 1;
        }
        return (lo, visualRow - _wrapOffsets[lo]);
    }

    /// <summary>
    /// Returns the visual column count of <paramref name="line"/>, expanding each
    /// tab character to <c>TabVisualWidth</c> (4) columns.
    /// </summary>
    private static int VisualLength(string line)
    {
        const int TabVisualWidth = 4;
        int len = 0;
        foreach (char c in line)
            len += c == '\t' ? TabVisualWidth : 1;
        return len;
    }

    /// <summary>
    /// Maps a visual column offset to the corresponding character index in
    /// <paramref name="line"/>, accounting for tab expansion.
    /// </summary>
    private static int VisualColToCharIndex(string line, int visualCol)
    {
        const int TabVisualWidth = 4;
        int vc = 0;
        for (int i = 0; i < line.Length; i++)
        {
            if (vc >= visualCol) return i;
            vc += line[i] == '\t' ? TabVisualWidth : 1;
        }
        return line.Length;
    }

    /// <summary>
    /// Maps a character index to its visual column offset, accounting for tab expansion.
    /// </summary>
    private static int CharIndexToVisualCol(string line, int charIndex)
    {
        const int TabVisualWidth = 4;
        int vc = 0;
        int end = Math.Min(charIndex, line.Length);
        for (int i = 0; i < end; i++)
            vc += line[i] == '\t' ? TabVisualWidth : 1;
        return vc;
    }

    /// <summary>
    /// Converts a pixel position inside the viewport to a logical (line, col) pair.
    /// Handles both normal and word-wrap modes.
    /// </summary>
    private (int line, int col) HitTestPosition(System.Windows.Point pos)
    {
        if (!_isWordWrapEnabled || _wrapOffsets.Length == 0 || _vm is null)
        {
            int ln = Math.Clamp((int)(pos.Y / _lineHeight) + _firstVisibleLine, 0, (_vm?.LineCount ?? 1) - 1);
            int cl = Math.Clamp((int)((pos.X - LineNumberColumnWidth - LeftMargin + _horizontalOffset) / _charWidth), 0, _vm?.GetLine(ln).Length ?? 0);
            return (ln, cl);
        }
        int visRow         = Math.Clamp((int)(pos.Y / _lineHeight) + _firstVisibleLine, 0, _totalVisualRows - 1);
        var (logLine, sub) = VisualRowToLogical(visRow);
        logLine            = Math.Clamp(logLine, 0, _vm.LineCount - 1);
        int visualCol      = Math.Clamp((int)((pos.X - LineNumberColumnWidth - LeftMargin) / _charWidth), 0, _charsPerRow);
        string hitLine     = _vm.GetLine(logLine);
        int logCol         = Math.Clamp(VisualColToCharIndex(hitLine, sub * _charsPerRow + visualCol), 0, hitLine.Length);
        return (logLine, logCol);
    }

    // -----------------------------------------------------------------------
    // OnRender — triggered by WPF for layout/size/theme/InvalidateVisual changes.
    // Performs a full synchronous render and cancels any pending async renders.
    // -----------------------------------------------------------------------

    protected override void OnRender(DrawingContext dc)
    {
        _refreshStopwatch.Restart();

        // Reserve hit-test area. Actual rendering is in DrawingVisual children.
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));

        // Cancel any queued async renders — we're doing a full render now.
        _fullRenderPending       = false;
        _backgroundRenderPending = false;

        EnsureFontMetrics();
        EnsureDpi();

        UpdateBackground();
        UpdateTextContent();
        DrawWhitespaceMarkers();
        DrawCursor();
        DrawPanOverlay();

        _refreshStopwatch.Stop();
        RefreshTimeUpdated?.Invoke(this, _refreshStopwatch.ElapsedMilliseconds);
    }

    private void DrawPanOverlay()
    {
        using var dc = _panOverlay.RenderOpen();
        _panMode.Render(dc);
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

        if (_isWordWrapEnabled && _wrapOffsets.Length > 0)
        {
            // ---- Word-wrap rendering path ----
            int firstVisRow = _firstVisibleLine;
            double codeX    = LineNumberColumnWidth + LeftMargin;

            // Current-line highlight: cover all visual rows of the caret's logical line.
            int caretLogLine = _vm.CaretLine;
            if (caretLogLine >= 0 && caretLogLine < _wrapOffsets.Length)
            {
                int rowStart = _wrapOffsets[caretLogLine];
                int rowEnd   = rowStart + _wrapHeights[caretLogLine] - 1;
                for (int vr = Math.Max(rowStart, firstVisRow);
                     vr <= Math.Min(rowEnd, firstVisRow + _visibleLineCount); vr++)
                {
                    double y = (vr - firstVisRow) * _lineHeight;
                    dc.DrawRectangle(GetBrush("TE_CurrentLineBrush"), null,
                        new Rect(0, y, ActualWidth, _lineHeight));
                }
            }

            // Selection rectangles (word-wrap path).
            if (_vm.HasSelection)
                DrawSelectionRectsWrapped(dc, firstVisRow, codeX);
        }
        else
        {
            // ---- Normal (no-wrap) rendering path ----
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

            // Feature A: rectangular (block) selection overlay.
            DrawRectSelectionRects(dc, firstLine, lastLine, codeX);

            // Feature B: drag-to-move insertion caret.
            DrawDragDropCaret(dc, firstLine, codeX);
        }
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

    /// <summary>
    /// Word-wrap path: draws selection highlights mapped to visual rows.
    /// Each logical line's selected character range is split across its visual sub-rows.
    /// </summary>
    private void DrawSelectionRectsWrapped(DrawingContext dc, int firstVisRow, double codeX)
    {
        if (_vm is null || !_vm.HasSelection || _wrapOffsets.Length == 0) return;

        _vm.NormalizeSelection(out int startLine, out int startCol, out int endLine, out int endCol);

        var selBrush = GetBrush("TE_SelectionBackground");
        int lastVisRow = firstVisRow + _visibleLineCount;

        for (int li = startLine; li <= endLine; li++)
        {
            if (li >= _wrapOffsets.Length) break;
            int lineLen  = _vm.GetLine(li).Length;
            int selStart = li == startLine ? startCol : 0;
            int selEnd   = li == endLine   ? endCol   : lineLen;
            if (selStart >= selEnd && li < endLine) { selEnd = Math.Max(selStart + 1, lineLen); }

            int rowBase   = _wrapOffsets[li];
            int rowHeight = _wrapHeights[li];
            for (int sr = 0; sr < rowHeight; sr++)
            {
                int vr        = rowBase + sr;
                if (vr > lastVisRow) break;
                if (vr < firstVisRow) continue;

                string lineText     = _vm.GetLine(li);
                int rowStartChar    = VisualColToCharIndex(lineText, sr * _charsPerRow);
                int rowEndChar      = VisualColToCharIndex(lineText, (sr + 1) * _charsPerRow);
                if (rowEndChar <= rowStartChar) rowEndChar = Math.Min(rowStartChar + 1, lineLen);
                int clampedSelStart = Math.Max(selStart, rowStartChar);
                int clampedSelEnd   = Math.Min(selEnd,   rowEndChar);
                if (clampedSelStart >= clampedSelEnd) continue;

                double y  = (vr - firstVisRow) * _lineHeight;
                double x1 = codeX + (CharIndexToVisualCol(lineText, clampedSelStart) - sr * _charsPerRow) * _charWidth;
                double x2 = codeX + (CharIndexToVisualCol(lineText, clampedSelEnd)   - sr * _charsPerRow) * _charWidth;
                dc.DrawRoundedRectangle(selBrush, null,
                    new Rect(x1, y, Math.Max(x2 - x1, _charWidth), _lineHeight),
                    SelectionCornerRadius, SelectionCornerRadius);
            }
        }
    }

    /// <summary>
    /// Draws the rectangular (block/column) selection overlay as a single seamless rectangle
    /// spanning the full vertical extent of the visible selection. One draw call eliminates
    /// the anti-aliasing seams produced by per-line independent rasterization.
    /// </summary>
    private void DrawRectSelectionRects(DrawingContext dc, int firstVisLine, int lastVisLine, double codeX)
    {
        if (_rectSelection.IsEmpty) return;

        // Clamp selection to visible viewport.
        int visTop    = Math.Max(_rectSelection.TopLine,    firstVisLine);
        int visBottom = Math.Min(_rectSelection.BottomLine, lastVisLine);
        if (visTop > visBottom) return;

        var selBrush = GetBrush("TE_SelectionBackground");

        var (leftCol, rightCol) = _rectSelection.GetColumnRange();
        double x1    = codeX + leftCol  * _charWidth;
        double x2    = codeX + rightCol * _charWidth;
        double width = Math.Max(x2 - x1, 1.0);

        double yTop    = (visTop    - firstVisLine) * _lineHeight;
        double yBottom = (visBottom - firstVisLine + 1) * _lineHeight;

        dc.DrawRectangle(selBrush, null, new Rect(x1, yTop, width, yBottom - yTop));
    }

    /// <summary>
    /// Draws a 2px wide vertical insertion-caret at the drag-drop target — Feature B.
    /// </summary>
    private void DrawDragDropCaret(DrawingContext dc, int firstVisLine, double codeX)
    {
        if (_textDragDrop.Phase != DragPhase.Dragging) return;

        int dropLine = _textDragDrop.DropLine;
        int dropCol  = _textDragDrop.DropCol;

        if (dropLine < firstVisLine || dropLine > firstVisLine + _visibleLineCount) return;

        double y = (dropLine - firstVisLine) * _lineHeight;
        double x = codeX + dropCol * _charWidth;

        var caretBrush = GetBrush("TE_DragCaret");
        dc.DrawRectangle(caretBrush, null, new Rect(x - 1, y, 2, _lineHeight));
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

        var lines = _vm.Lines;

        if (_isWordWrapEnabled && _wrapOffsets.Length > 0)
        {
            // ---- Word-wrap rendering path ----
            int firstVisRow = _firstVisibleLine;
            int lastVisRow  = firstVisRow + _visibleLineCount;
            double codeX    = LineNumberColumnWidth + LeftMargin;

            var (firstLogLine, _) = VisualRowToLogical(Math.Max(0, firstVisRow));
            int currentVisRow = _wrapOffsets[firstLogLine];

            for (int li = firstLogLine; li < lines.Count && currentVisRow <= lastVisRow; li++)
            {
                var line     = lines[li];
                int rowCount = li < _wrapHeights.Length ? _wrapHeights[li] : 1;
                var spans    = _vm.GetHighlightedSpans(li);

                for (int sr = 0; sr < rowCount; sr++, currentVisRow++)
                {
                    if (currentVisRow > lastVisRow) break;
                    if (currentVisRow < firstVisRow) continue;

                    double y = (currentVisRow - firstVisRow) * _lineHeight;

                    // Line number label on the first sub-row only
                    if (sr == 0)
                    {
                        var lnText = BuildFormattedText((li + 1).ToString(), GetBrush("TE_LineNumberForeground"));
                        double lnX = LineNumberColumnWidth - lnText.Width - LineNumberPadding;
                        dc.DrawText(lnText, new Point(lnX, y + (_lineHeight - lnText.Height) / 2));
                    }

                    if (string.IsNullOrEmpty(line)) continue;

                    int startChar = VisualColToCharIndex(line, sr * _charsPerRow);
                    if (startChar >= line.Length) continue;
                    int endChar = VisualColToCharIndex(line, (sr + 1) * _charsPerRow);
                    var subLine = line.Substring(startChar, endChar - startChar);

                    if (spans.Count > 0)
                        RenderSubLineHighlighted(dc, spans, subLine, startChar, endChar, codeX, y);
                    else
                    {
                        var ft = BuildFormattedText(subLine, GetBrush("TE_Foreground"));
                        dc.DrawText(ft, new Point(codeX, y));
                    }
                }
            }
            return;
        }

        // ---- Normal (no-wrap) rendering path ----
        {
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
    }

    // -----------------------------------------------------------------------
    // Whitespace markers (dots for spaces, arrows for tabs)
    // -----------------------------------------------------------------------

    private static readonly char WsSpaceChar = '\u00B7'; // middle dot ·
    private static readonly char WsTabChar   = '\u2192'; // right arrow →

    private void DrawWhitespaceMarkers()
    {
        using var dc = _whitespaceVisual.RenderOpen();

        if (WhitespaceDisplayMode == WhitespaceMode.None) return;
        if (_vm is null || _lineHeight <= 0) return;

        bool selOnly = WhitespaceDisplayMode == WhitespaceMode.Selection;
        if (selOnly && !_vm.HasSelection) return;

        var markerBrush = GetBrush("CE_WhitespaceMarker")
                          ?? new SolidColorBrush(Color.FromArgb(0x50, 0x80, 0x80, 0x80));

        int firstLine = Math.Max(0, _firstVisibleLine);
        int lastLine  = Math.Min(_vm.Lines.Count - 1, firstLine + _visibleLineCount);
        double codeX  = LineNumberColumnWidth + LeftMargin - _horizontalOffset;

        // Selection range
        int selStartLine = 0, selStartCol = 0, selEndLine = 0, selEndCol = 0;
        if (selOnly)
            GetNormalizedSelection(out selStartLine, out selStartCol, out selEndLine, out selEndCol);

        for (int li = firstLine; li <= lastLine; li++)
        {
            var line = _vm.Lines[li];
            if (string.IsNullOrEmpty(line)) continue;

            int colStart = 0, colEnd = line.Length;
            if (selOnly)
            {
                if (li < selStartLine || li > selEndLine) continue;
                colStart = (li == selStartLine) ? selStartCol : 0;
                colEnd   = (li == selEndLine)   ? selEndCol   : line.Length;
            }

            double y = (li - firstLine) * _lineHeight;
            DrawWhitespaceForLine(dc, line, codeX, y, colStart, colEnd, markerBrush);
        }
    }

    private void DrawWhitespaceForLine(
        DrawingContext dc, string line, double codeX, double y,
        int colStart, int colEnd, Brush brush)
    {
        double x = codeX;
        for (int i = 0; i < line.Length && i < colEnd; i++)
        {
            char c = line[i];
            double advance = c == '\t' ? _charWidth * 4 : _charWidth;

            if (i >= colStart && (c == ' ' || c == '\t'))
            {
                char marker = c == ' ' ? WsSpaceChar : WsTabChar;
                var ft = BuildFormattedText(marker.ToString(), brush);
                dc.DrawText(ft, new Point(x, y + (_lineHeight - ft.Height) / 2));
            }
            x += advance;
        }
    }

    private void GetNormalizedSelection(
        out int startLine, out int startCol, out int endLine, out int endCol)
    {
        int aL = _vm!.SelectionAnchorLine, aC = _vm.SelectionAnchorColumn;
        int cL = _vm.CaretLine,            cC = _vm.CaretColumn;
        bool anchorFirst = aL < cL || (aL == cL && aC <= cC);
        startLine = anchorFirst ? aL : cL;
        startCol  = anchorFirst ? aC : cC;
        endLine   = anchorFirst ? cL : aL;
        endCol    = anchorFirst ? cC : aC;
    }

    /// <summary>
    /// Renders a sub-line segment (one visual wrap row) with syntax highlighting.
    /// Clips each span to [<paramref name="startCol"/>, <paramref name="endCol"/>)
    /// and adjusts X positions relative to the start of the sub-line.
    /// </summary>
    private void RenderSubLineHighlighted(DrawingContext dc, IReadOnlyList<ColoredSpan> spans,
        string subLine, int startCol, int endCol, double codeX, double y)
    {
        var defaultBrush = GetBrush("TE_Foreground");
        int pos = startCol; // tracks progress along the original logical line

        foreach (var span in spans)
        {
            int spanEnd = span.Start + span.Length;
            if (spanEnd <= startCol) continue; // before sub-line
            if (span.Start >= endCol) break;    // after sub-line

            // Unstyled gap before this span (clipped to sub-line range)
            int rawStart = Math.Max(pos, startCol);
            int rawEnd   = Math.Min(span.Start, endCol);
            if (rawEnd > rawStart)
            {
                var raw = subLine.Substring(rawStart - startCol, rawEnd - rawStart);
                var ft  = BuildFormattedText(raw, defaultBrush);
                dc.DrawText(ft, new Point(codeX + (rawStart - startCol) * _charWidth, y));
            }

            // Styled portion (clipped)
            int styledStart = Math.Max(span.Start, startCol);
            int styledEnd   = Math.Min(spanEnd, endCol);
            if (styledEnd > styledStart)
            {
                var text = subLine.Substring(styledStart - startCol, styledEnd - styledStart);
                var ft   = BuildFormattedText(text, GetBrush(span.ColorKey, defaultBrush));
                dc.DrawText(ft, new Point(codeX + (styledStart - startCol) * _charWidth, y));
            }

            pos = Math.Max(pos, spanEnd);
        }

        // Remaining unstyled tail
        int tailStart = Math.Max(pos, startCol);
        if (tailStart < endCol)
        {
            var tail = subLine.Substring(tailStart - startCol);
            if (!string.IsNullOrEmpty(tail))
            {
                var ft = BuildFormattedText(tail, defaultBrush);
                dc.DrawText(ft, new Point(codeX + (tailStart - startCol) * _charWidth, y));
            }
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

        double x, y;
        if (_isWordWrapEnabled && _wrapOffsets.Length > caretLine && _charsPerRow > 0)
        {
            string caretLineText = _vm.GetLine(caretLine);
            int caretVisColAbs   = CharIndexToVisualCol(caretLineText, caretCol);
            int caretVisRow      = _wrapOffsets[caretLine] + caretVisColAbs / _charsPerRow;
            int caretVisCol      = caretVisColAbs % _charsPerRow;
            if (caretVisRow < _firstVisibleLine || caretVisRow > _firstVisibleLine + _visibleLineCount)
                return;
            y = (caretVisRow - _firstVisibleLine) * _lineHeight;
            x = LineNumberColumnWidth + LeftMargin + caretVisCol * _charWidth;
        }
        else
        {
            if (caretLine < _firstVisibleLine || caretLine > _firstVisibleLine + _visibleLineCount)
                return;
            y = (caretLine - _firstVisibleLine) * _lineHeight;
            x = LineNumberColumnWidth + LeftMargin + caretCol * _charWidth - _horizontalOffset;
        }

        var pen = new Pen(GetBrush("TE_Foreground"), 1.5);
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
        // Word wrap: keep map in sync with content changes before rendering.
        if (_isWordWrapEnabled) RebuildWrapMap();
        UpdateBackground();
        UpdateTextContent();
        DrawCursor();
        // Schedule background highlighting for visible + buffer range (P1-TE-06)
        int hlFirst = _firstVisibleLine;
        int hlLast  = hlFirst + _visibleLineCount;
        if (_isWordWrapEnabled && _wrapOffsets.Length > 0)
        {
            hlFirst = VisualRowToLogical(Math.Max(0, hlFirst)).logLine;
            hlLast  = VisualRowToLogical(Math.Min(_totalVisualRows - 1, hlLast)).logLine;
        }
        _vm?.ScheduleHighlightAsync(hlFirst, hlLast);
    }

    /// <summary>
    /// Called on the UI thread when the background highlight pipeline completes a range.
    /// Invalidates the FormattedText cache for those lines so the next render picks up
    /// the new highlight spans, then queues a full render at Background priority.
    /// </summary>
    private void OnHighlightsComputed(int firstLine, int lastLine)
    {
        for (int i = firstLine; i <= lastLine; i++)
            _lineRenderCache.Remove(i);
        QueueFullRender();
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
        _panMode.HandleLostFocus();
        StopCursorBlink();
        UpdateBackground();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Pan mode: Escape exits before any other key handling
        if (_panMode.HandleKeyDown(e)) return;

        if (_vm is null) return;
        base.OnKeyDown(e);

        var ctrl  = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

        switch (e.Key)
        {
            // Feature A/B/C: Escape clears rect selection, cancels drag, or clears text selection.
            case Key.Escape:
                if (!_rectSelection.IsEmpty)
                {
                    _rectSelection.Clear();
                    _isRectSelecting = false;
                    UpdateBackground();
                    e.Handled = true;
                }
                else if (_textDragDrop.Phase != DragPhase.None)
                {
                    if (_textDragDrop.Phase == DragPhase.Dragging) ReleaseMouseCapture();
                    Cursor = Cursors.IBeam;
                    // Restore original selection.
                    if (_vm is not null)
                    {
                        _vm.SelectionAnchorLine   = _textDragDrop.SnapshotStartLine;
                        _vm.SelectionAnchorColumn = _textDragDrop.SnapshotStartCol;
                        _vm.CaretLine   = _textDragDrop.SnapshotEndLine;
                        _vm.CaretColumn = _textDragDrop.SnapshotEndCol;
                    }
                    _textDragDrop.Reset();
                    UpdateBackground();
                    e.Handled = true;
                }
                else if (_vm.HasSelection)
                {
                    // Feature C: annuler la sélection de texte normale.
                    _vm.ClearSelection();
                    UpdateBackground();
                    e.Handled = true;
                }
                break;

            // -- Zoom shortcuts (P2-01) -----------------------------------
            case Key.OemPlus  when ctrl:
            case Key.Add      when ctrl:
                ZoomLevel = Math.Round(ZoomLevel + 0.1, 1);
                e.Handled = true; break;
            case Key.OemMinus when ctrl:
            case Key.Subtract when ctrl:
                ZoomLevel = Math.Round(ZoomLevel - 0.1, 1);
                e.Handled = true; break;
            case Key.D0 when ctrl:
            case Key.NumPad0 when ctrl:
                ZoomLevel = 1.0;
                e.Handled = true; break;

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
                if (ctrl) { MoveWordLeft(shift); }
                else if (_vm.CaretColumn > 0) _vm.CaretColumn--;
                else if (_vm.CaretLine > 0) { _vm.CaretLine--; _vm.CaretColumn = _vm.GetLine(_vm.CaretLine).Length; }
                if (!shift) _vm.ClearSelection();
                e.Handled = true; break;
            case Key.Right:
                BeginSelectionIfShift(shift);
                var curLine = _vm.GetLine(_vm.CaretLine);
                if (ctrl) { MoveWordRight(shift); }
                else if (_vm.CaretColumn < curLine.Length) _vm.CaretColumn++;
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
            case Key.Z when ctrl && shift:
                _vm.Redo(); ScrollIntoView(_vm.CaretLine);
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
        // Middle-click toggles pan mode; any other click while active exits it.
        if (_panMode.HandleMouseDown(e)) return;

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

        var pos         = e.GetPosition(this);
        var (line, col) = HitTestPosition(pos);

        // Double-click: select word
        if (e.ClickCount == 2 && e.ChangedButton == MouseButton.Left)
        {
            _vm.CaretLine   = line;
            _vm.CaretColumn = col;
            SelectWordAtCaret();
            e.Handled = true;
            return;
        }

        bool altDown = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);

        // Feature A: Alt+LeftClick → start rectangular selection.
        if (altDown && e.ChangedButton == MouseButton.Left && e.ClickCount == 1)
        {
            _isDragging = false;
            _vm.ClearSelection();
            if (!_rectSelection.IsEmpty) _rectSelection.Clear();
            _rectSelection.Begin(line, col);
            _isRectSelecting = true;
            CaptureMouse();
            UpdateBackground();
            e.Handled = true;
            return;
        }

        // Any non-Alt, single click: check for rect-block drag BEFORE clearing the rect.
        if (!_rectSelection.IsEmpty && e.ClickCount == 1
            && IsInsideRectBlock(line, col))
        {
            // Click inside the active rect block → start potential rect drag-to-move.
            _textDragDrop.Phase             = DragPhase.Pending;
            _textDragDrop.ClickPixel        = pos;
            _textDragDrop.ClickedLine       = line;
            _textDragDrop.ClickedCol        = col;
            _textDragDrop.SnapshotStartLine = _rectSelection.TopLine;
            _textDragDrop.SnapshotStartCol  = _rectSelection.LeftColumn;
            _textDragDrop.SnapshotEndLine   = _rectSelection.BottomLine;
            _textDragDrop.SnapshotEndCol    = _rectSelection.RightColumn;
            _isRectDrag = true;
            e.Handled   = true;
            return;
        }

        // Non-Alt click with no rect-drag → clear rectangular selection.
        if (!_rectSelection.IsEmpty)
        {
            _rectSelection.Clear();
            _isRectSelecting = false;
        }
        _isRectDrag = false;

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
            // Feature B: click inside existing text selection → potential drag-to-move.
            if (!shift && e.ChangedButton == MouseButton.Left
                && _vm.HasSelection && IsInsideSelection(line, col))
            {
                _vm.NormalizeSelection(out int sl, out int sc, out int el, out int ec);
                _textDragDrop.Phase             = DragPhase.Pending;
                _textDragDrop.ClickPixel        = pos;
                _textDragDrop.ClickedLine       = line;
                _textDragDrop.ClickedCol        = col;
                _textDragDrop.SnapshotStartLine = sl;
                _textDragDrop.SnapshotStartCol  = sc;
                _textDragDrop.SnapshotEndLine   = el;
                _textDragDrop.SnapshotEndCol    = ec;
                e.Handled = true;
                return;
            }

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

    /// <summary>Returns true if (line, col) lies within the VM's current selection.</summary>
    private bool IsInsideSelection(int line, int col)
    {
        if (_vm is null || !_vm.HasSelection) return false;
        _vm.NormalizeSelection(out int sl, out int sc, out int el, out int ec);
        if (line < sl || line > el) return false;
        if (line == sl && col < sc) return false;
        if (line == el && col > ec) return false;
        return true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        // In pan mode: update directional cursor; suppress normal hover logic.
        if (_panMode.HandleMouseMove(e)) return;

        if (_vm is null) return;

        // Feature A: extend rectangular selection.
        if (_isRectSelecting && e.LeftButton == MouseButtonState.Pressed)
        {
            var rectPos = e.GetPosition(this);

            // Auto-scroll: raise event so the parent ScrollViewer scrolls (FirstVisibleLine
            // is driven by ScrollChanged — setting it directly would be immediately overridden).
            if (rectPos.Y < 0)
                DragScrollRequested?.Invoke(this, -1);
            else if (rectPos.Y > ActualHeight)
                DragScrollRequested?.Invoke(this, 1);

            var clampedRectPos   = new Point(rectPos.X, Math.Clamp(rectPos.Y, 0, ActualHeight - 1));
            var (mLine, mCol)    = HitTestPosition(clampedRectPos);
            _rectSelection.Extend(mLine, mCol);

            long rectNow = System.Diagnostics.Stopwatch.GetTimestamp();
            if (rectNow - _lastDragRenderTick >= DragThrottleTicks)
            {
                _lastDragRenderTick = rectNow;
                UpdateBackground();
            }
            return;
        }

        // Feature B: handle drag-pending or drag-in-progress.
        if (_textDragDrop.Phase != DragPhase.None && e.LeftButton == MouseButtonState.Pressed)
        {
            var ddPos        = e.GetPosition(this);
            var (dLine, dCol) = HitTestPosition(ddPos);

            if (_textDragDrop.Phase == DragPhase.Pending && _textDragDrop.HasMovedBeyondThreshold(ddPos))
            {
                _textDragDrop.Phase = DragPhase.Dragging;
                CaptureMouse();
                Cursor = Cursors.SizeAll;
            }

            if (_textDragDrop.Phase == DragPhase.Dragging)
            {
                _textDragDrop.DropLine = dLine;
                _textDragDrop.DropCol  = dCol;

                long ddNow = System.Diagnostics.Stopwatch.GetTimestamp();
                if (ddNow - _lastDragRenderTick >= DragThrottleTicks)
                {
                    _lastDragRenderTick = ddNow;
                    UpdateBackground();
                }
            }
            return;
        }

        // Update cursor: Arrow over line-number gutter, IBeam over text area.
        Cursor = e.GetPosition(this).X < LineNumberColumnWidth ? Cursors.Arrow : Cursors.IBeam;

        if (!_isDragging || e.LeftButton != MouseButtonState.Pressed) return;

        // Auto-scroll: raise event so the parent ScrollViewer scrolls (FirstVisibleLine
        // is driven by ScrollChanged — setting it directly would be immediately overridden).
        var rawPos = e.GetPosition(this);
        if (rawPos.Y < 0)
            DragScrollRequested?.Invoke(this, -1);
        else if (rawPos.Y > ActualHeight)
            DragScrollRequested?.Invoke(this, 1);

        // 60 Hz gate: skip if last drag-render was < 16.7 ms ago (P1-TE-02)
        long now = System.Diagnostics.Stopwatch.GetTimestamp();
        if (now - _lastDragRenderTick < DragThrottleTicks) return;
        _lastDragRenderTick = now;

        // Set anchor on first movement after button-down
        if (!_vm.HasSelection)
        {
            _vm.SelectionAnchorLine   = _vm.CaretLine;
            _vm.SelectionAnchorColumn = _vm.CaretColumn;
        }

        var pos          = new Point(rawPos.X, Math.Clamp(rawPos.Y, 0, ActualHeight - 1));
        var (line, col)  = HitTestPosition(pos);

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

        // Feature A: end rectangular selection drag.
        if (_isRectSelecting)
        {
            _isRectSelecting = false;
            ReleaseMouseCapture();
            UpdateBackground();
            return;
        }

        // Feature B: commit or cancel text drag-to-move.
        if (_textDragDrop.Phase != DragPhase.None)
        {
            ReleaseMouseCapture();
            Cursor = Cursors.IBeam;

            if (_textDragDrop.Phase == DragPhase.Dragging)
            {
                if (_isRectDrag) CommitRectDrop();
                else             CommitTextDrop();
            }
            else
            {
                // Pending phase: simple click inside selection — clear selection, move caret.
                if (_vm is not null)
                {
                    _vm.ClearSelection();
                    _rectSelection.Clear();
                    _vm.CaretLine   = _textDragDrop.ClickedLine;
                    _vm.CaretColumn = _textDragDrop.ClickedCol;
                }
            }

            _isRectDrag = false;
            _textDragDrop.Reset();
            _cursorVisible = true;
            DrawCursor();
            UpdateBackground();
            return;
        }

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

    /// <summary>
    /// Commits the text drag-to-move: deletes source selection, adjusts the drop position,
    /// and inserts at the target. Feature B.
    /// </summary>
    private void CommitTextDrop()
    {
        if (_vm is null || _vm.IsReadOnly) return;

        int dropLine = _textDragDrop.DropLine;
        int dropCol  = _textDragDrop.DropCol;
        int sl       = _textDragDrop.SnapshotStartLine;
        int sc       = _textDragDrop.SnapshotStartCol;
        int el       = _textDragDrop.SnapshotEndLine;
        int ec       = _textDragDrop.SnapshotEndCol;

        // Drop inside original selection → cancel.
        if (_textDragDrop.IsDropInsideSnapshot(dropLine, dropCol))
        {
            _vm.SelectionAnchorLine   = sl;
            _vm.SelectionAnchorColumn = sc;
            _vm.CaretLine   = el;
            _vm.CaretColumn = ec;
            return;
        }

        // Restore selection to snapshot so GetSelectedText works correctly.
        _vm.SelectionAnchorLine   = sl;
        _vm.SelectionAnchorColumn = sc;
        _vm.CaretLine   = el;
        _vm.CaretColumn = ec;

        string movedText  = _vm.GetSelectedText();
        bool   dropBefore = dropLine < sl || (dropLine == sl && dropCol <= sc);

        _vm.DeleteSelectedText();

        // Adjust insertion point for lines removed above the drop.
        int insertLine = dropLine;
        int insertCol  = dropCol;
        if (!dropBefore)
        {
            int removedLines = el - sl;
            insertLine -= removedLines;
            if (dropLine == el) // on the line where deletion ended → fix column
                insertCol = sc + (dropCol - ec);
        }

        _vm.CaretLine   = Math.Max(0, insertLine);
        _vm.CaretColumn = Math.Max(0, insertCol);
        _vm.ClearSelection();
        _vm.InsertText(movedText);
    }

    /// <summary>
    /// Returns true when (line, col) falls inside the active rectangular selection block.
    /// </summary>
    private bool IsInsideRectBlock(int line, int col)
        => !_rectSelection.IsEmpty
           && line >= _rectSelection.TopLine    && line <= _rectSelection.BottomLine
           && col  >= _rectSelection.LeftColumn && col  <= _rectSelection.RightColumn;

    /// <summary>
    /// Commits a rect-block drag-to-move: extracts the column block, deletes it, then
    /// inserts each row at the drop column. Feature A+B combined.
    /// </summary>
    private void CommitRectDrop()
    {
        if (_vm is null || _vm.IsReadOnly) { _isRectDrag = false; _textDragDrop.Reset(); return; }

        int topLine    = _rectSelection.TopLine;
        int bottomLine = _rectSelection.BottomLine;
        int leftCol    = _rectSelection.LeftColumn;
        int rightCol   = _rectSelection.RightColumn;
        int blockWidth = rightCol - leftCol;
        int blockHeight= bottomLine - topLine + 1;

        int dropLine   = _textDragDrop.DropLine;
        int dropCol    = _textDragDrop.DropCol;

        // Drop inside the original block → no-op.
        if (dropLine >= topLine && dropLine <= bottomLine
            && dropCol >= leftCol && dropCol <= rightCol)
        {
            _isRectDrag = false;
            _textDragDrop.Reset();
            return;
        }

        // Snapshot block text before deletion.
        var lineList = new List<string>();
        for (int i = 0; i < _vm.LineCount; i++) lineList.Add(_vm.GetLine(i));
        string blockText = _rectSelection.ExtractText(lineList);
        string[] blockLines = blockText.Split('\n');

        // Delete the source block (bottom-to-top to preserve line indices).
        DeleteRectSelection();

        // Adjust drop column when drop is on an affected line and after the deleted block.
        if (dropLine >= topLine && dropLine <= bottomLine && dropCol > rightCol)
            dropCol = Math.Max(leftCol, dropCol - blockWidth);

        // Insert each block row at the drop column.
        for (int i = 0; i < blockHeight && (dropLine + i) < _vm.LineCount; i++)
        {
            string lineContent = i < blockLines.Length ? blockLines[i] : string.Empty;
            if (string.IsNullOrEmpty(lineContent)) continue;

            int targetLine = dropLine + i;
            int targetCol  = Math.Min(dropCol, _vm.GetLine(targetLine).Length);
            _vm.CaretLine   = targetLine;
            _vm.CaretColumn = targetCol;
            _vm.InsertText(lineContent);
        }

        // Reposition rect selection at the new block location.
        int newBottom = Math.Min(dropLine + blockHeight - 1, _vm.LineCount - 1);
        _rectSelection.Begin(dropLine, dropCol);
        _rectSelection.Extend(newBottom, dropCol + blockWidth);

        _vm.CaretLine   = dropLine;
        _vm.CaretColumn = dropCol;
        _isRectDrag     = false;
        _textDragDrop.Reset();
        UpdateBackground();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        // Ctrl+Wheel → zoom (P2-01)
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            double step = e.Delta > 0 ? 0.1 : -0.1;
            ZoomLevel = Math.Round(ZoomLevel + step, 1);
            e.Handled = true;
            return;
        }

        // Use MouseWheelSpeed (lines per notch) — same model as HexEditor.
        int speed = MouseWheelSpeed == MouseWheelSpeed.System
            ? SystemParameters.WheelScrollLines
            : (int)MouseWheelSpeed;
        int delta = Math.Sign(e.Delta) * speed;
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
        var baseSize = TryFindResource("TE_FontSize") is double fs ? fs : DefaultFontSize;
        var zoom     = ZoomLevel; // P2-01

        // Apply zoom to the effective em-size.
        var size = baseSize * zoom;

        if (_typeface is not null && _emSize == size
            && _cachedFontSize == baseSize && _cachedZoom == zoom
            && Equals(_cachedTypeface, _typeface))
            return;

        _typeface       = new Typeface(font, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        _emSize         = size;
        _cachedFontSize = baseSize;
        _cachedZoom     = zoom;     // P2-01
        _cachedTypeface = _typeface;
        _brushCache.Clear();
        _lineRenderCache.Clear(); // font/zoom changed — all cached FormattedText is stale

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
        // Feature A: rect selection takes priority.
        if (!_rectSelection.IsEmpty) { CopyRectSelection(); return; }
        if (_vm is null || !_vm.HasSelection) return;
        var text = _vm.GetSelectedText();
        if (!string.IsNullOrEmpty(text)) Clipboard.SetText(text);
    }

    private void ViewportCut()
    {
        // Feature A: rect selection takes priority.
        if (!_rectSelection.IsEmpty) { CutRectSelection(); return; }
        if (_vm is null || !_vm.HasSelection || _vm.IsReadOnly) return;
        ViewportCopy();
        _vm.DeleteSelectedText();
        // Lines/LineCount PropertyChanged → QueueFullRender()
    }

    private void ViewportPaste()
    {
        // Paste is disabled when a rectangular selection is active.
        if (!_rectSelection.IsEmpty) return;
        if (_vm is null || _vm.IsReadOnly || !Clipboard.ContainsText()) return;
        _vm.InsertText(Clipboard.GetText());
        // Lines/LineCount PropertyChanged → QueueFullRender()
    }

    internal void CopyRectSelection()
    {
        if (_rectSelection.IsEmpty || _vm is null) return;
        string text = _rectSelection.ExtractText(_vm.Lines);
        if (!string.IsNullOrEmpty(text))
        {
            try { Clipboard.SetText(text); }
            catch { /* Silently ignore clipboard errors */ }
        }
    }

    internal void CutRectSelection()
    {
        if (_rectSelection.IsEmpty || _vm is null || _vm.IsReadOnly) return;
        CopyRectSelection();
        DeleteRectSelection();
    }

    internal void DeleteRectSelection()
    {
        if (_rectSelection.IsEmpty || _vm is null || _vm.IsReadOnly) return;

        var (left, right) = _rectSelection.GetColumnRange();

        // Wrap all per-line deletions in a single transaction so Ctrl+Z undoes the entire rect-delete atomically.
        using (_vm.BeginUndoTransaction("Delete Rectangular Selection"))
        {
            // Iterate bottom-to-top so line indices stay stable.
            for (int li = _rectSelection.BottomLine; li >= _rectSelection.TopLine; li--)
            {
                if (li >= _vm.LineCount) continue;
                var   line  = _vm.GetLine(li);
                int   safeL = Math.Min(left,  line.Length);
                int   safeR = Math.Min(right, line.Length);
                if (safeR <= safeL) continue;

                // Position anchor+caret on this line's column range, then delete.
                _vm.SelectionAnchorLine   = li;
                _vm.SelectionAnchorColumn = safeL;
                _vm.CaretLine   = li;
                _vm.CaretColumn = safeR;
                _vm.DeleteSelectedText();
            }
        }

        _vm.CaretLine   = _rectSelection.TopLine;
        _vm.CaretColumn = _rectSelection.LeftColumn;
        _rectSelection.Clear();
        UpdateBackground();
    }

    // -----------------------------------------------------------------------
    // Word selection (double-click)
    // -----------------------------------------------------------------------

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

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

    private void MoveWordLeft(bool shift)
    {
        if (_vm is null) return;
        string line = _vm.GetLine(_vm.CaretLine);
        int col = _vm.CaretColumn;

        // Skip non-word chars to the left
        while (col > 0 && !IsWordChar(line[col - 1])) col--;
        // Skip word chars to the left
        while (col > 0 && IsWordChar(line[col - 1])) col--;

        if (col == _vm.CaretColumn && _vm.CaretLine > 0)
        {
            _vm.CaretLine--;
            _vm.CaretColumn = _vm.GetLine(_vm.CaretLine).Length;
        }
        else
        {
            _vm.CaretColumn = col;
        }

        if (!shift) _vm.ClearSelection();
        ScrollIntoView(_vm.CaretLine);
    }

    private void MoveWordRight(bool shift)
    {
        if (_vm is null) return;
        string line = _vm.GetLine(_vm.CaretLine);
        int col = _vm.CaretColumn;

        // Skip word chars to the right
        while (col < line.Length && IsWordChar(line[col]))  col++;
        // Skip non-word chars to the right
        while (col < line.Length && !IsWordChar(line[col])) col++;

        if (col == _vm.CaretColumn && _vm.CaretLine < _vm.LineCount - 1)
        {
            _vm.CaretLine++;
            _vm.CaretColumn = 0;
        }
        else
        {
            _vm.CaretColumn = col;
        }

        if (!shift) _vm.ClearSelection();
        ScrollIntoView(_vm.CaretLine);
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

    // ── Zoom ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Scales the displayed text (0.5 = 50 %, 1.0 = 100 %, 4.0 = 400 %).
    /// <c>Ctrl+Wheel</c>, <c>Ctrl+=</c>, <c>Ctrl+-</c>, and <c>Ctrl+0</c> adjust this value.
    /// Mirrors <c>CodeEditor.ZoomLevel</c> for API consistency.
    /// </summary>
    public static readonly DependencyProperty ZoomLevelProperty =
        DependencyProperty.Register(
            nameof(ZoomLevel),
            typeof(double),
            typeof(TextViewport),
            new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsMeasure,
                (d, _) =>
                {
                    var vp = (TextViewport)d;
                    vp._lineRenderCache.Clear(); // zoom changes emSize → cached FormattedText is stale
                    vp.QueueFullRender();
                    vp.ZoomLevelChanged?.Invoke(vp, vp.ZoomLevel);
                }));

    /// <summary>Gets or sets the zoom level (0.5–4.0).</summary>
    public double ZoomLevel
    {
        get => (double)GetValue(ZoomLevelProperty);
        set => SetValue(ZoomLevelProperty, Math.Max(0.5, Math.Min(4.0, value)));
    }

    /// <summary>Raised when <see cref="ZoomLevel"/> changes (for status-bar display).</summary>
    public event EventHandler<double>? ZoomLevelChanged;

    /// <summary>True while a left-button drag-selection (normal or rectangular) is active.</summary>
    public bool IsDraggingSelection => _isDragging || _isRectSelecting;

    /// <summary>
    /// Extends the selection endpoint to the top or bottom visible edge.
    /// Called by the parent after a programmatic scroll so the selection tracks the new scroll position.
    /// <paramref name="direction"/> is -1 (scrolled up → top edge) or +1 (scrolled down → bottom edge).
    /// <paramref name="visibleHeight"/> is the ScrollViewer's ViewportHeight (the true visible area height).
    /// </summary>
    internal void ExtendSelectionToEdge(int direction, double visibleHeight)
    {
        if (_vm is null || !_isDragging) return;

        // HitTestPosition uses visible-area Y (0 = top of visible area).
        double edgeY = direction < 0 ? 0 : visibleHeight - 1;
        var edgePos  = new Point(LineNumberColumnWidth, edgeY);
        var (line, col) = HitTestPosition(edgePos);

        if (line == _vm.CaretLine && col == _vm.CaretColumn) return;

        _suppressVmNotify = true;
        _vm.CaretLine   = line;
        _vm.CaretColumn = col;
        _suppressVmNotify = false;

        UpdateBackground();
        DrawCursor();
    }

    /// <summary>
    /// Raised during a drag-selection when the mouse exits the viewport bounds and the
    /// parent ScrollViewer should scroll. The argument is the line delta (-1 = up, +1 = down).
    /// </summary>
    public event EventHandler<int>? DragScrollRequested;

    // ─────────────────────────────────────────────────────────────────────────

    // ── MouseWheel scroll speed ──────────────────────────────────────────────

    /// <summary>
    /// Lines scrolled per mouse-wheel notch — same enum and behaviour as HexEditor.
    /// <c>System</c> uses <see cref="System.Windows.SystemParameters.WheelScrollLines"/>.
    /// </summary>
    public static readonly DependencyProperty MouseWheelSpeedProperty =
        DependencyProperty.Register(
            nameof(MouseWheelSpeed),
            typeof(MouseWheelSpeed),
            typeof(TextViewport),
            new FrameworkPropertyMetadata(MouseWheelSpeed.System));

    /// <summary>Gets or sets the lines-per-notch scroll speed.</summary>
    public MouseWheelSpeed MouseWheelSpeed
    {
        get => (MouseWheelSpeed)GetValue(MouseWheelSpeedProperty);
        set => SetValue(MouseWheelSpeedProperty, value);
    }

    /// <summary>
    /// Kept for API compatibility — no longer used for vertical scroll.
    /// Use <see cref="MouseWheelSpeed"/> instead.
    /// </summary>
    public static readonly DependencyProperty ScrollSpeedMultiplierProperty =
        DependencyProperty.Register(
            nameof(ScrollSpeedMultiplier),
            typeof(double),
            typeof(TextViewport),
            new FrameworkPropertyMetadata(1.0));

    /// <inheritdoc cref="ScrollSpeedMultiplierProperty"/>
    public double ScrollSpeedMultiplier
    {
        get => (double)GetValue(ScrollSpeedMultiplierProperty);
        set => SetValue(ScrollSpeedMultiplierProperty, Math.Max(0.5, Math.Min(3.0, value)));
    }

    // ─────────────────────────────────────────────────────────────────────────

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
