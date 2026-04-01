//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.TextEditor.Highlighting;

/// <summary>
/// Language syntax definition loaded from a <c>.whlang</c> file.
/// Describes the display name, associated file extensions and an ordered list
/// of <see cref="SyntaxRule"/>s used by <see cref="RegexSyntaxHighlighter"/>.
/// </summary>
public sealed class SyntaxDefinition
{
    /// <summary>
    /// Human-readable language name (e.g. "Assembly x86/x64").
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Version of this syntax definition file (e.g. "1.0").
    /// </summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// Author or team that created this definition (e.g. "WpfHexEditor Team").
    /// </summary>
    public string Author { get; init; } = string.Empty;

    /// <summary>
    /// Short description of the language or file format.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Language category (e.g. "Assembly", "C-like", "Scripting", "Data", "Misc").
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Technical references: specification documents and web links.
    /// </summary>
    public SyntaxReferences References { get; init; } = new();

    /// <summary>
    /// File extensions this definition applies to (lower-case, with dot, e.g. ".asm").
    /// </summary>
    public IReadOnlyList<string> Extensions { get; init; } = [];

    /// <summary>
    /// Single-line comment prefix, or <see langword="null"/> / empty if none.
    /// </summary>
    public string? LineComment { get; init; }

    /// <summary>
    /// Block comment opening token (e.g. "/*"), or <see langword="null"/> if none.
    /// </summary>
    public string? BlockCommentStart { get; init; }

    /// <summary>
    /// Block comment closing token (e.g. "*/"), or <see langword="null"/> if none.
    /// </summary>
    public string? BlockCommentEnd { get; init; }

    /// <summary>
    /// Ordered list of syntax rules (evaluated top-to-bottom per line).
    /// </summary>
    public IReadOnlyList<SyntaxRule> Rules { get; init; } = [];

    /// <summary>
    /// Language injection map for fenced code blocks.
    /// Non-empty when the base language embeds other languages (e.g. Markdown fences).
    /// </summary>
    public IReadOnlyList<EmbeddedLanguageEntry> EmbeddedLanguages { get; init; } = [];

    /// <summary>
    /// Internal resource key used to identify where this definition was loaded from.
    /// </summary>
    internal string SourceKey { get; init; } = string.Empty;
}
