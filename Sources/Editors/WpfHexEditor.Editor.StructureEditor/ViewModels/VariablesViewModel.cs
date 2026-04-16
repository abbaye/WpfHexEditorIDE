//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////
// Project: WpfHexEditor.Editor.StructureEditor
// File: ViewModels/VariablesViewModel.cs
// Description: VM for the Variables tab — editable Key/Type/Value/Description rows.
//////////////////////////////////////////////

using System.Collections.ObjectModel;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Editor.StructureEditor.ViewModels;

internal sealed class VariableItemViewModel : ViewModelBase
{
    internal event EventHandler? Changed;
    internal event EventHandler? RemoveRequested;

    private string _key         = "";
    private string _type        = "string";
    private string _value       = "";
    private string _description = "";

    public string Key         { get => _key;         set { if (SetField(ref _key, value))         RaiseChanged(); } }
    public string Description { get => _description; set { if (SetField(ref _description, value)) RaiseChanged(); } }

    public string Type
    {
        get => _type;
        set
        {
            if (!SetField(ref _type, value)) return;
            // Coerce value to a valid default for the new type
            if (value == "bool" && !bool.TryParse(_value, out _)) Value = "false";
            OnPropertyChanged(nameof(IsBool));
            OnPropertyChanged(nameof(IsInt));
            OnPropertyChanged(nameof(IsFloat));
            OnPropertyChanged(nameof(IsString));
            OnPropertyChanged(nameof(IsHex));
            RaiseChanged();
        }
    }

    public string Value
    {
        get => _value;
        set
        {
            if (SetField(ref _value, value))
            {
                OnPropertyChanged(nameof(BoolValue));
                RaiseChanged();
            }
        }
    }

    // ── Type helpers for DataTemplate selection ───────────────────────────
    public bool IsBool   => _type == "bool";
    public bool IsInt    => _type == "int";
    public bool IsFloat  => _type == "float";
    public bool IsHex    => _type == "hex";
    public bool IsString => _type == "string";

    /// <summary>Two-way bool binding for the CheckBox cell template.</summary>
    public bool BoolValue
    {
        get => bool.TryParse(_value, out var b) && b;
        set => Value = value ? "true" : "false";
    }

    public static IReadOnlyList<string> TypeOptions { get; } = ["string", "int", "float", "bool", "hex"];

    public System.Windows.Input.ICommand RemoveCommand =>
        new RelayCommand(() => RemoveRequested?.Invoke(this, EventArgs.Empty));

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}

internal sealed class VariablesViewModel : ViewModelBase
{
    internal event EventHandler? Changed;

    private string _filterText = "";

    public ObservableCollection<VariableItemViewModel> Items { get; } = [];

    public string FilterText
    {
        get => _filterText;
        set { if (SetField(ref _filterText, value)) OnPropertyChanged(nameof(FilteredItems)); }
    }

    public IEnumerable<VariableItemViewModel> FilteredItems =>
        string.IsNullOrWhiteSpace(_filterText)
            ? Items
            : Items.Where(i => i.Key.Contains(_filterText, StringComparison.OrdinalIgnoreCase));

    internal void LoadFrom(Dictionary<string, object>? vars)
    {
        Items.Clear();
        foreach (var kv in vars ?? [])
        {
            var valStr = kv.Value?.ToString() ?? "";
            var type = kv.Value switch
            {
                bool   => "bool",
                int    => "int",
                long   => "int",
                float  => "float",
                double => "float",
                _ when bool.TryParse(valStr, out _)   => "bool",
                _ when valStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase) => "hex",
                _ when long.TryParse(valStr, out _)   => "int",
                _ when double.TryParse(valStr, out _)  => "float",
                _ => "string"
            };
            AddItem(kv.Key, type, valStr);
        }
    }

    internal void SaveTo(FormatDefinition def)
    {
        def.Variables = [];
        foreach (var item in Items)
        {
            if (string.IsNullOrWhiteSpace(item.Key)) continue;
            def.Variables[item.Key] = item.Type switch
            {
                "int"   when long.TryParse(item.Value, out var n)                                       => (object)n,
                "float" when double.TryParse(item.Value, out var d)                                     => (object)d,
                "bool"  when bool.TryParse(item.Value, out var b)                                       => (object)b,
                "hex"   when long.TryParse(item.Value.TrimStart('0', 'x'), System.Globalization.NumberStyles.HexNumber, null, out var h) => (object)h,
                _ => item.Value
            };
        }
    }

    internal void AddItem(string key = "", string type = "string", string value = "")
    {
        var vm = new VariableItemViewModel { Key = key, Type = type, Value = value };
        vm.RemoveRequested += (s, _) =>
        {
            Items.Remove((VariableItemViewModel)s!);
            OnPropertyChanged(nameof(FilteredItems));
            RaiseChanged();
        };
        vm.Changed += (_, _) => { OnPropertyChanged(nameof(FilteredItems)); RaiseChanged(); };
        Items.Add(vm);
        OnPropertyChanged(nameof(FilteredItems));
        RaiseChanged();
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
