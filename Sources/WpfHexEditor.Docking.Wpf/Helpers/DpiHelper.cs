//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6, Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

namespace WpfHexEditor.Docking.Wpf;

/// <summary>
/// Shared DPI conversion utilities for the docking system.
/// Centralizes screen pixel ↔ DIP conversions previously duplicated
/// in DockDragManager, DockOverlayWindow, and DockEdgeOverlayWindow.
/// Includes per-monitor DPI awareness via Win32 API (Win8.1+).
/// </summary>
internal static class DpiHelper
{
    /// <summary>
    /// Converts a physical screen pixel point to WPF DIPs using the given visual's DPI.
    /// </summary>
    public static Point ScreenToDip(Visual visual, Point screenPoint)
    {
        var source = PresentationSource.FromVisual(visual);
        if (source?.CompositionTarget != null)
            return source.CompositionTarget.TransformFromDevice.Transform(screenPoint);
        return screenPoint;
    }

    /// <summary>
    /// Converts a WPF DIP point to physical screen pixels using the given visual's DPI.
    /// </summary>
    public static Point DipToScreen(Visual visual, Point dipPoint)
    {
        var source = PresentationSource.FromVisual(visual);
        if (source?.CompositionTarget != null)
            return source.CompositionTarget.TransformToDevice.Transform(dipPoint);
        return dipPoint;
    }

    /// <summary>
    /// Returns the DPI scale factor (e.g. 1.0 for 96 DPI, 1.25 for 120 DPI).
    /// </summary>
    public static double GetDpiScale(Visual visual)
    {
        var source = PresentationSource.FromVisual(visual);
        return source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
    }

    /// <summary>
    /// Returns the DPI scale factor for the monitor containing the given screen point
    /// (in physical pixels). Falls back to 1.0 on failure.
    /// Uses Win32 <c>MonitorFromPoint</c> + <c>GetDpiForMonitor</c> (Win8.1+ / net8.0-windows).
    /// </summary>
    public static double GetDpiScaleForScreenPoint(Point screenPoint)
    {
        try
        {
            var pt = new POINT((int)screenPoint.X, (int)screenPoint.Y);
            var hMonitor = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
            if (hMonitor == IntPtr.Zero) return 1.0;

            var hr = GetDpiForMonitor(hMonitor, MonitorDpiType.MDT_EFFECTIVE_DPI, out var dpiX, out _);
            return hr == 0 ? dpiX / 96.0 : 1.0;
        }
        catch
        {
            return 1.0;
        }
    }

    /// <summary>
    /// Converts a physical screen pixel point to DIPs using the DPI of the monitor
    /// containing that point (more accurate than using the host visual's DPI when
    /// the cursor is on a different monitor).
    /// </summary>
    public static Point ScreenToDipForPoint(Point screenPoint)
    {
        var scale = GetDpiScaleForScreenPoint(screenPoint);
        return new Point(screenPoint.X / scale, screenPoint.Y / scale);
    }

    // --- Win32 interop ---

    private const int MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT(int x, int y)
    {
        public int X = x;
        public int Y = y;
    }

    private enum MonitorDpiType
    {
        MDT_EFFECTIVE_DPI = 0
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, int flags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hMonitor, MonitorDpiType dpiType, out uint dpiX, out uint dpiY);
}
