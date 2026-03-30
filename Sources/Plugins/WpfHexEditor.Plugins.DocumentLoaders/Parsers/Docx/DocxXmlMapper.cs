// ==========================================================
// Project: WpfHexEditor.Plugins.DocumentLoaders
// File: Parsers/Docx/DocxXmlMapper.cs
// Description:
//     Parses word/document.xml (OOXML) into DocumentBlock trees.
// ==========================================================

using System.Xml.Linq;
using WpfHexEditor.Editor.DocumentEditor.Core.BinaryMap;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Plugins.DocumentLoaders.Parsers.Docx;

internal sealed class DocxXmlMapper
{
    private static readonly XNamespace W   = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace W14 = "http://schemas.microsoft.com/office/word/2010/wordml";

    public List<DocumentBlock> Map(
        string            documentXml,
        long              entryBaseOffset,
        BinaryMapBuilder  mapBuilder,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var root   = XDocument.Parse(documentXml, LoadOptions.SetLineInfo);
        var body   = root.Descendants(W + "body").FirstOrDefault();
        var blocks = new List<DocumentBlock>();

        if (body is null) return blocks;

        foreach (var child in body.Elements())
        {
            ct.ThrowIfCancellationRequested();
            var block = MapElement(child, entryBaseOffset, mapBuilder);
            if (block is not null) blocks.Add(block);
        }

        return blocks;
    }

    private static DocumentBlock? MapElement(XElement elem, long baseOffset, BinaryMapBuilder mapBuilder)
    {
        return elem.Name.LocalName switch
        {
            "p"             => MapParagraph(elem, baseOffset, mapBuilder),
            "tbl"           => MapTable(elem, baseOffset, mapBuilder),
            "sdt"           => MapStructuredTag(elem, baseOffset, mapBuilder),
            "bookmarkStart" => null,
            "bookmarkEnd"   => null,
            _               => MapGenericElement(elem, elem.Name.LocalName, baseOffset, mapBuilder)
        };
    }

    private static DocumentBlock MapParagraph(XElement pElem, long baseOffset, BinaryMapBuilder mapBuilder)
    {
        long off = ResolveOffset(pElem, baseOffset);
        int  len = EstimateLength(pElem);

        var para = new DocumentBlock { Kind = "paragraph", RawOffset = off, RawLength = len, Text = string.Empty };

        var pPr = pElem.Element(W + "pPr");
        if (pPr is not null) ExtractParagraphProps(pPr, para);

        foreach (var rElem in pElem.Elements(W + "r"))
        {
            var run = MapRun(rElem, baseOffset, mapBuilder);
            para.Children.Add(run);
            para.Text += run.Text;
        }

        mapBuilder.AddZipRelative("word/document.xml", para, off, len);
        return para;
    }

    private static DocumentBlock MapRun(XElement rElem, long baseOffset, BinaryMapBuilder mapBuilder)
    {
        long off = ResolveOffset(rElem, baseOffset);
        int  len = EstimateLength(rElem);

        var run = new DocumentBlock { Kind = "run", RawOffset = off, RawLength = len, Text = string.Empty };

        var rPr = rElem.Element(W + "rPr");
        if (rPr is not null) ExtractRunProps(rPr, run);

        var sb = new System.Text.StringBuilder();
        foreach (var t  in rElem.Elements(W + "t"))  sb.Append(t.Value);
        foreach (var _  in rElem.Elements(W + "br")) sb.Append('\n');
        foreach (var __ in rElem.Elements(W + "tab"))sb.Append('\t');

        run.Text = sb.ToString();
        mapBuilder.AddZipRelative("word/document.xml", run, off, len);
        return run;
    }

    private static DocumentBlock MapTable(XElement tblElem, long baseOffset, BinaryMapBuilder mapBuilder)
    {
        long off = ResolveOffset(tblElem, baseOffset);
        int  len = EstimateLength(tblElem);

        var table = new DocumentBlock { Kind = "table", RawOffset = off, RawLength = len, Text = "[table]" };

        foreach (var trElem in tblElem.Elements(W + "tr"))
        {
            var row = new DocumentBlock
            {
                Kind      = "table-row",
                RawOffset = ResolveOffset(trElem, baseOffset),
                RawLength = EstimateLength(trElem),
                Text      = string.Empty
            };
            foreach (var tcElem in trElem.Elements(W + "tc"))
                foreach (var pElem in tcElem.Elements(W + "p"))
                    row.Children.Add(MapParagraph(pElem, baseOffset, mapBuilder));
            table.Children.Add(row);
        }

        mapBuilder.AddZipRelative("word/document.xml", table, off, len);
        return table;
    }

    private static DocumentBlock MapStructuredTag(XElement elem, long baseOffset, BinaryMapBuilder mapBuilder)
    {
        var content = elem.Element(W + "sdtContent");
        if (content is null) return MapGenericElement(elem, "sdt", baseOffset, mapBuilder);

        var block = new DocumentBlock
        {
            Kind      = "structured-tag",
            RawOffset = ResolveOffset(elem, baseOffset),
            RawLength = EstimateLength(elem),
            Text      = string.Empty
        };
        foreach (var child in content.Elements())
        {
            var cb = MapElement(child, baseOffset, mapBuilder);
            if (cb is not null) block.Children.Add(cb);
        }
        mapBuilder.AddZipRelative("word/document.xml", block, block.RawOffset, block.RawLength);
        return block;
    }

    private static DocumentBlock MapGenericElement(XElement elem, string kind, long baseOffset, BinaryMapBuilder mapBuilder)
    {
        long off = ResolveOffset(elem, baseOffset);
        int  len = EstimateLength(elem);
        var  b   = new DocumentBlock
        {
            Kind      = kind,
            RawOffset = off,
            RawLength = len,
            Text      = elem.Value.Length > 200 ? elem.Value[..200] + "…" : elem.Value
        };
        mapBuilder.AddZipRelative("word/document.xml", b, off, len);
        return b;
    }

    private static void ExtractParagraphProps(XElement pPr, DocumentBlock para)
    {
        var style = pPr.Element(W + "pStyle")?.Attribute(W + "val")?.Value;
        if (style is not null) para.Attributes["style"] = style;

        var jc = pPr.Element(W + "jc")?.Attribute(W + "val")?.Value;
        if (jc is not null) para.Attributes["alignment"] = jc;
    }

    private static void ExtractRunProps(XElement rPr, DocumentBlock run)
    {
        if (rPr.Element(W + "b") is not null) run.Attributes["bold"]      = true;
        if (rPr.Element(W + "i") is not null) run.Attributes["italic"]    = true;
        if (rPr.Element(W + "u") is not null) run.Attributes["underline"] = true;

        var szCs = rPr.Element(W + "szCs")?.Attribute(W + "val")?.Value;
        if (szCs is not null && int.TryParse(szCs, out int hpt))
            run.Attributes["fontSize"] = hpt / 2;

        var rStyle = rPr.Element(W + "rStyle")?.Attribute(W + "val")?.Value;
        if (rStyle is not null) run.Attributes["style"] = rStyle;
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
