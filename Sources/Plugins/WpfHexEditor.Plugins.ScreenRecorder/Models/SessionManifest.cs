// Project: WpfHexEditor.Plugins.ScreenRecorder
// File: Models/SessionManifest.cs
// Description: JSON-serializable manifest stored in manifest.json inside the .whscr archive.

using System.Text.Json.Serialization;

namespace WpfHexEditor.Plugins.ScreenRecorder.Models;

public sealed class SessionManifest
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("frameCount")]
    public int FrameCount { get; set; }

    [JsonPropertyName("globalDelay_ms")]
    public int GlobalDelay_ms { get; set; }

    [JsonPropertyName("loopCount")]
    public int LoopCount { get; set; }

    [JsonPropertyName("repeatLastFrameDelay_ms")]
    public int RepeatLastFrameDelay_ms { get; set; }

    [JsonPropertyName("outputScale")]
    public double OutputScale { get; set; }

    [JsonPropertyName("captureRegion")]
    public RegionDto CaptureRegion { get; set; } = new();

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = nameof(RecordingMode.Screenshot);

    public sealed class RegionDto
    {
        [JsonPropertyName("x")]      public int X      { get; set; }
        [JsonPropertyName("y")]      public int Y      { get; set; }
        [JsonPropertyName("width")]  public int Width  { get; set; }
        [JsonPropertyName("height")] public int Height { get; set; }
    }
}

public sealed class FrameMeta
{
    [JsonPropertyName("index")]     public int            Index     { get; set; }
    [JsonPropertyName("delay_ms")]  public int            Delay_ms  { get; set; }
    [JsonPropertyName("label")]     public string?        Label     { get; set; }
    [JsonPropertyName("timestamp")] public DateTimeOffset Timestamp { get; set; }
}
