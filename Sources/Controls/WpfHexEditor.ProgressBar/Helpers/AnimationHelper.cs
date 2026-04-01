// ==========================================================
// Project: WpfHexEditor.ProgressBar
// File: Helpers/AnimationHelper.cs
// Description:
//     Shared CompositionTarget.Rendering engine with ref-counting.
//     Controls register/unregister callbacks; the engine only
//     subscribes to the rendering event when at least one control
//     is active, avoiding idle overhead.
// ==========================================================

using System.Windows.Media;

namespace WpfHexEditor.ProgressBar.Helpers;

/// <summary>
/// Centralized animation tick engine using <see cref="CompositionTarget.Rendering"/>.
/// Ref-counted: subscribes only when ≥1 callback is registered.
/// </summary>
internal static class AnimationHelper
{
    private static readonly List<Action<TimeSpan>> s_callbacks = [];
    private static readonly object s_lock = new();
    private static bool s_subscribed;
    private static TimeSpan s_lastTimestamp;

    /// <summary>Registers a per-frame callback. Returns a disposable that unregisters it.</summary>
    internal static IDisposable Register(Action<TimeSpan> onFrame)
    {
        lock (s_lock)
        {
            s_callbacks.Add(onFrame);
            if (!s_subscribed)
            {
                CompositionTarget.Rendering += OnRendering;
                s_subscribed = true;
            }
        }
        return new Unsubscriber(onFrame);
    }

    private static void OnRendering(object? sender, EventArgs e)
    {
        var args = (RenderingEventArgs)e;
        var elapsed = args.RenderingTime - s_lastTimestamp;
        s_lastTimestamp = args.RenderingTime;

        // Snapshot to avoid lock during iteration.
        Action<TimeSpan>[] snapshot;
        lock (s_lock) snapshot = [.. s_callbacks];

        foreach (var cb in snapshot)
            cb(elapsed);
    }

    private static void Unregister(Action<TimeSpan> onFrame)
    {
        lock (s_lock)
        {
            s_callbacks.Remove(onFrame);
            if (s_callbacks.Count == 0 && s_subscribed)
            {
                CompositionTarget.Rendering -= OnRendering;
                s_subscribed = false;
            }
        }
    }

    /// <summary>Quadratic ease-out interpolation.</summary>
    internal static double EaseOut(double from, double to, double t)
    {
        t = Math.Clamp(t, 0, 1);
        t = 1 - (1 - t) * (1 - t); // quadratic ease-out
        return from + (to - from) * t;
    }

    private sealed class Unsubscriber(Action<TimeSpan> callback) : IDisposable
    {
        private Action<TimeSpan>? _callback = callback;

        public void Dispose()
        {
            if (_callback is { } cb)
            {
                _callback = null;
                Unregister(cb);
            }
        }
    }
}
