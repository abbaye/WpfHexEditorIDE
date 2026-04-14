//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////
// Project: WpfHexEditor.Editor.StructureEditor
// File: ViewModels/StructureEditorViewModel.cs
// Description: Root VM — orchestrates all child VMs, dirty tracking,
//              500ms validation debounce, load/save.
//////////////////////////////////////////////

using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Threading;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Editor.StructureEditor.ViewModels;

internal sealed class StructureEditorViewModel : ViewModelBase
{
    // ── Events ────────────────────────────────────────────────────────────────

    internal event EventHandler? DirtyChanged;
    internal event EventHandler? ValidationCompleted;

    // ── Child VMs ─────────────────────────────────────────────────────────────

    public MetadataViewModel    Metadata    { get; } = new();
    public DetectionViewModel   Detection   { get; } = new();
    public BlocksViewModel      Blocks      { get; } = new();
    public VariablesViewModel   Variables   { get; } = new();
    public QualityMetricsViewModel QualityMetrics { get; } = new();
    public ForensicViewModel    Forensic    { get; } = new();
    public NavigationViewModel  Navigation  { get; } = new();
    public InspectorViewModel   Inspector   { get; } = new();
    public AiHintsViewModel     AiHints     { get; } = new();

    public ObservableCollection<AssertionViewModel>     Assertions      { get; } = [];
    public ObservableCollection<ChecksumViewModel>      Checksums       { get; } = [];
    public ObservableCollection<ExportTemplateViewModel> ExportTemplates { get; } = [];

    // ── Validation ────────────────────────────────────────────────────────────

    public ObservableCollection<ValidationSummaryItem> ValidationSummary { get; } = [];

    private int _errorCount;
    private int _warningCount;
    public int ErrorCount   { get => _errorCount;   private set => SetField(ref _errorCount, value); }
    public int WarningCount { get => _warningCount; private set => SetField(ref _warningCount, value); }

    // ── Dirty state ───────────────────────────────────────────────────────────

    private bool _isDirty;
    public bool IsDirty { get => _isDirty; private set { if (SetField(ref _isDirty, value)) DirtyChanged?.Invoke(this, EventArgs.Empty); } }

    // ── JSON options ──────────────────────────────────────────────────────────

    internal static readonly JsonSerializerOptions LoadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling         = JsonCommentHandling.Skip,
        AllowTrailingCommas         = true,
    };

    internal static readonly JsonSerializerOptions SaveOptions = new()
    {
        WriteIndented          = true,
        PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // ── Debounce ──────────────────────────────────────────────────────────────

    private DispatcherTimer? _debounce;
    private Func<string, Task<List<ValidationSummaryItem>>>? _validateAsync;

    // ── Constructor ───────────────────────────────────────────────────────────

    internal StructureEditorViewModel()
    {
        WireAll();
    }

    internal void SetValidator(Func<string, Task<List<ValidationSummaryItem>>> validateAsync)
    {
        _validateAsync = validateAsync;
        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _debounce.Tick += OnDebounceElapsed;
    }

    // ── Load / Save ───────────────────────────────────────────────────────────

    internal void LoadFromDefinition(FormatDefinition def)
    {
        Metadata.LoadFrom(def);
        Detection.LoadFrom(def.Detection);
        Blocks.LoadFrom(def.Blocks);
        Variables.LoadFrom(def.Variables);
        QualityMetrics.LoadFrom(def.QualityMetrics);
        Forensic.LoadFrom(def.Forensic);
        Navigation.LoadFrom(def.Navigation);
        Inspector.LoadFrom(def.Inspector);
        AiHints.LoadFrom(def.AiHints);

        LoadCollection(Assertions, def.Assertions, (vm, a) => vm.LoadFrom(a), () => new AssertionViewModel());
        LoadCollection(Checksums,  def.Checksums,  (vm, c) => vm.LoadFrom(c), () => new ChecksumViewModel());
        LoadCollection(ExportTemplates, def.ExportTemplates, (vm, t) => vm.LoadFrom(t), () => new ExportTemplateViewModel());

        IsDirty = false;
    }

    internal FormatDefinition BuildDefinition()
    {
        var def = new FormatDefinition();
        Metadata.SaveTo(def);
        Detection.SaveTo(def);
        Variables.SaveTo(def);
        def.Blocks = Blocks.BuildBlocks();

        def.QualityMetrics = QualityMetrics.Build();
        def.Forensic       = Forensic.Build();
        def.Navigation     = Navigation.Build();
        def.Inspector      = Inspector.Build();
        def.AiHints        = AiHints.Build();

        def.Assertions      = Assertions.Count > 0 ? [..Assertions.Select(vm => vm.Build())] : null;
        def.Checksums       = Checksums.Count  > 0 ? [..Checksums.Select(vm => vm.Build())]  : null;
        def.ExportTemplates = ExportTemplates.Count > 0 ? [..ExportTemplates.Select(vm => vm.Build())] : null;

        return def;
    }

    internal string SerializeToJson() =>
        JsonSerializer.Serialize(BuildDefinition(), SaveOptions);

    internal void MarkSaved() => IsDirty = false;

    /// <summary>Alias for <see cref="MarkSaved"/> used by code-behind after file write.</summary>
    internal void ClearDirty() => MarkSaved();

    /// <summary>Deserializes <paramref name="json"/> and populates all child VMs.</summary>
    internal void LoadFromJson(string json)
    {
        var def = JsonSerializer.Deserialize<FormatDefinition>(json, LoadOptions) ?? new FormatDefinition();
        LoadFromDefinition(def);
    }

    /// <summary>Clears all child VMs back to defaults.</summary>
    internal void Reset()
    {
        LoadFromDefinition(new FormatDefinition());
        IsDirty = false;
    }

    /// <summary>Forces an immediate validation cycle (bypasses debounce).</summary>
    internal void TriggerValidationNow()
    {
        _debounce?.Stop();
        OnDebounceElapsed(this, EventArgs.Empty);
    }

    // ── Commands for v2.0 collections ─────────────────────────────────────────

    internal void AddAssertion()
    {
        var vm = new AssertionViewModel();
        vm.Changed += OnCollectionItemChanged;
        Assertions.Add(vm);
        MarkDirty();
    }

    internal void AddChecksum()
    {
        var vm = new ChecksumViewModel();
        vm.Changed += OnCollectionItemChanged;
        Checksums.Add(vm);
        MarkDirty();
    }

    internal void AddExportTemplate()
    {
        var vm = new ExportTemplateViewModel();
        vm.Changed += OnCollectionItemChanged;
        ExportTemplates.Add(vm);
        MarkDirty();
    }

    // ── Validation debounce ───────────────────────────────────────────────────

    private async void OnDebounceElapsed(object? sender, EventArgs e)
    {
        _debounce?.Stop();
        if (_validateAsync is null) return;

        var json    = SerializeToJson();
        var results = await _validateAsync(json);

        ValidationSummary.Clear();
        foreach (var item in results.Take(10))
            ValidationSummary.Add(item);

        ErrorCount   = results.Count(r => r.Severity == ValidationSeverity.Error);
        WarningCount = results.Count(r => r.Severity == ValidationSeverity.Warning);
        ValidationCompleted?.Invoke(this, EventArgs.Empty);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Called via subscription in ctor — but C# events require ref attachment:
    internal void WireAll()
    {
        Metadata.Changed      += OnChildChanged;
        Detection.Changed     += OnChildChanged;
        Blocks.Changed        += OnChildChanged;
        Variables.Changed     += OnChildChanged;
        QualityMetrics.Changed += OnChildChanged;
        Forensic.Changed      += OnChildChanged;
        Navigation.Changed    += OnChildChanged;
        Inspector.Changed     += OnChildChanged;
        AiHints.Changed       += OnChildChanged;
    }

    private void OnChildChanged(object? sender, EventArgs e) => MarkDirty();
    private void OnCollectionItemChanged(object? sender, EventArgs e) => MarkDirty();

    private void MarkDirty()
    {
        IsDirty = true;
        _debounce?.Stop();
        _debounce?.Start();
    }

    private void LoadCollection<TVM, TModel>(
        ObservableCollection<TVM> col,
        List<TModel>? src,
        Action<TVM, TModel> load,
        Func<TVM> create) where TVM : class
    {
        col.Clear();
        foreach (var item in src ?? [])
        {
            var vm = create();
            load(vm, item);
            col.Add(vm);
        }
    }

}
