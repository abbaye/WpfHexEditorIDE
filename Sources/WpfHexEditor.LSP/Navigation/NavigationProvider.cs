// ==========================================================
// Project: WpfHexEditor.LSP
// File: Navigation/NavigationProvider.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Implements Go-to-Definition and Find-All-References navigation
//     using the workspace SymbolTableManager.
//
// Architecture Notes:
//     Pattern: Query Service
//     - GoToDefinition: resolves the token under cursor to its declaration.
//     - FindAllReferences: returns every usage site across all open documents.
//     Both operations are O(n) over the workspace symbol tables — acceptable
//     for file counts typical in WpfHexEditor workspaces (< 200 files).
// ==========================================================

using WpfHexEditor.LSP.Parsing;
using WpfHexEditor.LSP.Symbols;

namespace WpfHexEditor.LSP.Navigation;

/// <summary>
/// Provides Go-to-Definition and Find-All-References for LSP-aware editors.
/// </summary>
public sealed class NavigationProvider
{
    private readonly SymbolTableManager _symbolTableManager;

    public NavigationProvider(SymbolTableManager symbolTableManager)
        => _symbolTableManager = symbolTableManager ?? throw new ArgumentNullException(nameof(symbolTableManager));

    // -----------------------------------------------------------------------
    // API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Resolves the symbol at (<paramref name="line"/>, <paramref name="column"/>) in
    /// <paramref name="filePath"/> to its declaration site.
    /// Returns <c>null</c> when no definition can be found.
    /// </summary>
    public NavigationLocation? GoToDefinition(
        string      filePath,
        ParseResult parseResult,
        int         line,
        int         column)
    {
        var token = FindToken(parseResult, line, column);
        if (token is null || string.IsNullOrWhiteSpace(token.Text)) return null;

        var symbol = _symbolTableManager.FindSymbol(filePath, token.Text);
        if (symbol is null) return null;

        return new NavigationLocation(
            FilePath: symbol.FilePath ?? filePath,
            Line:     symbol.Line,
            Column:   symbol.Column);
    }

    /// <summary>
    /// Returns all locations in the workspace where the symbol named
    /// <paramref name="symbolName"/> appears.
    /// </summary>
    public IReadOnlyList<NavigationLocation> FindAllReferences(string symbolName)
    {
        if (string.IsNullOrWhiteSpace(symbolName))
            return [];

        return _symbolTableManager
            .FindAllReferences(symbolName)
            .Select(s => new NavigationLocation(
                FilePath: s.FilePath ?? string.Empty,
                Line:     s.Line,
                Column:   s.Column))
            .ToList();
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
}

/// <summary>A file + line + column navigation target.</summary>
public sealed record NavigationLocation(string FilePath, int Line, int Column);
