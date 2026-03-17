// ==========================================================
// Project: WpfHexEditor.LSP
// File: IntelliSense/BoostedIntelliSenseManager.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Workspace-aware IntelliSense completion provider.
//     Aggregates: language keywords, workspace symbols (cross-file),
//     plugin-contributed snippets, and auto-import suggestions.
//     Priority order: UserCreated language > Imported > BuiltIn.
//
// Architecture Notes:
//     Pattern: Aggregator + Priority Queue
//     - GetCompletions() runs synchronously (called on UI thread for popup).
//     - Language priority mirrors LanguagePriority enum: UserCreated items ranked first.
//     - Workspace symbol lookup delegates to WorkspaceSymbolTableManager.
//     - Snippet lookup delegates to LspSnippetsManager.
// ==========================================================

using WpfHexEditor.LSP.Models;
using WpfHexEditor.LSP.Symbols;

namespace WpfHexEditor.LSP.IntelliSense;

/// <summary>
/// Provides context-aware completion items combining language keywords,
/// workspace symbols, and snippets.
/// </summary>
public sealed class BoostedIntelliSenseManager
{
    private readonly LanguageDefinitionManager   _languages;
    private readonly WorkspaceSymbolTableManager _workspaceSymbols;
    private readonly Snippets.LspSnippetsManager _snippets;

    public BoostedIntelliSenseManager(
        LanguageDefinitionManager   languages,
        WorkspaceSymbolTableManager workspaceSymbols,
        Snippets.LspSnippetsManager snippets)
    {
        _languages       = languages       ?? throw new ArgumentNullException(nameof(languages));
        _workspaceSymbols = workspaceSymbols ?? throw new ArgumentNullException(nameof(workspaceSymbols));
        _snippets        = snippets        ?? throw new ArgumentNullException(nameof(snippets));
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns ordered completion items for the given trigger context.
    /// </summary>
    /// <param name="languageId">Active language of the document.</param>
    /// <param name="prefix">Text already typed by the user (filter prefix).</param>
    /// <param name="filePath">Active document path (for local symbol priority).</param>
    public IReadOnlyList<CompletionItem> GetCompletions(
        string languageId,
        string prefix,
        string? filePath = null)
    {
        var results = new List<CompletionItem>();

        // 1. Language keywords (priority by LanguagePriority of the definition).
        var lang = _languages.GetById(languageId);
        if (lang is not null)
            results.AddRange(KeywordsToCompletions(lang, prefix));

        // 2. Workspace symbols (cross-file, local symbols ranked higher).
        results.AddRange(SymbolsToCompletions(_workspaceSymbols.FindByPrefix(prefix), filePath));

        // 3. Snippets.
        results.AddRange(SnippetsToCompletions(
            _snippets.GetSnippets(languageId, prefix)));

        // Sort: SortPriority desc, then alphabetical.
        return [.. results
            .DistinctBy(c => c.Label)
            .OrderByDescending(c => c.SortPriority)
            .ThenBy(c => c.Label)];
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private static IEnumerable<CompletionItem> KeywordsToCompletions(
        LanguageDefinition lang, string prefix)
    {
        int priority = (int)lang.Priority + 10; // keywords above symbols
        foreach (var kw in lang.Keywords)
        {
            if (kw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                yield return new CompletionItem(kw, CompletionKind.Keyword, kw,
                    SortPriority: priority);
        }
    }

    private static IEnumerable<CompletionItem> SymbolsToCompletions(
        IReadOnlyList<Symbol> symbols, string? activeFilePath)
    {
        foreach (var sym in symbols)
        {
            int priority = sym.FilePath.Equals(activeFilePath, StringComparison.OrdinalIgnoreCase)
                ? 5 : 1;
            var kind = sym.Kind switch
            {
                SymbolKind.Function or SymbolKind.Method => CompletionKind.Function,
                SymbolKind.Class                         => CompletionKind.Class,
                SymbolKind.Interface                     => CompletionKind.Interface,
                SymbolKind.Enum                          => CompletionKind.Enum,
                SymbolKind.Property                      => CompletionKind.Property,
                SymbolKind.Field                         => CompletionKind.Field,
                _                                        => CompletionKind.Symbol,
            };
            yield return new CompletionItem(
                sym.Name, kind, sym.Name,
                Detail:       sym.TypeName,
                SortPriority: priority);
        }
    }

    private static IEnumerable<CompletionItem> SnippetsToCompletions(
        IReadOnlyList<(string Trigger, string Body, string Description)> snippets)
    {
        foreach (var (trigger, body, desc) in snippets)
            yield return new CompletionItem(trigger, CompletionKind.Snippet, body,
                Detail: desc, SortPriority: 3);
    }
}
