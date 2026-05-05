// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Controls/DocumentMiniMap.xaml.cs
// Description:
//     Vertical VS Code-style minimap drawn right of the content area.
//     Renders a scaled-down view of all document blocks top-to-bottom.
//     A semi-transparent viewport rectangle shows the currently visible area.
//     Clicking / dragging scrolls the document to that position.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;
using WpfHexEditor.Editor.DocumentEditor.Properties;

namespace WpfHexEditor.Editor.DocumentEditor.Controls;

/// <summary>
/// Vertical minimap panel — shows condensed document content and a
/// viewport indicator, allowing click-to-scroll navigation.
/// </summary>
public partial class DocumentMiniMap : System.Windows.Controls.UserControl
{
    private DocumentModel? _model;
    private double         _scrollOffset;    // current vertical scroll offset in document pixels
    private double         _scrollExtent;    // total document canvas height
    private double         _viewportHeight;  // visible area height

    // ── Static frozen brushes (same pattern as CodeEditor MinimapControl) ─────
    private static readonly Brush ViewportBrush;
    private static readonly Pen   ViewportPen;
    private static readonly Brush HoverBandBrush;

    static DocumentMiniMap()
    {
        ViewportBrush  = Freeze(new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)));
        ViewportPen    = FreezePen(new Pen(Freeze(new SolidColorBrush(Color.FromArgb(120, 255, 255, 255))), 1.0));
        HoverBandBrush = Freeze(new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)));
    }

    private static Brush   Freeze(SolidColorBrush b)  { b.Freeze(); return b; }
    private static Pen     FreezePen(Pen p)            { p.Freeze(); return p; }

    // ── Context-menu state ────────────────────────────────────────────────────
    private bool _renderBlocks = true;   // show block-type colored strips vs uniform dots
    private bool _sliderAlways = true;   // viewport rect always visible vs mouse-over only
    private bool _isMouseOver;
    private double _hoverY = -1;

    /// <summary>Raised when the user clicks/drags to request a scroll offset (0–1 normalised).</summary>
    public event EventHandler<double>? ScrollRequested;

    public DocumentMiniMap()
    {
        InitializeComponent();
        Loaded            += (_, _) => Redraw();
        SizeChanged       += (_, _) => Redraw();
        MouseDown         += OnMouseDown;
        MouseMove         += OnMouseMove;
        MouseEnter        += (_, _) => { _isMouseOver = true;  RedrawOverlay(); };
        MouseLeave        += (_, _) => { _isMouseOver = false; _hoverY = -1; RedrawOverlay(); };
        MouseLeftButtonUp += (_, _) => ReleaseMouseCapture();
        MouseRightButtonUp+= OnRightClick;
        InitializeContextMenu();
    }

    // ── Context menu ─────────────────────────────────────────────────────────

    private MenuItem? _miShow;
    private MenuItem? _miBlocks;
    private MenuItem? _miSliderAlways;
    private MenuItem? _miSliderHover;

    private void InitializeContextMenu()
    {
        var cm = new ContextMenu();

        _miShow = new MenuItem { Header = DocumentEditorResources.DocMiniMap_ShowMiniMap, IsCheckable = true, IsChecked = true };
        _miShow.Click += (_, _) => { Visibility = _miShow.IsChecked ? Visibility.Visible : Visibility.Collapsed; };

        _miBlocks = new MenuItem { Header = DocumentEditorResources.DocMiniMap_RenderBlocks, IsCheckable = true, IsChecked = true };
        _miBlocks.Click += (_, _) => { _renderBlocks = _miBlocks.IsChecked; Redraw(); };

        _miSliderAlways = new MenuItem { Header = DocumentEditorResources.DocMiniMap_SliderAlways, IsCheckable = true, IsChecked = true };
        _miSliderHover  = new MenuItem { Header = DocumentEditorResources.DocMiniMap_SliderMouseOver, IsCheckable = true };

        _miSliderAlways.Click += (_, _) =>
        {
            _sliderAlways = true;
            _miSliderAlways.IsChecked = true;
            _miSliderHover!.IsChecked = false;
            Redraw();
        };
        _miSliderHover.Click += (_, _) =>
        {
            _sliderAlways = false;
            _miSliderHover.IsChecked   = true;
            _miSliderAlways!.IsChecked = false;
            Redraw();
        };

        cm.Items.Add(_miShow);
        cm.Items.Add(new Separator());
        cm.Items.Add(_miBlocks);
        cm.Items.Add(new Separator());
        cm.Items.Add(_miSliderAlways);
        cm.Items.Add(_miSliderHover);

        ContextMenu = cm;
    }

    private void OnRightClick(object sender, MouseButtonEventArgs e)
    {
        if (ContextMenu is not null)
        {
            ContextMenu.PlacementTarget = this;
            ContextMenu.IsOpen = true;
            e.Handled = true;
        }
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public void BindModel(DocumentModel model)
    {
        if (_model is not null)
        {
            _model.BlocksChanged        -= OnModelChanged;
            _model.BinaryMap.MapRebuilt -= OnModelChanged;
        }

        _model = model;
        _model.BlocksChanged        += OnModelChanged;
        _model.BinaryMap.MapRebuilt += OnModelChanged;

        Dispatcher.InvokeAsync(Redraw);
    }

    /// <summary>Updates scroll state so the viewport rectangle reflects the current position.</summary>
    public void UpdateScroll(double scrollOffset, double scrollExtent, double viewportHeight)
    {
        _scrollOffset   = scrollOffset;
        _scrollExtent   = scrollExtent;
        _viewportHeight = viewportHeight;
        // Only the cheap overlay needs to refresh on scroll — block bitmap is unchanged.
        RedrawOverlay();
    }

    // ── Drawing ──────────────────────────────────────────────────────────────

    private System.Windows.Controls.Image? _bgImage;
    private System.Windows.Shapes.Rectangle? _viewportRect;
    private System.Windows.Shapes.Rectangle? _hoverBand;
    private double _cachedW, _cachedH;
    private int    _cachedBlockCount;

    private void Redraw()
    {
        if (_model is null || ActualWidth < 4 || ActualHeight < 4) return;
        var blocks = _model.Blocks.ToList();
        int count  = blocks.Count;
        if (count == 0) { PART_Canvas.Children.Clear(); _bgImage = null; return; }

        double w = ActualWidth;
        double h = ActualHeight;

        // Re-rasterise the (expensive) block strips only when geometry changed.
        bool geometryChanged = _bgImage is null
                            || Math.Abs(_cachedW - w) > 0.5
                            || Math.Abs(_cachedH - h) > 0.5
                            || _cachedBlockCount != count;
        if (geometryChanged)
        {
            PART_Canvas.Children.Clear();
            _bgImage = BuildBlocksImage(blocks, w, h);
            PART_Canvas.Children.Add(_bgImage);
            _viewportRect = null;
            _hoverBand    = null;
            _cachedW = w; _cachedH = h; _cachedBlockCount = count;
        }
        RedrawOverlay();
    }

    /// <summary>
    /// Approximate column width used to split long block text into visual lines.
    /// The exact value does not have to match the renderer's wrap; it only
    /// controls how dense the minimap looks compared to a single body of text.
    /// </summary>
    private const int MinimapWrapColumns = 80;

    private System.Windows.Controls.Image BuildBlocksImage(List<DocumentBlock> blocks, double w, double h)
    {
        // Walk every block once and emit (lineLengthChars, blockKind) pairs so we
        // can pick a uniform vertical scale that matches both very short and very
        // long documents — same trick CodeEditor's MinimapControl uses.
        var lines = new List<(int Len, DocumentBlock Block)>(capacity: blocks.Count * 2);
        foreach (var block in blocks)
        {
            if (block.Kind is "page-break") continue;
            string text = block.Children.Count > 0
                ? string.Concat(block.Children.Select(c => c.Text))
                : block.Text;
            if (string.IsNullOrEmpty(text)) { lines.Add((0, block)); continue; }

            int start = 0;
            for (int i = 0; i <= text.Length; i++)
            {
                bool atEnd  = i == text.Length;
                bool atNl   = !atEnd && text[i] == '\n';
                bool atWrap = !atEnd && !atNl && (i - start) >= MinimapWrapColumns;
                if (!atEnd && !atNl && !atWrap) continue;

                lines.Add((i - start, block));
                start = atNl ? i + 1 : i;
            }
        }
        if (lines.Count == 0) lines.Add((0, blocks[0]));

        // Per-line height: enough lines to read the document at a glance, but
        // never so dense that individual lines disappear (min 1.4 dip).
        double rawScale = h / Math.Max(1, lines.Count);
        double lineH    = Math.Clamp(rawScale, 1.4, 4.0);
        double rowFill  = Math.Max(0.8, lineH * 0.55);
        double indent   = 4;
        double maxW     = w - indent * 2;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var bgBrush = TryFindResource("DE_MiniMapBg") as Brush
                          ?? new SolidColorBrush(Color.FromRgb(22, 22, 22));
            dc.DrawRectangle(bgBrush, null, new Rect(0, 0, w, h));

            double y = 0;
            foreach (var (len, block) in lines)
            {
                if (y > h) break;
                if (len > 0)
                {
                    double frac  = Math.Clamp(len / (double)MinimapWrapColumns, 0.05, 1.0);
                    double lineW = Math.Max(2, maxW * frac);
                    dc.DrawRectangle(GetKindBrush(block), null,
                        new Rect(indent, y + (lineH - rowFill) / 2, lineW, rowFill));
                }
                y += lineH;
            }
        }

        return new System.Windows.Controls.Image
        {
            Source  = RenderToBitmap(visual, (int)Math.Max(1, w), (int)Math.Max(1, h)),
            Stretch = Stretch.Fill,
            Width   = w,
            Height  = h,
            IsHitTestVisible = false
        };
    }

    private void RedrawOverlay()
    {
        if (_bgImage is null) { Redraw(); return; }
        double w = ActualWidth, h = ActualHeight;

        // Lazy-create overlays once; subsequent updates only mutate properties.
        if (_viewportRect is null)
        {
            _viewportRect = new System.Windows.Shapes.Rectangle
            {
                Fill   = ViewportBrush,
                Stroke = ViewportPen.Brush,
                StrokeThickness  = ViewportPen.Thickness,
                IsHitTestVisible = false
            };
            PART_Canvas.Children.Add(_viewportRect);
        }
        if (_hoverBand is null)
        {
            _hoverBand = new System.Windows.Shapes.Rectangle
            {
                Fill = HoverBandBrush,
                IsHitTestVisible = false
            };
            PART_Canvas.Children.Add(_hoverBand);
        }

        bool showSlider = _scrollExtent > 0 && (_sliderAlways || _isMouseOver);
        if (!showSlider)
        {
            _viewportRect.Visibility = Visibility.Collapsed;
            _hoverBand.Visibility    = Visibility.Collapsed;
            return;
        }

        double scale = h / _scrollExtent;
        double vpH   = Math.Max(8, _viewportHeight * scale);
        double top   = Math.Clamp(_scrollOffset * scale, 0, h - vpH);

        _viewportRect.Width  = w;
        _viewportRect.Height = vpH;
        Canvas.SetLeft(_viewportRect, 0);
        Canvas.SetTop (_viewportRect, top);
        _viewportRect.Visibility = Visibility.Visible;

        if (_isMouseOver && _hoverY >= 0)
        {
            double bandTop = Math.Clamp(_hoverY - vpH / 2, 0, h - vpH);
            _hoverBand.Width  = w;
            _hoverBand.Height = vpH;
            Canvas.SetLeft(_hoverBand, 0);
            Canvas.SetTop (_hoverBand, bandTop);
            _hoverBand.Visibility = Visibility.Visible;
        }
        else
        {
            _hoverBand.Visibility = Visibility.Collapsed;
        }
    }

    // ── Interaction ──────────────────────────────────────────────────────────

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        CaptureMouse();
        ScrollTo(e.GetPosition(this).Y);
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(this);
        if (e.LeftButton == MouseButtonState.Pressed && IsMouseCaptured)
        {
            ScrollTo(pos.Y);
        }
        else
        {
            _hoverY = pos.Y;
            RedrawOverlay();
        }
    }

    private void ScrollTo(double mouseY)
    {
        if (ActualHeight <= 0) return;
        double normalised = Math.Clamp(mouseY / ActualHeight, 0, 1);
        ScrollRequested?.Invoke(this, normalised);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static int GetFlatLength(DocumentBlock block)
    {
        if (block.Children.Count > 0)
            return block.Children.Sum(c => c.Text.Length);
        return block.Text.Length;
    }

    private Brush GetKindBrush(DocumentBlock block)
    {
        bool bold = block.Attributes.ContainsKey("bold") ||
                    block.Children.Any(c => c.Attributes.ContainsKey("bold"));

        return block.Kind switch
        {
            "heading"   => TryFindResource("DE_HeadingFgBrush") as Brush
                           ?? new SolidColorBrush(Color.FromArgb(200, 220, 220, 255)),
            "image"     => new SolidColorBrush(Color.FromArgb(160, 255, 160, 100)),
            "table"     => new SolidColorBrush(Color.FromArgb(160, 255, 210,  80)),
            "code"      => new SolidColorBrush(Color.FromArgb(160, 150, 220, 150)),
            "paragraph" => bold
                           ? new SolidColorBrush(Color.FromArgb(200, 210, 210, 210))
                           : new SolidColorBrush(Color.FromArgb(120, 180, 180, 180)),
            _           => new SolidColorBrush(Color.FromArgb(100, 160, 160, 160)),
        };
    }

    private static System.Windows.Media.Imaging.RenderTargetBitmap RenderToBitmap(
        Visual visual, int w, int h)
    {
        var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(
            w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        return rtb;
    }

    private void OnModelChanged(object? sender, EventArgs e) =>
        Dispatcher.InvokeAsync(Redraw);
}
