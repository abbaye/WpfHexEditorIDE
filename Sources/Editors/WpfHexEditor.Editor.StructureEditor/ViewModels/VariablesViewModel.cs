//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////
// Project: WpfHexEditor.Editor.StructureEditor
// File: ViewModels/VariablesViewModel.cs
// Description: VM for the Variables tab — editable Key/Value dictionary.
//////////////////////////////////////////////

using System.Collections.ObjectModel;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Editor.StructureEditor.ViewModels;

internal sealed class VariableItemViewModel : ViewModelBase
{
    internal event EventHandler? Changed;
    internal event EventHandler? RemoveRequested;

    private string _key   = "";
    private string _value = "";

    public string Key   { get => _key;   set { if (SetField(ref _key, value))   RaiseChanged(); } }
    public string Value { get => _value; set { if (SetField(ref _value, value)) RaiseChanged(); } }

    public System.Windows.Input.ICommand RemoveCommand =>
        new RelayCommand(() => RemoveRequested?.Invoke(this, EventArgs.Empty));

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}

internal sealed class VariablesViewModel : ViewModelBase
{
    internal event EventHandler? Changed;

    public ObservableCollection<VariableItemViewModel> Items { get; } = [];

    internal void LoadFrom(Dictionary<string, object>? vars)
    {
        Items.Clear();
        foreach (var kv in vars ?? [])
            AddItem(kv.Key, kv.Value?.ToString() ?? "");
    }

    internal void SaveTo(FormatDefinition def)
    {
        def.Variables = [];
        foreach (var item in Items)
        {
            if (string.IsNullOrWhiteSpace(item.Key)) continue;
            if (long.TryParse(item.Value, out var n))
                def.Variables[item.Key] = n;
            else if (double.TryParse(item.Value, out var d))
                def.Variables[item.Key] = d;
            else
                def.Variables[item.Key] = item.Value;
        }
    }

    internal void AddItem(string key = "", string value = "")
    {
        var vm = new VariableItemViewModel { Key = key, Value = value };
        vm.RemoveRequested += (s, _) => { Items.Remove((VariableItemViewModel)s!); RaiseChanged(); };
        vm.Changed         += (_, _) => RaiseChanged();
        Items.Add(vm);
        RaiseChanged();
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
