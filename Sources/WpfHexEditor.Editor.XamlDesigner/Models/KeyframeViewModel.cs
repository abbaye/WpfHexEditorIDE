// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: KeyframeViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Updated: 2026-03-22 — Moved from ViewModels/ to Models/
//                        (used by StoryboardSyncService and StoryboardExportService).
// Description:
//     Domain model representing a single animation keyframe on the timeline.
//     Wraps TimeSpan position, animated value, and easing function name.
//     Built by StoryboardSyncService; consumed by AnimationTrackViewModel and
//     AnimationTimelinePanelViewModel (plugin).
//
// Architecture Notes:
//     INPC. IsSelected for timeline thumb selection.
// ==========================================================

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfHexEditor.Editor.XamlDesigner.Models;

/// <summary>
/// A single keyframe on the animation timeline.
/// </summary>
public sealed class KeyframeViewModel : INotifyPropertyChanged
{
    private TimeSpan _time;
    private string   _value = string.Empty;
    private string   _easingFunction = "Linear";
    private bool     _isSelected;

    public TimeSpan Time
    {
        get => _time;
        set { _time = value; OnPropertyChanged(); OnPropertyChanged(nameof(TimeMs)); }
    }

    public double TimeMs
    {
        get => _time.TotalMilliseconds;
        set { _time = TimeSpan.FromMilliseconds(value); OnPropertyChanged(); OnPropertyChanged(nameof(Time)); }
    }

    public string Value
    {
        get => _value;
        set { _value = value; OnPropertyChanged(); }
    }

    public string EasingFunction
    {
        get => _easingFunction;
        set { _easingFunction = value; OnPropertyChanged(); }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public static IReadOnlyList<string> KnownEasingFunctions { get; } =
    [
        "Linear",
        "SineEase", "QuadraticEase", "CubicEase", "QuarticEase", "QuinticEase",
        "ExponentialEase", "CircleEase", "BackEase", "BounceEase", "ElasticEase",
        "PowerEase"
    ];

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
