// Project: WpfHexEditor.Plugins.ScreenRecorder
// File: Models/ExportOptions.cs
// Description: Output options for GIF, PNG sequence, and MP4 export.

namespace WpfHexEditor.Plugins.ScreenRecorder.Models;

public sealed record ExportOptions(
    string OutputPath,
    double OutputScale            = 1.0,
    int    LoopCount              = 0,
    int    RepeatLastFrameDelay   = 1000);
