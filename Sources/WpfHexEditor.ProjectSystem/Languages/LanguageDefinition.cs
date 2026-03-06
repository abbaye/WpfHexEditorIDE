// ==========================================================
// Project: WpfHexEditor.ProjectSystem
// File: Languages/LanguageDefinition.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-05
// Description:
//     Immutable model describing a programming/data language:
//     its file extensions, syntax-highlight rules, snippet triggers,
//     and preferred code-folding strategy.
//     Instances are loaded from .whlang files via LanguageDefinitionSerializer
//     and registered in LanguageRegistry.
//
// Architecture Notes:
//     Value Object — all properties are init-only.
//     Composite    — SyntaxRule[] and SnippetDefinition[] are aggregated.
// ==========================================================

namespace WpfHexEditor.ProjectSystem.Languages;

/// <summary>
/// Describes the syntax, snippets and folding behaviour for a language.
/// Consumed by <see cref="LanguageRegistry"/> and the CodeEditor plug-in.
/// </summary>
public sealed class LanguageDefinition
{
    /// <summary>Unique language identifier (e.g. "json", "csharp", "asm-6502").</summary>
    public required string Id { get; init; }

    /// <summary>Human-readable name displayed in UI pickers.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// File extensions handled by this language (lower-case, with dot, e.g. ".json").
    /// </summary>
    public IReadOnlyList<string> Extensions { get; init; } = [];

    /// <summary>Ordered list of regex-based token rules for syntax highlighting.</summary>
    public IReadOnlyList<SyntaxRule> SyntaxRules { get; init; } = [];

    /// <summary>Snippet definitions registered in SnippetManager at startup.</summary>
    public IReadOnlyList<SnippetDefinition> Snippets { get; init; } = [];

    /// <summary>Preferred code-folding strategy for this language.</summary>
    public FoldingStrategyKind FoldingStrategy { get; init; } = FoldingStrategyKind.Brace;

    /// <summary>
    /// Optional single-line comment prefix (e.g. "//" or "#").
    /// Used for Toggle Comment commands.
    /// </summary>
    public string? LineCommentPrefix { get; init; }
}

/// <summary>Maps a regex pattern to a token kind for syntax highlighting.</summary>
public sealed class SyntaxRule
{
    /// <summary>Regular expression applied to each line of source text.</summary>
    public required string Pattern { get; init; }

    /// <summary>Token kind assigned to matches of <see cref="Pattern"/>.</summary>
    public required SyntaxTokenKind Kind { get; init; }
}

/// <summary>A snippet definition serialised in a .whlang file.</summary>
public sealed class SnippetDefinition
{
    /// <summary>Text trigger (typed before Tab press) that expands this snippet.</summary>
    public required string Trigger { get; init; }

    /// <summary>
    /// Expanded body. Use <c>$cursor</c> to mark the caret insertion point.
    /// </summary>
    public required string Body { get; init; }

    /// <summary>Short description shown in the IntelliSense completion list.</summary>
    public string Description { get; init; } = string.Empty;
}

/// <summary>Token category used by <see cref="SyntaxRule"/> and the renderer.</summary>
public enum SyntaxTokenKind
{
    Default,
    Keyword,
    String,
    Number,
    Comment,
    Identifier,
    Operator,
    Bracket,
    Type,
    Attribute
}

/// <summary>Selects the folding algorithm used by the CodeEditor gutter.</summary>
public enum FoldingStrategyKind
{
    None,
    Brace,
    Indent
}
