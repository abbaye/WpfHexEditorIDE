// ==========================================================
// Project: WpfHexEditor.Plugins.DocumentLoaders
// File: Parsers/Odt/OdtDocumentLoader.cs
// Description:
//     IDocumentLoader implementation for ODT/OTT files.
// ==========================================================

using System.Globalization;
using System.Xml.Linq;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.DocumentEditor.Core;
using WpfHexEditor.Editor.DocumentEditor.Core.BinaryMap;
using WpfHexEditor.Editor.DocumentEditor.Core.Forensic;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;
using WpfHexEditor.Editor.DocumentEditor.Core.Options;

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

        // Build style-name → formatting-properties map from styles.xml and content.xml automatic-styles.
        // Two-pass: collect raw style props + parent-style-name, then flatten basedOn chains.
        var rawStyles = new Dictionary<string, OdtRawStyle>(StringComparer.OrdinalIgnoreCase);
        LoadOdtStylesRaw(zipReader, "styles.xml",  rawStyles); // named styles (baseline)
        LoadOdtStylesRaw(zipReader, "content.xml", rawStyles); // automatic styles override

        var styleProps = FlattenOdtStyles(rawStyles);

        var mapper = new OdtXmlMapper(styleProps);
        var blocks = await Task.Run(
            () => mapper.Map(contentXml, contentEntryOffset, mapBuilder, ct), ct);

        var metadata = ReadMetaXml(zipReader);
        metadata = metadata with
        {
            MimeType = "application/vnd.oasis.opendocument.text",
            Title    = !string.IsNullOrEmpty(metadata.Title)
                           ? metadata.Title
                           : Path.GetFileNameWithoutExtension(filePath)
        };

        target.FilePath     = filePath;
        target.Metadata     = metadata;
        target.PageSettings = ReadOdtPageSettings(zipReader);

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

    /// <summary>
    /// Reads all <c>style:style</c> elements from the given ZIP entry into <paramref name="into"/>.
    /// Captures raw text-properties + parent-style-name + family for later basedOn flattening.
    /// Content.xml automatic-styles should be loaded last so they override base styles.xml entries.
    /// </summary>
    private static void LoadOdtStylesRaw(
        OdtZipReader zipReader,
        string entryName,
        Dictionary<string, OdtRawStyle> into)
    {
        var xml = zipReader.ReadEntryText(entryName);
        if (xml is null) return;

        XDocument doc;
        try { doc = XDocument.Parse(xml); }
        catch { return; }

        XNamespace styleNs = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
        XNamespace foNs    = "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0";

        foreach (var style in doc.Descendants(styleNs + "style"))
        {
            var name = style.Attribute(styleNs + "name")?.Value;
            if (name is null) continue;

            var parent = style.Attribute(styleNs + "parent-style-name")?.Value;
            var family = style.Attribute(styleNs + "family")?.Value;

            var props = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var tp    = style.Element(styleNs + "text-properties");
            if (tp is not null)
            {
                if (tp.Attribute(foNs + "font-weight")?.Value     == "bold")      props["bold"]      = true;
                if (tp.Attribute(foNs + "font-style")?.Value      == "italic")    props["italic"]    = true;
                if (tp.Attribute(foNs + "text-decoration")?.Value == "underline") props["underline"] = true;

                var fs = tp.Attribute(foNs + "font-size")?.Value;
                double? sz = ParseOdtFontSizePt(fs);
                if (sz is not null) props["fontSize"] = sz.Value;

                var ff = tp.Attribute(foNs  + "font-family")?.Value
                      ?? tp.Attribute(styleNs + "font-name")?.Value;
                if (ff is not null) props["fontFamily"] = ff;

                var color = tp.Attribute(foNs + "color")?.Value;
                if (!string.IsNullOrEmpty(color))
                    props["color"] = color.StartsWith('#') ? color : $"#{color.TrimStart('#')}";
            }

            into[name] = new OdtRawStyle(props, parent, family);
        }
    }

    /// <summary>
    /// Flattens basedOn chains (parent-style-name) with cycle guard. Parent props come first,
    /// child overrides. Mirrors DocxStyleTable.Flatten and the new RtfStyleTable approach.
    /// </summary>
    private static Dictionary<string, IReadOnlyDictionary<string, object>> FlattenOdtStyles(
        IReadOnlyDictionary<string, OdtRawStyle> raw)
    {
        var resolved = new Dictionary<string, IReadOnlyDictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in raw.Keys)
            resolved[name] = Flatten(name, raw, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        return resolved;

        static IReadOnlyDictionary<string, object> Flatten(
            string name,
            IReadOnlyDictionary<string, OdtRawStyle> r,
            HashSet<string> visited)
        {
            if (!r.TryGetValue(name, out var self) || !visited.Add(name))
                return new Dictionary<string, object>();

            var parentProps = self.Parent is { Length: > 0 } p
                ? Flatten(p, r, visited)
                : new Dictionary<string, object>();

            var merged = new Dictionary<string, object>(parentProps, StringComparer.OrdinalIgnoreCase);
            foreach (var kv in self.Props) merged[kv.Key] = kv.Value;

            // Carry the resolved family attribute so consumers can distinguish paragraph/text styles.
            if (self.Family is not null) merged["__family"] = self.Family;
            return merged;
        }
    }

    /// <summary>Parses ODT font-size strings (12pt, 14pt, 1cm, 0.5in) into points.</summary>
    private static double? ParseOdtFontSizePt(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return null;

        if (raw.EndsWith("pt", StringComparison.Ordinal) &&
            double.TryParse(raw[..^2], NumberStyles.Any, CultureInfo.InvariantCulture, out double pt))
            return pt;
        if (raw.EndsWith("cm", StringComparison.Ordinal) &&
            double.TryParse(raw[..^2], NumberStyles.Any, CultureInfo.InvariantCulture, out double cm))
            return cm * 28.3464567;          // 1 cm = 28.346 pt
        if (raw.EndsWith("mm", StringComparison.Ordinal) &&
            double.TryParse(raw[..^2], NumberStyles.Any, CultureInfo.InvariantCulture, out double mm))
            return mm * 2.83464567;
        if (raw.EndsWith("in", StringComparison.Ordinal) &&
            double.TryParse(raw[..^2], NumberStyles.Any, CultureInfo.InvariantCulture, out double inch))
            return inch * 72.0;
        return null;
    }

    private sealed record OdtRawStyle(
        IReadOnlyDictionary<string, object> Props,
        string? Parent,
        string? Family);

    private static DocumentPageSettings? ReadOdtPageSettings(OdtZipReader zip)
    {
        try
        {
            var xml = zip.ReadEntryText("styles.xml");
            if (xml is null) return null;

            XNamespace StyleNs = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
            XNamespace FoNs    = "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0";

            var doc   = XDocument.Parse(xml);
            var props = doc.Descendants(StyleNs + "page-layout-properties").FirstOrDefault();
            if (props is null) return null;

            string? pgW   = props.Attribute(FoNs + "page-width")?.Value;
            string? pgH   = props.Attribute(FoNs + "page-height")?.Value;
            if (pgW is null || pgH is null) return null;

            string? mTop  = props.Attribute(FoNs + "margin-top")?.Value;
            string? mBot  = props.Attribute(FoNs + "margin-bottom")?.Value;
            string? mLeft = props.Attribute(FoNs + "margin-left")?.Value;
            string? mRight= props.Attribute(FoNs + "margin-right")?.Value;

            return DocumentPageSettings.FromOdt(pgW, pgH, mTop, mBot, mLeft, mRight);
        }
        catch { return null; }
    }
}
