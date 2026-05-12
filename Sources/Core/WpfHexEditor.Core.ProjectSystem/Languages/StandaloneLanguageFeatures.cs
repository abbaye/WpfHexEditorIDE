// ==========================================================
// Project: WpfHexEditor.ProjectSystem
// File: Languages/StandaloneLanguageFeatures.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7 (1M context)
// Created: 2026-05-11 (whfmt v3 P8)
// Description:
//     Three new syntaxDefinition sub-features added in P8 (ADR-038 D8) so that
//     CodeEditor can deliver completions, source outline and diagnostics for
//     languages that do NOT have a Roslyn workspace (e.g. Python, shell,
//     custom DSLs declared purely in .whfmt).
//
//     - CompletionItem    : pattern-free keyword/symbol completions
//     - OutlineRule       : regex-based source-outline / symbol-list rules
//     - DiagnosticRule    : regex-based pattern diagnostics with severity
//
// Architecture Notes:
//     Value Objects, all init-only. Compiled regexes are constructed once at
//     load time and cached on the rule instance.
// ==========================================================

using System.Text.RegularExpressions;

namespace WpfHexEditor.Core.ProjectSystem.Languages;

/// <summary>Kind of completion item — drives the IDE's IntelliSense icon.</summary>
public enum WhfmtCompletionKind
{
    Keyword,
    Class,
    Method,
    Property,
    Variable,
    Module,
    Snippet,
    Other,
}

/// <summary>
/// A single completion proposed by the language definition's standalone path
/// (when no Roslyn workspace is available).
/// </summary>
public sealed class WhfmtCompletionItem
{
    public required string Label { get; init; }
    public WhfmtCompletionKind Kind { get; init; } = WhfmtCompletionKind.Keyword;
    public string? Detail { get; init; }
    /// <summary>Text inserted on accept. Falls back to <see cref="Label"/> when null.</summary>
    public string? InsertText { get; init; }
}

/// <summary>Severity of a pattern-based whfmt diagnostic.</summary>
public enum WhfmtDiagnosticSeverity
{
    Error,
    Warning,
    Info,
    Hint,
}

/// <summary>
/// A regex pattern that produces an outline / source-symbol entry on match.
/// </summary>
public sealed class WhfmtOutlineRule
{
    public required string Kind { get; init; }
    public required Regex  Pattern { get; init; }
    /// <summary>Regex capture group containing the displayed symbol name. Default 1.</summary>
    public int Group { get; init; } = 1;
}

/// <summary>
/// A regex pattern that produces a diagnostic on match (e.g. WH0001 TODO).
/// </summary>
public sealed class WhfmtDiagnosticRule
{
    public required string Id { get; init; }
    public required Regex  Pattern { get; init; }
    public WhfmtDiagnosticSeverity Severity { get; init; } = WhfmtDiagnosticSeverity.Info;
    public required string Message { get; init; }
}
