// ==========================================================
// Project: WpfHexEditor.Editor.ResxEditor
// File: Services/ResxDocumentParser.cs
// Description:
//     Parses a .resx/.resw file from disk into a ResxDocument.
//     Uses XDocument with LoadOptions.PreserveWhitespace so the
//     serializer can perform a lossless round-trip.
// ==========================================================

using System.Xml.Linq;
using System.IO;
using WpfHexEditor.Editor.ResxEditor.Models;

namespace WpfHexEditor.Editor.ResxEditor.Services;

/// <summary>Parses a <c>.resx</c> or <c>.resw</c> file into a <see cref="ResxDocument"/>.</summary>
public static class ResxDocumentParser
{
    private static readonly XNamespace XmlNs = "http://www.w3.org/XML/1998/namespace";

    /// <summary>
    /// Loads and parses the file at <paramref name="filePath"/>.
    /// Throws on malformed XML — callers should catch and surface via IDiagnosticSource.
    /// </summary>
    public static async Task<ResxDocument> ParseAsync(string filePath, CancellationToken ct = default)
    {
        var xml = await File.ReadAllTextAsync(filePath, ct);
        return ParseXml(filePath, xml);
    }

    /// <summary>Parses an in-memory XML string (used by IBufferAwareEditor sync path).</summary>
    public static ResxDocument ParseXml(string filePath, string xml)
    {
        var xdoc    = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        var entries = ParseEntries(xdoc);
        return new ResxDocument(filePath, entries, xdoc);
    }

    // ------------------------------------------------------------------

    private static IReadOnlyList<ResxEntry> ParseEntries(XDocument xdoc)
    {
        var root = xdoc.Root;
        if (root is null) return [];

        var entries = new List<ResxEntry>();

        foreach (var data in root.Elements("data"))
        {
            var name     = (string?)data.Attribute("name")              ?? string.Empty;
            var typeName = (string?)data.Attribute("type");
            var mimeType = (string?)data.Attribute("mimetype");
            var space    = (string?)data.Attribute(XmlNs + "space");
            var value    = (string?)data.Element("value")               ?? string.Empty;
            var comment  = (string?)data.Element("comment")             ?? string.Empty;

            entries.Add(new ResxEntry(name, value, comment, typeName, mimeType, space));
        }

        return entries;
    }
}
