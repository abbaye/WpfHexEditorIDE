// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor.Core
// File: Editing/DocumentBlockFactory.cs
// Description:
//     Creates new DocumentBlocks with correct defaults.
//     All inserted blocks receive RawOffset=-1 (no original binary
//     position) until a save round-trip establishes real offsets.
// ==========================================================

using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Editor.DocumentEditor.Core.Editing;

/// <summary>
/// Factory for creating new <see cref="DocumentBlock"/> instances during editing.
/// All created blocks use <c>RawOffset = -1</c> to indicate no binary origin.
/// </summary>
public static class DocumentBlockFactory
{
    /// <summary>Creates a new empty paragraph block.</summary>
    public static DocumentBlock NewParagraph(string text = "") => new()
    {
        Kind      = "paragraph",
        Text      = text,
        RawOffset = -1,
        RawLength = 0
    };

    /// <summary>Creates a new run (inline text) block with optional bold/italic.</summary>
    public static DocumentBlock NewRun(string text = "", bool bold = false, bool italic = false)
    {
        var block = new DocumentBlock
        {
            Kind      = "run",
            Text      = text,
            RawOffset = -1,
            RawLength = 0
        };
        if (bold)   block.Attributes["bold"]   = true;
        if (italic) block.Attributes["italic"] = true;
        return block;
    }

    /// <summary>Creates a new heading block at the specified level (1–6).</summary>
    public static DocumentBlock NewHeading(int level, string text = "") => new()
    {
        Kind      = "heading",
        Text      = text,
        RawOffset = -1,
        RawLength = 0,
        Attributes = { ["level"] = Math.Clamp(level, 1, 6) }
    };

    /// <summary>Creates a new empty table-cell block.</summary>
    public static DocumentBlock NewTableCell() => new()
    {
        Kind      = "table-cell",
        Text      = string.Empty,
        RawOffset = -1,
        RawLength = 0
    };

    /// <summary>Creates a new table-row with the specified number of empty cells.</summary>
    public static DocumentBlock NewTableRow(int cellCount = 1)
    {
        var row = new DocumentBlock { Kind = "table-row", Text = string.Empty, RawOffset = -1, RawLength = 0 };
        for (var i = 0; i < cellCount; i++)
            row.Children.Add(NewTableCell());
        return row;
    }

    /// <summary>Creates a new list-item block at the given level (0-based) with bullet or numbered style.</summary>
    public static DocumentBlock NewListItem(string text = "", int level = 0, string listStyle = "bullet") => new()
    {
        Kind      = "list-item",
        Text      = text,
        RawOffset = -1,
        RawLength = 0,
        Attributes =
        {
            ["listLevel"] = level,
            ["listStyle"] = listStyle
        }
    };

    /// <summary>Creates a hyperlink block. Rendered as blue underlined text; Ctrl+Click opens the URL.</summary>
    public static DocumentBlock NewHyperlink(string text, string url) => new()
    {
        Kind      = "hyperlink",
        Text      = text,
        RawOffset = -1,
        RawLength = 0,
        Attributes = { ["href"] = url }
    };

    /// <summary>Creates a page-break block. Rendered as a dashed rule; forces a new page in paged layout.</summary>
    public static DocumentBlock NewPageBreak() => new()
    {
        Kind      = "page-break",
        Text      = string.Empty,
        RawOffset = -1,
        RawLength = 0
    };

    /// <summary>
    /// Creates a header block.
    /// <paramref name="pageScope"/>: "all" (default), "odd", "even", "first".
    /// </summary>
    public static DocumentBlock NewHeader(string text = "", string pageScope = "all") => new()
    {
        Kind      = "header",
        Text      = text,
        RawOffset = -1,
        RawLength = 0,
        Attributes = { ["pageScope"] = pageScope }
    };

    /// <summary>
    /// Creates a footer block.
    /// <paramref name="pageScope"/>: "all" (default), "odd", "even", "first".
    /// </summary>
    public static DocumentBlock NewFooter(string text = "", string pageScope = "all") => new()
    {
        Kind      = "footer",
        Text      = text,
        RawOffset = -1,
        RawLength = 0,
        Attributes = { ["pageScope"] = pageScope }
    };

    /// <summary>Creates a new table block with <paramref name="rows"/> rows and <paramref name="columns"/> columns.</summary>
    public static DocumentBlock NewTable(int rows, int columns)
    {
        rows    = Math.Max(1, rows);
        columns = Math.Max(1, columns);
        var table = new DocumentBlock { Kind = "table", Text = "[table]", RawOffset = -1, RawLength = 0 };
        for (var r = 0; r < rows; r++)
        {
            var row = new DocumentBlock { Kind = "table-row", Text = string.Empty, RawOffset = -1, RawLength = 0 };
            for (var c = 0; c < columns; c++)
                row.Children.Add(NewTableCell());
            table.Children.Add(row);
        }
        return table;
    }

    /// <summary>
    /// Creates an image block whose pixels are embedded as a raw PNG/JPEG byte array.
    /// <paramref name="pngBytes"/> is the raw encoded image; natural dimensions are optional hints.
    /// </summary>
    public static DocumentBlock NewImage(byte[] pngBytes, double naturalWidth = 0, double naturalHeight = 0)
    {
        var block = new DocumentBlock
        {
            Kind      = "image",
            Text      = "[image]",
            RawOffset = -1,
            RawLength = 0
        };
        block.Attributes["binaryData"] = pngBytes;
        if (naturalWidth  > 0) block.Attributes["naturalWidth"]  = naturalWidth;
        if (naturalHeight > 0) block.Attributes["naturalHeight"] = naturalHeight;
        return block;
    }

    /// <summary>
    /// Clones an existing block's text and attributes into a new block with
    /// <c>RawOffset = -1</c>. Used by SplitBlock to produce the second half.
    /// </summary>
    public static DocumentBlock CloneWithText(DocumentBlock source, string newText)
    {
        var clone = new DocumentBlock
        {
            Kind      = source.Kind,
            Text      = newText,
            RawOffset = -1,
            RawLength = 0
        };
        foreach (var kvp in source.Attributes)
            clone.Attributes[kvp.Key] = kvp.Value;
        return clone;
    }
}
