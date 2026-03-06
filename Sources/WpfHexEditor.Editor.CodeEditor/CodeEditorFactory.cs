//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using System.Linq;
using System.Windows.Media;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.CodeEditor.Controls;
using WpfHexEditor.Editor.CodeEditor.Helpers;
using WpfHexEditor.Editor.CodeEditor.Snippets;
using WpfHexEditor.ProjectSystem.Languages;

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
        // Accept any extension that LanguageRegistry knows about, plus the core JSON extensions.
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
        if (ext is ".json" or ".whfmt" or ".whjson" or ".whlang") return true;

        // Also accept files for which a LanguageDefinition is registered.
        return ext is not null && LanguageRegistry.Instance.GetLanguageForFile(filePath) is not null;
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

    // ── Language → highlighter ───────────────────────────────────────────────

    private static ISyntaxHighlighter BuildHighlighter(LanguageDefinition language)
    {
        var rules = language.SyntaxRules.Select(rule => new RegexHighlightRule(
            rule.Pattern,
            TokenKindToBrush(rule.Kind),
            isBold:   rule.Kind is SyntaxTokenKind.Keyword or SyntaxTokenKind.Type,
            isItalic: rule.Kind is SyntaxTokenKind.Comment));

        return new SyntaxRuleHighlighter(rules);
    }

    private static SnippetManager BuildSnippetManager(LanguageDefinition language)
    {
        var mgr = new SnippetManager();
        foreach (var def in language.Snippets)
            mgr.Register(new Snippet(def.Trigger, def.Body, def.Description));
        return mgr;
    }

    // ── Token kind → brush mapping (VS-inspired defaults) ───────────────────

    private static Brush TokenKindToBrush(SyntaxTokenKind kind) => kind switch
    {
        SyntaxTokenKind.Keyword    => new SolidColorBrush(Color.FromRgb(86,  156, 214)),  // VS blue
        SyntaxTokenKind.String     => new SolidColorBrush(Color.FromRgb(214, 157, 133)),  // VS tan
        SyntaxTokenKind.Number     => new SolidColorBrush(Color.FromRgb(181, 206, 168)),  // VS light green
        SyntaxTokenKind.Comment    => new SolidColorBrush(Color.FromRgb(106, 153, 85)),   // VS green
        SyntaxTokenKind.Type       => new SolidColorBrush(Color.FromRgb(78,  201, 176)),  // VS teal
        SyntaxTokenKind.Identifier => new SolidColorBrush(Color.FromRgb(220, 220, 170)),  // VS yellow
        SyntaxTokenKind.Operator   => new SolidColorBrush(Color.FromRgb(180, 180, 180)),  // light gray
        SyntaxTokenKind.Bracket    => new SolidColorBrush(Color.FromRgb(255, 215, 0)),    // gold
        SyntaxTokenKind.Attribute  => new SolidColorBrush(Color.FromRgb(176, 176, 255)),  // lavender
        _                          => Brushes.White
    };
}

file sealed class CodeEditorDescriptor : IEditorDescriptor
{
    public string Id          => "code-editor";
    public string DisplayName => "Code Editor";
    public string Description => "Multi-language code editor (JSON, .whlang, and any registered language)";
    public IReadOnlyList<string> SupportedExtensions => [".json", ".whfmt", ".whjson", ".whlang"];
}
