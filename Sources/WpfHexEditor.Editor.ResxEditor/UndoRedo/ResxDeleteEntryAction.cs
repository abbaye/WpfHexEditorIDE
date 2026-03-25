// ==========================================================
// Project: WpfHexEditor.Editor.ResxEditor
// File: UndoRedo/ResxDeleteEntryAction.cs
// Description:
//     Reversible action that removes an entry.  Remembers the
//     insertion index so undo restores it to the original position.
// ==========================================================

using System.Collections.ObjectModel;
using WpfHexEditor.Editor.ResxEditor.ViewModels;

namespace WpfHexEditor.Editor.ResxEditor.UndoRedo;

/// <summary>Deletes an entry; undo re-inserts it at the original index.</summary>
public sealed class ResxDeleteEntryAction : IResxUndoAction
{
    private readonly ResxEntryViewModel _entry;
    private readonly int                _index;

    public ResxDeleteEntryAction(ResxEntryViewModel entry, int index)
    {
        _entry = entry;
        _index = index;
    }

    public string Description => $"Delete entry '{_entry.Name}'";

    public void Execute(ObservableCollection<ResxEntryViewModel> entries)
        => entries.Remove(_entry);

    public void Undo(ObservableCollection<ResxEntryViewModel> entries)
    {
        var clampedIndex = Math.Clamp(_index, 0, entries.Count);
        entries.Insert(clampedIndex, _entry);
    }
}
