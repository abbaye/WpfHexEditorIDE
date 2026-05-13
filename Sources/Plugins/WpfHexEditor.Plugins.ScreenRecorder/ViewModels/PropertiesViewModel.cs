// Project: WpfHexEditor.Plugins.ScreenRecorder
// File: ViewModels/PropertiesViewModel.cs
// Description: Exposes editable capture region, scale, loop and repeat settings.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Plugins.ScreenRecorder.Models;

namespace WpfHexEditor.Plugins.ScreenRecorder.ViewModels;

public sealed class PropertiesViewModel : INotifyPropertyChanged
{
    private CaptureRegion _captureRegion;
    private double        _outputScale    = 1.0;
    private int           _loopCount;
    private int           _repeatLastFrameDelay = 1000;
    private int           _timerInterval  = 100;

    public CaptureRegion CaptureRegion
    {
        get => _captureRegion;
        set { if (_captureRegion == value) return; _captureRegion = value; OnPropertyChanged(); }
    }

    public double OutputScale
    {
        get => _outputScale;
        set { if (Math.Abs(_outputScale - value) < 0.001) return; _outputScale = Math.Clamp(value, 0.1, 1.0); OnPropertyChanged(); }
    }

    public int LoopCount
    {
        get => _loopCount;
        set { if (_loopCount == value) return; _loopCount = Math.Max(0, value); OnPropertyChanged(); }
    }

    public int RepeatLastFrameDelay
    {
        get => _repeatLastFrameDelay;
        set { if (_repeatLastFrameDelay == value) return; _repeatLastFrameDelay = Math.Max(0, value); OnPropertyChanged(); }
    }

    public int TimerInterval
    {
        get => _timerInterval;
        set { if (_timerInterval == value) return; _timerInterval = Math.Max(80, value); OnPropertyChanged(); }
    }

    public void ApplyRegion(CaptureRegion region) => CaptureRegion = region;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}
