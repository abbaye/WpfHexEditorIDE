// ==========================================================
// Project: WpfHexEditor.Plugins.SynalysisGrammar
// File: Bridge/SynalysisToFieldViewModelBridge.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Converts SynalysisField DTOs (from the Core interpreter) into
//     ParsedFieldViewModel instances for display in the Parsed Fields panel.
//
// Architecture Notes:
//     Pattern: Adapter / Bridge
//     Color is passed as-is (6-char hex) from the grammar fillcolor attribute.
//     IndentLevel is preserved for tree indentation.
//     FieldIcon is assigned based on SynalysisFieldKind.
// ==========================================================

using WpfHexEditor.Core.SynalysisGrammar;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Plugins.SynalysisGrammar.Bridge;

/// <summary>
/// Converts <see cref="SynalysisField"/> list into
/// <see cref="ParsedFieldViewModel"/> list for the Parsed Fields panel.
/// </summary>
public static class SynalysisToFieldViewModelBridge
{
    /// <summary>
    /// Converts a collection of parsed fields into view-model instances.
    /// Returns an empty list when <paramref name="fields"/> is null or empty.
    /// </summary>
    public static IReadOnlyList<ParsedFieldViewModel> Convert(
        IReadOnlyList<SynalysisField>? fields)
    {
        if (fields is null || fields.Count == 0)
            return [];

        var result = new List<ParsedFieldViewModel>(fields.Count);

        foreach (var field in fields)
        {
            result.Add(new ParsedFieldViewModel
            {
                Name           = field.Name,
                Offset         = field.Offset,
                Length         = field.Length,
                FormattedValue = field.ValueDisplay,
                ValueType      = KindToTypeString(field.Kind),
                Description    = field.Description,
                Color          = NormalizeColor(field.Color),
                IndentLevel    = field.IndentLevel,
                GroupName      = field.GroupName,
                FieldIcon      = KindToIcon(field.Kind),
                IsValid        = field.IsValid,
            });
        }

        return result;
    }

    // -- Helpers -----------------------------------------------------------

    private static string KindToTypeString(SynalysisFieldKind kind) => kind switch
    {
        SynalysisFieldKind.Number    => "number",
        SynalysisFieldKind.Binary    => "binary",
        SynalysisFieldKind.String    => "string",
        SynalysisFieldKind.Structure => "structure",
        _                            => "unknown",
    };

    private static string KindToIcon(SynalysisFieldKind kind) => kind switch
    {
        SynalysisFieldKind.Number    => "\uE8EF",   // NumberSymbol glyph
        SynalysisFieldKind.Binary    => "\uE7C3",   // Page glyph
        SynalysisFieldKind.String    => "\uE8AB",   // Font glyph
        SynalysisFieldKind.Structure => "\uE8B7",   // BulletedList glyph
        _                            => "\uE8EF",
    };

    /// <summary>
    /// Normalises a colour string: adds leading '#' if missing.
    /// Returns empty string when <paramref name="colorHex"/> is empty.
    /// </summary>
    private static string NormalizeColor(string colorHex)
    {
        if (string.IsNullOrEmpty(colorHex)) return string.Empty;
        return colorHex.StartsWith('#') ? colorHex : "#" + colorHex;
    }
}
