// ==========================================================
// Project: WpfHexEditor.Editor.ResxEditor
// File: UndoRedo/ResxUndoStack.cs
// Description:
//     Fixed-depth (200) undo/redo stack for RESX operations.
//     Fires CanUndoChanged / CanRedoChanged so the ViewModel
//     can update bound commands without extra polling.
// ==========================================================

using System.Collections.ObjectModel;
using WpfHexEditor.Editor.ResxEditor.ViewModels;

namespace WpfHexEditor.Editor.ResxEditor.UndoRedo;

/// <summary>Fixed-depth undo/redo stack for RESX entry operations.</summary>
public sealed class ResxUndoStack
{
    private const int MaxDepth = 200;

    private readonly LinkedList<IResxUndoAction> _undoStack = new();
    private readonly LinkedList<IResxUndoAction> _redoStack = new();

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public event EventHandler? CanUndoChanged;
    public event EventHandler? CanRedoChanged;

    // ------------------------------------------------------------------

    /// <summary>Executes the action and pushes it onto the undo stack.</summary>
    public void Push(IResxUndoAction action, ObservableCollection<ResxEntryViewModel> entries)
    {
        action.Execute(entries);

        _undoStack.AddFirst(action);
        if (_undoStack.Count > MaxDepth)
            _undoStack.RemoveLast();

        var hadRedo = CanRedo;
        _redoStack.Clear();

        CanUndoChanged?.Invoke(this, EventArgs.Empty);
        if (hadRedo) CanRedoChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Undoes the most recent action.</summary>
    public void Undo(ObservableCollection<ResxEntryViewModel> entries)
    {
        if (!CanUndo) return;
        var action = _undoStack.First!.Value;
        _undoStack.RemoveFirst();
        action.Undo(entries);
        _redoStack.AddFirst(action);
        CanUndoChanged?.Invoke(this, EventArgs.Empty);
        CanRedoChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Re-executes the most recently undone action.</summary>
    public void Redo(ObservableCollection<ResxEntryViewModel> entries)
    {
        if (!CanRedo) return;
        var action = _redoStack.First!.Value;
        _redoStack.RemoveFirst();
        action.Execute(entries);
        _undoStack.AddFirst(action);
        CanUndoChanged?.Invoke(this, EventArgs.Empty);
        CanRedoChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Clears both stacks (called after Save As or file reload).</summary>
    public void Clear()
    {
        var hadUndo = CanUndo;
        var hadRedo = CanRedo;
        _undoStack.Clear();
        _redoStack.Clear();
        if (hadUndo) CanUndoChanged?.Invoke(this, EventArgs.Empty);
        if (hadRedo) CanRedoChanged?.Invoke(this, EventArgs.Empty);
    }
}
