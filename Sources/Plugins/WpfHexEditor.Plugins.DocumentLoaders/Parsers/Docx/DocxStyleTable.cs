// ==========================================================
// Project: WpfHexEditor.Plugins.DocumentLoaders
// File: Parsers/Docx/DocxStyleTable.cs
// Description:
//     Parses word/styles.xml into a flat lookup keyed by w:styleId.
//     Handles w:docDefaults (rPrDefault) and w:basedOn chains so the
//     mapper can apply pStyle/rStyle properties as a single resolved
//     ResolvedStyle without re-walking the inheritance graph at map-time.
// Architecture notes:
//     basedOn is flattened at parse time (parent first, child overrides)
//     with a HashSet cycle guard. Only paragraph + character style types
//     are exposed; table/numbering styles are ignored (renderer does not
//     consume them). Theme fonts (w:asciiTheme/hAnsiTheme) are intentionally
//     deferred — only direct ascii/hAnsi font names are read.
// ==========================================================

using System.Xml.Linq;

namespace WpfHexEditor.Plugins.DocumentLoaders.Parsers.Docx;

internal sealed class DocxStyleTable
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    public static readonly DocxStyleTable Empty = new(
        new Dictionary<string, ResolvedStyle>(StringComparer.Ordinal),
        new Dictionary<string, ResolvedStyle>(StringComparer.Ordinal),
        defaultFont: null,
        defaultSizePt: null);

    private readonly IReadOnlyDictionary<string, ResolvedStyle> _paragraphStyles;
    private readonly IReadOnlyDictionary<string, ResolvedStyle> _characterStyles;

    public string? DefaultFont    { get; }
    public double? DefaultSizePt  { get; }

    private DocxStyleTable(
        IReadOnlyDictionary<string, ResolvedStyle> paragraphStyles,
        IReadOnlyDictionary<string, ResolvedStyle> characterStyles,
        string? defaultFont,
        double? defaultSizePt)
    {
        _paragraphStyles = paragraphStyles;
        _characterStyles = characterStyles;
        DefaultFont      = defaultFont;
        DefaultSizePt    = defaultSizePt;
    }

    public bool TryResolveParagraph(string styleId, out ResolvedStyle style) =>
        _paragraphStyles.TryGetValue(styleId, out style!);

    public bool TryResolveCharacter(string styleId, out ResolvedStyle style) =>
        _characterStyles.TryGetValue(styleId, out style!);

    /// <summary>
    /// Parses <c>word/styles.xml</c>. Returns <see cref="Empty"/> for null/invalid input.
    /// </summary>
    public static DocxStyleTable Parse(string? stylesXml)
    {
        if (string.IsNullOrEmpty(stylesXml)) return Empty;
        try
        {
            var doc = XDocument.Parse(stylesXml);

            var (defFont, defSize) = ReadDocDefaults(doc);

            // Index raw (unflattened) styles by id, separated by type.
            var rawParagraph = new Dictionary<string, RawStyle>(StringComparer.Ordinal);
            var rawCharacter = new Dictionary<string, RawStyle>(StringComparer.Ordinal);

            foreach (var styleEl in doc.Descendants(W + "style"))
            {
                var id   = styleEl.Attribute(W + "styleId")?.Value;
                var type = styleEl.Attribute(W + "type")?.Value;
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(type)) continue;
                if (type != "paragraph" && type != "character") continue;

                var raw = ReadRawStyle(styleEl);
                if (type == "paragraph") rawParagraph[id] = raw;
                else                     rawCharacter[id] = raw;
            }

            // Flatten basedOn chains (parent first, child overrides) with cycle guard.
            var paragraph = FlattenAll(rawParagraph);
            var character = FlattenAll(rawCharacter);

            return new DocxStyleTable(paragraph, character, defFont, defSize);
        }
        catch
        {
            return Empty;
        }
    }

    // ── Flattening ────────────────────────────────────────────────────────────

    private static Dictionary<string, ResolvedStyle> FlattenAll(
        IReadOnlyDictionary<string, RawStyle> raw)
    {
        var resolved = new Dictionary<string, ResolvedStyle>(StringComparer.Ordinal);
        foreach (var id in raw.Keys)
            resolved[id] = Flatten(id, raw, new HashSet<string>(StringComparer.Ordinal));
        return resolved;
    }

    private static ResolvedStyle Flatten(string id,
        IReadOnlyDictionary<string, RawStyle> raw,
        HashSet<string> visited)
    {
        if (!raw.TryGetValue(id, out var self))
            return ResolvedStyle.Empty;
        if (!visited.Add(id))
            return ResolvedStyle.Empty;

        var parent = self.BasedOn is { Length: > 0 } b
            ? Flatten(b, raw, visited)
            : ResolvedStyle.Empty;

        return new ResolvedStyle(
            Font:   self.Font   ?? parent.Font,
            SizePt: self.SizePt ?? parent.SizePt,
            Bold:   self.Bold   ?? parent.Bold,
            Italic: self.Italic ?? parent.Italic,
            Color:  self.Color  ?? parent.Color);
    }

    // ── Raw extraction (single style element) ─────────────────────────────────

    private static RawStyle ReadRawStyle(XElement styleEl)
    {
        var basedOn = styleEl.Element(W + "basedOn")?.Attribute(W + "val")?.Value;
        var rPr     = styleEl.Element(W + "rPr");
        if (rPr is null) return new RawStyle(null, null, null, null, null, basedOn);

        var (font, size, bold, italic, color) = ReadRPr(rPr);
        return new RawStyle(font, size, bold, italic, color, basedOn);
    }

    private static (string? family, double? sizePt) ReadDocDefaults(XDocument doc)
    {
        var rPrDef = doc.Descendants(W + "rPrDefault").FirstOrDefault()?.Element(W + "rPr");
        if (rPrDef is null) return (null, null);

        var (font, size, _, _, _) = ReadRPr(rPrDef);
        return (font, size);
    }

    /// <summary>
    /// Extracts font/size/bold/italic/color from a w:rPr element.
    /// Mirrors DocxXmlMapper.ExtractRunProps semantics so the resolved
    /// ResolvedStyle and the direct rPr produce equivalent attribute sets.
    /// </summary>
    private static (string? font, double? sizePt, bool? bold, bool? italic, string? color)
        ReadRPr(XElement rPr)
    {
        // Font: prefer ascii, fallback to hAnsi. Theme variants ignored (Phase 1).
        string? font = null;
        var fonts = rPr.Element(W + "rFonts");
        if (fonts is not null)
        {
            font = fonts.Attribute(W + "ascii")?.Value
                ?? fonts.Attribute(W + "hAnsi")?.Value;
        }

        // Size: w:sz is half-points; prefer w:sz over w:szCs.
        double? sizePt = null;
        var szRaw = rPr.Element(W + "sz")?.Attribute(W + "val")?.Value
                 ?? rPr.Element(W + "szCs")?.Attribute(W + "val")?.Value;
        if (szRaw is not null && int.TryParse(szRaw, out int hpt))
            sizePt = hpt / 2.0;

        // Bold / italic: presence = on; explicit val="0"/"false" = off.
        bool? bold   = ReadBoolToggle(rPr.Element(W + "b"));
        bool? italic = ReadBoolToggle(rPr.Element(W + "i"));

        // Color: hex without '#' — prefix it for WPF ColorConverter consistency.
        string? color = null;
        var colorRaw = rPr.Element(W + "color")?.Attribute(W + "val")?.Value;
        if (colorRaw is not null && colorRaw != "auto")
            color = colorRaw.StartsWith('#') ? colorRaw : $"#{colorRaw}";

        return (font, sizePt, bold, italic, color);
    }

    private static bool? ReadBoolToggle(XElement? toggle)
    {
        if (toggle is null) return null;
        var val = toggle.Attribute(W + "val")?.Value;
        if (val is "0" or "false") return false;
        return true;
    }

    // ── Records ───────────────────────────────────────────────────────────────

    private sealed record RawStyle(
        string? Font,
        double? SizePt,
        bool?   Bold,
        bool?   Italic,
        string? Color,
        string? BasedOn);

    /// <summary>
    /// Flattened style values ready to apply to a DocumentBlock.
    /// Null fields mean "not set by this style chain" — caller falls
    /// back to docDefaults or the renderer's hardcoded defaults.
    /// </summary>
    public readonly record struct ResolvedStyle(
        string? Font,
        double? SizePt,
        bool?   Bold,
        bool?   Italic,
        string? Color)
    {
        public static ResolvedStyle Empty => default;

        public bool IsEmpty =>
            Font is null && SizePt is null && Bold is null && Italic is null && Color is null;
    }
}
