// ==========================================================
// Project: WpfHexEditor.LSP
// File: Parsing/IncrementalParser.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Incremental document parser that re-tokenises only the dirty lines
//     affected by an edit operation, rather than the entire document.
//     Produces a ParseResult consumed by DiagnosticsEngine and SymbolTableManager.
//
// Architecture Notes:
//     Pattern: Incremental / Dirty-range Tracking
//     - Full parse: ParseFull() — replaces all tokens.
//     - Incremental update: UpdateRange() — re-lexes [startLine..endLine] and
//       merges back into the previous result.
//     - Block-comment state propagation: after re-lexing a range the lexer state
//       (IsInsideBlockComment) is re-propagated downward until stable.
// ==========================================================

namespace WpfHexEditor.LSP.Parsing;

/// <summary>
/// Incremental parser that maintains a <see cref="ParseResult"/> and updates it
/// efficiently when document lines change.
/// </summary>
public sealed class IncrementalParser
{
    private readonly Lexer _lexer;

    // Current full token store: lineIndex → token list.
    private Dictionary<int, IReadOnlyList<Token>> _tokens
        = new();

    // Block-comment state at the *start* of each line (for propagation).
    private Dictionary<int, bool> _commentStateAtLine
        = new();

    public string FilePath { get; }

    public IncrementalParser(string filePath, Lexer lexer)
    {
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        _lexer   = lexer   ?? throw new ArgumentNullException(nameof(lexer));
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Performs a full parse of the document text and returns the result.
    /// </summary>
    public ParseResult ParseFull(string text)
    {
        _tokens            = new();
        _commentStateAtLine = new();
        _lexer.Reset();

        var lines = SplitLines(text);
        for (int i = 0; i < lines.Count; i++)
        {
            _commentStateAtLine[i] = _lexer.IsInsideBlockComment;
            _tokens[i] = _lexer.TokenizeLine(lines[i], i);
        }

        return Snapshot();
    }

    /// <summary>
    /// Re-lexes lines in the range [<paramref name="firstLine"/>..<paramref name="lastLine"/>]
    /// (0-based, inclusive) and propagates block-comment state changes downward.
    /// Returns the updated <see cref="ParseResult"/>.
    /// </summary>
    public ParseResult UpdateRange(string text, int firstLine, int lastLine)
    {
        var lines = SplitLines(text);
        firstLine = Math.Max(0, firstLine);
        lastLine  = Math.Min(lastLine, lines.Count - 1);

        // Re-lex dirty range, carrying comment state from line before.
        _lexer.IsInsideBlockComment = _commentStateAtLine.TryGetValue(firstLine, out var prev) && prev;

        for (int i = firstLine; i <= lastLine; i++)
        {
            _commentStateAtLine[i] = _lexer.IsInsideBlockComment;
            _tokens[i] = _lexer.TokenizeLine(lines[i], i);
        }

        // Propagate state changes beyond the dirty range until stable.
        int propagate = lastLine + 1;
        while (propagate < lines.Count)
        {
            bool prevState = _lexer.IsInsideBlockComment;
            bool storedState = _commentStateAtLine.TryGetValue(propagate, out var s) && s;
            if (prevState == storedState) break; // stable — stop propagation

            _commentStateAtLine[propagate] = prevState;
            _tokens[propagate] = _lexer.TokenizeLine(lines[propagate], propagate);
            propagate++;
        }

        return Snapshot();
    }

    // -----------------------------------------------------------------------

    private ParseResult Snapshot()
        => new(FilePath, new Dictionary<int, IReadOnlyList<Token>>(_tokens), DateTime.UtcNow);

    private static List<string> SplitLines(string text)
    {
        if (string.IsNullOrEmpty(text)) return [];
        return [.. text.Split('\n').Select(l => l.TrimEnd('\r'))];
    }
}
