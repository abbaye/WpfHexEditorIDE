// ==========================================================
// Project: WpfHexEditor.Plugins.DocumentLoaders
// File: Parsers/Docx/DocxDocumentLoader.cs
// Description:
//     IDocumentLoader for DOCX/DOCM/DOTX/DOTM files.
//     All four are OOXML wordprocessingml — same parser, same
//     cascade. DOCM/DOTM differ only by presence of vbaProject.bin
//     (macro flag); DOTX/DOTM differ only by template MIME type.
// ==========================================================

using System.Xml.Linq;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.DocumentEditor.Core;
using WpfHexEditor.Editor.DocumentEditor.Core.BinaryMap;
using WpfHexEditor.Editor.DocumentEditor.Core.Forensic;
using WpfHexEditor.Editor.DocumentEditor.Core.Helpers;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;
using WpfHexEditor.Editor.DocumentEditor.Core.Options;

namespace WpfHexEditor.Plugins.DocumentLoaders.Parsers.Docx;

public sealed class DocxDocumentLoader : IDocumentLoader
{
    public string LoaderName => "DOCX Document Loader";

    public IReadOnlyList<string> SupportedExtensions { get; } = ["docx", "docm", "dotx", "dotm"];

    public bool CanLoad(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        if (ext is null) return false;
        return ext.Equals(".docx", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".docm", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".dotx", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".dotm", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Maps the file extension to the canonical OOXML MIME type.</summary>
    private static string GetMimeType(string filePath) =>
        Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".docm" => "application/vnd.ms-word.document.macroEnabled.12",
            ".dotx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.template",
            ".dotm" => "application/vnd.ms-word.template.macroEnabled.12",
            _       => "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        };

    private static bool IsTemplate(string filePath) =>
        Path.GetExtension(filePath).Equals(".dotx", StringComparison.OrdinalIgnoreCase) ||
        Path.GetExtension(filePath).Equals(".dotm", StringComparison.OrdinalIgnoreCase);

    public async Task LoadAsync(
        string            filePath,
        Stream            stream,
        DocumentModel     target,
        CancellationToken ct = default)
    {
        byte[] rawBytes = await DocumentLoaderHelpers.BufferStreamAsync(stream, ct);
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

        // Parse word/styles.xml once: docDefaults + pStyle/rStyle inheritance
        // (basedOn chains flattened) so the mapper can apply resolved style
        // attributes during traversal without re-walking the graph per node.
        var styleTable = DocxStyleTable.Parse(zipReader.ReadEntryText("word/styles.xml"));

        var mapper = new DocxXmlMapper(relsMap, styleTable);
        var blocks = await Task.Run(
            () => mapper.Map(documentXml, docEntryOffset, mapBuilder, ct), ct);

        // Apply docDefaults (Cambria 11pt etc.) only to text-bearing blocks that
        // don't already declare a font/size — paragraphs, headings, runs, etc.
        // Skip structural kinds (image, page-break, header, footer, table-row)
        // so the renderer doesn't pick up a font on something that should not
        // be drawn as text.
        if (styleTable.DefaultFont is not null || styleTable.DefaultSizePt is not null)
            ApplyTextDefaults(blocks, styleTable.DefaultFont, styleTable.DefaultSizePt);

        var metadata = ReadCoreProperties(zipReader);
        bool hasMacros = zipReader.ReadEntryText("word/vbaProject.bin") is not null;
        metadata = metadata with
        {
            MimeType  = GetMimeType(filePath),
            HasMacros = hasMacros,
            Title     = !string.IsNullOrEmpty(metadata.Title)
                            ? metadata.Title
                            : Path.GetFileNameWithoutExtension(filePath)
        };
        if (IsTemplate(filePath))
            metadata.Extra[DocumentMetadataExtraKeys.IsTemplate] = "true";

        target.FilePath    = filePath;
        target.Metadata    = metadata;
        target.PageSettings = ReadDocxPageSettings(documentXml);

        foreach (var block in blocks)
            target.Blocks.Add(block);

        target.BinaryMap.MergeFrom(mapBuilder.Build());

        var alerts = new ForensicAnalyzer().Analyze(target, rawBytes);
        target.SetForensicAlerts(alerts);
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
                CreatedUtc  = DocumentLoaderHelpers.TryParseDate(doc.Descendants(dcterms + "created").FirstOrDefault()?.Value),
                ModifiedUtc = DocumentLoaderHelpers.TryParseDate(doc.Descendants(dcterms + "modified").FirstOrDefault()?.Value)
            };
        }
        catch { return new DocumentMetadata(); }
    }

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

    /// <summary>
    /// Writes <c>fontFamily</c>/<c>fontSize</c> only on text-bearing block kinds
    /// that don't already declare them. Structural kinds (image, page-break,
    /// header, footer, table-row, table) are left untouched so the renderer
    /// doesn't apply a typeface to something that isn't drawn as text.
    /// </summary>
    private static void ApplyTextDefaults(IEnumerable<DocumentBlock> blocks,
        string? defFont, double? defSize)
    {
        foreach (var b in blocks)
        {
            bool isText = b.Kind is "paragraph" or "heading" or "list-item"
                                 or "run" or "hyperlink" or "structured-tag";
            if (isText)
            {
                if (defFont is not null && !b.Attributes.ContainsKey("fontFamily"))
                    b.Attributes["fontFamily"] = defFont;
                if (defSize.HasValue && !b.Attributes.ContainsKey("fontSize"))
                    b.Attributes["fontSize"]   = defSize.Value;
            }
            if (b.Children.Count > 0)
                ApplyTextDefaults(b.Children, defFont, defSize);
        }
    }
}
