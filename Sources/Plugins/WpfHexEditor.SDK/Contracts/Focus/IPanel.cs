//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.SDK.Contracts.Focus;

/// <summary>
/// Represents an active dockable panel visible to plugins.
/// </summary>
public interface IPanel
{
    /// <summary>Gets the panel unique content identifier (e.g. "panel-solution-explorer").</summary>
    string ContentId { get; }

    /// <summary>Gets the panel display title.</summary>
    string Title { get; }

    /// <summary>Gets whether the panel is currently visible.</summary>
    bool IsVisible { get; }
}
