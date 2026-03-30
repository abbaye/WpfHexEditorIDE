// ==========================================================
// Project: WpfHexEditor.Plugins.DocumentLoader.Docx
// File: Parsers/DocxDocumentLoader.cs
// Description:
//     IDocumentLoader implementation for DOCX/DOTX files.
//     Opens the ZIP container, parses word/document.xml via
//     DocxXmlMapper, and resolves ZIP-relative offsets to absolute
//     file offsets in the BinaryMap.
// ==========================================================

using WpfHexEditor.Editor.DocumentEditor.Core.BinaryMap;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.DocumentEditor.Core.Forensic;
using System.Xml.Linq;
using WpfHexEditor.Editor.DocumentEditor.Core;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Plugins.DocumentLoader.Docx.Parsers;

/// <summary>
/// Loads DOCX/DOTX files into a <see cref="DocumentModel"/>.
/// </summary>
public sealed class DocxDocumentLoader : IDocumentLoader
{
    // ── IDocumentLoader ────────────────────────────────────────────────────

    public string LoaderName => "DOCX Document Loader";

    public IReadOnlyList<string> SupportedExtensions { get; } =
        ["docx", "dotx"];

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
        // ── 1. Buffer the stream so we can seek for offset resolution ─────
        byte[] rawBytes = await BufferStreamAsync(stream, ct);
        using var ms = new MemoryStream(rawBytes, writable: false);

        // ── 2. Open ZIP ───────────────────────────────────────────────────
        using var zipReader = new DocxZipReader(ms);

        // ── 3. Read document.xml ──────────────────────────────────────────
        string? documentXml = zipReader.ReadEntryText("word/document.xml");
        if (documentXml is null)
            throw new InvalidDataException("DOCX is missing word/document.xml entry.");

        long docEntryOffset = zipReader.GetEntryDataOffset("word/document.xml");

        // ── 4. Map XML → blocks + binary map ─────────────────────────────
        var mapBuilder = new BinaryMapBuilder();
        if (docEntryOffset >= 0)
            mapBuilder.RegisterZipEntry("word/document.xml", docEntryOffset);

        var mapper = new DocxXmlMapper();
        var blocks = await Task.Run(
            () => mapper.Map(documentXml, docEntryOffset, mapBuilder, ct), ct);

        // ── 5. Metadata from core properties ─────────────────────────────
        var metadata = ReadCoreProperties(zipReader);
        metadata = metadata with { MimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document" };

        // ── 6. Check for macros (forensic) ───────────────────────────────
        bool hasMacros = zipReader.ReadEntryText("word/vbaProject.bin") is not null;
        metadata = metadata with { HasMacros = hasMacros };

        // ── 7. Populate model ─────────────────────────────────────────────
        target.FilePath = filePath;
        target.Metadata = metadata;

        foreach (var block in blocks)
            target.Blocks.Add(block);

        var binaryMap = mapBuilder.Build();
        target.BinaryMap.MergeFrom(binaryMap);

        // ── 8. Forensic analysis ──────────────────────────────────────────
        var analyzer = new ForensicAnalyzer();
        var alerts   = analyzer.Analyze(target, rawBytes);
        target.SetForensicAlerts(alerts);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

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
            var doc   = System.Xml.Linq.XDocument.Parse(xml);
            XNamespace dc  = "http://purl.org/dc/elements/1.1/";
            XNamespace cp  = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";
            XNamespace dcterms = "http://purl.org/dc/terms/";

            var title   = doc.Descendants(dc  + "title").FirstOrDefault()?.Value;
            var author  = doc.Descendants(dc  + "creator").FirstOrDefault()?.Value;
            var created = doc.Descendants(dcterms + "created").FirstOrDefault()?.Value;
            var modified= doc.Descendants(dcterms + "modified").FirstOrDefault()?.Value;

            return new DocumentMetadata
            {
                Title       = title   ?? string.Empty,
                Author      = author  ?? string.Empty,
                CreatedUtc  = TryParseDate(created),
                ModifiedUtc = TryParseDate(modified)
            };
        }
        catch
        {
            return new DocumentMetadata();
        }
    }

    private static DateTime? TryParseDate(string? value) =>
        DateTime.TryParse(value, out var dt) ? dt.ToUniversalTime() : null;
}
