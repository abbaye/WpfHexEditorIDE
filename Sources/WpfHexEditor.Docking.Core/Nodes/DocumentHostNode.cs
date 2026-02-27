//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

namespace WpfHexEditor.Docking.Core.Nodes;

/// <summary>
/// A specialized group node that hosts documents.
/// The <see cref="IsMain"/> host cannot be removed from the layout.
/// </summary>
public class DocumentHostNode : DockGroupNode
{
    /// <summary>
    /// Whether this is the main (central) document host. A layout must always have exactly one main host.
    /// </summary>
    public bool IsMain { get; init; }
}
