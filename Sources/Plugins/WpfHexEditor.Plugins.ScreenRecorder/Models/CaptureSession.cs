// Project: WpfHexEditor.Plugins.ScreenRecorder
// File: Models/CaptureSession.cs
// Description: Mutable session state accumulating captured frames.

namespace WpfHexEditor.Plugins.ScreenRecorder.Models;

public sealed class CaptureSession
{
    private readonly List<CaptureFrame> _frames = [];

    public IReadOnlyList<CaptureFrame> Frames       => _frames;
    public RecordingMode               Mode         { get; set; } = RecordingMode.Screenshot;
    public CaptureRegion               Region       { get; set; } = CaptureRegion.FullScreen();
    public int                         GlobalDelay  { get; set; } = 500;
    public int                         LoopCount    { get; set; } = 0;
    public int                         RepeatLastFrameDelay { get; set; } = 1000;
    public double                      OutputScale  { get; set; } = 1.0;
    public DateTimeOffset              CreatedAt    { get; }      = DateTimeOffset.UtcNow;

    public void AddFrame(CaptureFrame frame) => _frames.Add(frame);
    public void RemoveAt(int index)          => _frames.RemoveAt(index);
    public void Clear()                      => _frames.Clear();
}
