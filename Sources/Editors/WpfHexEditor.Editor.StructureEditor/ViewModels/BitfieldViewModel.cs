//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////
// Project: WpfHexEditor.Editor.StructureEditor
// File: ViewModels/BitfieldViewModel.cs
// Description: VM for a single BitfieldDefinition within a field/signature block.
//////////////////////////////////////////////

using System.Collections.ObjectModel;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Editor.StructureEditor.ViewModels;

internal sealed class BitfieldViewModel : ViewModelBase
{
    internal event EventHandler? Changed;
    internal event EventHandler? RemoveRequested;

    private string _name        = "";
    private string _bits        = "";
    private string _storeAs     = "";
    private string _description = "";

    public string Name        { get => _name;        set { if (SetField(ref _name, value))        RaiseChanged(); } }
    public string Bits        { get => _bits;        set { if (SetField(ref _bits, value))        RaiseChanged(); } }
    public string StoreAs     { get => _storeAs;     set { if (SetField(ref _storeAs, value))     RaiseChanged(); } }
    public string Description { get => _description; set { if (SetField(ref _description, value)) RaiseChanged(); } }

    // ValueMap as flat string list of "raw=display" pairs for simple editing
    public ObservableCollection<StringItemViewModel> ValueMapItems { get; } = [];

    public System.Windows.Input.ICommand RemoveCommand =>
        new RelayCommand(() => RemoveRequested?.Invoke(this, EventArgs.Empty));

    internal void LoadFrom(BitfieldDefinition bf)
    {
        Name        = bf.Name        ?? "";
        Bits        = bf.Bits        ?? "";
        StoreAs     = bf.StoreAs     ?? "";
        Description = bf.Description ?? "";

        ValueMapItems.Clear();
        foreach (var kv in bf.ValueMap ?? [])
        {
            var item = new StringItemViewModel($"{kv.Key}={kv.Value}");
            item.RemoveRequested += (s, _) => { ValueMapItems.Remove((StringItemViewModel)s!); RaiseChanged(); };
            item.ValueChanged    += (_, _) => RaiseChanged();
            ValueMapItems.Add(item);
        }
    }

    internal BitfieldDefinition Build()
    {
        var bf = new BitfieldDefinition
        {
            Name        = Name,
            Bits        = Bits,
            StoreAs     = string.IsNullOrEmpty(StoreAs) ? null : StoreAs,
            Description = string.IsNullOrEmpty(Description) ? null : Description,
        };

        if (ValueMapItems.Count > 0)
        {
            bf.ValueMap = [];
            foreach (var item in ValueMapItems)
            {
                var parts = item.Value.Split('=', 2);
                if (parts.Length == 2)
                    bf.ValueMap[parts[0].Trim()] = parts[1].Trim();
            }
        }
        return bf;
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
