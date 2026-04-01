// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: AnimationPreviewService.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     Plays, pauses, stops, and seeks Storyboard animations on the design canvas
//     rendered root element. Supports multi-storyboard documents.
//
// Architecture Notes:
//     Service (stateful per-session — one per split host).
//     Uses WPF Storyboard.Begin/Pause/Resume/Stop with the rendered root.
// ==========================================================

using System.Windows;
using System.Windows.Media.Animation;

namespace WpfHexEditor.Editor.XamlDesigner.Services;

/// <summary>
/// Controls animation playback on the design canvas rendered root.
/// </summary>
public sealed class AnimationPreviewService
{
    private Storyboard?    _active;
    private FrameworkElement? _root;

    // ── State ─────────────────────────────────────────────────────────────────

    public bool IsPlaying { get; private set; }

    // ── Events ────────────────────────────────────────────────────────────────

    public event EventHandler? PlaybackStateChanged;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Attaches the service to a new rendered root element.</summary>
    public void Attach(FrameworkElement? root)
    {
        Stop();
        _root = root;
    }

    /// <summary>
    /// Starts playing the first Storyboard found in <paramref name="xaml"/>.
    /// </summary>
    public void Play(string xaml)
    {
        if (_root is null) return;
        Stop();

        var sb = TryExtractFirstStoryboard(xaml);
        if (sb is null) return;

        _active = sb;
        _active.Completed += (_, _) =>
        {
            IsPlaying = false;
            PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
        };

        _active.Begin(_root, HandoffBehavior.SnapshotAndReplace, true);
        IsPlaying = true;
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Pauses the currently playing storyboard.</summary>
    public void Pause()
    {
        if (_active is null || _root is null) return;
        _active.Pause(_root);
        IsPlaying = false;
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Resumes a paused storyboard.</summary>
    public void Resume()
    {
        if (_active is null || _root is null) return;
        _active.Resume(_root);
        IsPlaying = true;
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Stops and removes the active storyboard.</summary>
    public void Stop()
    {
        if (_active is null || _root is null) return;
        _active.Stop(_root);
        _active    = null;
        IsPlaying  = false;
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Seeks the storyboard to <paramref name="time"/>.</summary>
    public void Seek(TimeSpan time)
    {
        if (_active is null || _root is null) return;
        _active.Seek(_root, time, TimeSeekOrigin.BeginTime);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private static Storyboard? TryExtractFirstStoryboard(string xaml)
    {
        try
        {
            var doc = System.Xml.Linq.XDocument.Parse(xaml);
            var sb  = doc.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "Storyboard");

            if (sb is null) return null;

            // Reconstruct a minimal XAML fragment that XamlReader can parse.
            var ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            var xns = "http://schemas.microsoft.com/winfx/2006/xaml";
            var fragment = $"<Storyboard xmlns=\"{ns}\" xmlns:x=\"{xns}\">{sb.Elements().Aggregate("", (acc, e) => acc + e.ToString())}</Storyboard>";

            if (System.Windows.Markup.XamlReader.Parse(fragment) is Storyboard result)
                return result;
        }
        catch { }

        return null;
    }
}
