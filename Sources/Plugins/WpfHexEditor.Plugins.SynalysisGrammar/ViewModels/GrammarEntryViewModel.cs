// ==========================================================
// Project: WpfHexEditor.Plugins.SynalysisGrammar
// File: ViewModels/GrammarEntryViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     ViewModel for a single grammar entry displayed in GrammarSelectorPanel.
//     Represents either an embedded grammar or a user-loaded disk grammar.
//
// Architecture Notes:
//     Pattern: ViewModel (MVVM)
//     Theme: uses global IDE tokens via SetResourceReference in the panel XAML.
// ==========================================================

namespace WpfHexEditor.Plugins.SynalysisGrammar.ViewModels;

/// <summary>
/// Indicates where a grammar entry was loaded from.
/// </summary>
public enum GrammarSource
{
    /// <summary>Shipped as an embedded resource in WpfHexEditor.Definitions.</summary>
    Embedded,

    /// <summary>Loaded from a user-specified file on disk.</summary>
    Disk,

    /// <summary>Contributed by another plugin via IGrammarProvider.</summary>
    Plugin,
}

/// <summary>
/// Represents a single grammar entry in the Grammar Explorer list.
/// </summary>
public sealed class GrammarEntryViewModel
{
    /// <summary>Repository key used to load the grammar (resource name or file path).</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>Human-readable grammar name, e.g. "PNG Images".</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Author name or email. Empty when not specified.</summary>
    public string Author { get; init; } = string.Empty;

    /// <summary>
    /// Comma-separated extension list for display, e.g. ".png, .apng".
    /// </summary>
    public string ExtensionsDisplay { get; init; } = string.Empty;

    /// <summary>Short description of the format.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Where this grammar entry was loaded from.</summary>
    public GrammarSource Source { get; init; }

    /// <summary>Icon glyph for the source badge.</summary>
    public string SourceIcon => Source switch
    {
        GrammarSource.Embedded => "\uE8A5",   // Library glyph
        GrammarSource.Disk     => "\uE8B7",   // OpenFolder glyph
        GrammarSource.Plugin   => "\uE74C",   // Plug glyph
        _                      => "\uE8A5",
    };

    /// <summary>Tooltip label for the source badge.</summary>
    public string SourceLabel => Source switch
    {
        GrammarSource.Embedded => "Built-in",
        GrammarSource.Disk     => "From disk",
        GrammarSource.Plugin   => "Plugin",
        _                      => string.Empty,
    };
}
