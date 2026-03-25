//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Text;

namespace WpfHexEditor.Core.ProjectSystem.Templates;

// =============================================================================
// Web file templates (4)
// =============================================================================

/// <summary>Template for a new HTML5 file.</summary>
public sealed class HtmlFileTemplate : IFileTemplate
{
    public string Name             => "HTML File";
    public string Description      => "Creates a new HTML5 file with a standard document scaffold.";
    public string DefaultExtension => ".html";
    public string Category         => "Web";
    public string IconGlyph        => "\uE774";

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "<!DOCTYPE html>\n" +
        "<html lang=\"en\">\n" +
        "<head>\n" +
        "    <meta charset=\"UTF-8\" />\n" +
        "    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />\n" +
        "    <title>Document</title>\n" +
        "</head>\n" +
        "<body>\n\n" +
        "</body>\n" +
        "</html>\n");
}

/// <summary>Template for a new CSS stylesheet file.</summary>
public sealed class CssFileTemplate : IFileTemplate
{
    public string Name             => "CSS File";
    public string Description      => "Creates a new CSS stylesheet file with a minimal body rule.";
    public string DefaultExtension => ".css";
    public string Category         => "Web";
    public string IconGlyph        => "\uE771";

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "/* Stylesheet */\n\nbody {\n    margin: 0;\n    padding: 0;\n}\n");
}

/// <summary>Template for a new JavaScript file.</summary>
public sealed class JavaScriptFileTemplate : IFileTemplate
{
    public string Name             => "JavaScript File";
    public string Description      => "Creates a new JavaScript file in strict mode.";
    public string DefaultExtension => ".js";
    public string Category         => "Web";
    public string IconGlyph        => "\uE943";

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "'use strict';\n\n");
}

/// <summary>Template for a new TypeScript file.</summary>
public sealed class TypeScriptFileTemplate : IFileTemplate
{
    public string Name             => "TypeScript File";
    public string Description      => "Creates a new TypeScript module file.";
    public string DefaultExtension => ".ts";
    public string Category         => "Web";
    public string IconGlyph        => "\uE943";

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "export {};\n\n");
}
