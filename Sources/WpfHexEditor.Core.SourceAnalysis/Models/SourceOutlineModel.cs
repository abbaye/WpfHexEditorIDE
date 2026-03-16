// ==========================================================
// Project: WpfHexEditor.Core.SourceAnalysis
// File: Models/SourceOutlineModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-16
// Description:
//     Root result of a source-outline parse for a single source file.
//     Produced by SourceOutlineEngine; immutable.
//
// Architecture Notes:
//     BCL-only — no Roslyn, no WPF, no external dependencies.
//     Mirrors the AssemblyModel pattern from WpfHexEditor.Core.AssemblyAnalysis.
// ==========================================================

namespace WpfHexEditor.Core.SourceAnalysis.Models;

/// <summary>Kind of source file that can be outlined by <see cref="Services.SourceOutlineEngine"/>.</summary>
public enum SourceFileKind
{
    CSharp,
    Xaml
}

/// <summary>
/// Root result of a source-outline parse for a single source file.
/// Immutable; produced by <see cref="Services.SourceOutlineEngine"/>.
/// </summary>
public sealed class SourceOutlineModel
{
    /// <summary>Absolute path of the parsed file.</summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>File kind — determines which parser branch was used.</summary>
    public SourceFileKind Kind { get; init; }

    /// <summary>
    /// For .xaml files: the x:Class value (e.g. "MyApp.MainWindow").
    /// Null for .cs files or when x:Class is absent.
    /// </summary>
    public string? XamlClass { get; init; }

    /// <summary>
    /// For .xaml files: named WPF elements discovered via x:Name attributes.
    /// Empty for .cs files.
    /// </summary>
    public IReadOnlyList<XamlNamedElement> XamlElements { get; init; } = [];

    /// <summary>Top-level types declared in the file (for .cs files).</summary>
    public IReadOnlyList<SourceTypeModel> Types { get; init; } = [];

    /// <summary>UTC timestamp of the file when it was parsed. Used for cache invalidation.</summary>
    public DateTime ParsedAt { get; init; }
}

/// <summary>A named WPF element found via x:Name in a .xaml file.</summary>
public sealed class XamlNamedElement
{
    /// <summary>Value of the x:Name attribute, e.g. "SaveButton".</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>XML element tag name used as a type hint, e.g. "Button", "TextBox".</summary>
    public string TypeHint { get; init; } = string.Empty;

    /// <summary>1-based line number where this element was found.</summary>
    public int LineNumber { get; init; }
}
