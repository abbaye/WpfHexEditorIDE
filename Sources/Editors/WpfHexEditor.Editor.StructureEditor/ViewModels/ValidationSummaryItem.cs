//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////
// Project: WpfHexEditor.Editor.StructureEditor
// File: ViewModels/ValidationSummaryItem.cs
// Description: Model for a single row in the ValidationBar.
//////////////////////////////////////////////

using WpfHexEditor.Editor.Core.Validation;

namespace WpfHexEditor.Editor.StructureEditor.ViewModels;

/// <summary>A single row displayed in the inline <c>ValidationBar</c>.</summary>
public sealed class ValidationSummaryItem
{
    /// <summary>Severity level of this validation result.</summary>
    public ValidationSeverity Severity { get; init; }
    /// <summary>Human-readable description of the issue.</summary>
    public string             Message  { get; init; } = "";
    /// <summary>Line where the issue starts (0-based).</summary>
    public int                Line     { get; init; }
    /// <summary>Column where the issue starts (0-based).</summary>
    public int                Column   { get; init; }
    /// <summary>Validation layer that produced this item (e.g. Schema, Semantic).</summary>
    public string             Layer    { get; init; } = "";

    /// <summary>Segoe MDL2 glyph for the severity icon.</summary>
    public string Glyph => Severity switch
    {
        ValidationSeverity.Error   => "\uE783",
        ValidationSeverity.Warning => "\uE7BA",
        _                          => "\uE946",
    };
}
