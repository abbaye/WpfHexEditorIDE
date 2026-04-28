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

    /// <summary>
    /// Column ruler positions (e.g. [80, 120]) from whfmt "columnRulers".
    /// Null = inherit from AppSettings.DefaultColumnRulers.
    /// </summary>
    public IReadOnlyList<int>? ColumnRulers { get; init; }

    /// <summary>
    /// Bracket pair definitions from whfmt "bracketPairs".
    /// When non-null, enables multi-level bracket colorization with CE_Bracket_1/2/3/4.
    /// Null = single-color fallback via CE_Bracket (no depth tracking).
    /// </summary>
    public IReadOnlyList<(char Open, char Close)>? BracketPairs { get; init; }

    /// <summary>
    /// Language-specific formatting rules from whfmt "formattingRules".
    /// Null = inherit AppSettings defaults (indentSize=4, useTabs=false).
    /// </summary>
    public FormattingRules? FormattingRules { get; init; }

    /// <summary>
    /// Pre-compiled color literal detection patterns from whfmt "colorLiteralPatterns".
    /// Null = no color swatch preview for this language.
    /// </summary>
    public IReadOnlyList<System.Text.RegularExpressions.Regex>? ColorLiteralPatterns { get; init; }

    // ── IDE Metadata (whfmt-driven) ──────────────────────────────────────────

    /// <summary>
    /// When true, this language represents a programming source file (C#, JS, Python, etc.).
    /// Drives Solution Explorer expansion, Class Diagram availability, etc.
    /// Sourced from "ideMetadata.isSourceFile" in the .whfmt syntaxDefinition block.
    /// </summary>
    public bool IsSourceFile { get; init; }

    /// <summary>
    /// When true, this language represents a structured data file (JSON, XML, YAML, etc.)
    /// that benefits from semantic diff mode.
    /// Sourced from "ideMetadata.isStructuredDataFile" in the .whfmt syntaxDefinition block.
    /// </summary>
    public bool IsStructuredDataFile { get; init; }

    /// <summary>
    /// When true, this language/format represents a solution file (.sln, .slnx, .whsln, etc.)
    /// Sourced from "ideMetadata.isSolutionFile" in the .whfmt syntaxDefinition block.
    /// </summary>
    public bool IsSolutionFile { get; init; }

    /// <summary>
    /// When true, this language/format represents a project file (.csproj, .vbproj, etc.)
    /// Sourced from "ideMetadata.isProjectFile" in the .whfmt syntaxDefinition block.
    /// </summary>
    public bool IsProjectFile { get; init; }

    /// <summary>
    /// When true, the Class Diagram plugin can analyze files of this language.
    /// Sourced from "ideMetadata.supportsClassDiagram" in the .whfmt syntaxDefinition block.
    /// </summary>
    public bool SupportsClassDiagram { get; init; }

    /// <summary>
    /// When true, the Source Outline provider can produce a document structure for this language.
    /// Sourced from "ideMetadata.supportsSourceOutline" in the .whfmt syntaxDefinition block.
    /// </summary>
    public bool SupportsSourceOutline { get; init; }

    /// <summary>
    /// When true, this language can be used as a project language in the New Project dialog.
    /// Sourced from "ideMetadata.isProjectLanguage" in the .whfmt syntaxDefinition block.
    /// </summary>
    public bool IsProjectLanguage { get; init; }

    /// <summary>
    /// VS-like hex color string for IDE language badges (e.g. "#4FC1FF" for C#).
    /// Sourced from "ideMetadata.languageColor" in the .whfmt syntaxDefinition block.
    /// Null = fall back to a generic teal (#4EC9B0).
    /// </summary>
    public string? LanguageColor { get; init; }

    /// <summary>
    /// Common alternate identifiers for this language (e.g. ["c#","cs"] for csharp).
    /// Used by <see cref="LanguageRegistry.FindByAlias"/> and ISyntaxColoringService.ResolveLanguageId
    /// to resolve fenced code block aliases without a static dictionary in C# code.
    /// Sourced from "ideMetadata.aliases" in the .whfmt syntaxDefinition block.
    /// </summary>
    public IReadOnlyList<string> Aliases { get; init; } = [];

    /// <summary>
    /// Segoe MDL2 icon glyph for this language, used in Archive Explorer and similar icon-based UIs.
    /// Sourced from "ideMetadata.iconGlyph" in the .whfmt syntaxDefinition block.
    /// Null = use the generic document icon (\uE8A5).
    /// </summary>
    public string? IconGlyph { get; init; }

    /// <summary>
    /// Preferred diff algorithm for files of this language.
    /// "semantic" | "text" | "binary".
    /// Sourced from "ideMetadata.diffMode" in the .whfmt syntaxDefinition block.
    /// Null = auto-detect via content sniffing.
    /// </summary>
    public string? IdeDiffMode { get; init; }

    /// <summary>
    /// Language-specific prefix for diagnostic error codes shown in the Error List
    /// (e.g. "CS" for C#, "PY" for Python, "JSON" for JSON).
    /// Null = use raw code as produced by the validator.
    /// Sourced from "diagnosticPrefix" in the whfmt syntaxDefinition block.
    /// </summary>
    public string? DiagnosticPrefix { get; init; }

    /// <summary>
    /// Script globals injected into the execution environment for this language.
    /// Populated from the "scriptGlobals" block in the .whfmt syntaxDefinition.
    /// Empty for non-script languages.
    /// Used by <see cref="WpfHexEditor.Editor.CodeEditor.Providers.ScriptGlobalsCompletionProvider"/>
    /// to provide context-aware completions for script globals and their members.
    /// </summary>
    public IReadOnlyList<ScriptGlobalEntry> ScriptGlobals { get; init; } = [];

    // ── Formatting preview (whfmt-driven) ───────────────────────────────────

    /// <summary>
    /// A short multi-line code snippet representative of this language's syntax.
    /// Used as the live preview source in the Formatting options page.
    /// Sourced from "previewSnippet" in the whfmt syntaxDefinition block.
    /// Null = the preview panel will synthesise a fallback from <see cref="PreviewSamples"/>.
    /// </summary>
    public string? PreviewSnippet { get; init; }

    /// <summary>
    /// Per-rule before/after micro-snippets for the Formatting options page tooltips.
    /// Key = formatting rule name (e.g. "spaceAfterKeywords").
    /// Sourced from "previewSamples" in the whfmt syntaxDefinition block.
    /// Rules absent from the dictionary → tooltip disabled for that rule in this language.
    /// </summary>
    public IReadOnlyDictionary<string, FormattingPreviewSample> PreviewSamples { get; init; }
        = new Dictionary<string, FormattingPreviewSample>();
}

// ==========================================================
// File: FormattingPreviewSample
//       (embedded in LanguageDefinition.cs)
// Description: Before/after micro-snippet for one formatting rule.
// ==========================================================

/// <summary>
/// A pair of before/after code fragments illustrating the effect of a single
/// formatting rule. Sourced from "previewSamples" in the .whfmt file.
/// Consumed by <c>FormattingRuleTooltip</c> in the Formatting options page.
/// </summary>
public sealed record FormattingPreviewSample
{
    /// <summary>Code fragment showing how the rule transforms the source (unformatted state).</summary>
    public string Before { get; init; } = string.Empty;

    /// <summary>Code fragment showing the result after the rule is applied.</summary>
    public string After  { get; init; } = string.Empty;
}

// ==========================================================
// File: ScriptGlobalEntry / ScriptMemberEntry
//       (embedded in LanguageDefinition.cs)
// Description: Script global + member metadata for SmartComplete.
// ==========================================================

/// <summary>
/// A member of a script global (property, method, or field).
/// </summary>
public sealed record ScriptMemberEntry(
    string Name,
    string Type,
    string Kind,           // "method" | "property" | "field"
    string Documentation);

/// <summary>
/// A top-level global injected into the script execution environment.
/// Listed in the "scriptGlobals" block of a .whfmt syntaxDefinition.
/// </summary>
public sealed record ScriptGlobalEntry(
    string                         Name,
    string                         Type,
    string                         Documentation,
    IReadOnlyList<ScriptMemberEntry> Members);

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

    // ── Block comment delimiters (used to skip { } inside /* ... */) ───────
    /// <summary>Opening token for block comments (e.g. "/*"). Null = no block comments.</summary>
    public string? BlockCommentStart { get; init; }
    /// <summary>Closing token for block comments (e.g. "*/"). Null = no block comments.</summary>
    public string? BlockCommentEnd   { get; init; }

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

    /// <summary>
    /// Regex patterns applied to each line's raw text.
    /// If a line matches ANY pattern, its statement is considered to continue
    /// on the next line — breakpoint/execution highlights extend forward.
    /// An empty list means every line is treated as a single-line statement.
    /// </summary>
    public IReadOnlyList<string> StatementContinuationPatterns { get; init; } = [];

    /// <summary>
    /// Maximum number of lines to scan forward when resolving multi-line
    /// statement extent.  Prevents runaway scanning.  Default = 20.
    /// </summary>
    public int MaxStatementScanLines { get; init; } = 20;

    /// <summary>
    /// When true, breakpoint/execution highlights extend to cover the enclosing
    /// block scope (e.g., from <c>var x = new Foo</c> down to <c>};</c>) by
    /// consulting folding regions whose StartLine falls within the statement
    /// continuation range.  Default = true.
    /// </summary>
    public bool BlockScopeHighlight { get; init; } = true;
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

// ==========================================================
// File: FormattingRules.cs (embedded in LanguageDefinition.cs)
// Description: Per-language code formatting configuration from whfmt.
// ==========================================================

/// <summary>
/// Structural formatting strategy for the fallback formatter.
/// Derived automatically from <c>foldingRules.tagBased</c> in the whfmt file.
/// </summary>
public enum FormatterStrategy
{
    /// <summary>Brace-counted re-indentation (C, C#, Java, JS…). Default.</summary>
    Brace,
    /// <summary>Tag-aware re-indentation for XML/XAML/HTML markup languages.</summary>
    Xml,
}

/// <summary>Brace placement style for languages with C-style blocks.</summary>
public enum BraceStyle
{
    /// <summary>Opening brace on its own line (Allman / BSD style).</summary>
    Allman,
    /// <summary>Opening brace at the end of the previous line (K&amp;R / 1TBS style).</summary>
    KR,
    /// <summary>Opening brace at end of line for functions, own line for control flow (Stroustrup).</summary>
    Stroustrup,
}

/// <summary>Preferred quote character for string literals.</summary>
public enum QuoteStyle { Double, Single, Backtick }

/// <summary>Line ending normalisation mode.</summary>
public enum LineEndingStyle { Auto, LF, CRLF }

/// <summary>Trailing comma insertion mode (JS/TS/JSON).</summary>
public enum TrailingCommaStyle { None, ES5, All }

/// <summary>
/// Language-specific formatting preferences read from whfmt "formattingRules".
/// Used by <c>CodeFormattingService</c> as fallback when no LSP formatter is available.
/// All properties can be overridden by <c>CodeEditorOptions</c> at the user level.
/// </summary>
public sealed record FormattingRules
{
    // ── Whitespace ──────────────────────────────────────────────────────────

    /// <summary>Number of spaces per indent level. Default = 4.</summary>
    public int IndentSize { get; init; } = 4;

    /// <summary>When true, indent uses tab characters instead of spaces.</summary>
    public bool UseTabs { get; init; }

    /// <summary>Remove trailing whitespace on each line. Default = true.</summary>
    public bool TrimTrailingWhitespace { get; init; } = true;

    /// <summary>Ensure file ends with exactly one newline. Default = true.</summary>
    public bool InsertFinalNewline { get; init; } = true;

    /// <summary>Line ending normalisation. Default = Auto (preserve existing).</summary>
    public LineEndingStyle LineEnding { get; init; } = LineEndingStyle.Auto;

    // ── Braces ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Brace placement style. <see langword="null"/> = no brace reformatting
    /// (e.g. Python, SQL, Markdown).
    /// </summary>
    public BraceStyle? BraceStyle { get; init; } = null;

    /// <summary>Insert a space between the keyword/identifier and the opening brace.</summary>
    public bool SpaceBeforeOpenBrace { get; init; } = true;

    /// <summary>Insert a space between a control-flow keyword and its opening paren: <c>if (</c> vs <c>if(</c>.</summary>
    public bool SpaceAfterKeywords { get; init; } = true;

    /// <summary>Insert spaces inside parentheses: <c>( x )</c> vs <c>(x)</c>.</summary>
    public bool SpaceInsideParens { get; init; }

    /// <summary>Indent <c>case</c>/<c>when</c> labels inside <c>switch</c>. Default = false.</summary>
    public bool IndentCaseLabels { get; init; }

    // ── Blank lines ─────────────────────────────────────────────────────────

    /// <summary>Maximum number of consecutive blank lines allowed. Default = 2.</summary>
    public int MaxConsecutiveBlankLines { get; init; } = 2;

    /// <summary>Ensure a blank line before method/function declarations.</summary>
    public bool BlankLineBeforeMethod { get; init; } = true;

    /// <summary>Ensure a blank line after the import/using block.</summary>
    public bool BlankLineAfterImports { get; init; } = true;

    // ── Spacing ─────────────────────────────────────────────────────────────

    /// <summary>Insert spaces around binary operators: <c>a + b</c> vs <c>a+b</c>.</summary>
    public bool SpaceAroundBinaryOperators { get; init; } = true;

    /// <summary>Insert a space after commas in argument/parameter lists.</summary>
    public bool SpaceAfterComma { get; init; } = true;

    // ── Imports ─────────────────────────────────────────────────────────────

    /// <summary>Sort import/using directives alphabetically on format.</summary>
    public bool OrganizeImports { get; init; }

    /// <summary>
    /// Separate <c>System.*</c> imports into their own group with a blank line
    /// (C#, Java, Kotlin). Ignored when <see cref="OrganizeImports"/> is false.
    /// </summary>
    public bool SeparateSystemImports { get; init; }

    // ── Language-specific ───────────────────────────────────────────────────

    /// <summary>Preferred quote style for string literals. <see langword="null"/> = no change.</summary>
    public QuoteStyle? QuoteStyle { get; init; } = null;

    /// <summary>Trailing comma insertion strategy for JS/TS/JSON.</summary>
    public TrailingCommaStyle TrailingCommas { get; init; } = TrailingCommaStyle.None;

    /// <summary>Maximum line length for diagnostics and optional wrapping. 0 = disabled.</summary>
    public int MaxLineLength { get; init; } = 120;

    /// <summary>Uppercase SQL reserved keywords (SELECT, FROM, WHERE…). Only relevant for SQL.</summary>
    public bool SqlKeywordsUppercase { get; init; }

    // ── XML / XAML-specific ─────────────────────────────────────────────────

    /// <summary>
    /// Indent level multiplier for attribute continuation lines in XML/XAML/HTML.
    /// Default = 2 (double-indent from the element's own level, matching VS XAML style).
    /// Only used when <see cref="FormatterStrategy"/> == <see cref="FormatterStrategy.Xml"/>.
    /// </summary>
    public int XmlAttributeIndentLevels { get; init; } = 2;

    /// <summary>
    /// When true, each XML/XAML attribute is placed on its own line
    /// (first attribute stays on the tag line; subsequent attributes are indented).
    /// Default = false. Only used when <see cref="FormatterStrategy"/> == <see cref="FormatterStrategy.Xml"/>.
    /// </summary>
    public bool XmlOneAttributePerLine { get; init; } = false;

    // ── Formatter strategy (auto-derived from foldingRules.tagBased) ────────

    /// <summary>
    /// Structural formatting strategy used by the fallback formatter.
    /// Set to <see cref="FormatterStrategy.Xml"/> for tag-based languages (XAML, XML, HTML).
    /// Derived from <c>foldingRules.tagBased</c> in the whfmt — no manual override needed.
    /// </summary>
    public FormatterStrategy FormatterStrategy { get; init; } = FormatterStrategy.Brace;

    /// <summary>
    /// Optional allow-list of formatting rule IDs supported by this language.
    /// When <see langword="null"/>, all formatting controls are enabled (backward-compatible default).
    /// When set, only the listed rule IDs are enabled in the Formatting options page.
    /// Rule IDs match the JSON keys in the whfmt <c>formattingRules</c> block
    /// (e.g. "spaceAfterKeywords", "xmlOneAttributePerLine").
    /// </summary>
    public IReadOnlyList<string>? SupportedRules { get; init; }

    // ── Pattern keywords (whfmt-driven, replaces hardcoded regexes) ─────────

    /// <summary>
    /// Keywords that trigger space-after-keyword formatting (e.g. <c>if(</c> → <c>if (</c>).
    /// Null = use <see cref="FormattingDefaults.KeywordParenKeywords"/>.
    /// </summary>
    public IReadOnlyList<string>? KeywordParenKeywords { get; init; }

    /// <summary>
    /// Binary operators that trigger space-around formatting (e.g. <c>a+b</c> → <c>a + b</c>).
    /// Null = use <see cref="FormattingDefaults.BinaryOperators"/>.
    /// </summary>
    public IReadOnlyList<string>? BinaryOperators { get; init; }

    /// <summary>
    /// Keywords that identify method/function declarations for blank-line-before-method.
    /// Null = use <see cref="FormattingDefaults.MethodDeclKeywords"/>.
    /// </summary>
    public IReadOnlyList<string>? MethodDeclKeywords { get; init; }

    /// <summary>
    /// Keywords that identify import/using directives for organisation and blank-line-after-imports.
    /// Null = use <see cref="FormattingDefaults.ImportKeywords"/>.
    /// </summary>
    public IReadOnlyList<string>? ImportKeywords { get; init; }

    /// <summary>
    /// SQL reserved keywords for uppercase normalisation.
    /// Null = use <see cref="FormattingDefaults.SqlKeywords"/>.
    /// </summary>
    public IReadOnlyList<string>? SqlKeywords { get; init; }

    // ── Keyword-based block delimiters ───────────────────────────────────────

    /// <summary>
    /// Words that open an indented block (e.g. "Then", "Do", "Sub", "Function",
    /// "Class", "def", "do"). A line whose <em>last non-whitespace word</em>
    /// matches one of these causes the <em>next</em> line to be indented one level.
    /// Null or empty = use brace-based indentation only.
    /// </summary>
    public IReadOnlyList<string> BlockOpenKeywords { get; init; } = [];

    /// <summary>
    /// Words that close an indented block (e.g. "End Sub", "End Class", "end",
    /// "End If", "End Select"). A line whose trimmed content starts with one of
    /// these is de-dented before being emitted.
    /// </summary>
    public IReadOnlyList<string> BlockCloseKeywords { get; init; } = [];

    // ── Override merge ──────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new <see cref="FormattingRules"/> by merging this instance with
    /// user-level overrides.  Non-null override values take precedence over the
    /// whfmt-sourced values stored in this record.
    /// </summary>
    public FormattingRules WithOverrides(FormattingOverrides? ov)
    {
        if (ov is null) return this;
        return this with
        {
            IndentSize                 = ov.IndentSize ?? IndentSize,
            UseTabs                    = ov.UseTabs ?? UseTabs,
            TrimTrailingWhitespace     = ov.TrimTrailingWhitespace ?? TrimTrailingWhitespace,
            InsertFinalNewline         = ov.InsertFinalNewline ?? InsertFinalNewline,
            BraceStyle                 = ov.BraceStyle ?? BraceStyle,
            SpaceAfterKeywords         = ov.SpaceAfterKeywords ?? SpaceAfterKeywords,
            SpaceAroundBinaryOperators = ov.SpaceAroundBinaryOperators ?? SpaceAroundBinaryOperators,
            SpaceAfterComma            = ov.SpaceAfterComma ?? SpaceAfterComma,
            IndentCaseLabels           = ov.IndentCaseLabels ?? IndentCaseLabels,
            OrganizeImports            = ov.OrganizeImports ?? OrganizeImports,
            MaxLineLength              = ov.MaxLineLength ?? MaxLineLength,
            XmlAttributeIndentLevels   = ov.XmlAttributeIndentLevels ?? XmlAttributeIndentLevels,
            XmlOneAttributePerLine     = ov.XmlOneAttributePerLine   ?? XmlOneAttributePerLine,
            KeywordParenKeywords       = ov.KeywordParenKeywords ?? KeywordParenKeywords,
            BinaryOperators            = ov.BinaryOperators ?? BinaryOperators,
            MethodDeclKeywords         = ov.MethodDeclKeywords ?? MethodDeclKeywords,
            ImportKeywords             = ov.ImportKeywords ?? ImportKeywords,
            SqlKeywords                = ov.SqlKeywords ?? SqlKeywords,
        };
    }
}

/// <summary>
/// User-level formatting overrides.  <see langword="null"/> values mean
/// "use the whfmt language default".
/// </summary>
public sealed class FormattingOverrides
{
    public int?        IndentSize                 { get; set; }
    public bool?       UseTabs                    { get; set; }
    public bool?       TrimTrailingWhitespace     { get; set; }
    public bool?       InsertFinalNewline         { get; set; }
    public BraceStyle? BraceStyle                 { get; set; }
    public bool?       SpaceAfterKeywords         { get; set; }
    public bool?       SpaceAroundBinaryOperators { get; set; }
    public bool?       SpaceAfterComma            { get; set; }
    public bool?       IndentCaseLabels           { get; set; }
    public bool?       OrganizeImports            { get; set; }
    public int?        MaxLineLength              { get; set; }
    public int?        XmlAttributeIndentLevels   { get; set; }
    public bool?       XmlOneAttributePerLine     { get; set; }
    public IReadOnlyList<string>? KeywordParenKeywords { get; set; }
    public IReadOnlyList<string>? BinaryOperators      { get; set; }
    public IReadOnlyList<string>? MethodDeclKeywords   { get; set; }
    public IReadOnlyList<string>? ImportKeywords       { get; set; }
    public IReadOnlyList<string>? SqlKeywords          { get; set; }
}

/// <summary>
/// Default keyword lists used by <see cref="StructuralFormatter"/> when the
/// whfmt language definition does not provide per-language overrides.
/// </summary>
public static class FormattingDefaults
{
    public static readonly IReadOnlyList<string> KeywordParenKeywords =
        ["if", "for", "foreach", "while", "switch", "catch", "using", "lock", "when", "elif", "except"];

    public static readonly IReadOnlyList<string> BinaryOperators =
        ["+", "-", "*", "/", "%", "==", "!=", "<=", ">=", "&&", "||", "<<", ">>", "??"];

    public static readonly IReadOnlyList<string> MethodDeclKeywords =
        ["public", "private", "protected", "internal", "static", "async", "override",
         "virtual", "abstract", "sealed", "partial", "def", "func", "fn", "fun", "sub", "function"];

    public static readonly IReadOnlyList<string> ImportKeywords =
        ["using", "imports", "import", "from", "require", "include", "#include"];

    public static readonly IReadOnlyList<string> SqlKeywords =
        ["SELECT", "FROM", "WHERE", "JOIN", "LEFT", "RIGHT", "INNER", "OUTER", "FULL", "CROSS",
         "ON", "AND", "OR", "NOT", "IN", "EXISTS", "BETWEEN", "LIKE", "ORDER", "BY", "GROUP",
         "HAVING", "UNION", "ALL", "INSERT", "INTO", "VALUES", "UPDATE", "SET", "DELETE",
         "CREATE", "ALTER", "DROP", "TABLE", "INDEX", "VIEW", "AS", "IS", "NULL", "DISTINCT",
         "TOP", "LIMIT", "OFFSET", "ASC", "DESC", "CASE", "WHEN", "THEN", "ELSE", "END",
         "COUNT", "SUM", "AVG", "MIN", "MAX", "CAST", "CONVERT", "COALESCE", "ISNULL"];
}
