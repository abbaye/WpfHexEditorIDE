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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Editor.DocumentEditor.Core.Forensic;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Editor.DocumentEditor.Controls;

/// <summary>
/// High-performance DrawingContext-based renderer for document blocks.
/// Implements <see cref="IScrollInfo"/> for virtual viewport scrolling.
/// </summary>
public sealed class DocumentCanvasRenderer : FrameworkElement, IScrollInfo
{
    // ── Constants ────────────────────────────────────────────────────────────

    private const double PageMarginH    = 56.0;   // left + right padding inside page
    private const double PageMarginV    = 40.0;   // top padding
    private const double PageMarginVBot = 56.0;   // bottom padding
    private const double PageCanvasPad  = 32.0;   // space between canvas edge and page
    private const double PageShadowBlur = 12.0;
    private const double PageBreakH     = 40.0;   // height of page-break separator zone
    private const double BlockPadBot    = 10.0;   // extra space below each block
    private const double TableCellPad   = 6.0;

    private const string BodyFontFamily = "Georgia";
    private const string UIFontFamily   = "Segoe UI";

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Raised when the user clicks a block.</summary>
    public event EventHandler<DocumentBlock?>? SelectedBlockChanged;

    /// <summary>Raised when a block is selected — host should show pop-toolbar.</summary>
    public event EventHandler<PopToolbarRequestedArgs>? PopToolbarRequested;

    // ── Fields ────────────────────────────────────────────────────────────────

    // Model
    private DocumentModel?           _model;
    private List<RenderBlock>        _blocks = [];
    private string                   _loadingMessage = "Loading…";
    private bool                     _isLoading  = true;
    private bool                     _isError    = false;

    // Interaction
    private int    _hoverIndex    = -1;
    private int    _selectedIndex = -1;
    private bool   _forensicMode  = false;

    // Layout cache
    private double _totalHeight     = 0;
    private double _pageWidth       = 0;
    private double _pageLeft        = 0;
    private double _zoom            = 1.0;

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
    private Pen?   _pageCardPen, _pageBreakPen, _blockHoverPen, _tableGridPen;

    // Typeface cache
    private Typeface? _bodyFace, _bodyBoldFace, _bodyItalicFace, _bodyBoldItalicFace;
    private Typeface? _uiFace, _uiBoldFace, _monoFace;
    private double    _baseFontSize = 14.0;

    // ── Constructor ──────────────────────────────────────────────────────────

    public DocumentCanvasRenderer()
    {
        ClipToBounds  = true;
        Focusable     = true;
        Cursor        = Cursors.IBeam;

        MouseMove     += OnMouseMove;
        MouseLeave    += OnMouseLeave;
        MouseDown     += OnMouseDown;
        SizeChanged   += OnSizeChanged;
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
    }

    public void SetVerticalOffset(double offset)
    {
        _offset.Y = Math.Clamp(offset, 0, Math.Max(0, _extent.Height - _viewport.Height));
        _scrollOwner?.InvalidateScrollInfo();
        InvalidateVisual();
    }

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

        // ── 2. Page card (full height rectangle) ──────────────────────────
        double pageCardTop = PageCanvasPad - _offset.Y;
        double pageCardH   = _totalHeight + PageMarginV + PageMarginVBot;
        var pageRect = new Rect(_pageLeft, pageCardTop, _pageWidth, pageCardH);

        // Soft shadow (4 layers of increasing blur / decreasing opacity)
        DrawPageShadow(dc, pageRect);
        dc.DrawRectangle(_pageBg, _pageCardPen, pageRect);

        // ── 3. Blocks (virtual: only render what's in viewport) ───────────
        double visTop = _offset.Y - PageCanvasPad;
        double visBot = visTop + vh;

        foreach (var rb in _blocks)
        {
            double blockScreenY = pageCardTop + PageMarginV + rb.Y;

            if (blockScreenY + rb.Height < 0)    continue;  // above viewport
            if (blockScreenY > vh)                break;     // below viewport

            if (rb.IsPageBreak)
            {
                DrawPageBreak(dc, blockScreenY, rb.PageNumber);
                continue;
            }

            // Block highlight (hover / selected)
            var blockRect = new Rect(_pageLeft + PageMarginH - 8,
                                     blockScreenY - 4,
                                     _pageWidth - PageMarginH * 2 + 16,
                                     rb.Height + 8);

            int idx = _blocks.IndexOf(rb);
            if (idx == _selectedIndex)
                dc.DrawRoundedRectangle(_selBrush, null, blockRect, 3, 3);
            else if (idx == _hoverIndex)
                dc.DrawRoundedRectangle(_hoverBrush, _blockHoverPen, blockRect, 3, 3);

            // Kind chip (margin icon)
            DrawKindChip(dc, rb, blockScreenY);

            // Forensic badge
            if (_forensicMode && rb.ForensicSeverity.HasValue)
                DrawForensicBadge(dc, rb, blockScreenY);

            // Text content
            DrawBlock(dc, rb, blockScreenY);
        }

        // ── 4. Page card top border line ──────────────────────────────────
        if (_pageCardPen is not null)
        {
            dc.DrawLine(_pageCardPen,
                new Point(_pageLeft, pageCardTop),
                new Point(_pageLeft + _pageWidth, pageCardTop));
        }
    }

    // ── Rendering helpers ────────────────────────────────────────────────────

    private void DrawLoadingOrError(DrawingContext dc, double vw, double vh)
    {
        var brush = _isError ? Brushes.Salmon : _fgDimBrush;
        var ft = MakeFormattedText(_loadingMessage, _uiFace!, 13, brush ?? Brushes.Gray,
                                   vw - 40);
        dc.DrawText(ft, new Point(20, vh / 2 - ft.Height / 2));
    }

    private void DrawPageShadow(DrawingContext dc, Rect page)
    {
        // Cheap shadow: several offset semi-transparent rects
        for (int i = 4; i >= 1; i--)
        {
            double expand = i * 2.0;
            double opacity = 0.08 * i;
            var shadowBrush = new SolidColorBrush(Color.FromArgb(
                (byte)(opacity * 255), 0, 0, 0));
            shadowBrush.Freeze();
            dc.DrawRectangle(shadowBrush, null,
                new Rect(page.X - expand + 2, page.Y + 2 + i,
                         page.Width + expand * 2, page.Height + expand));
        }
    }

    private void DrawPageBreak(DrawingContext dc, double y, int pageNum)
    {
        double lineY = y + PageBreakH / 2;
        dc.DrawLine(_pageBreakPen!,
            new Point(_pageLeft + 20, lineY),
            new Point(_pageLeft + _pageWidth - 20, lineY));

        if (_pageNumFg is not null)
        {
            var label = MakeFormattedText($"— Page {pageNum} —", _uiFace!, 11, _pageNumFg, 200);
            dc.DrawText(label, new Point(
                _pageLeft + _pageWidth / 2 - label.Width / 2,
                lineY - label.Height - 3));
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

        var dot = new Rect(_pageLeft + _pageWidth - PageMarginH - 2, y + 4, 8, 8);
        dc.DrawEllipse(brush, null,
            new Point(dot.X + dot.Width / 2, dot.Y + dot.Height / 2), 4, 4);
    }

    private void DrawBlock(DrawingContext dc, RenderBlock rb, double y)
    {
        double x = _pageLeft + PageMarginH;
        double maxW = _pageWidth - PageMarginH * 2;

        if (rb.Block.Kind == "table")
        {
            DrawTable(dc, rb, x, y, maxW);
            return;
        }

        if (rb.Block.Kind == "image")
        {
            DrawImagePlaceholder(dc, rb, x, y, maxW);
            return;
        }

        // Paragraph / heading / run / default
        if (rb.FormattedLines is { Count: > 0 })
        {
            double lineY = y;
            foreach (var ft in rb.FormattedLines)
            {
                dc.DrawText(ft, new Point(x, lineY));
                lineY += ft.Height + 2;
            }
        }
    }

    private void DrawTable(DrawingContext dc, RenderBlock rb, double x, double y, double maxW)
    {
        if (rb.Block.Children.Count == 0) return;

        int cols = rb.Block.Children.Max(r => r.Children.Count);
        if (cols == 0) return;

        double colW   = Math.Min(maxW / cols, 200);
        double rowH   = 24;
        double tableW = colW * cols;

        // Table border
        dc.DrawRectangle(null, _tableGridPen!,
            new Rect(x, y, tableW, rb.Block.Children.Count * rowH));

        int row = 0;
        foreach (var rowBlock in rb.Block.Children.Where(c => c.Kind == "table-row"))
        {
            int col = 0;
            foreach (var cell in rowBlock.Children)
            {
                var cellRect = new Rect(x + col * colW, y + row * rowH, colW, rowH);
                // Cell border
                dc.DrawRectangle(null, _tableGridPen!, cellRect);

                // Cell text
                var ft = MakeFormattedText(cell.Text, _uiFace!, 12, _fgBrush!, colW - TableCellPad * 2);
                dc.DrawText(ft, new Point(cellRect.X + TableCellPad,
                                          cellRect.Y + (rowH - ft.Height) / 2));
                col++;
            }
            row++;
        }
    }

    private void DrawImagePlaceholder(DrawingContext dc, RenderBlock rb, double x, double y, double maxW)
    {
        var placeholderRect = new Rect(x, y, Math.Min(maxW, 200), 40);
        dc.DrawRoundedRectangle(_kindChipBg, _tableGridPen!, placeholderRect, 3, 3);

        var ft = MakeFormattedText($"  ⬛ Image @ 0x{rb.Block.RawOffset:X}",
                                   _uiFace!, 12, _imageFg ?? _fgDimBrush!, maxW);
        dc.DrawText(ft, new Point(x + 8, y + (40 - ft.Height) / 2));
    }

    // ── Layout ────────────────────────────────────────────────────────────────

    private void RebuildLayout()
    {
        if (_model is null) { InvalidateVisual(); return; }

        EnsureTypefaces();

        double maxW = Math.Max(100, _pageWidth - PageMarginH * 2);
        double y    = 0;
        int pageNum = 1;
        var result  = new List<RenderBlock>();

        // Build alert map for forensic badges
        var alertMap = BuildAlertMap();

        foreach (var block in _model.Blocks)
        {
            var rb = BuildRenderBlock(block, y, maxW, alertMap);
            result.Add(rb);
            y += rb.Height + BlockPadBot;

            // Insert page break after section blocks
            if (block.Kind == "section")
            {
                pageNum++;
                var pbRb = new RenderBlock(
                    Block: block, Y: y, Height: PageBreakH,
                    FormattedLines: null, IsPageBreak: true, PageNumber: pageNum,
                    ForensicSeverity: null);
                result.Add(pbRb);
                y += PageBreakH;
            }
        }

        _blocks      = result;
        _totalHeight = y;

        UpdateScrollExtent();
        InvalidateVisual();
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
                int level = int.TryParse(
                    block.Attributes.GetValueOrDefault("level") as string, out int l) ? l : 1;
                double fs  = level == 1 ? 22 : level == 2 ? 18 : 15;
                double topM = level == 1 ? 16 : 10;
                var face  = _bodyBoldFace!;
                var lines = WrapText(block.Text, face, fs, maxW, _fgBrush!);
                double h  = lines.Sum(t => t.Height + 2) + topM;
                return new RenderBlock(block, y + topM, h, lines, false, 0, severity);
            }

            case "paragraph":
            case "run":
            {
                var lines = BuildInlineFormattedText(block, maxW);
                double h  = lines.Sum(t => t.Height + 2) + 4;
                return new RenderBlock(block, y, h, lines, false, 0, severity);
            }

            case "table":
            {
                int rows = block.Children.Count(c => c.Kind == "table-row");
                double h = Math.Max(24, rows * 24 + 4);
                return new RenderBlock(block, y, h, null, false, 0, severity);
            }

            case "image":
                return new RenderBlock(block, y, 48, null, false, 0, severity);

            default:
            {
                if (string.IsNullOrEmpty(block.Text))
                    return new RenderBlock(block, y, 8, null, false, 0, severity);

                var lines = WrapText(block.Text, _bodyFace!, _baseFontSize, maxW, _fgBrush!);
                double h  = lines.Sum(t => t.Height + 2) + 4;
                return new RenderBlock(block, y, h, lines, false, 0, severity);
            }
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
                if (run.Attributes.TryGetValue("fontSize",  out var fs) && fs is int sz)
                    ft.SetFontSize(sz, pos, len);

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
        double available = viewWidth - PageCanvasPad * 2;
        _pageWidth = Math.Clamp(available, 400, 900);
        _pageLeft  = (viewWidth - _pageWidth) / 2;
    }

    private void UpdateScrollExtent()
    {
        double pageH = _totalHeight + PageMarginV + PageMarginVBot + PageCanvasPad * 2;
        _extent  = new Size(Math.Max(_viewport.Width, _pageWidth + PageCanvasPad * 2), pageH);
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

        _canvasBg     = GetBrush("DE_PageCanvasBackground", Color.FromRgb(42, 42, 42));
        _pageBg       = GetBrush("DE_PageBackground",       Color.FromRgb(30, 30, 30));
        _fgBrush      = GetBrush("DE_TextPaneForeground",   Color.FromRgb(212, 212, 212));
        _fgDimBrush   = new SolidColorBrush((_fgBrush is SolidColorBrush sb
                             ? sb.Color : Color.FromRgb(212, 212, 212)) with { A = 140 });
        ((SolidColorBrush)_fgDimBrush).Freeze();

        _hoverBrush   = GetBrush("DE_BlockHoverBrush",      Color.FromArgb(32, 86, 156, 214));
        _selBrush     = GetBrush("DE_SelectedBlockBrush",   Color.FromArgb(80, 78, 201, 176));
        _pageBreakBrush = GetBrush("DE_PageShadowBrush",    Color.FromArgb(24, 0, 0, 0));
        _kindChipBg   = GetBrush("DE_ToolbarButtonActiveBrush", Color.FromRgb(64, 64, 64));
        _kindChipFg   = GetBrush("DE_StructureKindFg",      Color.FromRgb(78, 201, 176));
        _headingFg    = GetBrush("DE_StructureNodeFg",      Color.FromRgb(156, 220, 254));
        _forensicErrBrush  = GetBrush("DE_ForensicBadgeErrorBg", Color.FromRgb(204, 51, 51));
        _forensicWarnBrush = GetBrush("DE_ForensicBadgeWarnBg",  Color.FromRgb(204, 122, 0));
        _pageNumFg    = _fgDimBrush;
        _imageFg      = GetBrush("DE_StructureKindFg",      Color.FromRgb(78, 201, 176));
        _tableBorderBrush = GetBrush("DE_ToolbarBorderBrush", Color.FromRgb(60, 60, 60));

        // Page card border
        var borderColor = _tableBorderBrush is SolidColorBrush tb ? tb.Color : Color.FromRgb(60,60,60);
        _pageCardPen = new Pen(new SolidColorBrush(Color.FromArgb(80, borderColor.R, borderColor.G, borderColor.B)), 1);
        _pageCardPen.Freeze();

        // Page break line
        _pageBreakPen = new Pen(_pageBreakBrush, 1) { DashStyle = DashStyles.Dash };
        _pageBreakPen.Freeze();

        // Block hover border
        var hoverColor = _hoverBrush is SolidColorBrush hb ? hb.Color : Color.FromArgb(60, 86, 156, 214);
        _blockHoverPen = new Pen(new SolidColorBrush(Color.FromArgb(80, hoverColor.R, hoverColor.G, hoverColor.B)), 1);
        _blockHoverPen.Freeze();

        // Table grid
        _tableGridPen = new Pen(_tableBorderBrush, 0.5);
        _tableGridPen.Freeze();

        _pageBorder = _pageCardPen.Brush;
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
        int idx = HitTestBlock(e.GetPosition(this));
        if (idx == _hoverIndex) return;
        _hoverIndex = idx;
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
        int idx = HitTestBlock(e.GetPosition(this));
        if (idx < 0 || idx >= _blocks.Count) { _selectedIndex = -1; return; }

        _selectedIndex = idx;
        var block = _blocks[idx].Block;
        SelectedBlockChanged?.Invoke(this, block);

        // Raise pop-toolbar at click position
        var pt = e.GetPosition(this);
        PopToolbarRequested?.Invoke(this, new PopToolbarRequestedArgs(
            new Rect(pt.X, pt.Y - 36, 0, 0), block));

        InvalidateVisual();
        e.Handled = true;
    }

    private int HitTestBlock(Point pt)
    {
        double pageCardTop = PageCanvasPad - _offset.Y;
        double contentTop  = pageCardTop + PageMarginV;

        for (int i = 0; i < _blocks.Count; i++)
        {
            var rb = _blocks[i];
            if (rb.IsPageBreak) continue;

            double blockScreenY = contentTop + rb.Y;
            double blockScreenX = _pageLeft + PageMarginH - 8;
            double blockW       = _pageWidth - PageMarginH * 2 + 16;

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
        double pageCardTop = PageCanvasPad;
        double contentTop  = pageCardTop + PageMarginV;
        double blockY      = contentTop + _blocks[idx].Y;

        if (blockY < _offset.Y)
            SetVerticalOffset(blockY - 20);
        else if (blockY + _blocks[idx].Height > _offset.Y + _viewport.Height)
            SetVerticalOffset(blockY + _blocks[idx].Height - _viewport.Height + 20);

        InvalidateVisual();
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
        InvalidateBrushCache();
        Dispatcher.InvokeAsync(RebuildLayout);
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RecalcPageGeometry(e.NewSize.Width);
        if (_model is not null) RebuildLayout();
        else InvalidateVisual();
    }
}

// ── RenderBlock record ────────────────────────────────────────────────────────

/// <summary>Pre-computed layout entry for a single document block.</summary>
internal sealed record RenderBlock(
    DocumentBlock         Block,
    double                Y,
    double                Height,
    List<FormattedText>?  FormattedLines,
    bool                  IsPageBreak,
    int                   PageNumber,
    ForensicSeverity?     ForensicSeverity);
