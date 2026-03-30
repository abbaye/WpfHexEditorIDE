// ==========================================================
// Project: WpfHexEditor.Plugins.DocumentLoaders
// File: Parsers/Rtf/RtfStructureBuilder.cs
// Description:
//     Consumes an RtfTokenizer stream and builds a DocumentBlock tree.
// ==========================================================

using WpfHexEditor.Editor.DocumentEditor.Core.BinaryMap;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Plugins.DocumentLoaders.Parsers.Rtf;

internal sealed class RtfStructureBuilder
{
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

    private static void WalkGroup(
        RtfTokenizer     tokenizer,
        GroupContext     ctx,
        BinaryMapBuilder mapBuilder,
        DocumentMetadata metadata,
        CancellationToken ct)
    {
        DocumentBlock? currentParagraph = null;
        DocumentBlock? currentRun       = null;
        long           runStart         = -1;

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
                    var child = new GroupContext(ctx, tok.Offset);
                    WalkGroup(tokenizer, child, mapBuilder, metadata, ct);

                    var block = child.ToBlock();
                    if (block is not null)
                    {
                        currentParagraph ??= MakeParagraph(tok.Offset);
                        currentParagraph.Children.Add(block);
                        mapBuilder.Add(block, block.RawOffset, block.RawLength);
                    }
                    break;
                }

                case RtfTokenKind.GroupClose:
                    FlushParagraph();
                    ctx.CloseOffset = tok.Offset + tok.Length;
                    return;

                case RtfTokenKind.ControlWord:
                    HandleControlWord(tok, ctx, ref currentParagraph, ref currentRun,
                                      ref runStart, mapBuilder, metadata, FlushRun, FlushParagraph);
                    break;

                case RtfTokenKind.ControlSymbol:
                    AppendSymbolText(tok, ref currentParagraph, ref currentRun, ref runStart, tok.Offset);
                    break;

                case RtfTokenKind.Text:
                    if (!string.IsNullOrEmpty(tok.Text))
                    {
                        currentParagraph ??= MakeParagraph(tok.Offset);
                        currentRun       ??= MakeRun(tok.Offset);
                        runStart          = runStart < 0 ? tok.Offset : runStart;
                        currentRun.Text  += tok.Text;
                        SetRawLength(ref currentRun, tok.Offset + tok.Length);
                    }
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

    private static void HandleControlWord(
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
        switch (tok.Word)
        {
            case "par":
            case "pard":
                flushParagraph();
                break;

            case "line":
                if (currentRun is not null) currentRun.Text += "\n";
                break;

            case "tab":
                if (currentRun is not null) currentRun.Text += "\t";
                break;

            case "b":
                EnsureRun(ref currentParagraph, ref currentRun, tok.Offset);
                currentRun!.Attributes["bold"] = tok.Parameter != 0;
                break;

            case "i":
                EnsureRun(ref currentParagraph, ref currentRun, tok.Offset);
                currentRun!.Attributes["italic"] = tok.Parameter != 0;
                break;

            case "ul":
                EnsureRun(ref currentParagraph, ref currentRun, tok.Offset);
                currentRun!.Attributes["underline"] = tok.Parameter != 0;
                break;

            case "fs":
                EnsureRun(ref currentParagraph, ref currentRun, tok.Offset);
                currentRun!.Attributes["fontSize"] = tok.Parameter;
                break;

            case "cf":
                EnsureRun(ref currentParagraph, ref currentRun, tok.Offset);
                currentRun!.Attributes["colorIndex"] = tok.Parameter;
                break;

            case "fonttbl":   ctx.Destination = RtfDestination.FontTable;   break;
            case "colortbl":  ctx.Destination = RtfDestination.ColorTable;  break;
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

    private static DocumentBlock MakeParagraph(long offset) =>
        new() { Kind = "paragraph", RawOffset = offset, RawLength = 0 };

    private static DocumentBlock MakeRun(long offset) =>
        new() { Kind = "run", RawOffset = offset, RawLength = 0 };

    private static void EnsureRun(ref DocumentBlock? para, ref DocumentBlock? run, long offset)
    {
        para ??= MakeParagraph(offset);
        run  ??= MakeRun(offset);
    }

    private static void SetRawLength(ref DocumentBlock? block, long endOffset)
    {
        if (block is null) return;
        int newLen = (int)(endOffset - block.RawOffset);
        if (newLen > block.RawLength)
        {
            block = new DocumentBlock
            {
                Kind      = block.Kind,
                Text      = block.Text,
                RawOffset = block.RawOffset,
                RawLength = newLen
            };
        }
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
            "~"  => "\u00A0",
            "-"  => "\u00AD",
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
