// ==========================================================
// Project: WpfHexEditor.Plugins.DocumentLoader.Odt
// File: Parsers/OdtDocumentLoader.cs
// Description:
//     IDocumentLoader implementation for ODT/OTT files.
//     Opens the ODF ZIP container, parses content.xml via
//     OdtXmlMapper, and resolves ZIP-relative offsets to absolute
//     file offsets in the BinaryMap.
// ==========================================================

using WpfHexEditor.Editor.DocumentEditor.Core.BinaryMap;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.DocumentEditor.Core.Forensic;
using System.Xml.Linq;
using WpfHexEditor.Editor.DocumentEditor.Core;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Plugins.DocumentLoader.Odt.Parsers;

/// <summary>
/// Loads ODT/OTT files into a <see cref="DocumentModel"/>.
/// </summary>
public sealed class OdtDocumentLoader : IDocumentLoader
{
    // ── IDocumentLoader ────────────────────────────────────────────────────

    public string LoaderName => "ODT Document Loader";

    public IReadOnlyList<string> SupportedExtensions { get; } =
        ["odt", "ott"];

    public bool CanLoad(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return ext is not null &&
               (ext.Equals(".odt", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".ott", StringComparison.OrdinalIgnoreCase));
    }

    public async Task LoadAsync(
        string            filePath,
        Stream            stream,
        DocumentModel     target,
        CancellationToken ct = default)
    {
        // ── 1. Buffer stream ──────────────────────────────────────────────
        byte[] rawBytes = await BufferStreamAsync(stream, ct);
        using var ms = new MemoryStream(rawBytes, writable: false);

        // ── 2. Open ZIP ───────────────────────────────────────────────────
        using var zipReader = new OdtZipReader(ms);

        // ── 3. Validate mimetype entry ────────────────────────────────────
        string? mimeEntry = zipReader.ReadEntryText("mimetype");
        bool isOdt = mimeEntry is null ||
                     mimeEntry.Contains("opendocument.text", StringComparison.OrdinalIgnoreCase);
        if (!isOdt)
            throw new InvalidDataException("Archive mimetype does not identify an ODT/OTT document.");

        // ── 4. Read content.xml ───────────────────────────────────────────
        string? contentXml = zipReader.ReadEntryText("content.xml");
        if (contentXml is null)
            throw new InvalidDataException("ODT is missing content.xml entry.");

        long contentEntryOffset = zipReader.GetEntryDataOffset("content.xml");

        // ── 5. Map XML → blocks ───────────────────────────────────────────
        var mapBuilder = new BinaryMapBuilder();
        if (contentEntryOffset >= 0)
            mapBuilder.RegisterZipEntry("content.xml", contentEntryOffset);

        var mapper = new OdtXmlMapper();
        var blocks = await Task.Run(
            () => mapper.Map(contentXml, contentEntryOffset, mapBuilder, ct), ct);

        // ── 6. Metadata from meta.xml ─────────────────────────────────────
        var metadata = ReadMetaXml(zipReader);
        metadata = metadata with
        {
            MimeType = "application/vnd.oasis.opendocument.text"
        };

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

    private static DocumentMetadata ReadMetaXml(OdtZipReader zip)
    {
        var xml = zip.ReadEntryText("meta.xml");
        if (xml is null) return new DocumentMetadata();

        try
        {
            var doc = System.Xml.Linq.XDocument.Parse(xml);
            XNamespace dc    = "http://purl.org/dc/elements/1.1/";
            XNamespace meta  = "urn:oasis:names:tc:opendocument:xmlns:meta:1.0";

            var title   = doc.Descendants(dc   + "title").FirstOrDefault()?.Value;
            var author  = doc.Descendants(meta + "initial-creator").FirstOrDefault()?.Value;
            var created = doc.Descendants(meta + "creation-date").FirstOrDefault()?.Value;

            return new DocumentMetadata
            {
                Title      = title  ?? string.Empty,
                Author     = author ?? string.Empty,
                CreatedUtc = TryParseDate(created)
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
