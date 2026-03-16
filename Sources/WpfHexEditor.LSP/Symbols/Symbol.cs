// ==========================================================
// Project: WpfHexEditor.LSP
// File: Symbols/Symbol.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Represents a named symbol (variable, function, class, etc.) discovered
//     during document parsing. Consumed by SymbolTable, IntelliSense, and
//     the RefactoringEngine.
// ==========================================================

namespace WpfHexEditor.LSP.Symbols;

/// <summary>Semantic kind of a symbol.</summary>
public enum SymbolKind
{
    Unknown,
    Variable,
    Parameter,
    Function,
    Method,
    Class,
    Interface,
    Struct,
    Enum,
    EnumMember,
    Namespace,
    Import,
    Property,
    Field,
    Event,
    Constant,
    Module,
}

/// <summary>
/// A named symbol within a document.
/// </summary>
public sealed record Symbol(
    string     Name,
    SymbolKind Kind,
    string     FilePath,
    int        Line,
    int        Column,
    string?    Scope      = null,
    string?    TypeName   = null,
    string?    Detail     = null);
