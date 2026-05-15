// ==========================================================
// Project: WpfHexEditor.Plugins.ScreenRecorder
// File: Services/FrameCaptureEngine.cs
// Description: Captures screen regions as BitmapSource using Win32 BitBlt.
//              Temporarily hides the overlay HWND during capture to exclude it.
// Architecture Notes:
//     BitBlt on the desktop DC is used (not RenderTargetBitmap) because we capture
//     the actual screen pixels — including other windows, not just WPF visuals.
//     RenderTargetBitmap is used only for WPF-element-only captures (unit tests).
//     The hide/show cycle for the overlay is <20 ms — imperceptible at >=80 ms intervals.
// ==========================================================

using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfHexEditor.Plugins.ScreenRecorder.Models;

namespace WpfHexEditor.Plugins.ScreenRecorder.Services;

public static class FrameCaptureEngine
{
    public static async Task<BitmapSource> CaptureRegionAsync(CaptureRegion region, IntPtr excludeHwnd = default)
    {
        // Hide overlay on UI thread, then BitBlt on a thread-pool thread to avoid
        // blocking WPF rendering during capture, then create BitmapSource back on UI thread.
        var wasVisible = false;
        if (excludeHwnd != IntPtr.Zero)
            wasVisible = await Application.Current.Dispatcher.InvokeAsync(() => IsWindowVisible(excludeHwnd));

        if (wasVisible)
            await Application.Current.Dispatcher.InvokeAsync(() => ShowWindow(excludeHwnd, SW_HIDE));

        // Small delay so the hide takes effect before we BitBlt.
        if (wasVisible) await Task.Delay(20);

        try
        {
            // BitBlt runs on a thread-pool thread — GDI calls are safe off-UI-thread.
            var hBitmap = await Task.Run(() => BitBltToHBitmap(region));

            // BitmapSource must be created on the UI thread (Dispatcher).
            // Use the screen DPI so WPF does not upscale the bitmap on high-DPI displays.
            return await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var screenDpi = GetScreenDpi();
                var bmp = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero, Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                DeleteObject(hBitmap);

                // Wrap in WriteableBitmap at true screen DPI so WPF renders at 1:1 pixels.
                var wb = new WriteableBitmap(
                    bmp.PixelWidth, bmp.PixelHeight,
                    screenDpi, screenDpi,
                    bmp.Format, bmp.Palette);
                var stride = (bmp.PixelWidth * bmp.Format.BitsPerPixel + 7) / 8;
                var pixels = new byte[bmp.PixelHeight * stride];
                bmp.CopyPixels(pixels, stride, 0);
                wb.WritePixels(new Int32Rect(0, 0, bmp.PixelWidth, bmp.PixelHeight), pixels, stride, 0);
                wb.Freeze();
                return (BitmapSource)wb;
            });
        }
        finally
        {
            if (wasVisible)
                await Application.Current.Dispatcher.InvokeAsync(() => ShowWindow(excludeHwnd, SW_SHOW));
        }
    }

    public static Task<BitmapSource> CaptureElementAsync(UIElement element, double dpi = 96) =>
        Application.Current.Dispatcher.InvokeAsync(() => RenderElement(element, dpi)).Task;

    public static BitmapSource ScaleBitmap(BitmapSource source, double scale)
    {
        if (Math.Abs(scale - 1.0) < 0.001) return source;
        var scaled = new TransformedBitmap(source, new ScaleTransform(scale, scale));
        scaled.Freeze();
        return scaled;
    }

    public static BitmapSource CreateThumbnail(BitmapSource source, int targetWidth = 120)
    {
        var ratio  = (double)targetWidth / source.PixelWidth;
        var thumb  = new TransformedBitmap(source, new ScaleTransform(ratio, ratio));
        thumb.Freeze();
        return thumb;
    }

    public static Task<byte[]> EncodePngOnUiThreadAsync(BitmapSource bitmap) =>
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            return ms.ToArray();
        }).Task;

    // ── Win32 screen capture ──────────────────────────────────────────────────

    // Returns a raw HBITMAP (caller must DeleteObject). Safe to call off-UI-thread.
    private static IntPtr BitBltToHBitmap(CaptureRegion r)
    {
        var desktopDc = GetDC(IntPtr.Zero);
        var memDc     = CreateCompatibleDC(desktopDc);
        var hBitmap   = CreateCompatibleBitmap(desktopDc, r.Width, r.Height);
        var oldBitmap = SelectObject(memDc, hBitmap);

        BitBlt(memDc, 0, 0, r.Width, r.Height, desktopDc, r.X, r.Y, SRCCOPY);

        SelectObject(memDc, oldBitmap);
        DeleteDC(memDc);
        ReleaseDC(IntPtr.Zero, desktopDc);

        return hBitmap;
    }

    private static BitmapSource RenderElement(UIElement element, double dpi)
    {
        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        element.Arrange(new Rect(element.DesiredSize));

        var rtb = new RenderTargetBitmap(
            (int)element.RenderSize.Width,
            (int)element.RenderSize.Height,
            dpi, dpi, PixelFormats.Pbgra32);
        rtb.Render(element);
        rtb.Freeze();
        return rtb;
    }

    // ── DPI helpers ───────────────────────────────────────────────────────────

    private const int LOGPIXELSX = 88;

    private static double GetScreenDpi()
    {
        var dc = GetDC(IntPtr.Zero);
        try   { return GetDeviceCaps(dc, LOGPIXELSX); }
        finally { ReleaseDC(IntPtr.Zero, dc); }
    }

    // ── Win32 P/Invoke ────────────────────────────────────────────────────────

    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;
    private const uint SRCCOPY = 0x00CC0020;

    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int    ReleaseDC(IntPtr hwnd, IntPtr hdc);
    [DllImport("gdi32.dll")]  private static extern int    GetDeviceCaps(IntPtr hdc, int idx);
    [DllImport("user32.dll")] private static extern bool   IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool   ShowWindow(IntPtr hwnd, int cmd);
    [DllImport("gdi32.dll")]  private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")]  private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int w, int h);
    [DllImport("gdi32.dll")]  private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hObj);
    [DllImport("gdi32.dll")]  private static extern bool   DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")]  private static extern bool   DeleteObject(IntPtr hObj);
    [DllImport("gdi32.dll")]  private static extern bool   BitBlt(IntPtr dst, int dX, int dY, int w, int h,
                                                                   IntPtr src, int sX, int sY, uint rop);
}
