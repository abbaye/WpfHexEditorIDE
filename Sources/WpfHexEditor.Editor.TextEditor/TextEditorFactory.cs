//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.TextEditor.Controls;
using WpfHexEditor.Editor.TextEditor.Services;

namespace WpfHexEditor.Editor.TextEditor;

/// <summary>
/// Factory that registers the <see cref="TextEditor"/> with the
/// <see cref="IEditorRegistry"/> so the host application can open text
/// files automatically by extension.
/// </summary>
/// <remarks>
/// All 26 embedded language extensions are declared in <see cref="TextEditorDescriptor.SupportedExtensions"/>.
/// The catalog (<see cref="SyntaxDefinitionCatalog"/>) is consulted at open-time
/// to select the correct syntax definition for the file.
/// </remarks>
public sealed class TextEditorFactory : IEditorFactory
{
    private static readonly IEditorDescriptor _descriptor = new TextEditorDescriptor();

    /// <inheritdoc/>
    public IEditorDescriptor Descriptor => _descriptor;

    /// <inheritdoc/>
    public bool CanOpen(string filePath)
    {
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
        if (string.IsNullOrEmpty(ext)) return false;
        // Check exact extension match against known extensions.
        if (_descriptor.SupportedExtensions.Contains(ext)) return true;
        // Also accept if the SyntaxDefinitionCatalog knows the extension.
        return SyntaxDefinitionCatalog.Instance.FindByExtension(ext) is not null;
    }

    /// <inheritdoc/>
    public IDocumentEditor Create() => new Controls.TextEditor();
}

file sealed class TextEditorDescriptor : IEditorDescriptor
{
    public string Id          => "text-editor";
    public string DisplayName => "Text Editor";
    public string Description => "Multi-language text editor with syntax highlighting (.whlang definitions)";

    public IReadOnlyList<string> SupportedExtensions =>
    [
        // Assembly
        ".asm", ".s", ".z80", ".arm", ".mips", ".65s", ".6502",
        // C-style
        ".c", ".h", ".cpp", ".hpp", ".cc", ".cxx", ".hxx", ".inl",
        ".cs", ".java",
        ".js", ".mjs", ".cjs", ".ts", ".tsx", ".jsx",
        ".rs", ".go",
        ".swift", ".kt", ".kts", ".php", ".phtml", ".dart",
        // Scripting
        ".py", ".pyw", ".pyi", ".lua",
        ".rb", ".rbw", ".rake", ".gemspec",
        ".pl", ".pm", ".pod",
        ".sh", ".bash", ".zsh", ".fish",
        ".bat", ".cmd", ".ps1", ".psm1", ".psd1",
        // Data
        ".xml", ".html", ".htm", ".xaml", ".xsl", ".xslt", ".svg", ".xhtml", ".resx",
        ".json", ".jsonc", ".json5",
        ".ini", ".cfg", ".conf", ".config", ".properties", ".env", ".inf",
        ".yaml", ".yml", ".toml",
        ".sql", ".ddl", ".dml",
        // Misc
        ".md", ".markdown", ".mkd", ".mdx",
        ".txt", ".log", ".text",
        // Generic scripts
        ".scr", ".msg", ".evt", ".script", ".dec", ".lua"
    ];
}
