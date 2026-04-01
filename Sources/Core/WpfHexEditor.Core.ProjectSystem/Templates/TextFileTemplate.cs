//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Text;

namespace WpfHexEditor.Core.ProjectSystem.Templates;

/// <summary>
/// Template for an empty text file.
/// </summary>
public sealed class TextFileTemplate : IFileTemplate
{
    public string Name             => "Text File";
    public string Description      => "Creates a new empty text file (.txt).";
    public string DefaultExtension => ".txt";
    public string Category         => "General";
    public string IconGlyph        => "\uE8A5";

    public byte[] CreateContent()
        => Encoding.UTF8.GetBytes("");
}

/// <summary>Template for a Markdown document.</summary>
public sealed class MarkdownFileTemplate : IFileTemplate
{
    public string Name             => "Markdown Document";
    public string Description      => "Creates a new Markdown document (.md) with a title and section stub.";
    public string DefaultExtension => ".md";
    public string Category         => "General";
    public string IconGlyph        => "\uE8A5";

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "# Title\n\n" +
        "## Section\n\n" +
        "Write your content here.\n");
}

/// <summary>Template for a Git ignore file.</summary>
public sealed class GitIgnoreTemplate : IFileTemplate
{
    public string Name             => "Git Ignore";
    public string Description      => "Creates a new .gitignore file with common .NET ignore patterns.";
    public string DefaultExtension => ".gitignore";
    public string Category         => "General";
    public string IconGlyph        => "\uE8A5";

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "# .gitignore\n\n" +
        "# Build outputs\n" +
        "bin/\nobj/\n\n" +
        "# User-specific files\n" +
        "*.user\n*.suo\n*.userprefs\n\n" +
        "# IDE\n" +
        ".vs/\n.idea/\n\n" +
        "# NuGet\n" +
        "packages/\n*.nupkg\n\n" +
        "# OS\n" +
        ".DS_Store\nThumbs.db\n");
}
