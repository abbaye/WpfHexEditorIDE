// ==========================================================
// Project: WpfHexEditor.Plugins.XamlDesigner
// File: AnimationTimelinePanelViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Updated: 2026-03-19
//          2026-03-22 â€” Moved to plugin project (WpfHexEditor.Plugins.XamlDesigner.ViewModels).
// Description:
//     ViewModel for the Animation Timeline panel.
//     Coordinates the AnimationPreviewService (playback) and
//     StoryboardSyncService (parse/serialize) with the timeline UI.
//
// Architecture: Plugin-owned panel ViewModel; uses StoryboardSyncService + AnimationPreviewService
//               from editor core. Domain models (AnimationTrackViewModel, KeyframeViewModel)
//               from WpfHexEditor.Editor.XamlDesigner.Models.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using WpfHexEditor.Editor.XamlDesigner.Models;
using WpfHexEditor.Editor.XamlDesigner.Services;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Plugins.XamlDesigner.ViewModels;

/// <summary>
/// ViewModel for the animation timeline dockable panel.
/// </summary>
public sealed class AnimationTimelinePanelViewModel : ViewModelBase
{
    private readonly StoryboardSyncService    _syncService     = new();
    private readonly StoryboardExportService  _exportService   = new();
    private AnimationPreviewService?          _previewService;

    private string   _xamlSource          = string.Empty;
    private bool     _isPlaying;
    private TimeSpan _currentTime;
    private TimeSpan _duration            = TimeSpan.FromSeconds(2);
    private int      _frameRate           = 30;
    private bool     _isLooping;
    private bool     _autoReverse;
    private string   _activeStoryboardName = string.Empty;
    private AnimationTrackViewModel? _selectedTrack;

    // ── Constructor ───────────────────────────────────────────────────────────

    public AnimationTimelinePanelViewModel()
    {
        PlayCommand    = new RelayCommand(_ => Play(),  _ => !_isPlaying && _previewService is not null);
        PauseCommand   = new RelayCommand(_ => Pause(), _ =>  _isPlaying);
        StopCommand    = new RelayCommand(_ => Stop(),  _ => _previewService is not null);
        SeekCommand    = new RelayCommand(p => { if (p is TimeSpan ts) Seek(ts); });

        AddKeyframeCommand      = new RelayCommand(_ => ExecuteAddKeyframe(),    _ => _selectedTrack is not null);
        DeleteKeyframeCommand   = new RelayCommand(p => ExecuteDeleteKeyframe(p as KeyframeViewModel));
        ExportStoryboardCommand = new RelayCommand(_ => ExecuteExport());
        AddTrackCommand         = new RelayCommand(_ => ExecuteAddTrack());
        RemoveTrackCommand      = new RelayCommand(p => ExecuteRemoveTrack(p as AnimationTrackViewModel));
        MoveKeyframeCommand     = new RelayCommand(p => ExecuteMoveKeyframe(p));
        ZoomInCommand           = new RelayCommand(_ => PixelsPerSecond *= 1.5);
        ZoomOutCommand          = new RelayCommand(_ => PixelsPerSecond /= 1.5);
    }

    // ── Static data ───────────────────────────────────────────────────────────

    public static IReadOnlyList<int> AvailableFrameRates { get; } = [12, 24, 30, 60];
    public static readonly IReadOnlyList<double> AvailablePlaybackSpeeds = [0.25, 0.5, 1.0, 2.0];

    // ── Properties ────────────────────────────────────────────────────────────

    public string XamlSource
    {
        get => _xamlSource;
        set
        {
            _xamlSource = value;
            RefreshTracks();
            UpdateDurationFromTracks();
            RefreshStoryboardNames();
            OnPropertyChanged();
        }
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        private set { if (_isPlaying == value) return; _isPlaying = value; OnPropertyChanged(); }
    }

    public TimeSpan CurrentTime
    {
        get => _currentTime;
        private set { if (_currentTime == value) return; _currentTime = value; OnPropertyChanged(); }
    }

    public TimeSpan Duration
    {
        get => _duration;
        private set { if (_duration == value) return; _duration = value; OnPropertyChanged(); }
    }

    public AnimationTrackViewModel? SelectedTrack
    {
        get => _selectedTrack;
        set { if (_selectedTrack == value) return; _selectedTrack = value; OnPropertyChanged(); }
    }

    public int FrameRate
    {
        get => _frameRate;
        set
        {
            if (!AvailableFrameRates.Contains(value)) return;
            if (_frameRate == value) return;
            _frameRate = value;
            OnPropertyChanged();
        }
    }

    public bool IsLooping
    {
        get => _isLooping;
        set { if (_isLooping == value) return; _isLooping = value; OnPropertyChanged(); }
    }

    public bool AutoReverse
    {
        get => _autoReverse;
        set { if (_autoReverse == value) return; _autoReverse = value; OnPropertyChanged(); }
    }

    private double _playbackSpeed = 1.0;

    public double PlaybackSpeed
    {
        get => _playbackSpeed;
        set
        {
            if (Math.Abs(_playbackSpeed - value) < 0.001) return;
            _playbackSpeed = value;
            OnPropertyChanged();
        }
    }

    private double _pixelsPerSecond = 80.0;

    public double PixelsPerSecond
    {
        get => _pixelsPerSecond;
        set
        {
            var clamped = Math.Clamp(value, 20.0, 400.0);
            if (Math.Abs(_pixelsPerSecond - clamped) < 0.1) return;
            _pixelsPerSecond = clamped;
            OnPropertyChanged();
        }
    }

    public string ActiveStoryboardName
    {
        get => _activeStoryboardName;
        set { if (_activeStoryboardName == value) return; _activeStoryboardName = value; OnPropertyChanged(); }
    }

    public ObservableCollection<AnimationTrackViewModel> Tracks          { get; } = new();
    public ObservableCollection<string>                  StoryboardNames { get; } = new();

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand PlayCommand             { get; }
    public ICommand PauseCommand            { get; }
    public ICommand StopCommand             { get; }
    public ICommand SeekCommand             { get; }
    public ICommand AddKeyframeCommand      { get; }
    public ICommand DeleteKeyframeCommand   { get; }
    public ICommand ExportStoryboardCommand { get; }
    public ICommand AddTrackCommand         { get; }
    public ICommand RemoveTrackCommand      { get; }
    public ICommand MoveKeyframeCommand     { get; }
    public ICommand ZoomInCommand           { get; }
    public ICommand ZoomOutCommand          { get; }

    // ── Public API ────────────────────────────────────────────────────────────

    public void AttachPreviewService(AnimationPreviewService service)
    {
        _previewService = service;
        _previewService.PlaybackStateChanged += OnPlaybackStateChanged;
    }

    public void SetContextElement(string? elementName)
    {
        foreach (var track in Tracks)
            track.IsContextMatch = !string.IsNullOrEmpty(elementName)
                                   && track.TargetName == elementName;
    }

    // ── Private: playback ─────────────────────────────────────────────────────

    private void Play()
    {
        _previewService?.Play(_xamlSource);
        IsPlaying = true;
    }

    private void Pause()
    {
        _previewService?.Pause();
        IsPlaying = false;
    }

    private void Stop()
    {
        _previewService?.Stop();
        IsPlaying   = false;
        CurrentTime = TimeSpan.Zero;
    }

    private void Seek(TimeSpan time)
    {
        CurrentTime = time;
        _previewService?.Seek(time);
    }

    private void OnPlaybackStateChanged(object? sender, EventArgs e)
    {
        if (_previewService is not null)
            IsPlaying = _previewService.IsPlaying;
    }

    // ── Private: commands ─────────────────────────────────────────────────────

    private void ExecuteAddKeyframe()
    {
        if (_selectedTrack is null) return;

        var kf = new KeyframeViewModel { Time = _currentTime, Value = "0" };
        _selectedTrack.Keyframes.Add(kf);
        OnPropertyChanged(nameof(Tracks));
    }

    private void ExecuteDeleteKeyframe(KeyframeViewModel? kf)
    {
        if (kf is null || _selectedTrack is null) return;
        _selectedTrack.Keyframes.Remove(kf);
        OnPropertyChanged(nameof(Tracks));
    }

    private void ExecuteExport()
    {
        string name   = string.IsNullOrWhiteSpace(_activeStoryboardName) ? "AnimationStoryboard" : _activeStoryboardName;
        string xaml   = _exportService.ExportToXaml(Tracks, _duration, name);
        System.Windows.Clipboard.SetText(xaml);
    }

    private void ExecuteAddTrack()
    {
        var track = new AnimationTrackViewModel("NewTarget", "Opacity");
        Tracks.Add(track);
    }

    private void ExecuteRemoveTrack(AnimationTrackViewModel? track)
    {
        if (track is null) return;
        Tracks.Remove(track);
        if (SelectedTrack == track)
            SelectedTrack = null;
    }

    private void ExecuteMoveKeyframe(object? param)
    {
        if (param is not (KeyframeViewModel kf, TimeSpan newTime)) return;
        kf.Time = newTime;
    }

    // ── Private: parse ────────────────────────────────────────────────────────

    private void RefreshTracks()
    {
        Tracks.Clear();
        foreach (var track in _syncService.ParseTracks(_xamlSource))
            Tracks.Add(track);
        if (SelectedTrack is not null && !Tracks.Contains(SelectedTrack))
            SelectedTrack = null;
    }

    private void UpdateDurationFromTracks()
    {
        if (Tracks.Count == 0) return;

        double maxMs = 0;
        foreach (var track in Tracks)
            foreach (var kf in track.Keyframes)
                if (kf.Time.TotalMilliseconds > maxMs)
                    maxMs = kf.Time.TotalMilliseconds;

        if (maxMs > 0)
            Duration = TimeSpan.FromMilliseconds(Math.Max(maxMs * 1.2, maxMs + 500));
    }

    private void RefreshStoryboardNames()
    {
        var names = Tracks
            .Select(t => t.TargetName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        StoryboardNames.Clear();
        foreach (var name in names)
            StoryboardNames.Add(name);

        if (StoryboardNames.Count > 0 && string.IsNullOrEmpty(_activeStoryboardName))
            ActiveStoryboardName = StoryboardNames[0];
    }

    // ── INPC ──────────────────────────────────────────────────────────────────


}
