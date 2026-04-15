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
using System.Linq;
using System.Windows.Input;
using WpfHexEditor.Core.Interfaces;
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
    private string  _extensionsDisplay = string.Empty;
    private int     _qualityScore;
    private string  _detectionRulesSummary = string.Empty;
    private int     _blockCount;
    private string  _loadStatusDisplay = "—";
    private bool    _isLoadFailure;
    private string? _failureReason;
    private string? _rawJson;
    private bool    _hasSelection;

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
        Platform            = string.IsNullOrEmpty(item.Platform) ? "—" : item.Platform;
        DiffMode            = string.IsNullOrEmpty(item.DiffMode) ? "—" : item.DiffMode;
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
        }
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
        ExtensionsDisplay    = string.Empty;
        QualityScore         = 0;
        DetectionRulesSummary= "—";
        BlockCount           = 0;
        LoadStatusDisplay    = "—";
        IsLoadFailure        = false;
        FailureReason        = null;
        RawJson              = null;
    }
}

file sealed class DisabledDetailCommand : ICommand
{
    public static readonly DisabledDetailCommand Instance = new();
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => false;
    public void Execute(object? parameter) { }
}
