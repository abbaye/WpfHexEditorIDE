// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: Parser/ParseError.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Immutable record describing a single diagnostic produced during
//     DSL parsing: source location (line/column) and a human-readable
//     error message.
//
// Architecture Notes:
//     Record type for value-equality — callers can deduplicate errors
//     trivially via HashSet<ParseError>.
//     Line and Column are 1-based to match editor convention.
// ==========================================================

namespace WpfHexEditor.Editor.ClassDiagram.Core.Parser;

/// <summary>
/// Describes a single parse error produced when processing a DSL source text.
/// </summary>
/// <param name="Line">1-based line number where the error occurred.</param>
/// <param name="Column">1-based column number where the error starts.</param>
/// <param name="Message">Human-readable description of the error.</param>
public sealed record ParseError(int Line, int Column, string Message);
