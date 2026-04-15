// ==========================================================
// Project: WpfHexEditor.Shell.Panels
// File: Panels/ViewModels/WhfmtFormatItemVm.cs
// Description: ViewModel for a single .whfmt format definition row in the
//              Format Browser tool window and Format Catalog document tab.
// Architecture: Pure ViewModel; no WPF controls. Commands are wired by the
//              parent ViewModel via closure after construction.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Windows.Input;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Shell.Panels.ViewModels;

/// <summary>
/// Identifies the source of a format definition.
/// </summary>
public enum FormatSource
{
    /// <summary>Format is embedded in the WpfHexEditor.Core.Definitions assembly.</summary>
    BuiltIn,
    /// <summary>Format was loaded from the user AppData directory or an additional search path.</summary>
    User,
    /// <summary>Format failed to load; partial metadata may be available.</summary>
    LoadFailure
}

/// <summary>
/// ViewModel representing a single .whfmt format entry in the browser/catalog.
/// </summary>
public sealed class WhfmtFormatItemVm : ViewModelBase
{
    // ------------------------------------------------------------------
    // Identity
    // ------------------------------------------------------------------

    /// <summary>Human-readable format name, e.g. "ZIP Archive".</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Logical category, e.g. "Archives", "Images".</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>Formatted extension list, e.g. ".zip, .jar".</summary>
    public string ExtensionsDisplay { get; init; } = string.Empty;

    /// <summary>Raw extension list for filtering.</summary>
    public IReadOnlyList<string> Extensions { get; init; } = [];

    /// <summary>Short description of the format.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Format specification version.</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>Author or authoring organization.</summary>
    public string Author { get; init; } = string.Empty;

    /// <summary>Target platform, e.g. "NES". Empty for cross-platform.</summary>
    public string Platform { get; init; } = string.Empty;

    /// <summary>Preferred editor factory ID declared in the .whfmt file.</summary>
    public string? PreferredEditor { get; init; }

    /// <summary>Diff mode declared in the .whfmt file.</summary>
    public string? DiffMode { get; init; }

    /// <summary>0-100 quality score. -1 when not available (load failure).</summary>
    public int QualityScore { get; init; }

    /// <summary>Source of this format definition.</summary>
    public FormatSource Source { get; init; }

    /// <summary>
    /// Assembly manifest resource key for built-in formats.
    /// Null for user-supplied formats.
    /// </summary>
    public string? ResourceKey { get; init; }

    /// <summary>
    /// Absolute file path for user-supplied formats or load-failure entries.
    /// Null for built-in formats.
    /// </summary>
    public string? FilePath { get; init; }

    // ------------------------------------------------------------------
    // Derived display helpers
    // ------------------------------------------------------------------

    /// <summary>"Built-in" / "User" / "Failed".</summary>
    public string SourceLabel => Source switch
    {
        FormatSource.BuiltIn     => "Built-in",
        FormatSource.User        => "User",
        FormatSource.LoadFailure => "Failed",
        _                        => "Unknown"
    };

    /// <summary>True when <see cref="Source"/> is <see cref="FormatSource.LoadFailure"/>.</summary>
    public bool IsLoadFailure => Source == FormatSource.LoadFailure;

    /// <summary>True when <see cref="Source"/> is <see cref="FormatSource.User"/>.</summary>
    public bool IsUserFormat => Source == FormatSource.User;

    /// <summary>Error message for load-failure entries. Null otherwise.</summary>
    public string? FailureReason { get; init; }

    // ------------------------------------------------------------------
    // Commands (wired by WhfmtBrowserViewModel / WhfmtCatalogViewModel)
    // ------------------------------------------------------------------

    public ICommand OpenCommand           { get; set; } = DisabledCommand.Instance;
    public ICommand OpenReadOnlyCommand   { get; set; } = DisabledCommand.Instance;
    public ICommand ExportToFileCommand   { get; set; } = DisabledCommand.Instance;
    public ICommand ViewJsonCommand       { get; set; } = DisabledCommand.Instance;
    public ICommand DeleteCommand         { get; set; } = DisabledCommand.Instance;
    public ICommand CopyPathCommand       { get; set; } = DisabledCommand.Instance;
    public ICommand RevealInExplorerCommand { get; set; } = DisabledCommand.Instance;
}

/// <summary>
/// Sentinel ICommand that is always disabled — used as a safe default for
/// unassigned command slots on <see cref="WhfmtFormatItemVm"/>.
/// </summary>
file sealed class DisabledCommand : ICommand
{
    public static readonly DisabledCommand Instance = new();
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => false;
    public void Execute(object? parameter) { }
}
