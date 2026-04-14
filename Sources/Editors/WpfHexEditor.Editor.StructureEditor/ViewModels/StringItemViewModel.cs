//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////
// Project: WpfHexEditor.Editor.StructureEditor
// File: ViewModels/StringItemViewModel.cs
// Description: Reusable single-string list item with a remove command.
//////////////////////////////////////////////

using System.Windows.Input;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Editor.StructureEditor.ViewModels;

/// <summary>Wraps a single editable string in a list (extensions, MIME types, etc.).</summary>
internal sealed class StringItemViewModel : ViewModelBase
{
    private string _value = "";

    internal event EventHandler? RemoveRequested;
    internal event EventHandler? ValueChanged;

    public StringItemViewModel(string value) => _value = value;

    public string Value
    {
        get => _value;
        set
        {
            if (SetField(ref _value, value))
                ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public ICommand RemoveCommand => new RelayCommand(() => RemoveRequested?.Invoke(this, EventArgs.Empty));
}
