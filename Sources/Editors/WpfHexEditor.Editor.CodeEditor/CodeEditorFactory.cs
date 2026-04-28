//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using WpfHexEditor.Core.Definitions;
using WpfHexEditor.Core.Definitions.Query;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.CodeEditor.Controls;
using WpfHexEditor.Editor.CodeEditor.Helpers;
using WpfHexEditor.Editor.CodeEditor.Snippets;
using WpfHexEditor.Core.ProjectSystem.Languages;

namespace WpfHexEditor.Editor.CodeEditor;

/// <summary>
/// <see cref="IEditorFactory"/> for text / code files.
/// Queries <see cref="LanguageRegistry"/> to inject the correct
/// <see cref="ISyntaxHighlighter"/> and <see cref="SnippetManager"/>
/// into each new <see cref="CodeEditorSplitHost"/> at creation time.
///
/// Register at application startup:
/// <code>EditorRegistry.Instance.Register(new CodeEditorFactory());</code>
/// </summary>
public sealed class CodeEditorFactory : IEditorFactory
{
    private static readonly IEditorDescriptor _descriptor = new CodeEditorDescriptor();

    public IEditorDescriptor Descriptor => _descriptor;

    public bool CanOpen(string filePath)
    {
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
        if (ext is ".json" or ".whjson" or ".whlang") return true;

        // EditorHint fast-path: language definition carries the preferred editor hint
        // populated from the parent .whfmt "preferredEditor" field.  O(1) after registry warmup.
        var language = ext is not null ? LanguageRegistry.Instance.GetLanguageForFile(filePath) : null;
        if (language?.EditorHint == "code-editor") return true;

        // If EditorHint is explicitly set to a different editor (e.g. "xaml-designer", "hex-editor"),
        // defer — do not steal the file from its dedicated factory.
        if (language?.EditorHint is not null) return false;

        // Catalog-driven: accept any extension explicitly mapped to this editor via .whfmt preferredEditor field.
        var entry = EmbeddedFormatCatalog.Instance.Query().WithExtension(ext).First();
        if (entry?.PreferredEditor == "code-editor") return true;

        // Fallback: any registered language definition implies the code editor handles this file.
        return language is not null;
    }

    /// <summary>
    /// Creates a new <see cref="CodeEditorSplitHost"/> and configures it with the
    /// highlighter and snippets appropriate for <paramref name="filePath"/>.
    /// Falls back to the built-in JSON highlighter for <c>.json</c>/<c>.whfmt</c>/<c>.whjson</c>.
    /// </summary>
    public IDocumentEditor CreateForFile(string filePath)
    {
        var host = new CodeEditorSplitHost();

        var language = LanguageRegistry.Instance.GetLanguageForFile(filePath);
        if (language is not null)
        {
            var highlighter = BuildHighlighter(language);
            host.PrimaryEditor.ExternalHighlighter   = highlighter;
            host.SecondaryEditor.ExternalHighlighter = highlighter;

            // Propagate the language definition so CodeEditor can gate InlineHints and Ctrl+Click.
            host.PrimaryEditor.Language   = language;
            host.SecondaryEditor.Language = language;

            if (language.Snippets.Count > 0)
            {
                var mgr = BuildSnippetManager(language);
                host.PrimaryEditor.SnippetManager   = mgr;
                host.SecondaryEditor.SnippetManager = mgr;
            }
        }

        return host;
    }

    /// <summary>
    /// Creates a new editor without file-specific language configuration.
    /// Prefer <see cref="CreateForFile"/> when the target file is known.
    /// </summary>
    public IDocumentEditor Create() => new CodeEditorSplitHost();

    // -- Language → highlighter -----------------------------------------------

    internal static ISyntaxHighlighter BuildHighlighter(LanguageDefinition language)
    {
        var rules = language.SyntaxRules.Select(rule => new RegexHighlightRule(
            rule.Pattern,
            TokenKindToBrush(rule.Kind),
            isBold:   rule.Kind is SyntaxTokenKind.Keyword,
            isItalic: rule.Kind is SyntaxTokenKind.Comment,
            kind:     rule.Kind));

        return new SyntaxRuleHighlighter(
            rules,
            language.Name,
            language.BlockCommentStart,
            language.BlockCommentEnd);
    }

    private static SnippetManager BuildSnippetManager(LanguageDefinition language)
    {
        var mgr = new SnippetManager();
        foreach (var def in language.Snippets)
            mgr.Register(new Snippet(def.Trigger, def.Body, def.Description));
        return mgr;
    }

    // -- Token kind → brush mapping ------------------------------------------

    /// <summary>
    /// Resolves a brush for <paramref name="kind"/> from active application resources (CE_* keys).
    /// Falls back to hard-coded VS Dark defaults when resources are not yet loaded (e.g. design time).
    /// </summary>
    private static Brush TokenKindToBrush(SyntaxTokenKind kind)
    {
        var resourceKey = kind switch
        {
            SyntaxTokenKind.Keyword    => "CE_Keyword",
            SyntaxTokenKind.String     => "CE_String",
            SyntaxTokenKind.Number     => "CE_Number",
            SyntaxTokenKind.Comment    => "CE_Comment",
            SyntaxTokenKind.Type       => "CE_Type",
            SyntaxTokenKind.Identifier => "CE_Identifier",
            SyntaxTokenKind.Operator   => "CE_Operator",
            SyntaxTokenKind.Bracket    => "CE_Bracket",
            SyntaxTokenKind.Attribute   => "CE_Attribute",
            SyntaxTokenKind.ControlFlow => "CE_ControlFlow",
            _                           => "CE_Foreground"
        };

        if (Application.Current?.TryFindResource(resourceKey) is Brush themeBrush)
            return themeBrush;

        return FallbackBrush(kind);
    }

    /// <summary>
    /// Hard-coded VS Dark fallback palette used when theme resources are unavailable.
    /// </summary>
    private static Brush FallbackBrush(SyntaxTokenKind kind) => kind switch
    {
        SyntaxTokenKind.Keyword    => new SolidColorBrush(Color.FromRgb(86,  156, 214)),  // #569CD6
        SyntaxTokenKind.String     => new SolidColorBrush(Color.FromRgb(206, 145, 120)),  // #CE9178
        SyntaxTokenKind.Number     => new SolidColorBrush(Color.FromRgb(181, 206, 168)),  // #B5CEA8
        SyntaxTokenKind.Comment    => new SolidColorBrush(Color.FromRgb(106, 153, 85)),   // #6A9955
        SyntaxTokenKind.Type       => new SolidColorBrush(Color.FromRgb(78,  201, 176)),  // #4EC9B0
        SyntaxTokenKind.Identifier => new SolidColorBrush(Color.FromRgb(220, 220, 170)),  // #DCDCAA
        SyntaxTokenKind.Operator   => new SolidColorBrush(Color.FromRgb(212, 212, 212)),  // #D4D4D4
        SyntaxTokenKind.Bracket    => new SolidColorBrush(Color.FromRgb(255, 215, 0)),    // #FFD700
        SyntaxTokenKind.Attribute   => new SolidColorBrush(Color.FromRgb(156, 220, 254)),  // #9CDCFE
        SyntaxTokenKind.ControlFlow => new SolidColorBrush(Color.FromRgb(197, 134, 192)), // #C586C0 pink
        _                           => new SolidColorBrush(Color.FromRgb(212, 212, 212))  // #D4D4D4
    };
}

file sealed class CodeEditorDescriptor : IEditorDescriptor
{
    public string Id          => "code-editor";
    public string DisplayName => "Code Editor";
    public string Description => "Multi-language code editor (JSON, .whlang, and any registered language)";
    public IReadOnlyList<string> SupportedExtensions => [".json", ".whjson", ".whlang"];
}
