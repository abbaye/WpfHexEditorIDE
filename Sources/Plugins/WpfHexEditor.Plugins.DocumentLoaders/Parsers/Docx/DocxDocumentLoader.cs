// ==========================================================
// Project: WpfHexEditor.Plugins.DocumentLoaders
// File: Parsers/Docx/DocxDocumentLoader.cs
// Description:
//     IDocumentLoader for DOCX/DOTX files.
// ==========================================================

using System.Xml.Linq;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.DocumentEditor.Core;
using WpfHexEditor.Editor.DocumentEditor.Core.BinaryMap;
using WpfHexEditor.Editor.DocumentEditor.Core.Forensic;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;
using WpfHexEditor.Editor.DocumentEditor.Core.Options;

namespace WpfHexEditor.Plugins.DocumentLoaders.Parsers.Docx;

public sealed class DocxDocumentLoader : IDocumentLoader
{
    public string LoaderName => "DOCX Document Loader";

    public IReadOnlyList<string> SupportedExtensions { get; } = ["docx", "dotx"];

    public bool CanLoad(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return ext is not null &&
               (ext.Equals(".docx", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".dotx", StringComparison.OrdinalIgnoreCase));
    }

    public async Task LoadAsync(
        string            filePath,
        Stream            stream,
        DocumentModel     target,
        CancellationToken ct = default)
    {
        byte[] rawBytes = await BufferStreamAsync(stream, ct);
        using var ms = new MemoryStream(rawBytes, writable: false);

        using var zipReader = new DocxZipReader(ms);

        string? documentXml = zipReader.ReadEntryText("word/document.xml");
        if (documentXml is null)
            throw new InvalidDataException("DOCX is missing word/document.xml entry.");

        long docEntryOffset = zipReader.GetEntryDataOffset("word/document.xml");

        var mapBuilder = new BinaryMapBuilder();
        if (docEntryOffset >= 0)
            mapBuilder.RegisterZipEntry("word/document.xml", docEntryOffset);

        // Build relationship map: rId → zip entry path (needed to resolve image paths)
        var relsMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var relsXml = zipReader.ReadEntryText("word/_rels/document.xml.rels");
        if (relsXml is not null)
        {
            try
            {
                var relsDoc = XDocument.Parse(relsXml);
                var relNs   = XNamespace.Get("http://schemas.openxmlformats.org/package/2006/relationships");
                foreach (var rel in relsDoc.Descendants(relNs + "Relationship"))
                {
                    var id  = rel.Attribute("Id")?.Value;
                    var tgt = rel.Attribute("Target")?.Value;
                    if (id is not null && tgt is not null)
                        relsMap[id] = tgt.StartsWith('/') ? tgt[1..] : $"word/{tgt}";
                }
            }
            catch { /* best-effort — missing rels just means no images */ }
        }

        var mapper = new DocxXmlMapper(relsMap);
        var blocks = await Task.Run(
            () => mapper.Map(documentXml, docEntryOffset, mapBuilder, ct), ct);

        var metadata = ReadCoreProperties(zipReader);
        metadata = metadata with
        {
            MimeType  = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            HasMacros = zipReader.ReadEntryText("word/vbaProject.bin") is not null,
            Title     = !string.IsNullOrEmpty(metadata.Title)
                            ? metadata.Title
                            : Path.GetFileNameWithoutExtension(filePath)
        };

        target.FilePath    = filePath;
        target.Metadata    = metadata;
        target.PageSettings = ReadDocxPageSettings(documentXml);

        foreach (var block in blocks)
            target.Blocks.Add(block);

        target.BinaryMap.MergeFrom(mapBuilder.Build());

        var alerts = new ForensicAnalyzer().Analyze(target, rawBytes);
        target.SetForensicAlerts(alerts);
    }

    private static async Task<byte[]> BufferStreamAsync(Stream stream, CancellationToken ct)
    {
        if (stream is MemoryStream ms && ms.TryGetBuffer(out _))
            return ms.ToArray();
        using var buf = new MemoryStream();
        await stream.CopyToAsync(buf, ct);
        return buf.ToArray();
    }

    private static DocumentMetadata ReadCoreProperties(DocxZipReader zip)
    {
        var xml = zip.ReadEntryText("docProps/core.xml");
        if (xml is null) return new DocumentMetadata();
        try
        {
            var doc      = XDocument.Parse(xml);
            XNamespace dc      = "http://purl.org/dc/elements/1.1/";
            XNamespace dcterms = "http://purl.org/dc/terms/";

            return new DocumentMetadata
            {
                Title       = doc.Descendants(dc      + "title").FirstOrDefault()?.Value   ?? string.Empty,
                Author      = doc.Descendants(dc      + "creator").FirstOrDefault()?.Value ?? string.Empty,
                CreatedUtc  = TryParseDate(doc.Descendants(dcterms + "created").FirstOrDefault()?.Value),
                ModifiedUtc = TryParseDate(doc.Descendants(dcterms + "modified").FirstOrDefault()?.Value)
            };
        }
        catch { return new DocumentMetadata(); }
    }

    private static DateTime? TryParseDate(string? value) =>
        DateTime.TryParse(value, out var dt) ? dt.ToUniversalTime() : null;

    private static DocumentPageSettings? ReadDocxPageSettings(string documentXml)
    {
        try
        {
            XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var doc    = XDocument.Parse(documentXml);
            var sectPr = doc.Descendants(W + "sectPr").FirstOrDefault();
            if (sectPr is null) return null;

            var pgSz  = sectPr.Element(W + "pgSz");
            var pgMar = sectPr.Element(W + "pgMar");
            if (pgSz is null) return null;

            int pgW = ParseTwips(pgSz.Attribute(W + "w")?.Value);
            int pgH = ParseTwips(pgSz.Attribute(W + "h")?.Value);
            if (pgW <= 0 || pgH <= 0) return null;

            int? orient = pgSz.Attribute(W + "orient")?.Value == "landscape" ? 1 : null;
            int mTop    = pgMar is null ? 720  : ParseTwips(pgMar.Attribute(W + "top")?.Value);
            int mBot    = pgMar is null ? 1080 : ParseTwips(pgMar.Attribute(W + "bottom")?.Value);
            int mLeft   = pgMar is null ? 1080 : ParseTwips(pgMar.Attribute(W + "left")?.Value);
            int mRight  = pgMar is null ? 1080 : ParseTwips(pgMar.Attribute(W + "right")?.Value);
            int mGutter = pgMar is null ? 0    : ParseTwips(pgMar.Attribute(W + "gutter")?.Value);

            return DocumentPageSettings.FromDocx(pgW, pgH, orient, mTop, mBot, mLeft, mRight, mGutter);
        }
        catch { return null; }
    }

    private static int ParseTwips(string? s) =>
        int.TryParse(s, out var v) ? v : 0;
}
