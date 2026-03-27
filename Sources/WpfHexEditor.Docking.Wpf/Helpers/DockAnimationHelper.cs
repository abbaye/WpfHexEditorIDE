// ==========================================================
// Project: WpfHexEditor.Shell
// File: DockAnimationHelper.cs
// Description:
//     Static animation helpers for dock overlay fades, floating
//     window transitions, and auto-hide flyout easing.
//
// Architecture Notes:
//     All animations use FillBehavior.Stop so the final value
//     is set explicitly — avoids animation "holding" the property.
//     Durations are configurable via static properties (set from
//     AppSettings by DockControl on startup and settings change).
// ==========================================================

using System.Windows;
using System.Windows.Media.Animation;

namespace WpfHexEditor.Shell;

/// <summary>
/// Lightweight animation helpers for the docking system.
/// Durations are configurable via <see cref="OverlayFadeInMs"/>,
/// <see cref="OverlayFadeOutMs"/>, <see cref="FloatingFadeInMs"/>,
/// and <see cref="AnimationsEnabled"/>.
/// </summary>
internal static class DockAnimationHelper
{
    private static readonly QuadraticEase QuadEaseOut = new() { EasingMode = EasingMode.EaseOut };
    private static readonly CubicEase CubicEaseOut = new() { EasingMode = EasingMode.EaseOut };

    /// <summary>Master toggle. When false, all methods set final state instantly.</summary>
    public static bool AnimationsEnabled { get; set; } = true;

    /// <summary>Duration in ms for dock overlay fade-in (default 150).</summary>
    public static int OverlayFadeInMs { get; set; } = 150;

    /// <summary>Duration in ms for dock overlay fade-out (default 100).</summary>
    public static int OverlayFadeOutMs { get; set; } = 100;

    /// <summary>Duration in ms for floating window fade-in (default 120).</summary>
    public static int FloatingFadeInMs { get; set; } = 120;

    /// <summary>Fades a <see cref="UIElement"/> from 0 to 1 opacity.</summary>
    public static void FadeIn(UIElement element, int durationMs)
    {
        if (!AnimationsEnabled || durationMs <= 0)
        {
            element.Opacity = 1;
            return;
        }
        element.Opacity = 0;
        var anim = new DoubleAnimation(0, 1, Duration(durationMs))
        {
            EasingFunction = QuadEaseOut,
            FillBehavior = FillBehavior.Stop
        };
        anim.Completed += (_, _) => element.Opacity = 1;
        element.BeginAnimation(UIElement.OpacityProperty, anim);
    }

    /// <summary>Fades a <see cref="UIElement"/> from current opacity to 0, then invokes <paramref name="onComplete"/>.</summary>
    public static void FadeOut(UIElement element, int durationMs, Action? onComplete = null)
    {
        if (!AnimationsEnabled || durationMs <= 0)
        {
            element.Opacity = 0;
            onComplete?.Invoke();
            return;
        }
        var anim = new DoubleAnimation(element.Opacity, 0, Duration(durationMs))
        {
            EasingFunction = QuadEaseOut,
            FillBehavior = FillBehavior.Stop
        };
        anim.Completed += (_, _) =>
        {
            element.Opacity = 0;
            onComplete?.Invoke();
        };
        element.BeginAnimation(UIElement.OpacityProperty, anim);
    }

    /// <summary>Fades a <see cref="Window"/> from 0 to 1 opacity.</summary>
    public static void FadeInWindow(Window window, int durationMs)
    {
        if (!AnimationsEnabled || durationMs <= 0)
        {
            window.Opacity = 1;
            return;
        }
        window.Opacity = 0;
        var anim = new DoubleAnimation(0, 1, Duration(durationMs))
        {
            EasingFunction = QuadEaseOut,
            FillBehavior = FillBehavior.Stop
        };
        anim.Completed += (_, _) => window.Opacity = 1;
        window.BeginAnimation(Window.OpacityProperty, anim);
    }

    /// <summary>Fades a <see cref="Window"/> to 0 then invokes <paramref name="onComplete"/>.</summary>
    public static void FadeOutWindow(Window window, int durationMs, Action? onComplete = null)
    {
        if (!AnimationsEnabled || durationMs <= 0)
        {
            window.Opacity = 0;
            onComplete?.Invoke();
            return;
        }
        var anim = new DoubleAnimation(window.Opacity, 0, Duration(durationMs))
        {
            EasingFunction = QuadEaseOut,
            FillBehavior = FillBehavior.Stop
        };
        anim.Completed += (_, _) =>
        {
            window.Opacity = 0;
            onComplete?.Invoke();
        };
        window.BeginAnimation(Window.OpacityProperty, anim);
    }

    /// <summary>Returns a <see cref="CubicEase"/> with EaseOut for auto-hide flyout slide animations.</summary>
    public static CubicEase GetFlyoutEase() => CubicEaseOut;

    private static Duration Duration(int ms) => new(TimeSpan.FromMilliseconds(ms));
}
