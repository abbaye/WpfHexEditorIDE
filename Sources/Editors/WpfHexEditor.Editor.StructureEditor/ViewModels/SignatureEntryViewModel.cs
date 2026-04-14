//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////
// Project: WpfHexEditor.Editor.StructureEditor
// File: ViewModels/SignatureEntryViewModel.cs
// Description: VM for a single v2.0 SignatureEntry in the Detection tab.
//////////////////////////////////////////////

using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Editor.StructureEditor.ViewModels;

internal sealed class SignatureEntryViewModel : ViewModelBase
{
    internal event EventHandler? Changed;
    internal event EventHandler? RemoveRequested;

    private string _value  = "";
    private long   _offset;
    private string _label  = "";
    private double _weight = 0.9;

    public string Value  { get => _value;  set { if (SetField(ref _value, value))  RaiseChanged(); } }
    public long   Offset { get => _offset; set { if (SetField(ref _offset, value)) RaiseChanged(); } }
    public string Label  { get => _label;  set { if (SetField(ref _label, value))  RaiseChanged(); } }
    public double Weight { get => _weight; set { if (SetField(ref _weight, value)) RaiseChanged(); } }

    public System.Windows.Input.ICommand RemoveCommand =>
        new RelayCommand(() => RemoveRequested?.Invoke(this, EventArgs.Empty));

    internal void LoadFrom(SignatureEntry entry)
    {
        Value  = entry.Value  ?? "";
        Offset = entry.Offset;
        Label  = entry.Label  ?? "";
        Weight = entry.Weight;
    }

    internal SignatureEntry Build() => new()
    {
        Value  = Value,
        Offset = Offset,
        Label  = string.IsNullOrEmpty(Label) ? null : Label,
        Weight = Weight,
    };

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
