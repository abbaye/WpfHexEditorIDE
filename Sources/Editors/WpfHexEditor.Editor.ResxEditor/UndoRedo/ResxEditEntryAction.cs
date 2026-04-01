// ==========================================================
// Project: WpfHexEditor.Editor.ResxEditor
// File: UndoRedo/ResxEditEntryAction.cs
// Description:
//     Reversible action that edits a single field on an entry.
//     Stores the previous state so undo can restore it precisely.
// ==========================================================

using System.Collections.ObjectModel;
using WpfHexEditor.Editor.ResxEditor.ViewModels;

namespace WpfHexEditor.Editor.ResxEditor.UndoRedo;

/// <summary>Captures a before/after snapshot for an entry field change.</summary>
public sealed class ResxEditEntryAction : IResxUndoAction
{
    private readonly ResxEntryViewModel _target;
    private readonly string             _field;
    private readonly string             _before;
    private readonly string             _after;

    public ResxEditEntryAction(ResxEntryViewModel target, string field, string before, string after)
    {
        _target = target;
        _field  = field;
        _before = before;
        _after  = after;
    }

    public string Description => $"Edit '{_field}' on '{_target.Name}'";

    public void Execute(ObservableCollection<ResxEntryViewModel> _)
        => ApplyField(_target, _field, _after);

    public void Undo(ObservableCollection<ResxEntryViewModel> _)
        => ApplyField(_target, _field, _before);

    private static void ApplyField(ResxEntryViewModel vm, string field, string value)
    {
        switch (field)
        {
            case nameof(ResxEntryViewModel.Name):    vm.SetNameSilent(value);    break;
            case nameof(ResxEntryViewModel.Value):   vm.SetValueSilent(value);   break;
            case nameof(ResxEntryViewModel.Comment): vm.SetCommentSilent(value); break;
        }
    }
}
