// ==========================================================
// Project: WpfHexEditor.Plugins.DocumentLoaders
// File: Parsers/Epub/EpubDocumentLoader.cs
// Description:
//     IDocumentLoader for EPUB 2.x / 3.x files.
//     EPUB = ZIP archive whose META-INF/container.xml points
//     to a .opf manifest. The OPF lists XHTML spine items in
//     reading order; we concatenate them into the DocumentModel
//     as paragraphs/headings/lists for visual review.
// Architecture notes:
//     XHTML → DocumentBlock mapping is intentionally coarse
//     (paragraph/heading/list-item/run) — DocumentEditor's
//     renderer doesn't need full HTML fidelity for forensic
//     read-only review of ebooks.
// ==========================================================

using System.IO.Compression;
using System.Xml.Linq;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.DocumentEditor.Core;
using WpfHexEditor.Editor.DocumentEditor.Core.BinaryMap;
using WpfHexEditor.Editor.DocumentEditor.Core.Forensic;
using WpfHexEditor.Editor.DocumentEditor.Core.Helpers;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Plugins.DocumentLoaders.Parsers.Epub;

public sealed class EpubDocumentLoader : IDocumentLoader
{
    private static readonly XNamespace ContainerNs = "urn:oasis:names:tc:opendocument:xmlns:container";
    private static readonly XNamespace OpfNs       = "http://www.idpf.org/2007/opf";
    private static readonly XNamespace DcNs        = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace XhtmlNs     = "http://www.w3.org/1999/xhtml";

    public string LoaderName => "EPUB Document Loader";

    public IReadOnlyList<string> SupportedExtensions { get; } = ["epub"];

    public bool CanLoad(string filePath) =>
        Path.GetExtension(filePath).Equals(".epub", StringComparison.OrdinalIgnoreCase);

    public async Task LoadAsync(
        string            filePath,
        Stream            stream,
        DocumentModel     target,
        CancellationToken ct = default)
    {
        byte[] rawBytes = await DocumentLoaderHelpers.BufferStreamAsync(stream, ct);
        using var ms = new MemoryStream(rawBytes, writable: false);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: true);

        string opfPath = ResolveOpfPath(zip)
            ?? throw new InvalidDataException("EPUB is missing META-INF/container.xml or rootfile reference.");

        var (metadata, spineHrefs) = ParseOpf(zip, opfPath, filePath);
        target.FilePath = filePath;
        target.Metadata = metadata with { MimeType = "application/epub+zip" };

        var mapBuilder = new BinaryMapBuilder();
        string opfDir  = GetParentPath(opfPath);

        foreach (var href in spineHrefs)
        {
            ct.ThrowIfCancellationRequested();
            string entryName = CombineZipPath(opfDir, href);
            var entry = zip.GetEntry(entryName);
            if (entry is null) continue;
            string xhtml = ReadEntryText(entry);
            AppendXhtmlBlocks(target, xhtml, entryName);
        }

        target.BinaryMap.MergeFrom(mapBuilder.Build());
        var alerts = new ForensicAnalyzer().Analyze(target, rawBytes);
        target.SetForensicAlerts(alerts);
    }

    // ── Container / OPF resolution ────────────────────────────────────────

    private static string? ResolveOpfPath(ZipArchive zip)
    {
        var container = zip.GetEntry("META-INF/container.xml");
        if (container is null) return null;
        try
        {
            var doc = XDocument.Parse(ReadEntryText(container));
            var rootfile = doc.Descendants(ContainerNs + "rootfile").FirstOrDefault();
            return rootfile?.Attribute("full-path")?.Value;
        }
        catch { return null; }
    }

    private static (DocumentMetadata Metadata, List<string> SpineHrefs) ParseOpf(
        ZipArchive zip, string opfPath, string filePath)
    {
        var entry = zip.GetEntry(opfPath)
            ?? throw new InvalidDataException($"EPUB rootfile not found: {opfPath}");
        XDocument doc;
        try { doc = XDocument.Parse(ReadEntryText(entry)); }
        catch (Exception ex) { throw new InvalidDataException("EPUB OPF manifest is malformed.", ex); }

        var metaEl = doc.Descendants(OpfNs + "metadata").FirstOrDefault();
        var meta = new DocumentMetadata
        {
            Title  = metaEl?.Element(DcNs + "title")?.Value   ?? Path.GetFileNameWithoutExtension(filePath),
            Author = metaEl?.Element(DcNs + "creator")?.Value ?? string.Empty,
            CreatedUtc = DocumentLoaderHelpers.TryParseDate(metaEl?.Element(DcNs + "date")?.Value),
        };

        // Build id → href lookup from <manifest>.
        var manifest = doc.Descendants(OpfNs + "item")
            .Where(e => e.Attribute("id") is not null && e.Attribute("href") is not null)
            .ToDictionary(e => e.Attribute("id")!.Value, e => e.Attribute("href")!.Value, StringComparer.Ordinal);

        // <spine> dictates reading order; filter to XHTML items.
        var spine = doc.Descendants(OpfNs + "itemref")
            .Select(e => e.Attribute("idref")?.Value)
            .Where(id => id is not null && manifest.ContainsKey(id))
            .Select(id => manifest[id!])
            .ToList();

        return (meta, spine);
    }

    // ── XHTML → DocumentBlock mapping ─────────────────────────────────────

    private static void AppendXhtmlBlocks(DocumentModel target, string xhtml, string sourceEntry)
    {
        XDocument doc;
        try { doc = XDocument.Parse(xhtml, LoadOptions.None); }
        catch
        {
            // Some EPUBs are tag-soup HTML; emit the raw text as a paragraph.
            target.Blocks.Add(new DocumentBlock
            {
                Kind = DocumentBlockKinds.Paragraph,
                Text = DocumentLoaderHelpers.StripHtmlTags(xhtml),
            });
            return;
        }

        var body = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "body");
        if (body is null) return;

        // Chapter break: prefer the XHTML <title> for a user-visible label; fall back to filename.
        string chapterTitle =
            doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "title")?.Value?.Trim() is { Length: > 0 } t
                ? t
                : Path.GetFileNameWithoutExtension(sourceEntry);
        target.Blocks.Add(new DocumentBlock
        {
            Kind = DocumentBlockKinds.Heading,
            Text = chapterTitle,
            Attributes = { [DocumentBlockAttributes.Level] = 1 },
        });

        foreach (var el in body.Elements())
            MapElement(el, target.Blocks);
    }

    private static void MapElement(XElement el, ICollection<DocumentBlock> sink)
    {
        string local = el.Name.LocalName.ToLowerInvariant();
        switch (local)
        {
            case "h1": case "h2": case "h3": case "h4": case "h5": case "h6":
                sink.Add(new DocumentBlock
                {
                    Kind = DocumentBlockKinds.Heading,
                    Text = CollapseWhitespace(el.Value),
                    Attributes = { [DocumentBlockAttributes.Level] = local[1] - '0' },
                });
                break;
            case "p":
                sink.Add(new DocumentBlock
                {
                    Kind = DocumentBlockKinds.Paragraph,
                    Text = CollapseWhitespace(el.Value),
                });
                break;
            case "li":
                sink.Add(new DocumentBlock
                {
                    Kind = DocumentBlockKinds.ListItem,
                    Text = CollapseWhitespace(el.Value),
                });
                break;
            case "ul": case "ol":
                foreach (var child in el.Elements()) MapElement(child, sink);
                break;
            case "div": case "section": case "article":
                foreach (var child in el.Elements()) MapElement(child, sink);
                break;
            case "img":
                sink.Add(new DocumentBlock
                {
                    Kind = DocumentBlockKinds.Image,
                    Text = "[image]",
                    Attributes =
                    {
                        [DocumentBlockAttributes.ZipEntryName] = el.Attribute("src")?.Value ?? string.Empty,
                    },
                });
                break;
            default:
                // Fallback: flatten text content under an unknown tag as a paragraph.
                var text = CollapseWhitespace(el.Value);
                if (!string.IsNullOrEmpty(text))
                    sink.Add(new DocumentBlock { Kind = DocumentBlockKinds.Paragraph, Text = text });
                break;
        }
    }

    // ── Helpers (EPUB-specific only) ──────────────────────────────────────

    private static string ReadEntryText(ZipArchiveEntry entry)
    {
        using var s  = entry.Open();
        using var sr = new StreamReader(s, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return sr.ReadToEnd();
    }

    private static string GetParentPath(string zipPath)
    {
        int i = zipPath.LastIndexOf('/');
        return i < 0 ? string.Empty : zipPath[..i];
    }

    private static string CombineZipPath(string dir, string href)
    {
        if (string.IsNullOrEmpty(dir)) return href;
        return $"{dir}/{href}".Replace("//", "/");
    }

    /// <summary>
    /// Collapses runs of whitespace (including line breaks) to a single space
    /// and trims edges — XHTML preserves source formatting that DocumentEditor
    /// doesn't honor at the renderer layer.
    /// </summary>
    private static string CollapseWhitespace(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var sb = new System.Text.StringBuilder(s.Length);
        bool prevSpace = false;
        foreach (char ch in s)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!prevSpace) { sb.Append(' '); prevSpace = true; }
            }
            else { sb.Append(ch); prevSpace = false; }
        }
        return sb.ToString().Trim();
    }
}
