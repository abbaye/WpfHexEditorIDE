// ==========================================================
// Project: WpfHexEditor.Plugins.DocumentLoaders
// File: Parsers/Rtf/RtfStructureBuilder.cs
// Description:
//     Consumes an RtfTokenizer stream and builds a DocumentBlock tree.
//     Now resolves font/color/named-style references via RtfStyleTable
//     so runs receive fontFamily/fontSize(pt)/color(#RRGGBB)/style cascade
//     equivalent to the DOCX pipeline (ADR-013).
// Architecture notes:
//     RTF is single-pass: \fonttbl, \colortbl, \stylesheet groups appear
//     in the header before any text runs (per spec). We capture them as
//     they close, then apply resolved values when seeing \f<n>, \cf<n>,
//     \s<n>, \cs<n> on subsequent runs.
//     fontSize stored as double POINTS (\fs is half-points → /2).
// ==========================================================

using System.Globalization;
using System.Text;
using WpfHexEditor.Editor.DocumentEditor.Core.BinaryMap;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;
using WpfHexEditor.Editor.DocumentEditor.Core.Options;

namespace WpfHexEditor.Plugins.DocumentLoaders.Parsers.Rtf;

internal sealed class RtfStructureBuilder
{
    // Page dimensions extracted from \paperw, \paperh, \margl, etc.
    private int _pageW, _pageH, _margl, _margr, _margt, _margb;

    // Resource tables, populated as their destination groups close.
    private readonly RtfStyleTable _styleTable = new();

    public DocumentPageSettings? ExtractedPageSettings =>
        _pageW > 0 && _pageH > 0
            ? DocumentPageSettings.FromDocx(_pageW, _pageH, null,
                _margt, _margb, _margl, _margr, 0)
            : null;

    public (List<DocumentBlock> Roots, DocumentMetadata Metadata) Build(
        RtfTokenizer     tokenizer,
        BinaryMapBuilder mapBuilder,
        CancellationToken ct = default)
    {
        var roots    = new List<DocumentBlock>();
        var metadata = new DocumentMetadata { MimeType = "application/rtf" };

        var rootCtx = new GroupContext(null, 0);
        WalkGroup(tokenizer, rootCtx, mapBuilder, metadata, ct);

        foreach (var b in rootCtx.Children)
            roots.Add(b);

        return (roots, metadata);
    }

    private void WalkGroup(
        RtfTokenizer     tokenizer,
        GroupContext     ctx,
        BinaryMapBuilder mapBuilder,
        DocumentMetadata metadata,
        CancellationToken ct)
    {
        DocumentBlock? currentParagraph = null;
        DocumentBlock? currentRun       = null;
        long           runStart         = -1;

        // Lightweight buffer used for collecting font names / color components /
        // \stylesheet sub-group properties without allocating a DocumentBlock.
        var headerBuf = new StringBuilder();

        void FlushRun()
        {
            if (currentRun is null) return;
            currentParagraph?.Children.Add(currentRun);
            mapBuilder.Add(currentRun, currentRun.RawOffset, currentRun.RawLength);
            currentRun = null;
        }

        void FlushParagraph()
        {
            FlushRun();
            if (currentParagraph is null) return;
            ctx.Children.Add(currentParagraph);
            mapBuilder.Add(currentParagraph, currentParagraph.RawOffset, currentParagraph.RawLength);
            currentParagraph = null;
        }

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var tok = tokenizer.NextToken(ct);

            switch (tok.Kind)
            {
                case RtfTokenKind.EndOfStream:
                    FlushParagraph();
                    return;

                case RtfTokenKind.GroupOpen:
                {
                    FlushRun();
                    var child = new GroupContext(ctx, tok.Offset)
                    {
                        // Inherit destination so nested groups inside \fonttbl/\colortbl/\stylesheet
                        // are still recognized as resource entries.
                        Destination = InheritsDestination(ctx.Destination) ? ctx.Destination : RtfDestination.None
                    };
                    WalkGroup(tokenizer, child, mapBuilder, metadata, ct);

                    // Emit the closed sub-group as a content block when applicable.
                    var block = child.ToBlock();
                    if (block is not null)
                    {
                        currentParagraph ??= MakeParagraph(tok.Offset);
                        currentParagraph.Children.Add(block);
                        mapBuilder.Add(block, block.RawOffset, block.RawLength);
                    }

                    // Capture sub-group output for \fonttbl / \stylesheet entries.
                    if (ctx.Destination == RtfDestination.FontTable && child.PendingFontIndex is int fIdx)
                    {
                        var name = child.HeaderText.ToString().Trim().TrimEnd(';').Trim();
                        if (name.Length > 0) _styleTable.AddFont(fIdx, name);
                    }
                    else if (ctx.Destination == RtfDestination.StyleSheet && child.PendingStyle is RtfRawStyle rs)
                    {
                        var name = child.HeaderText.ToString().Trim().TrimEnd(';').Trim();
                        if (child.PendingParagraphStyleIndex is int pi)
                            _styleTable.AddParagraphStyle(pi, rs);
                        if (child.PendingCharacterStyleIndex is int ci)
                            _styleTable.AddCharacterStyle(ci, rs);
                        _ = name; // name kept for future surface; cascade is index-based
                    }
                    break;
                }

                case RtfTokenKind.GroupClose:
                    // Finalize \colortbl on close (semicolon-separated entries already accumulated).
                    FlushParagraph();
                    ctx.CloseOffset = tok.Offset + tok.Length;
                    return;

                case RtfTokenKind.ControlWord:
                    HandleControlWord(tok, ctx, ref currentParagraph, ref currentRun,
                                      ref runStart, mapBuilder, metadata, FlushRun, FlushParagraph);
                    break;

                case RtfTokenKind.ControlSymbol:
                    // Inside \colortbl, ';' is a literal text token, not a symbol — ignore here.
                    AppendSymbolText(tok, ref currentParagraph, ref currentRun, ref runStart, tok.Offset);
                    break;

                case RtfTokenKind.Text:
                    if (string.IsNullOrEmpty(tok.Text)) break;

                    // Text inside resource destinations feeds the resource parser,
                    // not the document body.
                    if (ctx.Destination == RtfDestination.FontTable ||
                        ctx.Destination == RtfDestination.StyleSheet)
                    {
                        ctx.HeaderText.Append(tok.Text);
                        break;
                    }
                    if (ctx.Destination == RtfDestination.ColorTable)
                    {
                        // \colortbl entries: \red0\green0\blue0; — semicolon ends one color.
                        // The text token here typically contains the trailing ';' plus whitespace.
                        if (tok.Text.Contains(';'))
                            ctx.FlushPendingColor(_styleTable);
                        break;
                    }

                    currentParagraph ??= MakeParagraph(tok.Offset);
                    currentRun       ??= MakeRun(tok.Offset);
                    ApplyPendingFormatting(currentRun, ctx);
                    runStart          = runStart < 0 ? tok.Offset : runStart;
                    currentRun.Text  += tok.Text;
                    SetRawLength(ref currentRun, tok.Offset + tok.Length);
                    break;

                case RtfTokenKind.BinaryData:
                {
                    FlushRun();
                    currentParagraph ??= MakeParagraph(tok.Offset);
                    var imgBlock = new DocumentBlock
                    {
                        Kind      = "image",
                        RawOffset = tok.Offset,
                        RawLength = tok.Length,
                        Text      = $"[binary data {tok.Parameter} bytes]"
                    };
                    imgBlock.Attributes["binarySize"] = tok.Parameter;
                    if (tok.Binary is not null)
                        imgBlock.Attributes["binaryData"] = tok.Binary;

                    currentParagraph.Children.Add(imgBlock);
                    mapBuilder.Add(imgBlock, tok.Offset, tok.Length);
                    break;
                }
            }
        }
    }

    private static bool InheritsDestination(RtfDestination d) =>
        d is RtfDestination.FontTable or RtfDestination.ColorTable or RtfDestination.StyleSheet;

    private void HandleControlWord(
        RtfToken         tok,
        GroupContext     ctx,
        ref DocumentBlock? currentParagraph,
        ref DocumentBlock? currentRun,
        ref long           runStart,
        BinaryMapBuilder   mapBuilder,
        DocumentMetadata   metadata,
        Action             flushRun,
        Action             flushParagraph)
    {
        // ── Color-table component words ────────────────────────────────────────
        if (ctx.Destination == RtfDestination.ColorTable)
        {
            switch (tok.Word)
            {
                case "red":   ctx.PendingR = (byte)(tok.Parameter & 0xFF); ctx.HasPendingColor = true; return;
                case "green": ctx.PendingG = (byte)(tok.Parameter & 0xFF); ctx.HasPendingColor = true; return;
                case "blue":  ctx.PendingB = (byte)(tok.Parameter & 0xFF); ctx.HasPendingColor = true; return;
            }
        }

        // ── Font-table entry index ─────────────────────────────────────────────
        if (ctx.Destination == RtfDestination.FontTable && tok.Word == "f" && tok.Parameter != int.MinValue)
        {
            ctx.PendingFontIndex = tok.Parameter;
            return;
        }

        // ── Stylesheet entry: \s<n> = paragraph, \cs<n> = character, \ds<n> = section ──
        if (ctx.Destination == RtfDestination.StyleSheet)
        {
            switch (tok.Word)
            {
                case "s"  when tok.Parameter != int.MinValue: ctx.PendingParagraphStyleIndex = tok.Parameter; ctx.EnsurePendingStyle(); return;
                case "cs" when tok.Parameter != int.MinValue: ctx.PendingCharacterStyleIndex = tok.Parameter; ctx.EnsurePendingStyle(); return;
                case "sbasedon" when tok.Parameter != int.MinValue:
                    ctx.UpdatePendingStyle(s => s with { BasedOn = tok.Parameter });
                    return;
                case "f"  when tok.Parameter != int.MinValue: ctx.UpdatePendingStyle(s => s with { FontIndex = tok.Parameter }); return;
                case "fs" when tok.Parameter != int.MinValue: ctx.UpdatePendingStyle(s => s with { SizePt = tok.Parameter / 2.0 }); return;
                case "b":   ctx.UpdatePendingStyle(s => s with { Bold   = tok.Parameter != 0 }); return;
                case "i":   ctx.UpdatePendingStyle(s => s with { Italic = tok.Parameter != 0 }); return;
                case "cf" when tok.Parameter != int.MinValue: ctx.UpdatePendingStyle(s => s with { ColorIndex = tok.Parameter }); return;
            }
        }

        // ── Body of the document ───────────────────────────────────────────────
        switch (tok.Word)
        {
            case "paperw": _pageW = tok.Parameter; break;
            case "paperh": _pageH = tok.Parameter; break;
            case "margl":  _margl = tok.Parameter; break;
            case "margr":  _margr = tok.Parameter; break;
            case "margt":  _margt = tok.Parameter; break;
            case "margb":  _margb = tok.Parameter; break;

            case "par":
            case "pard":
                flushParagraph();
                if (tok.Word == "pard") ctx.ResetParagraphFormatting();
                break;

            case "line":
                if (currentRun is not null) currentRun.Text += "\n";
                break;

            case "tab":
                if (currentRun is not null) currentRun.Text += "\t";
                break;

            // Direct character formatting — buffered on ctx until next text token
            // so the run carries the formatting active at the moment text starts.
            case "b":
                ctx.PendingBold = tok.Parameter != 0;
                if (currentRun is not null) currentRun.Attributes["bold"] = ctx.PendingBold.Value;
                break;

            case "i":
                ctx.PendingItalic = tok.Parameter != 0;
                if (currentRun is not null) currentRun.Attributes["italic"] = ctx.PendingItalic.Value;
                break;

            case "ul":
                ctx.PendingUnderline = tok.Parameter != 0;
                if (currentRun is not null) currentRun.Attributes["underline"] = ctx.PendingUnderline.Value;
                break;

            case "fs" when tok.Parameter != int.MinValue:
                ctx.PendingSizePt = tok.Parameter / 2.0;
                if (currentRun is not null) currentRun.Attributes["fontSize"] = ctx.PendingSizePt.Value;
                break;

            case "f" when tok.Parameter != int.MinValue:
                ctx.PendingFontRef = tok.Parameter;
                if (_styleTable.TryResolveFont(tok.Parameter, out var family))
                {
                    ctx.PendingFontFamily = family;
                    if (currentRun is not null) currentRun.Attributes["fontFamily"] = family;
                }
                break;

            case "cf" when tok.Parameter != int.MinValue:
                ctx.PendingColorRef = tok.Parameter;
                if (_styleTable.TryResolveColor(tok.Parameter, out var hex))
                {
                    ctx.PendingColor = hex;
                    if (currentRun is not null) currentRun.Attributes["color"] = hex;
                }
                break;

            // Named style references applied to following runs/paragraphs.
            case "s" when tok.Parameter != int.MinValue:
                if (_styleTable.TryResolveParagraph(tok.Parameter, out var pStyle))
                    ctx.ApplyParagraphStyle(pStyle);
                break;
            case "cs" when tok.Parameter != int.MinValue:
                if (_styleTable.TryResolveCharacter(tok.Parameter, out var cStyle))
                    ctx.ApplyCharacterStyle(cStyle);
                break;

            case "fonttbl":   ctx.Destination = RtfDestination.FontTable;   break;
            case "colortbl":  ctx.Destination = RtfDestination.ColorTable;  _styleTable.EnsureAutoColor(); break;
            case "stylesheet":ctx.Destination = RtfDestination.StyleSheet;  break;
            case "info":      ctx.Destination = RtfDestination.Info;        break;

            case "author":
                if (ctx.Destination == RtfDestination.Info)
                    ctx.PendingMetaKey = "author";
                break;

            case "title":
                if (ctx.Destination == RtfDestination.Info)
                    ctx.PendingMetaKey = "title";
                break;

            case "pict":   ctx.Destination = RtfDestination.Picture; break;
            case "object": ctx.Destination = RtfDestination.Object;  break;
        }
    }

    /// <summary>
    /// Applies any formatting buffered on the context (cascade + direct rPr) to a
    /// freshly created run. Called the first time text is appended to the run.
    /// </summary>
    private static void ApplyPendingFormatting(DocumentBlock run, GroupContext ctx)
    {
        if (run.Attributes.Count > 0) return; // already initialized

        if (ctx.PendingFontFamily is not null) run.Attributes["fontFamily"] = ctx.PendingFontFamily;
        if (ctx.PendingSizePt    is double sz) run.Attributes["fontSize"]   = sz;
        if (ctx.PendingBold      is bool   bd) run.Attributes["bold"]       = bd;
        if (ctx.PendingItalic    is bool   it) run.Attributes["italic"]     = it;
        if (ctx.PendingUnderline is bool   ul) run.Attributes["underline"]  = ul;
        if (ctx.PendingColor     is not null)  run.Attributes["color"]      = ctx.PendingColor;
    }

    private static DocumentBlock MakeParagraph(long offset) =>
        new() { Kind = "paragraph", RawOffset = offset, RawLength = 0 };

    private static DocumentBlock MakeRun(long offset) =>
        new() { Kind = "run", RawOffset = offset, RawLength = 0 };

    private static void SetRawLength(ref DocumentBlock? block, long endOffset)
    {
        if (block is null) return;
        int newLen = (int)(endOffset - block.RawOffset);
        if (newLen <= block.RawLength) return;

        var updated = new DocumentBlock
        {
            Kind      = block.Kind,
            Text      = block.Text,
            RawOffset = block.RawOffset,
            RawLength = newLen
        };
        foreach (var (k, v) in block.Attributes)
            updated.Attributes[k] = v;
        foreach (var child in block.Children)
            updated.Children.Add(child);
        block = updated;
    }

    private static void AppendSymbolText(
        RtfToken         tok,
        ref DocumentBlock? para,
        ref DocumentBlock? run,
        ref long           runStart,
        long               offset)
    {
        string text = tok.Word switch
        {
            "\\" => "\\",
            "{"  => "{",
            "}"  => "}",
            "~"  => " ",
            "-"  => "­",
            _    => string.Empty
        };
        if (text.Length == 0) return;

        para     ??= MakeParagraph(offset);
        run      ??= MakeRun(offset);
        runStart   = runStart < 0 ? offset : runStart;
        run.Text  += text;
        SetRawLength(ref run, offset + tok.Length);
    }
}

internal enum RtfDestination
{
    None, FontTable, ColorTable, StyleSheet, Info, Picture, Object, Header, Footer
}

internal sealed class GroupContext(GroupContext? parent, long openOffset)
{
    public GroupContext?       Parent         { get; } = parent;
    public long                OpenOffset     { get; } = openOffset;
    public long                CloseOffset    { get; set; }
    public RtfDestination      Destination    { get; set; }
    public string?             PendingMetaKey { get; set; }
    public List<DocumentBlock> Children       { get; } = [];

    // Resource-parsing scratch state (only used inside font/color/stylesheet groups).
    public StringBuilder       HeaderText                 { get; } = new();
    public int?                PendingFontIndex           { get; set; }
    public int?                PendingParagraphStyleIndex { get; set; }
    public int?                PendingCharacterStyleIndex { get; set; }
    public RtfRawStyle?        PendingStyle               { get; set; }

    // Color-table accumulator
    public byte                PendingR;
    public byte                PendingG;
    public byte                PendingB;
    public bool                HasPendingColor;
    private int                _nextColorIndex; // first ;-terminated triplet → index 0

    // Cascading formatting state for runs in this group.
    public string? PendingFontFamily;
    public int?    PendingFontRef;
    public double? PendingSizePt;
    public bool?   PendingBold;
    public bool?   PendingItalic;
    public bool?   PendingUnderline;
    public string? PendingColor;
    public int?    PendingColorRef;

    public void EnsurePendingStyle() =>
        PendingStyle ??= new RtfRawStyle(null, null, null, null, null, null, null, null);

    public void UpdatePendingStyle(Func<RtfRawStyle, RtfRawStyle> mutate)
    {
        EnsurePendingStyle();
        PendingStyle = mutate(PendingStyle!);
    }

    public void FlushPendingColor(RtfStyleTable table)
    {
        if (!HasPendingColor)
        {
            // Empty entry (just ';') = "auto" placeholder; advance the index.
            _nextColorIndex++;
            return;
        }
        table.AddColor(_nextColorIndex++, PendingR, PendingG, PendingB);
        PendingR = PendingG = PendingB = 0;
        HasPendingColor = false;
    }

    /// <summary>Resets paragraph-level direct formatting on \pard.</summary>
    public void ResetParagraphFormatting()
    {
        // Only paragraph-mark properties reset here — character formatting persists.
        // RTF spec: \pard resets paragraph properties; \plain resets character.
    }

    /// <summary>Applies a resolved paragraph style as the cascade baseline.</summary>
    public void ApplyParagraphStyle(RtfResolvedStyle s) => ApplyResolved(s);

    /// <summary>Applies a resolved character style as the cascade baseline.</summary>
    public void ApplyCharacterStyle(RtfResolvedStyle s) => ApplyResolved(s);

    private void ApplyResolved(RtfResolvedStyle s)
    {
        // Style provides defaults — direct formatting that follows overrides.
        PendingFontFamily ??= s.Font;
        PendingSizePt     ??= s.SizePt;
        PendingBold       ??= s.Bold;
        PendingItalic     ??= s.Italic;
        PendingColor      ??= s.Color;
    }

    public DocumentBlock? ToBlock() => Destination switch
    {
        RtfDestination.Picture => new DocumentBlock
        {
            Kind      = "image",
            RawOffset = OpenOffset,
            RawLength = (int)(CloseOffset - OpenOffset),
            Text      = "[picture]"
        },
        RtfDestination.Object => new DocumentBlock
        {
            Kind      = "object",
            RawOffset = OpenOffset,
            RawLength = (int)(CloseOffset - OpenOffset),
            Text      = "[OLE object — offset only]"
        },
        _ => null
    };
}
