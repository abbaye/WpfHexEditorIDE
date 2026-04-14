//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////
// Project: WpfHexEditor.Editor.StructureEditor
// File: ViewModels/ValidationSummaryItem.cs
// Description: Model for a single row in the ValidationBar.
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.StructureEditor.ViewModels;

/// <summary>Severity level for a validation result.</summary>
public enum ValidationSeverity { Info, Warning, Error }

/// <summary>A single row displayed in the inline <c>ValidationBar</c>.</summary>
public sealed class ValidationSummaryItem
{
    public ValidationSeverity Severity { get; init; }
    public string             Message  { get; init; } = "";
    public int                Line     { get; init; }
    public int                Column   { get; init; }
    public string             Layer    { get; init; } = "";

    /// <summary>Segoe MDL2 glyph for the severity icon.</summary>
    public string Glyph => Severity switch
    {
        ValidationSeverity.Error   => "\uE783",
        ValidationSeverity.Warning => "\uE7BA",
        _                          => "\uE946",
    };
}
