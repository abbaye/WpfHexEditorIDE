//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6, Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Docking.Core.Commands;

/// <summary>
/// Manages an undo/redo stack of <see cref="IDockCommand"/> operations.
/// </summary>
public class DockCommandStack
{
    private readonly Stack<IDockCommand> _undoStack = new();
    private readonly Stack<IDockCommand> _redoStack = new();
    private readonly int _maxDepth;

    public DockCommandStack(int maxDepth = 50)
    {
        _maxDepth = maxDepth;
    }

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public event Action? StackChanged;

    /// <summary>
    /// Executes a command and pushes it onto the undo stack.
    /// </summary>
    public void Execute(IDockCommand command)
    {
        command.Execute();
        _undoStack.Push(command);
        _redoStack.Clear();
        TrimStack();
        StackChanged?.Invoke();
    }

    /// <summary>
    /// Undoes the most recent command.
    /// </summary>
    public void Undo()
    {
        if (!CanUndo) return;
        var command = _undoStack.Pop();
        command.Undo();
        _redoStack.Push(command);
        StackChanged?.Invoke();
    }

    /// <summary>
    /// Redoes the most recently undone command.
    /// </summary>
    public void Redo()
    {
        if (!CanRedo) return;
        var command = _redoStack.Pop();
        command.Execute();
        _undoStack.Push(command);
        StackChanged?.Invoke();
    }

    /// <summary>
    /// Clears both stacks.
    /// </summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        StackChanged?.Invoke();
    }

    private void TrimStack()
    {
        if (_undoStack.Count <= _maxDepth) return;
        var temp = _undoStack.ToArray();
        _undoStack.Clear();
        for (var i = temp.Length - _maxDepth; i < temp.Length; i++)
            _undoStack.Push(temp[i]);
    }
}
