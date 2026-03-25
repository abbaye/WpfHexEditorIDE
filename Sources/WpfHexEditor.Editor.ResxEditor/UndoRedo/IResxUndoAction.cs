// ==========================================================
// Project: WpfHexEditor.Editor.ResxEditor
// File: UndoRedo/IResxUndoAction.cs
// Description:
//     Contract for all reversible operations on a RESX document.
//     Each action receives the live ObservableCollection so it
//     can modify it directly without going through the ViewModel.
// ==========================================================

using System.Collections.ObjectModel;
using WpfHexEditor.Editor.ResxEditor.ViewModels;

namespace WpfHexEditor.Editor.ResxEditor.UndoRedo;

/// <summary>A single reversible operation on the RESX entry collection.</summary>
public interface IResxUndoAction
{
    /// <summary>Human-readable label shown in the undo tooltip.</summary>
    string Description { get; }

    /// <summary>Applies (or re-applies) the operation to <paramref name="entries"/>.</summary>
    void Execute(ObservableCollection<ResxEntryViewModel> entries);

    /// <summary>Reverses the operation on <paramref name="entries"/>.</summary>
    void Undo(ObservableCollection<ResxEntryViewModel> entries);
}
