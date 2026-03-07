//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.SDK.Descriptors;

/// <summary>
/// Describes a dockable panel contributed by a plugin.
/// </summary>
public sealed class PanelDescriptor
{
    /// <summary>Panel title displayed in the tab header.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Default dock side when first shown.
    /// Valid values: "Left", "Right", "Bottom", "Top", "Center".
    /// </summary>
    public string DefaultDockSide { get; init; } = "Right";

    /// <summary>Whether the user can close this panel (default: true).</summary>
    public bool CanClose { get; init; } = true;

    /// <summary>Whether the panel starts as a floating window (default: false).</summary>
    public bool IsFloating { get; init; }

    /// <summary>Preferred width when docked horizontally (0 = auto).</summary>
    public double PreferredWidth { get; init; }

    /// <summary>Preferred height when docked vertically (0 = auto).</summary>
    public double PreferredHeight { get; init; }

    /// <summary>
    /// When true, the panel starts in auto-hide (pinned to the side bar) instead of docked open.
    /// Use for secondary panels that should not consume screen space by default.
    /// </summary>
    public bool DefaultAutoHide { get; init; }
}
