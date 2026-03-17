// ==========================================================
// Project: WpfHexEditor.LSP
// File: Parsing/ParseResult.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Immutable parse result snapshot for one or more document lines.
//     Produced by IncrementalParser; consumed by DiagnosticsEngine,
//     SymbolTableManager, and FoldingManagerIntegration.
// ==========================================================

namespace WpfHexEditor.LSP.Parsing;

/// <summary>
/// Snapshot of the lexer output for a document (or a dirty range within it).
/// </summary>
/// <param name="FilePath">Absolute path of the parsed document.</param>
/// <param name="TokensByLine">Ordered token lists keyed by 0-based line index.</param>
/// <param name="ParsedAt">UTC timestamp of when this result was computed.</param>
public sealed record ParseResult(
    string                                   FilePath,
    IReadOnlyDictionary<int, IReadOnlyList<Token>> TokensByLine,
    DateTime                                 ParsedAt)
{
    /// <summary>Returns all tokens across all lines in line/column order.</summary>
    public IEnumerable<Token> AllTokens
        => TokensByLine.OrderBy(kv => kv.Key).SelectMany(kv => kv.Value);
}
