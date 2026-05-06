// ==========================================================
// Project: WpfHexEditor.Plugins.DocumentLoaders
// File: Parsers/Docx/DocxXmlMapper.cs
// Description:
//     Parses word/document.xml (OOXML) into DocumentBlock trees.
//     Handles paragraphs, runs, tables, structured tags, and inline
//     images (w:drawing → a:blip → relationship → zip entry).
// ==========================================================

using System.Globalization;
using System.Xml.Linq;
using WpfHexEditor.Editor.DocumentEditor.Core.BinaryMap;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;
using WpfHexEditor.Editor.DocumentEditor.Core.Options;

namespace WpfHexEditor.Plugins.DocumentLoaders.Parsers.Docx;

internal sealed class DocxXmlMapper
{
    // ── XML namespaces ────────────────────────────────────────────────────────

    private static readonly XNamespace W      = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace W14    = "http://schemas.microsoft.com/office/word/2010/wordml";
    private static readonly XNamespace ADrawNs = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    private static readonly XNamespace ANs    = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace RelNs  = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    // ── Relationship map (rId → zip entry path) ───────────────────────────────

    private readonly IReadOnlyDictionary<string, string> _relsMap;

    public DocxXmlMapper(IReadOnlyDictionary<string, string>? relsMap = null)
        => _relsMap = relsMap ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    // ── Entry point ───────────────────────────────────────────────────────────

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

    // ── Element dispatch ──────────────────────────────────────────────────────

    private DocumentBlock? MapElement(XElement elem, long baseOffset, BinaryMapBuilder mapBuilder)
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

    // ── Paragraph ─────────────────────────────────────────────────────────────

    private DocumentBlock MapParagraph(XElement pElem, long baseOffset, BinaryMapBuilder mapBuilder)
    {
        long off = ResolveOffset(pElem, baseOffset);
        int  len = EstimateLength(pElem);

        var pPr   = pElem.Element(W + "pPr");
        var numPr = pPr?.Element(W + "numPr");
        bool isList = false;
        int listLevel = 0, numId = 0;
        if (numPr is not null)
        {
            var ilvlStr  = numPr.Element(W + "ilvl")?.Attribute(W + "val")?.Value;
            var numIdStr = numPr.Element(W + "numId")?.Attribute(W + "val")?.Value;
            listLevel = ilvlStr  is not null && int.TryParse(ilvlStr,  out int lv) ? lv : 0;
            numId     = numIdStr is not null && int.TryParse(numIdStr, out int ni) ? ni : 0;
            isList    = numId > 0;
        }

        var para = new DocumentBlock
        {
            Kind      = isList ? "list-item" : "paragraph",
            RawOffset = off,
            RawLength = len,
            Text      = string.Empty
        };

        if (isList)
        {
            para.Attributes["listLevel"] = listLevel;
            para.Attributes["listStyle"] = "bullet";
            para.Attributes["numId"]     = numId;
        }

        if (pPr is not null) ExtractParagraphProps(pPr, para);

        // Runs and inline hyperlinks (w:hyperlink wraps w:r elements)
        foreach (var child in pElem.Elements())
        {
            if (child.Name == W + "r")
            {
                var run = MapRun(child, baseOffset, mapBuilder);
                para.Children.Add(run);
                para.Text += run.Text;
            }
            else if (child.Name == W + "hyperlink")
            {
                var rId  = child.Attribute(RelNs + "id")?.Value;
                var url  = rId is not null && _relsMap.TryGetValue(rId, out var u) ? u : null;
                var text = string.Concat(child.Descendants(W + "t").Select(t => t.Value));
                var hlBlock = new DocumentBlock
                {
                    Kind = "hyperlink", Text = text, RawOffset = -1, RawLength = 0
                };
                if (url is not null) hlBlock.Attributes["href"] = url;
                para.Children.Add(hlBlock);
                para.Text += text;
            }
        }

        // Inline images (w:drawing at paragraph level or nested inside w:r)
        foreach (var drawing in pElem.Descendants(W + "drawing"))
        {
            var blip  = drawing.Descendants(ANs + "blip").FirstOrDefault();
            var embed = blip?.Attribute(RelNs + "embed")?.Value;
            if (embed is null || !_relsMap.TryGetValue(embed, out var entryName)) continue;

            var ext = drawing.Descendants(ADrawNs + "extent").FirstOrDefault();
            double? cx = ParseEmu(ext?.Attribute("cx")?.Value);
            double? cy = ParseEmu(ext?.Attribute("cy")?.Value);

            var imgBlock = new DocumentBlock
                { Kind = "image", RawOffset = -1, RawLength = 0, Text = "[image]" };
            imgBlock.Attributes["zipEntryName"] = entryName;
            if (cx.HasValue)
                imgBlock.Attributes["naturalWidth"]  = cx.Value.ToString(CultureInfo.InvariantCulture);
            if (cy.HasValue)
                imgBlock.Attributes["naturalHeight"] = cy.Value.ToString(CultureInfo.InvariantCulture);
            para.Children.Add(imgBlock);
        }

        mapBuilder.AddZipRelative("word/document.xml", para, off, len);
        return para;
    }

    // ── Run ───────────────────────────────────────────────────────────────────

    private DocumentBlock MapRun(XElement rElem, long baseOffset, BinaryMapBuilder mapBuilder)
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

    // ── Table ─────────────────────────────────────────────────────────────────

    private DocumentBlock MapTable(XElement tblElem, long baseOffset, BinaryMapBuilder mapBuilder)
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

    // ── Structured content tag ────────────────────────────────────────────────

    private DocumentBlock MapStructuredTag(XElement elem, long baseOffset, BinaryMapBuilder mapBuilder)
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

    private DocumentBlock MapGenericElement(XElement elem, string kind, long baseOffset, BinaryMapBuilder mapBuilder)
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

    // ── Property extractors ───────────────────────────────────────────────────

    private static void ExtractParagraphProps(XElement pPr, DocumentBlock para)
    {
        var style = pPr.Element(W + "pStyle")?.Attribute(W + "val")?.Value;
        if (style is not null) para.Attributes["style"] = style;

        var jc = pPr.Element(W + "jc")?.Attribute(W + "val")?.Value;
        if (jc is not null) para.Attributes["alignment"] = jc;

        // Indentation
        var ind = pPr.Element(W + "ind");
        if (ind is not null)
        {
            var left      = ind.Attribute(W + "left")?.Value;
            var right     = ind.Attribute(W + "right")?.Value;
            var firstLine = ind.Attribute(W + "firstLine")?.Value;
            if (left      is not null && int.TryParse(left,      out int l)) para.Attributes["indent"]          = DocumentPageSettings.TwipsToPx(l);
            if (right     is not null && int.TryParse(right,     out int r)) para.Attributes["indentRight"]     = DocumentPageSettings.TwipsToPx(r);
            if (firstLine is not null && int.TryParse(firstLine, out int f)) para.Attributes["indentFirstLine"] = DocumentPageSettings.TwipsToPx(f);
        }

        // Paragraph spacing
        var spacing = pPr.Element(W + "spacing");
        if (spacing is not null)
        {
            var before   = spacing.Attribute(W + "before")?.Value;
            var after    = spacing.Attribute(W + "after")?.Value;
            var line     = spacing.Attribute(W + "line")?.Value;
            var lineRule = spacing.Attribute(W + "lineRule")?.Value;
            if (before   is not null && int.TryParse(before,   out int b)) para.Attributes["spaceBefore"]   = DocumentPageSettings.TwipsToPx(b);
            if (after    is not null && int.TryParse(after,    out int a)) para.Attributes["spaceAfter"]    = DocumentPageSettings.TwipsToPx(a);
            if (line     is not null && int.TryParse(line,     out int l)) para.Attributes["lineSpacing"]   = l;
            if (lineRule is not null) para.Attributes["lineSpacingRule"] = lineRule;
        }

        // Paragraph-level run props (w:pPr/w:rPr) — apply as paragraph defaults
        var pRpr = pPr.Element(W + "rPr");
        if (pRpr is not null) ExtractRunProps(pRpr, para);

        // Tab stops
        var tabs = pPr.Element(W + "tabs");
        if (tabs is not null)
        {
            var tabList = tabs.Elements(W + "tab")
                .Select(t => new {
                    Pos = t.Attribute(W + "pos")?.Value,
                    Val = t.Attribute(W + "val")?.Value ?? "left"
                })
                .Where(t => t.Pos is not null && int.TryParse(t.Pos, out _))
                .Select(t => $"{t.Val}:{DocumentPageSettings.TwipsToPx(int.Parse(t.Pos!)):F1}")
                .ToList();
            if (tabList.Count > 0) para.Attributes["tabStops"] = string.Join(";", tabList);
        }
    }

    private static void ExtractRunProps(XElement rPr, DocumentBlock run)
    {
        if (rPr.Element(W + "b") is not null) run.Attributes["bold"]      = true;
        if (rPr.Element(W + "i") is not null) run.Attributes["italic"]    = true;
        if (rPr.Element(W + "u") is not null) run.Attributes["underline"] = true;

        // w:sz is the primary size (half-points); w:szCs is for complex scripts — prefer w:sz
        var szVal = rPr.Element(W + "sz")?.Attribute(W + "val")?.Value
                 ?? rPr.Element(W + "szCs")?.Attribute(W + "val")?.Value;
        if (szVal is not null && int.TryParse(szVal, out int hpt))
            run.Attributes["fontSize"] = hpt / 2.0;

        // Font family from w:rFonts (ascii/hAnsi preferred; theme fonts fallback)
        var fonts = rPr.Element(W + "rFonts");
        if (fonts is not null)
        {
            var ff = fonts.Attribute(W + "ascii")?.Value
                  ?? fonts.Attribute(W + "hAnsi")?.Value;
            if (ff is not null) run.Attributes["fontFamily"] = ff;
        }

        var rStyle = rPr.Element(W + "rStyle")?.Attribute(W + "val")?.Value;
        if (rStyle is not null) run.Attributes["style"] = rStyle;

        // Font color — OOXML stores as 6-digit hex without #; prefix it for WPF ColorConverter
        var color = rPr.Element(W + "color")?.Attribute(W + "val")?.Value;
        if (color is not null && color != "auto")
            run.Attributes["color"] = color.StartsWith('#') ? color : $"#{color}";

        // Highlight / shading
        var highlight = rPr.Element(W + "highlight")?.Attribute(W + "val")?.Value;
        if (highlight is not null) run.Attributes["highlight"] = highlight;

        // Strikethrough
        if (rPr.Element(W + "strike") is not null) run.Attributes["strikethrough"] = true;

        // Vertical alignment (superscript / subscript)
        var vertAlign = rPr.Element(W + "vertAlign")?.Attribute(W + "val")?.Value;
        if (vertAlign is not null) run.Attributes["vertAlign"] = vertAlign;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>EMU (English Metric Units) → pixels at 96 dpi.</summary>
    private static double? ParseEmu(string? s) =>
        s is not null && long.TryParse(s, out var v) ? v / 914400.0 * 96.0 : null;

    private static long ResolveOffset(XElement elem, long baseOffset)
    {
        if (elem is System.Xml.IXmlLineInfo li && li.HasLineInfo())
            return (long)li.LineNumber * 1000 + li.LinePosition;
        return baseOffset;
    }

    private static int EstimateLength(XElement elem) =>
        System.Text.Encoding.UTF8.GetByteCount(elem.ToString());
}
