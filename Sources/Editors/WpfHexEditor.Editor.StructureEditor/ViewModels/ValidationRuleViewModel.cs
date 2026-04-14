//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////
// Project: WpfHexEditor.Editor.StructureEditor
// File: ViewModels/ValidationRuleViewModel.cs
// Description: VM for FieldValidationRules within a block.
//////////////////////////////////////////////

using System.Collections.ObjectModel;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Editor.StructureEditor.ViewModels;

internal sealed class ValidationRuleViewModel : ViewModelBase
{
    internal event EventHandler? Changed;

    private string _minValue       = "";
    private string _maxValue       = "";
    private string _pattern        = "";
    private string _customValidator = "";
    private string _errorMessage   = "";

    public string MinValue        { get => _minValue;        set { if (SetField(ref _minValue, value))        RaiseChanged(); } }
    public string MaxValue        { get => _maxValue;        set { if (SetField(ref _maxValue, value))        RaiseChanged(); } }
    public string Pattern         { get => _pattern;         set { if (SetField(ref _pattern, value))         RaiseChanged(); } }
    public string CustomValidator { get => _customValidator; set { if (SetField(ref _customValidator, value)) RaiseChanged(); } }
    public string ErrorMessage    { get => _errorMessage;    set { if (SetField(ref _errorMessage, value))    RaiseChanged(); } }

    public ObservableCollection<StringItemViewModel> AllowedValues { get; } = [];

    internal void LoadFrom(FieldValidationRules? rules)
    {
        if (rules is null) return;
        MinValue        = rules.MinValue?.ToString()       ?? "";
        MaxValue        = rules.MaxValue?.ToString()       ?? "";
        Pattern         = rules.Pattern                    ?? "";
        CustomValidator = rules.CustomValidator            ?? "";
        ErrorMessage    = rules.ErrorMessage               ?? "";

        AllowedValues.Clear();
        foreach (var v in rules.AllowedValues ?? [])
        {
            var item = new StringItemViewModel(v?.ToString() ?? "");
            item.RemoveRequested += (s, _) => { AllowedValues.Remove((StringItemViewModel)s!); RaiseChanged(); };
            item.ValueChanged    += (_, _) => RaiseChanged();
            AllowedValues.Add(item);
        }
    }

    internal FieldValidationRules? Build()
    {
        if (string.IsNullOrEmpty(MinValue) && string.IsNullOrEmpty(MaxValue) &&
            string.IsNullOrEmpty(Pattern)  && AllowedValues.Count == 0)
            return null;

        return new FieldValidationRules
        {
            MinValue        = ParseValue(MinValue),
            MaxValue        = ParseValue(MaxValue),
            Pattern         = string.IsNullOrEmpty(Pattern)         ? null : Pattern,
            CustomValidator = string.IsNullOrEmpty(CustomValidator) ? null : CustomValidator,
            ErrorMessage    = string.IsNullOrEmpty(ErrorMessage)    ? null : ErrorMessage,
            AllowedValues   = AllowedValues.Count > 0
                ? [..AllowedValues.Select(x => (object?)x.Value)]
                : null,
        };
    }

    private static object? ParseValue(string s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        if (long.TryParse(s, out var n)) return n;
        return s;
    }

    internal void AddAllowedValue()
    {
        var item = new StringItemViewModel("");
        item.RemoveRequested += (s, _) => { AllowedValues.Remove((StringItemViewModel)s!); RaiseChanged(); };
        item.ValueChanged    += (_, _) => RaiseChanged();
        AllowedValues.Add(item);
        RaiseChanged();
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
