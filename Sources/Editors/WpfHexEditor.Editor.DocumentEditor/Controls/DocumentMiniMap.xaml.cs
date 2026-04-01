// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Controls/DocumentMiniMap.xaml.cs
// Description:
//     Two-strip mini-map drawn via DrawingVisual.
//     Left strip: condensed text lines (one pixel line per paragraph).
//     Right strip: BinaryMap block colors + entropy gradient.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Editor.DocumentEditor.Controls;

/// <summary>
/// Compact dual-strip overview: condensed text on the left,
/// BinaryMap color blocks + entropy on the right.
/// </summary>
public partial class DocumentMiniMap : UserControl
{
    private DocumentModel? _model;

    private static readonly Pen LinePen = new(
        new SolidColorBrush(Color.FromArgb(120, 212, 212, 212)), 1);

    static DocumentMiniMap()
    {
        LinePen.Freeze();
    }

    public DocumentMiniMap()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Redraw();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public void BindModel(DocumentModel model)
    {
        if (_model is not null)
        {
            _model.BlocksChanged    -= OnModelChanged;
            _model.BinaryMap.MapRebuilt -= OnModelChanged;
        }

        _model = model;
        _model.BlocksChanged       += OnModelChanged;
        _model.BinaryMap.MapRebuilt += OnModelChanged;

        Dispatcher.InvokeAsync(Redraw);
    }

    // ── Drawing ───────────────────────────────────────────────────────────────

    private void Redraw()
    {
        DrawTextStrip();
        DrawBinaryStrip();
    }

    private void DrawTextStrip()
    {
        PART_TextStrip.Children.Clear();
        if (_model is null || ActualWidth < 8 || ActualHeight < 4) return;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var blocks   = _model.Blocks.ToList();
            int count    = blocks.Count;
            if (count == 0) return;

            double stripW = PART_TextStrip.ActualWidth;
            double stripH = ActualHeight;
            double lineH  = Math.Max(1.5, stripH / Math.Max(count, 1));

            var bgBrush = TryFindResource("DE_MiniMapBg") as Brush
                          ?? new SolidColorBrush(Color.FromRgb(20, 20, 20));

            dc.DrawRectangle(bgBrush, null, new Rect(0, 0, stripW, stripH));

            for (int i = 0; i < count; i++)
            {
                double y = i * lineH;
                var block = blocks[i];
                double textLen = Math.Min(block.Text.Length / 100.0, 1.0) * stripW;

                dc.DrawLine(LinePen,
                    new Point(2, y + lineH / 2),
                    new Point(2 + textLen, y + lineH / 2));
            }
        }

        var host = new System.Windows.Controls.Canvas();
        PART_TextStrip.Children.Add(new System.Windows.Controls.Image
        {
            Source = RenderVisualToBitmap(visual,
                (int)Math.Max(1, PART_TextStrip.ActualWidth),
                (int)Math.Max(1, ActualHeight)),
            Stretch = Stretch.Fill,
            Width   = PART_TextStrip.ActualWidth,
            Height  = ActualHeight
        });
    }

    private void DrawBinaryStrip()
    {
        PART_BinaryStrip.Children.Clear();
        if (_model is null || ActualWidth < 8 || ActualHeight < 4) return;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            double stripW  = PART_BinaryStrip.ActualWidth;
            double stripH  = ActualHeight;
            long   fileLen = _model.BinaryMap.TotalMappedLength;

            if (fileLen <= 0)
            {
                // Empty: draw grey background
                dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, stripW, stripH));
                return;
            }

            var bgBrush = TryFindResource("DE_MiniMapBg") as Brush
                          ?? new SolidColorBrush(Color.FromRgb(20, 20, 20));
            dc.DrawRectangle(bgBrush, null, new Rect(0, 0, stripW, stripH));

            foreach (var entry in _model.BinaryMap.GetAll())
            {
                if (entry.Length <= 0) continue;

                double x = (double)entry.Offset  / fileLen * stripW;
                double w = (double)entry.Length   / fileLen * stripW;
                w = Math.Max(w, 2.0);

                var brush = GetKindBrush(entry.Block.Kind);
                dc.DrawRectangle(brush, null, new Rect(x, 0, w, stripH));
            }
        }

        PART_BinaryStrip.Children.Add(new System.Windows.Controls.Image
        {
            Source = RenderVisualToBitmap(visual,
                (int)Math.Max(1, PART_BinaryStrip.ActualWidth),
                (int)Math.Max(1, ActualHeight)),
            Stretch = Stretch.Fill,
            Width   = PART_BinaryStrip.ActualWidth,
            Height  = ActualHeight
        });
    }

    private Brush GetKindBrush(string kind) => kind switch
    {
        "paragraph" => TryFindResource("DE_BlockHoverBrush")    as Brush ?? Brushes.CornflowerBlue,
        "run"       => TryFindResource("DE_SelectedBlockBrush") as Brush ?? Brushes.Teal,
        "image"     => Brushes.Salmon,
        "table"     => Brushes.Goldenrod,
        _           => Brushes.Gray
    };

    private static System.Windows.Media.Imaging.RenderTargetBitmap RenderVisualToBitmap(
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
