// Project: WpfHexEditor.Plugins.ScreenRecorder
// File: Models/CaptureRegion.cs
// Description: Immutable screen region used to bound frame captures.
// Architecture Notes:
//     All dimensions are in physical (device) pixels — required for BitBlt on the desktop DC.
//     GetSystemMetrics returns virtualized (logical) pixels when the process is DPI-unaware;
//     we bypass this by querying DEVMODE.dmPelsWidth/Height via EnumDisplaySettings which
//     always returns the true hardware resolution regardless of process DPI awareness.

using System.Runtime.InteropServices;
using System.Windows;

namespace WpfHexEditor.Plugins.ScreenRecorder.Models;

public readonly record struct CaptureRegion(int X, int Y, int Width, int Height)
{
    public static CaptureRegion FromRect(Rect r) =>
        new((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height);

    public static CaptureRegion PrimaryScreen()
    {
        // EnumDisplaySettings with ENUM_CURRENT_SETTINGS returns hardware physical pixels,
        // unaffected by process DPI awareness mode or DPI virtualization.
        var dm = new DEVMODE { dmSize = (ushort)Marshal.SizeOf<DEVMODE>() };
        if (EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref dm) && dm.dmPelsWidth > 0)
            return new CaptureRegion(0, 0, (int)dm.dmPelsWidth, (int)dm.dmPelsHeight);

        // Fallback: SM_CXSCREEN (may be virtualized on DPI-unaware processes).
        return new CaptureRegion(0, 0, GetSystemMetrics(0), GetSystemMetrics(1));
    }

    public static CaptureRegion FullScreen()
    {
        // SM_CXVIRTUALSCREEN covers all monitors. On DPI-aware processes this returns
        // physical pixels; on unaware processes use the desktop DC extents instead.
        int x = GetSystemMetrics(76), y = GetSystemMetrics(77);
        int w = GetSystemMetrics(78), h = GetSystemMetrics(79);
        if (w > 0) return new CaptureRegion(x, y, w, h);
        return PrimaryScreen();
    }

    public bool IsEmpty => Width <= 0 || Height <= 0;
    public Rect ToRect() => new(X, Y, Width, Height);

    // ── Win32 ────────────────────────────────────────────────────────────────────

    private const int ENUM_CURRENT_SETTINGS = -1;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public ushort dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
        public uint   dmFields;
        public int    dmPositionX, dmPositionY;
        public uint   dmDisplayOrientation, dmDisplayFixedOutput;
        public short  dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public ushort dmLogPixels;
        public uint   dmBitsPerPel, dmPelsWidth, dmPelsHeight;
        public uint   dmDisplayFlags, dmDisplayFrequency;
        public uint   dmICMMethod, dmICMIntent, dmMediaType, dmDitherType;
        public uint   dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
    }

    [DllImport("user32.dll", CharSet = CharSet.Ansi)]
    private static extern bool EnumDisplaySettings(string? lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);
}
