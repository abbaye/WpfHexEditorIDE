//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Text;

namespace WpfHexEditor.Core.ProjectSystem.Templates;

// =============================================================================
// Data file templates (3)
// =============================================================================

/// <summary>Template for a new XML file.</summary>
public sealed class XmlFileTemplate : IFileTemplate
{
    public string Name             => "XML File";
    public string Description      => "Creates a new XML file with a UTF-8 declaration and a root element.";
    public string DefaultExtension => ".xml";
    public string Category         => "Data";
    public string IconGlyph        => "\uE8EC";

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<root>\n</root>\n");
}

/// <summary>Template for a new YAML document file.</summary>
public sealed class YamlFileTemplate : IFileTemplate
{
    public string Name             => "YAML File";
    public string Description      => "Creates a new YAML document file (.yaml) with a document separator.";
    public string DefaultExtension => ".yaml";
    public string Category         => "Data";
    public string IconGlyph        => "\uE8EC";

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "# YAML document\n---\n\n");
}

/// <summary>Template for a new CSV file.</summary>
public sealed class CsvFileTemplate : IFileTemplate
{
    public string Name             => "CSV File";
    public string Description      => "Creates a new comma-separated values file (.csv) with a sample header row.";
    public string DefaultExtension => ".csv";
    public string Category         => "Data";
    public string IconGlyph        => "\uE9D2";

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "Column1,Column2,Column3\n");
}
