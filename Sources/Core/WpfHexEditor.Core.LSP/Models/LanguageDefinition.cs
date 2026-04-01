// ==========================================================
// Project: WpfHexEditor.Core.LSP
// File: Models/LanguageDefinition.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Rich language metadata model parsed from a .whlang file.
//     Consumed by LanguageDefinitionManager, Lexer, FoldingEngine,
//     SmartComplete, and SnippetManager.
// ==========================================================

namespace WpfHexEditor.Core.LSP.Models;

/// <summary>Defines a single tokenisation rule in a language definition.</summary>
/// <param name="Type">Rule type string from .whlang (e.g. "Keyword", "String", "Comment").</param>
/// <param name="Pattern">Regex pattern to match tokens of this type.</param>
/// <param name="ColorKey">Theme token key (e.g. "TE_Keyword") used by the renderer.</param>
/// <param name="IsBold">Whether tokens of this type are bold.</param>
/// <param name="IsItalic">Whether tokens of this type are italic.</param>
public sealed record LanguageRule(
    string Type,
    string Pattern,
    string ColorKey,
    bool   IsBold   = false,
    bool   IsItalic = false);

/// <summary>
/// Rich metadata for a language loaded from a <c>.whlang</c> definition file.
/// </summary>
public sealed class LanguageDefinition
{
    // -- Identity -----------------------------------------------------------

    /// <summary>Unique identifier for this language (lower-case, e.g. <c>"csharp"</c>).</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Human-readable name (e.g. <c>"C#"</c>).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Logical category (e.g. <c>"CLike"</c>, <c>"Script"</c>).</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>Source priority (BuiltIn / Imported / UserCreated).</summary>
    public LanguagePriority Priority { get; init; } = LanguagePriority.BuiltIn;

    // -- File associations --------------------------------------------------

    /// <summary>File extensions mapped to this language (e.g. <c>[".cs", ".csx"]</c>).</summary>
    public IReadOnlyList<string> Extensions { get; init; } = [];

    // -- Comment tokens -----------------------------------------------------

    /// <summary>Single-line comment prefix (e.g. <c>"//"</c>). Null if not applicable.</summary>
    public string? LineComment { get; init; }

    /// <summary>Block comment start token (e.g. <c>"/*"</c>).</summary>
    public string? BlockCommentStart { get; init; }

    /// <summary>Block comment end token (e.g. <c>"*/"</c>).</summary>
    public string? BlockCommentEnd { get; init; }

    // -- Tokenisation rules -------------------------------------------------

    /// <summary>Ordered list of tokenisation rules parsed from the <c>rules</c> array.</summary>
    public IReadOnlyList<LanguageRule> Rules { get; init; } = [];

    // -- SmartComplete hints -------------------------------------------------

    /// <summary>Keyword list extracted from <c>Keyword</c>-type rules (for quick completion).</summary>
    public IReadOnlyList<string> Keywords { get; init; } = [];

    /// <summary>Operator characters (used for auto-insertion pairing).</summary>
    public IReadOnlyList<string> Operators { get; init; } = [];

    // -- Folding hints -------------------------------------------------------

    /// <summary>Open brace token (e.g. <c>"{"</c>) used by BraceFoldingStrategy.</summary>
    public string? FoldingOpen { get; init; } = "{";

    /// <summary>Close brace token (e.g. <c>"}"</c>) used by BraceFoldingStrategy.</summary>
    public string? FoldingClose { get; init; } = "}";

    /// <summary>
    /// When <c>true</c> the language uses indent-based folding (Python-style).
    /// </summary>
    public bool UseIndentFolding { get; init; }

    // -- Snippet templates --------------------------------------------------

    /// <summary>Built-in snippet triggers and bodies from the .whlang definition.</summary>
    public IReadOnlyList<(string Trigger, string Body, string Description)> SnippetTemplates { get; init; } = [];

    // -- Raw source info ----------------------------------------------------

    /// <summary>Manifest resource key or file path this definition was loaded from.</summary>
    public string SourceKey { get; init; } = string.Empty;
}
