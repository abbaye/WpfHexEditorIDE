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

namespace WpfHexEditor.Core.ProjectSystem.Languages;

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

    /// <summary>Opening delimiter for block comments (e.g. "/*" or "&lt;!--"). Null if not applicable.</summary>
    public string? BlockCommentStart { get; init; }

    /// <summary>Closing delimiter for block comments (e.g. "*/" or "--&gt;"). Null if not applicable.</summary>
    public string? BlockCommentEnd { get; init; }

    /// <summary>
    /// When <see langword="true"/>, the CodeEditor will render inline hints (inline
    /// reference counts) for this language.  Should only be enabled for languages that
    /// have structural parsing support (e.g. C#, VB.NET).
    /// </summary>
    public bool EnableInlineHints { get; init; }

    /// <summary>
    /// When <see langword="true"/>, Ctrl+click go-to-definition navigation is active for
    /// this language.  Disable for data/markup/script languages that have no declaration
    /// model understood by the local parser.
    /// </summary>
    public bool EnableCtrlClickNavigation { get; init; }

    /// <summary>
    /// When <see langword="true"/>, this language is the preferred (default) highlighter
    /// for its declared extensions inside the owning project.
    /// Only one definition per extension should be marked default; the registry uses
    /// <see cref="LanguageRegistry.SetProjectDefault"/> to enforce this constraint.
    /// </summary>
    public bool IsDefault { get; init; }

    /// <summary>
    /// IDs of languages whose rules are prepended as a base layer.
    /// Resolved in <see cref="LanguageRegistry.ResolveIncludes"/> after full registration.
    /// </summary>
    public IReadOnlyList<string> Includes { get; init; } = [];

    /// <summary>
    /// Editor affinity inherited from "preferredEditor" in the parent .whfmt.
    /// "code-editor" | "text-editor" | null.
    /// </summary>
    public string? EditorHint { get; init; }

    /// <summary>
    /// Data-driven folding configuration deserialized from "foldingRules" in the .whfmt.
    /// Null = no explicit rules → CodeEditor uses its built-in default strategy.
    /// </summary>
    public FoldingRules? FoldingRules { get; init; }

    /// <summary>
    /// Breakpoint placement rules for this language.
    /// Null = no restrictions (all lines are valid breakpoint targets).
    /// </summary>
    public BreakpointRules? BreakpointRules { get; init; }
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

    /// <summary>Short description shown in the SmartComplete completion list.</summary>
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
    Attribute,
    ControlFlow
}

/// <summary>Selects the folding algorithm used by the CodeEditor gutter.</summary>
public enum FoldingStrategyKind
{
    None,
    Brace,
    Indent
}

// ==========================================================
// Project: WpfHexEditor.ProjectSystem
// File: FoldingRules.cs (embedded in LanguageDefinition.cs)
// Description: Data-driven folding configuration for a language.
// ==========================================================

/// <summary>
/// Describes how the CodeEditor should build fold regions for a given language.
/// All properties are optional; null/empty disables the corresponding strategy.
/// </summary>
public sealed record FoldingRules
{
    // ── Pattern-based (C#, JS, JSON, Rust, …) ─────────────────────────────
    public IReadOnlyList<string> StartPatterns { get; init; } = [];
    public IReadOnlyList<string> EndPatterns   { get; init; } = [];

    // ── Named-region directives (C#: #region / #endregion) ────────────────
    public string? NamedRegionStartPattern { get; init; }
    public string? NamedRegionEndPattern   { get; init; }

    // ── Indentation-based (Python, YAML, CoffeeScript, …) ─────────────────
    public bool    IndentBased       { get; init; }
    public string? BlockStartPattern { get; init; }
    public int     IndentTabWidth    { get; init; } = 4;

    // ── Tag-based (HTML, XML, XAML) ────────────────────────────────────────
    public bool                  TagBased           { get; init; }
    public IReadOnlyList<string> SelfClosingTags    { get; init; } = [];

    /// <summary>
    /// When true, <see cref="TagFoldingStrategy"/> merges continuation lines
    /// (attributes on separate lines before the closing &gt;) into a single virtual
    /// line before running the tag-matching regex. Required for XAML and HTML.
    /// </summary>
    public bool MultilineTagSupport { get; init; }

    // ── Heading-based (Markdown) ───────────────────────────────────────────
    public bool HeadingBased    { get; init; }
    public int  MinHeadingLevel { get; init; } = 2;

    // ── End-of-block hover hint ────────────────────────────────────────────
    /// <summary>
    /// Per-language configuration for the end-of-block hover hint popup.
    /// Null means "use defaults" (hint enabled for all region kinds).
    /// </summary>
    public EndOfBlockHintSettings? EndOfBlockHint { get; init; }
}

// ==========================================================
// Project: WpfHexEditor.ProjectSystem
// File: EndOfBlockHintSettings.cs (embedded in LanguageDefinition.cs)
// Description: Per-language configuration for the end-of-block hover hint.
// ==========================================================

// ==========================================================
// File: BreakpointRules.cs (embedded in LanguageDefinition.cs)
// Description: Per-language breakpoint placement rules.
// ==========================================================

/// <summary>
/// Controls where breakpoints may be placed in this language.
/// </summary>
public sealed record BreakpointRules
{
    /// <summary>
    /// Regex patterns applied to each line's raw text.
    /// A line matching ANY pattern is considered non-executable and
    /// may not receive a breakpoint (e.g. comment-only, blank, bare brace).
    /// An empty list means all lines are valid targets.
    /// </summary>
    public IReadOnlyList<string> NonExecutablePatterns { get; init; } = [];
}

/// <summary>
/// Controls the end-of-block hover hint for a language.
/// All booleans default to true (opt-in per language).
/// </summary>
public sealed record EndOfBlockHintSettings(
    bool IsEnabled        = true,
    bool ShowLineNumber   = true,
    bool ShowLineCount    = true,
    int  MaxContextLines  = 3,
    bool TriggerBrace     = true,
    bool TriggerDirective = true);
