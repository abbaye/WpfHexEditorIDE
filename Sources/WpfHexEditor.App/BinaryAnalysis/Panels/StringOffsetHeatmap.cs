// Project     : WpfHexEditor.App
// File        : StringOffsetHeatmap.cs
// Description : Thin horizontal band that renders string-run density by encoding colour.
//               Each rendered pixel column represents a file-offset slice; clicking navigates.
// Architecture: Standalone FrameworkElement, driven by the StringExtractionViewModel.

using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfHexEditor.App.BinaryAnalysis.Services;
using WpfHexEditor.App.BinaryAnalysis.ViewModels;

namespace WpfHexEditor.App.BinaryAnalysis.Panels;

internal sealed class StringOffsetHeatmap : FrameworkElement
{
    private static readonly SolidColorBrush EmptyBrush = FreezeB(new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)));
    private static SolidColorBrush FreezeB(SolidColorBrush b) { b.Freeze(); return b; }

    // Pixel columns cached as frozen ImageSource; rebuilt when results or width change.
    private WriteableBitmap? _bitmap;
    private StringExtractionViewModel? _vm;
    private long _bufferLength;

    // Navigation callback wired by the panel.
    public Action<long>? NavigateToOffset { get; set; }

    public void Attach(StringExtractionViewModel vm)
    {
        if (_vm is not null) _vm.PropertyChanged -= OnVmChanged;
        _vm = vm;
        _vm.PropertyChanged += OnVmChanged;
    }

    private void OnVmChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(StringExtractionViewModel.TotalCount))
        {
            _bufferLength = _vm?.LastBuffer?.Length ?? 0;
            RebuildBitmap();
            InvalidateVisual();
        }
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        RebuildBitmap();
        InvalidateVisual();
    }

    private void RebuildBitmap()
    {
        int w = (int)Math.Max(1, ActualWidth);
        int h = (int)Math.Max(1, ActualHeight);
        if (_vm is null || _bufferLength <= 0 || w <= 0) { _bitmap = null; return; }

        _bitmap = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgr32, null);
        _bitmap.Lock();

        // One int[] row, written then blitted to every bitmap row.
        var pixels = new int[w];
        var emptyColor = unchecked((int)0xFF2A2A2A);

        for (int x = 0; x < w; x++) pixels[x] = emptyColor;

        // Bucket runs into pixel columns.
        foreach (var run in _vm.GetAllRuns())
        {
            int col = (int)((double)run.Offset / _bufferLength * (w - 1));
            col = Math.Clamp(col, 0, w - 1);
            var c = EncodingPalette.Colors.TryGetValue(run.Encoding, out var ec) ? ec : EncodingPalette.FallbackColor;
            // Blend with existing pixel (additive saturation towards encoding colour).
            int existing = pixels[col];
            int er = (existing >> 16) & 0xFF;
            int eg = (existing >> 8)  & 0xFF;
            int eb =  existing        & 0xFF;
            // Move 25% of the gap towards target colour per run (saturates quickly with density).
            int nr = er + ((c.R - er) >> 2);
            int ng = eg + ((c.G - eg) >> 2);
            int nb = eb + ((c.B - eb) >> 2);
            pixels[col] = unchecked((int)(0xFF000000u | (uint)(nr << 16) | (uint)(ng << 8) | (uint)nb));
        }

        // Replicate the single pixel row across all rows then write in one call.
        var fullPixels = new int[w * h];
        for (int y = 0; y < h; y++)
            pixels.CopyTo(fullPixels, y * w);
        _bitmap.WritePixels(new Int32Rect(0, 0, w, h), fullPixels, w * 4, 0);

        _bitmap.Unlock();
        _bitmap.Freeze();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        if (_bitmap is null)
        {
            dc.DrawRectangle(EmptyBrush, null, bounds);
            return;
        }
        dc.DrawImage(_bitmap, bounds);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (_vm is null || _bufferLength <= 0 || ActualWidth <= 0) return;
        double x = e.GetPosition(this).X;
        long offset = (long)(x / ActualWidth * _bufferLength);
        offset = Math.Clamp(offset, 0, _bufferLength - 1);
        NavigateToOffset?.Invoke(offset);
    }
}
