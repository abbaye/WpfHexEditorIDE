// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Controls/DocumentCanvasRenderer.cs
// Description:
//     Overkill DrawingContext-based document renderer.
//     Replaces RichTextBox with a pure FrameworkElement+IScrollInfo
//     canvas that renders DocumentBlocks using FormattedText with
//     inline formatting runs, virtual scrolling, page-card visual
//     (page floating on gray canvas), page-break labels, block
//     hover/selection highlight, forensic badges, and zoom via
//     LayoutTransform. Zero WPF containers per block row.
// Architecture: FrameworkElement + IScrollInfo + frozen brush/pen cache.
// ==========================================================

using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WpfHexEditor.Editor.DocumentEditor.Core.Forensic;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;
using WpfHexEditor.Editor.DocumentEditor.Core.Options;
using WpfHexEditor.Editor.DocumentEditor.Rendering;
using WpfHexEditor.Editor.DocumentEditor.Services;
using WpfHexEditor.Editor.DocumentEditor.ViewModels;
using RenderMode = WpfHexEditor.Editor.DocumentEditor.Core.Options.DocumentRenderMode;

namespace WpfHexEditor.Editor.DocumentEditor.Controls;

/// <summary>
/// High-performance DrawingContext-based renderer for document blocks.
/// Implements <see cref="IScrollInfo"/> for virtual viewport scrolling.
/// </summary>
public sealed class DocumentCanvasRenderer : FrameworkElement, IScrollInfo
{
    // ── Constants ────────────────────────────────────────────────────────────

    private const double PageMarginH    = 56.0;   // left + right padding inside page
    private const double PageMarginV    = 40.0;   // top padding inside each page card
    private const double PageMarginVBot = 56.0;   // bottom padding inside each page card
    private const double PageCanvasPad  = 32.0;   // space between canvas edge and first/last page
    private const double PageShadowBlur = 12.0;
    private const double TableCellPad   = 6.0;

    // A4 portrait page card dimensions at 96 dpi (297mm × 210mm)
    private const double PageHeightPx   = 1122.0; // 297mm × (96 / 25.4)
    private const double PageGapPx      = 24.0;   // Dark canvas gap between page cards

    private const string BodyFontFamily = "Georgia";
    private const string UIFontFamily   = "Segoe UI";

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Raised when the user clicks a block.</summary>
    public event EventHandler<DocumentBlock?>? SelectedBlockChanged;

    /// <summary>Raised when a block is selected — host should show pop-toolbar.</summary>
    public event EventHandler<PopToolbarRequestedArgs>? PopToolbarRequested;

    /// <summary>Raised when caret or selection moves so the host can refresh format toggle states.</summary>
    public event EventHandler? SelectionFormatChanged;

    /// <summary>Raised when the vertical scroll offset changes; args carry (currentPage, totalPages).</summary>
    public event EventHandler<(int Current, int Total)>? PageChanged;

    // ── Fields ────────────────────────────────────────────────────────────────

    // Model
    private DocumentModel?           _model;
    private List<RenderBlock>        _blocks = [];
    private string                   _loadingMessage = "Loading…";
    private bool                     _isLoading  = true;
    private bool                     _isError    = false;

    // Caret staleness flag: set when blocks change, cleared after RebuildLayout
    private bool _caretFtDirty;

    // Interaction
    private int    _hoverIndex    = -1;
    private int    _selectedIndex = -1;
    private bool   _forensicMode  = false;

    // Layout cache
    private double       _totalHeight = 0;
    private double       _pageWidth   = 0;
    private double       _pageLeft    = 0;
    private double       _zoom        = 1.0;
    private List<double> _pageStarts  = [0.0]; // Absolute canvas Y of each page card's top
    private int          _pageCount   = 1;

    // IScrollInfo
    private ScrollViewer? _scrollOwner;
    private Vector        _offset;
    private Size          _viewport;
    private Size          _extent;
    private bool          _canHScroll = false;
    private bool          _canVScroll = true;

    // Brush / pen cache (frozen)
    private Brush? _canvasBg, _pageBg, _pageBorder, _fgBrush, _fgDimBrush;
    private Brush? _hoverBrush, _selBrush, _pageBreakBrush, _headingFg;
    private Brush? _kindChipBg, _kindChipFg, _forensicErrBrush, _forensicWarnBrush;
    private Brush? _pageNumFg, _tableBorderBrush, _imageFg;
    private Brush? _textSelBrush, _caretBrush, _findHighlightBrush;
    private Pen?   _pageCardPen, _pageBreakPen, _blockHoverPen, _tableGridPen;
    // Cached per-style brushes/pens (avoid per-render allocations)
    private Brush? _codeBgBrush, _errorBgBrush, _shadowBrush;
    private Pen?   _h1RulePen, _h2RulePen, _pageBorderPen;
    // Pre-built shadow brushes for DrawPageShadow (i=1..4, alpha = 0.08*i*255)
    private readonly Brush?[] _pageShadowBrushes = new Brush?[5]; // index 1-4

    // Phase 19 — find results
    private IReadOnlyList<DocumentSearchMatch>? _findResults;
    private int _findCursor;

    // Phase 20 — image cache: true LRU, capacity covers 64 real bitmaps + loading/error sentinels
    private const int ImageCacheMaxEntries = 64;
    private LruCache<string, BitmapImage?>? _imageCache;

    // ── Phase 12: Cursor + TextSelection ─────────────────────────────────────

    private TextCaret          _caret;
    private TextSelection      _selection  = new();
    private DispatcherTimer?   _blinkTimer;
    private bool               _caretVisible;
    private bool               _isDragging;

    // Dedicated DrawingVisual for the caret so blink only redraws a 2px rect
    private readonly DrawingVisual _caretVisual = new();

    // Visual-line cache: keyed by block index, cleared on RebuildLayout
    private Dictionary<int, IReadOnlyList<VisualLine>> _visualLineCache = [];
    private bool _lastMoveWasVertical;

    // Typeface cache
    private Typeface? _bodyFace, _bodyBoldFace, _bodyItalicFace, _bodyBoldItalicFace;
    private Typeface? _uiFace, _uiBoldFace, _monoFace;
    private double    _baseFontSize = 14.0;

    // ── Constructor ──────────────────────────────────────────────────────────

    // ── Visual children (caret layer on top of main render) ──────────────────

    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) => _caretVisual;

    // ── Constructor ──────────────────────────────────────────────────────────

    public DocumentCanvasRenderer()
    {
        ClipToBounds  = true;
        Focusable     = true;
        Cursor        = Cursors.IBeam;

        AddVisualChild(_caretVisual);

        MouseMove     += OnMouseMove;
        MouseLeave    += OnMouseLeave;
        MouseDown     += OnMouseDown;
        MouseUp       += OnMouseUp;
        SizeChanged   += OnSizeChanged;
        GotFocus          += OnGotFocus;
        LostKeyboardFocus += OnLostFocus;
        PreviewKeyDown    += OnPreviewKeyDown;
        PreviewTextInput  += OnPreviewTextInput;

        // Blink timer: only redraws the 2px caret visual, not the whole page
        _blinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _blinkTimer.Tick += (_, _) => { _caretVisible = !_caretVisible; RefreshCaretVisual(); };
    }

    protected override void OnVisualParentChanged(DependencyObject oldParent)
    {
        base.OnVisualParentChanged(oldParent);
        if (VisualParent is null)
            _blinkTimer?.Stop();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Currently selected block (null when nothing is selected).</summary>
    public DocumentBlock? SelectedBlock =>
        _selectedIndex >= 0 && _selectedIndex < _blocks.Count
            ? _blocks[_selectedIndex].Block : null;

    /// <summary>Binds the renderer to a document model and triggers layout.</summary>
    public void BindModel(DocumentModel model)
    {
        if (_model is not null)
            _model.BlocksChanged -= OnBlocksChanged;

        _model       = model;
        _isLoading   = false;
        _isError     = false;
        _model.BlocksChanged += OnBlocksChanged;

        InvalidateBrushCache();
        RebuildLayout();
    }

    /// <summary>Shows a loading placeholder.</summary>
    public void ShowLoading(string message = "Loading…")
    {
        _loadingMessage = message;
        _isLoading      = true;
        _isError        = false;
        _blocks.Clear();
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Shows an error placeholder.</summary>
    public void ShowError(string message)
    {
        _loadingMessage = $"⚠ {message}";
        _isLoading      = false;
        _isError        = true;
        _blocks.Clear();
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Scrolls to and selects the block covering the given binary offset.</summary>
    public void ScrollToOffset(long offset)
    {
        if (_model is null) return;
        var block = _model.BinaryMap.BlockAt(offset);
        if (block is not null) SelectBlock(block);
    }

    /// <summary>Scrolls to and selects a specific block.</summary>
    public void ScrollToBlock(DocumentBlock block) => SelectBlock(block);

    /// <summary>Toggles forensic badge display.</summary>
    public void SetForensicMode(bool enabled)
    {
        _forensicMode = enabled;
        InvalidateVisual();
    }

    /// <summary>Sets the zoom level (0.5–2.0). Applied via LayoutTransform by the parent.</summary>
    public void SetZoom(double zoom) => _zoom = Math.Clamp(zoom, 0.5, 2.0);

    /// <summary>
    /// Switches the render mode (Page / Draft / Outline) and triggers a full layout rebuild.
    /// </summary>
    public void SetRenderMode(RenderMode mode)
    {
        if (_renderMode == mode) return;
        _renderMode = mode;
        RebuildLayout();
    }

    // ── Read-Only / Render mode ───────────────────────────────────────────────

    private bool                 _isReadOnly      = false;
    private bool                 _showPageShadows = true;
    private Thickness            _pageMargin      = new(40);
    private DocumentPageSettings _pageSettings    = DocumentPageSettings.Default;
    private RenderMode           _renderMode      = RenderMode.Page;

    /// <summary>
    /// When true, all text input handlers are suppressed and cursor reverts to Arrow.
    /// Ctrl+C still works for read-only copy.
    /// </summary>
    public bool IsReadOnly
    {
        get => _isReadOnly;
        set
        {
            _isReadOnly = value;
            Cursor      = value ? Cursors.Arrow : Cursors.IBeam;
        }
    }

    /// <summary>Show A4/Letter page card shadows (Page mode). False in Draft mode.</summary>
    public bool ShowPageShadows
    {
        get => _showPageShadows;
        set { _showPageShadows = value; InvalidateVisual(); }
    }

    /// <summary>Page content margin in Page/Draft modes.</summary>
    public Thickness PageMargin
    {
        get => _pageMargin;
        set { _pageMargin = value; RebuildLayout(); InvalidateVisual(); }
    }

    /// <summary>
    /// Full page layout settings (size, orientation, margins, columns, header/footer, border).
    /// Setting this property triggers a full layout rebuild.
    /// </summary>
    public DocumentPageSettings PageSettings
    {
        get => _pageSettings;
        set
        {
            _pageSettings = value;
            InvalidateBrushCache();
            RebuildLayout();
        }
    }

    // ── IScrollInfo ───────────────────────────────────────────────────────────

    public bool CanHorizontallyScroll { get => _canHScroll; set => _canHScroll = value; }
    public bool CanVerticallyScroll   { get => _canVScroll; set => _canVScroll = value; }
    public double ExtentWidth  => _extent.Width;
    public double ExtentHeight => _extent.Height;
    public double ViewportWidth  => _viewport.Width;
    public double ViewportHeight => _viewport.Height;
    public double HorizontalOffset => _offset.X;
    public double VerticalOffset   => _offset.Y;
    public ScrollViewer? ScrollOwner { get => _scrollOwner; set => _scrollOwner = value; }

    public void LineUp()      => SetVerticalOffset(_offset.Y - 20);
    public void LineDown()    => SetVerticalOffset(_offset.Y + 20);
    public void PageUp()      => SetVerticalOffset(_offset.Y - _viewport.Height);
    public void PageDown()    => SetVerticalOffset(_offset.Y + _viewport.Height);
    public void MouseWheelUp()   => SetVerticalOffset(_offset.Y - 60);
    public void MouseWheelDown() => SetVerticalOffset(_offset.Y + 60);
    public void LineLeft()    => SetHorizontalOffset(_offset.X - 20);
    public void LineRight()   => SetHorizontalOffset(_offset.X + 20);
    public void PageLeft()    => SetHorizontalOffset(_offset.X - _viewport.Width);
    public void PageRight()   => SetHorizontalOffset(_offset.X + _viewport.Width);
    public void MouseWheelLeft()  => SetHorizontalOffset(_offset.X - 60);
    public void MouseWheelRight() => SetHorizontalOffset(_offset.X + 60);

    public void SetHorizontalOffset(double offset)
    {
        _offset.X = Math.Clamp(offset, 0, Math.Max(0, _extent.Width - _viewport.Width));
        _scrollOwner?.InvalidateScrollInfo();
        InvalidateVisual();
        RefreshCaretVisual();
    }

    public void SetVerticalOffset(double offset)
    {
        _offset.Y = Math.Clamp(offset, 0, Math.Max(0, _extent.Height - _viewport.Height));
        _scrollOwner?.InvalidateScrollInfo();
        InvalidateVisual();
        RefreshCaretVisual();
        FirePageChanged();
    }

    private int GetCurrentPage()
    {
        // Find the first page whose bottom edge is below the current viewport top.
        double viewTop = _offset.Y;
        for (int i = 0; i < _pageStarts.Count; i++)
        {
            double nextStart = i + 1 < _pageStarts.Count ? _pageStarts[i + 1] : double.MaxValue;
            if (nextStart > viewTop) return i + 1;
        }
        return _pageCount;
    }

    private void FirePageChanged() =>
        PageChanged?.Invoke(this, (GetCurrentPage(), _pageCount));

    public Rect MakeVisible(Visual visual, Rect rectangle) => rectangle;

    // ── Measure / Arrange ────────────────────────────────────────────────────

    protected override Size MeasureOverride(Size availableSize)
    {
        var vw = double.IsInfinity(availableSize.Width)  ? 800 : availableSize.Width;
        var vh = double.IsInfinity(availableSize.Height) ? 600 : availableSize.Height;
        return new Size(vw, vh);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _viewport = finalSize;
        RecalcPageGeometry(finalSize.Width);
        UpdateScrollExtent();
        _scrollOwner?.InvalidateScrollInfo();
        return finalSize;
    }

    // ── OnRender ─────────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        EnsureBrushCache();
        EnsureTypefaces();

        var vw = _viewport.Width;
        var vh = _viewport.Height;

        // ── 1. Canvas background ──────────────────────────────────────────
        dc.DrawRectangle(_canvasBg, null, new Rect(0, 0, vw, vh));

        if (_isLoading || _isError)
        {
            DrawLoadingOrError(dc, vw, vh);
            return;
        }

        // Dispatch to mode-specific renderer
        if (_renderMode == RenderMode.Draft)
        {
            OnRenderDraft(dc, vw, vh);
            return;
        }

        if (_renderMode == RenderMode.Outline)
        {
            OnRenderOutline(dc, vw, vh);
            return;
        }

        // ── Page mode (default) ───────────────────────────────────────────
        OnRenderPage(dc, vw, vh);
    }

    private void OnRenderPage(DrawingContext dc, double vw, double vh)
    {
        // ── 2. Page cards ─────────────────────────────────────────────────
        double pageH = _pageSettings.EffectivePageHeight;
        foreach (var pageStart in _pageStarts)
        {
            double cardTop = PageCanvasPad + pageStart - _offset.Y;
            if (cardTop + pageH < 0 || cardTop > vh) continue;

            var pageRect = new Rect(_pageLeft, cardTop, _pageWidth, pageH);
            DrawPageShadow(dc, pageRect);
            dc.DrawRectangle(_pageBg, _pageCardPen, pageRect);

            if (_pageSettings.HeaderEnabled)
            {
                double hSep = cardTop + _pageSettings.MarginTop + _pageSettings.HeaderHeightPx;
                dc.DrawLine(_pageBreakPen!,
                    new Point(_pageLeft + _pageSettings.MarginLeft, hSep),
                    new Point(_pageLeft + _pageWidth - _pageSettings.MarginRight, hSep));
            }

            if (_pageSettings.FooterEnabled)
            {
                double fSep = cardTop + pageH - _pageSettings.MarginBottom - _pageSettings.FooterHeightPx;
                dc.DrawLine(_pageBreakPen!,
                    new Point(_pageLeft + _pageSettings.MarginLeft, fSep),
                    new Point(_pageLeft + _pageWidth - _pageSettings.MarginRight, fSep));
            }

            if (_pageSettings.BorderStyle != DocumentPageBorderStyle.None)
                DrawPageBorder(dc, pageRect);
        }

        DrawBlocksInViewport(dc, vw, vh,
            contentX: _pageLeft + _pageSettings.MarginLeft,
            contentW: Math.Max(1, _pageWidth - _pageSettings.MarginLeft - _pageSettings.MarginRight));
    }

    /// <summary>
    /// Draft render: solid background, no page cards, compact margins, continuous flow.
    /// </summary>
    private void OnRenderDraft(DrawingContext dc, double vw, double vh)
    {
        const double DraftMarginH = 32.0;

        // Solid paper-white strip centered on the canvas (no card chrome)
        double stripW = Math.Max(400, _pageWidth);
        double stripX = (vw - stripW) / 2;
        var stripBrush = _pageBg ?? Brushes.White;
        dc.DrawRectangle(stripBrush, null, new Rect(stripX, 0, stripW, vh));

        // Subtle left/right border lines to delimit the content area
        if (_fgDimBrush is not null && _pageBreakPen is not null)
        {
            dc.DrawLine(_pageBreakPen, new Point(stripX, 0), new Point(stripX, vh));
            dc.DrawLine(_pageBreakPen, new Point(stripX + stripW, 0), new Point(stripX + stripW, vh));
        }

        DrawBlocksInViewport(dc, vw, vh,
            contentX: stripX + DraftMarginH,
            contentW: Math.Max(1, stripW - DraftMarginH * 2));
    }

    /// <summary>
    /// Outline render: white background, only headings drawn with level-based indentation.
    /// Non-heading blocks are invisible; paragraph text is shown as a dim placeholder line.
    /// </summary>
    private void OnRenderOutline(DrawingContext dc, double vw, double vh)
    {
        // White background covering the full viewport
        dc.DrawRectangle(_pageBg ?? Brushes.White, null, new Rect(0, 0, vw, vh));

        double baseX  = _pageLeft + _pageSettings.MarginLeft;
        double maxW   = Math.Max(1, _pageWidth - _pageSettings.MarginLeft - _pageSettings.MarginRight);

        for (int idx = 0; idx < _blocks.Count; idx++)
        {
            var rb = _blocks[idx];
            if (rb.Block.Kind != "heading") continue;

            double blockScreenY = PageCanvasPad + rb.Y - _offset.Y;
            if (blockScreenY + rb.Height < 0) continue;
            if (blockScreenY > vh)            break;

            int level  = int.TryParse(rb.Block.Attributes.GetValueOrDefault("level") as string, out int lv) ? lv : 1;
            double indent = (level - 1) * 20.0;

            // Selection highlight
            if (idx == _selectedIndex)
                dc.DrawRoundedRectangle(_selBrush, null,
                    new Rect(baseX + indent - 4, blockScreenY - 2, maxW - indent + 8, rb.Height + 4), 3, 3);
            else if (idx == _hoverIndex)
                dc.DrawRoundedRectangle(_hoverBrush, _blockHoverPen,
                    new Rect(baseX + indent - 4, blockScreenY - 2, maxW - indent + 8, rb.Height + 4), 3, 3);

            // Expand/collapse triangle indicator (visual affordance)
            DrawOutlineTriangle(dc, baseX + indent - 16, blockScreenY + rb.Height / 2, level);

            if (rb.GlyphLines is { Count: > 0 })
                DrawVisualLines(dc, rb.GlyphLines, baseX + indent, blockScreenY, isHeading: true, headingLevel: level);
            else if (rb.FormattedLines is { Count: > 0 })
                dc.DrawText(rb.FormattedLines[0], new Point(baseX + indent, blockScreenY));
        }
    }

    /// <summary>Small right-pointing triangle for outline mode hierarchy affordance.</summary>
    private void DrawOutlineTriangle(DrawingContext dc, double x, double midY, int level)
    {
        if (_fgDimBrush is null) return;
        double sz = Math.Max(3, 7 - level);
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(new Point(x, midY - sz / 2), isFilled: true, isClosed: true);
            ctx.LineTo(new Point(x + sz, midY), isStroked: false, isSmoothJoin: false);
            ctx.LineTo(new Point(x, midY + sz / 2), isStroked: false, isSmoothJoin: false);
        }
        geo.Freeze();
        dc.DrawGeometry(_fgDimBrush, null, geo);
    }

    /// <summary>
    /// Shared block-draw loop used by Page and Draft modes.
    /// </summary>
    private void DrawBlocksInViewport(DrawingContext dc, double vw, double vh,
                                       double contentX, double contentW)
    {
        for (int idx = 0; idx < _blocks.Count; idx++)
        {
            var rb = _blocks[idx];
            double blockScreenY = PageCanvasPad + rb.Y - _offset.Y;

            if (blockScreenY + rb.Height < 0) continue;
            if (blockScreenY > vh)            break;

            if (rb.IsPageBreak) continue;

            bool isStructural = rb.Block.Kind is "heading" or "table" or "image" or "code";
            if (isStructural)
            {
                var blockRect = new Rect(contentX - 8, blockScreenY - 4,
                                         contentW + 16, rb.Height + 8);
                if (idx == _selectedIndex)
                    dc.DrawRoundedRectangle(_selBrush, null, blockRect, 3, 3);
                else if (idx == _hoverIndex)
                    dc.DrawRoundedRectangle(_hoverBrush, _blockHoverPen, blockRect, 3, 3);
            }

            if (_forensicMode) DrawKindChip(dc, rb, blockScreenY);
            if (_forensicMode && rb.ForensicSeverity.HasValue) DrawForensicBadge(dc, rb, blockScreenY);

            DrawBlock(dc, rb, blockScreenY);
        }

        DrawFindHighlights(dc, contentX, contentW);
        DrawTextSelection(dc, contentX, contentW);
        // Caret is drawn in _caretVisual (separate layer) — see RefreshCaretVisual()
    }

    // ── Rendering helpers ────────────────────────────────────────────────────

    private void DrawLoadingOrError(DrawingContext dc, double vw, double vh)
    {
        var brush = _isError ? Brushes.Salmon : _fgDimBrush;
        var ft = MakeFormattedText(_loadingMessage, _uiFace!, 13, brush ?? Brushes.Gray,
                                   vw - 40);
        dc.DrawText(ft, new Point(20, vh / 2 - ft.Height / 2));
    }

    private string? _pageBorderColorKey;

    private Pen GetPageBorderPen()
    {
        var key = $"{_pageSettings.BorderColor}:{_pageSettings.BorderWidthPx}";
        if (_pageBorderPen is not null && _pageBorderColorKey == key)
            return _pageBorderPen;
        var color = System.Windows.Media.ColorConverter.ConvertFromString(_pageSettings.BorderColor);
        var borderColor = color is System.Windows.Media.Color c ? c : Colors.Black;
        _pageBorderPen = new Pen(new SolidColorBrush(borderColor), _pageSettings.BorderWidthPx);
        _pageBorderPen.Freeze();
        _pageBorderColorKey = key;
        return _pageBorderPen;
    }

    private void DrawPageBorder(DrawingContext dc, Rect page)
    {
        double pad = _pageSettings.BorderPaddingPx;
        var borderRect = new Rect(
            page.X + pad, page.Y + pad,
            page.Width - pad * 2, page.Height - pad * 2);

        if (_pageSettings.BorderStyle == DocumentPageBorderStyle.Shadow)
        {
            dc.DrawRectangle(_shadowBrush, null,
                new Rect(borderRect.X + 3, borderRect.Y + 3, borderRect.Width, borderRect.Height));
        }
        dc.DrawRectangle(null, GetPageBorderPen(), borderRect);
    }

    private void DrawPageShadow(DrawingContext dc, Rect page)
    {
        // Cheap shadow: several offset semi-transparent rects (brushes pre-cached)
        for (int i = 4; i >= 1; i--)
        {
            double expand = i * 2.0;
            dc.DrawRectangle(_pageShadowBrushes[i], null,
                new Rect(page.X - expand + 2, page.Y + 2 + i,
                         page.Width + expand * 2, page.Height + expand));
        }
    }


    private void DrawKindChip(DrawingContext dc, RenderBlock rb, double y)
    {
        if (_kindChipBg is null || _kindChipFg is null) return;

        var label = rb.Block.Kind.ToUpperInvariant()[..Math.Min(3, rb.Block.Kind.Length)];
        var ft    = MakeFormattedText(label, _uiFace!, 9.5, _kindChipFg, 36);

        var chipRect = new Rect(_pageLeft + 8, y + 2, 36, 16);
        dc.DrawRoundedRectangle(_kindChipBg, null, chipRect, 2, 2);
        dc.DrawText(ft, new Point(chipRect.X + (chipRect.Width - ft.Width) / 2,
                                  chipRect.Y + (chipRect.Height - ft.Height) / 2));
    }

    private void DrawForensicBadge(DrawingContext dc, RenderBlock rb, double y)
    {
        var brush = rb.ForensicSeverity == ForensicSeverity.Error
            ? _forensicErrBrush! : _forensicWarnBrush!;

        var dot = new Rect(_pageLeft + _pageWidth - _pageSettings.MarginRight - 2, y + 4, 8, 8);
        dc.DrawEllipse(brush, null,
            new Point(dot.X + dot.Width / 2, dot.Y + dot.Height / 2), 4, 4);
    }

    private void DrawBlock(DrawingContext dc, RenderBlock rb, double y)
    {
        double x    = _pageLeft + _pageSettings.MarginLeft;
        double maxW = Math.Max(1, _pageWidth - _pageSettings.MarginLeft - _pageSettings.MarginRight);

        if (rb.Block.Kind == "table")
        {
            DrawTable(dc, rb, x, y, maxW);
            return;
        }

        if (rb.Block.Kind == "image")
        {
            DrawImageBlock(dc, rb, x, y, maxW);
            return;
        }

        // ── Style-based rendering (quote / code) ──────────────────────────
        var style = rb.Block.Attributes.GetValueOrDefault("style") as string ?? string.Empty;

        if (style == "quote")
        {
            // Left accent bar + indentation
            var barBrush = _fgDimBrush ?? Brushes.Gray;
            dc.DrawRectangle(barBrush, null, new Rect(x, y, 3, rb.Height));
            x    += 12;
            maxW -= 12;
        }
        else if (style == "code")
        {
            // Monospace background pill (cached brush)
            EnsureBrushCache();
            dc.DrawRoundedRectangle(_codeBgBrush, null, new Rect(x - 4, y - 2, maxW + 8, rb.Height + 4), 3, 3);
        }

        // ── Alignment offset ─────────────────────────────────────────────
        var align = rb.Block.Attributes.GetValueOrDefault("align") as string ?? "left";
        double drawX = x;
        if (rb.GlyphLines is { Count: > 0 } && align != "left")
        {
            double lineW = rb.GlyphLines.Count > 0 ? rb.GlyphLines.Max(l => l.Width) : 0;
            drawX = align == "center" ? x + (maxW - lineW) / 2
                                       : x + maxW - lineW; // right
            drawX = Math.Max(x, drawX);
        }
        else if (rb.FormattedLines is { Count: > 0 } && align != "left")
        {
            double lineW = rb.FormattedLines.Max(ft => ft.Width);
            drawX = align == "center" ? x + (maxW - lineW) / 2
                                       : x + maxW - lineW;
            drawX = Math.Max(x, drawX);
        }

        // ── Draw ─────────────────────────────────────────────────────────
        if (rb.GlyphLines is { Count: > 0 })
        {
            bool isHeading = rb.Block.Kind == "heading" || style == "heading";
            int level = isHeading && int.TryParse(
                rb.Block.Attributes.GetValueOrDefault("level") as string, out int lv) ? lv : 1;
            DrawVisualLines(dc, rb.GlyphLines, drawX, y, isHeading, level);
            return;
        }

        if (rb.FormattedLines is { Count: > 0 })
        {
            double lineY = y;
            foreach (var ft in rb.FormattedLines)
            {
                dc.DrawText(ft, new Point(drawX, lineY));
                lineY += ft.Height + 2;
            }
        }
    }

    /// <summary>
    /// Measures a table's column width and per-row heights based on cell content.
    /// Called from both <see cref="BuildRenderBlock"/> (for total height) and
    /// <see cref="DrawTable"/> (for layout during render).
    /// </summary>
    private (double ColW, double[] RowHeights) MeasureTable(DocumentBlock tableBlock, double maxW)
    {
        var rows = tableBlock.Children.Where(c => c.Kind == "table-row").ToList();
        int cols = rows.Count > 0 ? rows.Max(r => r.Children.Count) : 1;
        cols = Math.Max(1, cols);
        double colW = maxW / cols;

        var rowHeights = new double[rows.Count];
        for (int ri = 0; ri < rows.Count; ri++)
        {
            double maxCellH = 22.0; // minimum row height
            foreach (var cell in rows[ri].Children)
            {
                var cellText = cell.Text ?? string.Empty;
                if (string.IsNullOrEmpty(cellText)) continue;
                var ft = MakeFormattedText(cellText,
                    _uiFace ?? new Typeface(UIFontFamily),
                    12,
                    _fgBrush ?? Brushes.Black,
                    Math.Max(1, colW - TableCellPad * 2));
                maxCellH = Math.Max(maxCellH, ft.Height + TableCellPad * 2);
            }
            rowHeights[ri] = maxCellH;
        }
        return (colW, rowHeights);
    }

    private void DrawTable(DrawingContext dc, RenderBlock rb, double x, double y, double maxW)
    {
        var rows = rb.Block.Children.Where(c => c.Kind == "table-row").ToList();
        if (rows.Count == 0) return;

        var (colW, rowHeights) = MeasureTable(rb.Block, maxW);
        int cols    = (int)Math.Round(maxW / Math.Max(1, colW));
        double tableW = colW * cols;
        double tableH = rowHeights.Length > 0 ? rowHeights.Sum() : rows.Count * 22.0;

        // Outer table border
        dc.DrawRectangle(null, _tableGridPen!, new Rect(x, y, tableW, tableH));

        double rowY = y;
        for (int ri = 0; ri < rows.Count; ri++)
        {
            double rowH = ri < rowHeights.Length ? rowHeights[ri] : 22.0;
            int col = 0;
            foreach (var cell in rows[ri].Children)
            {
                var cellRect = new Rect(x + col * colW, rowY, colW, rowH);
                dc.DrawRectangle(null, _tableGridPen!, cellRect);

                var cellText = cell.Text ?? string.Empty;
                if (!string.IsNullOrEmpty(cellText))
                {
                    var ft = MakeFormattedText(cellText, _uiFace!, 12, _fgBrush!,
                                               Math.Max(1, colW - TableCellPad * 2));
                    dc.DrawText(ft, new Point(cellRect.X + TableCellPad,
                                              cellRect.Y + TableCellPad));
                }
                col++;
            }
            rowY += rowH;
        }
    }

    // ── Phase 20: Image rendering ─────────────────────────────────────────────

    /// <summary>Renders an image block — uses cached BitmapImage or triggers async decode from ZIP or binary data.</summary>
    private void DrawImageBlock(DrawingContext dc, RenderBlock rb, double x, double y, double maxW)
    {
        _imageCache ??= new LruCache<string, BitmapImage?>(ImageCacheMaxEntries * 3);

        // ── Branch 1: ZIP entry (DOCX / ODT) ──────────────────────────────────
        var entryName = rb.Block.Attributes.TryGetValue("zipEntryName", out var ev) ? ev?.ToString() : null;
        var filePath  = _model?.FilePath;

        if (entryName is not null && filePath is not null && File.Exists(filePath))
        {
            var cacheKey = $"{filePath}|{entryName}";

            if (_imageCache.TryGetValue(cacheKey, out var bmp) && bmp is not null)
            {
                RenderCachedBitmap(dc, bmp, x, y, maxW, rb.Block);
                return;
            }

            if (_imageCache.ContainsKey(cacheKey + "\0error"))
            {
                DrawImageErrorPlaceholder(dc, rb, x, y, maxW);
                return;
            }

            var sentinelKey = cacheKey + "\0loading";
            if (!_imageCache.ContainsKey(sentinelKey))
            {
                _imageCache.Add(sentinelKey, null);
                var captFilePath  = filePath;
                var captEntryName = entryName;
                var captKey       = cacheKey;
                Task.Run(() =>
                {
                    try
                    {
                        using var zip = ZipFile.OpenRead(captFilePath);
                        var entry     = zip.GetEntry(captEntryName);
                        if (entry is null) throw new FileNotFoundException(captEntryName);
                        BitmapImage img;
                        using (var ms = new MemoryStream())
                        {
                            using (var s = entry.Open()) s.CopyTo(ms);
                            ms.Position = 0;
                            img = new BitmapImage();
                            img.BeginInit();
                            img.StreamSource = ms;
                            img.CacheOption  = BitmapCacheOption.OnLoad;
                            img.EndInit();
                            img.Freeze();
                        }

                        Dispatcher.InvokeAsync(() =>
                        {
                            if (_imageCache is null) return;
                            _imageCache.Add(captKey, img);
                            _imageCache.Remove(captKey + "\0loading");
                            InvalidateVisual();
                        });
                    }
                    catch
                    {
                        Dispatcher.InvokeAsync(() =>
                        {
                            if (_imageCache is null) return;
                            _imageCache.Remove(captKey + "\0loading");
                            _imageCache.Add(captKey + "\0error", null);
                            InvalidateVisual();
                        });
                    }
                });
            }
            // Show placeholder while loading
            DrawImagePlaceholder(dc, rb, x, y, maxW);
            return;
        }

        // ── Branch 2: Inline binary data (RTF \pict) ──────────────────────────
        if (rb.Block.Attributes.TryGetValue("binaryData", out var bdv) && bdv is byte[] binaryBytes)
        {
            var cacheKey    = $"binaryData|{rb.Block.RawOffset}";
            var sentinelKey = cacheKey + "\0loading";

            if (_imageCache.TryGetValue(cacheKey, out var cachedBmp) && cachedBmp is not null)
            {
                RenderCachedBitmap(dc, cachedBmp, x, y, maxW, rb.Block);
                return;
            }

            if (_imageCache.ContainsKey(cacheKey + "\0error"))
            {
                DrawImageErrorPlaceholder(dc, rb, x, y, maxW);
                return;
            }

            if (!_imageCache.ContainsKey(sentinelKey))
            {
                _imageCache.Add(sentinelKey, null);
                var captBytes = binaryBytes;
                var captKey   = cacheKey;
                Task.Run(() =>
                {
                    try
                    {
                        BitmapImage img;
                        using (var ms = new MemoryStream(captBytes))
                        {
                            img = new BitmapImage();
                            img.BeginInit();
                            img.StreamSource = ms;
                            img.CacheOption  = BitmapCacheOption.OnLoad;
                            img.EndInit();
                            img.Freeze();
                        }
                        Dispatcher.InvokeAsync(() =>
                        {
                            if (_imageCache is null) return;
                            _imageCache.Add(captKey, img);
                            _imageCache.Remove(captKey + "\0loading");
                            InvalidateVisual();
                        });
                    }
                    catch
                    {
                        Dispatcher.InvokeAsync(() =>
                        {
                            if (_imageCache is null) return;
                            _imageCache.Remove(captKey + "\0loading");
                            _imageCache.Add(captKey + "\0error", null);
                            InvalidateVisual();
                        });
                    }
                });
            }
        }

        // Fallback placeholder while image is loading or unavailable
        DrawImagePlaceholder(dc, rb, x, y, maxW);
    }

    /// <summary>Renders a decoded bitmap respecting natural size attributes, capped at maxW.</summary>
    private void RenderCachedBitmap(DrawingContext dc, BitmapImage bmp,
        double x, double y, double maxW, DocumentBlock block)
    {
        double natW  = TryParseAttr(block, "naturalWidth");
        double natH  = TryParseAttr(block, "naturalHeight");
        double imgW  = natW > 0 ? Math.Min(natW, maxW)
                                : Math.Min(maxW, bmp.PixelWidth > 0 ? bmp.PixelWidth : maxW);
        double ratio = imgW / Math.Max(1, natW > 0 ? natW : bmp.PixelWidth);
        double imgH  = natH > 0 ? natH * ratio
                                : bmp.PixelHeight * (imgW / Math.Max(1, bmp.PixelWidth));
        dc.DrawImage(bmp, new Rect(x, y, imgW, Math.Max(1, imgH)));
    }

    private static double TryParseAttr(DocumentBlock block, string key) =>
        block.Attributes.TryGetValue(key, out var v) && v is string s &&
        double.TryParse(s, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0;

    private void DrawImagePlaceholder(DrawingContext dc, RenderBlock rb, double x, double y, double maxW)
    {
        var rect = new Rect(x, y, Math.Min(maxW, 260), 48);
        dc.DrawRoundedRectangle(_kindChipBg, _tableGridPen!, rect, 4, 4);
        var en  = rb.Block.Attributes.TryGetValue("zipEntryName", out var ev) ? ev?.ToString() : null;
        string ext  = en is not null ? System.IO.Path.GetExtension(en).TrimStart('.').ToUpperInvariant() : "IMG";
        double natW = TryParseAttr(rb.Block, "naturalWidth");
        double natH = TryParseAttr(rb.Block, "naturalHeight");
        string dims = natW > 0 && natH > 0 ? $"  [{ext}] {(int)natW}×{(int)natH}  Loading…" : $"  [{ext}] Loading…";
        var ft = MakeFormattedText(dims, _uiFace!, 12, _imageFg ?? _fgDimBrush!, maxW);
        dc.DrawText(ft, new Point(x + 8, y + (48 - ft.Height) / 2));
    }

    private void DrawImageErrorPlaceholder(DrawingContext dc, RenderBlock rb, double x, double y, double maxW)
    {
        var rect = new Rect(x, y, Math.Min(maxW, 260), 48);
        EnsureBrushCache();
        dc.DrawRoundedRectangle(_errorBgBrush, _tableGridPen!, rect, 4, 4);
        var en    = rb.Block.Attributes.TryGetValue("zipEntryName", out var ev) ? ev?.ToString() : null;
        string label = en is not null
            ? $"  Failed: {System.IO.Path.GetFileName(en)}  (click to retry)"
            : $"  Failed to load image  (click to retry)";
        var ft = MakeFormattedText(label, _uiFace!, 11, _forensicErrBrush ?? Brushes.Red, maxW);
        dc.DrawText(ft, new Point(x + 8, y + (48 - ft.Height) / 2));
    }

    // ── Layout ────────────────────────────────────────────────────────────────

    private void RebuildLayout()
    {
        if (_model is null) { InvalidateVisual(); return; }

        EnsureTypefaces();

        if (_renderMode == RenderMode.Draft)
            RebuildLayoutDraft();
        else
            RebuildLayoutPaged();
    }

    private void RebuildLayoutPaged()
    {
        double maxW         = Math.Max(100, _pageWidth - _pageSettings.MarginLeft - _pageSettings.MarginRight);
        double pageContentH = _pageSettings.ContentHeight;
        double yCanvas      = _pageSettings.ContentTopY;
        double yOnPage      = 0;
        int    curPage      = 0;
        var    result       = new List<RenderBlock>();
        var    pageStarts   = new List<double> { 0.0 };
        var    alertMap     = BuildAlertMap();

        foreach (var block in _model!.Blocks)
        {
            // Outline mode: skip non-heading blocks in the paged layout too
            if (_renderMode == RenderMode.Outline && block.Kind != "heading")
                continue;

            var rb = BuildRenderBlock(block, 0, maxW, alertMap);

            if (yOnPage > 0 && yOnPage + rb.SpaceBefore + rb.Height > pageContentH)
            {
                curPage++;
                double pageOrigin = curPage * (_pageSettings.EffectivePageHeight + PageGapPx);
                pageStarts.Add(pageOrigin);
                yCanvas = pageOrigin + _pageSettings.ContentTopY;
                yOnPage = 0;
            }

            yCanvas += rb.SpaceBefore;
            yOnPage += rb.SpaceBefore;
            var placed = rb with { Y = yCanvas };
            result.Add(placed);
            yCanvas += placed.Height + placed.SpaceAfter;
            yOnPage += placed.Height + placed.SpaceAfter;
        }

        _pageStarts      = pageStarts;
        _pageCount       = pageStarts.Count;
        _blocks          = result;
        _totalHeight     = yCanvas;
        _visualLineCache.Clear();
        _caretFtDirty    = false;

        UpdateScrollExtent();
        InvalidateVisual();
        FirePageChanged();
    }

    /// <summary>
    /// Draft layout: continuous flow — no page cards, no pagination.
    /// Y grows linearly from a small top pad using compact margins.
    /// </summary>
    private void RebuildLayoutDraft()
    {
        const double DraftPad    = 16.0;  // top/bottom canvas padding in draft mode
        const double DraftMarginH = 32.0; // left/right content margin in draft mode

        double maxW     = Math.Max(100, _pageWidth - DraftMarginH * 2);
        double yCanvas  = DraftPad;
        var    result   = new List<RenderBlock>();
        var    alertMap = BuildAlertMap();

        foreach (var block in _model!.Blocks)
        {
            var rb = BuildRenderBlock(block, 0, maxW, alertMap);
            yCanvas += rb.SpaceBefore;
            result.Add(rb with { Y = yCanvas });
            yCanvas += rb.Height + rb.SpaceAfter;
        }

        // Draft uses a single virtual "page" so scroll extent = content height
        _pageStarts      = [0.0];
        _pageCount       = 1;
        _blocks          = result;
        _totalHeight     = yCanvas + DraftPad;
        _visualLineCache.Clear();
        _caretFtDirty    = false;

        UpdateScrollExtentDraft();
        InvalidateVisual();
        FirePageChanged();
    }

    private void UpdateScrollExtentDraft()
    {
        _extent = new Size(
            Math.Max(_viewport.Width, _pageWidth + PageCanvasPad * 2),
            Math.Max(_viewport.Height, _totalHeight));
        _scrollOwner?.InvalidateScrollInfo();
    }

    private RenderBlock BuildRenderBlock(
        DocumentBlock block, double y, double maxW,
        Dictionary<DocumentBlock, ForensicSeverity> alertMap)
    {
        ForensicSeverity? severity = alertMap.TryGetValue(block, out var s) ? s : null;

        switch (block.Kind)
        {
            case "heading":
            {
                int level  = int.TryParse(
                    block.Attributes.GetValueOrDefault("level") as string, out int l) ? l : 1;
                double fs     = level == 1 ? 22 : level == 2 ? 18 : 15;
                double spaceB = level == 1 ? 18 : level == 2 ? 14 : 10;
                double spaceA = level == 1 ?  8 : level == 2 ?  6 :  4;
                var glyphLines = BuildGlyphLines(block, maxW);
                double h  = glyphLines.Count > 0
                    ? glyphLines.Sum(vl => vl.LineHeight)
                    : fs + 4;
                // Extra space for H1 rule
                if (level == 1) h += 6;
                if (level == 2) h += 4;
                var ftLines = WrapText(block.Text, _bodyBoldFace!, fs, maxW, _fgBrush!);
                return new RenderBlock(block, y, h, spaceB, spaceA, ftLines, false, 0, severity, glyphLines);
            }

            case "paragraph":
            case "run":
            {
                // Style attribute may promote a paragraph to heading-like rendering
                var pStyle = block.Attributes.GetValueOrDefault("style") as string ?? string.Empty;
                if (pStyle == "heading")
                {
                    int level  = int.TryParse(
                        block.Attributes.GetValueOrDefault("level") as string, out int l) ? l : 1;
                    double fs     = level == 1 ? 22 : level == 2 ? 18 : 15;
                    double spaceB = level == 1 ? 18 : level == 2 ? 14 : 10;
                    double spaceA = level == 1 ?  8 : level == 2 ?  6 :  4;
                    var hGlyphLines = BuildGlyphLines(block, maxW);
                    double hh = hGlyphLines.Count > 0 ? hGlyphLines.Sum(vl => vl.LineHeight) : fs + 4;
                    if (level == 1) hh += 6;
                    if (level == 2) hh += 4;
                    var hFtLines = WrapText(block.Text, _bodyBoldFace!, fs, maxW, _fgBrush!);
                    return new RenderBlock(block, y, hh, spaceB, spaceA, hFtLines, false, 0, severity, hGlyphLines);
                }

                double spaceAfter = pStyle == "quote" ? 6 : pStyle == "code" ? 6 : 4;
                var glyphLines = BuildGlyphLines(block, maxW);
                double h  = glyphLines.Count > 0
                    ? glyphLines.Sum(vl => vl.LineHeight)
                    : _baseFontSize + 4;
                var ftLines = BuildInlineFormattedText(block, maxW);
                return new RenderBlock(block, y, h, 0, spaceAfter, ftLines, false, 0, severity, glyphLines);
            }

            case "table":
            {
                var (_, rowHeights) = MeasureTable(block, maxW);
                double h = rowHeights.Length > 0 ? rowHeights.Sum() + 2 : 28;
                return new RenderBlock(block, y, h, 8, 8, null, false, 0, severity);
            }

            case "image":
            {
                double natH = TryParseAttr(block, "naturalHeight");
                double h    = natH > 0 ? natH : 120.0;
                return new RenderBlock(block, y, h, 8, 8, null, false, 0, severity);
            }

            default:
            {
                if (string.IsNullOrEmpty(block.Text))
                    return new RenderBlock(block, y, 8, 0, 2, null, false, 0, severity);

                var glyphLines = BuildGlyphLines(block, maxW);
                double h  = glyphLines.Count > 0
                    ? glyphLines.Sum(vl => vl.LineHeight)
                    : _baseFontSize + 4;
                var ftLines = WrapText(block.Text, _bodyFace!, _baseFontSize, maxW, _fgBrush!);
                return new RenderBlock(block, y, h, 0, 2, ftLines, false, 0, severity, glyphLines);
            }
        }
    }

    // ── GlyphRun pipeline ─────────────────────────────────────────────────────

    private double _cachedPixelsPerDip = 0;

    private double GetPixelsPerDip()
    {
        if (_cachedPixelsPerDip > 0) return _cachedPixelsPerDip;
        try
        {
            var mainWindow = Application.Current?.MainWindow;
            if (mainWindow is not null)
                _cachedPixelsPerDip = VisualTreeHelper.GetDpi(mainWindow).PixelsPerDip;
        }
        catch { }
        if (_cachedPixelsPerDip <= 0) _cachedPixelsPerDip = 1.0;
        return _cachedPixelsPerDip;
    }

    /// <summary>
    /// Converts a document block into <see cref="InlineVisualLine"/> objects using
    /// <see cref="InlineLineBreaker"/>. Called from <see cref="BuildRenderBlock"/>.
    /// </summary>
    private IReadOnlyList<InlineVisualLine> BuildGlyphLines(DocumentBlock block, double maxW)
    {
        var segments = BuildSegments(block);
        if (segments.Count == 0) return [];
        return InlineLineBreaker.Break(segments, maxW, GetPixelsPerDip());
    }

    /// <summary>
    /// Produces <see cref="InlineSegment"/> list from a block's runs (or plain text).
    /// </summary>
    private List<InlineSegment> BuildSegments(DocumentBlock block)
    {
        EnsureBrushCache();
        var defaultColor = _fgBrush is SolidColorBrush sb ? sb.Color : Color.FromRgb(20, 20, 20);
        var result = new List<InlineSegment>();

        var blockStyle = block.Attributes.GetValueOrDefault("style") as string ?? string.Empty;
        bool isHeading = block.Kind == "heading" || blockStyle == "heading";
        int headingLevel = isHeading && int.TryParse(
            block.Attributes.GetValueOrDefault("level") as string, out int hl) ? hl : 1;
        double defaultSize = isHeading
            ? (headingLevel == 1 ? 22 : headingLevel == 2 ? 18 : 15)
            : _baseFontSize;
        bool defaultBold = isHeading;
        string defaultFamily = blockStyle == "code" ? "Courier New" : BodyFontFamily;

        IEnumerable<DocumentBlock> runs = block.Children.Count > 0
            ? block.Children.Where(c => c.Kind == "run")
            : [block];

        foreach (var run in runs)
        {
            var text = run.Text;
            if (string.IsNullOrEmpty(text)) continue;

            bool   bold   = defaultBold || (run.Attributes.TryGetValue("bold",   out var b) && b is true);
            bool   italic = run.Attributes.TryGetValue("italic",  out var i) && i is true;
            bool   under  = run.Attributes.TryGetValue("underline", out var u) && u is true;
            bool   strike = run.Attributes.TryGetValue("strikethrough", out var st) && st is true;

            double size = defaultSize;
            if (run.Attributes.TryGetValue("fontSize", out var fs))
                size = fs is int fi ? fi : fs is double fd ? fd : size;

            string family = defaultFamily;
            if (run.Attributes.TryGetValue("fontFamily", out var ffv) && ffv is string ff && ff.Length > 0)
                family = ff;

            Color color = defaultColor;
            if (run.Attributes.TryGetValue("color", out var cv) && cv is string cs)
            {
                try
                {
                    var converted = System.Windows.Media.ColorConverter.ConvertFromString(cs);
                    if (converted is Color c2) color = c2;
                }
                catch { }
            }

            var gt = GlyphTypefaceCache.Get(family, bold, italic);
            result.Add(new InlineSegment(text, gt, size, color, under, strike));
        }

        return result;
    }

    /// <summary>
    /// Draws <see cref="InlineVisualLine"/> objects at (originX, blockTopY) using DrawGlyphRun.
    /// Underline and strikethrough are drawn as separate lines.
    /// </summary>
    private void DrawVisualLines(DrawingContext dc,
                                  IReadOnlyList<InlineVisualLine> lines,
                                  double originX, double blockTopY,
                                  bool isHeading = false, int headingLevel = 1)
    {
        double ppd = GetPixelsPerDip();
        double y   = blockTopY;

        for (int li = 0; li < lines.Count; li++)
        {
            var    line      = lines[li];
            double baselineY = y + line.Ascent;

            foreach (var seg in line.Segments)
            {
                var absOrigin = new Point(originX + seg.OffsetX, baselineY);
                var run       = seg.BuildGlyphRun(absOrigin, ppd);

                var brush = new SolidColorBrush(seg.Foreground);
                brush.Freeze();
                dc.DrawGlyphRun(brush, run);

                if (seg.Underline)
                {
                    double uY  = baselineY + seg.UnderlineOffset;
                    var uPen   = new Pen(brush, 0.75);
                    uPen.Freeze();
                    dc.DrawLine(uPen,
                        new Point(originX + seg.OffsetX,               uY),
                        new Point(originX + seg.OffsetX + seg.Width,   uY));
                }

                if (seg.Strikethrough)
                {
                    double sY  = baselineY - seg.StrikethroughOffset;
                    var sPen   = new Pen(brush, 0.75);
                    sPen.Freeze();
                    dc.DrawLine(sPen,
                        new Point(originX + seg.OffsetX,               sY),
                        new Point(originX + seg.OffsetX + seg.Width,   sY));
                }
            }

            // H1: full-width rule below first line (cached pen)
            if (isHeading && headingLevel == 1 && li == 0 && _h1RulePen is not null)
            {
                double ruleY = y + line.LineHeight + 3;
                double ruleW = Math.Max(1, _pageWidth - _pageSettings.MarginLeft - _pageSettings.MarginRight);
                dc.DrawLine(_h1RulePen, new Point(originX, ruleY), new Point(originX + ruleW, ruleY));
            }

            // H2: 30%-width rule below first line (cached pen)
            if (isHeading && headingLevel == 2 && li == 0 && _h2RulePen is not null)
            {
                double ruleY = y + line.LineHeight + 2;
                double ruleW = Math.Max(1, (_pageWidth - _pageSettings.MarginLeft - _pageSettings.MarginRight) * 0.3);
                dc.DrawLine(_h2RulePen, new Point(originX, ruleY), new Point(originX + ruleW, ruleY));
            }

            y += line.LineHeight;
        }
    }

    private List<FormattedText> BuildInlineFormattedText(DocumentBlock block, double maxW)
    {
        // If block has run children, render each run segment individually on same line
        // For simplicity, flatten runs into one composite text for now.
        // Full run-level formatting needs GlyphRun composition — use FormattedText with SetXxx ranges.
        if (block.Children.Count > 0)
        {
            // Build combined text from runs, applying bold/italic to ranges
            var sb = new System.Text.StringBuilder();
            foreach (var run in block.Children.Where(c => c.Kind == "run"))
                sb.Append(run.Text);

            var fullText = sb.ToString();
            if (string.IsNullOrEmpty(fullText)) return [];

            var ft = MakeFormattedText(fullText, _bodyFace!, _baseFontSize, _fgBrush!, maxW);

            // Apply per-run formatting ranges
            int pos = 0;
            foreach (var run in block.Children.Where(c => c.Kind == "run"))
            {
                int len = run.Text.Length;
                if (len == 0) { pos += len; continue; }

                if (run.Attributes.TryGetValue("bold",      out var b) && b is true)
                    ft.SetFontWeight(FontWeights.Bold, pos, len);
                if (run.Attributes.TryGetValue("italic",    out var i) && i is true)
                    ft.SetFontStyle(FontStyles.Italic, pos, len);
                if (run.Attributes.TryGetValue("underline", out var u) && u is true)
                    ft.SetTextDecorations(TextDecorations.Underline, pos, len);

                // fontSize may be int (DOCX) or double (ODT)
                if (run.Attributes.TryGetValue("fontSize", out var fs))
                {
                    double sz = fs is int i2 ? i2 : fs is double d2 ? d2 : 0;
                    if (sz > 0) ft.SetFontSize(sz, pos, len);
                }

                // fontFamily — per-run override (DOCX: w:rFonts, ODT: style fo:font-family)
                if (run.Attributes.TryGetValue("fontFamily", out var ffv) &&
                    ffv is string ff && !string.IsNullOrEmpty(ff))
                    ft.SetFontFamily(new FontFamily(ff), pos, len);

                // color — per-run foreground (was silently dropped before)
                if (run.Attributes.TryGetValue("color", out var cv) && cv is string cs2)
                {
                    try
                    {
                        var converted = System.Windows.Media.ColorConverter.ConvertFromString(cs2);
                        if (converted is Color rc)
                        {
                            var brush = new SolidColorBrush(rc);
                            brush.Freeze();
                            ft.SetForegroundBrush(brush, pos, len);
                        }
                    }
                    catch { }
                }

                pos += len;
            }

            return WrapFormattedText(ft, maxW);
        }

        return WrapText(block.Text, _bodyFace!, _baseFontSize, maxW, _fgBrush!);
    }

    private static List<FormattedText> WrapFormattedText(FormattedText ft, double maxW)
    {
        // FormattedText handles wrapping internally via MaxTextWidth
        ft.MaxTextWidth = maxW;
        return [ft];
    }

    private List<FormattedText> WrapText(string text, Typeface face, double size, double maxW, Brush brush)
    {
        if (string.IsNullOrEmpty(text)) return [];
        var ft = MakeFormattedText(text, face, size, brush, maxW);
        ft.MaxTextWidth = maxW;
        return [ft];
    }

    private static FormattedText MakeFormattedText(string text, Typeface face, double size,
                                                    Brush brush, double maxWidth)
    {
        var ft = new FormattedText(
            text.Length == 0 ? " " : text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            face,
            size,
            brush,
            VisualTreeHelper.GetDpi(Application.Current.MainWindow ?? new Window()).PixelsPerDip);

        ft.MaxTextWidth  = Math.Max(1, maxWidth);
        ft.Trimming      = TextTrimming.None;
        ft.TextAlignment = TextAlignment.Left;
        return ft;
    }

    // ── Geometry ─────────────────────────────────────────────────────────────

    private void RecalcPageGeometry(double viewWidth)
    {
        double available  = viewWidth - PageCanvasPad * 2;
        double nominalW   = _pageSettings.EffectivePageWidth;
        _pageWidth = Math.Clamp(available, 400, Math.Max(400, nominalW));
        _pageLeft  = (viewWidth - _pageWidth) / 2;
    }

    private void UpdateScrollExtent()
    {
        // Total canvas height: N pages stacked with gaps, plus top/bottom canvas padding
        double pageH   = _pageSettings.EffectivePageHeight;
        double canvasH = _pageCount * (pageH + PageGapPx) - PageGapPx + PageCanvasPad * 2;
        _extent = new Size(Math.Max(_viewport.Width, _pageWidth + PageCanvasPad * 2), canvasH);
        _scrollOwner?.InvalidateScrollInfo();
    }

    // ── Brush / typeface init ────────────────────────────────────────────────

    private void InvalidateBrushCache()
    {
        _canvasBg = null;
    }

    private void EnsureBrushCache()
    {
        if (_canvasBg is not null) return;

        // Canvas around the page — dark (themed)
        _canvasBg     = GetBrush("DE_PageCanvasBackground", Color.FromRgb(42, 42, 42));

        // Page itself = ALWAYS white (paper / WYSIWYG)
        _pageBg = Brushes.White;

        // Text = ALWAYS near-black (ink on paper)
        _fgBrush = new SolidColorBrush(Color.FromRgb(20, 20, 20));
        ((SolidColorBrush)_fgBrush).Freeze();
        _fgDimBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100));
        ((SolidColorBrush)_fgDimBrush).Freeze();

        // Heading = dark, same as body (Word-style)
        _headingFg = new SolidColorBrush(Color.FromRgb(20, 20, 20));
        ((SolidColorBrush)_headingFg).Freeze();

        // Interactive overlays — semi-transparent accent (readable on white)
        _hoverBrush     = new SolidColorBrush(Color.FromArgb(24, 86, 156, 214));
        ((SolidColorBrush)_hoverBrush).Freeze();
        _selBrush       = new SolidColorBrush(Color.FromArgb(48, 78, 148, 220));
        ((SolidColorBrush)_selBrush).Freeze();

        _pageBreakBrush = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0));
        ((SolidColorBrush)_pageBreakBrush).Freeze();

        // Chip / forensic colors (only used in forensic mode)
        _kindChipBg        = new SolidColorBrush(Color.FromRgb(220, 220, 220));
        ((SolidColorBrush)_kindChipBg).Freeze();
        _kindChipFg        = new SolidColorBrush(Color.FromRgb(60, 100, 160));
        ((SolidColorBrush)_kindChipFg).Freeze();
        _forensicErrBrush  = GetBrush("DE_ForensicBadgeErrorBg", Color.FromRgb(204, 51, 51));
        _forensicWarnBrush = GetBrush("DE_ForensicBadgeWarnBg",  Color.FromRgb(204, 122, 0));
        _pageNumFg         = _fgDimBrush;
        _imageFg           = new SolidColorBrush(Color.FromRgb(80, 80, 200));
        ((SolidColorBrush)_imageFg).Freeze();

        // Table borders — light gray (like Word)
        _tableBorderBrush = new SolidColorBrush(Color.FromRgb(180, 180, 180));
        ((SolidColorBrush)_tableBorderBrush).Freeze();

        // Page card subtle drop-shadow border (like Word)
        _pageCardPen = new Pen(new SolidColorBrush(Color.FromArgb(50, 0, 0, 0)), 1);
        _pageCardPen.Freeze();

        // Page break dashed line
        _pageBreakPen = new Pen(new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)), 1) { DashStyle = DashStyles.Dash };
        _pageBreakPen.Freeze();

        // Hover/selection border — blue accent
        _blockHoverPen = new Pen(new SolidColorBrush(Color.FromArgb(60, 66, 133, 244)), 1);
        _blockHoverPen.Freeze();

        // Table grid — light gray (like Word)
        _tableGridPen = new Pen(_tableBorderBrush, 0.75);
        _tableGridPen.Freeze();

        _pageBorder = _pageCardPen.Brush;

        // Phase 12 — cursor + selection (WYSIWYG: black caret, blue selection on white)
        _textSelBrush       = new SolidColorBrush(Color.FromArgb(80, 66, 133, 244));
        ((SolidColorBrush)_textSelBrush).Freeze();
        // WYSIWYG: page is always white, so caret must always be near-black
        _caretBrush = new SolidColorBrush(Color.FromRgb(20, 20, 20));
        ((SolidColorBrush)_caretBrush).Freeze();

        // Phase 19 — find highlight (yellow, like Word Ctrl+F)
        _findHighlightBrush = new SolidColorBrush(Color.FromArgb(120, 255, 215, 0));
        ((SolidColorBrush)_findHighlightBrush).Freeze();

        // Per-style cached brushes/pens
        _codeBgBrush = new SolidColorBrush(Color.FromArgb(30, 128, 128, 128));
        ((SolidColorBrush)_codeBgBrush).Freeze();
        _errorBgBrush = new SolidColorBrush(Color.FromRgb(255, 235, 235));
        ((SolidColorBrush)_errorBgBrush).Freeze();
        _shadowBrush = new SolidColorBrush(Color.FromArgb(80, 0, 0, 0));
        ((SolidColorBrush)_shadowBrush).Freeze();
        _h1RulePen = new Pen(_fgDimBrush, 0.5);
        _h1RulePen.Freeze();
        _h2RulePen = new Pen(_fgDimBrush, 0.5);
        _h2RulePen.Freeze();
        for (int i = 1; i <= 4; i++)
        {
            var b = new SolidColorBrush(Color.FromArgb((byte)(0.08 * i * 255), 0, 0, 0));
            b.Freeze();
            _pageShadowBrushes[i] = b;
        }
        _pageBorderPen = null; // rebuilt when settings change; see DrawPageBorder
    }

    private Brush GetBrush(string key, Color fallbackColor)
    {
        var brush = TryFindResource(key) as Brush
                    ?? new SolidColorBrush(fallbackColor);
        if (brush.CanFreeze) brush.Freeze();
        return brush;
    }

    private void EnsureTypefaces()
    {
        if (_uiFace is not null) return;
        _bodyFace          = new Typeface(BodyFontFamily);
        _bodyBoldFace      = new Typeface(new FontFamily(BodyFontFamily), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        _bodyItalicFace    = new Typeface(new FontFamily(BodyFontFamily), FontStyles.Italic, FontWeights.Normal, FontStretches.Normal);
        _bodyBoldItalicFace= new Typeface(new FontFamily(BodyFontFamily), FontStyles.Italic, FontWeights.Bold, FontStretches.Normal);
        _uiFace            = new Typeface(UIFontFamily);
        _uiBoldFace        = new Typeface(new FontFamily(UIFontFamily), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        _monoFace          = new Typeface("Consolas");
    }

    // ── Interaction ───────────────────────────────────────────────────────────

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var pt = e.GetPosition(this);

        // Drag-to-select
        if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
        {
            int idx = HitTestBlock(pt);
            if (idx >= 0 && idx < _blocks.Count)
            {
                int off          = GetCharOffsetAtPoint(idx, pt);
                _caret           = new TextCaret(idx, off, ComputePreferredX(idx, off));
                _selection.Focus = _caret;
                _caretVisible    = true;
                EnsureCaretVisible();
                InvalidateVisual();
            }
            return;
        }

        int hovered = HitTestBlock(pt);
        if (hovered == _hoverIndex) return;
        _hoverIndex = hovered;
        InvalidateVisual();
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (_hoverIndex == -1) return;
        _hoverIndex = -1;
        InvalidateVisual();
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        Focus();
        Keyboard.Focus(this);
        var pt  = e.GetPosition(this);
        int idx = HitTestBlock(pt);
        if (idx < 0 || idx >= _blocks.Count) { _selectedIndex = -1; return; }

        _selectedIndex = idx;
        var rb    = _blocks[idx];
        var block = rb.Block;
        SelectedBlockChanged?.Invoke(this, block);

        // Image error retry: click clears the error sentinel so the next render retries
        if (block.Kind == "image" && _imageCache is not null && _model?.FilePath is string fp)
        {
            var en = block.Attributes.TryGetValue("zipEntryName", out var ev2) ? ev2?.ToString() : null;
            if (en is not null)
            {
                string errKey = $"{fp}|{en}\0error";
                if (_imageCache.ContainsKey(errKey)) { _imageCache.Remove(errKey); InvalidateVisual(); e.Handled = true; return; }
            }
        }

        // Phase 16: double-click on table-cell → inline CellEditorAdorner
        if (e.ClickCount >= 2 && block.Kind == "table-cell" &&
            !_isReadOnly && _mutator is not null)
        {
            OpenCellEditor(rb, pt);
            e.Handled = true;
            return;
        }

        bool shift   = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        int  charOff = GetCharOffsetAtPoint(idx, pt);

        // Double-click: select word at click position
        if (e.ClickCount == 2)
        {
            SelectWordAt(idx, charOff);
            e.Handled = true;
            return;
        }

        // Shift+Click: extend selection (keep anchor)
        if (shift && e.ClickCount == 1)
        {
            _caret           = new TextCaret(idx, charOff, ComputePreferredX(idx, charOff));
            _selection.Focus = _caret;
        }
        else
        {
            _caret            = new TextCaret(idx, charOff, ComputePreferredX(idx, charOff));
            _selection.Anchor = _caret;
            _selection.Focus  = _caret;
        }

        _isDragging   = true;
        CaptureMouse();

        _caretVisible = true;
        if (_blinkTimer is { IsEnabled: false }) _blinkTimer.Start();
        RefreshCaretVisual();
        InvalidateVisual();
        e.Handled = true;
    }

    private void SelectWordAt(int blockIdx, int charOff)
    {
        var text = GetFlatText(blockIdx);
        if (string.IsNullOrEmpty(text)) return;
        int start = charOff, end = charOff;
        while (start > 0 && !IsWordSeparator(text[start - 1])) start--;
        while (end < text.Length && !IsWordSeparator(text[end])) end++;
        if (start == end && end < text.Length) end++;
        _selection.Anchor = new TextCaret(blockIdx, start, 0);
        _selection.Focus  = new TextCaret(blockIdx, end,   0);
        _caret            = _selection.Focus;
        _caretVisible     = true;
        InvalidateVisual();
    }

    private void OpenCellEditor(RenderBlock rb, Point clickPt)
    {
        var layer = AdornerLayer.GetAdornerLayer(this);
        if (layer is null) return;

        double blockScreenY = PageCanvasPad + rb.Y - _offset.Y;
        var cellRect = new Rect(
            _pageLeft + _pageSettings.MarginLeft,
            blockScreenY,
            Math.Max(1, _pageWidth - _pageSettings.MarginLeft - _pageSettings.MarginRight),
            rb.Height);

        var adorner = new CellEditorAdorner(this, cellRect, rb.Block, _mutator!);
        layer.Add(adorner);
        adorner.Focus();

        adorner.EditCommitted += (_, _) =>
        {
            layer.Remove(adorner);
            RebuildLayout();
            InvalidateVisual();
        };
        adorner.EditCancelled += (_, _) => layer.Remove(adorner);
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        ReleaseMouseCapture();
        SelectionFormatChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnGotFocus(object sender, RoutedEventArgs e)
    {
        _caretVisible = true;
        if (_blinkTimer is { IsEnabled: false }) _blinkTimer.Start();
        RefreshCaretVisual();
    }

    private void OnLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        // Keep blinking while focus stays within the same top-level window
        // (toolbar buttons, font dropdown, Bold/Italic toggles — all still "our" window).
        // Only stop when focus genuinely leaves our window (another app, dialog, etc).
        var newFocus = e.NewFocus as DependencyObject ?? Keyboard.FocusedElement as DependencyObject;
        if (newFocus is not null)
        {
            var thisWindow = Window.GetWindow(this);
            var thatWindow = newFocus is Window w ? w : Window.GetWindow(newFocus);
            if (thisWindow is not null && ReferenceEquals(thisWindow, thatWindow))
                return; // Focus stayed inside our window — keep blink timer running
        }

        _blinkTimer?.Stop();
        _caretVisible = false;
        RefreshCaretVisual();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_isReadOnly && e.Key is not Key.C and not Key.A) return;

        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        bool ctrl  = (Keyboard.Modifiers & ModifierKeys.Control) != 0;

        switch (e.Key)
        {
            // ── Horizontal char / word navigation ─────────────────────────
            case Key.Left  when !ctrl: MoveCaretByChar(-1, shift); e.Handled = true; break;
            case Key.Right when !ctrl: MoveCaretByChar(+1, shift); e.Handled = true; break;
            case Key.Left  when ctrl:  MoveCaretByWord(-1, shift); e.Handled = true; break;
            case Key.Right when ctrl:  MoveCaretByWord(+1, shift); e.Handled = true; break;

            // ── Vertical visual-line navigation ───────────────────────────
            case Key.Up   when !ctrl: MoveCaretByVisualLine(-1, shift); e.Handled = true; break;
            case Key.Down when !ctrl: MoveCaretByVisualLine(+1, shift); e.Handled = true; break;

            // ── Document edge (Ctrl+Home / Ctrl+End) ──────────────────────
            case Key.Home when ctrl: MoveCaretToDocumentEdge(toEnd: false, extend: shift); e.Handled = true; break;
            case Key.End  when ctrl: MoveCaretToDocumentEdge(toEnd: true,  extend: shift); e.Handled = true; break;

            // ── Visual-line Home / End ────────────────────────────────────
            case Key.Home when !ctrl: MoveCaretToLineEdge(toEnd: false, extend: shift); e.Handled = true; break;
            case Key.End  when !ctrl: MoveCaretToLineEdge(toEnd: true,  extend: shift); e.Handled = true; break;

            // ── Page navigation ───────────────────────────────────────────
            case Key.PageUp:   MoveCaretByPage(-1, shift); e.Handled = true; break;
            case Key.PageDown: MoveCaretByPage(+1, shift); e.Handled = true; break;

            // ── Editing ───────────────────────────────────────────────────
            case Key.A when ctrl:  SelectAll();              e.Handled = true; break;
            case Key.C when ctrl:  CopySelection();          e.Handled = true; break;
            case Key.X when ctrl:  CutSelection();           e.Handled = true; break;
            case Key.V when ctrl:  PasteAtCaret();           e.Handled = true; break;
            case Key.Z when ctrl:  _mutator?.TryUndo();      e.Handled = true; break;
            case Key.Y when ctrl:  _mutator?.TryRedo();      e.Handled = true; break;
            case Key.Back:         DeleteAtCaret(forward: false); e.Handled = true; break;
            case Key.Delete:       DeleteAtCaret(forward: true);  e.Handled = true; break;
            case Key.Return:       SplitBlockAtCaret();       e.Handled = true; break;
        case Key.Escape:
            if (!_selection.IsEmpty)
            {
                _selection.Anchor = _caret;
                _selection.Focus  = _caret;
                InvalidateVisual();
            }
            e.Handled = true;  // prevent propagation to IDE shell
            break;
        }
        if (e.Handled) SelectionFormatChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (_isReadOnly || string.IsNullOrEmpty(e.Text) || _mutator is null) return;
        DeleteSelectionIfAny();
        InsertTextAtCaret(e.Text);
        e.Handled = true;
    }

    private int HitTestBlock(Point pt)
    {
        if (_blocks.Count == 0) return -1;

        double blockScreenX = _pageLeft + _pageSettings.MarginLeft - 8;
        double blockW       = _pageWidth - _pageSettings.MarginLeft - _pageSettings.MarginRight + 16;

        // Fast X rejection: if pt is outside the column strip, nothing will match
        if (pt.X < blockScreenX || pt.X > blockScreenX + blockW) return -1;

        // Binary search: find the first block whose bottom edge (Y + Height + 4) >= pt.Y
        // Blocks are sorted by Y, so we binary-search for the first candidate
        double ptYInCanvas = pt.Y + _offset.Y - PageCanvasPad; // convert to canvas space
        int lo = 0, hi = _blocks.Count - 1, candidate = -1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            double midBottom = _blocks[mid].Y + _blocks[mid].Height + 4;
            if (midBottom < ptYInCanvas)
                lo = mid + 1;
            else
            { candidate = mid; hi = mid - 1; }
        }

        // Verify the found candidate (and its neighbours) with exact rect check
        for (int i = Math.Max(0, candidate - 1); i <= Math.Min(_blocks.Count - 1, candidate + 1) && candidate >= 0; i++)
        {
            var rb = _blocks[i];
            if (rb.IsPageBreak) continue;
            double blockScreenY = PageCanvasPad + rb.Y - _offset.Y;
            var rect = new Rect(blockScreenX, blockScreenY - 4, blockW, rb.Height + 8);
            if (rect.Contains(pt)) return i;
        }
        return -1;
    }

    private void SelectBlock(DocumentBlock target)
    {
        int idx = _blocks.FindIndex(rb => rb.Block == target);
        if (idx < 0) return;

        _selectedIndex = idx;

        // Scroll to make block visible
        double blockY = PageCanvasPad + _blocks[idx].Y;

        if (blockY < _offset.Y)
            SetVerticalOffset(blockY - 20);
        else if (blockY + _blocks[idx].Height > _offset.Y + _viewport.Height)
            SetVerticalOffset(blockY + _blocks[idx].Height - _viewport.Height + 20);

        InvalidateVisual();
    }

    // ── Block typeface / size helpers ─────────────────────────────────────────

    private Typeface GetBlockTypeface(DocumentBlock block)
    {
        EnsureTypefaces();
        if (block.Kind == "heading") return _bodyBoldFace!;
        if (block.Attributes.ContainsKey("bold") && block.Attributes.ContainsKey("italic")) return _bodyBoldItalicFace!;
        if (block.Attributes.ContainsKey("bold"))   return _bodyBoldFace!;
        if (block.Attributes.ContainsKey("italic")) return _bodyItalicFace!;
        return _bodyFace!;
    }

    private double GetBlockFontSize(DocumentBlock block)
    {
        if (block.Kind == "heading")
        {
            int level = int.TryParse(block.Attributes.GetValueOrDefault("level") as string, out int l) ? l : 1;
            return level == 1 ? 22 : level == 2 ? 18 : 15;
        }
        if (block.Attributes.TryGetValue("fontSize", out var fs) && fs is int sz) return sz;
        return _baseFontSize;
    }

    // ── Phase 12: Caret / Selection helpers ───────────────────────────────────

    /// <summary>Returns all run-concatenated flat text for a block.</summary>
    private string GetFlatText(int blockIdx)
    {
        if (blockIdx < 0 || blockIdx >= _blocks.Count) return string.Empty;
        var b = _blocks[blockIdx].Block;
        return b.Children.Count > 0
            ? string.Concat(b.Children.Select(c => c.Text))
            : b.Text;
    }

    /// <summary>Returns (cached) visual-line list for a block. Empty list if block has no FormattedText.</summary>
    private IReadOnlyList<VisualLine> GetVisualLines(int blockIdx)
    {
        if (_visualLineCache.TryGetValue(blockIdx, out var cached)) return cached;
        var rb = _blocks[blockIdx];
        if (rb.FormattedLines is not { Count: > 0 })
            return _visualLineCache[blockIdx] = [];
        var ft    = rb.FormattedLines[0];
        var text  = GetFlatText(blockIdx);
        var lines = CaretNavHelper.GetLines(ft, text.Length);
        return _visualLineCache[blockIdx] = lines;
    }

    /// <summary>Returns caret X (FormattedText-local) for a given char offset.</summary>
    private double ComputePreferredX(int blockIdx, int charOffset)
    {
        if (blockIdx < 0 || blockIdx >= _blocks.Count) return 0;
        var rb = _blocks[blockIdx];
        if (rb.FormattedLines is not { Count: > 0 }) return 0;
        return CaretNavHelper.GetCaretX(rb.FormattedLines[0], charOffset, GetFlatText(blockIdx).Length);
    }

    /// <summary>
    /// Central caret commit: updates _caret, extends or collapses selection,
    /// resets PreferredX when vertical=false, triggers scroll + repaint.
    /// </summary>
    private void CommitCaret(TextCaret newCaret, bool extend, bool vertical)
    {
        if (!vertical)
        {
            double px = ComputePreferredX(newCaret.BlockIndex, newCaret.CharOffset);
            newCaret  = newCaret with { PreferredX = px };
        }
        _caret              = newCaret;
        _lastMoveWasVertical = vertical;
        if (!extend) { _selection.Anchor = _caret; _selection.Focus = _caret; }
        else           _selection.Focus  = _caret;
        _caretVisible = true;
        EnsureCaretVisible();
        InvalidateVisual();
        RefreshCaretVisual();
    }

    /// <summary>Scrolls the viewport to make the caret's visual line visible (± 16px).</summary>
    private void EnsureCaretVisible()
    {
        if (_caret.BlockIndex < 0 || _caret.BlockIndex >= _blocks.Count) return;
        var rb    = _blocks[_caret.BlockIndex];
        var lines = GetVisualLines(_caret.BlockIndex);
        int li    = lines.Count > 0 ? CaretNavHelper.GetLineIndex(lines, _caret.CharOffset) : 0;
        double lineTop = PageCanvasPad + rb.Y + (lines.Count > 0 ? lines[li].Top : 0);
        double lineH   = lines.Count > 0 ? lines[li].Height : _baseFontSize + 4;
        const double Pad = 16;
        if (lineTop - Pad < _offset.Y)
            SetVerticalOffset(lineTop - Pad);
        else if (lineTop + lineH + Pad > _offset.Y + _viewport.Height)
            SetVerticalOffset(lineTop + lineH + Pad - _viewport.Height);
    }

    /// <summary>
    /// Returns the char offset in the block nearest to the given canvas point.
    /// Uses a simple linear probe across char positions.
    /// </summary>
    private int GetCharOffsetAtPoint(int blockIdx, Point canvasPt)
    {
        if (_blocks.Count == 0 || blockIdx < 0 || blockIdx >= _blocks.Count) return 0;
        var rb   = _blocks[blockIdx];
        var text = GetFlatText(blockIdx);
        if (string.IsNullOrEmpty(text)) return 0;

        double originX = _pageLeft + _pageSettings.MarginLeft;
        double originY = PageCanvasPad + rb.Y - _offset.Y;
        double relX    = canvasPt.X - originX;

        // Use the cached FormattedText (same one used for rendering + navigation) so
        // character positions agree with what is drawn and with GetVisualLines.
        var ft = rb.FormattedLines is { Count: > 0 }
            ? rb.FormattedLines[0]
            : MakeFormattedText(text, GetBlockTypeface(rb.Block), GetBlockFontSize(rb.Block),
                                _fgBrush ?? Brushes.Gray,
                                Math.Max(1, _pageWidth - _pageSettings.MarginLeft - _pageSettings.MarginRight));

        // For wrapped text, chars on later visual lines have Bounds.Left ≈ 0 again, so a
        // simple Left-based binary search gives wrong results across line breaks.
        // Correct approach: determine which visual line was clicked (by Y), then binary-search
        // within that line's char range using Bounds.Left.
        double relY = canvasPt.Y - originY;

        var vlines = GetVisualLines(blockIdx);
        VisualLine targetLine = vlines.Count > 0 ? vlines[^1] : new VisualLine(0, text.Length, 0, _baseFontSize + 2);

        if (vlines.Count > 0)
        {
            foreach (var vl in vlines)
            {
                if (relY <= vl.Top + vl.Height)
                {
                    targetLine = vl;
                    break;
                }
            }
        }

        // Binary search within [targetLine.Start, targetLine.End] on Bounds.Left
        int lo = targetLine.Start, hi = targetLine.End;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            var geo = ft.BuildHighlightGeometry(new Point(0, 0), mid, 1);
            if (geo is null || geo.Bounds.IsEmpty) { hi = mid; continue; }
            if (geo.Bounds.Left <= relX) lo = mid + 1;
            else hi = mid;
        }
        return Math.Clamp(lo, 0, text.Length);
    }

    /// <summary>Returns the canvas point (top-left) of the caret insertion position.</summary>
    private Point GetCaretPoint(TextCaret caret)
    {
        if (caret.BlockIndex < 0 || caret.BlockIndex >= _blocks.Count)
            return new Point(0, 0);

        var rb    = _blocks[caret.BlockIndex];
        var text  = GetFlatText(caret.BlockIndex);
        double ox = _pageLeft + _pageSettings.MarginLeft;
        double oy = PageCanvasPad + rb.Y - _offset.Y;

        if (string.IsNullOrEmpty(text) || caret.CharOffset == 0)
            return new Point(ox, oy);

        var ft  = MakeFormattedText(text, GetBlockTypeface(rb.Block), GetBlockFontSize(rb.Block),
                                    _fgBrush ?? Brushes.Gray,
                                    Math.Max(1, _pageWidth - _pageSettings.MarginLeft - _pageSettings.MarginRight));
        int off = Math.Clamp(caret.CharOffset, 0, text.Length);
        if (off == 0) return new Point(ox, oy);

        var geo = ft.BuildHighlightGeometry(new Point(0, 0), Math.Max(0, off - 1), 1);
        double x = geo is not null && !geo.Bounds.IsEmpty ? geo.Bounds.Right : 0;
        return new Point(ox + x, oy);
    }

    /// <summary>Draws selection highlight rects for the current text selection.</summary>
    private void DrawTextSelection(DrawingContext dc, double contentX, double contentW)
    {
        if (_textSelBrush is null || _selection.IsEmpty) return;

        var (start, end) = _selection.Ordered;
        for (int bi = start.BlockIndex; bi <= end.BlockIndex && bi < _blocks.Count; bi++)
        {
            var rb   = _blocks[bi];
            if (rb.IsPageBreak) continue;

            var text = GetFlatText(bi);
            if (string.IsNullOrEmpty(text)) continue;

            int fromChar = bi == start.BlockIndex ? start.CharOffset : 0;
            int toChar   = bi == end.BlockIndex   ? end.CharOffset   : text.Length;
            if (fromChar >= toChar) continue;

            double blockScreenY = PageCanvasPad + rb.Y - _offset.Y;

            // Use glyph-line metrics for accurate highlight geometry when available
            if (rb.GlyphLines is { Count: > 0 })
            {
                DrawGlyphLineSelection(dc, rb.GlyphLines, contentX, blockScreenY, fromChar, toChar);
                continue;
            }

            var ft = rb.FormattedLines is { Count: > 0 }
                ? rb.FormattedLines[0]
                : MakeFormattedText(text, GetBlockTypeface(rb.Block), GetBlockFontSize(rb.Block),
                                    _fgBrush ?? Brushes.Gray, contentW);

            var geo = ft.BuildHighlightGeometry(new Point(contentX, blockScreenY),
                                                fromChar, toChar - fromChar);
            if (geo is null) continue;
            dc.DrawGeometry(_textSelBrush, null, geo);
        }
    }

    /// <summary>
    /// Draws selection highlight over glyph-rendered lines by computing X extents
    /// from per-glyph advance widths. More accurate than FormattedText.BuildHighlightGeometry
    /// because it uses the same metrics as the actual rendered glyphs.
    /// </summary>
    private void DrawGlyphLineSelection(DrawingContext dc,
                                        IReadOnlyList<InlineVisualLine> glyphLines,
                                        double originX, double originY,
                                        int fromChar, int toChar)
    {
        if (_textSelBrush is null) return;

        double lineY = originY;
        foreach (var line in glyphLines)
        {
            double lineH = line.LineHeight;

            // Skip lines completely outside the selection range
            if (line.CharEnd <= fromChar || line.CharStart >= toChar)
            {
                lineY += lineH;
                continue;
            }

            // Compute X start and X end of the selected region within this line
            double xStart = 0, xEnd = 0;
            bool   startSet = false;

            foreach (var seg in line.Segments)
            {
                int segStart = seg.CharStart;
                int segEnd   = segStart + seg.AdvanceWidths.Count;

                double segX = originX + seg.OffsetX;

                double cx = segX;
                for (int gi = 0; gi < seg.AdvanceWidths.Count; gi++)
                {
                    int charIdx = segStart + gi;
                    double adv = seg.AdvanceWidths[gi];

                    if (charIdx >= fromChar && charIdx < toChar)
                    {
                        if (!startSet) { xStart = cx; startSet = true; }
                        xEnd = cx + adv;
                    }
                    cx += adv;
                }
            }

            if (startSet && xEnd > xStart)
                dc.DrawRectangle(_textSelBrush, null,
                    new Rect(xStart, lineY, xEnd - xStart, lineH));

            lineY += lineH;
        }
    }

    /// <summary>Draws the blinking insertion caret — one visual line tall, on the correct wrapped line.</summary>
    /// <summary>
    /// Redraws only the caret layer (<see cref="_caretVisual"/>).
    /// Called by the blink timer and by any state change that moves or shows/hides the caret.
    /// Never triggers a full <see cref="OnRender"/> pass.
    /// </summary>
    private void RefreshCaretVisual()
    {
        using var dc = _caretVisual.RenderOpen();

        if (_caretBrush is null || !_caretVisible || !_selection.IsEmpty) return;
        if (_caret.BlockIndex < 0 || _caret.BlockIndex >= _blocks.Count) return;

        EnsureBrushCache();

        double contentX    = _pageLeft + _pageSettings.MarginLeft;
        double contentW    = Math.Max(1, _pageWidth - _pageSettings.MarginLeft - _pageSettings.MarginRight);
        var rb             = _blocks[_caret.BlockIndex];
        double blockScreenY = PageCanvasPad + rb.Y - _offset.Y;
        double caretX      = contentX;
        double caretY      = blockScreenY;
        double caretH      = _baseFontSize + 2;

        var text = GetFlatText(_caret.BlockIndex);
        if (!string.IsNullOrEmpty(text))
        {
            var ft = (!_caretFtDirty && rb.FormattedLines is { Count: > 0 })
                ? rb.FormattedLines[0]
                : MakeFormattedText(text, GetBlockTypeface(rb.Block), GetBlockFontSize(rb.Block),
                                    _fgBrush ?? Brushes.Gray, contentW);

            int probeChar = Math.Clamp(
                _caret.CharOffset > 0 ? _caret.CharOffset - 1 : 0,
                0, text.Length - 1);
            var geo = ft.BuildHighlightGeometry(new Point(0, 0), probeChar, 1);

            if (geo is not null && !geo.Bounds.IsEmpty)
            {
                caretY = blockScreenY + geo.Bounds.Top;
                caretH = geo.Bounds.Height;
                if (_caret.CharOffset > 0)
                    caretX = contentX + geo.Bounds.Right;
            }
        }

        dc.DrawRectangle(_caretBrush, null, new Rect(caretX, caretY, 2, caretH));
    }

    private void MoveCaretByChar(int delta, bool extend)
    {
        if (_blocks.Count == 0) return;
        int bi   = _caret.BlockIndex;
        int off  = _caret.CharOffset + delta;
        var text = GetFlatText(bi);

        if (off < 0 && bi > 0)  { bi--; text = GetFlatText(bi); off = text.Length; }
        if (off > text.Length && bi < _blocks.Count - 1) { bi++; off = 0; }
        off = Math.Clamp(off, 0, GetFlatText(bi).Length);

        CommitCaret(new TextCaret(bi, off, 0), extend, vertical: false);
    }

    private void MoveCaretByVisualLine(int delta, bool extend)
    {
        if (_blocks.Count == 0) return;
        int  bi    = _caret.BlockIndex;
        int  off   = _caret.CharOffset;
        var  lines = GetVisualLines(bi);

        if (lines.Count == 0)
        {
            int newBi = Math.Clamp(bi + delta, 0, _blocks.Count - 1);
            CommitCaret(new TextCaret(newBi, 0, _caret.PreferredX), extend, vertical: true);
            return;
        }

        int lineIdx  = CaretNavHelper.GetLineIndex(lines, off);
        int targetLi = lineIdx + delta;

        if (targetLi >= 0 && targetLi < lines.Count)
        {
            MoveCaretToVisualLine(bi, lines, targetLi, extend);
            return;
        }

        int newBiV = bi + delta;
        if (newBiV < 0 || newBiV >= _blocks.Count)
        {
            if (delta < 0) CommitCaret(new TextCaret(0, 0, _caret.PreferredX), extend, vertical: true);
            else            CommitCaret(new TextCaret(_blocks.Count - 1, GetFlatText(_blocks.Count - 1).Length, _caret.PreferredX), extend, vertical: true);
            return;
        }

        var newLines = GetVisualLines(newBiV);
        if (newLines.Count == 0)
        {
            CommitCaret(new TextCaret(newBiV, 0, _caret.PreferredX), extend, vertical: true);
            return;
        }
        int entryLi = delta < 0 ? newLines.Count - 1 : 0;
        MoveCaretToVisualLine(newBiV, newLines, entryLi, extend);
    }

    private void MoveCaretToVisualLine(int bi, IReadOnlyList<VisualLine> lines, int lineIdx, bool extend)
    {
        var rb     = _blocks[bi];
        var ft     = rb.FormattedLines![0];
        var ln     = lines[lineIdx];
        int newOff = CaretNavHelper.GetCharAtX(ft, _caret.PreferredX, ln);
        CommitCaret(new TextCaret(bi, newOff, _caret.PreferredX), extend, vertical: true);
    }

    private void MoveCaretToLineEdge(bool toEnd, bool extend)
    {
        if (_blocks.Count == 0) return;
        int  bi    = _caret.BlockIndex;
        int  off   = _caret.CharOffset;
        var  text  = GetFlatText(bi);
        var  lines = GetVisualLines(bi);

        if (lines.Count == 0)
        {
            CommitCaret(new TextCaret(bi, toEnd ? text.Length : 0, 0), extend, vertical: false);
            return;
        }

        int li          = CaretNavHelper.GetLineIndex(lines, off);
        var ln          = lines[li];
        int visualEdge  = toEnd ? ln.End : ln.Start;
        int blockEdge   = toEnd ? text.Length : 0;
        int newOff      = (off == visualEdge) ? blockEdge : visualEdge;
        CommitCaret(new TextCaret(bi, newOff, 0), extend, vertical: false);
    }

    private void MoveCaretToDocumentEdge(bool toEnd, bool extend)
    {
        if (_blocks.Count == 0) return;
        var newCaret = toEnd
            ? new TextCaret(_blocks.Count - 1, GetFlatText(_blocks.Count - 1).Length, 0)
            : new TextCaret(0, 0, 0);
        CommitCaret(newCaret, extend, vertical: false);
    }

    private void MoveCaretByWord(int delta, bool extend)
    {
        if (_blocks.Count == 0) return;
        int bi   = _caret.BlockIndex;
        int off  = _caret.CharOffset;
        var text = GetFlatText(bi);

        if (delta > 0)
        {
            while (off < text.Length && IsWordSeparator(text[off])) off++;
            while (off < text.Length && !IsWordSeparator(text[off])) off++;
        }
        else
        {
            if (off == 0 && bi > 0) { bi--; text = GetFlatText(bi); off = text.Length; }
            if (off > 0 && IsWordSeparator(text[off - 1])) off--;
            while (off > 0 && IsWordSeparator(text[off - 1])) off--;
            while (off > 0 && !IsWordSeparator(text[off - 1])) off--;
        }
        CommitCaret(new TextCaret(bi, off, 0), extend, vertical: false);
    }

    private void MoveCaretByPage(int delta, bool extend)
    {
        if (_blocks.Count == 0) return;
        double pageStep  = _viewport.Height * 0.9;
        double newOffset = Math.Clamp(_offset.Y + delta * pageStep, 0,
                                       Math.Max(0, _extent.Height - _viewport.Height));
        SetVerticalOffset(newOffset);

        double targetY = newOffset + _viewport.Height / 2;
        int bi = _blocks.Count - 1;
        for (int i = 0; i < _blocks.Count; i++)
        {
            if (_blocks[i].IsPageBreak) continue;
            double top = PageCanvasPad + _blocks[i].Y;
            if (targetY >= top && targetY <= top + _blocks[i].Height) { bi = i; break; }
        }

        var lines = GetVisualLines(bi);
        if (lines.Count == 0) { CommitCaret(new TextCaret(bi, 0, _caret.PreferredX), extend, vertical: true); return; }
        var rb    = _blocks[bi];
        double relY = targetY - PageCanvasPad - rb.Y;
        int bestLi = 0; double bestDist = double.MaxValue;
        for (int i = 0; i < lines.Count; i++)
        {
            double d = Math.Abs(lines[i].Top + lines[i].Height / 2 - relY);
            if (d < bestDist) { bestDist = d; bestLi = i; }
        }
        MoveCaretToVisualLine(bi, lines, bestLi, extend);
    }

    private static bool IsWordSeparator(char c) =>
        char.IsWhiteSpace(c) || char.IsPunctuation(c) || char.IsSymbol(c);

    // ── Phase 13 helpers (stubs expanded in Phase 13) ─────────────────────────

    private void DeleteSelectionIfAny()
    {
        if (_selection.IsEmpty || _mutator is null) return;
        var (start, end) = _selection.Ordered;

        if (start.BlockIndex == end.BlockIndex)
        {
            var block = _blocks[start.BlockIndex].Block;
            _mutator.DeleteText(block, start.CharOffset, end.CharOffset - start.CharOffset);
        }
        else
        {
            DeleteMultiBlockSelection(start, end);
        }

        _caret = start with { PreferredX = 0 };
        _selection.Anchor = _caret;
        _selection.Focus  = _caret;
    }

    private void DeleteMultiBlockSelection(TextCaret start, TextCaret end)
    {
        // 1. Delete suffix of first block
        var firstBlock = _blocks[start.BlockIndex].Block;
        int firstLen   = GetFlatText(start.BlockIndex).Length;
        if (start.CharOffset < firstLen)
            _mutator!.DeleteText(firstBlock, start.CharOffset, firstLen - start.CharOffset);

        // 2. Delete prefix of last block
        var lastBlock = _blocks[end.BlockIndex].Block;
        if (end.CharOffset > 0)
            _mutator!.DeleteText(lastBlock, 0, end.CharOffset);

        // 3. Delete middle blocks in reverse order (index stability)
        for (int bi = end.BlockIndex - 1; bi > start.BlockIndex; bi--)
            _mutator!.DeleteBlock(bi);

        // 4. Merge first block with what is now start.BlockIndex + 1
        _mutator!.MergeWithNext(start.BlockIndex);
    }

    private void InsertTextAtCaret(string text)
    {
        if (_mutator is null || _blocks.Count == 0) return;
        int bi  = _caret.BlockIndex;
        var block = _blocks[bi].Block;
        int flatLen = GetFlatText(bi).Length;
        int off = Math.Clamp(_caret.CharOffset, 0, flatLen);
        _mutator.InsertText(block, off, text);
        _caret = _caret with { CharOffset = off + text.Length };
        _selection.Anchor = _caret;
        _selection.Focus  = _caret;
        InvalidateVisual();
    }

    private void SplitBlockAtCaret()
    {
        if (_mutator is null || _blocks.Count == 0) return;
        int bi  = _caret.BlockIndex;
        int off = Math.Clamp(_caret.CharOffset, 0, GetFlatText(bi).Length);
        _mutator.SplitBlock(bi, off);
        // Caret moves to start of newly created block
        _caret = new TextCaret(bi + 1, 0, 0);
        _selection.Anchor = _caret;
        _selection.Focus  = _caret;
        InvalidateVisual();
    }

    // ── Phase 12 public clipboard (Phase 13 fills in DeleteSelectionIfAny) ────

    private string GetSelectedFlatText()
    {
        if (_selection.IsEmpty) return string.Empty;
        var (start, end) = _selection.Ordered;
        var sb = new System.Text.StringBuilder();
        for (int bi = start.BlockIndex; bi <= end.BlockIndex && bi < _blocks.Count; bi++)
        {
            var text = GetFlatText(bi);
            int from = bi == start.BlockIndex ? start.CharOffset : 0;
            int to   = bi == end.BlockIndex   ? end.CharOffset   : text.Length;
            sb.Append(text[from..to]);
            if (bi < end.BlockIndex) sb.Append(Environment.NewLine);
        }
        return sb.ToString();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Dictionary<DocumentBlock, ForensicSeverity> BuildAlertMap()
    {
        var map = new Dictionary<DocumentBlock, ForensicSeverity>();
        if (_model is null) return map;
        foreach (var alert in _model.ForensicAlerts)
        {
            var block = alert.Offset.HasValue ? _model.BinaryMap.BlockAt(alert.Offset.Value) : null;
            if (block is not null && !map.ContainsKey(block))
                map[block] = alert.Severity;
        }
        return map;
    }

    private void OnBlocksChanged(object? sender, EventArgs e)
    {
        _caretFtDirty = true;
        InvalidateBrushCache();
        Dispatcher.InvokeAsync(RebuildLayout);
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _cachedPixelsPerDip = 0; // invalidate on window resize (DPI may have changed)
        RecalcPageGeometry(e.NewSize.Width);
        if (_model is not null) RebuildLayout();
        else InvalidateVisual();
    }

    // ── Editing stubs (wired in Phase 12/13) ─────────────────────────────────

    private DocumentEditor.Core.Editing.DocumentMutator? _mutator;

    /// <summary>Injects the mutator used for all text/block edits (Phase 12+).</summary>
    public void SetMutator(DocumentEditor.Core.Editing.DocumentMutator mutator) =>
        _mutator = mutator;

    // ── Phase 14: Inline formatting ───────────────────────────────────────────

    /// <summary>
    /// Applies a named attribute to the current text selection.
    /// attribute examples: "bold", "italic", "underline", "strikethrough"
    /// </summary>
    public void ApplyFormatToSelection(string attribute, object value)
    {
        if (_selection.IsEmpty || _mutator is null) return;

        bool remove = value is false;

        void Apply(DocumentBlock block, int from, int to)
        {
            if (remove) _mutator.RemoveRunAttribute(block, from, to, attribute);
            else        _mutator.ApplyRunAttribute(block, from, to, attribute, value);
        }

        var (start, end) = _selection.Ordered;
        if (start.BlockIndex == end.BlockIndex)
        {
            Apply(_blocks[start.BlockIndex].Block, start.CharOffset, end.CharOffset);
        }
        else
        {
            Apply(_blocks[start.BlockIndex].Block,
                  start.CharOffset, GetFlatText(start.BlockIndex).Length);
            for (int bi = start.BlockIndex + 1; bi < end.BlockIndex; bi++)
                Apply(_blocks[bi].Block, 0, GetFlatText(bi).Length);
            Apply(_blocks[end.BlockIndex].Block, 0, end.CharOffset);
        }
        InvalidateVisual();
        SelectionFormatChanged?.Invoke(this, EventArgs.Empty);
        // Reclaim keyboard focus so the caret keeps blinking after toolbar interactions
        Focus();
        Keyboard.Focus(this);
        _caretVisible = true;
        if (_blinkTimer is { IsEnabled: false }) _blinkTimer.Start();
        RefreshCaretVisual();
    }

    /// <summary>Returns which formatting attributes are present on all selected runs.</summary>
    /// <summary>
    /// Returns attributes present on ALL runs (or blocks) covered by the selection —
    /// i.e., the intersection so that Bold is active only if every selected char is bold.
    /// </summary>
    public HashSet<string> GetSelectionAttributes()
    {
        if (_blocks.Count == 0) return [];
        var (start, end) = _selection.IsEmpty
            ? (_caret, _caret)
            : _selection.Ordered;

        HashSet<string>? result = null;

        for (int bi = start.BlockIndex; bi <= end.BlockIndex && bi < _blocks.Count; bi++)
        {
            var block = _blocks[bi].Block;
            int charFrom = bi == start.BlockIndex ? start.CharOffset : 0;
            int charTo   = bi == end.BlockIndex   ? end.CharOffset   : GetFlatText(bi).Length;

            if (block.Children.Count == 0)
            {
                // Flat block — use its own attributes
                var keys = block.Attributes.Keys.ToHashSet();
                result = result is null ? keys : result.Intersect(keys).ToHashSet();
            }
            else
            {
                // Run-based block — find which runs overlap the selection range
                int pos = 0;
                foreach (var run in block.Children)
                {
                    int runEnd = pos + run.Text.Length;
                    bool overlaps = pos < charTo && runEnd > charFrom;
                    if (overlaps)
                    {
                        var keys = run.Attributes.Keys.ToHashSet();
                        result = result is null ? keys : result.Intersect(keys).ToHashSet();
                    }
                    pos = runEnd;
                }
            }
        }

        return result ?? [];
    }

    // ── Phase 15: Block-level formatting ─────────────────────────────────────

    /// <summary>Sets a block-level attribute (style, align, listStyle, indent) on the selected block.</summary>
    public void SetBlockAttribute(string attribute, object? value)
    {
        if (_mutator is null || _blocks.Count == 0) return;
        var block = _blocks[_selectedIndex >= 0 ? _selectedIndex : _caret.BlockIndex].Block;
        _mutator.SetBlockAttribute(block, attribute, value);
        InvalidateVisual();
        Focus();
        Keyboard.Focus(this);
    }

    /// <summary>Copies the current text selection to the clipboard.</summary>
    public void CopySelection()
    {
        var text = GetSelectedFlatText();
        if (!string.IsNullOrEmpty(text))
            System.Windows.Clipboard.SetText(text);
    }

    /// <summary>Cuts the current text selection.</summary>
    public void CutSelection()
    {
        if (_isReadOnly) return;
        CopySelection();
        DeleteSelectionIfAny();
    }

    /// <summary>Pastes clipboard text at the caret position.</summary>
    public void PasteAtCaret()
    {
        if (_isReadOnly) return;
        var text = System.Windows.Clipboard.GetText();
        if (!string.IsNullOrEmpty(text))
        {
            DeleteSelectionIfAny();
            InsertTextAtCaret(text);
        }
    }

    /// <summary>Deletes one character or the selection at the caret.</summary>
    public void DeleteAtCaret(bool forward)
    {
        if (_isReadOnly || _mutator is null || _blocks.Count == 0) return;
        if (!_selection.IsEmpty) { DeleteSelectionIfAny(); return; }

        int bi   = _caret.BlockIndex;
        int off  = _caret.CharOffset;
        var block = _blocks[bi].Block;

        if (forward)
        {
            if (off < GetFlatText(bi).Length) _mutator.DeleteText(block, off, 1);
            else if (bi + 1 < _blocks.Count) _mutator.MergeWithNext(bi);
        }
        else
        {
            if (off > 0) { _mutator.DeleteText(block, off - 1, 1); _caret = _caret with { CharOffset = off - 1 }; }
            else if (bi > 0) { _mutator.MergeWithNext(bi - 1); _caret = new TextCaret(bi - 1, GetFlatText(bi - 1).Length, 0); }
        }
        _selection.Anchor = _caret;
        _selection.Focus  = _caret;
    }

    /// <summary>Selects all text in the document.</summary>
    public void SelectAll()
    {
        if (_blocks.Count == 0) return;
        _selection.Anchor = new TextCaret(0, 0, 0);
        _selection.Focus  = new TextCaret(_blocks.Count - 1, GetFlatText(_blocks.Count - 1).Length, 0);
        _caret = _selection.Focus;
        InvalidateVisual();
    }

    // ── Phase 19: Find & Replace highlight support ────────────────────────────

    /// <summary>
    /// Called by <see cref="DocumentSearchViewModel"/> to push find highlights to the renderer.
    /// Active cursor match renders at full opacity; others at 50%.
    /// </summary>
    public void SetFindResults(IReadOnlyList<DocumentSearchMatch> results, int activeCursor)
    {
        _findResults = results;
        _findCursor  = activeCursor;
        InvalidateVisual();
    }

    private void DrawFindHighlights(DrawingContext dc, double contentX, double contentW)
    {
        if (_findResults is null || _findResults.Count == 0 || _findHighlightBrush is null) return;

        var activeBrush = _findHighlightBrush;
        var dimColor    = _findHighlightBrush is SolidColorBrush sb
                            ? sb.Color with { A = 48 }
                            : Color.FromArgb(48, 215, 186, 44);
        var dimBrush    = new SolidColorBrush(dimColor);
        dimBrush.Freeze();

        for (int ri = 0; ri < _findResults.Count; ri++)
        {
            var match = _findResults[ri];
            if (match.BlockIndex < 0 || match.BlockIndex >= _blocks.Count) continue;
            var rb = _blocks[match.BlockIndex];

            double blockScreenY = PageCanvasPad + rb.Y - _offset.Y;
            if (blockScreenY + rb.Height < 0 || blockScreenY > ActualHeight) continue;
            if (rb.FormattedLines is not { Count: > 0 }) continue;

            var ft     = rb.FormattedLines[0];
            var brush  = ri == _findCursor ? activeBrush : dimBrush;
            int length = Math.Max(1, match.EndChar - match.StartChar);
            int start  = Math.Max(0, Math.Min(match.StartChar, ft.Text.Length - 1));

            var geo = ft.BuildHighlightGeometry(new Point(contentX, blockScreenY), start, length);
            if (geo is not null)
                dc.DrawGeometry(brush, null, geo);
        }
    }

}

// ── RenderBlock record ────────────────────────────────────────────────────────

/// <summary>Pre-computed layout entry for a single document block.</summary>
internal sealed record RenderBlock(
    DocumentBlock              Block,
    double                     Y,
    double                     Height,
    double                     SpaceBefore,
    double                     SpaceAfter,
    List<FormattedText>?       FormattedLines,
    bool                       IsPageBreak,
    int                        PageNumber,
    ForensicSeverity?          ForensicSeverity,
    IReadOnlyList<InlineVisualLine>? GlyphLines = null);

// ── Visual-line navigation types ──────────────────────────────────────────────

/// <summary>
/// One visual (wrapped) line within a FormattedText.
/// Start/End are char indices (End is exclusive for iteration, inclusive for caret placement).
/// Top/Height are pixel offsets within the FormattedText's own coordinate space.
/// </summary>
internal readonly record struct VisualLine(int Start, int End, double Top, double Height);

/// <summary>
/// Pure static helpers that extract visual-line structure from a WPF FormattedText
/// by probing BuildHighlightGeometry. Caching lives in the renderer.
/// </summary>
internal static class CaretNavHelper
{
    /// <summary>
    /// Probes every character; groups by Bounds.Top (0.5 px tolerance) into VisualLine list.
    /// The last line's End is always textLen so caret-at-end is always covered.
    /// </summary>
    internal static IReadOnlyList<VisualLine> GetLines(FormattedText ft, int textLen)
    {
        if (textLen == 0) return [new VisualLine(0, 0, 0, ft.Height)];

        var    lines     = new List<VisualLine>();
        int    lineStart = 0;
        double lineTop   = double.NaN;
        double lineH     = ft.Height;

        for (int i = 0; i < textLen; i++)
        {
            var geo = ft.BuildHighlightGeometry(new Point(0, 0), i, 1);
            if (geo is null || geo.Bounds.IsEmpty) continue;
            double top = geo.Bounds.Top;
            double h   = geo.Bounds.Height;

            if (double.IsNaN(lineTop)) { lineTop = top; lineH = h; }
            else if (Math.Abs(top - lineTop) > 0.5)
            {
                lines.Add(new VisualLine(lineStart, i, lineTop, lineH));
                lineStart = i; lineTop = top; lineH = h;
            }
        }
        lines.Add(new VisualLine(lineStart, textLen, double.IsNaN(lineTop) ? 0 : lineTop, lineH));
        return lines;
    }

    /// <summary>Returns the index of the line that contains <paramref name="charOffset"/>.</summary>
    internal static int GetLineIndex(IReadOnlyList<VisualLine> lines, int charOffset)
    {
        // Use strict < for End on all but the last line so that a position exactly at
        // the end of line N (= start of line N+1) is resolved to line N+1, not line N.
        // This prevents Up/Down navigation from skipping visual lines at boundaries.
        for (int i = 0; i < lines.Count - 1; i++)
            if (charOffset >= lines[i].Start && charOffset < lines[i].End) return i;
        return lines.Count - 1;  // last line: charOffset may equal End (= textLen)
    }

    /// <summary>Binary-searches within a visual line for the char position closest to targetX.</summary>
    internal static int GetCharAtX(FormattedText ft, double targetX, VisualLine line)
    {
        int lo = line.Start, hi = line.End;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            var geo = ft.BuildHighlightGeometry(new Point(0, 0), mid, 1);
            if (geo is null || geo.Bounds.IsEmpty) { hi = mid; continue; }
            double midX = (geo.Bounds.Left + geo.Bounds.Right) / 2.0;
            if (midX <= targetX) lo = mid + 1; else hi = mid;
        }
        return Math.Clamp(lo, line.Start, line.End);
    }

    /// <summary>Returns pixel X of the caret at <paramref name="charOffset"/> (probe char before it).</summary>
    internal static double GetCaretX(FormattedText ft, int charOffset, int textLen)
    {
        if (charOffset <= 0 || textLen == 0) return 0;
        int probe = Math.Clamp(charOffset - 1, 0, textLen - 1);
        var geo   = ft.BuildHighlightGeometry(new Point(0, 0), probe, 1);
        return geo is not null && !geo.Bounds.IsEmpty ? geo.Bounds.Right : 0;
    }
}

// ── Phase 12: Cursor types ─────────────────────────────────────────────────────

/// <summary>Identifies a position inside a document block.</summary>
internal record struct TextCaret(int BlockIndex, int CharOffset, double PreferredX);

/// <summary>Tracks the anchor and focus of a text selection range.</summary>
internal sealed class TextSelection
{
    public TextCaret Anchor { get; set; }
    public TextCaret Focus  { get; set; }

    public bool IsEmpty =>
        Anchor.BlockIndex == Focus.BlockIndex &&
        Anchor.CharOffset  == Focus.CharOffset;

    /// <summary>Returns (Start, End) in document order (Anchor may be after Focus).</summary>
    public (TextCaret Start, TextCaret End) Ordered
    {
        get
        {
            bool anchorFirst = Anchor.BlockIndex < Focus.BlockIndex ||
                               (Anchor.BlockIndex == Focus.BlockIndex &&
                                Anchor.CharOffset <= Focus.CharOffset);
            return anchorFirst ? (Anchor, Focus) : (Focus, Anchor);
        }
    }
}
