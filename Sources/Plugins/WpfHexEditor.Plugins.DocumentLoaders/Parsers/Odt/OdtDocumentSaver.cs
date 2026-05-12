// ==========================================================
// Project: WpfHexEditor.Plugins.DocumentLoaders
// File: Parsers/Odt/OdtDocumentSaver.cs
// Description:
//     IDocumentSaver for ODT files.
//     Strategy: copy-modify — open the original ZIP, copy all entries
//     except "content.xml", then rebuild that entry using
//     OoXmlSchemaEngine.SerializeBlocks() driven by ODT.whfmt documentSchema.
// ==========================================================

using System.IO.Compression;
using System.Xml.Linq;
using WpfHexEditor.Editor.DocumentEditor.Core;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;
using WpfHexEditor.Editor.DocumentEditor.Core.Schema;

namespace WpfHexEditor.Plugins.DocumentLoaders.Parsers.Odt;

public sealed class OdtDocumentSaver : IDocumentSaver
{
    public string SaverName => "ODT Document Saver";

    public IReadOnlyList<string> SupportedExtensions { get; } = [".odt", ".ott"];

    public bool CanSave(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return ext.Equals(".odt", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".ott", StringComparison.OrdinalIgnoreCase);
    }

    public async Task SaveAsync(DocumentModel model, Stream output, CancellationToken ct = default)
    {
        var schema = DocumentSchemaReader.ReadFromWhfmt("ODT.whfmt");

        if (!File.Exists(model.FilePath))
            throw new FileNotFoundException("Original ODT file not found.", model.FilePath);

        using var outputMs   = new MemoryStream();

        bool anonymize = model.Metadata?.Extra is { } extra &&
                         extra.TryGetValue(DocumentMetadataExtraKeys.Anonymized, out var anon) && anon == "true";

        // Stream from disk rather than buffering — relevant for ODT with embedded media.
        await using var originalStream = File.OpenRead(model.FilePath!);
        using (var originalZip = new ZipArchive(originalStream, ZipArchiveMode.Read, leaveOpen: true))
        using (var outputZip   = new ZipArchive(outputMs,       ZipArchiveMode.Create, leaveOpen: true))
        {
            const string contentEntry = "content.xml";
            const string metaEntry    = "meta.xml";

            foreach (var entry in originalZip.Entries)
            {
                if (entry.FullName.Equals(contentEntry, StringComparison.OrdinalIgnoreCase))
                    continue;
                // Anonymize: replace meta.xml with a stripped version.
                if (anonymize && entry.FullName.Equals(metaEntry, StringComparison.OrdinalIgnoreCase))
                    continue;

                var newEntry = outputZip.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                newEntry.LastWriteTime = entry.LastWriteTime;
                await using var src = entry.Open();
                await using var dst = newEntry.Open();
                await src.CopyToAsync(dst, ct);
            }

            if (anonymize)
            {
                var metaWriter = outputZip.CreateEntry(metaEntry, CompressionLevel.Optimal);
                await using var ms = metaWriter.Open();
                await using var mw = new StreamWriter(ms, Utf8NoBom);
                await mw.WriteAsync(BuildAnonymizedMetaXml(model.Metadata?.Title ?? string.Empty)
                    .AsMemory(), ct);
            }

            string newXml = schema is not null
                ? OoXmlSchemaEngine.SerializeBlocks(model.Blocks, schema).ToString(SaveOptions.DisableFormatting)
                : FallbackSerialize(model);

            var docEntry = outputZip.CreateEntry(contentEntry, CompressionLevel.Optimal);
            await using var docStream = docEntry.Open();
            await using var writer   = new StreamWriter(docStream, Utf8NoBom);
            await writer.WriteAsync(newXml.AsMemory(), ct);
        }

        outputMs.Position = 0;
        await outputMs.CopyToAsync(output, ct);
    }

    private static readonly System.Text.UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Minimal anonymized meta.xml: keeps the title, drops initial-creator/
    /// creation-date/dc:date and any user-defined fields.
    /// </summary>
    private static string BuildAnonymizedMetaXml(string title)
    {
        string safeTitle = System.Security.SecurityElement.Escape(title) ?? string.Empty;
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <office:document-meta xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
                                  xmlns:meta="urn:oasis:names:tc:opendocument:xmlns:meta:1.0"
                                  xmlns:dc="http://purl.org/dc/elements/1.1/">
              <office:meta>
                <dc:title>{safeTitle}</dc:title>
              </office:meta>
            </office:document-meta>
            """;
    }

    private static string FallbackSerialize(DocumentModel model)
    {
        const string textNs = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
        var doc = new XElement(XName.Get("document-content", textNs),
            new XElement(XName.Get("body", textNs),
                new XElement(XName.Get("text", textNs),
                    model.Blocks.Select(b =>
                        new XElement(XName.Get("p", textNs), b.Text)))));
        return doc.ToString(SaveOptions.DisableFormatting);
    }
}
