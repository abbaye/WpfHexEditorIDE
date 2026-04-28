// ==========================================================
// Project: WpfHexEditor.ProjectSystem
// File: Languages/LanguageDefinitionSerializer.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-05
// Description:
//     JSON deserialiser for .whlang language definition files.
//     .whlang files are standard UTF-8 JSON documents whose root object
//     maps directly to LanguageDefinitionDto (an internal DTO class).
//     The DTO is then projected to the immutable LanguageDefinition model.
//
// Architecture Notes:
//     Adapter Pattern — converts JSON DTO → domain model.
//     Uses System.Text.Json with camelCase naming policy for compact files.
// ==========================================================

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace WpfHexEditor.Core.ProjectSystem.Languages;

/// <summary>
/// Reads a <see cref="LanguageDefinition"/> from a <c>.whlang</c> JSON file.
/// </summary>
public static class LanguageDefinitionSerializer
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    /// <summary>
    /// Maps all .whlang "type" strings (including language-specific aliases) to the canonical
    /// <see cref="SyntaxTokenKind"/> enum value used by the rendering pipeline.
    /// Any unrecognised type name falls back to <see cref="SyntaxTokenKind.Default"/>.
    /// </summary>
    private static readonly Dictionary<string, SyntaxTokenKind> s_typeMap =
        new(StringComparer.OrdinalIgnoreCase)
    {
        // ── Standard names (direct match) ────────────────────────────────
        { "Keyword",     SyntaxTokenKind.Keyword     },
        { "ControlFlow", SyntaxTokenKind.ControlFlow },
        { "Comment",    SyntaxTokenKind.Comment    },
        { "String",     SyntaxTokenKind.String     },
        { "Number",     SyntaxTokenKind.Number     },
        { "Identifier", SyntaxTokenKind.Identifier },
        { "Operator",   SyntaxTokenKind.Operator   },
        { "Bracket",    SyntaxTokenKind.Bracket    },
        { "Type",       SyntaxTokenKind.Type       },
        { "UserType",   SyntaxTokenKind.Type       },   // PascalCase heuristic (C#, VB.NET)
        { "Attribute",  SyntaxTokenKind.Attribute  },

        // ── XML / HTML / XAML ─────────────────────────────────────────────
        { "Tag",        SyntaxTokenKind.Keyword    },   // element name
        { "AttrName",   SyntaxTokenKind.Attribute  },   // attribute name
        { "AttrValue",  SyntaxTokenKind.String     },   // attribute value
        { "TagBracket", SyntaxTokenKind.Operator   },   // < >
        { "Entity",     SyntaxTokenKind.Number     },   // &amp; &#x26; …
        { "DocType",    SyntaxTokenKind.Keyword    },   // <!DOCTYPE …>
        { "ProcInstr",  SyntaxTokenKind.Keyword    },   // <?xml …?>
        { "CData",      SyntaxTokenKind.String     },   // <![CDATA[…]]>

        // ── Comments (multi-format) ───────────────────────────────────────
        { "BlockComment", SyntaxTokenKind.Comment  },

        // ── Variables & identifiers ───────────────────────────────────────
        { "Variable",   SyntaxTokenKind.Identifier },   // $var, @var, …
        { "Symbol",     SyntaxTokenKind.Attribute  },   // Ruby :symbol
        { "Key",        SyntaxTokenKind.Attribute  },   // JSON / YAML / INI key
        { "Field",         SyntaxTokenKind.Attribute  },   // _underscore private fields (C#, VB.NET)
        { "NamespaceDecl", SyntaxTokenKind.Default    },   // name after 'namespace' / 'Namespace' — left uncolored
        { "UsingRef",      SyntaxTokenKind.Default    },   // name after 'using' / 'Imports' — left uncolored
        { "RegionKeyword", SyntaxTokenKind.Keyword    },   // #region / #endregion directive word
        { "RegionName", SyntaxTokenKind.Attribute  },   // label text after #region

        // ── Markup / scripting keywords ───────────────────────────────────
        { "Cmdlet",     SyntaxTokenKind.Keyword    },   // PowerShell / shell built-ins
        { "BatchLabel", SyntaxTokenKind.Identifier },   // shell batch labels
        { "Boolean",    SyntaxTokenKind.Keyword    },   // true / false / yes / no
        { "Preprocessor",SyntaxTokenKind.Keyword   },   // #include / #define
        { "Macro",      SyntaxTokenKind.Keyword    },   // Rust macro!
        { "Annotation", SyntaxTokenKind.Attribute  },   // @Override, @dataclass
        { "Decorator",  SyntaxTokenKind.Attribute  },   // @decorator

        // ── Assembly ──────────────────────────────────────────────────────
        { "Register",   SyntaxTokenKind.Type       },   // eax, rax, r0 …
        { "Directive",  SyntaxTokenKind.Keyword    },   // .section, .data …
        { "Label",      SyntaxTokenKind.Identifier },   // my_func:
        { "Condition",  SyntaxTokenKind.Keyword    },   // ARM condition codes (EQ, NE…)

        // ── Data formats ──────────────────────────────────────────────────
        { "Section",     SyntaxTokenKind.Type       },  // [INI section], YAML section
        { "Value",       SyntaxTokenKind.String     },  // INI value
        { "Anchor",      SyntaxTokenKind.Attribute  },  // YAML &anchor / *alias
        // Note: "Tag" is already mapped to Keyword (XML element names) above.
        // YAML !!tag annotations share the same string — Keyword is an acceptable fallback.
        { "DocumentMark",SyntaxTokenKind.Operator   },  // YAML --- / ...
        { "DataType",    SyntaxTokenKind.Type       },  // SQL INT, VARCHAR …
        { "Function",    SyntaxTokenKind.Identifier },  // SQL COUNT(), SUM() …

        // ── Rust-specific ─────────────────────────────────────────────────
        { "Lifetime",   SyntaxTokenKind.Type       },   // 'a, 'static

        // ── Markdown ──────────────────────────────────────────────────────
        { "Heading",    SyntaxTokenKind.Keyword    },
        { "Bold",       SyntaxTokenKind.Keyword    },
        { "Italic",     SyntaxTokenKind.Comment    },
        { "InlineCode", SyntaxTokenKind.String     },
        { "CodeBlock",  SyntaxTokenKind.String     },
        { "Link",       SyntaxTokenKind.Attribute  },
        { "Image",      SyntaxTokenKind.Attribute  },
        { "ListItem",   SyntaxTokenKind.Identifier },
        { "BlockQuote", SyntaxTokenKind.Comment    },
        { "HRule",      SyntaxTokenKind.Operator   },
        { "Table",      SyntaxTokenKind.Type       },
        { "FrontMatter",SyntaxTokenKind.Keyword    },

        // ── Plain-text specials ───────────────────────────────────────────
        { "Url",        SyntaxTokenKind.Attribute  },
        { "Email",      SyntaxTokenKind.Attribute  },
    };

    /// <summary>
    /// Deserialises a <c>.whlang</c> file and returns the corresponding
    /// <see cref="LanguageDefinition"/>.
    /// </summary>
    /// <param name="filePath">Absolute path to a <c>.whlang</c> file.</param>
    /// <returns>The parsed language definition.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the file is malformed or missing required fields.</exception>
    public static LanguageDefinition Load(string filePath)
    {
        var json = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
        return Parse(json);
    }

    /// <summary>
    /// Parses a JSON string and returns the corresponding <see cref="LanguageDefinition"/>.
    /// </summary>
    public static LanguageDefinition Parse(string json)
    {
        var dto = JsonSerializer.Deserialize<LanguageDefinitionDto>(json, s_options)
            ?? throw new InvalidOperationException("Failed to deserialise language definition: JSON root is null.");

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new InvalidOperationException("Language definition is missing required field 'name'.");

        // Derive a stable Id from Name when the file omits the "id" field.
        // Most .whlang files do not include an "id" — lowercase-hyphenated Name is the fallback.
        var id = string.IsNullOrWhiteSpace(dto.Id)
            ? dto.Name!.ToLowerInvariant().Replace(' ', '-').Replace('/', '-')
            : dto.Id;

        return new LanguageDefinition
        {
            Id          = id,
            Name        = dto.Name,
            Extensions  = dto.Extensions ?? [],
            SyntaxRules = dto.SyntaxRules?.Select(r => new SyntaxRule
            {
                Pattern = r.Pattern ?? string.Empty,
                Kind    = MapKind(r.KindRaw)
            }).ToArray() ?? [],
            Snippets = dto.Snippets?.Select(s => new SnippetDefinition
            {
                Trigger     = s.Trigger ?? string.Empty,
                Body        = s.Body    ?? string.Empty,
                Description = s.Description ?? string.Empty
            }).ToArray() ?? [],
            FoldingStrategy          = dto.FoldingStrategy,
            LineCommentPrefix        = dto.LineCommentPrefix,
            BlockCommentStart        = dto.BlockCommentStart,
            BlockCommentEnd          = dto.BlockCommentEnd,
            EnableInlineHints        = dto.EnableInlineHints,
            EnableCtrlClickNavigation = dto.EnableCtrlClickNavigation,
            IsDefault                = dto.IsDefault,
            DiagnosticPrefix         = dto.DiagnosticPrefix,
        };
    }

    /// <summary>
    /// Parses a <c>syntaxDefinition</c> JSON block extracted from a <c>.whfmt</c> file
    /// and returns the corresponding <see cref="LanguageDefinition"/>.
    /// </summary>
    /// <param name="syntaxJson">JSON string of the syntaxDefinition sub-object.</param>
    /// <param name="formatName">Human-readable format name from the parent .whfmt root.</param>
    /// <param name="extensions">File extensions declared in the parent .whfmt root.</param>
    /// <param name="preferredEditor">Value of "preferredEditor" in the parent .whfmt root.</param>
    public static LanguageDefinition ParseSyntaxDefinitionBlock(
        string                  syntaxJson,
        string                  formatName,
        IReadOnlyList<string>   extensions,
        string?                 preferredEditor)
    {
        var dto = JsonSerializer.Deserialize<SyntaxDefinitionBlockDto>(syntaxJson, s_options)
            ?? throw new InvalidOperationException("Failed to deserialise syntaxDefinition block: JSON root is null.");

        // "id" inside the block takes precedence; fall back to the format name.
        var id = !string.IsNullOrWhiteSpace(dto.Id)
            ? dto.Id!
            : formatName.ToLowerInvariant().Replace(' ', '-').Replace('/', '-');

        // Extensions declared inside the block override the parent-level extensions.
        var resolvedExtensions = (dto.Extensions is { Length: > 0 })
            ? dto.Extensions
            : extensions;

        return new LanguageDefinition
        {
            Id          = id,
            Name        = !string.IsNullOrWhiteSpace(dto.Name) ? dto.Name! : formatName,
            Extensions  = resolvedExtensions,
            SyntaxRules = dto.SyntaxRules?.Select(r => new SyntaxRule
            {
                Pattern = r.Pattern ?? string.Empty,
                Kind    = MapKind(r.KindRaw)
            }).ToArray() ?? [],
            Snippets = dto.Snippets?.Select(s => new SnippetDefinition
            {
                Trigger     = s.Trigger ?? string.Empty,
                Body        = s.Body    ?? string.Empty,
                Description = s.Description ?? string.Empty
            }).ToArray() ?? [],
            FoldingStrategy           = dto.FoldingStrategy,
            LineCommentPrefix         = dto.LineCommentPrefix,
            BlockCommentStart         = dto.BlockCommentStart,
            BlockCommentEnd           = dto.BlockCommentEnd,
            EnableInlineHints         = dto.EnableInlineHints,
            EnableCtrlClickNavigation = dto.EnableCtrlClickNavigation,
            IsDefault                 = dto.IsDefault,
            Includes                  = dto.Includes ?? [],
            EditorHint                = preferredEditor,
            FoldingRules              = MapFoldingRules(dto.FoldingRules, dto.BlockCommentStart, dto.BlockCommentEnd),
            BreakpointRules           = MapBreakpointRules(dto.BreakpointRules),
            ColumnRulers              = dto.ColumnRulers,
            BracketPairs              = MapBracketPairs(dto.BracketPairs),
            FormattingRules           = MapFormattingRules(dto.FormattingRules, dto.FoldingRules?.TagBased ?? false),
            ColorLiteralPatterns      = MapColorLiteralPatterns(dto.ColorLiteralPatterns),
            DiagnosticPrefix          = dto.DiagnosticPrefix,
            ScriptGlobals             = MapScriptGlobals(dto.ScriptGlobals),
            PreviewSnippet            = dto.PreviewSnippet,
            PreviewSamples            = MapPreviewSamples(dto.PreviewSamples),
            IsSourceFile          = dto.IdeMetadata?.IsSourceFile         ?? false,
            IsStructuredDataFile  = dto.IdeMetadata?.IsStructuredDataFile ?? false,
            IsSolutionFile        = dto.IdeMetadata?.IsSolutionFile       ?? false,
            IsProjectFile         = dto.IdeMetadata?.IsProjectFile        ?? false,
            SupportsClassDiagram  = dto.IdeMetadata?.SupportsClassDiagram  ?? false,
            SupportsSourceOutline = dto.IdeMetadata?.SupportsSourceOutline ?? false,
            IsProjectLanguage     = dto.IdeMetadata?.IsProjectLanguage     ?? false,
            LanguageColor         = dto.IdeMetadata?.LanguageColor,
            Aliases               = (IReadOnlyList<string>?)dto.IdeMetadata?.Aliases ?? [],
            IconGlyph             = dto.IdeMetadata?.IconGlyph,
            IdeDiffMode           = dto.IdeMetadata?.DiffMode,
        };
    }

    // -- Helpers --------------------------------------------------------------

    /// <summary>
    /// Resolves a raw "type" string from a .whlang file to the canonical <see cref="SyntaxTokenKind"/>.
    /// Falls back to <see cref="SyntaxTokenKind.Default"/> for unrecognised names.
    /// </summary>
    private static SyntaxTokenKind MapKind(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return SyntaxTokenKind.Default;
        return s_typeMap.TryGetValue(raw, out var kind) ? kind : SyntaxTokenKind.Default;
    }

    /// <summary>
    /// Maps a <see cref="FoldingRulesDto"/> to the <see cref="FoldingRules"/> domain model.
    /// Returns null when <paramref name="dto"/> is null (no explicit folding rules declared).
    /// <paramref name="blockCommentStart"/> and <paramref name="blockCommentEnd"/> come from
    /// the parent syntax block (root-level whfmt fields) so that PatternFoldingStrategy can
    /// skip brace tokens that appear inside multi-line block comments.
    /// </summary>
    private static FoldingRules? MapFoldingRules(FoldingRulesDto? dto,
                                                  string? blockCommentStart,
                                                  string? blockCommentEnd)
    {
        if (dto is null) return null;

        return new FoldingRules
        {
            StartPatterns           = dto.StartPatterns            ?? [],
            EndPatterns             = dto.EndPatterns              ?? [],
            BlockCommentStart       = blockCommentStart,
            BlockCommentEnd         = blockCommentEnd,
            NamedRegionStartPattern = dto.NamedRegionStart,
            NamedRegionEndPattern   = dto.NamedRegionEnd,
            IndentBased             = dto.IndentBased,
            BlockStartPattern       = dto.BlockStartPattern,
            IndentTabWidth          = dto.IndentTabWidth,
            TagBased                = dto.TagBased,
            SelfClosingTags         = dto.SelfClosingTags          ?? [],
            MultilineTagSupport     = dto.MultilineTagSupport,
            HeadingBased            = dto.HeadingBased,
            MinHeadingLevel         = dto.MinHeadingLevel,
            EndOfBlockHint          = MapEndOfBlockHint(dto.EndOfBlockHint),
        };
    }

    private static BreakpointRules? MapBreakpointRules(BreakpointRulesDto? dto)
    {
        if (dto is null) return null;
        return new BreakpointRules
        {
            NonExecutablePatterns          = dto.NonExecutablePatterns ?? [],
            StatementContinuationPatterns  = dto.StatementContinuationPatterns ?? [],
            MaxStatementScanLines          = dto.MaxStatementScanLines ?? 20,
            BlockScopeHighlight            = dto.BlockScopeHighlight ?? true,
        };
    }

    private static EndOfBlockHintSettings? MapEndOfBlockHint(EndOfBlockHintDto? dto)
    {
        if (dto is null) return null;
        return new EndOfBlockHintSettings(
            IsEnabled        : dto.Enabled,
            ShowLineNumber   : dto.ShowLineNumber,
            ShowLineCount    : dto.ShowLineCount,
            MaxContextLines  : dto.MaxContextLines,
            TriggerBrace     : dto.TriggerBrace,
            TriggerDirective : dto.TriggerDirective);
    }

    // -- Internal DTO ---------------------------------------------------------

    private sealed class LanguageDefinitionDto
    {
        public string?               Id                { get; set; }
        public string?               Name              { get; set; }
        public string[]?             Extensions        { get; set; }
        // .whlang files use "rules" not "syntaxRules".
        [JsonPropertyName("rules")]
        public SyntaxRuleDto[]?      SyntaxRules       { get; set; }
        public SnippetDefinitionDto[]? Snippets         { get; set; }
        public FoldingStrategyKind   FoldingStrategy   { get; set; } = FoldingStrategyKind.Brace;
        public string?               LineCommentPrefix   { get; set; }
        public string?               BlockCommentStart   { get; set; }
        public string?               BlockCommentEnd     { get; set; }
        /// <summary>
        /// When true, the CodeEditor renders inline hints for this language.
        /// </summary>
        public bool EnableInlineHints { get; set; }

        /// <summary>
        /// When true, Ctrl+click go-to-definition is active for this language.
        /// </summary>
        public bool EnableCtrlClickNavigation { get; set; }

        /// <summary>
        /// When true, the registry will call SetProjectDefault() automatically for all
        /// extensions declared in the file, making this the preferred language.
        /// </summary>
        public bool IsDefault { get; set; }

        [JsonPropertyName("diagnosticPrefix")]
        public string? DiagnosticPrefix { get; set; }
    }

    private sealed class SyntaxRuleDto
    {
        public string? Pattern { get; set; }

        // .whlang files store the token kind under the field "type" using free-form strings
        // (e.g. "Keyword", "Tag", "AttrName") that do NOT necessarily match the enum names.
        // Deserialise as raw string and resolve via s_typeMap to support all aliases.
        [JsonPropertyName("type")]
        public string? KindRaw { get; set; }
    }

    private sealed class SnippetDefinitionDto
    {
        public string? Trigger     { get; set; }
        public string? Body        { get; set; }
        public string? Description { get; set; }
    }

    /// <summary>
    /// DTO for a "syntaxDefinition" block embedded inside a .whfmt file.
    /// Mirrors LanguageDefinitionDto with additional fields for .whfmt context.
    /// </summary>
    private sealed class SyntaxDefinitionBlockDto
    {
        public string?               Id                { get; set; }
        public string?               Name              { get; set; }
        public string[]?             Extensions        { get; set; }
        [JsonPropertyName("rules")]
        public SyntaxRuleDto[]?      SyntaxRules       { get; set; }
        public SnippetDefinitionDto[]? Snippets         { get; set; }
        public FoldingStrategyKind   FoldingStrategy   { get; set; } = FoldingStrategyKind.Brace;
        public string?               LineCommentPrefix  { get; set; }
        public string?               BlockCommentStart  { get; set; }
        public string?               BlockCommentEnd    { get; set; }
        public bool                  EnableInlineHints        { get; set; }
        public bool                  EnableCtrlClickNavigation { get; set; }
        public bool                  IsDefault                { get; set; }
        public List<string>?         Includes                 { get; set; }
        public FoldingRulesDto?      FoldingRules             { get; set; }

        [JsonPropertyName("breakpointRules")]
        public BreakpointRulesDto?   BreakpointRules          { get; set; }

        [JsonPropertyName("columnRulers")]
        public int[]?                ColumnRulers             { get; set; }

        [JsonPropertyName("bracketPairs")]
        public BracketPairDto[]?     BracketPairs             { get; set; }

        [JsonPropertyName("formattingRules")]
        public FormattingRulesDto?   FormattingRules          { get; set; }

        [JsonPropertyName("colorLiteralPatterns")]
        public string[]?             ColorLiteralPatterns     { get; set; }

        [JsonPropertyName("diagnosticPrefix")]
        public string?               DiagnosticPrefix         { get; set; }

        [JsonPropertyName("scriptGlobals")]
        public ScriptGlobalDto[]?    ScriptGlobals            { get; set; }

        [JsonPropertyName("previewSnippet")]
        public string?               PreviewSnippet           { get; set; }

        [JsonPropertyName("previewSamples")]
        public Dictionary<string, PreviewSampleDto>? PreviewSamples { get; set; }

        [JsonPropertyName("ideMetadata")]
        public IdeMetadataDto? IdeMetadata { get; set; }
    }

    private sealed class IdeMetadataDto
    {
        [JsonPropertyName("isSourceFile")]          public bool      IsSourceFile          { get; set; }
        [JsonPropertyName("isStructuredDataFile")]  public bool      IsStructuredDataFile  { get; set; }
        [JsonPropertyName("isSolutionFile")]        public bool      IsSolutionFile        { get; set; }
        [JsonPropertyName("isProjectFile")]         public bool      IsProjectFile         { get; set; }
        [JsonPropertyName("supportsClassDiagram")]  public bool      SupportsClassDiagram  { get; set; }
        [JsonPropertyName("supportsSourceOutline")] public bool      SupportsSourceOutline { get; set; }
        [JsonPropertyName("isProjectLanguage")]     public bool      IsProjectLanguage     { get; set; }
        [JsonPropertyName("languageColor")]         public string?   LanguageColor         { get; set; }
        [JsonPropertyName("aliases")]               public string[]? Aliases               { get; set; }
        [JsonPropertyName("iconGlyph")]             public string?   IconGlyph             { get; set; }
        [JsonPropertyName("diffMode")]              public string?   DiffMode              { get; set; }
    }

    private sealed class ScriptGlobalDto
    {
        [JsonPropertyName("name")]          public string?              Name          { get; set; }
        [JsonPropertyName("type")]          public string?              Type          { get; set; }
        [JsonPropertyName("documentation")] public string?              Documentation { get; set; }
        [JsonPropertyName("members")]       public ScriptMemberDto[]?   Members       { get; set; }
    }

    private sealed class ScriptMemberDto
    {
        [JsonPropertyName("name")]          public string? Name          { get; set; }
        [JsonPropertyName("type")]          public string? Type          { get; set; }
        [JsonPropertyName("kind")]          public string? Kind          { get; set; }
        [JsonPropertyName("documentation")] public string? Documentation { get; set; }
    }

    /// <summary>DTO for a single before/after formatting preview micro-snippet.</summary>
    private sealed class PreviewSampleDto
    {
        [JsonPropertyName("before")] public string? Before { get; set; }
        [JsonPropertyName("after")]  public string? After  { get; set; }
    }

    private sealed class FoldingRulesDto
    {
        [JsonPropertyName("startPatterns")]     public List<string>? StartPatterns     { get; set; }
        [JsonPropertyName("endPatterns")]       public List<string>? EndPatterns       { get; set; }
        [JsonPropertyName("namedRegionStart")]  public string?       NamedRegionStart  { get; set; }
        [JsonPropertyName("namedRegionEnd")]    public string?       NamedRegionEnd    { get; set; }
        [JsonPropertyName("indentBased")]       public bool          IndentBased       { get; set; }
        [JsonPropertyName("blockStartPattern")] public string?       BlockStartPattern { get; set; }
        [JsonPropertyName("indentTabWidth")]    public int           IndentTabWidth    { get; set; } = 4;
        [JsonPropertyName("tagBased")]             public bool          TagBased             { get; set; }
        [JsonPropertyName("selfClosingTags")]      public List<string>? SelfClosingTags      { get; set; }
        [JsonPropertyName("multilineTagSupport")]  public bool          MultilineTagSupport  { get; set; }
        [JsonPropertyName("headingBased")]         public bool          HeadingBased         { get; set; }
        [JsonPropertyName("minHeadingLevel")]      public int           MinHeadingLevel      { get; set; } = 2;
        [JsonPropertyName("endOfBlockHint")]       public EndOfBlockHintDto? EndOfBlockHint  { get; set; }
    }

    private sealed class BreakpointRulesDto
    {
        [JsonPropertyName("nonExecutablePatterns")]
        public string[]? NonExecutablePatterns { get; set; }

        [JsonPropertyName("statementContinuationPatterns")]
        public string[]? StatementContinuationPatterns { get; set; }

        [JsonPropertyName("maxStatementScanLines")]
        public int? MaxStatementScanLines { get; set; }

        [JsonPropertyName("blockScopeHighlight")]
        public bool? BlockScopeHighlight { get; set; }
    }

    private sealed class EndOfBlockHintDto
    {
        [JsonPropertyName("enabled")]          public bool Enabled          { get; set; } = true;
        [JsonPropertyName("showLineNumber")]   public bool ShowLineNumber   { get; set; } = true;
        [JsonPropertyName("showLineCount")]    public bool ShowLineCount    { get; set; } = true;
        [JsonPropertyName("maxContextLines")]  public int  MaxContextLines  { get; set; } = 3;
        [JsonPropertyName("triggerBrace")]     public bool TriggerBrace     { get; set; } = true;
        [JsonPropertyName("triggerDirective")] public bool TriggerDirective { get; set; } = true;
    }

    private sealed class BracketPairDto
    {
        [JsonPropertyName("open")]  public string? Open  { get; set; }
        [JsonPropertyName("close")] public string? Close { get; set; }
    }

    private sealed class FormattingRulesDto
    {
        // ── Whitespace ──────────────────────────────────────────────────────
        [JsonPropertyName("indentSize")]             public int    IndentSize             { get; set; } = 4;
        [JsonPropertyName("useTabs")]                public bool   UseTabs                { get; set; }
        [JsonPropertyName("trimTrailingWhitespace")] public bool   TrimTrailingWhitespace { get; set; } = true;
        [JsonPropertyName("insertFinalNewline")]      public bool   InsertFinalNewline      { get; set; } = true;
        [JsonPropertyName("lineEnding")]             public string? LineEnding             { get; set; }

        // ── Braces ──────────────────────────────────────────────────────────
        [JsonPropertyName("braceStyle")]             public string? BraceStyle            { get; set; }
        [JsonPropertyName("spaceBeforeOpenBrace")]   public bool   SpaceBeforeOpenBrace   { get; set; } = true;
        [JsonPropertyName("spaceAfterKeywords")]     public bool   SpaceAfterKeywords     { get; set; } = true;
        [JsonPropertyName("spaceInsideParens")]      public bool   SpaceInsideParens      { get; set; }
        [JsonPropertyName("indentCaseLabels")]       public bool   IndentCaseLabels       { get; set; }

        // ── Blank lines ─────────────────────────────────────────────────────
        [JsonPropertyName("maxConsecutiveBlankLines")] public int  MaxConsecutiveBlankLines { get; set; } = 2;
        [JsonPropertyName("blankLineBeforeMethod")]  public bool   BlankLineBeforeMethod  { get; set; } = true;
        [JsonPropertyName("blankLineAfterImports")]  public bool   BlankLineAfterImports  { get; set; } = true;

        // ── Spacing ─────────────────────────────────────────────────────────
        [JsonPropertyName("spaceAroundBinaryOperators")] public bool SpaceAroundBinaryOperators { get; set; } = true;
        [JsonPropertyName("spaceAfterComma")]        public bool   SpaceAfterComma        { get; set; } = true;

        // ── Imports ─────────────────────────────────────────────────────────
        [JsonPropertyName("organizeImports")]        public bool   OrganizeImports        { get; set; }
        [JsonPropertyName("separateSystemImports")]  public bool   SeparateSystemImports  { get; set; }

        // ── Language-specific ───────────────────────────────────────────────
        [JsonPropertyName("quoteStyle")]             public string? QuoteStyle            { get; set; }
        [JsonPropertyName("trailingCommas")]         public string? TrailingCommas        { get; set; }
        [JsonPropertyName("maxLineLength")]          public int    MaxLineLength          { get; set; } = 120;
        [JsonPropertyName("sqlKeywordsUppercase")]      public bool   SqlKeywordsUppercase      { get; set; }
        [JsonPropertyName("blockOpenKeywords")]         public string[]? BlockOpenKeywords    { get; set; }
        [JsonPropertyName("blockCloseKeywords")]        public string[]? BlockCloseKeywords   { get; set; }

        // ── XML / XAML-specific ─────────────────────────────────────────────
        [JsonPropertyName("xmlAttributeIndentLevels")] public int?  XmlAttributeIndentLevels { get; set; }
        [JsonPropertyName("xmlOneAttributePerLine")]   public bool? XmlOneAttributePerLine   { get; set; }

        // ── Formatting capabilities ──────────────────────────────────────────
        [JsonPropertyName("supportedRules")] public List<string>? SupportedRules { get; set; }

        // ── Pattern keywords (whfmt-driven regexes) ─────────────────────────
        [JsonPropertyName("keywordParenKeywords")]   public string[]? KeywordParenKeywords { get; set; }
        [JsonPropertyName("binaryOperators")]        public string[]? BinaryOperators      { get; set; }
        [JsonPropertyName("methodDeclKeywords")]     public string[]? MethodDeclKeywords   { get; set; }
        [JsonPropertyName("importKeywords")]         public string[]? ImportKeywords       { get; set; }
        [JsonPropertyName("sqlKeywords")]            public string[]? SqlKeywords          { get; set; }
    }

    // -- New mapping helpers ------------------------------------------------

    private static IReadOnlyList<(char Open, char Close)>? MapBracketPairs(BracketPairDto[]? dtos)
    {
        if (dtos is null or { Length: 0 }) return null;

        var pairs = new List<(char, char)>(dtos.Length);
        foreach (var dto in dtos)
        {
            if (dto.Open is { Length: 1 } && dto.Close is { Length: 1 })
                pairs.Add((dto.Open[0], dto.Close[0]));
        }
        return pairs.Count > 0 ? pairs : null;
    }

    private static FormattingRules? MapFormattingRules(FormattingRulesDto? dto, bool tagBased = false)
    {
        if (dto is null) return null;
        return new FormattingRules
        {
            FormatterStrategy          = tagBased ? FormatterStrategy.Xml : FormatterStrategy.Brace,
            IndentSize                 = dto.IndentSize,
            UseTabs                    = dto.UseTabs,
            TrimTrailingWhitespace     = dto.TrimTrailingWhitespace,
            InsertFinalNewline         = dto.InsertFinalNewline,
            LineEnding                 = ParseLineEnding(dto.LineEnding),
            BraceStyle                 = ParseBraceStyle(dto.BraceStyle),
            SpaceBeforeOpenBrace       = dto.SpaceBeforeOpenBrace,
            SpaceAfterKeywords         = dto.SpaceAfterKeywords,
            SpaceInsideParens          = dto.SpaceInsideParens,
            IndentCaseLabels           = dto.IndentCaseLabels,
            MaxConsecutiveBlankLines   = dto.MaxConsecutiveBlankLines,
            BlankLineBeforeMethod      = dto.BlankLineBeforeMethod,
            BlankLineAfterImports      = dto.BlankLineAfterImports,
            SpaceAroundBinaryOperators = dto.SpaceAroundBinaryOperators,
            SpaceAfterComma            = dto.SpaceAfterComma,
            OrganizeImports            = dto.OrganizeImports,
            SeparateSystemImports      = dto.SeparateSystemImports,
            QuoteStyle                 = ParseQuoteStyle(dto.QuoteStyle),
            TrailingCommas             = ParseTrailingCommas(dto.TrailingCommas),
            MaxLineLength              = dto.MaxLineLength,
            SqlKeywordsUppercase       = dto.SqlKeywordsUppercase,
            BlockOpenKeywords          = (IReadOnlyList<string>?)dto.BlockOpenKeywords ?? [],
            BlockCloseKeywords         = (IReadOnlyList<string>?)dto.BlockCloseKeywords ?? [],
            KeywordParenKeywords       = dto.KeywordParenKeywords is { Length: > 0 } ? dto.KeywordParenKeywords : null,
            BinaryOperators            = dto.BinaryOperators is { Length: > 0 } ? dto.BinaryOperators : null,
            MethodDeclKeywords         = dto.MethodDeclKeywords is { Length: > 0 } ? dto.MethodDeclKeywords : null,
            ImportKeywords             = dto.ImportKeywords is { Length: > 0 } ? dto.ImportKeywords : null,
            SqlKeywords                = dto.SqlKeywords is { Length: > 0 } ? dto.SqlKeywords : null,
            XmlAttributeIndentLevels   = dto.XmlAttributeIndentLevels ?? 2,
            XmlOneAttributePerLine     = dto.XmlOneAttributePerLine   ?? false,
            SupportedRules             = dto.SupportedRules is { Count: > 0 } sr
                                             ? sr.AsReadOnly()
                                             : null,
        };
    }

    private static LineEndingStyle ParseLineEnding(string? value) =>
        value?.ToLowerInvariant() switch
        {
            "lf"   => LineEndingStyle.LF,
            "crlf" => LineEndingStyle.CRLF,
            _      => LineEndingStyle.Auto,
        };

    private static BraceStyle? ParseBraceStyle(string? value) =>
        value?.ToLowerInvariant() switch
        {
            "allman"            => Languages.BraceStyle.Allman,
            "kr" or "k&r"       => Languages.BraceStyle.KR,
            "stroustrup"        => Languages.BraceStyle.Stroustrup,
            _                   => null,
        };

    private static QuoteStyle? ParseQuoteStyle(string? value) =>
        value?.ToLowerInvariant() switch
        {
            "double"   => Languages.QuoteStyle.Double,
            "single"   => Languages.QuoteStyle.Single,
            "backtick" => Languages.QuoteStyle.Backtick,
            _          => null,
        };

    private static TrailingCommaStyle ParseTrailingCommas(string? value) =>
        value?.ToLowerInvariant() switch
        {
            "es5" => TrailingCommaStyle.ES5,
            "all" => TrailingCommaStyle.All,
            _     => TrailingCommaStyle.None,
        };

    private static IReadOnlyList<ScriptGlobalEntry> MapScriptGlobals(ScriptGlobalDto[]? dtos)
    {
        if (dtos is null or { Length: 0 }) return [];

        return dtos.Select(g => new ScriptGlobalEntry(
            Name:          g.Name          ?? string.Empty,
            Type:          g.Type          ?? string.Empty,
            Documentation: g.Documentation ?? string.Empty,
            Members:       (g.Members ?? [])
                           .Select(m => new ScriptMemberEntry(
                               Name:          m.Name          ?? string.Empty,
                               Type:          m.Type          ?? string.Empty,
                               Kind:          m.Kind          ?? "property",
                               Documentation: m.Documentation ?? string.Empty))
                           .ToList()))
            .ToList();
    }

    private static IReadOnlyDictionary<string, FormattingPreviewSample> MapPreviewSamples(
        Dictionary<string, PreviewSampleDto>? dtos)
    {
        if (dtos is null or { Count: 0 })
            return new Dictionary<string, FormattingPreviewSample>();

        var result = new Dictionary<string, FormattingPreviewSample>(dtos.Count, StringComparer.Ordinal);
        foreach (var (key, dto) in dtos)
        {
            if (!string.IsNullOrEmpty(dto.Before) || !string.IsNullOrEmpty(dto.After))
                result[key] = new FormattingPreviewSample
                {
                    Before = dto.Before ?? string.Empty,
                    After  = dto.After  ?? string.Empty,
                };
        }
        return result;
    }

    private static IReadOnlyList<Regex>? MapColorLiteralPatterns(string[]? patterns)
    {
        if (patterns is null or { Length: 0 }) return null;

        var regexes = new List<Regex>(patterns.Length);
        foreach (var p in patterns)
        {
            if (string.IsNullOrWhiteSpace(p)) continue;
            try { regexes.Add(new Regex(p, RegexOptions.Compiled | RegexOptions.IgnoreCase)); }
            catch { /* skip malformed patterns */ }
        }
        return regexes.Count > 0 ? regexes : null;
    }
}
