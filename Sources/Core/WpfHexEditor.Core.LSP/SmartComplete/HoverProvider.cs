// ==========================================================
// Project: WpfHexEditor.Core.LSP
// File: SmartComplete/HoverProvider.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Provides hover tooltip content for a given cursor position.
//     Looks up the token under the cursor, checks the symbol table for
//     definition info, and falls back to language keyword documentation.
//
// Architecture Notes:
//     Pattern: Chain of Responsibility
//     Resolution order: Symbol (workspace) → Keyword doc → Token type
//     Returns null when nothing meaningful is available (tooltip suppressed).
// ==========================================================

using WpfHexEditor.Core.LSP.Models;
using WpfHexEditor.Core.LSP.Parsing;
using WpfHexEditor.Core.LSP.Symbols;

namespace WpfHexEditor.Core.LSP.SmartComplete;

/// <summary>
/// Resolves hover tooltip content for a given cursor position in a document.
/// </summary>
public sealed class HoverProvider
{
    private readonly SymbolTableManager      _symbolTableManager;
    private readonly LanguageDefinitionManager _languageManager;

    public HoverProvider(
        SymbolTableManager       symbolTableManager,
        LanguageDefinitionManager languageManager)
    {
        _symbolTableManager = symbolTableManager ?? throw new ArgumentNullException(nameof(symbolTableManager));
        _languageManager    = languageManager    ?? throw new ArgumentNullException(nameof(languageManager));
    }

    // -----------------------------------------------------------------------
    // API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns hover content for the token at (<paramref name="line"/>, <paramref name="column"/>)
    /// in document <paramref name="filePath"/>, using parse tokens from <paramref name="parseResult"/>.
    /// Returns <c>null</c> when there is nothing to show.
    /// </summary>
    public HoverResult? GetHover(
        string      filePath,
        ParseResult parseResult,
        int         line,
        int         column)
    {
        // 1 — Find token under cursor.
        var token = FindToken(parseResult, line, column);
        if (token is null) return null;

        // 2 — Check symbol table for definition info.
        var symbol = _symbolTableManager.FindSymbol(filePath, token.Text);
        if (symbol is not null)
        {
            return new HoverResult(
                Title:   symbol.Name,
                Detail:  $"{symbol.Kind} — defined at line {symbol.Line + 1}",
                Range:   new TextRange(line, token.StartColumn, line, token.StartColumn + token.Text.Length));
        }

        // 3 — Check if the token is a language keyword with documentation.
        var language = _languageManager.GetByFilePath(filePath);
        if (language is not null && IsKeyword(language, token.Text))
        {
            return new HoverResult(
                Title:   token.Text,
                Detail:  $"Keyword ({language.Name})",
                Range:   new TextRange(line, token.StartColumn, line, token.StartColumn + token.Text.Length));
        }

        // 4 — Show token type as minimal fallback for non-whitespace tokens.
        if (token.Type is not TokenType.Whitespace and not TokenType.Unknown)
        {
            return new HoverResult(
                Title:  token.Text,
                Detail: token.Type.ToString(),
                Range:  new TextRange(line, token.StartColumn, line, token.StartColumn + token.Text.Length));
        }

        return null;
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static Token? FindToken(ParseResult parseResult, int line, int column)
    {
        if (!parseResult.TokensByLine.TryGetValue(line, out var lineTokens)) return null;
        return lineTokens.FirstOrDefault(
            t => column >= t.StartColumn && column < t.StartColumn + t.Text.Length);
    }

    private static bool IsKeyword(LanguageDefinition language, string word)
        => language.Keywords.Any(k => k.Equals(word, StringComparison.Ordinal));
}

// -----------------------------------------------------------------------
// Result types
// -----------------------------------------------------------------------

/// <summary>Hover tooltip content with optional source range.</summary>
/// <param name="Title">Primary label (symbol name or keyword).</param>
/// <param name="Detail">Secondary description (type, location, etc.).</param>
/// <param name="Range">Character range that should be highlighted while the tooltip is visible.</param>
public sealed record HoverResult(string Title, string Detail, TextRange Range);

/// <summary>A contiguous character range in a document.</summary>
public sealed record TextRange(int StartLine, int StartColumn, int EndLine, int EndColumn);
