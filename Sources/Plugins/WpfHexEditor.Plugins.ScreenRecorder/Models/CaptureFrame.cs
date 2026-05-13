// Project: WpfHexEditor.Plugins.ScreenRecorder
// File: Models/CaptureFrame.cs
// Description: Immutable record representing a single captured frame.

using System.Windows.Media.Imaging;

namespace WpfHexEditor.Plugins.ScreenRecorder.Models;

public sealed record CaptureFrame(
    int          Index,
    BitmapSource Bitmap,
    int          Delay_ms,
    string?      Label,
    DateTimeOffset Timestamp);
