// ==========================================================
// Project: WpfHexEditor.Shell.Panels
// File: Panels/ViewModels/WhfmtFormatDetailVm.cs
// Description: Detail card ViewModel for the currently selected format.
//              Shown in the WhfmtFormatDetailPanel (shared by both the
//              Format Browser tool window and the Format Catalog document tab).
// Architecture: Pure ViewModel; lazily loads block count and raw JSON
//              on demand to avoid blocking the UI thread.
// ==========================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using WpfHexEditor.Core.Interfaces;
using WpfHexEditor.Core.Contracts;
using WpfHexEditor.Core.Definitions.Metadata;
using WpfHexEditor.Core.Definitions.Query;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Shell.Panels.ViewModels;

/// <summary>
/// Detail card for a single selected <see cref="WhfmtFormatItemVm"/>.
/// </summary>
public sealed class WhfmtFormatDetailVm : ViewModelBase
{
    private string  _name              = string.Empty;
    private string  _description       = string.Empty;
    private string  _version           = string.Empty;
    private string  _author            = string.Empty;
    private string  _category          = string.Empty;
    private string  _platform          = string.Empty;
    private string  _diffMode          = string.Empty;
    private string  _preferredEditor   = "—";
    private string  _extensionsDisplay = string.Empty;
    private int     _qualityScore;
    private string  _detectionRulesSummary = string.Empty;
    private int     _blockCount;
    private string  _loadStatusDisplay = "—";
    private bool    _isLoadFailure;
    private string? _failureReason;
    private string? _rawJson;
    private bool    _hasSelection;
    private IReadOnlyList<string>               _specificationsDisplay = [];
    private IReadOnlyList<string>               _webLinksDisplay       = [];
    private IReadOnlyList<AssertionDisplayItem> _assertionsSummary     = [];
    private string                              _aiAnalysisContext     = string.Empty;
    private IReadOnlyList<string>               _aiVulnerabilities     = [];
    private int                                 _assertionCount;
    private int                                 _aiHintCount;
    // Piste A — documentary fields surfaced via Format*Extensions
    private IReadOnlyList<SoftwareReference>    _softwareDisplay       = [];
    private IReadOnlyList<string>               _useCasesDisplay       = [];
    private IReadOnlyList<FormatRelationship>   _relationshipsDisplay  = [];
    private string                              _navigationStructure   = string.Empty;
    private string                              _navigationNotes       = string.Empty;

    // ------------------------------------------------------------------
    // Display properties
    // ------------------------------------------------------------------

    public string  Name                 { get => _name;                 set => SetField(ref _name, value); }
    public string  Description          { get => _description;          set => SetField(ref _description, value); }
    public string  Version              { get => _version;              set => SetField(ref _version, value); }
    public string  Author               { get => _author;               set => SetField(ref _author, value); }
    public string  Category             { get => _category;             set => SetField(ref _category, value); }
    public string  Platform             { get => _platform;             set => SetField(ref _platform, value); }
    public string  DiffMode             { get => _diffMode;             set => SetField(ref _diffMode, value); }
    public string  PreferredEditor      { get => _preferredEditor;      set => SetField(ref _preferredEditor, value); }
    public string  ExtensionsDisplay    { get => _extensionsDisplay;    set => SetField(ref _extensionsDisplay, value); }
    public int     QualityScore         { get => _qualityScore;         set => SetField(ref _qualityScore, value); }
    public string  DetectionRulesSummary{ get => _detectionRulesSummary;set => SetField(ref _detectionRulesSummary, value); }
    public int     BlockCount           { get => _blockCount;           set => SetField(ref _blockCount, value); }
    public string  LoadStatusDisplay    { get => _loadStatusDisplay;    set => SetField(ref _loadStatusDisplay, value); }
    public bool    IsLoadFailure        { get => _isLoadFailure;        set => SetField(ref _isLoadFailure, value); }
    public string? FailureReason        { get => _failureReason;        set => SetField(ref _failureReason, value); }

    /// <summary>Raw JSONC text. Loaded lazily when CopyJson/ViewJson is triggered.</summary>
    public string? RawJson              { get => _rawJson;              set => SetField(ref _rawJson, value); }

    /// <summary>True when a format item is selected; false when panel shows placeholder text.</summary>
    public bool HasSelection            { get => _hasSelection;         set => SetField(ref _hasSelection, value); }

    // Tab: References
    public IReadOnlyList<string>               SpecificationsDisplay { get => _specificationsDisplay; set => SetField(ref _specificationsDisplay, value); }
    public IReadOnlyList<string>               WebLinksDisplay       { get => _webLinksDisplay;       set => SetField(ref _webLinksDisplay, value); }
    public bool HasReferences => _specificationsDisplay.Count > 0 || _webLinksDisplay.Count > 0;

    // Tab: Assertions
    public IReadOnlyList<AssertionDisplayItem> AssertionsSummary { get => _assertionsSummary; set => SetField(ref _assertionsSummary, value); }
    public int  AssertionCount { get => _assertionCount; set => SetField(ref _assertionCount, value); }
    public bool HasAssertions  => _assertionCount > 0;

    // Live assertion results (P12) — populated by EvaluateAssertionsAsync
    private IReadOnlyList<AssertionLiveResult> _assertionLiveResults = [];
    private bool   _assertionEvalInProgress;
    private string _assertionEvalStatus = string.Empty;

    /// <summary>Live pass/fail/error results for the current open file. Empty until EvaluateAssertionsAsync is called.</summary>
    public IReadOnlyList<AssertionLiveResult> AssertionLiveResults
    {
        get => _assertionLiveResults;
        private set
        {
            SetField(ref _assertionLiveResults, value);
            OnPropertyChanged(nameof(AssertionLivePassCount));
            OnPropertyChanged(nameof(AssertionLiveFailCount));
            OnPropertyChanged(nameof(HasLiveResults));
        }
    }

    /// <summary>True while an async evaluation is running.</summary>
    public bool   AssertionEvalInProgress { get => _assertionEvalInProgress; private set => SetField(ref _assertionEvalInProgress, value); }
    /// <summary>Short status string ("Evaluating…", "3 pass, 1 fail", etc.).</summary>
    public string AssertionEvalStatus     { get => _assertionEvalStatus;     private set => SetField(ref _assertionEvalStatus, value); }

    public int  AssertionLivePassCount => _assertionLiveResults.Count(r => r.Status == AssertionStatus.Pass);
    public int  AssertionLiveFailCount => _assertionLiveResults.Count(r => r.Status == AssertionStatus.Fail);
    public bool HasLiveResults         => _assertionLiveResults.Count > 0;

    // Tab: AI Hints
    public string                AiAnalysisContext  { get => _aiAnalysisContext; set => SetField(ref _aiAnalysisContext, value); }
    public IReadOnlyList<string> AiVulnerabilities  { get => _aiVulnerabilities; set => SetField(ref _aiVulnerabilities, value); }
    public int  AiHintCount { get => _aiHintCount; set => SetField(ref _aiHintCount, value); }
    public bool HasAiHints  => _aiHintCount > 0;

    // Piste A — Documentary tab (Software / UseCases / Relationships / Navigation)
    public IReadOnlyList<SoftwareReference>  SoftwareDisplay      { get => _softwareDisplay;      set => SetField(ref _softwareDisplay, value); }
    public IReadOnlyList<string>             UseCasesDisplay      { get => _useCasesDisplay;      set => SetField(ref _useCasesDisplay, value); }
    public IReadOnlyList<FormatRelationship> RelationshipsDisplay { get => _relationshipsDisplay; set => SetField(ref _relationshipsDisplay, value); }
    public string                            NavigationStructure  { get => _navigationStructure;  set => SetField(ref _navigationStructure, value); }
    public string                            NavigationNotes      { get => _navigationNotes;      set => SetField(ref _navigationNotes, value); }

    // Per-section visibility flags consumed by Tab 5 XAML to avoid showing
    // section headers above empty content (e.g. "Software" label with no rows).
    public bool HasSoftware             => _softwareDisplay.Count > 0;
    public bool HasUseCases             => _useCasesDisplay.Count > 0;
    public bool HasRelationships        => _relationshipsDisplay.Count > 0;
    public bool HasNavigationStructure  => !string.IsNullOrEmpty(_navigationStructure);
    public bool HasNavigationNotes      => !string.IsNullOrEmpty(_navigationNotes);
    public bool HasDocumentation =>
        HasSoftware || HasUseCases || HasRelationships ||
        HasNavigationStructure || HasNavigationNotes;

    // ------------------------------------------------------------------
    // Commands (set by the parent ViewModel after construction)
    // ------------------------------------------------------------------

    private ICommand _openCommand             = DisabledDetailCommand.Instance;
    private ICommand _exportCommand           = DisabledDetailCommand.Instance;
    private ICommand _copyJsonCommand         = DisabledDetailCommand.Instance;
    private ICommand _retryLoadCommand        = DisabledDetailCommand.Instance;
    private ICommand _excludeCommand          = DisabledDetailCommand.Instance;
    private ICommand _generateParserCommand   = DisabledDetailCommand.Instance;

    public ICommand OpenCommand             { get => _openCommand;           set => SetField(ref _openCommand, value); }
    public ICommand ExportCommand           { get => _exportCommand;         set => SetField(ref _exportCommand, value); }
    public ICommand CopyJsonCommand         { get => _copyJsonCommand;       set => SetField(ref _copyJsonCommand, value); }
    public ICommand RetryLoadCommand        { get => _retryLoadCommand;      set => SetField(ref _retryLoadCommand, value); }
    public ICommand ExcludeCommand          { get => _excludeCommand;        set => SetField(ref _excludeCommand, value); }
    public ICommand GenerateParserCommand   { get => _generateParserCommand; set => SetField(ref _generateParserCommand, value); }

    // ------------------------------------------------------------------
    // Load
    // ------------------------------------------------------------------

    /// <summary>
    /// Populates all display properties from a <see cref="WhfmtFormatItemVm"/>.
    /// Enriches with block count and detection summary from the catalog service
    /// when available.
    /// </summary>
    public void LoadFrom(
        WhfmtFormatItemVm?     item,
        IEmbeddedFormatCatalog embCatalog,
        IFormatCatalogService  catalogSvc)
    {
        if (item is null)
        {
            Clear();
            return;
        }

        HasSelection        = true;
        Name                = item.Name;
        Description         = item.Description;
        Version             = item.Version;
        Author              = item.Author;
        Category            = item.Category;
        Platform            = string.IsNullOrEmpty(item.Platform)         ? "—" : item.Platform;
        DiffMode            = string.IsNullOrEmpty(item.DiffMode)          ? "—" : item.DiffMode;
        PreferredEditor     = string.IsNullOrEmpty(item.PreferredEditor)   ? "—" : item.PreferredEditor;
        ExtensionsDisplay   = item.ExtensionsDisplay;
        QualityScore        = item.QualityScore >= 0 ? item.QualityScore : 0;
        IsLoadFailure       = item.IsLoadFailure;
        FailureReason       = item.FailureReason;
        RawJson             = null; // lazy

        if (item.IsLoadFailure)
        {
            LoadStatusDisplay        = $"FAILED: {item.FailureReason ?? "unknown error"}";
            DetectionRulesSummary    = "—";
            BlockCount               = 0;
        }
        else
        {
            LoadStatusDisplay = "OK";

            // Try to enrich from the full FormatDefinition
            var def = catalogSvc.FindFormat(item.Name);
            if (def is not null)
            {
                BlockCount = def.Blocks?.Count ?? 0;

                var sigCount = def.Detection?.Signatures?.Count ?? 0;
                var extCount = def.Extensions?.Count ?? 0;
                DetectionRulesSummary = sigCount > 0
                    ? $"{sigCount} signature{(sigCount == 1 ? "" : "s")}, {extCount} extension{(extCount == 1 ? "" : "s")}"
                    : extCount > 0
                        ? $"{extCount} extension{(extCount == 1 ? "" : "s")}"
                        : "No detection rules";
            }
            else
            {
                BlockCount            = 0;
                DetectionRulesSummary = "—";
            }

            // Enrich with References, Assertions, AiHints
            if (def is not null)
            {
                SpecificationsDisplay = (IReadOnlyList<string>?)def.References?.Specifications ?? [];
                WebLinksDisplay       = (IReadOnlyList<string>?)def.References?.WebLinks       ?? [];

                if (def.Assertions is { Count: > 0 })
                {
                    AssertionCount    = def.Assertions.Count;
                    AssertionsSummary = def.Assertions
                        .Select(a => new AssertionDisplayItem(a.Name, a.Expression, a.Severity, a.Message))
                        .ToList();
                }
                else
                {
                    AssertionCount    = 0;
                    AssertionsSummary = [];
                }

                if (def.AiHints is not null)
                {
                    AiAnalysisContext = def.AiHints.AnalysisContext ?? string.Empty;
                    AiVulnerabilities = (IReadOnlyList<string>?)def.AiHints.KnownVulnerabilities ?? [];
                    AiHintCount       = (def.AiHints.KnownVulnerabilities?.Count ?? 0)
                                      + (string.IsNullOrEmpty(def.AiHints.AnalysisContext) ? 0 : 1);
                }
                else
                {
                    AiAnalysisContext = string.Empty;
                    AiVulnerabilities = [];
                    AiHintCount       = 0;
                }
            }
            else
            {
                SpecificationsDisplay = [];
                WebLinksDisplay       = [];
                AssertionCount        = 0;
                AssertionsSummary     = [];
                AiAnalysisContext     = string.Empty;
                AiVulnerabilities     = [];
                AiHintCount           = 0;
            }

            // Piste A — documentary fields from the v3 extension methods.
            // The embedded catalog can resolve by name; user-loaded formats won't
            // have an EmbeddedFormatEntry and are skipped (empty lists).
            var entry = embCatalog.GetByName(item.Name);
            if (entry is not null)
            {
                SoftwareDisplay      = entry.GetSoftware(embCatalog);
                UseCasesDisplay      = entry.GetUseCases(embCatalog);
                RelationshipsDisplay = entry.GetFormatRelationships(embCatalog);
                var nav = entry.GetNavigationOverview(embCatalog);
                NavigationStructure  = nav?.Structure is { Count: > 0 } s ? string.Join(" → ", s) : string.Empty;
                NavigationNotes      = nav?.Notes ?? string.Empty;
            }
            else
            {
                SoftwareDisplay      = [];
                UseCasesDisplay      = [];
                RelationshipsDisplay = [];
                NavigationStructure  = string.Empty;
                NavigationNotes      = string.Empty;
            }

            OnPropertyChanged(nameof(HasReferences));
            OnPropertyChanged(nameof(HasAssertions));
            OnPropertyChanged(nameof(HasAiHints));
            OnPropertyChanged(nameof(HasDocumentation));
        }
    }

    /// <summary>
    /// P12 — evaluates the format's assertions against the binary header of the active file.
    /// Safe to call from a background thread; marshals results back via the captured context.
    /// Throttle: callers should debounce ≥500 ms before invoking.
    /// </summary>
    /// <param name="header">First N bytes of the open file (typically 512 B).</param>
    /// <param name="fileSize">Total file length in bytes.</param>
    /// <param name="catalog">The embedded catalog used by <see cref="AssertionLiveEvaluator"/>.</param>
    /// <param name="ct">Cancellation token — previous evaluation should be cancelled before starting a new one.</param>
    public async Task EvaluateAssertionsAsync(
        byte[]                 header,
        long                   fileSize,
        IEmbeddedFormatCatalog catalog,
        CancellationToken      ct = default)
    {
        if (!HasAssertions || !HasSelection) return;

        var entry = catalog.GetByName(Name);
        if (entry is null) return;

        AssertionEvalInProgress = true;
        AssertionEvalStatus     = "Evaluating…";

        try
        {
            var results = await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                var raw = AssertionLiveEvaluator.Evaluate(entry, catalog, header.AsSpan(), fileSize);
                return raw.Select(r => new AssertionLiveResult(r.Rule.Name, r.Status, r.Error)).ToList();
            }, ct);

            AssertionLiveResults = results;
            int pass = results.Count(r => r.Status == AssertionStatus.Pass);
            int fail = results.Count(r => r.Status == AssertionStatus.Fail);
            int err  = results.Count(r => r.Status == AssertionStatus.Error);
            AssertionEvalStatus = $"{pass} pass, {fail} fail" + (err > 0 ? $", {err} error" : "");
        }
        catch (OperationCanceledException)
        {
            AssertionEvalStatus = "Cancelled";
        }
        catch (Exception ex)
        {
            AssertionEvalStatus = $"Error: {ex.Message}";
        }
        finally
        {
            AssertionEvalInProgress = false;
        }
    }

    /// <summary>
    /// Lazily loads raw JSONC text. Call when the JSON tab becomes active.
    /// </summary>
    public void LoadRawJsonIfNeeded(IEmbeddedFormatCatalog emb, IFormatCatalogService svc)
    {
        if (RawJson is not null) return;

        // Try embedded catalog first, then disk
        var item = svc.FindFormat(Name);
        if (item is null) return;

        // For user formats there's no resource key — try file path via FindFormat
        // For built-in formats, IEmbeddedFormatCatalog.GetJson(resourceKey) is the source
        var entry = emb.GetByName(Name);
        if (entry?.ResourceKey is not null)
        {
            RawJson = emb.GetJson(entry.ResourceKey);
            return;
        }

        // User/adhoc format: locate by convention (the catalog service knows the path)
        // Fall back gracefully if not available
        RawJson = "(raw JSON not available for this format source)";
    }

    /// <summary>Resets the panel to its empty/no-selection state.</summary>
    public void Clear()
    {
        HasSelection         = false;
        Name                 = string.Empty;
        Description          = string.Empty;
        Version              = string.Empty;
        Author               = string.Empty;
        Category             = string.Empty;
        Platform             = "—";
        DiffMode             = "—";
        PreferredEditor      = "—";
        ExtensionsDisplay    = string.Empty;
        QualityScore         = 0;
        DetectionRulesSummary= "—";
        BlockCount           = 0;
        LoadStatusDisplay    = "—";
        IsLoadFailure        = false;
        FailureReason        = null;
        RawJson              = null;
        OpenCommand          = DisabledDetailCommand.Instance;
        ExportCommand           = DisabledDetailCommand.Instance;
        CopyJsonCommand         = DisabledDetailCommand.Instance;
        GenerateParserCommand   = DisabledDetailCommand.Instance;
        RetryLoadCommand     = DisabledDetailCommand.Instance;
        ExcludeCommand       = DisabledDetailCommand.Instance;
        SpecificationsDisplay = [];
        WebLinksDisplay      = [];
        AssertionCount           = 0;
        AssertionsSummary        = [];
        AssertionLiveResults     = [];
        AssertionEvalInProgress  = false;
        AssertionEvalStatus      = string.Empty;
        AiAnalysisContext    = string.Empty;
        AiVulnerabilities    = [];
        AiHintCount          = 0;
        SoftwareDisplay      = [];
        UseCasesDisplay      = [];
        RelationshipsDisplay = [];
        NavigationStructure  = string.Empty;
        NavigationNotes      = string.Empty;
        OnPropertyChanged(nameof(HasReferences));
        OnPropertyChanged(nameof(HasAssertions));
        OnPropertyChanged(nameof(HasAiHints));
        OnPropertyChanged(nameof(HasDocumentation));
    }
}

/// <summary>Display model for a single assertion row in the Assertions tab (static catalog view).</summary>
public sealed record AssertionDisplayItem(string Name, string Expression, string Severity, string Message);

/// <summary>P12 — live evaluation result for a single assertion against the active file.</summary>
public sealed record AssertionLiveResult(
    string          Name,
    AssertionStatus Status,
    string?         Error)
{
    /// <summary>Emoji indicator for XAML data templates.</summary>
    public string StatusIcon => Status switch
    {
        AssertionStatus.Pass  => "✓",
        AssertionStatus.Fail  => "✗",
        AssertionStatus.Error => "⚠",
        _                     => "…"
    };
}

file sealed class DisabledDetailCommand : ICommand
{
    public static readonly DisabledDetailCommand Instance = new();
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => false;
    public void Execute(object? parameter) { }
}
