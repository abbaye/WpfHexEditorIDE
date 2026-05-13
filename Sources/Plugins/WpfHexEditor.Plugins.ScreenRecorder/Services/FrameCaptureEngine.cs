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

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfHexEditor.Plugins.ScreenRecorder.Models;

namespace WpfHexEditor.Plugins.ScreenRecorder.Services;

public static class FrameCaptureEngine
{
    public static Task<BitmapSource> CaptureRegionAsync(CaptureRegion region, IntPtr excludeHwnd = default) =>
        Application.Current.Dispatcher.InvokeAsync(() => CaptureRegion(region, excludeHwnd)).Task;

    public static Task<BitmapSource> CaptureElementAsync(UIElement element, double dpi = 96) =>
        Application.Current.Dispatcher.InvokeAsync(() => RenderElement(element, dpi)).Task;

    public static BitmapSource ScaleBitmap(BitmapSource source, double scale)
    {
        if (Math.Abs(scale - 1.0) < 0.001) return source;
        var w = (int)(source.PixelWidth  * scale);
        var h = (int)(source.PixelHeight * scale);
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

    // ── Win32 screen capture ──────────────────────────────────────────────────

    private static BitmapSource CaptureRegion(CaptureRegion region, IntPtr excludeHwnd)
    {
        var wasVisible = excludeHwnd != IntPtr.Zero && IsWindowVisible(excludeHwnd);
        if (wasVisible) ShowWindow(excludeHwnd, SW_HIDE);

        try
        {
            return BitBltCapture(region);
        }
        finally
        {
            if (wasVisible) ShowWindow(excludeHwnd, SW_SHOW);
        }
    }

    private static BitmapSource BitBltCapture(CaptureRegion r)
    {
        var desktopDc  = GetDC(IntPtr.Zero);
        var memDc      = CreateCompatibleDC(desktopDc);
        var hBitmap    = CreateCompatibleBitmap(desktopDc, r.Width, r.Height);
        var oldBitmap  = SelectObject(memDc, hBitmap);

        BitBlt(memDc, 0, 0, r.Width, r.Height, desktopDc, r.X, r.Y, SRCCOPY);

        SelectObject(memDc, oldBitmap);
        DeleteDC(memDc);
        ReleaseDC(IntPtr.Zero, desktopDc);

        var bmp = Imaging.CreateBitmapSourceFromHBitmap(
            hBitmap, IntPtr.Zero, Int32Rect.Empty,
            BitmapSizeOptions.FromEmptyOptions());
        DeleteObject(hBitmap);
        bmp.Freeze();
        return bmp;
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

    // ── Win32 P/Invoke ────────────────────────────────────────────────────────

    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;
    private const uint SRCCOPY = 0x00CC0020;

    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int    ReleaseDC(IntPtr hwnd, IntPtr hdc);
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
