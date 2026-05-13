// Project: WpfHexEditor.Plugins.ScreenRecorder
// File: Models/CaptureRegion.cs
// Description: Immutable screen region used to bound frame captures.

using System.Windows;

namespace WpfHexEditor.Plugins.ScreenRecorder.Models;

public readonly record struct CaptureRegion(int X, int Y, int Width, int Height)
{
    public static CaptureRegion FromRect(Rect r) =>
        new((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height);

    public static CaptureRegion FullScreen()
    {
        var screen = SystemParameters.WorkArea;
        return new(0, 0, (int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight);
    }

    public bool IsEmpty => Width <= 0 || Height <= 0;
    public Rect ToRect() => new(X, Y, Width, Height);
}
