// ==========================================================
// Project: WpfHexEditor.Plugins.DocumentLoaders
// File: Parsers/Odt/FlatOdtDocumentLoader.cs
// Description:
//     IDocumentLoader for FlatODT (.fodt / .fott) — the
//     single-file XML variant of OpenDocument Text. All
//     sections (office:meta, office:styles, office:automatic-
//     styles, office:body) are inlined into one document
//     instead of being zipped as separate entries.
// Architecture notes:
//     Reuses OdtXmlMapper and the LoadOdtStylesRawFromXml +
//     FlattenOdtStyles helpers exposed by OdtDocumentLoader.
//     Cascade equivalence with ODT is by construction.
// ==========================================================

using System.Xml.Linq;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.DocumentEditor.Core;
using WpfHexEditor.Editor.DocumentEditor.Core.BinaryMap;
using WpfHexEditor.Editor.DocumentEditor.Core.Forensic;
using WpfHexEditor.Editor.DocumentEditor.Core.Helpers;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;
using WpfHexEditor.Editor.DocumentEditor.Core.Options;

namespace WpfHexEditor.Plugins.DocumentLoaders.Parsers.Odt;

public sealed class FlatOdtDocumentLoader : IDocumentLoader
{
    private static readonly XNamespace OfficeNs = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
    private static readonly XNamespace MetaNs   = "urn:oasis:names:tc:opendocument:xmlns:meta:1.0";
    private static readonly XNamespace DcNs     = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace StyleNs  = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
    private static readonly XNamespace FoNs     = "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0";

    public string LoaderName => "FlatODT Document Loader";

    public IReadOnlyList<string> SupportedExtensions { get; } = ["fodt", "fott"];

    public bool CanLoad(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        if (ext is null) return false;
        return ext.Equals(".fodt", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".fott", StringComparison.OrdinalIgnoreCase);
    }

    public async Task LoadAsync(
        string            filePath,
        Stream            stream,
        DocumentModel     target,
        CancellationToken ct = default)
    {
        byte[] rawBytes = await DocumentLoaderHelpers.BufferStreamAsync(stream, ct);
        string xml      = System.Text.Encoding.UTF8.GetString(rawBytes);

        // Strip UTF-8 BOM if present (XDocument.Parse rejects it).
        if (xml.Length > 0 && xml[0] == '﻿') xml = xml[1..];

        XDocument doc;
        try { doc = XDocument.Parse(xml, LoadOptions.SetLineInfo); }
        catch (Exception ex) { throw new InvalidDataException("FlatODT XML is malformed.", ex); }

        // Validate root: <office:document>
        var root = doc.Root;
        if (root is null || root.Name.Namespace != OfficeNs || root.Name.LocalName != "document")
            throw new InvalidDataException("FlatODT root element must be <office:document>.");

        // ── Style table ─────────────────────────────────────────────────────────
        // Both <office:styles> and <office:automatic-styles> are descendants of
        // the same root, so a single LoadOdtStylesRawFromXml pass picks both up.
        var rawStyles  = new Dictionary<string, OdtDocumentLoader.OdtRawStyle>(StringComparer.OrdinalIgnoreCase);
        OdtDocumentLoader.LoadOdtStylesRawFromXml(xml, rawStyles);
        var styleProps = OdtDocumentLoader.FlattenOdtStyles(rawStyles);

        // ── Map content ─────────────────────────────────────────────────────────
        var mapBuilder = new BinaryMapBuilder();
        var mapper     = new OdtXmlMapper(styleProps);
        var blocks     = await Task.Run(
            () => mapper.Map(xml, entryBaseOffset: 0, mapBuilder, ct), ct);

        // ── Metadata + page settings ───────────────────────────────────────────
        var metadata = ReadMeta(doc);
        metadata = metadata with
        {
            MimeType = "application/vnd.oasis.opendocument.text-flat-xml",
            Title    = !string.IsNullOrEmpty(metadata.Title)
                           ? metadata.Title
                           : Path.GetFileNameWithoutExtension(filePath)
        };

        target.FilePath     = filePath;
        target.Metadata     = metadata;
        target.PageSettings = ReadPageSettings(doc);

        foreach (var block in blocks)
            target.Blocks.Add(block);

        target.BinaryMap.MergeFrom(mapBuilder.Build());

        var alerts = new ForensicAnalyzer().Analyze(target, rawBytes);
        target.SetForensicAlerts(alerts);
    }


    private static DocumentMetadata ReadMeta(XDocument doc)
    {
        try
        {
            var meta = doc.Descendants(OfficeNs + "meta").FirstOrDefault();
            if (meta is null) return new DocumentMetadata();

            return new DocumentMetadata
            {
                Title      = meta.Element(DcNs   + "title")?.Value           ?? string.Empty,
                Author     = meta.Element(MetaNs + "initial-creator")?.Value ?? string.Empty,
                CreatedUtc = DocumentLoaderHelpers.TryParseDate(meta.Element(MetaNs + "creation-date")?.Value)
            };
        }
        catch { return new DocumentMetadata(); }
    }


    private static DocumentPageSettings? ReadPageSettings(XDocument doc)
    {
        try
        {
            var props = doc.Descendants(StyleNs + "page-layout-properties").FirstOrDefault();
            if (props is null) return null;

            string? pgW = props.Attribute(FoNs + "page-width")?.Value;
            string? pgH = props.Attribute(FoNs + "page-height")?.Value;
            if (pgW is null || pgH is null) return null;

            return DocumentPageSettings.FromOdt(
                pgW, pgH,
                props.Attribute(FoNs + "margin-top")?.Value,
                props.Attribute(FoNs + "margin-bottom")?.Value,
                props.Attribute(FoNs + "margin-left")?.Value,
                props.Attribute(FoNs + "margin-right")?.Value);
        }
        catch { return null; }
    }
}
