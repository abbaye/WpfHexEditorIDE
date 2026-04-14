//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////
// Project: WpfHexEditor.Editor.StructureEditor
// File: ViewModels/V2ViewModels.cs
// Description: All v2.0 feature ViewModels — Assertions, Checksums, Forensic,
//              Navigation, Inspector, ExportTemplates, AiHints, QualityMetrics.
//              Grouped in one file to avoid proliferation of tiny files.
//////////////////////////////////////////////

using System.Collections.ObjectModel;
using System.Windows.Input;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Editor.StructureEditor.ViewModels;

// ── AssertionViewModel ─────────────────────────────────────────────────────────

internal sealed class AssertionViewModel : ViewModelBase
{
    internal event EventHandler? Changed;
    internal event EventHandler? RemoveRequested;

    private string _name       = "";
    private string _expression = "";
    private string _severity   = "Error";
    private string _message    = "";

    public string AssertionName { get => _name;       set { if (SetField(ref _name, value))       RaiseChanged(); } }
    public string Expression    { get => _expression; set { if (SetField(ref _expression, value)) RaiseChanged(); } }
    public string Severity      { get => _severity;   set { if (SetField(ref _severity, value))   RaiseChanged(); } }
    public string Message       { get => _message;    set { if (SetField(ref _message, value))    RaiseChanged(); } }

    public static IReadOnlyList<string> SeverityOptions { get; } = ["Error", "Warning", "Info"];
    public ICommand RemoveCommand => new RelayCommand(() => RemoveRequested?.Invoke(this, EventArgs.Empty));

    internal void LoadFrom(AssertionDefinition a)
    {
        AssertionName = a.Name       ?? "";
        Expression    = a.Expression ?? "";
        Severity      = a.Severity   ?? "Error";
        Message       = a.Message    ?? "";
    }

    internal AssertionDefinition Build() => new()
    {
        Name       = AssertionName,
        Expression = Expression,
        Severity   = Severity,
        Message    = string.IsNullOrEmpty(Message) ? null : Message,
    };

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}

// ── ChecksumViewModel ──────────────────────────────────────────────────────────

internal sealed class ChecksumViewModel : ViewModelBase
{
    internal event EventHandler? Changed;
    internal event EventHandler? RemoveRequested;

    private string _name        = "";
    private string _algorithm   = "crc32";
    private string _severity    = "warning";

    public string ChecksumName { get => _name;      set { if (SetField(ref _name, value))      RaiseChanged(); } }
    public string Algorithm    { get => _algorithm; set { if (SetField(ref _algorithm, value)) RaiseChanged(); } }
    public string Severity     { get => _severity;  set { if (SetField(ref _severity, value))  RaiseChanged(); } }

    public static IReadOnlyList<string> AlgorithmOptions { get; } = ["crc32", "crc16", "md5", "sha1", "sha256", "adler32"];
    public static IReadOnlyList<string> SeverityOptions  { get; } = ["error", "warning", "info"];

    public ICommand RemoveCommand => new RelayCommand(() => RemoveRequested?.Invoke(this, EventArgs.Empty));

    internal void LoadFrom(ChecksumDefinition c)
    {
        ChecksumName = c.Name      ?? "";
        Algorithm    = c.Algorithm ?? "crc32";
        Severity     = c.Severity  ?? "warning";
    }

    internal ChecksumDefinition Build() => new()
    {
        Name      = ChecksumName,
        Algorithm = Algorithm,
        Severity  = Severity,
    };

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}

// ── ForensicViewModel ─────────────────────────────────────────────────────────

internal sealed class ForensicPatternViewModel : ViewModelBase
{
    internal event EventHandler? Changed;
    internal event EventHandler? RemoveRequested;

    private string _name        = "";
    private string _condition   = "";
    private string _severity    = "Warning";
    private string _description = "";

    public string PatternName  { get => _name;        set { if (SetField(ref _name, value))        RaiseChanged(); } }
    public string Condition    { get => _condition;   set { if (SetField(ref _condition, value))   RaiseChanged(); } }
    public string Severity     { get => _severity;    set { if (SetField(ref _severity, value))    RaiseChanged(); } }
    public string Description  { get => _description; set { if (SetField(ref _description, value)) RaiseChanged(); } }

    public static IReadOnlyList<string> SeverityOptions { get; } = ["Critical", "High", "Medium", "Low", "Info", "Warning"];
    public ICommand RemoveCommand => new RelayCommand(() => RemoveRequested?.Invoke(this, EventArgs.Empty));

    internal void LoadFrom(ForensicPattern p)
    {
        PatternName = p.Name        ?? "";
        Condition   = p.Condition   ?? "";
        Severity    = p.Severity    ?? "Warning";
        Description = p.Description ?? "";
    }

    internal ForensicPattern Build() => new()
    {
        Name        = PatternName,
        Condition   = Condition,
        Severity    = Severity,
        Description = string.IsNullOrEmpty(Description) ? null : Description,
    };

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}

internal sealed class ForensicViewModel : ViewModelBase
{
    internal event EventHandler? Changed;

    private string _category  = "";
    private string _riskLevel = "Low";

    public string ForensicCategory { get => _category;  set { if (SetField(ref _category, value))  RaiseChanged(); } }
    public string RiskLevel        { get => _riskLevel; set { if (SetField(ref _riskLevel, value)) RaiseChanged(); } }

    public ObservableCollection<ForensicPatternViewModel> SuspiciousPatterns { get; } = [];
    public ObservableCollection<ForensicPatternViewModel> MaliciousPatterns  { get; } = [];

    public static IReadOnlyList<string> RiskLevelOptions { get; } = ["None", "Low", "Medium", "High", "Critical"];

    public ICommand AddSuspiciousCommand => new RelayCommand(() => AddPattern(SuspiciousPatterns));
    public ICommand AddMaliciousCommand  => new RelayCommand(() => AddPattern(MaliciousPatterns));

    internal void LoadFrom(ForensicDefinition? def)
    {
        if (def is null) return;
        ForensicCategory = def.Category  ?? "";
        RiskLevel        = def.RiskLevel ?? "Low";
        LoadPatterns(SuspiciousPatterns, def.SuspiciousPatterns);
        LoadPatterns(MaliciousPatterns,  def.KnownMaliciousPatterns);
    }

    internal ForensicDefinition? Build()
    {
        if (string.IsNullOrEmpty(ForensicCategory) && SuspiciousPatterns.Count == 0 && MaliciousPatterns.Count == 0)
            return null;
        return new ForensicDefinition
        {
            Category           = ForensicCategory,
            RiskLevel          = RiskLevel,
            SuspiciousPatterns    = BuildPatterns(SuspiciousPatterns),
            KnownMaliciousPatterns = BuildPatterns(MaliciousPatterns),
        };
    }

    private void AddPattern(ObservableCollection<ForensicPatternViewModel> col)
    {
        var vm = new ForensicPatternViewModel();
        vm.RemoveRequested += (s, _) => { col.Remove((ForensicPatternViewModel)s!); RaiseChanged(); };
        vm.Changed         += (_, _) => RaiseChanged();
        col.Add(vm);
        RaiseChanged();
    }

    private void LoadPatterns(ObservableCollection<ForensicPatternViewModel> col, List<ForensicPattern>? src)
    {
        col.Clear();
        foreach (var p in src ?? [])
        {
            var vm = new ForensicPatternViewModel();
            vm.LoadFrom(p);
            vm.RemoveRequested += (s, _) => { col.Remove((ForensicPatternViewModel)s!); RaiseChanged(); };
            vm.Changed         += (_, _) => RaiseChanged();
            col.Add(vm);
        }
    }

    private static List<ForensicPattern>? BuildPatterns(ObservableCollection<ForensicPatternViewModel> col) =>
        col.Count > 0 ? [..col.Select(vm => vm.Build())] : null;

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}

// ── NavigationViewModel ────────────────────────────────────────────────────────

internal sealed class NavigationBookmarkViewModel : ViewModelBase
{
    internal event EventHandler? Changed;
    internal event EventHandler? RemoveRequested;

    private string _name      = "";
    private string _offsetVar = "";
    private string _icon      = "";
    private string _color     = "";

    public string BookmarkName { get => _name;      set { if (SetField(ref _name, value))      RaiseChanged(); } }
    public string OffsetVar    { get => _offsetVar; set { if (SetField(ref _offsetVar, value)) RaiseChanged(); } }
    public string Icon         { get => _icon;      set { if (SetField(ref _icon, value))      RaiseChanged(); } }
    public string BookmarkColor { get => _color;   set { if (SetField(ref _color, value))     RaiseChanged(); } }

    public ICommand RemoveCommand => new RelayCommand(() => RemoveRequested?.Invoke(this, EventArgs.Empty));

    internal void LoadFrom(NavigationBookmark b)
    {
        BookmarkName  = b.Name      ?? "";
        OffsetVar     = b.OffsetVar ?? "";
        Icon          = b.Icon      ?? "";
        BookmarkColor = b.Color     ?? "";
    }

    internal NavigationBookmark Build() => new()
    {
        Name      = BookmarkName,
        OffsetVar = OffsetVar,
        Icon      = string.IsNullOrEmpty(Icon)          ? null : Icon,
        Color     = string.IsNullOrEmpty(BookmarkColor) ? null : BookmarkColor,
    };

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}

internal sealed class NavigationViewModel : ViewModelBase
{
    internal event EventHandler? Changed;

    public ObservableCollection<NavigationBookmarkViewModel> Bookmarks { get; } = [];

    public ICommand AddBookmarkCommand => new RelayCommand(AddBookmark);

    internal void LoadFrom(NavigationDefinition? def)
    {
        Bookmarks.Clear();
        foreach (var b in def?.Bookmarks ?? []) AddBookmarkFrom(b);
    }

    internal NavigationDefinition? Build()
    {
        if (Bookmarks.Count == 0) return null;
        return new NavigationDefinition { Bookmarks = [..Bookmarks.Select(vm => vm.Build())] };
    }

    private void AddBookmark()
    {
        var vm = new NavigationBookmarkViewModel();
        WireBookmark(vm);
        Bookmarks.Add(vm);
        RaiseChanged();
    }

    private void AddBookmarkFrom(NavigationBookmark b)
    {
        var vm = new NavigationBookmarkViewModel();
        vm.LoadFrom(b);
        WireBookmark(vm);
        Bookmarks.Add(vm);
    }

    private void WireBookmark(NavigationBookmarkViewModel vm)
    {
        vm.RemoveRequested += (s, _) => { Bookmarks.Remove((NavigationBookmarkViewModel)s!); RaiseChanged(); };
        vm.Changed         += (_, _) => RaiseChanged();
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}

// ── InspectorViewModel ────────────────────────────────────────────────────────

internal sealed class InspectorGroupViewModel : ViewModelBase
{
    internal event EventHandler? Changed;
    internal event EventHandler? RemoveRequested;

    private string _title     = "";
    private string _icon      = "";
    private bool   _collapsed;

    public string GroupTitle  { get => _title;     set { if (SetField(ref _title, value))     RaiseChanged(); } }
    public string GroupIcon   { get => _icon;      set { if (SetField(ref _icon, value))      RaiseChanged(); } }
    public bool   IsCollapsed { get => _collapsed; set { if (SetField(ref _collapsed, value)) RaiseChanged(); } }

    public ObservableCollection<StringItemViewModel> Fields { get; } = [];
    public ICommand RemoveCommand => new RelayCommand(() => RemoveRequested?.Invoke(this, EventArgs.Empty));

    public ICommand AddFieldCommand => new RelayCommand(() =>
    {
        var item = new StringItemViewModel("");
        item.RemoveRequested += (s, _) => { Fields.Remove((StringItemViewModel)s!); RaiseChanged(); };
        item.ValueChanged    += (_, _) => RaiseChanged();
        Fields.Add(item);
        RaiseChanged();
    });

    internal void LoadFrom(InspectorGroup g)
    {
        GroupTitle  = g.Title     ?? "";
        GroupIcon   = g.Icon      ?? "";
        IsCollapsed = g.Collapsed;

        Fields.Clear();
        foreach (var f in g.Fields ?? [])
        {
            var item = new StringItemViewModel(f);
            item.RemoveRequested += (s, _) => { Fields.Remove((StringItemViewModel)s!); RaiseChanged(); };
            item.ValueChanged    += (_, _) => RaiseChanged();
            Fields.Add(item);
        }
    }

    internal InspectorGroup Build() => new()
    {
        Title     = GroupTitle,
        Icon      = string.IsNullOrEmpty(GroupIcon) ? null : GroupIcon,
        Collapsed = IsCollapsed,
        Fields    = Fields.Count > 0 ? [..Fields.Select(x => x.Value)] : null,
    };

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}

internal sealed class InspectorViewModel : ViewModelBase
{
    internal event EventHandler? Changed;

    private string _badge        = "";
    private string _primaryField = "";

    public string Badge        { get => _badge;        set { if (SetField(ref _badge, value))        RaiseChanged(); } }
    public string PrimaryField { get => _primaryField; set { if (SetField(ref _primaryField, value)) RaiseChanged(); } }

    public ObservableCollection<InspectorGroupViewModel> Groups { get; } = [];
    public ICommand AddGroupCommand => new RelayCommand(AddGroup);

    internal void LoadFrom(InspectorDefinition? def)
    {
        if (def is null) return;
        Badge        = def.Badge        ?? "";
        PrimaryField = def.PrimaryField ?? "";
        Groups.Clear();
        foreach (var g in def.Groups ?? [])
        {
            var vm = new InspectorGroupViewModel();
            vm.LoadFrom(g);
            WireGroup(vm);
            Groups.Add(vm);
        }
    }

    internal InspectorDefinition? Build()
    {
        if (string.IsNullOrEmpty(Badge) && string.IsNullOrEmpty(PrimaryField) && Groups.Count == 0)
            return null;
        return new InspectorDefinition
        {
            Badge        = string.IsNullOrEmpty(Badge)        ? null : Badge,
            PrimaryField = string.IsNullOrEmpty(PrimaryField) ? null : PrimaryField,
            Groups       = Groups.Count > 0 ? [..Groups.Select(g => g.Build())] : null,
        };
    }

    private void AddGroup()
    {
        var vm = new InspectorGroupViewModel();
        WireGroup(vm);
        Groups.Add(vm);
        RaiseChanged();
    }

    private void WireGroup(InspectorGroupViewModel vm)
    {
        vm.RemoveRequested += (s, _) => { Groups.Remove((InspectorGroupViewModel)s!); RaiseChanged(); };
        vm.Changed         += (_, _) => RaiseChanged();
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}

// ── ExportTemplateViewModel ────────────────────────────────────────────────────

internal sealed class ExportTemplateViewModel : ViewModelBase
{
    internal event EventHandler? Changed;
    internal event EventHandler? RemoveRequested;

    private string _name       = "";
    private string _format     = "json";
    private string _structName = "";

    public string TemplateName { get => _name;       set { if (SetField(ref _name, value))       RaiseChanged(); } }
    public string Format       { get => _format;     set { if (SetField(ref _format, value))     RaiseChanged(); } }
    public string StructName   { get => _structName; set { if (SetField(ref _structName, value)) RaiseChanged(); } }

    public ObservableCollection<StringItemViewModel> Fields   { get; } = [];
    public ObservableCollection<StringItemViewModel> Columns  { get; } = [];

    public static IReadOnlyList<string> FormatOptions { get; } = ["json", "csv", "c-struct", "python-bytes"];

    public ICommand RemoveCommand   => new RelayCommand(() => RemoveRequested?.Invoke(this, EventArgs.Empty));
    public ICommand AddFieldCommand => new RelayCommand(() => AddItem(Fields));
    public ICommand AddColumnCommand => new RelayCommand(() => AddItem(Columns));

    internal void LoadFrom(ExportTemplate t)
    {
        TemplateName = t.Name       ?? "";
        Format       = t.Format     ?? "json";
        StructName   = t.StructName ?? "";
        LoadItems(Fields,  t.Fields);
        LoadItems(Columns, t.Columns);
    }

    internal ExportTemplate Build() => new()
    {
        Name       = TemplateName,
        Format     = Format,
        StructName = string.IsNullOrEmpty(StructName) ? null : StructName,
        Fields     = Fields.Count  > 0 ? [..Fields.Select(x => x.Value)]  : null,
        Columns    = Columns.Count > 0 ? [..Columns.Select(x => x.Value)] : null,
    };

    private void AddItem(ObservableCollection<StringItemViewModel> col)
    {
        var item = new StringItemViewModel("");
        item.RemoveRequested += (s, _) => { col.Remove((StringItemViewModel)s!); RaiseChanged(); };
        item.ValueChanged    += (_, _) => RaiseChanged();
        col.Add(item);
        RaiseChanged();
    }

    private void LoadItems(ObservableCollection<StringItemViewModel> col, List<string>? src)
    {
        col.Clear();
        foreach (var v in src ?? [])
        {
            var item = new StringItemViewModel(v);
            item.RemoveRequested += (s, _) => { col.Remove((StringItemViewModel)s!); RaiseChanged(); };
            item.ValueChanged    += (_, _) => RaiseChanged();
            col.Add(item);
        }
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}

// ── AiHintsViewModel ──────────────────────────────────────────────────────────

internal sealed class AiHintsViewModel : ViewModelBase
{
    internal event EventHandler? Changed;

    private string _analysisContext  = "";
    private string _forensicContext  = "";

    public string AnalysisContext { get => _analysisContext; set { if (SetField(ref _analysisContext, value)) RaiseChanged(); } }
    public string ForensicContext { get => _forensicContext; set { if (SetField(ref _forensicContext, value)) RaiseChanged(); } }

    public ObservableCollection<StringItemViewModel> KnownVulnerabilities  { get; } = [];
    public ObservableCollection<StringItemViewModel> SuggestedInspections  { get; } = [];

    public ICommand AddVulnCommand       => new RelayCommand(() => AddItem(KnownVulnerabilities));
    public ICommand AddInspectionCommand => new RelayCommand(() => AddItem(SuggestedInspections));

    internal void LoadFrom(AiHints? hints)
    {
        if (hints is null) return;
        AnalysisContext = hints.AnalysisContext ?? "";
        ForensicContext = hints.ForensicContext ?? "";
        LoadItems(KnownVulnerabilities, hints.KnownVulnerabilities);
        LoadItems(SuggestedInspections, hints.SuggestedInspections);
    }

    internal AiHints? Build()
    {
        if (string.IsNullOrEmpty(AnalysisContext) && KnownVulnerabilities.Count == 0 && SuggestedInspections.Count == 0)
            return null;
        return new AiHints
        {
            AnalysisContext      = string.IsNullOrEmpty(AnalysisContext) ? null : AnalysisContext,
            ForensicContext      = string.IsNullOrEmpty(ForensicContext) ? null : ForensicContext,
            KnownVulnerabilities = KnownVulnerabilities.Count > 0 ? [..KnownVulnerabilities.Select(x => x.Value)] : null,
            SuggestedInspections = SuggestedInspections.Count > 0 ? [..SuggestedInspections.Select(x => x.Value)] : null,
        };
    }

    private void AddItem(ObservableCollection<StringItemViewModel> col)
    {
        var item = new StringItemViewModel("");
        item.RemoveRequested += (s, _) => { col.Remove((StringItemViewModel)s!); RaiseChanged(); };
        item.ValueChanged    += (_, _) => RaiseChanged();
        col.Add(item);
        RaiseChanged();
    }

    private void LoadItems(ObservableCollection<StringItemViewModel> col, List<string>? src)
    {
        col.Clear();
        foreach (var v in src ?? [])
        {
            var item = new StringItemViewModel(v);
            item.RemoveRequested += (s, _) => { col.Remove((StringItemViewModel)s!); RaiseChanged(); };
            item.ValueChanged    += (_, _) => RaiseChanged();
            col.Add(item);
        }
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}

// ── QualityMetricsViewModel ────────────────────────────────────────────────────

internal sealed class QualityMetricsViewModel : ViewModelBase
{
    internal event EventHandler? Changed;

    private int    _completenessScore;
    private string _documentationLevel = "basic";
    private string _lastUpdated        = "";
    private bool   _priorityFormat;
    private bool   _autoRefined;
    private int    _blocksDefined;
    private int    _validationRules;

    public int    CompletenessScore    { get => _completenessScore;    set => SetField(ref _completenessScore, value); }
    public string DocumentationLevel   { get => _documentationLevel;   set { if (SetField(ref _documentationLevel, value)) RaiseChanged(); } }
    public string LastUpdated          { get => _lastUpdated;          set { if (SetField(ref _lastUpdated, value))         RaiseChanged(); } }
    public bool   PriorityFormat       { get => _priorityFormat;       set { if (SetField(ref _priorityFormat, value))      RaiseChanged(); } }
    public bool   AutoRefined          { get => _autoRefined;          set => SetField(ref _autoRefined, value); }
    public int    BlocksDefined        { get => _blocksDefined;        set => SetField(ref _blocksDefined, value); }
    public int    ValidationRulesCount { get => _validationRules;      set => SetField(ref _validationRules, value); }

    public static IReadOnlyList<string> DocumentationLevelOptions { get; } =
        ["basic", "standard", "detailed", "comprehensive"];

    internal void LoadFrom(QualityMetrics? qm)
    {
        if (qm is null) return;
        CompletenessScore  = qm.CompletenessScore;
        DocumentationLevel = qm.DocumentationLevel ?? "basic";
        LastUpdated        = qm.LastUpdated        ?? "";
        PriorityFormat     = qm.PriorityFormat;
        AutoRefined        = qm.AutoRefined;
        BlocksDefined      = qm.BlocksDefined;
        ValidationRulesCount = qm.ValidationRules;
    }

    internal QualityMetrics Build() => new()
    {
        CompletenessScore  = CompletenessScore,
        DocumentationLevel = DocumentationLevel,
        LastUpdated        = string.IsNullOrEmpty(LastUpdated) ? null : LastUpdated,
        PriorityFormat     = PriorityFormat,
        AutoRefined        = AutoRefined,
        BlocksDefined      = BlocksDefined,
        ValidationRules    = ValidationRulesCount,
    };

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}

// ── UnionVariantViewModel ──────────────────────────────────────────────────────

/// <summary>One row in the union variants DataGrid.</summary>
public sealed class UnionVariantViewModel : ViewModelBase
{
    private string _key       = "";
    private string _length    = "";
    private string _valueType = "";
    private string _color     = "";

    public string Key       { get => _key;       set => SetField(ref _key,       value); }
    public string Length    { get => _length;    set => SetField(ref _length,    value); }
    public string ValueType { get => _valueType; set => SetField(ref _valueType, value); }
    public string Color     { get => _color;     set => SetField(ref _color,     value); }
}
