// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor.Core
// File: Options/DocumentPageSettings.cs
// Description:
//     Immutable page layout settings equivalent to LibreOffice / Word
//     Page Style dialog: paper size, orientation, margins, columns,
//     header/footer, and page border.
// Architecture:
//     init-only properties; callers create new instances via
//     DocumentPageSettings { … with … } syntax when changing settings.
// ==========================================================

namespace WpfHexEditor.Editor.DocumentEditor.Core.Options;

public enum DocumentPageSize        { A4, A3, A5, Letter, Legal, Custom }
public enum DocumentPageOrientation { Portrait, Landscape }
public enum DocumentPageBorderStyle { None, Box, Shadow }

public sealed class DocumentPageSettings
{
    /// <summary>Default A4 portrait with standard margins.</summary>
    public static readonly DocumentPageSettings Default = new();

    // ── Paper ─────────────────────────────────────────────────────────────────

    public DocumentPageSize        PageSize    { get; init; } = DocumentPageSize.A4;
    public DocumentPageOrientation Orientation { get; init; } = DocumentPageOrientation.Portrait;

    /// <summary>Custom page width in pixels at 96 dpi (only used when PageSize = Custom).</summary>
    public double CustomWidth       { get; init; } = 794.0;
    /// <summary>Custom page height in pixels at 96 dpi (only used when PageSize = Custom).</summary>
    public double CustomHeight      { get; init; } = 1122.0;

    // ── Margins (pixels at 96 dpi) ────────────────────────────────────────────

    public double MarginTop         { get; init; } = 40.0;
    public double MarginBottom      { get; init; } = 56.0;
    /// <summary>Left margin, or "Inside" margin when MirrorMargins is true.</summary>
    public double MarginLeft        { get; init; } = 56.0;
    /// <summary>Right margin, or "Outside" margin when MirrorMargins is true.</summary>
    public double MarginRight       { get; init; } = 56.0;
    /// <summary>Extra space added to the binding edge (gutter).</summary>
    public double MarginGutter      { get; init; } = 0.0;
    /// <summary>When true, Left/Right become Inside/Outside for two-sided printing.</summary>
    public bool   MirrorMargins     { get; init; } = false;

    // ── Columns ───────────────────────────────────────────────────────────────

    /// <summary>Number of text columns (1–5).</summary>
    public int    ColumnCount              { get; init; } = 1;
    /// <summary>When true all columns share the same width; otherwise custom widths apply.</summary>
    public bool   EqualColumnWidths        { get; init; } = true;
    /// <summary>Gap between columns in pixels.</summary>
    public double ColumnGapPx              { get; init; } = 20.0;
    /// <summary>Draw a vertical separator line between columns.</summary>
    public bool   ShowColumnSeparatorLine  { get; init; } = false;

    // ── Header ────────────────────────────────────────────────────────────────

    public bool   HeaderEnabled            { get; init; } = false;
    public double HeaderHeightPx           { get; init; } = 38.0;
    /// <summary>Gap between the bottom of the header area and the top of body content.</summary>
    public double HeaderMarginPx           { get; init; } = 10.0;
    /// <summary>First page uses a different header.</summary>
    public bool   HeaderDifferentFirstPage { get; init; } = false;
    /// <summary>Even/odd pages share the same header (Word-style).</summary>
    public bool   HeaderSameLeftRight      { get; init; } = true;

    // ── Footer ────────────────────────────────────────────────────────────────

    public bool   FooterEnabled            { get; init; } = false;
    public double FooterHeightPx           { get; init; } = 38.0;
    public double FooterMarginPx           { get; init; } = 10.0;
    public bool   FooterDifferentFirstPage { get; init; } = false;
    public bool   FooterSameLeftRight      { get; init; } = true;

    // ── Page Border ───────────────────────────────────────────────────────────

    public DocumentPageBorderStyle BorderStyle     { get; init; } = DocumentPageBorderStyle.None;
    /// <summary>Hex color string, e.g. "#000000".</summary>
    public string  BorderColor                     { get; init; } = "#000000";
    public double  BorderWidthPx                   { get; init; } = 1.0;
    /// <summary>Gap between page content and the border line.</summary>
    public double  BorderPaddingPx                 { get; init; } = 8.0;

    // ── Computed page dimensions ──────────────────────────────────────────────

    private static (double W, double H) BasePortraitSize(DocumentPageSize s) => s switch
    {
        DocumentPageSize.A4     => (794.0,  1122.0),   // 210 × 297 mm at 96 dpi
        DocumentPageSize.A3     => (1122.0, 1587.0),   // 297 × 420 mm
        DocumentPageSize.A5     => (559.0,   794.0),   // 148 × 210 mm
        DocumentPageSize.Letter => (816.0,  1056.0),   // 8.5 × 11 in
        DocumentPageSize.Legal  => (816.0,  1368.0),   // 8.5 × 14.25 in
        _                       => (794.0,  1122.0)
    };

    public double EffectivePageWidth
    {
        get
        {
            var (w, h) = PageSize == DocumentPageSize.Custom
                ? (CustomWidth, CustomHeight)
                : BasePortraitSize(PageSize);
            return Orientation == DocumentPageOrientation.Landscape ? h : w;
        }
    }

    public double EffectivePageHeight
    {
        get
        {
            var (w, h) = PageSize == DocumentPageSize.Custom
                ? (CustomWidth, CustomHeight)
                : BasePortraitSize(PageSize);
            return Orientation == DocumentPageOrientation.Landscape ? w : h;
        }
    }

    /// <summary>Horizontal content width (page width minus left + right margins).</summary>
    public double ContentWidth => EffectivePageWidth - MarginLeft - MarginRight;

    /// <summary>Top Y of body text within the page card (accounts for header area).</summary>
    public double ContentTopY =>
        MarginTop + (HeaderEnabled ? HeaderHeightPx + HeaderMarginPx : 0);

    /// <summary>Usable body content height per page.</summary>
    public double ContentHeight =>
        EffectivePageHeight
        - ContentTopY
        - MarginBottom
        - (FooterEnabled ? FooterHeightPx + FooterMarginPx : 0);

    /// <summary>Width of a single column (equal-width layout).</summary>
    public double SingleColumnWidth()
    {
        if (ColumnCount <= 1) return ContentWidth;
        double totalGap = ColumnGapPx * (ColumnCount - 1);
        return Math.Max(10, (ContentWidth - totalGap) / ColumnCount);
    }

    // ── Factory helpers ───────────────────────────────────────────────────────

    /// <summary>Converts twips (1/1440 inch) to pixels at 96 dpi.</summary>
    public static double TwipsToPx(int twips) => twips / 1440.0 * 96.0;

    /// <summary>
    /// Builds page settings from DOCX <c>w:pgSz</c> / <c>w:pgMar</c> values (all in twips).
    /// Pass <paramref name="orient"/> = 1 for landscape.
    /// </summary>
    public static DocumentPageSettings FromDocx(
        int pgW, int pgH, int? orient,
        int mTop, int mBot, int mLeft, int mRight, int mGutter) => new()
    {
        PageSize     = DocumentPageSize.Custom,
        Orientation  = orient == 1 ? DocumentPageOrientation.Landscape : DocumentPageOrientation.Portrait,
        CustomWidth  = TwipsToPx(pgW),
        CustomHeight = TwipsToPx(pgH),
        MarginTop    = TwipsToPx(mTop),
        MarginBottom = TwipsToPx(mBot),
        MarginLeft   = TwipsToPx(mLeft),
        MarginRight  = TwipsToPx(mRight),
        MarginGutter = TwipsToPx(mGutter),
    };

    /// <summary>
    /// Builds page settings from ODT <c>fo:</c> dimension strings (e.g. "21cm", "297mm", "8.5in").
    /// </summary>
    public static DocumentPageSettings FromOdt(
        string? pgW, string? pgH,
        string? mTop, string? mBot, string? mLeft, string? mRight) => new()
    {
        PageSize     = DocumentPageSize.Custom,
        CustomWidth  = ParseOdtDim(pgW) ?? 794,
        CustomHeight = ParseOdtDim(pgH) ?? 1122,
        MarginTop    = ParseOdtDim(mTop) ?? 40,
        MarginBottom = ParseOdtDim(mBot) ?? 56,
        MarginLeft   = ParseOdtDim(mLeft) ?? 56,
        MarginRight  = ParseOdtDim(mRight) ?? 56,
    };

    /// <summary>Parses ODT/CSS dimension strings: "21cm", "297mm", "8.5in", "595pt" → pixels at 96 dpi.</summary>
    private static double? ParseOdtDim(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        if (s.EndsWith("cm",  StringComparison.OrdinalIgnoreCase) &&
            double.TryParse(s[..^2], System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var cm))
            return cm * 37.7953;
        if (s.EndsWith("mm",  StringComparison.OrdinalIgnoreCase) &&
            double.TryParse(s[..^2], System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var mm))
            return mm * 3.77953;
        if (s.EndsWith("in",  StringComparison.OrdinalIgnoreCase) &&
            double.TryParse(s[..^2], System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var inch))
            return inch * 96.0;
        if (s.EndsWith("pt",  StringComparison.OrdinalIgnoreCase) &&
            double.TryParse(s[..^2], System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var pt))
            return pt * 1.33333;
        return null;
    }
}
