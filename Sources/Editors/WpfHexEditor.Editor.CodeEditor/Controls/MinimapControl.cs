//////////////////////////////////////////////
// Project: WpfHexEditor.Editor.CodeEditor
// File: Controls/MinimapControl.cs
// Description:
//     VS Code-style code overview minimap. Renders a compressed view of the
//     entire document using tiny colored rectangles per syntax token.
//     Supports character-level rendering, hover highlight, viewport slider,
//     side placement, and a VS Code-matching context menu.
// Architecture:
//     Standalone FrameworkElement — placed beside the CodeEditor in SplitHost.
//     Reads document lines + syntax tokens from the attached CodeEditor.
//     Renders via DrawingContext (zero WPF containers — same pattern as
//     BinaryDiffCanvas and BarChartPanel).
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Core.ProjectSystem.Languages;
using WpfHexEditor.Editor.CodeEditor.Helpers;

namespace WpfHexEditor.Editor.CodeEditor.Controls;

// ── Enums ────────────────────────────────────────────────────────────────────

public enum MinimapVerticalSize { Proportional, Fill, Fit }
public enum MinimapSliderMode { Always, MouseOver }
public enum MinimapSide { Left, Right }

// ── MinimapControl ───────────────────────────────────────────────────────────

/// <summary>
/// VS Code-style minimap overview of the code document. Each line is rendered
/// as a row of colored rectangles matching syntax token colors when
/// <see cref="RenderCharacters"/> is true; otherwise falls back to a single
/// heuristic-colored rect per line.
/// </summary>
public sealed class MinimapControl : FrameworkElement
{
    private CodeEditor? _editor;

    // ── Layout constants ─────────────────────────────────────────────────────

    private const double RowHeight = 3.0;
    private const double CharWidth = 0.85;
    private const int MaxVisibleChars = 120;
    private const double LeftPad = 4.0;
    private const double RowFillRatio = 0.55; // 55% fill, 45% gap — visible line spacing

    // ── Static frozen brushes ────────────────────────────────────────────────

    private static readonly Brush ViewportBrush;
    private static readonly Brush ViewportBorderBrush;
    private static readonly Pen ViewportPen;
    private static readonly Brush HoverBandBrush;
    private static readonly Brush DarkenOverlay;
    private static readonly Brush DefaultTextBrush;

    static MinimapControl()
    {
        ViewportBrush = Freeze(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)));
        ViewportBorderBrush = Freeze(new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)));
        ViewportPen = FreezePen(new Pen(ViewportBorderBrush, 1.0));
        HoverBandBrush = Freeze(new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)));
        DarkenOverlay = Freeze(new SolidColorBrush(Color.FromArgb(25, 0, 0, 0)));
        DefaultTextBrush = Freeze(new SolidColorBrush(Color.FromArgb(130, 200, 200, 200)));
    }

    private static Brush Freeze(SolidColorBrush b) { b.Freeze(); return b; }
    private static Pen FreezePen(Pen p) { p.Freeze(); return p; }

    // ── Token kind → theme key mapping ───────────────────────────────────────

    // Indexed by (int)SyntaxTokenKind. null = use fallback brush.
    private static readonly string?[] TokenKindToThemeKey =
    {
        null,            // Default
        "CE_Keyword",    // Keyword
        "CE_String",     // String
        "CE_Number",     // Number
        "CE_Comment",    // Comment
        "CE_Identifier", // Identifier
        "CE_Operator",   // Operator
        "CE_Bracket",    // Bracket
        "CE_Type",       // Type
        "CE_Attribute",  // Attribute
        "CE_Keyword",    // ControlFlow → reuse keyword
    };

    // Per-kind alpha multiplier (0.0–1.0). Faded silhouette — not full-color.
    private static readonly double[] TokenKindAlpha =
    {
        0.30, // Default      — barely visible
        0.60, // Keyword      — visible but soft
        0.55, // String       — moderate
        0.45, // Number       — subtle
        0.25, // Comment      — nearly invisible (VS Code style)
        0.30, // Identifier   — barely visible
        0.25, // Operator     — very subtle
        0.25, // Bracket      — very subtle
        0.55, // Type         — moderate
        0.45, // Attribute    — subtle
        0.60, // ControlFlow  — same as keyword
    };

    // Theme-resolved, alpha-blended brush cache. Invalidated on theme change.
    // Cache key encodes both theme key AND alpha: "CE_Keyword:0.85"
    private readonly Dictionary<string, Brush> _themeAlphaBrushCache = new();

    // Cached separator pen (rebuilt on theme change)
    private Pen? _separatorPen;

    // ── Public properties ────────────────────────────────────────────────────

    /// <summary>Width of the minimap in pixels.</summary>
    public double MinimapWidth { get; set; } = 100;

    private bool _renderCharacters = true;
    /// <summary>When true, renders per-token colored blocks. When false, one rect per line.</summary>
    public bool RenderCharacters
    {
        get => _renderCharacters;
        set { if (_renderCharacters != value) { _renderCharacters = value; InvalidateVisual(); } }
    }

    private MinimapVerticalSize _verticalSize = MinimapVerticalSize.Proportional;
    /// <summary>Controls how the minimap scales vertically.</summary>
    public MinimapVerticalSize VerticalSize
    {
        get => _verticalSize;
        set { if (_verticalSize != value) { _verticalSize = value; InvalidateVisual(); } }
    }

    private MinimapSliderMode _sliderMode = MinimapSliderMode.Always;
    /// <summary>Controls when the viewport slider is visible.</summary>
    public MinimapSliderMode SliderMode
    {
        get => _sliderMode;
        set { if (_sliderMode != value) { _sliderMode = value; InvalidateVisual(); } }
    }

    private MinimapSide _side = MinimapSide.Right;
    /// <summary>Minimap placement side (Left or Right). Fires <see cref="SideChangeRequested"/>.</summary>
    public MinimapSide Side
    {
        get => _side;
        set { if (_side != value) { _side = value; } }
    }

    // ── Events ───────────────────────────────────────────────────────────────

    /// <summary>Fired when user toggles minimap visibility from context menu.</summary>
    public event Action<bool>? MinimapToggled;

    /// <summary>Fired when user changes minimap side from context menu.</summary>
    public event Action<MinimapSide>? SideChangeRequested;

    // ── Hover state ──────────────────────────────────────────────────────────

    private bool _isMouseOver;
    private double _hoverY = -1;
    private bool _isDragging;
    private double _lastMinimapScrollOffset;

    // ── Constructor ──────────────────────────────────────────────────────────

    public MinimapControl()
    {
        InitializeContextMenu();
    }

    // ── Editor attachment ────────────────────────────────────────────────────

    /// <summary>Attaches to a CodeEditor for document and viewport tracking.</summary>
    public void SetEditor(CodeEditor editor)
    {
        _editor = editor;
        InvalidateVisual();
    }

    /// <summary>Called by the host when the document or viewport changes.</summary>
    public void Refresh() => InvalidateVisual();

    /// <summary>Clears the cached alpha brushes — call on theme change.</summary>
    public void InvalidateThemeCache()
    {
        _themeAlphaBrushCache.Clear();
        _separatorPen = null;
        InvalidateVisual();
    }

    // ── Measure ──────────────────────────────────────────────────────────────

    protected override Size MeasureOverride(Size availableSize)
    {
        var h = double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height;
        return new Size(MinimapWidth, h);
    }

    // ── Render ───────────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w < 1 || h < 1 || _editor is null) return;

        // Background — base + darken overlay for visual separation from editor
        var bgBrush = TryFindResource("TE_Background") as Brush ?? Brushes.Black;
        dc.DrawRectangle(bgBrush, null, new Rect(0, 0, w, h));
        dc.DrawRectangle(DarkenOverlay, null, new Rect(0, 0, w, h));

        var doc = _editor.Document;
        if (doc is null || doc.Lines.Count == 0) return;

        int totalLines = doc.Lines.Count;
        var folding = _editor.FoldingEngine;
        int hiddenLineCount = folding?.TotalHiddenLineCount ?? 0;
        int visibleLineCount = Math.Max(1, totalLines - hiddenLineCount);
        double scale = ComputeScale(visibleLineCount, h);

        // Read pixel-exact values from VE — no RenderBuffer inflation.
        // scrollRatio matches the scrollbar exactly.
        double minimapScrollOffset = 0;
        double scrollRatio = 0;
        int actualVisibleLines = 0;
        double veLineHeight = 20;

        if (_editor.VirtualizationEngine is { } ve && ve.TotalLines > 0)
        {
            veLineHeight = ve.LineHeight > 0 ? ve.LineHeight : 20;
            double maxScrollOffset = Math.Max(1, _editor.MaxScrollOffset);
            scrollRatio = Math.Clamp(ve.ScrollOffset / maxScrollOffset, 0, 1);
            actualVisibleLines = (int)Math.Ceiling(ve.ViewportHeight / veLineHeight);
        }

        double totalMinimapHeight = visibleLineCount * scale;
        if (totalMinimapHeight > h)
        {
            double minimapMaxScrollLines = (totalMinimapHeight - h) / scale;
            minimapScrollOffset = scrollRatio * minimapMaxScrollLines;
        }

        _lastMinimapScrollOffset = minimapScrollOffset;

        int firstVisibleIdx = (int)minimapScrollOffset;
        int maxVisibleIdx = firstVisibleIdx + (int)(h / scale) + 2;

        // Effective row drawing height (with gap)
        double drawH = Math.Max(scale * RowFillRatio, 0.5);

        // Active highlighter for on-demand fallback (whfmt-driven, not hardcoded)
        var highlighter = _editor.ActiveHighlighter;

        // Draw lines — iterate all absolute lines, skip hidden, use visibleIdx for Y.
        // O(totalLines) is acceptable for a minimap (DrawingContext, no allocations per line).
        int visibleIdx = 0;
        for (int i = 0; i < totalLines; i++)
        {
            if (folding?.IsLineHidden(i) == true) continue;

            int vIdx = visibleIdx++;
            if (vIdx < firstVisibleIdx) continue;
            if (vIdx >= maxVisibleIdx) break;

            var line = doc.Lines[i];
            if (line.Text is null || line.Text.Length == 0) continue;

            double y = (vIdx - minimapScrollOffset) * scale;
            if (y + scale < 0) continue;
            if (y > h) break;

            // Try cached tokens first, then on-demand highlight, then neutral fallback
            var lineTokens = (line.TokensCache is { } cached && !line.IsCacheDirty && cached.Count > 0)
                ? cached
                : null;

            // On-demand fallback: use the editor's active highlighter (whfmt-driven)
            if (lineTokens is null && _renderCharacters && highlighter is not null)
            {
                try { lineTokens = highlighter.Highlight(line.Text, i) as List<SyntaxHighlightToken>
                        ?? new List<SyntaxHighlightToken>(highlighter.Highlight(line.Text, i)); }
                catch { /* highlighter not ready */ }
            }

            if (_renderCharacters && lineTokens is { Count: > 0 })
            {
                // Character-level rendering: one rect per syntax token
                for (int t = 0; t < lineTokens.Count; t++)
                {
                    var token = lineTokens[t];
                    double x = LeftPad + token.StartColumn * CharWidth;
                    if (x >= w - 2) break;
                    double tw = token.Length * CharWidth;
                    tw = Math.Min(tw, w - x - 2);
                    if (tw <= 0) continue;

                    var brush = ResolveTokenBrush(token.Kind, token.Foreground);
                    dc.DrawRectangle(brush, null, new Rect(x, y, tw, drawH));
                }
            }
            else
            {
                // Neutral fallback: visible line shape, no language-specific heuristic
                int chars = Math.Min(line.Text.Length, MaxVisibleChars);
                double lineWidth = chars * CharWidth;
                dc.DrawRectangle(DefaultTextBrush, null,
                    new Rect(LeftPad, y, Math.Min(lineWidth, w - LeftPad - 2), drawH));
            }
        }

        // Content height — slider and hover band are clamped to this
        double contentHeight = Math.Min(totalMinimapHeight, h);

        // Hover highlight band — uses actualVisibleLines (no buffer)
        if (_isMouseOver && !_isDragging && _hoverY >= 0 && actualVisibleLines > 0)
        {
            double bandH      = Math.Max((actualVisibleLines / (double)visibleLineCount) * contentHeight, 10);
            double maxBandTop = Math.Max(contentHeight - bandH, 0);  // guard: bandH can exceed contentHeight on tiny documents
            double bandTop    = Math.Clamp(_hoverY - bandH / 2, 0, maxBandTop);
            dc.DrawRectangle(HoverBandBrush, null, new Rect(0, bandTop, w, bandH));
        }

        // Viewport slider — uses scrollRatio (pixel-exact, matches scrollbar)
        bool showSlider = _sliderMode == MinimapSliderMode.Always || _isMouseOver;
        if (showSlider && actualVisibleLines > 0)
        {
            double vpHeight = Math.Max((actualVisibleLines / (double)visibleLineCount) * contentHeight, 10);
            double sliderRange = Math.Max(contentHeight - vpHeight, 1);
            double vpTop = scrollRatio * sliderRange;

            dc.DrawRectangle(ViewportBrush, ViewportPen, new Rect(0, vpTop, w, vpHeight));
        }

        // Separator line on the editor-facing edge
        var sepPen = GetSeparatorPen();
        double sepX = (_side == MinimapSide.Right) ? 0.5 : w - 0.5;
        dc.DrawLine(sepPen, new Point(sepX, 0), new Point(sepX, h));
    }

    // ── Scale computation ────────────────────────────────────────────────────

    private double ComputeScale(int totalLines, double viewportHeight)
    {
        if (totalLines <= 0) return RowHeight;

        // MinRowHeight ensures drawH (scale × RowFillRatio) is ≥ 1px,
        // preventing sub-pixel aliasing on large files. Overflow is handled
        // by the proportional scroll in OnRender.
        const double MinRowHeight = 1.0 / RowFillRatio; // ≈ 1.82

        return _verticalSize switch
        {
            MinimapVerticalSize.Fill => viewportHeight / totalLines,
            MinimapVerticalSize.Fit => Math.Clamp(viewportHeight / totalLines, MinRowHeight, RowHeight),
            _ /* Proportional */ => Math.Clamp(viewportHeight / totalLines, MinRowHeight, RowHeight),
        };
    }

    // ── Token brush resolution ───────────────────────────────────────────────

    private Brush ResolveTokenBrush(SyntaxTokenKind kind, Brush fallback)
    {
        int idx = (int)kind;
        double alpha = (idx >= 0 && idx < TokenKindAlpha.Length) ? TokenKindAlpha[idx] : 0.55;

        if (idx < 0 || idx >= TokenKindToThemeKey.Length)
            return ApplyAlpha(fallback, alpha);

        var key = TokenKindToThemeKey[idx];
        if (key is null)
            return ApplyAlpha(fallback ?? DefaultTextBrush, alpha);

        // Cache key includes alpha so different kinds sharing the same theme key
        // (e.g. Keyword and ControlFlow → CE_Keyword) get separate entries if alpha differs.
        var cacheKey = $"{key}:{alpha:F2}";
        if (_themeAlphaBrushCache.TryGetValue(cacheKey, out var cached))
            return cached;

        if (TryFindResource(key) is SolidColorBrush themeBrush)
        {
            var c = themeBrush.Color;
            var a = (byte)(c.A * alpha);
            var result = new SolidColorBrush(Color.FromArgb(a, c.R, c.G, c.B));
            result.Freeze();
            _themeAlphaBrushCache[cacheKey] = result;
            return result;
        }

        var fb = ApplyAlpha(fallback ?? DefaultTextBrush, alpha);
        _themeAlphaBrushCache[cacheKey] = fb;
        return fb;
    }

    private static Brush ApplyAlpha(Brush brush, double alpha)
    {
        if (brush is SolidColorBrush scb)
        {
            var c = scb.Color;
            var a = (byte)(c.A * alpha);
            var result = new SolidColorBrush(Color.FromArgb(a, c.R, c.G, c.B));
            result.Freeze();
            return result;
        }
        return brush;
    }

    private Pen GetSeparatorPen()
    {
        if (_separatorPen is not null) return _separatorPen;
        var brush = TryFindResource("CE_LineNumFg") as Brush ?? ViewportBorderBrush;
        // Make it subtle — 50% opacity
        if (brush is SolidColorBrush scb)
        {
            var c = scb.Color;
            brush = new SolidColorBrush(Color.FromArgb((byte)(c.A * 0.3), c.R, c.G, c.B));
            ((SolidColorBrush)brush).Freeze();
        }
        _separatorPen = new Pen(brush, 1.0);
        _separatorPen.Freeze();
        return _separatorPen;
    }

    // ── Mouse interaction ────────────────────────────────────────────────────

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        _isMouseOver = true;
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _isMouseOver = false;
        _hoverY = -1;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        _isDragging = true;
        NavigateToY(e.GetPosition(this).Y);
        CaptureMouse();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var pos = e.GetPosition(this);

        if (e.LeftButton == MouseButtonState.Pressed && IsMouseCaptured)
        {
            NavigateToY(pos.Y);
        }
        else
        {
            _hoverY = pos.Y;
            InvalidateVisual();
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        _isDragging = false;
        ReleaseMouseCapture();
        InvalidateVisual();
    }

    private void NavigateToY(double y)
    {
        if (_editor?.Document is null || _editor.VirtualizationEngine is not { } ve) return;
        int totalLines = _editor.Document.Lines.Count;
        int hiddenLines = _editor.FoldingEngine?.TotalHiddenLineCount ?? 0;
        int visibleLineCount = Math.Max(1, totalLines - hiddenLines);
        double h = ActualHeight;
        double scale = ComputeScale(visibleLineCount, h);
        double veLineHeight = ve.LineHeight > 0 ? ve.LineHeight : 20;
        int actualVisibleLines = (int)Math.Ceiling(ve.ViewportHeight / veLineHeight);
        if (visibleLineCount <= 0 || actualVisibleLines <= 0) return;

        // Same formula as slider rendering — exact inverse.
        double contentHeight = Math.Min(visibleLineCount * scale, h);
        double vpHeight = Math.Max((actualVisibleLines / (double)visibleLineCount) * contentHeight, 10);
        double sliderRange = Math.Max(contentHeight - vpHeight, 1);

        // Center the slider on the click position
        double vpTop = Math.Clamp(y - vpHeight / 2, 0, sliderRange);
        double scrollRatio = vpTop / sliderRange;

        // Apply ratio to pixel-based max scroll — exact same range as scrollbar.
        // Use ScrollViewToOffset for sub-line precision (no quantization loss).
        double maxScrollOffset = Math.Max(1, _editor.MaxScrollOffset);
        double targetOffset = scrollRatio * maxScrollOffset;
        _editor.ScrollViewToOffset(targetOffset);
    }

    // ── Context menu ─────────────────────────────────────────────────────────

    private MenuItem? _minimapToggleItem;
    private MenuItem? _renderCharsItem;
    private MenuItem? _vProp, _vFill, _vFit;
    private MenuItem? _sAlways, _sMouseOver;
    private MenuItem? _sideLeft, _sideRight;

    private void InitializeContextMenu()
    {
        var menu = new ContextMenu();

        // Minimap toggle
        _minimapToggleItem = new MenuItem { Header = "_Minimap", IsCheckable = true, IsChecked = true };
        _minimapToggleItem.Click += (_, _) =>
        {
            MinimapToggled?.Invoke(false);
        };
        menu.Items.Add(_minimapToggleItem);

        menu.Items.Add(new Separator());

        // Render Characters toggle
        _renderCharsItem = new MenuItem { Header = "Render _Characters", IsCheckable = true, IsChecked = _renderCharacters };
        _renderCharsItem.Click += (_, _) =>
        {
            RenderCharacters = _renderCharsItem.IsChecked;
        };
        menu.Items.Add(_renderCharsItem);

        // Vertical size submenu
        var vSizeMenu = new MenuItem { Header = "_Vertical size" };
        _vProp = new MenuItem { Header = "Proportional" };
        _vFill = new MenuItem { Header = "Fill" };
        _vFit = new MenuItem { Header = "Fit" };
        _vProp.Click += (_, _) => VerticalSize = MinimapVerticalSize.Proportional;
        _vFill.Click += (_, _) => VerticalSize = MinimapVerticalSize.Fill;
        _vFit.Click += (_, _) => VerticalSize = MinimapVerticalSize.Fit;
        vSizeMenu.Items.Add(_vProp);
        vSizeMenu.Items.Add(_vFill);
        vSizeMenu.Items.Add(_vFit);
        menu.Items.Add(vSizeMenu);

        // Slider submenu
        var sliderMenu = new MenuItem { Header = "S_lider" };
        _sAlways = new MenuItem { Header = "Always" };
        _sMouseOver = new MenuItem { Header = "Mouse Over" };
        _sAlways.Click += (_, _) => SliderMode = MinimapSliderMode.Always;
        _sMouseOver.Click += (_, _) => SliderMode = MinimapSliderMode.MouseOver;
        sliderMenu.Items.Add(_sAlways);
        sliderMenu.Items.Add(_sMouseOver);
        menu.Items.Add(sliderMenu);

        // Side submenu
        var sideMenu = new MenuItem { Header = "Si_de" };
        _sideLeft = new MenuItem { Header = "Left" };
        _sideRight = new MenuItem { Header = "Right" };
        _sideLeft.Click += (_, _) => { Side = MinimapSide.Left; SideChangeRequested?.Invoke(MinimapSide.Left); };
        _sideRight.Click += (_, _) => { Side = MinimapSide.Right; SideChangeRequested?.Invoke(MinimapSide.Right); };
        sideMenu.Items.Add(_sideLeft);
        sideMenu.Items.Add(_sideRight);
        menu.Items.Add(sideMenu);

        // Sync check states when menu opens
        menu.Opened += (_, _) => SyncContextMenuChecks();

        ContextMenu = menu;
    }

    private void SyncContextMenuChecks()
    {
        if (_minimapToggleItem is not null) _minimapToggleItem.IsChecked = true; // visible if context menu shows
        if (_renderCharsItem is not null) _renderCharsItem.IsChecked = _renderCharacters;

        // Radio-style checks for submenus
        SetRadio(_vProp, _verticalSize == MinimapVerticalSize.Proportional);
        SetRadio(_vFill, _verticalSize == MinimapVerticalSize.Fill);
        SetRadio(_vFit, _verticalSize == MinimapVerticalSize.Fit);

        SetRadio(_sAlways, _sliderMode == MinimapSliderMode.Always);
        SetRadio(_sMouseOver, _sliderMode == MinimapSliderMode.MouseOver);

        SetRadio(_sideLeft, _side == MinimapSide.Left);
        SetRadio(_sideRight, _side == MinimapSide.Right);
    }

    private static void SetRadio(MenuItem? item, bool selected)
    {
        if (item is null) return;
        item.IsCheckable = true;
        item.IsChecked = selected;
    }
}
