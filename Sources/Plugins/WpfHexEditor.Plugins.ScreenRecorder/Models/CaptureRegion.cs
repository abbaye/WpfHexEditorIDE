// Project: WpfHexEditor.Plugins.ScreenRecorder
// File: Models/CaptureRegion.cs
// Description: Immutable screen region used to bound frame captures.

using System.Runtime.InteropServices;
using System.Windows;

namespace WpfHexEditor.Plugins.ScreenRecorder.Models;

public readonly record struct CaptureRegion(int X, int Y, int Width, int Height)
{
    public static CaptureRegion FromRect(Rect r) =>
        new((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height);

    // SM_CXVIRTUALSCREEN / SM_CYVIRTUALSCREEN cover all monitors in physical pixels,
    // regardless of DPI scaling — required for correct BitBlt coordinates.
    private const int SM_XVIRTUALSCREEN  = 76;
    private const int SM_YVIRTUALSCREEN  = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int nIndex);

    public static CaptureRegion FullScreen() => new(
        GetSystemMetrics(SM_XVIRTUALSCREEN),
        GetSystemMetrics(SM_YVIRTUALSCREEN),
        GetSystemMetrics(SM_CXVIRTUALSCREEN),
        GetSystemMetrics(SM_CYVIRTUALSCREEN));

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    public static CaptureRegion PrimaryScreen() => new(
        0, 0,
        GetSystemMetrics(SM_CXSCREEN),
        GetSystemMetrics(SM_CYSCREEN));

    public bool IsEmpty => Width <= 0 || Height <= 0;
    public Rect ToRect() => new(X, Y, Width, Height);
}
