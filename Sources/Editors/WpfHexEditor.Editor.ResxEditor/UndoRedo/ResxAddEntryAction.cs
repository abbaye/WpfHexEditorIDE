// ==========================================================
// Project: WpfHexEditor.Editor.ResxEditor
// File: UndoRedo/ResxAddEntryAction.cs
// Description:
//     Reversible action that adds a new entry to the collection.
// ==========================================================

using System.Collections.ObjectModel;
using WpfHexEditor.Editor.ResxEditor.ViewModels;

namespace WpfHexEditor.Editor.ResxEditor.UndoRedo;

/// <summary>Adds an entry; undo removes it.</summary>
public sealed class ResxAddEntryAction(ResxEntryViewModel entry) : IResxUndoAction
{
    public string Description => $"Add entry '{entry.Name}'";

    public void Execute(ObservableCollection<ResxEntryViewModel> entries)
        => entries.Add(entry);

    public void Undo(ObservableCollection<ResxEntryViewModel> entries)
        => entries.Remove(entry);
}
