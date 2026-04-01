// Project      : WpfHexEditorControl
// File         : Models/BinaryHexDiffRow.cs
// Description  : One 16-byte display row covering both sides of a binary diff.
// Architecture : Pure model — no WPF, no I/O. Pre-computes display cells at
//                construction time so the XAML template needs no value converters
//                with index parameters (which break WPF virtualization).

namespace WpfHexEditor.Core.Diff.Models;

/// <summary>
/// A flattened, display-ready representation of one byte position for use inside
/// a <see cref="BinaryHexDiffRow"/>.  Created once by <c>BinaryHexRowBuilder</c>
/// and consumed directly by the WPF <c>BinaryHexRowTemplate</c>.
/// </summary>
public sealed class BinaryHexByteCell
{
    /// <summary>Two-character uppercase hex text, e.g. <c>"4D"</c>.  Two spaces for padding slots.</summary>
    public string HexText   { get; init; } = "  ";

    /// <summary>Single printable ASCII character, or <c>"."</c> for non-printable / padding.</summary>
    public string AsciiChar { get; init; } = ".";

    /// <summary><see langword="true"/> when the byte value maps to a printable ASCII character (0x20–0x7E).</summary>
    public bool IsPrintable { get; init; }

    /// <summary>Diff classification driving the per-cell background colour.</summary>
    public BinaryByteKind Kind { get; init; }

    // ── Factory helpers ────────────────────────────────────────────────────

    internal static BinaryHexByteCell FromByte(byte value, BinaryByteKind kind)
    {
        bool printable = value is >= 0x20 and <= 0x7E;
        return new BinaryHexByteCell
        {
            HexText    = value.ToString("X2"),
            AsciiChar  = printable ? ((char)value).ToString() : ".",
            IsPrintable = printable,
            Kind        = kind,
        };
    }

    internal static BinaryHexByteCell Padding() => new()
    {
        HexText    = "  ",
        AsciiChar  = " ",
        IsPrintable = false,
        Kind        = BinaryByteKind.Padding,
    };
}

/// <summary>
/// A single 16-byte-wide hex dump row that contains data for both the left and
/// right sides of a binary diff simultaneously.
/// <para>
/// <b>Layout contract</b>: <see cref="LeftCells"/> and <see cref="RightCells"/>
/// always have exactly <see cref="BytesPerRow"/> elements.  Unused tail slots
/// (last row of a file) and alignment gaps (insertions / deletions) have
/// <see cref="BinaryByteKind.Padding"/> and a blank <see cref="BinaryHexByteCell.HexText"/>.
/// </para>
/// </summary>
public sealed class BinaryHexDiffRow
{
    /// <summary>Number of byte positions per row (must stay 16 — matches the hex editor convention).</summary>
    public const int BytesPerRow = 16;

    /// <summary>
    /// Byte offset in the left file for the first slot of this row, or
    /// <see langword="null"/> when the entire left side is padding (pure-insertion row).
    /// </summary>
    public long? LeftOffset { get; init; }

    /// <summary>
    /// Byte offset in the right file for the first slot of this row, or
    /// <see langword="null"/> when the entire right side is padding (pure-deletion row).
    /// </summary>
    public long? RightOffset { get; init; }

    /// <summary>
    /// Pre-computed display cells for the left side (always <see cref="BytesPerRow"/> elements).
    /// Bound directly to the inner <c>ItemsControl</c> in <c>BinaryHexRowTemplate</c>.
    /// </summary>
    public IReadOnlyList<BinaryHexByteCell> LeftCells  { get; init; } = [];

    /// <summary>
    /// Pre-computed display cells for the right side (always <see cref="BytesPerRow"/> elements).
    /// </summary>
    public IReadOnlyList<BinaryHexByteCell> RightCells { get; init; } = [];

    /// <summary>
    /// <see langword="true"/> when every cell in this row is <see cref="BinaryByteKind.Equal"/>
    /// and the row was retained purely as context around a nearby diff row.
    /// </summary>
    public bool IsContext { get; init; }

    /// <summary>
    /// <see langword="true"/> when this is a synthetic "collapsed N equal rows" placeholder
    /// rather than a real data row.  The XAML template switches to a compact fold banner.
    /// </summary>
    public bool IsCollapsedContext { get; init; }

    /// <summary>Number of suppressed rows represented by this placeholder.</summary>
    public int CollapsedRowCount { get; init; }

    // ── Derived helpers ────────────────────────────────────────────────────

    /// <summary>Formatted left offset string for display (8 uppercase hex digits, or blanks).</summary>
    public string LeftOffsetText  => LeftOffset.HasValue  ? LeftOffset.Value.ToString("X8")  : "        ";

    /// <summary>Formatted right offset string for display.</summary>
    public string RightOffsetText => RightOffset.HasValue ? RightOffset.Value.ToString("X8") : "        ";

    /// <summary>Whether this row contains any byte that differs between the two files.</summary>
    public bool HasDiff => LeftCells.Any(c => c.Kind is not (BinaryByteKind.Equal or BinaryByteKind.Padding))
                        || RightCells.Any(c => c.Kind is not (BinaryByteKind.Equal or BinaryByteKind.Padding));
}
