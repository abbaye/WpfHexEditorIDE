// ==========================================================
// Project: WpfHexEditor.Docking.Core
// File: AutoHideSettings.cs
// Description:
//     Configurable timing settings for the auto-hide flyout panel.
// ==========================================================

namespace WpfHexEditor.Docking.Core;

/// <summary>
/// Timing and behavior settings for the auto-hide flyout.
/// </summary>
public sealed class AutoHideSettings
{
    /// <summary>Delay in ms before the flyout opens on hover (default 400).</summary>
    public int HoverOpenDelayMs { get; set; } = 400;

    /// <summary>Delay in ms before the flyout closes after mouse leaves (default 300).</summary>
    public int HoverCloseDelayMs { get; set; } = 300;

    /// <summary>Duration in ms for the slide-in/slide-out animation (default 120).</summary>
    public int SlideAnimationMs { get; set; } = 120;
}
