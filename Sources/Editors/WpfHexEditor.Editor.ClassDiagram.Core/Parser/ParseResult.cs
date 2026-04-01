// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: Parser/ParseResult.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Encapsulates the outcome of a DSL parse operation: the produced
//     DiagramDocument (possibly partial on error) and the list of
//     ParseErrors collected during parsing.
//
// Architecture Notes:
//     Always returns a non-null Document so callers can render partial
//     diagrams even when errors are present.
//     IsValid is a convenience shorthand for Errors.Count == 0.
// ==========================================================

using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Core.Parser;

/// <summary>
/// Result returned by <see cref="ClassDiagramParser.Parse"/>.
/// Contains both the produced document and any diagnostics.
/// </summary>
public sealed class ParseResult
{
    /// <summary>
    /// The diagram document built from the DSL text.
    /// May be partially populated when <see cref="IsValid"/> is <see langword="false"/>.
    /// </summary>
    public required DiagramDocument Document { get; init; }

    /// <summary>All errors collected during parsing. Empty when parsing succeeded.</summary>
    public List<ParseError> Errors { get; init; } = [];

    /// <summary>
    /// <see langword="true"/> when no errors were encountered during parsing.
    /// </summary>
    public bool IsValid => Errors.Count == 0;
}
