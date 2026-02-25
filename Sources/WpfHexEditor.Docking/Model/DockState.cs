// Apache 2.0 - 2026
// WpfHexEditor.Docking - VS-Like Docking Framework

namespace WpfHexEditor.Docking.Model;

/// <summary>
/// The visibility state of a dockable element.
/// </summary>
public enum DockState
{
    /// <summary>Visible in the main layout tree.</summary>
    Docked,

    /// <summary>Collapsed to a side tab, slides in on hover.</summary>
    AutoHidden,

    /// <summary>In a floating window.</summary>
    Float,

    /// <summary>Completely hidden (closed but not destroyed).</summary>
    Hidden
}
