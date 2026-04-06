// GNU Affero General Public License v3.0 - 2026
// Contributors: Claude Sonnet 4.6

namespace WpfHexEditor.Docking.Core;

/// <summary>
/// Controls the visual highlight applied to the active panel/document container.
/// Tab header feedback (background + text color on IsSelected) is always preserved.
/// These modes only control the panel container border.
/// </summary>
public enum ActivePanelHighlightMode
{
    /// <summary>No border — only tab header visual change indicates active state.</summary>
    None,

    /// <summary>2px accent bar on the top edge of the active panel container (VS Code style).</summary>
    TopBar,

    /// <summary>1px full rounded-rect outline around the active panel (VS2026 style).</summary>
    FullBorder,

    /// <summary>Full border + DropShadowEffect glow in the accent color.</summary>
    Glow,
}
