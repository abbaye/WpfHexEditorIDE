// ==========================================================
// Project: WpfHexEditor.Plugins.DocumentLoaders
// File: Parsers/Odt/OdtDocumentLoader.cs
// Description:
//     IDocumentLoader implementation for ODT/OTT files.
// ==========================================================

using System.Xml.Linq;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.DocumentEditor.Core;
using WpfHexEditor.Editor.DocumentEditor.Core.BinaryMap;
using WpfHexEditor.Editor.DocumentEditor.Core.Forensic;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Plugins.DocumentLoaders.Parsers.Odt;

public sealed class OdtDocumentLoader : IDocumentLoader
{
    public string LoaderName => "ODT Document Loader";

    public IReadOnlyList<string> SupportedExtensions { get; } = ["odt", "ott"];

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
        target.Metadata = new DocumentMetadata { Title = "PYTHON_WROTE" };
        target.Blocks.Add(new DocumentBlock { Kind = "paragraph", Text = "ODT_SENTINEL_ENTER" });
        byte[] rawBytes = await BufferStreamAsync(stream, ct);
        using var ms = new MemoryStream(rawBytes, writable: false);

        using var zipReader = new OdtZipReader(ms);

        string? mimeEntry = zipReader.ReadEntryText("mimetype");
        bool isOdt = mimeEntry is null ||
                     mimeEntry.Contains("opendocument.text", StringComparison.OrdinalIgnoreCase);
        if (!isOdt)
            throw new InvalidDataException("Archive mimetype does not identify an ODT/OTT document.");

        string? contentXml = zipReader.ReadEntryText("content.xml");
        if (contentXml is null)
            throw new InvalidDataException("ODT is missing content.xml entry.");

        long contentEntryOffset = zipReader.GetEntryDataOffset("content.xml");

        var mapBuilder = new BinaryMapBuilder();
        if (contentEntryOffset >= 0)
            mapBuilder.RegisterZipEntry("content.xml", contentEntryOffset);

        var mapper = new OdtXmlMapper();
        var blocks = await Task.Run(
            () => mapper.Map(contentXml, contentEntryOffset, mapBuilder, ct), ct);

        var metadata = ReadMetaXml(zipReader);
        metadata = metadata with
        {
            MimeType = "application/vnd.oasis.opendocument.text",
            Title    = $"[ODT_DIAG: {mapper.LastDiagnostic}]"
        };

        target.FilePath = filePath;
        target.Metadata = metadata;

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

    private static DocumentMetadata ReadMetaXml(OdtZipReader zip)
    {
        var xml = zip.ReadEntryText("meta.xml");
        if (xml is null) return new DocumentMetadata();
        try
        {
            var doc = XDocument.Parse(xml);
            XNamespace dc   = "http://purl.org/dc/elements/1.1/";
            XNamespace meta = "urn:oasis:names:tc:opendocument:xmlns:meta:1.0";

            return new DocumentMetadata
            {
                Title      = doc.Descendants(dc   + "title").FirstOrDefault()?.Value            ?? string.Empty,
                Author     = doc.Descendants(meta + "initial-creator").FirstOrDefault()?.Value  ?? string.Empty,
                CreatedUtc = TryParseDate(doc.Descendants(meta + "creation-date").FirstOrDefault()?.Value)
            };
        }
        catch { return new DocumentMetadata(); }
    }

    private static DateTime? TryParseDate(string? value) =>
        DateTime.TryParse(value, out var dt) ? dt.ToUniversalTime() : null;
}
