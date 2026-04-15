//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////
// Project: WpfHexEditor.Editor.StructureEditor
// File: Models/ExpressionCompleteSuggestion.cs
// Description: Immutable suggestion item for ExpressionCompletePopup.
//              Mirrors SmartCompleteSuggestion but is self-contained within
//              the StructureEditor assembly — no CodeEditor dependency.
// Architecture Notes:
//     MatchScore and MatchedIndices are set by ExpressionFuzzyScorer
//     and are mutable to avoid re-allocation during filter passes.
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.StructureEditor.Models;

internal sealed class ExpressionCompleteSuggestion
{
    /// <summary>Text shown in the suggestion list.</summary>
    public required string DisplayText { get; init; }

    /// <summary>Text inserted into the expression box when committed.
    /// Replaces the current token only (the part after the active prefix).</summary>
    public required string InsertText { get; init; }

    /// <summary>Segoe MDL2 glyph character used as icon.</summary>
    public string Icon { get; init; } = "\uE74B"; // Variable default

    /// <summary>Muted secondary label (e.g. "function", "variable", "prefix").</summary>
    public string? TypeHint { get; init; }

    /// <summary>Signature or description shown in the popup footer.</summary>
    public string? Documentation { get; init; }

    /// <summary>Lower = shown first in sorted list.</summary>
    public int SortPriority { get; init; }

    // ── Set by fuzzy scorer during filter pass ────────────────────────────────

    public int MatchScore { get; set; }
    public IReadOnlyList<int>? MatchedIndices { get; set; }

    /// <summary>When non-null, caret is placed at InsertText.Length - CursorOffset
    /// after commit (e.g. 1 → inside closing parenthesis of a function call).</summary>
    public int? CursorOffset { get; init; }
}
