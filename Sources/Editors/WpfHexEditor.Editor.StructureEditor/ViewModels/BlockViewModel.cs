//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////
// Project: WpfHexEditor.Editor.StructureEditor
// File: ViewModels/BlockViewModel.cs
// Description: VM for a single BlockDefinition. Type-switchable properties +
//              Children collection for nested block types (conditional/loop/repeating).
//////////////////////////////////////////////

using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Editor.StructureEditor.ViewModels;

internal sealed class BlockViewModel : ViewModelBase
{
    internal event EventHandler? Changed;

    // Common
    private string _blockType   = "field";
    private string _name        = "";
    private string _color       = StructureEditorConstants.DefaultBlockColor;
    private double _opacity     = 0.3;
    private string _description = "";
    private bool   _hidden;
    private string _endianness  = "";

    // field/signature
    private string _offsetText    = "";
    private string _offsetFrom    = "";
    private string _offsetAddText = "";
    private string _lengthText    = "";
    private string _valueType     = "";
    private string _storeAs       = "";
    private string _mappedValueStoreAs = "";

    // metadata
    private string _variable = "";

    // conditional
    private string _trueLabel  = "";
    private string _falseLabel = "";

    // loop
    private string _countText     = "";
    private int    _maxIterations = 1000;

    // action
    private string _action         = "increment";
    private string _actionVariable = "";
    private string _actionValue    = "";

    // computeFromVariables
    private string _expression = "";

    // sentinel
    private string _untilText       = "";
    private int    _maxLength       = 0;
    private bool   _untilInclusive  = false;

    // repeating
    private string _entrySizeText = "";
    private string _indexVar      = "";

    // union
    private string _unionCondition = "";

    // nested
    private string _structRef = "";

    // pointer
    private string _targetVar = "";
    private string _label     = "";

    /// <summary>Transient nesting depth, set by QualityMetricsViewModel.Refresh(). Not persisted.</summary>
    internal int Depth { get; set; }

    // ── Collections ──────────────────────────────────────────────────────────

    public ObservableCollection<BitfieldViewModel>    Bitfields      { get; } = [];
    public ObservableCollection<StringItemViewModel>  ColorCycle     { get; } = [];
    public ObservableCollection<BlockViewModel>       Children       { get; } = [];
    public ValidationRuleViewModel                    ValidationRules { get; } = new();
    public ConditionViewModel                         Condition      { get; } = new();

    // ValueMap: flat "raw=display" pair list
    public ObservableCollection<StringItemViewModel>  ValueMapItems  { get; } = [];

    // ── Common properties ─────────────────────────────────────────────────────

    public string BlockType
    {
        get => _blockType;
        set { if (SetField(ref _blockType, value)) { OnPropertyChanged(nameof(DisplayName)); RaiseChanged(); } }
    }

    public string Name
    {
        get => _name;
        set { if (SetField(ref _name, value)) { OnPropertyChanged(nameof(DisplayName)); RaiseChanged(); } }
    }

    public string DisplayName => string.IsNullOrEmpty(Name) ? $"[{BlockType}]" : $"{BlockType}: {Name}";

    public string Color
    {
        get => _color;
        set
        {
            if (SetField(ref _color, value))
            {
                OnPropertyChanged(nameof(ColorBrush));
                RaiseChanged();
            }
        }
    }

    public Brush ColorBrush
    {
        get
        {
            try { return new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString(_color)); }
            catch { return Brushes.Transparent; }
        }
    }

    public double  Opacity     { get => _opacity;     set { if (SetField(ref _opacity, value))     RaiseChanged(); } }
    public string  Description { get => _description; set { if (SetField(ref _description, value)) RaiseChanged(); } }
    public bool    Hidden      { get => _hidden;       set { if (SetField(ref _hidden, value))      RaiseChanged(); } }
    public string  Endianness  { get => _endianness;  set { if (SetField(ref _endianness, value))  RaiseChanged(); } }

    // ── Field/Signature properties ────────────────────────────────────────────

    public string OffsetText       { get => _offsetText;       set { if (SetField(ref _offsetText, value))       RaiseChanged(); } }
    public string OffsetFrom       { get => _offsetFrom;       set { if (SetField(ref _offsetFrom, value))       RaiseChanged(); } }
    public string OffsetAddText    { get => _offsetAddText;    set { if (SetField(ref _offsetAddText, value))    RaiseChanged(); } }
    public string LengthText       { get => _lengthText;       set { if (SetField(ref _lengthText, value))       RaiseChanged(); } }
    public string UntilText        { get => _untilText;        set { if (SetField(ref _untilText, value))        RaiseChanged(); } }
    public int    MaxLength        { get => _maxLength;        set { if (SetField(ref _maxLength, value))        RaiseChanged(); } }
    public bool   UntilInclusive   { get => _untilInclusive;  set { if (SetField(ref _untilInclusive, value))   RaiseChanged(); } }
    public string ValueType        { get => _valueType;        set { if (SetField(ref _valueType, value))        RaiseChanged(); } }
    public string StoreAs          { get => _storeAs;          set { if (SetField(ref _storeAs, value))          RaiseChanged(); } }
    public string MappedValueStoreAs { get => _mappedValueStoreAs; set { if (SetField(ref _mappedValueStoreAs, value)) RaiseChanged(); } }

    // ── Type-specific properties ──────────────────────────────────────────────

    public string Variable       { get => _variable;      set { if (SetField(ref _variable, value))      RaiseChanged(); } }
    public string TrueLabel      { get => _trueLabel;     set { if (SetField(ref _trueLabel, value))     RaiseChanged(); } }
    public string FalseLabel     { get => _falseLabel;    set { if (SetField(ref _falseLabel, value))    RaiseChanged(); } }
    public string CountText      { get => _countText;     set { if (SetField(ref _countText, value))     RaiseChanged(); } }
    public int    MaxIterations  { get => _maxIterations; set { if (SetField(ref _maxIterations, value)) RaiseChanged(); } }
    public string Action         { get => _action;        set { if (SetField(ref _action, value))        RaiseChanged(); } }
    public string ActionVariable { get => _actionVariable; set { if (SetField(ref _actionVariable, value)) RaiseChanged(); } }
    public string ActionValue    { get => _actionValue;   set { if (SetField(ref _actionValue, value))   RaiseChanged(); } }
    public string Expression     { get => _expression;   set { if (SetField(ref _expression, value))    RaiseChanged(); } }
    public string EntrySizeText  { get => _entrySizeText; set { if (SetField(ref _entrySizeText, value)) RaiseChanged(); } }
    public string IndexVar       { get => _indexVar;      set { if (SetField(ref _indexVar, value))      RaiseChanged(); } }
    public string UnionCondition { get => _unionCondition; set { if (SetField(ref _unionCondition, value)) RaiseChanged(); } }
    public string StructRef      { get => _structRef;     set { if (SetField(ref _structRef, value))     RaiseChanged(); } }
    public string TargetVar      { get => _targetVar;     set { if (SetField(ref _targetVar, value))     RaiseChanged(); } }
    public string Label          { get => _label;         set { if (SetField(ref _label, value))         RaiseChanged(); } }

    // ── Static option lists ───────────────────────────────────────────────────

    public static IReadOnlyList<string> BlockTypes { get; } =
    [
        "field", "signature", "metadata", "conditional", "loop",
        "action", "computeFromVariables", "repeating", "union", "nested", "pointer",
        "group", "header", "data",
    ];

    public static IReadOnlyList<string> ValueTypes { get; } =
    [
        "uint8", "uint16", "uint32", "uint64",
        "int8",  "int16",  "int32",  "int64",
        "float", "double", "ascii",  "utf8", "utf16", "hex", "bytes",
    ];

    public static IReadOnlyList<string> EndiannessOptions { get; } = ["", "little", "big"];
    public static IReadOnlyList<string> ActionOptions     { get; } = ["increment", "decrement", "setVariable"];

    // ── Union variants ────────────────────────────────────────────────────────

    public ObservableCollection<UnionVariantViewModel> UnionVariants { get; } = [];

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand AddBitfieldCommand    => new RelayCommand(AddBitfield);
    public ICommand AddColorCycleCommand  => new RelayCommand(AddColorCycle);
    public ICommand AddValueMapCommand    => new RelayCommand(AddValueMapItem);
    public ICommand AddChildCommand       => new RelayCommand(AddChild);

    /// <summary>Raised when the host should open a raw JSON popup for this block.</summary>
    public event EventHandler? OpenRawRequested;
    public ICommand OpenRawCommand         => new RelayCommand(() => OpenRawRequested?.Invoke(this, EventArgs.Empty));

    /// <summary>Raised when the host should open a file-browse dialog for StructRef.</summary>
    public event EventHandler? BrowseStructRefRequested;
    public ICommand BrowseStructRefCommand => new RelayCommand(() => BrowseStructRefRequested?.Invoke(this, EventArgs.Empty));

    // ── Load / Build ──────────────────────────────────────────────────────────

    internal void LoadFrom(BlockDefinition b)
    {
        BlockType   = b.Type        ?? "field";
        Name        = b.Name        ?? "";
        Color       = b.Color       ?? StructureEditorConstants.DefaultBlockColor;
        Opacity     = b.Opacity;
        Description = b.Description ?? "";
        Hidden      = b.Hidden ?? false;
        Endianness  = b.Endianness  ?? "";

        OffsetText        = DisplayObj(b.Offset);
        OffsetFrom        = b.OffsetFrom       ?? "";
        OffsetAddText     = DisplayObj(b.OffsetAdd);
        LengthText        = DisplayObj(b.Length);
        UntilText         = b.Until            ?? "";
        MaxLength         = b.MaxLength;
        UntilInclusive    = b.UntilInclusive;
        ValueType         = b.ValueType        ?? "";
        StoreAs           = b.StoreAs          ?? "";
        MappedValueStoreAs = b.MappedValueStoreAs ?? "";

        Variable      = b.Variable     ?? "";
        TrueLabel     = b.TrueLabel    ?? "";
        FalseLabel    = b.FalseLabel   ?? "";
        CountText     = DisplayObj(b.Count);
        MaxIterations = b.MaxIterations > 0 ? b.MaxIterations : 1000;
        Action        = b.Action       ?? "increment";
        ActionVariable = b.Variable    ?? "";
        ActionValue   = DisplayObj(b.Value);
        Expression    = b.Expression   ?? "";
        EntrySizeText = DisplayObj(b.EntrySize);
        IndexVar      = b.IndexVar     ?? "";
        UnionCondition = b.UnionCondition ?? "";
        StructRef     = b.StructRef    ?? "";
        TargetVar     = b.TargetVar    ?? "";
        Label         = b.Label        ?? "";

        Condition.LoadFrom(b.Condition);
        ValidationRules.LoadFrom(b.ValidationRules);

        Bitfields.Clear();
        foreach (var bf in b.Bitfields ?? []) AddBitfieldFrom(bf);

        ValueMapItems.Clear();
        foreach (var kv in b.ValueMap ?? [])
        {
            var item = new StringItemViewModel($"{kv.Key}={kv.Value}");
            WireListItem(item, ValueMapItems);
            ValueMapItems.Add(item);
        }

        ColorCycle.Clear();
        foreach (var c in b.ColorCycle ?? [])
        {
            var item = new StringItemViewModel(c);
            WireListItem(item, ColorCycle);
            ColorCycle.Add(item);
        }

        Children.Clear();
        LoadChildBlocks(b.Then   ?? []);
        LoadChildBlocks(b.Else   ?? []);
        LoadChildBlocks(b.Body   ?? []);
        LoadChildBlocks(b.Fields ?? []);
    }

    internal BlockDefinition Build()
    {
        var b = new BlockDefinition
        {
            Type        = BlockType,
            Name        = Name,
            Color       = Color,
            Opacity     = Opacity,
            Description = string.IsNullOrEmpty(Description) ? null : Description,
            Hidden      = Hidden ? true : null,
            Endianness  = string.IsNullOrEmpty(Endianness) ? null : Endianness,
        };

        b.Offset    = ParseObj(OffsetText);
        b.OffsetFrom = string.IsNullOrEmpty(OffsetFrom) ? null : OffsetFrom;
        b.OffsetAdd  = ParseObj(OffsetAddText);
        b.Length     = ParseObj(LengthText);
        if (!string.IsNullOrEmpty(UntilText)) b.Until = UntilText;
        if (MaxLength > 0)                    b.MaxLength = MaxLength;
        if (UntilInclusive)                   b.UntilInclusive = true;
        b.ValueType  = string.IsNullOrEmpty(ValueType) ? null : ValueType;
        b.StoreAs    = string.IsNullOrEmpty(StoreAs)   ? null : StoreAs;
        b.MappedValueStoreAs = string.IsNullOrEmpty(MappedValueStoreAs) ? null : MappedValueStoreAs;
        b.Variable   = string.IsNullOrEmpty(Variable)  ? null : Variable;
        b.TrueLabel  = string.IsNullOrEmpty(TrueLabel) ? null : TrueLabel;
        b.FalseLabel = string.IsNullOrEmpty(FalseLabel)? null : FalseLabel;
        b.Count      = ParseObj(CountText);
        b.MaxIterations = MaxIterations;
        b.Action     = string.IsNullOrEmpty(Action)    ? null : Action;
        b.Variable   = string.IsNullOrEmpty(ActionVariable) ? null : ActionVariable;
        b.Value      = ParseObj(ActionValue);
        b.Expression = string.IsNullOrEmpty(Expression)? null : Expression;
        b.EntrySize  = ParseObj(EntrySizeText);
        b.IndexVar   = string.IsNullOrEmpty(IndexVar)  ? null : IndexVar;
        b.UnionCondition = string.IsNullOrEmpty(UnionCondition) ? null : UnionCondition;
        b.StructRef  = string.IsNullOrEmpty(StructRef) ? null : StructRef;
        b.TargetVar  = string.IsNullOrEmpty(TargetVar) ? null : TargetVar;
        b.Label      = string.IsNullOrEmpty(Label)     ? null : Label;

        b.Condition       = Condition.Build();
        b.ValidationRules = ValidationRules.Build();

        b.Bitfields = Bitfields.Count > 0 ? [..Bitfields.Select(vm => vm.Build())] : null;

        b.ValueMap = ValueMapItems.Count > 0 ? BuildValueMap() : null;
        b.ColorCycle = ColorCycle.Count > 0 ? [..ColorCycle.Select(x => x.Value)] : null;

        // Children: currently flat — proper then/else/body split handled by parent
        if (Children.Count > 0)
        {
            var childBlocks = Children.Select(c => c.Build()).ToList();
            switch (BlockType)
            {
                case "conditional": b.Then   = childBlocks; break;
                case "loop":        b.Body   = childBlocks; break;
                case "repeating":   b.Fields = childBlocks; break;
                case "group":       b.Fields = childBlocks; break;
                case "header":      b.Fields = childBlocks; break;
            }
        }

        return b;
    }

    // ── Filter ────────────────────────────────────────────────────────────────

    /// <summary>True if this block or any descendant matches <paramref name="text"/>.</summary>
    internal bool MatchesFilter(string text) =>
        DisplayName.Contains(text, StringComparison.OrdinalIgnoreCase) ||
        Description.Contains(text, StringComparison.OrdinalIgnoreCase) ||
        Children.Any(c => c.MatchesFilter(text));

    // ── Variable cross-reference ──────────────────────────────────────────────

    /// <summary>Yields every variable name this block references.</summary>
    internal IEnumerable<string> GetReferencedVariables()
    {
        if (!string.IsNullOrEmpty(StoreAs))         yield return StoreAs;
        if (!string.IsNullOrEmpty(MappedValueStoreAs)) yield return MappedValueStoreAs;
        if (!string.IsNullOrEmpty(OffsetFrom))      yield return OffsetFrom;
        if (!string.IsNullOrEmpty(TargetVar))       yield return TargetVar;
        if (!string.IsNullOrEmpty(ActionVariable))  yield return ActionVariable;
        if (!string.IsNullOrEmpty(IndexVar))        yield return IndexVar;
    }

    // ── Raw JSON for "{ } Raw" popup ─────────────────────────────────────────

    internal string ToRawJson()
    {
        var opts = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };
        return JsonSerializer.Serialize(Build(), opts);
    }

    internal void LoadFromRawJson(string json)
    {
        try
        {
            var opts = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
            };
            var b = JsonSerializer.Deserialize<BlockDefinition>(json, opts);
            if (b is not null) LoadFrom(b);
        }
        catch { /* malformed JSON — ignore */ }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void AddBitfield()
    {
        var vm = new BitfieldViewModel();
        vm.RemoveRequested += (s, _) => { Bitfields.Remove((BitfieldViewModel)s!); RaiseChanged(); };
        vm.Changed         += (_, _) => RaiseChanged();
        Bitfields.Add(vm);
        RaiseChanged();
    }

    private void AddBitfieldFrom(BitfieldDefinition bf)
    {
        var vm = new BitfieldViewModel();
        vm.LoadFrom(bf);
        vm.RemoveRequested += (s, _) => { Bitfields.Remove((BitfieldViewModel)s!); RaiseChanged(); };
        vm.Changed         += (_, _) => RaiseChanged();
        Bitfields.Add(vm);
    }

    private void AddColorCycle()
    {
        var item = new StringItemViewModel(StructureEditorConstants.DefaultBlockColor);
        WireListItem(item, ColorCycle);
        ColorCycle.Add(item);
        RaiseChanged();
    }

    private void AddValueMapItem()
    {
        var item = new StringItemViewModel("0=Label");
        WireListItem(item, ValueMapItems);
        ValueMapItems.Add(item);
        RaiseChanged();
    }

    private void AddChild()
    {
        var child = new BlockViewModel();
        child.Changed += (_, _) => RaiseChanged();
        Children.Add(child);
        RaiseChanged();
    }

    private void LoadChildBlocks(IEnumerable<BlockDefinition> blocks)
    {
        foreach (var b in blocks)
        {
            var child = new BlockViewModel();
            child.LoadFrom(b);
            child.Changed += (_, _) => RaiseChanged();
            Children.Add(child);
        }
    }

    private void WireListItem(StringItemViewModel item, ObservableCollection<StringItemViewModel> col)
    {
        item.RemoveRequested += (s, _) => { col.Remove((StringItemViewModel)s!); RaiseChanged(); };
        item.ValueChanged    += (_, _) => RaiseChanged();
    }

    private Dictionary<string, string> BuildValueMap()
    {
        var map = new Dictionary<string, string>();
        foreach (var item in ValueMapItems)
        {
            var parts = item.Value.Split('=', 2);
            if (parts.Length == 2) map[parts[0].Trim()] = parts[1].Trim();
        }
        return map;
    }

    private static string DisplayObj(object? val) => val switch
    {
        null        => "",
        JsonElement je => je.ValueKind == JsonValueKind.Number ? je.GetRawText() : je.GetString() ?? "",
        _           => val.ToString() ?? "",
    };

    private static object? ParseObj(string? s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        if (long.TryParse(s, out var n)) return n;
        return s;
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
