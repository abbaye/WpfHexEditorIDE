// ==========================================================
// Project: WpfHexEditor.Plugins.ScreenRecorder
// File: ViewModels/ScreenRecorderViewModel.cs
// Description: Root ViewModel for the Screen Recorder document editor.
//              Owns all child VMs and exposes toolbar commands.
// Architecture Notes:
//     Commands are stubs in Phase 1; wired to real services in Phases 3-9.
//     INotifyPropertyChanged via base class pattern used across this plugin.
// ==========================================================

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WpfHexEditor.Plugins.ScreenRecorder.Models;
using WpfHexEditor.SDK.Commands;

namespace WpfHexEditor.Plugins.ScreenRecorder.ViewModels;

public sealed class ScreenRecorderViewModel : INotifyPropertyChanged
{
    private RecordingMode _selectedMode = RecordingMode.Screenshot;
    private bool        _isSessionActive;

    public TimelineViewModel   Timeline   { get; } = new();
    public PreviewViewModel    Preview    { get; } = new();
    public PropertiesViewModel Properties { get; } = new();
    public CaptureHudViewModel Hud        { get; } = new();

    public RecordingMode SelectedMode
    {
        get => _selectedMode;
        set { if (_selectedMode == value) return; _selectedMode = value; OnPropertyChanged(); }
    }

    public bool IsSessionActive
    {
        get => _isSessionActive;
        set { if (_isSessionActive == value) return; _isSessionActive = value; OnPropertyChanged(); }
    }

    // ── Commands (wired to services in later phases) ──────────────────────────

    public ICommand StartCaptureCommand  { get; private set; }
    public ICommand StopCaptureCommand   { get; private set; }
    public ICommand PauseCaptureCommand  { get; private set; }
    public ICommand CaptureFrameCommand  { get; private set; }
    public ICommand SelectRegionCommand  { get; private set; }
    public ICommand SaveSessionCommand   { get; private set; }
    public ICommand OpenSessionCommand   { get; private set; }
    public ICommand ExportGifCommand     { get; private set; }
    public ICommand ExportPngCommand     { get; private set; }
    public ICommand ExportMp4Command     { get; private set; }

    public ScreenRecorderViewModel()
    {
        StartCaptureCommand = new RelayCommand(_ => OnStartCapture(),  _ => !IsSessionActive);
        StopCaptureCommand  = new RelayCommand(_ => OnStopCapture(),   _ => IsSessionActive);
        PauseCaptureCommand = new RelayCommand(_ => OnPauseCapture(),  _ => IsSessionActive);
        CaptureFrameCommand = new RelayCommand(_ => OnCaptureFrame(),  _ => IsSessionActive);
        SelectRegionCommand = new RelayCommand(_ => OnSelectRegion());
        SaveSessionCommand  = new RelayCommand(_ => OnSaveSession());
        OpenSessionCommand  = new RelayCommand(_ => OnOpenSession());
        ExportGifCommand    = new RelayCommand(_ => OnExportGif(),     _ => Timeline.Frames.Count > 0);
        ExportPngCommand    = new RelayCommand(_ => OnExportPng(),     _ => Timeline.Frames.Count > 0);
        ExportMp4Command    = new RelayCommand(_ => OnExportMp4(),     _ => Timeline.Frames.Count > 0);
    }

    // ── Stub handlers — replaced by real service calls in Phases 3-9 ─────────

    private void OnStartCapture()  { IsSessionActive = true;  Hud.IsRecording = true; }
    private void OnStopCapture()   { IsSessionActive = false; Hud.IsRecording = false; }
    private void OnPauseCapture()  { Hud.IsRecording = !Hud.IsRecording; }
    private void OnCaptureFrame()  { }
    private void OnSelectRegion()  { }
    private void OnSaveSession()   { }
    private void OnOpenSession()   { }
    private void OnExportGif()     { }
    private void OnExportPng()     { }
    private void OnExportMp4()     { }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}
