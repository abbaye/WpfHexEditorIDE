// ==========================================================
// Project: WpfHexEditor.Plugins.DocumentLoaders
// File: Parsers/Odt/OdtXmlMapper.cs
// Description:
//     Parses content.xml (ODF) into DocumentBlock trees.
//     Maps <text:p> → paragraph, <text:span> → run,
//     <text:h> → heading, <draw:frame>/<draw:image> → image blocks.
// Architecture:
//     Body element is <office:text> (OfficeNs), NOT <text:text>.
// ==========================================================

using System.Globalization;
using System.Xml.Linq;
using WpfHexEditor.Editor.DocumentEditor.Core.BinaryMap;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Plugins.DocumentLoaders.Parsers.Odt;

internal sealed class OdtXmlMapper
{
    // Resolved style properties: styleName → {bold, italic, underline, fontSize, fontFamily}
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, object>> _styleProps;

    public OdtXmlMapper(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, object>>? styleProps = null)
    {
        _styleProps = styleProps ?? new Dictionary<string, IReadOnlyDictionary<string, object>>();
    }

    private static readonly XNamespace OfficeNs = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
    private static readonly XNamespace TextNs   = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private static readonly XNamespace DrawNs   = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
    private static readonly XNamespace StyleNs  = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
    private static readonly XNamespace TableNs  = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
    private static readonly XNamespace XlinkNs  = "http://www.w3.org/1999/xlink";
    private static readonly XNamespace SvgNs    = "urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0";

    /// <summary>Diagnostic string from the last Map() call — routed to Output panel.</summary>
    public string LastDiagnostic { get; private set; } = string.Empty;

    public List<DocumentBlock> Map(
        string            contentXml,
        long              entryBaseOffset,
        BinaryMapBuilder  mapBuilder,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var root   = XDocument.Parse(contentXml, LoadOptions.SetLineInfo);
        // ODF body is <office:body><office:text> — namespace is office:, not text:
        var body   = root.Descendants(OfficeNs + "text").FirstOrDefault();

        var blocks = new List<DocumentBlock>();

        if (body is null)
        {
            LastDiagnostic = $"body=NULL xmlLen={contentXml.Length}";
            return blocks;
        }

        int childCount = body.Elements().Count();
        string firstKinds = string.Join(",", body.Elements().Take(5).Select(e => e.Name.LocalName));

        CollectBlocks(body, entryBaseOffset, mapBuilder, blocks, ct);

        LastDiagnostic = $"body=OK children={childCount} first5=[{firstKinds}] blocks={blocks.Count}";
        return blocks;
    }

    // Recursively collect blocks, transparently descending into ODF container elements
    // (text:section, text:text) that are not themselves content blocks.
    private void CollectBlocks(
        XElement          parent,
        long              baseOffset,
        BinaryMapBuilder  mapBuilder,
        List<DocumentBlock> blocks,
        CancellationToken ct)
    {
        foreach (var child in parent.Elements())
        {
            ct.ThrowIfCancellationRequested();

            // Transparent containers — descend without emitting a block
            if (child.Name.Namespace == TextNs &&
                child.Name.LocalName is "section" or "text-section" or "index-body" or "tracked-changes")
            {
                CollectBlocks(child, baseOffset, mapBuilder, blocks, ct);
                continue;
            }

            var block = MapElement(child, baseOffset, mapBuilder);
            if (block is not null) blocks.Add(block);
        }
    }

    private DocumentBlock? MapElement(XElement elem, long baseOffset, BinaryMapBuilder mapBuilder)
    {
        string local = elem.Name.LocalName;
        XNamespace ns = elem.Name.Namespace;

        if (ns == TextNs && local == "p")
            return MapParagraph(elem, baseOffset, mapBuilder, "paragraph");

        if (ns == TextNs && local == "h")
            return MapParagraph(elem, baseOffset, mapBuilder, "heading");

        if (ns == TextNs && local is "list" or "list-item")
            return MapList(elem, baseOffset, mapBuilder);

        if (ns == TableNs && local == "table")
            return MapTable(elem, baseOffset, mapBuilder);

        if (ns == DrawNs && local is "frame" or "image")
            return MapImage(elem, baseOffset, mapBuilder);

        return null;
    }

    private DocumentBlock MapParagraph(
        XElement elem, long baseOffset, BinaryMapBuilder mapBuilder, string kind)
    {
        long off = ResolveOffset(elem, baseOffset);
        int  len = EstimateLength(elem);

        var para = new DocumentBlock { Kind = kind, RawOffset = off, RawLength = len, Text = string.Empty };

        var styleAttr = elem.Attribute(TextNs + "style-name") ??
                        elem.Attribute(StyleNs + "style-name");

        IReadOnlyDictionary<string, object>? paraStyleProps = null;
        if (styleAttr is not null)
        {
            para.Attributes["style"] = styleAttr.Value;
            if (_styleProps.TryGetValue(styleAttr.Value, out var props))
            {
                paraStyleProps = props;
                // Apply paragraph-level font defaults (text-only kinds — runs inherit too).
                foreach (var kv in props)
                {
                    if (kv.Key.StartsWith("__", StringComparison.Ordinal)) continue;
                    if (!para.Attributes.ContainsKey(kv.Key))
                        para.Attributes[kv.Key] = kv.Value;
                }
            }
        }

        var outlineLvl = elem.Attribute(TextNs + "outline-level");
        if (outlineLvl is not null) para.Attributes["level"] = outlineLvl.Value;

        var sb = new System.Text.StringBuilder();

        foreach (var child in elem.Elements())
        {
            if (child.Name.Namespace == TextNs && child.Name.LocalName == "span")
            {
                var run = MapRun(child, baseOffset, mapBuilder, paraStyleProps);
                para.Children.Add(run);
                sb.Append(run.Text);
            }
            else if (child.Name.Namespace == TextNs && child.Name.LocalName == "s")
            {
                int count = int.TryParse(child.Attribute(TextNs + "c")?.Value, out int c) ? c : 1;
                sb.Append(' ', count);
            }
            else if (child.Name.Namespace == TextNs && child.Name.LocalName == "tab")
            {
                sb.Append('\t');
            }
            else if (child.Name.Namespace == DrawNs)
            {
                var imgBlock = MapImage(child, baseOffset, mapBuilder);
                para.Children.Add(imgBlock);
            }
        }

        foreach (var tn in elem.Nodes().OfType<XText>())
            sb.Append(tn.Value);

        para.Text = sb.ToString();
        mapBuilder.AddZipRelative("content.xml", para, off, len);
        return para;
    }

    private DocumentBlock MapRun(
        XElement rElem,
        long baseOffset,
        BinaryMapBuilder mapBuilder,
        IReadOnlyDictionary<string, object>? paraStyleProps = null)
    {
        long off = ResolveOffset(rElem, baseOffset);
        int  len = EstimateLength(rElem);

        var run = new DocumentBlock { Kind = "run", RawOffset = off, RawLength = len, Text = rElem.Value };

        // Paragraph style → run cascade (parent first, run-level overrides).
        if (paraStyleProps is not null)
        {
            foreach (var kv in paraStyleProps)
            {
                if (kv.Key.StartsWith("__", StringComparison.Ordinal)) continue;
                run.Attributes[kv.Key] = kv.Value;
            }
        }

        var styleName = rElem.Attribute(TextNs + "style-name")?.Value;
        if (styleName is not null)
        {
            run.Attributes["style"] = styleName;
            // Direct run style overrides cascaded paragraph defaults.
            if (_styleProps.TryGetValue(styleName, out var props))
                foreach (var kv in props)
                {
                    if (kv.Key.StartsWith("__", StringComparison.Ordinal)) continue;
                    run.Attributes[kv.Key] = kv.Value;
                }
        }

        mapBuilder.AddZipRelative("content.xml", run, off, len);
        return run;
    }

    private DocumentBlock MapList(XElement listElem, long baseOffset, BinaryMapBuilder mapBuilder)
    {
        long off = ResolveOffset(listElem, baseOffset);
        int  len = EstimateLength(listElem);

        var list = new DocumentBlock { Kind = "list", RawOffset = off, RawLength = len, Text = string.Empty };

        foreach (var item in listElem.Elements(TextNs + "list-item"))
        {
            foreach (var p in item.Elements())
            {
                var b = MapElement(p, baseOffset, mapBuilder);
                if (b is not null) list.Children.Add(b);
            }
        }

        mapBuilder.AddZipRelative("content.xml", list, off, len);
        return list;
    }

    private DocumentBlock MapTable(XElement tblElem, long baseOffset, BinaryMapBuilder mapBuilder)
    {
        long off = ResolveOffset(tblElem, baseOffset);
        int  len = EstimateLength(tblElem);

        var table = new DocumentBlock { Kind = "table", RawOffset = off, RawLength = len, Text = "[table]" };

        foreach (var rowElem in tblElem.Descendants(TableNs + "table-row"))
        {
            var row = new DocumentBlock
            {
                Kind      = "table-row",
                RawOffset = ResolveOffset(rowElem, baseOffset),
                RawLength = EstimateLength(rowElem),
                Text      = string.Empty
            };
            foreach (var cellElem in rowElem.Elements(TableNs + "table-cell"))
                foreach (var p in cellElem.Elements(TextNs + "p"))
                    row.Children.Add(MapParagraph(p, baseOffset, mapBuilder, "paragraph"));

            table.Children.Add(row);
        }

        mapBuilder.AddZipRelative("content.xml", table, off, len);
        return table;
    }

    private static DocumentBlock MapImage(XElement elem, long baseOffset, BinaryMapBuilder mapBuilder)
    {
        long off = ResolveOffset(elem, baseOffset);
        int  len = EstimateLength(elem);

        var img = new DocumentBlock { Kind = "image", RawOffset = off, RawLength = len, Text = "[image]" };
        mapBuilder.AddZipRelative("content.xml", img, off, len);

        // Extract zip entry path from draw:image@xlink:href (e.g. "Pictures/image1.png")
        var href = elem.Element(DrawNs + "image")?.Attribute(XlinkNs + "href")?.Value
                ?? elem.Attribute(XlinkNs + "href")?.Value;
        if (href is not null) img.Attributes["zipEntryName"] = href;

        // Natural display dimensions from svg:width / svg:height (e.g. "6.5cm", "2.3in")
        var w = elem.Attribute(SvgNs + "width")?.Value;
        var h = elem.Attribute(SvgNs + "height")?.Value;
        if (w is not null)
        {
            double px = ParseOdtLength(w);
            if (px > 0) img.Attributes["naturalWidth"] = px.ToString(CultureInfo.InvariantCulture);
        }
        if (h is not null)
        {
            double px = ParseOdtLength(h);
            if (px > 0) img.Attributes["naturalHeight"] = px.ToString(CultureInfo.InvariantCulture);
        }

        return img;
    }

    /// <summary>Converts an ODT length string (e.g. "6.5cm", "2.3in", "72pt") to pixels at 96 dpi.</summary>
    private static double ParseOdtLength(string val)
    {
        if (val.EndsWith("cm",  StringComparison.Ordinal) &&
            double.TryParse(val[..^2], NumberStyles.Any, CultureInfo.InvariantCulture, out double cm))
            return cm * 37.795;
        if (val.EndsWith("in",  StringComparison.Ordinal) &&
            double.TryParse(val[..^2], NumberStyles.Any, CultureInfo.InvariantCulture, out double inch))
            return inch * 96.0;
        if (val.EndsWith("pt",  StringComparison.Ordinal) &&
            double.TryParse(val[..^2], NumberStyles.Any, CultureInfo.InvariantCulture, out double pt))
            return pt * 1.333;
        if (val.EndsWith("mm",  StringComparison.Ordinal) &&
            double.TryParse(val[..^2], NumberStyles.Any, CultureInfo.InvariantCulture, out double mm))
            return mm * 3.7795;
        return 0;
    }

    private static long ResolveOffset(XElement elem, long baseOffset)
    {
        if (elem is System.Xml.IXmlLineInfo li && li.HasLineInfo())
            return (long)li.LineNumber * 1000 + li.LinePosition;
        return baseOffset;
    }

    private static int EstimateLength(XElement elem) =>
        System.Text.Encoding.UTF8.GetByteCount(elem.ToString());
}
