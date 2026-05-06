// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: KeyframeViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Updated: 2026-03-22 â€” Moved from ViewModels/ to Models/
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
using System.Windows;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Editor.XamlDesigner.Models;

/// <summary>
/// A single keyframe on the animation timeline.
/// </summary>
public sealed class KeyframeViewModel : ViewModelBase
{
    private TimeSpan _time;
    private string   _value = string.Empty;
    private string   _easingFunction = "Linear";
    private bool     _isSelected;
    private Point    _easingP1 = new(0.25, 0.1);
    private Point    _easingP2 = new(0.25, 1.0);

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

    /// <summary>Bezier control point 1 in normalized 0..1 space (KeySpline format).</summary>
    public Point EasingP1
    {
        get => _easingP1;
        set { _easingP1 = value; OnPropertyChanged(); }
    }

    /// <summary>Bezier control point 2 in normalized 0..1 space (KeySpline format).</summary>
    public Point EasingP2
    {
        get => _easingP2;
        set { _easingP2 = value; OnPropertyChanged(); }
    }

    public static IReadOnlyList<string> KnownEasingFunctions { get; } =
    [
        "Linear",
        "SineEase", "QuadraticEase", "CubicEase", "QuarticEase", "QuinticEase",
        "ExponentialEase", "CircleEase", "BackEase", "BounceEase", "ElasticEase",
        "PowerEase"
    ];


}
