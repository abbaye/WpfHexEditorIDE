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

        var blocks = await Task.Run(
            () => new DocxXmlMapper().Map(documentXml, docEntryOffset, mapBuilder, ct), ct);

        var metadata = ReadCoreProperties(zipReader);
        metadata = metadata with
        {
            MimeType  = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            HasMacros = zipReader.ReadEntryText("word/vbaProject.bin") is not null,
            Title     = !string.IsNullOrEmpty(metadata.Title)
                            ? metadata.Title
                            : Path.GetFileNameWithoutExtension(filePath)
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
}
