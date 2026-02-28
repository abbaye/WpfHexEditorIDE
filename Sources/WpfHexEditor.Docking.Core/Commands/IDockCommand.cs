//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
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
}
