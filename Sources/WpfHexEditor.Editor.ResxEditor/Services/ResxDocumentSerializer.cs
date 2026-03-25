// ==========================================================
// Project: WpfHexEditor.Editor.ResxEditor
// File: Services/ResxDocumentSerializer.cs
// Description:
//     Writes a list of ResxEntry records back to XML.
//     Round-trip safe: preserves the <resheader> block and
//     any comments that precede data entries.
//     All <data> elements are rebuilt from the current entry
//     list so edits and deletions are fully reflected.
// ==========================================================

using System.Text;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using WpfHexEditor.Editor.ResxEditor.Models;

namespace WpfHexEditor.Editor.ResxEditor.Services;

/// <summary>Serializes a <see cref="ResxDocument"/> to XML.</summary>
public static class ResxDocumentSerializer
{
    private static readonly XNamespace XmlNs = "http://www.w3.org/XML/1998/namespace";

    /// <summary>Writes entries to <paramref name="filePath"/> asynchronously.</summary>
    public static async Task SaveAsync(
        ResxDocument         document,
        IReadOnlyList<ResxEntry> entries,
        string               filePath,
        CancellationToken    ct = default)
    {
        var xml = Serialize(document, entries);
        await File.WriteAllTextAsync(filePath, xml, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), ct);
    }

    /// <summary>Serializes to an XML string without writing to disk.</summary>
    public static string Serialize(ResxDocument document, IReadOnlyList<ResxEntry> entries)
    {
        // Clone the template (preserves resheader block)
        var xdoc = new XDocument(document.InternalDoc);
        var root = xdoc.Root!;

        // Remove all existing <data> and <metadata> elements
        root.Elements("data").Remove();
        root.Elements("metadata").Remove();

        // Append rebuilt <data> elements
        foreach (var entry in entries)
            root.Add(BuildDataElement(entry));

        var settings = new XmlWriterSettings
        {
            Indent             = true,
            IndentChars        = "  ",
            Encoding           = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            OmitXmlDeclaration = false,
            NewLineChars       = "\r\n",
        };

        var sb = new StringBuilder();
        using (var writer = XmlWriter.Create(sb, settings))
            xdoc.WriteTo(writer);

        return sb.ToString();
    }

    // ------------------------------------------------------------------

    private static XElement BuildDataElement(ResxEntry e)
    {
        var data = new XElement("data");
        data.Add(new XAttribute("name", e.Name));

        if (e.Space is not null)
            data.Add(new XAttribute(XmlNs + "space", e.Space));
        if (e.TypeName is not null)
            data.Add(new XAttribute("type", e.TypeName));
        if (e.MimeType is not null)
            data.Add(new XAttribute("mimetype", e.MimeType));

        data.Add(new XElement("value", e.Value));

        if (!string.IsNullOrEmpty(e.Comment))
            data.Add(new XElement("comment", e.Comment));

        return data;
    }
}
