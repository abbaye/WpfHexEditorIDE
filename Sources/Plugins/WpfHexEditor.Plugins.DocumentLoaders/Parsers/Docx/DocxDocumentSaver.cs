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
        bool anonymize   = HasFlag(model, DocumentMetadataExtraKeys.Anonymized);
        bool stripMacros = anonymize && HasFlag(model, DocumentMetadataExtraKeys.MacrosRemoved);

        using var outputMs = new MemoryStream();

        using (var outputZip = new ZipArchive(outputMs, ZipArchiveMode.Create, leaveOpen: true))
        {
            const string documentEntry  = "word/document.xml";
            const string corePropsEntry = "docProps/core.xml";

            if (hasOriginal)
            {
                // Stream from disk rather than buffering the entire archive into memory
                // — relevant for large DOCX/DOCM with embedded media.
                await using var originalStream = File.OpenRead(model.FilePath!);
                using var originalZip = new ZipArchive(originalStream, ZipArchiveMode.Read, leaveOpen: true);

                foreach (var entry in originalZip.Entries)
                {
                    if (ShouldSkipEntry(entry.FullName, documentEntry, anonymize, stripMacros))
                        continue;

                    var newEntry = outputZip.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                    newEntry.LastWriteTime = entry.LastWriteTime;
                    await using var src = entry.Open();
                    await using var dst = newEntry.Open();
                    await src.CopyToAsync(dst, ct);
                }

                if (anonymize)
                    WriteEntry(outputZip, corePropsEntry, BuildAnonymizedCoreProps(model.Metadata?.Title ?? string.Empty));
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
            // OOXML spec requires UTF-8 without BOM. Default StreamWriter would emit a BOM.
            await using var writer   = new StreamWriter(docStream, Utf8NoBom);
            await writer.WriteAsync(newXml.AsMemory(), ct);
        }

        outputMs.Position = 0;
        await outputMs.CopyToAsync(output, ct);
    }

    private static readonly System.Text.UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

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

    private static bool HasFlag(DocumentModel model, string key) =>
        model.Metadata?.Extra is { } extra &&
        extra.TryGetValue(key, out var v) && v == "true";

    /// <summary>
    /// Returns true when <paramref name="entryName"/> should not be copied to
    /// the output archive. Skips the document entry (always rewritten), the
    /// docProps when anonymizing, and vbaProject.bin when macros are stripped.
    /// </summary>
    private static bool ShouldSkipEntry(string entryName, string documentEntry,
        bool anonymize, bool stripMacros)
    {
        if (entryName.Equals(documentEntry, StringComparison.OrdinalIgnoreCase)) return true;
        if (!anonymize) return false;
        if (entryName.Equals("docProps/core.xml", StringComparison.OrdinalIgnoreCase)) return true;
        if (entryName.Equals("docProps/app.xml",  StringComparison.OrdinalIgnoreCase)) return true;
        if (stripMacros &&
            entryName.Equals("word/vbaProject.bin", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>
    /// Builds a minimal anonymized docProps/core.xml: keeps the title only,
    /// drops creator/lastModifiedBy/created/modified.
    /// </summary>
    private static string BuildAnonymizedCoreProps(string title)
    {
        string safeTitle = System.Security.SecurityElement.Escape(title) ?? string.Empty;
        return $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties"
                               xmlns:dc="http://purl.org/dc/elements/1.1/"
                               xmlns:dcterms="http://purl.org/dc/terms/"
                               xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <dc:title>{safeTitle}</dc:title>
            </cp:coreProperties>
            """;
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
