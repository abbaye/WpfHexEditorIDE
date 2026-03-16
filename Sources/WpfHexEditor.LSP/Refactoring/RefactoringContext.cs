// ==========================================================
// Project: WpfHexEditor.LSP
// File: Refactoring/RefactoringContext.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Carries the context needed by IRefactoring implementations:
//     document text, caret position, selection, parse result, and symbol table.
// ==========================================================

using WpfHexEditor.LSP.Parsing;
using WpfHexEditor.LSP.Symbols;

namespace WpfHexEditor.LSP.Refactoring;

/// <summary>
/// Snapshot of editor state passed to <see cref="IRefactoring"/> operations.
/// </summary>
public sealed class RefactoringContext
{
    /// <summary>Current full document text.</summary>
    public string DocumentText { get; init; } = string.Empty;

    /// <summary>Absolute path of the document.</summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>Zero-based character offset of the caret.</summary>
    public int CaretOffset { get; init; }

    /// <summary>Zero-based offset of the selection start (equals CaretOffset if no selection).</summary>
    public int SelectionStart { get; init; }

    /// <summary>Length of the selection (0 = no selection).</summary>
    public int SelectionLength { get; init; }

    /// <summary>Latest parse result for this document (may be null if not yet parsed).</summary>
    public ParseResult? ParseResult { get; init; }

    /// <summary>Symbol table for this document (may be null if not yet built).</summary>
    public SymbolTable? SymbolTable { get; init; }

    /// <summary>Symbol table manager for workspace-wide renames.</summary>
    public SymbolTableManager? SymbolTableManager { get; init; }

    /// <summary>Gets the currently selected text, or empty string if nothing selected.</summary>
    public string SelectedText
        => SelectionLength > 0
           ? DocumentText.Substring(Math.Max(0, SelectionStart), Math.Min(SelectionLength, DocumentText.Length - SelectionStart))
           : string.Empty;
}
