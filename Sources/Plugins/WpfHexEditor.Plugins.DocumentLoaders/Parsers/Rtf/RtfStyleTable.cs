// ==========================================================
// Project: WpfHexEditor.Plugins.DocumentLoaders
// File: Parsers/Rtf/RtfStyleTable.cs
// Description:
//     Mutable RTF resource tables built incrementally as the
//     RtfStructureBuilder encounters \fonttbl, \colortbl and
//     \stylesheet destinations. After the header is consumed,
//     TryResolveFont/Color/Paragraph/Character provide a fast
//     lookup mirroring DocxStyleTable's API.
// Architecture notes:
//     RTF is a token stream — no second pass possible. The
//     tables are populated as their groups close, and styles
//     applied to runs/paragraphs after that. \sbasedon chains
//     are flattened lazily on first lookup with a cycle guard.
// ==========================================================

namespace WpfHexEditor.Plugins.DocumentLoaders.Parsers.Rtf;

internal sealed class RtfStyleTable
{
    private readonly Dictionary<int, string>        _fontNames    = [];
    private readonly Dictionary<int, string>        _colors       = []; // index → "#RRGGBB"
    private readonly Dictionary<int, RtfRawStyle>   _rawParagraph = [];
    private readonly Dictionary<int, RtfRawStyle>   _rawCharacter = [];

    // Lazy flattened cache (cleared on table mutation rare enough to ignore).
    private readonly Dictionary<int, RtfResolvedStyle> _flatParagraph = [];
    private readonly Dictionary<int, RtfResolvedStyle> _flatCharacter = [];

    public void AddFont(int index, string name)
    {
        if (index < 0 || string.IsNullOrEmpty(name)) return;
        _fontNames[index] = name;
    }

    public void AddColor(int index, byte r, byte g, byte b) =>
        _colors[index] = $"#{r:X2}{g:X2}{b:X2}";

    /// <summary>Adds the implicit "auto" color at index 0 if no color has been added yet.</summary>
    public void EnsureAutoColor()
    {
        if (_colors.Count == 0) _colors[0] = string.Empty;  // sentinel = use default
    }

    public void AddParagraphStyle(int index, RtfRawStyle raw)
    {
        _rawParagraph[index] = raw;
        _flatParagraph.Clear();
    }

    public void AddCharacterStyle(int index, RtfRawStyle raw)
    {
        _rawCharacter[index] = raw;
        _flatCharacter.Clear();
    }

    public bool TryResolveFont(int index, out string family)
    {
        if (_fontNames.TryGetValue(index, out var n) && !string.IsNullOrEmpty(n))
        { family = n; return true; }
        family = string.Empty;
        return false;
    }

    public bool TryResolveColor(int index, out string hex)
    {
        if (_colors.TryGetValue(index, out var c) && !string.IsNullOrEmpty(c))
        { hex = c; return true; }
        hex = string.Empty;
        return false;
    }

    public bool TryResolveParagraph(int index, out RtfResolvedStyle style)
    {
        if (_flatParagraph.TryGetValue(index, out style)) return !style.IsEmpty;
        style = Flatten(index, _rawParagraph, new HashSet<int>());
        _flatParagraph[index] = style;
        return !style.IsEmpty;
    }

    public bool TryResolveCharacter(int index, out RtfResolvedStyle style)
    {
        if (_flatCharacter.TryGetValue(index, out style)) return !style.IsEmpty;
        style = Flatten(index, _rawCharacter, new HashSet<int>());
        _flatCharacter[index] = style;
        return !style.IsEmpty;
    }

    private RtfResolvedStyle Flatten(
        int index,
        IReadOnlyDictionary<int, RtfRawStyle> raw,
        HashSet<int> visited)
    {
        if (!raw.TryGetValue(index, out var self)) return RtfResolvedStyle.Empty;
        if (!visited.Add(index))                   return RtfResolvedStyle.Empty;

        var parent = self.BasedOn is int b && b >= 0
            ? Flatten(b, raw, visited)
            : RtfResolvedStyle.Empty;

        // Resolve fontIndex → family via _fontNames here, so cascade carries family.
        string? family = self.FontFamily;
        if (family is null && self.FontIndex is int fi && _fontNames.TryGetValue(fi, out var fn))
            family = fn;

        string? color = self.Color;
        if (color is null && self.ColorIndex is int ci && _colors.TryGetValue(ci, out var hex) && !string.IsNullOrEmpty(hex))
            color = hex;

        return new RtfResolvedStyle(
            Font:   family       ?? parent.Font,
            SizePt: self.SizePt   ?? parent.SizePt,
            Bold:   self.Bold     ?? parent.Bold,
            Italic: self.Italic   ?? parent.Italic,
            Color:  color         ?? parent.Color);
    }
}

/// <summary>Direct properties read from a single \s/\cs group (pre-flattening).</summary>
internal sealed record RtfRawStyle(
    int?    FontIndex,
    string? FontFamily,
    double? SizePt,
    bool?   Bold,
    bool?   Italic,
    int?    ColorIndex,
    string? Color,
    int?    BasedOn);

/// <summary>Flattened style — same shape as DocxStyleTable.ResolvedStyle.</summary>
internal readonly record struct RtfResolvedStyle(
    string? Font,
    double? SizePt,
    bool?   Bold,
    bool?   Italic,
    string? Color)
{
    public static RtfResolvedStyle Empty => default;

    public bool IsEmpty =>
        Font is null && SizePt is null && Bold is null && Italic is null && Color is null;
}
