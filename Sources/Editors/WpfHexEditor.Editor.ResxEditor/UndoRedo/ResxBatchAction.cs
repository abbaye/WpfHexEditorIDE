// ==========================================================
// Project: WpfHexEditor.Editor.ResxEditor
// File: UndoRedo/ResxBatchAction.cs
// Description:
//     Composite action that groups multiple IResxUndoActions
//     into a single undo/redo step.  Used for paste, bulk-delete,
//     and replace-all operations.
// ==========================================================

using System.Collections.ObjectModel;
using WpfHexEditor.Editor.ResxEditor.ViewModels;

namespace WpfHexEditor.Editor.ResxEditor.UndoRedo;

/// <summary>Executes multiple actions as a single undo/redo step.</summary>
public sealed class ResxBatchAction : IResxUndoAction
{
    private readonly IReadOnlyList<IResxUndoAction> _actions;

    public ResxBatchAction(string description, IReadOnlyList<IResxUndoAction> actions)
    {
        Description = description;
        _actions    = actions;
    }

    public string Description { get; }

    public void Execute(ObservableCollection<ResxEntryViewModel> entries)
    {
        foreach (var a in _actions)
            a.Execute(entries);
    }

    public void Undo(ObservableCollection<ResxEntryViewModel> entries)
    {
        // Undo in reverse order
        for (int i = _actions.Count - 1; i >= 0; i--)
            _actions[i].Undo(entries);
    }
}
