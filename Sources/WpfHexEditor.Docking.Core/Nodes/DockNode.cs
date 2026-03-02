//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Docking.Core.Nodes;

/// <summary>
/// Abstract base class for all dock tree nodes.
/// </summary>
public abstract class DockNode
{
    public Guid Id { get; } = Guid.NewGuid();

    public DockNode? Parent { get; internal set; }

    public DockLockMode LockMode { get; set; } = DockLockMode.None;

    /// <summary>
    /// Minimum width constraint (in DIPs). NaN = no constraint.
    /// Applied by <c>DockSplitPanel</c> when building grid column/row definitions.
    /// </summary>
    public double DockMinWidth { get; set; } = double.NaN;

    /// <summary>
    /// Minimum height constraint (in DIPs). NaN = no constraint.
    /// </summary>
    public double DockMinHeight { get; set; } = double.NaN;

    /// <summary>
    /// Maximum width constraint (in DIPs). NaN = no constraint.
    /// </summary>
    public double DockMaxWidth { get; set; } = double.NaN;

    /// <summary>
    /// Maximum height constraint (in DIPs). NaN = no constraint.
    /// </summary>
    public double DockMaxHeight { get; set; } = double.NaN;

    /// <summary>
    /// Detaches this node from its parent.
    /// </summary>
    internal void DetachFromParent()
    {
        if (Parent is DockSplitNode split)
        {
            split.RemoveChild(this);
        }

        Parent = null;
    }
}
