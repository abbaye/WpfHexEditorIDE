// ==========================================================
// Project: WpfHexEditor.Core.LSP
// File: SmartComplete/CompletionItem.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Completion item produced by BoostedSmartCompleteManager and consumed
//     by the SmartCompletePopup in CodeEditor.
// ==========================================================

namespace WpfHexEditor.Core.LSP.SmartComplete;

/// <summary>Kind of a completion suggestion.</summary>
public enum CompletionKind
{
    Keyword,
    Snippet,
    Symbol,
    Variable,
    Function,
    Class,
    Interface,
    Property,
    Field,
    Enum,
    EnumMember,
    Module,
    Import,
    Text,
}

/// <summary>
/// A single completion suggestion produced by <see cref="BoostedSmartCompleteManager"/>.
/// </summary>
public sealed record CompletionItem(
    string         Label,
    CompletionKind Kind,
    string         InsertText,
    string?        Detail      = null,
    string?        Documentation = null,
    int            SortPriority = 0,
    bool           IsImportSuggestion = false,
    string?        ImportStatement    = null);
