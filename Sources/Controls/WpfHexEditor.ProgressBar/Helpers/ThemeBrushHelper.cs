// ==========================================================
// Project: WpfHexEditor.ProgressBar
// File: Helpers/ThemeBrushHelper.cs
// Description:
//     Resolves brushes with a 3-tier fallback:
//     1. Explicit brush set on the DP
//     2. DynamicResource from the host application theme
//     3. Hardcoded fallback color
// ==========================================================

using System.Windows;
using System.Windows.Media;

namespace WpfHexEditor.ProgressBar.Helpers;

/// <summary>
/// Resolves a <see cref="Brush"/> from an explicit value, a theme resource key, or a fallback color.
/// Returned brushes are always frozen for rendering performance.
/// </summary>
internal static class ThemeBrushHelper
{
    // Small cache of frozen fallback brushes keyed by ARGB to avoid re-allocation.
    private static readonly Dictionary<uint, SolidColorBrush> s_cache = [];

    /// <summary>
    /// Resolves a brush using the 3-tier strategy:
    /// explicit DP value → <see cref="FrameworkElement.TryFindResource"/> → hardcoded fallback.
    /// </summary>
    internal static Brush Resolve(FrameworkElement element, Brush? explicitBrush, string resourceKey, Color fallback)
    {
        if (explicitBrush is not null)
            return Freeze(explicitBrush);

        if (element.TryFindResource(resourceKey) is Brush themeBrush)
            return Freeze(themeBrush);

        return GetCachedFallback(fallback);
    }

    /// <summary>
    /// Returns a frozen <see cref="SolidColorBrush"/> for the given color, cached by ARGB value.
    /// </summary>
    internal static SolidColorBrush GetCachedFallback(Color color)
    {
        var key = (uint)(color.A << 24 | color.R << 16 | color.G << 8 | color.B);

        lock (s_cache)
        {
            if (s_cache.TryGetValue(key, out var cached))
                return cached;

            var brush = new SolidColorBrush(color);
            brush.Freeze();
            s_cache[key] = brush;
            return brush;
        }
    }

    private static Brush Freeze(Brush brush)
    {
        if (brush.IsFrozen) return brush;
        if (brush.CanFreeze)
        {
            brush.Freeze();
            return brush;
        }
        // DynamicResource brushes are often not freezable — clone first.
        var clone = brush.Clone();
        if (clone.CanFreeze) clone.Freeze();
        return clone;
    }
}
