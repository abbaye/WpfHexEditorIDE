//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.SDK.Descriptors;

/// <summary>
/// Describes a document tab (central area) contributed by a plugin.
/// </summary>
public sealed class DocumentDescriptor
{
    /// <summary>Title displayed in the document tab header.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Unique content identifier for the docking engine.
    /// Must follow the pattern: <c>doc-plugin-{pluginId}-{name}</c>.
    /// </summary>
    public string ContentId { get; init; } = string.Empty;

    /// <summary>Tooltip shown when hovering over the tab header.</summary>
    public string? ToolTip { get; init; }

    /// <summary>Whether the tab can be closed by the user (default: true).</summary>
    public bool CanClose { get; init; } = true;
}
