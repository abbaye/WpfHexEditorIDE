// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: AnimationTrackViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     ViewModel for a single animation track in the Timeline panel.
//     Represents one animated property on one target element.
//     Contains an ordered list of KeyframeViewModels.
//
// Architecture Notes:
//     INPC. ObservableCollection<KeyframeViewModel> for binding to the timeline ruler.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfHexEditor.Editor.XamlDesigner.ViewModels;

/// <summary>
/// One animated property track on the timeline.
/// </summary>
public sealed class AnimationTrackViewModel : INotifyPropertyChanged
{
    private bool _isExpanded = true;

    // ── Constructor ───────────────────────────────────────────────────────────

    public AnimationTrackViewModel(string targetName, string propertyName)
    {
        TargetName    = targetName;
        PropertyName  = propertyName;
        TrackLabel    = string.IsNullOrEmpty(targetName)
            ? propertyName
            : $"{targetName}.{propertyName}";
    }

    // ── Properties ────────────────────────────────────────────────────────────

    public string TargetName   { get; }
    public string PropertyName { get; }
    public string TrackLabel   { get; }

    public ObservableCollection<KeyframeViewModel> Keyframes { get; } = new();

    public bool IsExpanded
    {
        get => _isExpanded;
        set { _isExpanded = value; OnPropertyChanged(); }
    }

    // ── INPC ──────────────────────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
