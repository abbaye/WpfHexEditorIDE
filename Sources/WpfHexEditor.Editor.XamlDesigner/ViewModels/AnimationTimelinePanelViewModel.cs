// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: AnimationTimelinePanelViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     ViewModel for the Animation Timeline panel.
//     Coordinates the AnimationPreviewService (playback) and
//     StoryboardSyncService (parse/serialize) with the timeline UI.
//
// Architecture Notes:
//     INPC + RelayCommand.
//     AnimationPreviewService is injected by XamlDesignerSplitHost after
//     the DesignCanvas root is attached.
//     XamlSource property drives track re-parse on every XAML change.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.SDK.Commands;
using System.Windows;
using System.Windows.Input;
using WpfHexEditor.Editor.XamlDesigner.Services;

namespace WpfHexEditor.Editor.XamlDesigner.ViewModels;

/// <summary>
/// ViewModel for the animation timeline dockable panel.
/// </summary>
public sealed class AnimationTimelinePanelViewModel : INotifyPropertyChanged
{
    private readonly StoryboardSyncService   _syncService   = new();
    private AnimationPreviewService?         _previewService;

    private string  _xamlSource = string.Empty;
    private bool    _isPlaying;
    private TimeSpan _currentTime;
    private TimeSpan _duration   = TimeSpan.FromSeconds(2);
    private AnimationTrackViewModel? _selectedTrack;

    // ── Constructor ───────────────────────────────────────────────────────────

    public AnimationTimelinePanelViewModel()
    {
        PlayCommand  = new RelayCommand(_ => Play(),  _ => !_isPlaying && _previewService is not null);
        PauseCommand = new RelayCommand(_ => Pause(), _ =>  _isPlaying);
        StopCommand  = new RelayCommand(_ => Stop(),  _ => _previewService is not null);

        SeekCommand  = new RelayCommand(p =>
        {
            if (p is TimeSpan ts) Seek(ts);
        });
    }

    // ── Properties ────────────────────────────────────────────────────────────

    /// <summary>Current XAML being shown in the designer. Triggers track re-parse.</summary>
    public string XamlSource
    {
        get => _xamlSource;
        set
        {
            _xamlSource = value;
            RefreshTracks();
            UpdateDurationFromTracks();
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

    public ObservableCollection<AnimationTrackViewModel> Tracks { get; } = new();

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand PlayCommand  { get; }
    public ICommand PauseCommand { get; }
    public ICommand StopCommand  { get; }
    public ICommand SeekCommand  { get; }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Attaches a preview service (called by XamlDesignerSplitHost when the
    /// DesignCanvas root is available).
    /// </summary>
    public void AttachPreviewService(AnimationPreviewService service)
    {
        _previewService = service;
        _previewService.PlaybackStateChanged += OnPlaybackStateChanged;
    }

    // ── Private ───────────────────────────────────────────────────────────────

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
        IsPlaying  = false;
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

    // ── INPC ──────────────────────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
