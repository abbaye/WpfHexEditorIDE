// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Snippets/DefaultSnippetPack.cs
// Description:
//     Built-in snippets shipped with the IDE. Loaded as the baseline tier
//     by CodeEditorFactory before .whlang language-specific snippets and
//     user-defined snippets (last-tier wins on trigger collisions).
//
//     Bodies use $cursor to mark the final caret position and the
//     ${Variable} tokens recognised by SnippetVariableExpander.
// ==========================================================

namespace WpfHexEditor.Editor.CodeEditor.Snippets;

/// <summary>Built-in snippet pack indexed by language id.</summary>
public static class DefaultSnippetPack
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<Snippet>> Packs = Build();

    /// <summary>Returns the default snippets for <paramref name="languageId"/>, or an empty list.</summary>
    public static IReadOnlyList<Snippet> GetFor(string languageId)
        => Packs.TryGetValue(languageId, out var list) ? list : Array.Empty<Snippet>();

    /// <summary>All language ids that have at least one default snippet.</summary>
    public static IEnumerable<string> Languages => Packs.Keys;

    // ── Build ─────────────────────────────────────────────────────────────────

    private static IReadOnlyDictionary<string, IReadOnlyList<Snippet>> Build()
    {
        var d = new Dictionary<string, IReadOnlyList<Snippet>>(StringComparer.OrdinalIgnoreCase)
        {
            ["csharp"]     = BuildCSharp(),
            ["vbnet"]      = BuildVbNet(),
            ["javascript"] = BuildJavaScript(),
            ["typescript"] = BuildJavaScript(),   // reuse JS pack — TS is a superset
            ["python"]     = BuildPython(),
            ["fsharp"]     = BuildFSharp(),
            ["markdown"]   = BuildMarkdown(),
        };
        return d;
    }

    // ── C# ─────────────────────────────────────────────────────────────────────

    private static IReadOnlyList<Snippet> BuildCSharp() =>
    [
        new("for",      "for (int i = 0; i < $cursor; i++)\n{\n}",                                       "for loop"),
        new("foreach",  "foreach (var item in $cursor)\n{\n}",                                           "foreach loop"),
        new("while",    "while ($cursor)\n{\n}",                                                        "while loop"),
        new("if",       "if ($cursor)\n{\n}",                                                            "if statement"),
        new("ifelse",   "if ($cursor)\n{\n}\nelse\n{\n}",                                                 "if/else statement"),
        new("try",      "try\n{\n    $cursor\n}\ncatch (Exception ex)\n{\n}",                            "try/catch"),
        new("tryf",     "try\n{\n    $cursor\n}\ncatch (Exception ex)\n{\n}\nfinally\n{\n}",             "try/catch/finally"),
        new("using",    "using ($cursor)\n{\n}",                                                         "using block"),
        new("prop",     "public $cursor { get; set; }",                                                  "auto-property"),
        new("propfull", "private  _field;\npublic $cursor\n{\n    get => _field;\n    set => _field = value;\n}", "full property"),
        new("ctor",     "public ${FileNameBase}()\n{\n    $cursor\n}",                                   "constructor"),
        new("class",    "// Created by ${UserName} on ${Date}.\npublic class ${FileNameBase}\n{\n    $cursor\n}", "class with header"),
    ];

    // ── VB.NET ─────────────────────────────────────────────────────────────────

    private static IReadOnlyList<Snippet> BuildVbNet() =>
    [
        new("for",      "For i As Integer = 0 To $cursor\nNext",                                          "For loop"),
        new("if",       "If $cursor Then\n\nEnd If",                                                       "If statement"),
        new("try",      "Try\n    $cursor\nCatch ex As Exception\n\nEnd Try",                              "Try/Catch"),
        new("sub",      "Public Sub $cursor()\n\nEnd Sub",                                                 "Public Sub"),
        new("function", "Public Function $cursor() As \n\nEnd Function",                                   "Public Function"),
        new("class",    "' Created by ${UserName} on ${Date}.\nPublic Class ${FileNameBase}\n    $cursor\nEnd Class", "Class with header"),
        new("module",   "Public Module ${FileNameBase}\n    $cursor\nEnd Module",                          "Module"),
        new("prop",     "Public Property $cursor As ",                                                     "Auto-property"),
    ];

    // ── JavaScript / TypeScript ────────────────────────────────────────────────

    private static IReadOnlyList<Snippet> BuildJavaScript() =>
    [
        new("for",    "for (let i = 0; i < $cursor; i++) {\n}",                       "for loop"),
        new("forin",  "for (const key in $cursor) {\n}",                              "for…in loop"),
        new("forof",  "for (const item of $cursor) {\n}",                             "for…of loop"),
        new("if",     "if ($cursor) {\n}",                                            "if statement"),
        new("try",    "try {\n    $cursor\n} catch (err) {\n}",                       "try/catch"),
        new("func",   "function $cursor() {\n}",                                      "function"),
        new("arrow",  "const $cursor = () => {\n};",                                  "arrow function"),
        new("class",  "// Created by ${UserName} on ${Date}.\nclass ${FileNameBase} {\n    constructor() {\n        $cursor\n    }\n}", "ES6 class"),
    ];

    // ── Python ─────────────────────────────────────────────────────────────────

    private static IReadOnlyList<Snippet> BuildPython() =>
    [
        new("for",   "for $cursor in :\n    pass",                                    "for loop"),
        new("if",    "if $cursor:\n    pass",                                         "if statement"),
        new("def",   "def $cursor():\n    pass",                                      "function"),
        new("class", "# Created by ${UserName} on ${Date}.\nclass ${FileNameBase}:\n    def __init__(self):\n        $cursor",       "class with header"),
        new("try",   "try:\n    $cursor\nexcept Exception as e:\n    pass",           "try/except"),
        new("with",  "with $cursor as f:\n    pass",                                  "with statement"),
    ];

    // ── F# ─────────────────────────────────────────────────────────────────────

    private static IReadOnlyList<Snippet> BuildFSharp() =>
    [
        new("let",   "let $cursor = ",                                                "let binding"),
        new("match", "match $cursor with\n| _ -> ",                                   "match expression"),
        new("for",   "for i in 0 .. $cursor do\n    ",                                "for loop"),
        new("try",   "try\n    $cursor\nwith ex ->\n    ",                            "try/with"),
    ];

    // ── Markdown ───────────────────────────────────────────────────────────────

    private static IReadOnlyList<Snippet> BuildMarkdown() =>
    [
        new("link",      "[${SelectedText}]($cursor)",                                 "Markdown link wrapping selection"),
        new("img",       "![${SelectedText}]($cursor)",                                "Markdown image wrapping selection"),
        new("codeblock", "```$cursor\n${SelectedText}\n```",                           "Fenced code block"),
    ];
}
