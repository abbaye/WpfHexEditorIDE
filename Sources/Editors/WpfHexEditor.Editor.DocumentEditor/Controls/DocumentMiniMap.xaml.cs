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
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;

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

    /// <summary>Raised when the user clicks/drags to request a scroll offset (0–1 normalised).</summary>
    public event EventHandler<double>? ScrollRequested;

    public DocumentMiniMap()
    {
        InitializeComponent();
        SizeChanged   += (_, _) => Redraw();
        MouseDown     += OnMouseDown;
        MouseMove     += OnMouseMove;
        MouseLeftButtonUp += (_, _) => ReleaseMouseCapture();
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
        UpdateViewportRect();
    }

    // ── Drawing ──────────────────────────────────────────────────────────────

    private void Redraw()
    {
        PART_Canvas.Children.Clear();
        if (_model is null || ActualWidth < 4 || ActualHeight < 4) return;

        var blocks  = _model.Blocks.ToList();
        int count   = blocks.Count;
        if (count == 0) return;

        double w = ActualWidth;
        double h = ActualHeight;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var bgBrush = TryFindResource("DE_MiniMapBg") as Brush
                          ?? new SolidColorBrush(Color.FromRgb(22, 22, 22));
            dc.DrawRectangle(bgBrush, null, new Rect(0, 0, w, h));

            double lineH   = Math.Max(1.0, h / Math.Max(count, 1));
            double lineH2  = Math.Max(lineH * 0.6, 0.8);
            double indent  = 4;

            for (int i = 0; i < count; i++)
            {
                var block = blocks[i];
                if (block.Kind is "page-break") continue;

                double y = i * lineH;

                // Width proportional to text length (capped at full width)
                double textFrac = Math.Min(GetFlatLength(block) / 80.0, 1.0);
                double lineW    = Math.Max(2, (w - indent * 2) * textFrac);

                var brush = GetKindBrush(block);
                dc.DrawRectangle(brush, null,
                    new Rect(indent, y + (lineH - lineH2) / 2, lineW, lineH2));
            }
        }

        var img = new System.Windows.Controls.Image
        {
            Source  = RenderToBitmap(visual, (int)Math.Max(1, w), (int)Math.Max(1, h)),
            Stretch = Stretch.Fill,
            Width   = w,
            Height  = h
        };
        PART_Canvas.Children.Add(img);

        UpdateViewportRect();
    }

    private void UpdateViewportRect()
    {
        if (ActualHeight < 4 || _scrollExtent <= 0)
        {
            PART_Viewport.Visibility = Visibility.Collapsed;
            return;
        }

        double scale       = ActualHeight / _scrollExtent;
        double top         = _scrollOffset   * scale;
        double vpH         = Math.Max(4, _viewportHeight * scale);

        top = Math.Clamp(top, 0, ActualHeight - vpH);

        PART_Viewport.Visibility = Visibility.Visible;
        PART_Viewport.Margin     = new Thickness(0, top, 0, 0);
        PART_Viewport.Height     = vpH;
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
        if (e.LeftButton != MouseButtonState.Pressed || !IsMouseCaptured) return;
        ScrollTo(e.GetPosition(this).Y);
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
