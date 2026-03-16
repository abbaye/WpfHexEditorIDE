// ==========================================================
// Project: WpfHexEditor.LSP
// File: Symbols/SymbolTable.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Per-document symbol table built from ParseResult tokens.
//     Provides fast lookup by name, line range, and scope.
// ==========================================================

namespace WpfHexEditor.LSP.Symbols;

/// <summary>
/// Holds all <see cref="Symbol"/> objects discovered in a single document.
/// </summary>
public sealed class SymbolTable
{
    private readonly List<Symbol> _symbols = [];

    /// <summary>The file path this table belongs to.</summary>
    public string FilePath { get; }

    /// <summary>UTC timestamp of the last rebuild.</summary>
    public DateTime UpdatedAt { get; private set; }

    public SymbolTable(string filePath)
    {
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    }

    // -----------------------------------------------------------------------
    // Mutation (called by SymbolTableManager)
    // -----------------------------------------------------------------------

    /// <summary>Replaces all symbols with the given collection.</summary>
    internal void Rebuild(IEnumerable<Symbol> symbols)
    {
        _symbols.Clear();
        _symbols.AddRange(symbols);
        UpdatedAt = DateTime.UtcNow;
    }

    // -----------------------------------------------------------------------
    // Query API
    // -----------------------------------------------------------------------

    /// <summary>Returns all symbols in this document.</summary>
    public IReadOnlyList<Symbol> All => _symbols;

    /// <summary>Returns all symbols with the given name (case-sensitive).</summary>
    public IReadOnlyList<Symbol> FindByName(string name)
        => [.. _symbols.Where(s => s.Name == name)];

    /// <summary>Returns the symbol that best matches <paramref name="name"/> at the given position.</summary>
    public Symbol? FindDefinition(string name, int line)
    {
        // Prefer symbols defined on or before the reference line in the same scope.
        return _symbols
            .Where(s => s.Name == name && s.Line <= line)
            .OrderByDescending(s => s.Line)
            .FirstOrDefault()
            ?? _symbols.FirstOrDefault(s => s.Name == name);
    }

    /// <summary>Returns all symbols visible within <paramref name="scope"/>.</summary>
    public IReadOnlyList<Symbol> GetInScope(string scope)
        => [.. _symbols.Where(s => s.Scope is null || s.Scope == scope)];
}
