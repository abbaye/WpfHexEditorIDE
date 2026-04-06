// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: AnimationTrackViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Updated: 2026-03-22 â€” Moved from ViewModels/ to Models/
//                        (used by StoryboardSyncService and StoryboardExportService).
// Description:
//     Domain model for a single animation track in the Timeline panel.
//     Represents one animated property on one target element.
//     Contains an ordered list of KeyframeViewModels.
//     Built by StoryboardSyncService; consumed by AnimationTimelinePanelViewModel (plugin).
//
// Architecture Notes:
//     INPC. ObservableCollection<KeyframeViewModel> for binding to the timeline ruler.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Editor.XamlDesigner.Models;

/// <summary>
/// One animated property track on the timeline.
/// </summary>
public sealed class AnimationTrackViewModel : ViewModelBase
{
    private bool _isExpanded    = true;
    private bool _isContextMatch;

    // ── Constructor ───────────────────────────────────────────────────────────

    public AnimationTrackViewModel(string targetName, string propertyName)
    {
        TargetName    = targetName;
        PropertyName  = propertyName;
        TrackLabel    = string.IsNullOrEmpty(targetName)
            ? propertyName
            : $"{targetName}.{propertyName}";
        PropertyIcon  = ResolvePropertyIcon(propertyName);
    }

    // ── Properties ────────────────────────────────────────────────────────────

    public string TargetName   { get; }
    public string PropertyName { get; }
    public string TrackLabel   { get; }

    /// <summary>
    /// Segoe MDL2 glyph that represents the type of the animated property.
    /// (e.g. double=\uE8F4, Color=\uE790, Thickness=\uE8A0, Point=\uE80F)
    /// </summary>
    public string PropertyIcon { get; }

    /// <summary>True when this track's TargetName matches the currently selected canvas element.</summary>
    public bool IsContextMatch
    {
        get => _isContextMatch;
        set { _isContextMatch = value; OnPropertyChanged(); }
    }

    public ObservableCollection<KeyframeViewModel> Keyframes { get; } = new();

    public bool IsExpanded
    {
        get => _isExpanded;
        set { _isExpanded = value; OnPropertyChanged(); }
    }

    // ── INPC ──────────────────────────────────────────────────────────────────



    // ── Private ───────────────────────────────────────────────────────────────

    private static string ResolvePropertyIcon(string propertyName) => propertyName switch
    {
        "Opacity"              => "\uE7A7",
        "Width" or "Height"    => "\uE8F4",
        "Margin" or "Padding"  => "\uE8A0",
        "Background"
         or "Foreground"
         or "Fill"
         or "Stroke"
         or "BorderBrush"      => "\uE790",
        "FontSize"             => "\uE8D2",
        "FontFamily"           => "\uE8D2",
        "Visibility"           => "\uE7B3",
        "RenderTransform"
         or "LayoutTransform"  => "\uE8CB",
        "Canvas.Left"
         or "Canvas.Top"       => "\uE80F",
        _                      => "\uE8F4"
    };
}
