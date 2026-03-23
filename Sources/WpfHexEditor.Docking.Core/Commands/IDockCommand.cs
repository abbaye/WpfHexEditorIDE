//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6, Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Docking.Core.Commands;

/// <summary>
/// Represents an undoable dock layout operation.
/// </summary>
public interface IDockCommand
{
    string Description { get; }
    void Execute();
    void Undo();

    /// <summary>
    /// Re-applies the command. Default implementations may delegate to <see cref="Execute"/>,
    /// but snapshot-based commands should restore the post-execute snapshot to avoid stale refs.
    /// </summary>
    void Redo() => Execute();
}
