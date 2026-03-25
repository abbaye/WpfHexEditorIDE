// ==========================================================
// Project: WpfHexEditor.Core.LSP
// File: Symbols/SymbolTableManager.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Builds and maintains per-document SymbolTable objects from ParseResult data.
//     Exposes workspace-level symbol queries used by BoostedSmartCompleteManager
//     and RefactoringEngine.
//
// Architecture Notes:
//     Pattern: Observer + Registry
//     - Receives ParseResult via Update(); extracts Identifier tokens heuristically.
//     - Thread-safe: Dictionary protected by lock; SymbolTable.Rebuild called under lock.
// ==========================================================

using WpfHexEditor.Core.LSP.Parsing;

namespace WpfHexEditor.Core.LSP.Symbols;

/// <summary>
/// Manages per-document <see cref="SymbolTable"/> instances and provides
/// workspace-wide symbol queries.
/// </summary>
public sealed class SymbolTableManager
{
    private readonly object _lock = new();
    private readonly Dictionary<string, SymbolTable> _tables
        = new(StringComparer.OrdinalIgnoreCase);

    // -----------------------------------------------------------------------
    // Mutation
    // -----------------------------------------------------------------------

    /// <summary>
    /// Rebuilds the symbol table for the document described by <paramref name="result"/>.
    /// Called by IncrementalParser event handlers after each parse.
    /// </summary>
    public void Update(ParseResult result)
    {
        var symbols = ExtractSymbols(result);
        lock (_lock)
        {
            if (!_tables.TryGetValue(result.FilePath, out var table))
                _tables[result.FilePath] = table = new SymbolTable(result.FilePath);
            table.Rebuild(symbols);
        }

        SymbolTableUpdated?.Invoke(this, result.FilePath);
    }

    /// <summary>Removes the symbol table for a closed document.</summary>
    public void Remove(string filePath)
    {
        lock (_lock) _tables.Remove(filePath);
    }

    // -----------------------------------------------------------------------
    // Query API
    // -----------------------------------------------------------------------

    /// <summary>Returns the <see cref="SymbolTable"/> for <paramref name="filePath"/>, or null.</summary>
    public SymbolTable? GetTable(string filePath)
    {
        lock (_lock)
            return _tables.TryGetValue(filePath, out var t) ? t : null;
    }

    /// <summary>Returns all symbols across all open documents that match <paramref name="name"/>.</summary>
    public IReadOnlyList<Symbol> FindWorkspaceSymbol(string name)
    {
        lock (_lock)
            return [.. _tables.Values.SelectMany(t => t.FindByName(name))];
    }

    /// <summary>Returns all distinct symbol names across the workspace (for completion).</summary>
    public IReadOnlyList<string> GetAllSymbolNames()
    {
        lock (_lock)
            return [.. _tables.Values.SelectMany(t => t.All).Select(s => s.Name).Distinct().OrderBy(n => n)];
    }


    /// <summary>
    /// Finds the first symbol named <paramref name="name"/> in <paramref name="filePath"/>,
    /// falling back to workspace-wide search. Returns null when not found.
    /// </summary>
    public Symbol? FindSymbol(string filePath, string name)
    {
        lock (_lock)
        {
            if (_tables.TryGetValue(filePath, out var table))
            {
                var local = table.FindByName(name).FirstOrDefault();
                if (local is not null) return local;
            }
            return _tables.Values.SelectMany(t => t.FindByName(name)).FirstOrDefault();
        }
    }

    /// <summary>Returns all symbols across all documents whose Name matches name.</summary>
    public IReadOnlyList<Symbol> FindAllReferences(string name)
    {
        lock (_lock)
            return [.. _tables.Values.SelectMany(t => t.FindByName(name))];
    }

    /// <summary>Raised after a symbol table is rebuilt for a document.</summary>
    public event EventHandler<string>? SymbolTableUpdated;

    // -----------------------------------------------------------------------
    // Private: heuristic symbol extraction
    // -----------------------------------------------------------------------

    private static IEnumerable<Symbol> ExtractSymbols(ParseResult result)
    {
        // Heuristic: identifiers that follow def/class/function/var/let/const
        // keywords are treated as definitions. Everything else is a reference.
        // LSP Phase 2C — full language-aware extraction is in Phase 2 LSP.

        var definitionTriggers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "class", "interface", "struct", "enum", "def", "function",
              "func", "fn", "var", "let", "const", "void", "public", "private",
              "protected", "internal", "static", "async" };

        foreach (var (lineIdx, lineTokens) in result.TokensByLine)
        {
            for (int i = 0; i + 1 < lineTokens.Count; i++)
            {
                var cur  = lineTokens[i];
                var next = lineTokens[i + 1];

                if (cur.Type  == TokenType.Keyword
                    && definitionTriggers.Contains(cur.Text)
                    && next.Type == TokenType.Identifier)
                {
                    var kind = GuessKind(cur.Text);
                    yield return new Symbol(
                        Name:     next.Text,
                        Kind:     kind,
                        FilePath: result.FilePath,
                        Line:     next.Line,
                        Column:   next.StartColumn);
                }
            }
        }
    }

    private static SymbolKind GuessKind(string keyword) => keyword.ToLowerInvariant() switch
    {
        "class"                        => SymbolKind.Class,
        "interface"                    => SymbolKind.Interface,
        "struct"                       => SymbolKind.Struct,
        "enum"                         => SymbolKind.Enum,
        "def" or "function" or "func" or "fn" => SymbolKind.Function,
        "var" or "let" or "const"      => SymbolKind.Variable,
        _                              => SymbolKind.Unknown,
    };
}
