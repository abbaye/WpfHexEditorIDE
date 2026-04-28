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
using System.Windows.Input;
using WpfHexEditor.Core.Interfaces;
using WpfHexEditor.Core.Contracts;
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

    // Tab: AI Hints
    public string                AiAnalysisContext  { get => _aiAnalysisContext; set => SetField(ref _aiAnalysisContext, value); }
    public IReadOnlyList<string> AiVulnerabilities  { get => _aiVulnerabilities; set => SetField(ref _aiVulnerabilities, value); }
    public int  AiHintCount { get => _aiHintCount; set => SetField(ref _aiHintCount, value); }
    public bool HasAiHints  => _aiHintCount > 0;

    // ------------------------------------------------------------------
    // Commands (set by the parent ViewModel after construction)
    // ------------------------------------------------------------------

    public ICommand OpenCommand       { get; set; } = DisabledDetailCommand.Instance;
    public ICommand ExportCommand     { get; set; } = DisabledDetailCommand.Instance;
    public ICommand CopyJsonCommand   { get; set; } = DisabledDetailCommand.Instance;
    public ICommand RetryLoadCommand  { get; set; } = DisabledDetailCommand.Instance;
    public ICommand ExcludeCommand    { get; set; } = DisabledDetailCommand.Instance;

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

            OnPropertyChanged(nameof(HasReferences));
            OnPropertyChanged(nameof(HasAssertions));
            OnPropertyChanged(nameof(HasAiHints));
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
        var entry = emb.Query().WithName(Name).First();
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
        SpecificationsDisplay = [];
        WebLinksDisplay      = [];
        AssertionCount       = 0;
        AssertionsSummary    = [];
        AiAnalysisContext    = string.Empty;
        AiVulnerabilities    = [];
        AiHintCount          = 0;
        OnPropertyChanged(nameof(HasReferences));
        OnPropertyChanged(nameof(HasAssertions));
        OnPropertyChanged(nameof(HasAiHints));
    }
}

/// <summary>Display model for a single assertion row in the Assertions tab.</summary>
public sealed record AssertionDisplayItem(string Name, string Expression, string Severity, string Message);

file sealed class DisabledDetailCommand : ICommand
{
    public static readonly DisabledDetailCommand Instance = new();
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => false;
    public void Execute(object? parameter) { }
}
