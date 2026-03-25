// ==========================================================
// Project: WpfHexEditor.Core.LSP
// File: SmartComplete/WorkspaceSymbolTableManager.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Aggregates all per-document SymbolTable instances and exposes a
//     workspace-wide symbol query interface for BoostedSmartCompleteManager.
//     Subscribes to SymbolTableManager.SymbolTableUpdated to keep its
//     cache fresh after each parse.
// ==========================================================

using WpfHexEditor.Core.LSP.Symbols;

namespace WpfHexEditor.Core.LSP.SmartComplete;

/// <summary>
/// Workspace-level symbol view that aggregates all document <see cref="SymbolTable"/> instances.
/// </summary>
public sealed class WorkspaceSymbolTableManager
{
    private readonly SymbolTableManager _documentManager;
    private volatile IReadOnlyList<string> _cachedNames = [];

    public WorkspaceSymbolTableManager(SymbolTableManager documentManager)
    {
        _documentManager = documentManager ?? throw new ArgumentNullException(nameof(documentManager));
        _documentManager.SymbolTableUpdated += OnSymbolTableUpdated;
    }

    // -----------------------------------------------------------------------
    // Query API
    // -----------------------------------------------------------------------

    /// <summary>Returns all distinct symbol names known across the workspace.</summary>
    public IReadOnlyList<string> AllSymbolNames => _cachedNames;

    /// <summary>
    /// Returns workspace symbols whose name starts with <paramref name="prefix"/>
    /// (case-insensitive).
    /// </summary>
    public IReadOnlyList<Symbol> FindByPrefix(string prefix)
    {
        if (string.IsNullOrEmpty(prefix)) return [];
        return [.. _documentManager.GetAllSymbolNames()
            .Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .SelectMany(n => _documentManager.FindWorkspaceSymbol(n))];
    }

    // -----------------------------------------------------------------------

    private void OnSymbolTableUpdated(object? sender, string filePath)
    {
        _cachedNames = _documentManager.GetAllSymbolNames();
    }
}
