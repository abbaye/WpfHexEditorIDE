// Project: WpfHexEditor.Plugins.ScreenRecorder
// File: ViewModels/CaptureHudViewModel.cs
// Description: Data for the HUD overlay shown during an active capture session.

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfHexEditor.Plugins.ScreenRecorder.ViewModels;

public sealed class CaptureHudViewModel : INotifyPropertyChanged
{
    private bool   _isRecording;
    private int    _frameCount;
    private string _elapsed      = "00:00";
    private string _modeLabel    = string.Empty;
    private string _captureHotkey = "F9";

    public bool IsRecording
    {
        get => _isRecording;
        set { if (_isRecording == value) return; _isRecording = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusLabel)); }
    }

    public int FrameCount
    {
        get => _frameCount;
        set { if (_frameCount == value) return; _frameCount = value; OnPropertyChanged(); }
    }

    public string Elapsed
    {
        get => _elapsed;
        set { if (_elapsed == value) return; _elapsed = value; OnPropertyChanged(); }
    }

    public string ModeLabel
    {
        get => _modeLabel;
        set { if (_modeLabel == value) return; _modeLabel = value; OnPropertyChanged(); }
    }

    public string CaptureHotkey
    {
        get => _captureHotkey;
        set { if (_captureHotkey == value) return; _captureHotkey = value; OnPropertyChanged(); OnPropertyChanged(nameof(CaptureHotkeyLabel)); }
    }

    public string StatusLabel => _isRecording
        ? Properties.ScreenRecorderResources.ScreenRecorder_HudRecording
        : Properties.ScreenRecorderResources.ScreenRecorder_HudPaused;

    public string CaptureHotkeyLabel => $"{_captureHotkey} {Properties.ScreenRecorderResources.ScreenRecorder_HudCapture}";

    public string EscLabel => Properties.ScreenRecorderResources.ScreenRecorder_HudEscCancel;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}
