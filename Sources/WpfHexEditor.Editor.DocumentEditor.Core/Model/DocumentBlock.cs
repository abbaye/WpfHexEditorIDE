// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor.Core
// File: Model/DocumentBlock.cs
// Description:
//     Logical unit of a parsed document (paragraph, run, table, image …).
//     Carries a RawOffset + RawLength so the BinaryMap can bridge it to
//     the hex view.
// ==========================================================

namespace WpfHexEditor.Editor.DocumentEditor.Core.Model;

/// <summary>
/// A logical unit of a parsed document.
/// </summary>
public sealed class DocumentBlock
{
    // ──────────────────────────────── Identity ────────────────────────────────

    /// <summary>
    /// Semantic kind:
    /// <c>"paragraph"</c>, <c>"run"</c>, <c>"table"</c>, <c>"table-row"</c>,
    /// <c>"table-cell"</c>, <c>"image"</c>, <c>"header"</c>, <c>"footer"</c>,
    /// <c>"section"</c>, <c>"list-item"</c>, <c>"hyperlink"</c>.
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>Plain-text content of this block.</summary>
    public string Text { get; set; } = string.Empty;

    // ──────────────────────────────── Style ───────────────────────────────────

    /// <summary>
    /// Loader-specific style attributes
    /// (e.g. <c>["bold"]=true</c>, <c>["fontSize"]="12pt"</c>).
    /// </summary>
    public Dictionary<string, object> Attributes { get; } = [];

    // ──────────────────────────────── Hierarchy ───────────────────────────────

    /// <summary>Child blocks (runs inside a paragraph, cells in a row …).</summary>
    public List<DocumentBlock> Children { get; } = [];

    // ──────────────────────────────── Binary mapping ──────────────────────────

    /// <summary>
    /// Byte offset of this block in the <em>source file</em>.
    /// <para>
    /// For ZIP-based formats (DOCX/ODT) this is the offset of the XML element
    /// within the relevant ZIP entry (relative to that entry's data start).
    /// The <see cref="BinaryMap.BinaryMapEntry"/> stores the absolute ZIP offset
    /// once the entry header offset is known.
    /// </para>
    /// </summary>
    public long RawOffset { get; init; }

    /// <summary>Length in bytes of this block's representation in the source file.</summary>
    public int RawLength { get; init; }

    // ──────────────────────────────── Helpers ─────────────────────────────────

    /// <summary>
    /// Returns a depth-first enumeration of this block and all descendants.
    /// </summary>
    public IEnumerable<DocumentBlock> DescendantsAndSelf()
    {
        yield return this;
        foreach (var child in Children)
            foreach (var desc in child.DescendantsAndSelf())
                yield return desc;
    }

    public override string ToString() => $"[{Kind}] \"{Text.AsSpan(0, Math.Min(Text.Length, 40))}\"";
}
