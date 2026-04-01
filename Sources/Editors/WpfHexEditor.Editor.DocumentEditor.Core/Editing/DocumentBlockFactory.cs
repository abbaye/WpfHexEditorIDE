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
