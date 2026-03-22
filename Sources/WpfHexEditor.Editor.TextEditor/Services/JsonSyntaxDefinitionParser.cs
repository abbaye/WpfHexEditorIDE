//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using System.Text.Json;
using WpfHexEditor.Editor.TextEditor.Highlighting;

namespace WpfHexEditor.Editor.TextEditor.Services;

/// <summary>
/// Deserialises a <c>.whlang</c> JSON file into a <see cref="SyntaxDefinition"/>.
/// </summary>
internal static class JsonSyntaxDefinitionParser
{
    private static readonly JsonDocumentOptions _jsonOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Parses a .whlang definition from a stream.
    /// </summary>
    /// <param name="stream">Readable stream containing UTF-8 JSON.</param>
    /// <param name="sourceKey">Identifier used to populate <c>SyntaxDefinition.SourceKey</c>.</param>
    /// <returns>Parsed definition, or <see langword="null"/> if parsing fails.</returns>
    internal static SyntaxDefinition? Parse(Stream stream, string sourceKey)
    {
        try
        {
            using var doc = JsonDocument.Parse(stream, _jsonOptions);
            var root = doc.RootElement;

            var name        = root.TryGetProperty("name",        out var nameProp)  ? nameProp.GetString()  ?? string.Empty : string.Empty;
            var version     = root.TryGetProperty("version",     out var vp)        ? vp.GetString()        ?? string.Empty : string.Empty;
            var author      = root.TryGetProperty("author",      out var ap)        ? ap.GetString()        ?? string.Empty : string.Empty;
            var description = root.TryGetProperty("description", out var dp)        ? dp.GetString()        ?? string.Empty : string.Empty;
            var category    = root.TryGetProperty("category",    out var catp)      ? catp.GetString()      ?? string.Empty : string.Empty;

            var extensions = new List<string>();
            if (root.TryGetProperty("extensions", out var extProp) && extProp.ValueKind == JsonValueKind.Array)
                foreach (var e in extProp.EnumerateArray())
                    if (e.GetString() is string ext) extensions.Add(ext.ToLowerInvariant());

            var lineComment = root.TryGetProperty("lineComment", out var lcProp) ? lcProp.GetString() : null;
            var blockStart  = root.TryGetProperty("blockCommentStart", out var bcsProp) ? bcsProp.GetString() : null;
            var blockEnd    = root.TryGetProperty("blockCommentEnd",   out var bceProp) ? bceProp.GetString() : null;

            SyntaxReferences references = new();
            if (root.TryGetProperty("references", out var refEl))
            {
                var specs = refEl.TryGetProperty("specifications", out var spEl)
                    ? spEl.EnumerateArray()
                          .Select(e => e.GetString() ?? string.Empty)
                          .Where(s => s.Length > 0)
                          .ToList()
                    : (IReadOnlyList<string>)[];
                var links = refEl.TryGetProperty("webLinks", out var wlEl)
                    ? wlEl.EnumerateArray()
                          .Select(e => e.GetString() ?? string.Empty)
                          .Where(s => s.Length > 0)
                          .ToList()
                    : (IReadOnlyList<string>)[];
                references = new SyntaxReferences { Specifications = specs, WebLinks = links };
            }

            var rules = new List<SyntaxRule>();
            if (root.TryGetProperty("rules", out var rulesProp) && rulesProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var r in rulesProp.EnumerateArray())
                {
                    var type     = r.TryGetProperty("type",     out var tp) ? tp.GetString() ?? string.Empty : string.Empty;
                    var pattern  = r.TryGetProperty("pattern",  out var pp) ? pp.GetString() ?? string.Empty : string.Empty;
                    var colorKey = r.TryGetProperty("colorKey", out var cp) ? cp.GetString() ?? string.Empty : string.Empty;

                    if (!string.IsNullOrEmpty(pattern))
                        rules.Add(new SyntaxRule { Type = type, Pattern = pattern, ColorKey = colorKey });
                }
            }

            return new SyntaxDefinition
            {
                Name              = name,
                Version           = version,
                Author            = author,
                Description       = description,
                Category          = category,
                Extensions        = extensions,
                LineComment       = lineComment,
                BlockCommentStart = blockStart,
                BlockCommentEnd   = blockEnd,
                References        = references,
                Rules             = rules,
                SourceKey         = sourceKey
            };
        }
        catch
        {
            // Malformed .whlang — skip silently.
            return null;
        }
    }

    /// <summary>
    /// Parses a .whlang definition from a file path.
    /// </summary>
    internal static SyntaxDefinition? ParseFile(string filePath)
    {
        if (!File.Exists(filePath)) return null;
        using var fs = File.OpenRead(filePath);
        return Parse(fs, filePath);
    }

    /// <summary>
    /// Parses a <c>SyntaxDefinition</c> from a <c>syntaxDefinition</c> JSON block
    /// extracted from a <c>.whfmt</c> file.
    /// The block uses <c>lineCommentPrefix</c> (instead of <c>lineComment</c>),
    /// <c>blockCommentStart</c>, and <c>blockCommentEnd</c>.
    /// </summary>
    /// <param name="syntaxJson">JSON text of the syntaxDefinition sub-object.</param>
    /// <param name="formatName">Display name from the parent .whfmt file.</param>
    /// <param name="category">Category from the parent .whfmt file.</param>
    /// <param name="extensions">Extensions from the parent .whfmt file (fallback when not in block).</param>
    /// <param name="sourceKey">Resource key for the parent .whfmt embedded resource.</param>
    internal static SyntaxDefinition? ParseFromSyntaxDefinitionBlock(
        string syntaxJson,
        string formatName,
        string category,
        IReadOnlyList<string> extensions,
        string sourceKey)
    {
        try
        {
            using var doc = JsonDocument.Parse(syntaxJson, _jsonOptions);
            var root = doc.RootElement;

            var name    = root.TryGetProperty("name", out var np) ? np.GetString() ?? formatName : formatName;

            var exts = new List<string>();
            if (root.TryGetProperty("extensions", out var extProp) && extProp.ValueKind == JsonValueKind.Array)
                foreach (var e in extProp.EnumerateArray())
                    if (e.GetString() is string ext) exts.Add(ext.ToLowerInvariant());
            if (exts.Count == 0) exts.AddRange(extensions);

            // syntaxDefinition blocks use "lineCommentPrefix" (not "lineComment").
            var lineComment = root.TryGetProperty("lineCommentPrefix",  out var lcp) ? lcp.GetString()
                            : root.TryGetProperty("lineComment",        out var lc)  ? lc.GetString()
                            : null;
            var blockStart  = root.TryGetProperty("blockCommentStart", out var bcs) ? bcs.GetString() : null;
            var blockEnd    = root.TryGetProperty("blockCommentEnd",   out var bce) ? bce.GetString() : null;

            var rules = new List<SyntaxRule>();
            if (root.TryGetProperty("rules", out var rulesProp) && rulesProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var r in rulesProp.EnumerateArray())
                {
                    var type     = r.TryGetProperty("type",     out var tp) ? tp.GetString() ?? string.Empty : string.Empty;
                    var pattern  = r.TryGetProperty("pattern",  out var pp) ? pp.GetString() ?? string.Empty : string.Empty;
                    var colorKey = r.TryGetProperty("colorKey", out var cp) ? cp.GetString() ?? string.Empty : string.Empty;

                    // Fallback: derive colorKey from semantic type when not explicitly declared.
                    // Enables .whfmt files designed for CodeEditor (type-only rules) to also
                    // produce visible colors in the TextEditor's RegexSyntaxHighlighter.
                    if (string.IsNullOrEmpty(colorKey) && !string.IsNullOrEmpty(type))
                        colorKey = ResolveColorKeyFromType(type);

                    if (!string.IsNullOrEmpty(pattern))
                        rules.Add(new SyntaxRule { Type = type, Pattern = pattern, ColorKey = colorKey });
                }
            }

            // Parse embeddedLanguages (optional — for fenced code block injection)
            var embeddedLanguages = new List<EmbeddedLanguageEntry>();
            if (root.TryGetProperty("embeddedLanguages", out var embProp) &&
                embProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in embProp.EnumerateArray())
                {
                    var id  = e.TryGetProperty("id",        out var ip) ? ip.GetString()  ?? "" : "";
                    var ext = e.TryGetProperty("extension", out var ep) ? ep.GetString()  ?? "" : "";
                    if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(ext))
                        embeddedLanguages.Add(new EmbeddedLanguageEntry(id, ext));
                }
            }

            return new SyntaxDefinition
            {
                Name              = name,
                Category          = category,
                Extensions        = exts,
                LineComment       = lineComment,
                BlockCommentStart = blockStart,
                BlockCommentEnd   = blockEnd,
                Rules             = rules,
                EmbeddedLanguages = embeddedLanguages,
                SourceKey         = sourceKey,
            };
        }
        catch
        {
            // Malformed syntaxDefinition block — skip silently.
            return null;
        }
    }

    // -----------------------------------------------------------------------
    // Type → ColorKey fallback map
    // -----------------------------------------------------------------------

    // Maps semantic rule types (used by CodeEditor .whfmt files that omit colorKey)
    // to standard TE_* theme brush keys used by TextEditor's RegexSyntaxHighlighter.
    private static readonly Dictionary<string, string> _typeToColorKey =
        new(StringComparer.OrdinalIgnoreCase)
    {
        { "Keyword",         "TE_Keyword"    },
        { "ControlKeyword",  "TE_Keyword"    },
        { "Type",            "TE_Type"       },
        { "BuiltinType",     "TE_Type"       },
        { "String",          "TE_String"     },
        { "InterpolatedString", "TE_String"  },
        { "VerbatimString",  "TE_String"     },
        { "Char",            "TE_String"     },
        { "Number",          "TE_Literal"    },
        { "Literal",         "TE_Literal"    },
        { "Bool",            "TE_Literal"    },
        { "Null",            "TE_Literal"    },
        { "Comment",         "TE_Comment"    },
        { "BlockComment",    "TE_Comment"    },
        { "DocComment",      "TE_Comment"    },
        { "Preprocessor",    "TE_Directive"  },
        { "Directive",       "TE_Directive"  },
        { "Annotation",      "TE_Directive"  },
        { "Decorator",       "TE_Directive"  },
        { "Attribute",       "TE_Directive"  },
        { "RegionName",      "TE_Directive"  },
        { "RegionKeyword",   "TE_Directive"  },
        { "ControlFlow",     "TE_Keyword"    },
        { "NamespaceDecl",   "TE_Type"       },
        { "UsingRef",        "TE_Directive"  },
        { "UserType",        "TE_Type"       },
        { "Operator",        "TE_Operator"   },
        { "Punctuation",     "TE_Punctuation"},
        { "Label",           "TE_Label"      },
        { "Namespace",       "TE_Type"       },
        { "Function",        "TE_Type"       },
        { "Method",          "TE_Type"       },
        { "Variable",        "TE_Label"      },
        { "Parameter",       "TE_Label"      },
        { "Property",        "TE_Label"      },
        { "Field",           "TE_Label"      },
        { "Identifier",      "TE_Label"      },
    };

    /// <summary>
    /// Derives a <c>TE_*</c> color key from a semantic rule type when <c>colorKey</c>
    /// is not explicitly declared in the <c>.whfmt</c> rule block.
    /// Returns an empty string when no mapping is found (plain text rendering).
    /// </summary>
    private static string ResolveColorKeyFromType(string type)
        => _typeToColorKey.TryGetValue(type, out var key) ? key : string.Empty;
}
