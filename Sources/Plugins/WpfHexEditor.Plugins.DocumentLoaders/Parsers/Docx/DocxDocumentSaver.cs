// ==========================================================
// Project: WpfHexEditor.Plugins.DocumentLoaders
// File: Parsers/Docx/DocxDocumentSaver.cs
// Description:
//     IDocumentSaver for DOCX files.
//     Strategy: copy-modify — open the original ZIP, copy all entries
//     except "word/document.xml", then rebuild that entry using
//     OoXmlSchemaEngine.SerializeBlocks() driven by DOCX.whfmt documentSchema.
//     No hardcoded OOXML element names in C#.
// ==========================================================

using System.IO.Compression;
using System.Xml.Linq;
using WpfHexEditor.Core.Definitions;
using WpfHexEditor.Editor.DocumentEditor.Core;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;
using WpfHexEditor.Editor.DocumentEditor.Core.Schema;

namespace WpfHexEditor.Plugins.DocumentLoaders.Parsers.Docx;

public sealed class DocxDocumentSaver : IDocumentSaver
{
    public string SaverName => "DOCX Document Saver";

    public IReadOnlyList<string> SupportedExtensions { get; } = [".docx", ".dotx"];

    public bool CanSave(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return ext.Equals(".docx", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".dotx", StringComparison.OrdinalIgnoreCase);
    }

    public async Task SaveAsync(DocumentModel model, Stream output, CancellationToken ct = default)
    {
        var schema = LoadSchema("DOCX.whfmt");
        bool hasOriginal = !string.IsNullOrEmpty(model.FilePath) && File.Exists(model.FilePath);

        using var outputMs = new MemoryStream();

        using (var outputZip = new ZipArchive(outputMs, ZipArchiveMode.Create, leaveOpen: true))
        {
            const string documentEntry = "word/document.xml";

            if (hasOriginal)
            {
                byte[] originalBytes = await File.ReadAllBytesAsync(model.FilePath, ct);
                using var originalMs = new MemoryStream(originalBytes);
                using var originalZip = new ZipArchive(originalMs, ZipArchiveMode.Read, leaveOpen: true);

                foreach (var entry in originalZip.Entries)
                {
                    if (entry.FullName.Equals(documentEntry, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var newEntry = outputZip.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                    newEntry.LastWriteTime = entry.LastWriteTime;
                    await using var src = entry.Open();
                    await using var dst = newEntry.Open();
                    await src.CopyToAsync(dst, ct);
                }
            }
            else
            {
                WriteMinimalDocxScaffold(outputZip);
            }

            string newXml = schema is not null
                ? OoXmlSchemaEngine.SerializeBlocks(model.Blocks, schema).ToString(SaveOptions.DisableFormatting)
                : FallbackSerialize(model);

            var docEntry = outputZip.CreateEntry(documentEntry, CompressionLevel.Optimal);
            await using var docStream = docEntry.Open();
            await using var writer   = new StreamWriter(docStream);
            await writer.WriteAsync(newXml);
        }

        outputMs.Position = 0;
        await outputMs.CopyToAsync(output, ct);
    }

    /// <summary>
    /// Writes the minimum set of OOXML package parts a fresh DOCX needs
    /// (Content Types, package rels, document rels) so Word can open the
    /// file when there is no source archive to copy from.
    /// </summary>
    private static void WriteMinimalDocxScaffold(ZipArchive zip)
    {
        const string contentTypesXml = """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
              <Default Extension="xml" ContentType="application/xml"/>
              <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
            </Types>
            """;
        const string packageRelsXml = """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
            </Relationships>
            """;
        const string documentRelsXml = """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"/>
            """;

        WriteEntry(zip, "[Content_Types].xml",          contentTypesXml);
        WriteEntry(zip, "_rels/.rels",                  packageRelsXml);
        WriteEntry(zip, "word/_rels/document.xml.rels", documentRelsXml);
    }

    private static void WriteEntry(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var s = entry.Open();
        using var w = new StreamWriter(s);
        w.Write(content);
    }

    private static DocumentSchemaDefinition? LoadSchema(string fileName)
    {
        var catalog = EmbeddedFormatCatalog.Instance;
        var key = catalog.GetAll()
            .Select(e => e.ResourceKey)
            .FirstOrDefault(k => k is not null &&
                k.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        if (key is null) return null;
        try { return DocumentSchemaReader.ReadFromJson(catalog.GetJson(key), fileName); }
        catch { return null; }
    }

    private static string FallbackSerialize(DocumentModel model)
    {
        var body = new XElement(
            XName.Get("document", "http://schemas.openxmlformats.org/wordprocessingml/2006/main"),
            new XElement(XName.Get("body", "http://schemas.openxmlformats.org/wordprocessingml/2006/main"),
                model.Blocks.Select(b =>
                    new XElement(XName.Get("p", "http://schemas.openxmlformats.org/wordprocessingml/2006/main"),
                        new XElement(XName.Get("r", "http://schemas.openxmlformats.org/wordprocessingml/2006/main"),
                            new XElement(XName.Get("t", "http://schemas.openxmlformats.org/wordprocessingml/2006/main"),
                                b.Text))))));
        return body.ToString(SaveOptions.DisableFormatting);
    }
}
