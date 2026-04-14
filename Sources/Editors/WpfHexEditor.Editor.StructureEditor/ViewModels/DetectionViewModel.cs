//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////
// Project: WpfHexEditor.Editor.StructureEditor
// File: ViewModels/DetectionViewModel.cs
// Description: VM for the Detection tab — DetectionRule + FormatVersionDetection.
//////////////////////////////////////////////

using System.Collections.ObjectModel;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Editor.StructureEditor.ViewModels;

internal sealed class DetectionViewModel : ViewModelBase
{
    internal event EventHandler? Changed;

    // Legacy
    private string _signature    = "";
    private long   _offset;
    private bool   _required;
    private string _strength     = "Strong";
    private string _matchMode    = "any";
    private double _minimumScore = 0.70;
    private double _entropyMin;
    private double _entropyMax   = 8.0;

    public string Signature    { get => _signature;    set { if (SetField(ref _signature, value))    RaiseChanged(); } }
    public long   Offset       { get => _offset;       set { if (SetField(ref _offset, value))       RaiseChanged(); } }
    public bool   Required     { get => _required;     set { if (SetField(ref _required, value))     RaiseChanged(); } }
    public string Strength     { get => _strength;     set { if (SetField(ref _strength, value))     RaiseChanged(); } }
    public string MatchMode    { get => _matchMode;    set { if (SetField(ref _matchMode, value))    RaiseChanged(); } }
    public double MinimumScore { get => _minimumScore; set { if (SetField(ref _minimumScore, value)) RaiseChanged(); } }
    public double EntropyMin   { get => _entropyMin;   set { if (SetField(ref _entropyMin, value))   RaiseChanged(); } }
    public double EntropyMax   { get => _entropyMax;   set { if (SetField(ref _entropyMax, value))   RaiseChanged(); } }

    public ObservableCollection<SignatureEntryViewModel> Signatures      { get; } = [];
    public ObservableCollection<StringItemViewModel>     ContentPatterns { get; } = [];

    public static IReadOnlyList<string> StrengthOptions { get; } =
        ["None", "Weak", "Medium", "Strong", "Unique"];

    public static IReadOnlyList<string> MatchModeOptions { get; } = ["any", "all"];

    internal void LoadFrom(DetectionRule? rule)
    {
        if (rule is null) return;

        Signature    = rule.Signature    ?? "";
        Offset       = rule.Offset;
        Required     = rule.Required;
        Strength     = rule.Strength.ToString();
        MatchMode    = rule.MatchMode    ?? "any";
        MinimumScore = rule.MinimumScore;
        EntropyMin   = rule.EntropyHint?.Min ?? 0;
        EntropyMax   = rule.EntropyHint?.Max ?? 8.0;

        Signatures.Clear();
        foreach (var sig in rule.Signatures ?? [])
            AddSignature(sig);

        ContentPatterns.Clear();
        foreach (var cp in rule.ContentPatterns ?? [])
            AddContentPattern(cp);
    }

    internal void SaveTo(FormatDefinition def)
    {
        def.Detection ??= new DetectionRule();
        var d = def.Detection;
        d.Signature    = string.IsNullOrEmpty(Signature) ? null : Signature;
        d.Offset       = Offset;
        d.Required     = Required;
        d.MatchMode    = MatchMode;
        d.MinimumScore = MinimumScore;

        if (Enum.TryParse<SignatureStrength>(Strength, true, out var s))
            d.Strength = s;

        d.EntropyHint = EntropyMin > 0 || EntropyMax < 8.0
            ? new EntropyHint { Min = EntropyMin, Max = EntropyMax }
            : null;

        d.Signatures = Signatures.Count > 0
            ? [..Signatures.Select(vm => vm.Build())]
            : null;

        d.ContentPatterns = ContentPatterns.Count > 0
            ? [..ContentPatterns.Select(x => x.Value)]
            : null;
    }

    internal void AddSignature(SignatureEntry? entry = null)
    {
        var vm = new SignatureEntryViewModel();
        if (entry is not null) vm.LoadFrom(entry);
        vm.RemoveRequested += (s, _) => { Signatures.Remove((SignatureEntryViewModel)s!); RaiseChanged(); };
        vm.Changed         += (_, _) => RaiseChanged();
        Signatures.Add(vm);
        RaiseChanged();
    }

    internal void AddContentPattern(string pattern = "")
    {
        var item = new StringItemViewModel(pattern);
        item.RemoveRequested += (s, _) => { ContentPatterns.Remove((StringItemViewModel)s!); RaiseChanged(); };
        item.ValueChanged    += (_, _) => RaiseChanged();
        ContentPatterns.Add(item);
        RaiseChanged();
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
