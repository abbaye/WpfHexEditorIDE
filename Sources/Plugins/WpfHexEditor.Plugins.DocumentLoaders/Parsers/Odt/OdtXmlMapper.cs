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

using System.Xml.Linq;
using WpfHexEditor.Editor.DocumentEditor.Core.BinaryMap;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Plugins.DocumentLoaders.Parsers.Odt;

internal sealed class OdtXmlMapper
{
    private static readonly XNamespace OfficeNs = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
    private static readonly XNamespace TextNs   = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private static readonly XNamespace DrawNs   = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
    private static readonly XNamespace StyleNs  = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
    private static readonly XNamespace TableNs  = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";

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

        if (body is null) return blocks;

        CollectBlocks(body, entryBaseOffset, mapBuilder, blocks, ct);

        return blocks;
    }

    // Recursively collect blocks, transparently descending into ODF container elements
    // (text:section, text:text) that are not themselves content blocks.
    private static void CollectBlocks(
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

    private static DocumentBlock? MapElement(XElement elem, long baseOffset, BinaryMapBuilder mapBuilder)
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

    private static DocumentBlock MapParagraph(
        XElement elem, long baseOffset, BinaryMapBuilder mapBuilder, string kind)
    {
        long off = ResolveOffset(elem, baseOffset);
        int  len = EstimateLength(elem);

        var para = new DocumentBlock { Kind = kind, RawOffset = off, RawLength = len, Text = string.Empty };

        var styleAttr = elem.Attribute(TextNs + "style-name") ??
                        elem.Attribute(StyleNs + "style-name");
        if (styleAttr is not null) para.Attributes["style"] = styleAttr.Value;

        var outlineLvl = elem.Attribute(TextNs + "outline-level");
        if (outlineLvl is not null) para.Attributes["level"] = outlineLvl.Value;

        var sb = new System.Text.StringBuilder();

        foreach (var child in elem.Elements())
        {
            if (child.Name.Namespace == TextNs && child.Name.LocalName == "span")
            {
                var run = MapRun(child, baseOffset, mapBuilder);
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

    private static DocumentBlock MapRun(XElement rElem, long baseOffset, BinaryMapBuilder mapBuilder)
    {
        long off = ResolveOffset(rElem, baseOffset);
        int  len = EstimateLength(rElem);

        var run = new DocumentBlock { Kind = "run", RawOffset = off, RawLength = len, Text = rElem.Value };

        var style = rElem.Attribute(TextNs + "style-name");
        if (style is not null) run.Attributes["style"] = style.Value;

        mapBuilder.AddZipRelative("content.xml", run, off, len);
        return run;
    }

    private static DocumentBlock MapList(XElement listElem, long baseOffset, BinaryMapBuilder mapBuilder)
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

    private static DocumentBlock MapTable(XElement tblElem, long baseOffset, BinaryMapBuilder mapBuilder)
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
        return img;
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
