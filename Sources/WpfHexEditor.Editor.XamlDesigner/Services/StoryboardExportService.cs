// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: StoryboardExportService.cs
// Author: Derek Tremblay
// Created: 2026-03-19
// Description:
//     Serializes animation tracks and keyframes from the Timeline panel
//     back to XAML Storyboard markup, ready to paste into any XAML file.
//
// Architecture Notes:
//     Pure service — no WPF UI dependencies.
//     Builder pattern: a single StringBuilder is populated in a single
//     pass over the track/keyframe collections.
//     TimeSpan formatting uses the {0:hh\:mm\:ss\.fffffff} round-trip
//     form compatible with XAML Duration/KeyTime converters.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WpfHexEditor.Editor.XamlDesigner.ViewModels;

namespace WpfHexEditor.Editor.XamlDesigner.Services;

/// <summary>
/// Exports the animation tracks currently held in the XAML Designer timeline
/// to a ready-to-use XAML <c>Storyboard</c> fragment.
/// </summary>
public sealed class StoryboardExportService
{
    // ── Constants ─────────────────────────────────────────────────────────────

    private const string XmlnsWpf    = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private const string XmlnsX      = "http://schemas.microsoft.com/winfx/2006/xaml";
    private const string Indent      = "    ";

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Serializes <paramref name="tracks"/> to a XAML <c>Storyboard</c> string.
    /// </summary>
    /// <param name="tracks">The animation tracks to export.</param>
    /// <param name="duration">Total duration of the storyboard.</param>
    /// <param name="storyboardName">
    /// Value placed in the <c>x:Name</c> attribute of the root Storyboard element.
    /// </param>
    /// <returns>A well-formed XAML fragment containing the Storyboard.</returns>
    public string ExportToXaml(
        IEnumerable<AnimationTrackViewModel> tracks,
        TimeSpan duration,
        string storyboardName)
    {
        if (tracks is null)
            throw new ArgumentNullException(nameof(tracks));

        storyboardName = NormalizeName(storyboardName);

        var builder = new StringBuilder();
        List<AnimationTrackViewModel> trackList = tracks.ToList();

        AppendStoryboardOpen(builder, storyboardName, duration, trackList.Count == 0);

        foreach (AnimationTrackViewModel track in trackList)
            AppendTrack(builder, track, duration);

        if (trackList.Count > 0)
            builder.AppendLine("</Storyboard>");

        return builder.ToString();
    }

    // ── Private: storyboard-level XML ────────────────────────────────────────

    private static void AppendStoryboardOpen(
        StringBuilder builder,
        string name,
        TimeSpan duration,
        bool selfClose)
    {
        builder.Append("<Storyboard");
        builder.Append($" xmlns=\"{XmlnsWpf}\"");
        builder.Append($" xmlns:x=\"{XmlnsX}\"");
        builder.Append($" x:Name=\"{name}\"");
        builder.Append($" Duration=\"{FormatDuration(duration)}\"");

        if (selfClose)
            builder.AppendLine(" />");
        else
            builder.AppendLine(">");
    }

    // ── Private: per-track XML ────────────────────────────────────────────────

    private static void AppendTrack(
        StringBuilder builder,
        AnimationTrackViewModel track,
        TimeSpan storyboardDuration)
    {
        List<KeyframeViewModel> keyframes = track.Keyframes
            .OrderBy(k => k.Time)
            .ToList();

        if (keyframes.Count == 0)
        {
            AppendSimpleDoubleAnimation(builder, track, storyboardDuration);
            return;
        }

        AppendDoubleAnimationUsingKeyFrames(builder, track, keyframes);
    }

    private static void AppendSimpleDoubleAnimation(
        StringBuilder builder,
        AnimationTrackViewModel track,
        TimeSpan storyboardDuration)
    {
        builder.Append(Indent);
        builder.Append("<DoubleAnimation");
        AppendTargetAttributes(builder, track);
        builder.Append($" Duration=\"{FormatDuration(storyboardDuration)}\"");
        builder.AppendLine(" />");
    }

    private static void AppendDoubleAnimationUsingKeyFrames(
        StringBuilder builder,
        AnimationTrackViewModel track,
        List<KeyframeViewModel> keyframes)
    {
        builder.Append(Indent);
        builder.Append("<DoubleAnimationUsingKeyFrames");
        AppendTargetAttributes(builder, track);
        builder.AppendLine(">");

        foreach (KeyframeViewModel keyframe in keyframes)
            AppendKeyframe(builder, keyframe);

        builder.Append(Indent);
        builder.AppendLine("</DoubleAnimationUsingKeyFrames>");
    }

    private static void AppendTargetAttributes(StringBuilder builder, AnimationTrackViewModel track)
    {
        if (!string.IsNullOrWhiteSpace(track.TargetName))
            builder.Append($" Storyboard.TargetName=\"{track.TargetName}\"");

        builder.Append($" Storyboard.TargetProperty=\"{track.PropertyName}\"");
    }

    // ── Private: per-keyframe XML ─────────────────────────────────────────────

    private static void AppendKeyframe(StringBuilder builder, KeyframeViewModel keyframe)
    {
        // Resolve the XAML element name from the easing function stored on the keyframe.
        bool isLinear = string.IsNullOrEmpty(keyframe.EasingFunction)
                        || keyframe.EasingFunction == "Linear";

        builder.Append(Indent + Indent);
        builder.Append(isLinear ? "<LinearDoubleKeyFrame" : "<EasingDoubleKeyFrame");
        builder.Append($" KeyTime=\"{FormatKeyTime(keyframe.Time)}\"");
        builder.Append($" Value=\"{keyframe.Value}\"");

        if (!isLinear)
        {
            // Emit the easing function as a child element for non-linear keyframes.
            builder.AppendLine(">");
            builder.Append(Indent + Indent + Indent);
            builder.AppendLine($"<EasingDoubleKeyFrame.EasingFunction>");
            builder.Append(Indent + Indent + Indent + Indent);
            builder.AppendLine($"<{keyframe.EasingFunction} />");
            builder.Append(Indent + Indent + Indent);
            builder.AppendLine("</EasingDoubleKeyFrame.EasingFunction>");
            builder.Append(Indent + Indent);
            builder.AppendLine("</EasingDoubleKeyFrame>");
        }
        else
        {
            builder.AppendLine(" />");
        }
    }

    // ── Private: formatting helpers ───────────────────────────────────────────

    // XAML Duration format: "0:0:1.0000000" — hh:mm:ss.fffffff
    private static string FormatDuration(TimeSpan ts)
        => $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds * 10000:D7}";

    // KeyTime uses the same format wrapped in a TimeSpan literal.
    private static string FormatKeyTime(TimeSpan ts)
        => FormatDuration(ts);

    private static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "AnimationStoryboard";

        // Replace spaces and hyphens; XAML names must be valid identifiers.
        return name.Replace(' ', '_').Replace('-', '_');
    }
}
