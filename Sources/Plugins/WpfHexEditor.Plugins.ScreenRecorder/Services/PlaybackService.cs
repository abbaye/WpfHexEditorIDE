// Project: WpfHexEditor.Plugins.ScreenRecorder
// File: Services/PlaybackService.cs
// Description: Timer-driven playback that advances SelectedFrame through the timeline.
//              Fires FrameAdvanced each tick; caller updates SelectedFrame.

using System.Windows.Threading;

namespace WpfHexEditor.Plugins.ScreenRecorder.Services;

public sealed class PlaybackService : IDisposable
{
    private readonly DispatcherTimer _timer = new();
    private IReadOnlyList<int>?      _delays;
    private int                      _index;

    public event EventHandler<int>? FrameAdvanced;
    public event EventHandler?      PlaybackStopped;

    public bool IsPlaying => _timer.IsEnabled;

    public PlaybackService() => _timer.Tick += OnTick;

    public void Play(IReadOnlyList<int> frameDelays, int startIndex = 0)
    {
        if (frameDelays.Count == 0) return;
        _delays = frameDelays;
        _index  = Math.Clamp(startIndex, 0, frameDelays.Count - 1);
        _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, frameDelays[_index]));
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
        PlaybackStopped?.Invoke(this, EventArgs.Empty);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_delays is null) { Stop(); return; }

        _index++;
        if (_index >= _delays.Count) { Stop(); return; }

        FrameAdvanced?.Invoke(this, _index);
        _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, _delays[_index]));
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
    }
}
