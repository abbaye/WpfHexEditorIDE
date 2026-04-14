//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////
// Project: WpfHexEditor.Editor.StructureEditor
// File: ViewModels/ConditionViewModel.cs
// Description: VM for ConditionDefinition (conditional/loop blocks).
//////////////////////////////////////////////

using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Editor.StructureEditor.ViewModels;

internal sealed class ConditionViewModel : ViewModelBase
{
    internal event EventHandler? Changed;

    private string _field    = "";
    private string _operator = "equals";
    private string _value    = "";
    private int    _length   = 1;

    public string Field    { get => _field;    set { if (SetField(ref _field, value))    RaiseChanged(); } }
    public string Operator { get => _operator; set { if (SetField(ref _operator, value)) RaiseChanged(); } }
    public string Value    { get => _value;    set { if (SetField(ref _value, value))    RaiseChanged(); } }
    public int    Length   { get => _length;   set { if (SetField(ref _length, value))   RaiseChanged(); } }

    public static IReadOnlyList<string> OperatorOptions { get; } =
        ["equals", "notEquals", "greaterThan", "lessThan", "expression"];

    internal void LoadFrom(ConditionDefinition? cond)
    {
        if (cond is null) return;
        Field    = cond.Field    ?? "";
        Operator = cond.Operator ?? "equals";
        Value    = cond.Value    ?? "";
        Length   = cond.Length;
    }

    internal ConditionDefinition? Build()
    {
        if (string.IsNullOrWhiteSpace(Field)) return null;
        return new ConditionDefinition
        {
            Field    = Field,
            Operator = Operator,
            Value    = Value,
            Length   = Length,
        };
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
