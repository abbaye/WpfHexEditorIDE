// ==========================================================
// Project: WpfHexEditor.Core.LSP
// File: Parsing/Token.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Token model produced by the Lexer during document parsing.
//     Maps to a LanguageRule type; consumed by DiagnosticsEngine,
//     SymbolTableManager, SmartComplete, and FoldingEngine integrations.
// ==========================================================

namespace WpfHexEditor.Core.LSP.Parsing;

/// <summary>Semantic category of a parsed token.</summary>
public enum TokenType
{
    Unknown,
    Keyword,
    Identifier,
    Type,
    Number,
    String,
    Comment,
    Operator,
    Preprocessor,
    Whitespace,
    NewLine,
    Punctuation,
}

/// <summary>
/// A single lexed token within a document line.
/// </summary>
/// <param name="Type">Semantic type of this token.</param>
/// <param name="Text">The raw text covered by this token.</param>
/// <param name="StartColumn">0-based column where the token starts.</param>
/// <param name="Line">0-based line index.</param>
public sealed record Token(
    TokenType Type,
    string    Text,
    int       StartColumn,
    int       Line)
{
    /// <summary>Number of characters in this token.</summary>
    public int Length => Text.Length;

    /// <summary>0-based column of the last character (inclusive).</summary>
    public int EndColumn => StartColumn + Length - 1;
}
