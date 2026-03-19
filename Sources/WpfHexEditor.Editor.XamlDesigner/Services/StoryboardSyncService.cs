// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: StoryboardSyncService.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     Parses Storyboard elements from XAML and builds AnimationTrackViewModel
//     instances. Also serializes ViewModel changes back to XAML Storyboard nodes.
//
// Architecture Notes:
//     Service — stateless parse + serialize helpers.
//     Uses System.Xml.Linq for XDocument manipulation.
//     Supports DoubleAnimation and ColorAnimation (most common cases).
// ==========================================================

using System.Xml.Linq;
using WpfHexEditor.Editor.XamlDesigner.ViewModels;

namespace WpfHexEditor.Editor.XamlDesigner.Services;

/// <summary>
/// Parses XAML Storyboard nodes into animation track view models and
/// serializes track changes back to XAML.
/// </summary>
public sealed class StoryboardSyncService
{
    private static readonly XNamespace Wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    // ── Parsing ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses all Storyboard/BeginStoryboard nodes in <paramref name="xaml"/>
    /// and returns a list of animation tracks.
    /// Returns an empty list if no storyboards are found.
    /// </summary>
    public IReadOnlyList<AnimationTrackViewModel> ParseTracks(string xaml)
    {
        if (string.IsNullOrWhiteSpace(xaml)) return Array.Empty<AnimationTrackViewModel>();

        try
        {
            var doc    = XDocument.Parse(xaml, LoadOptions.PreserveWhitespace);
            var tracks = new List<AnimationTrackViewModel>();

            foreach (var storyboard in doc.Descendants().Where(e =>
                e.Name.LocalName == "Storyboard"))
            {
                ParseStoryboard(storyboard, tracks);
            }

            return tracks;
        }
        catch
        {
            return Array.Empty<AnimationTrackViewModel>();
        }
    }

    // ── Serialization ─────────────────────────────────────────────────────────

    /// <summary>
    /// Applies keyframe changes from <paramref name="track"/> back into the
    /// first matching animation element in <paramref name="rawXaml"/>.
    /// Returns the updated XAML string.
    /// </summary>
    public string ApplyTrackChanges(string rawXaml, AnimationTrackViewModel track)
    {
        if (string.IsNullOrWhiteSpace(rawXaml)) return rawXaml;

        try
        {
            var doc = XDocument.Parse(rawXaml, LoadOptions.PreserveWhitespace);

            foreach (var anim in doc.Descendants().Where(e =>
                (e.Name.LocalName == "DoubleAnimation" || e.Name.LocalName == "ColorAnimation")
                && e.Attribute("Storyboard.TargetProperty")?.Value == track.PropertyName
                && (string.IsNullOrEmpty(track.TargetName) ||
                    e.Attribute("Storyboard.TargetName")?.Value == track.TargetName)))
            {
                // Update keyframe values for KeyFrame children.
                var keyFrames = anim.Elements().ToList();
                var vm        = track.Keyframes.ToList();
                int count     = Math.Min(keyFrames.Count, vm.Count);

                for (int i = 0; i < count; i++)
                {
                    keyFrames[i].SetAttributeValue("KeyTime", $"0:0:{vm[i].Time.TotalSeconds:F2}");
                    keyFrames[i].SetAttributeValue("Value",   vm[i].Value);
                }

                break;
            }

            var sb = new System.Text.StringBuilder();
            using var writer = System.Xml.XmlWriter.Create(sb,
                new System.Xml.XmlWriterSettings
                {
                    OmitXmlDeclaration = true,
                    Indent             = false
                });
            doc.WriteTo(writer);
            writer.Flush();
            return sb.ToString();
        }
        catch
        {
            return rawXaml;
        }
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private static void ParseStoryboard(XElement storyboard, List<AnimationTrackViewModel> tracks)
    {
        foreach (var anim in storyboard.Descendants())
        {
            if (anim.Name.LocalName is not "DoubleAnimation" and not "ColorAnimation"
                and not "DoubleAnimationUsingKeyFrames" and not "ColorAnimationUsingKeyFrames")
                continue;

            string targetName = anim.Attribute("Storyboard.TargetName")?.Value ?? string.Empty;
            string prop       = anim.Attribute("Storyboard.TargetProperty")?.Value ?? anim.Name.LocalName;

            var track = new AnimationTrackViewModel(targetName, prop);

            // Simple From/To animation.
            string? from = anim.Attribute("From")?.Value;
            string? to   = anim.Attribute("To")?.Value;

            if (from is not null || to is not null)
            {
                if (from is not null)
                    track.Keyframes.Add(new KeyframeViewModel { Time = TimeSpan.Zero, Value = from });
                if (to is not null)
                {
                    string? dur = anim.Attribute("Duration")?.Value;
                    var dur_ts  = TryParseDuration(dur) ?? TimeSpan.FromSeconds(1);
                    track.Keyframes.Add(new KeyframeViewModel { Time = dur_ts, Value = to });
                }
            }
            else
            {
                // KeyFrame-based animation.
                foreach (var kf in anim.Elements())
                {
                    string? keyTime = kf.Attribute("KeyTime")?.Value;
                    string? val     = kf.Attribute("Value")?.Value;
                    if (val is null) continue;

                    track.Keyframes.Add(new KeyframeViewModel
                    {
                        Time  = TryParseDuration(keyTime) ?? TimeSpan.Zero,
                        Value = val
                    });
                }
            }

            tracks.Add(track);
        }
    }

    private static TimeSpan? TryParseDuration(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (TimeSpan.TryParse(s, out var ts)) return ts;

        // Handle "0:0:1.0" style.
        var parts = s.Split(':');
        if (parts.Length == 3
            && double.TryParse(parts[0], out double h)
            && double.TryParse(parts[1], out double m)
            && double.TryParse(parts[2], out double sec))
        {
            return TimeSpan.FromSeconds(h * 3600 + m * 60 + sec);
        }

        return null;
    }
}
