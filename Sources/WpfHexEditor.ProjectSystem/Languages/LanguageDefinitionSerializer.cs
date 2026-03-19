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

namespace WpfHexEditor.ProjectSystem.Languages;

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
            EnableCodeLens           = dto.EnableCodeLens,
            EnableCtrlClickNavigation = dto.EnableCtrlClickNavigation,
            IsDefault                = dto.IsDefault,
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
        /// When true, the CodeEditor renders CodeLens hints for this language.
        /// </summary>
        public bool EnableCodeLens { get; set; }

        /// <summary>
        /// When true, Ctrl+click go-to-definition is active for this language.
        /// </summary>
        public bool EnableCtrlClickNavigation { get; set; }

        /// <summary>
        /// When true, the registry will call SetProjectDefault() automatically for all
        /// extensions declared in the file, making this the preferred language.
        /// </summary>
        public bool IsDefault { get; set; }
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
}
