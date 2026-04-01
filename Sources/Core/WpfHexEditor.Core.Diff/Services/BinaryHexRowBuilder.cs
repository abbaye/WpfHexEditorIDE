// Project      : WpfHexEditorControl
// File         : Services/BinaryHexRowBuilder.cs
// Description  : Converts a BinaryDiffResult into a display-ready list of
//                BinaryHexDiffRow objects for the hex dump diff view.
// Architecture : Pure service — no WPF, no I/O.  Stateless; safe to share.
//                Streaming O(n) algorithm: walks regions once, maintaining
//                running lPos/rPos counters — no intermediate slots list,
//                no per-row offset rescans.

namespace WpfHexEditor.Core.Diff.Services;

using WpfHexEditor.Core.Diff.Models;

/// <summary>
/// Transforms a <see cref="BinaryDiffResult"/> (list of diff regions) into a
/// flat, ordered sequence of <see cref="BinaryHexDiffRow"/> objects suitable for
/// direct binding in a virtualised WPF <c>ItemsControl</c>.
/// <para>
/// Each row covers exactly 16 byte positions.  Equal bytes between diff regions
/// are reconstructed from the full file buffers and subjected to context-folding
/// so that uninteresting runs are collapsed into a single placeholder row.
/// </para>
/// </summary>
public static class BinaryHexRowBuilder
{
    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>
    /// Build the display rows for a binary diff.
    /// </summary>
    /// <param name="bin">The diff result produced by <c>BinaryDiffAlgorithm</c>.</param>
    /// <param name="contextLines">
    /// Number of equal rows to keep on each side of a diff row.
    /// Pass <see cref="int.MaxValue"/> to show every row (no folding).
    /// </param>
    /// <returns>An ordered, immutable list of display rows.</returns>
    public static IReadOnlyList<BinaryHexDiffRow> BuildRows(
        BinaryDiffResult bin,
        int contextLines = 3)
    {
        ArgumentNullException.ThrowIfNull(bin);

        var leftFull  = bin.FullLeftBytes;
        var rightFull = bin.FullRightBytes;

        var rawRows = leftFull is not null && rightFull is not null
            ? BuildFromFullBuffers(bin, leftFull, rightFull)
            : BuildFromRegionsOnly(bin);

        return contextLines == int.MaxValue
            ? rawRows
            : ApplyContextFolding(rawRows, contextLines);
    }

    // ── Full-buffer streaming path (preferred) ──────────────────────────────
    //
    // Single O(n) pass: walks regions + equal gaps simultaneously while
    // maintaining running lPos/rPos counters.  No intermediate slots list —
    // rows are emitted directly into the result list.

    private static List<BinaryHexDiffRow> BuildFromFullBuffers(
        BinaryDiffResult bin,
        byte[] leftFull,
        byte[] rightFull)
    {
        const int bpr = BinaryHexDiffRow.BytesPerRow;

        var rows = new List<BinaryHexDiffRow>(
            (int)((Math.Max(leftFull.Length, rightFull.Length) + bpr - 1) / bpr));

        // Scratch cell arrays — copied into new arrays only when a row is emitted.
        var leftCells  = new BinaryHexByteCell[bpr];
        var rightCells = new BinaryHexByteCell[bpr];

        int  cellIdx     = 0;
        long rowLeftOff  = 0;   // left file offset at start of current row
        long rowRightOff = 0;   // right file offset at start of current row
        long lPos        = 0;   // running left file byte index
        long rPos        = 0;   // running right file byte index
        bool rowIsCtx    = true;

        // Flush the current partial/full row into `rows` and reset state.
        void FlushRow()
        {
            for (int p = cellIdx; p < bpr; p++)
            {
                leftCells[p]  = BinaryHexByteCell.Padding();
                rightCells[p] = BinaryHexByteCell.Padding();
            }

            var lc = new BinaryHexByteCell[bpr];
            var rc = new BinaryHexByteCell[bpr];
            leftCells.CopyTo(lc, 0);
            rightCells.CopyTo(rc, 0);

            rows.Add(new BinaryHexDiffRow
            {
                LeftOffset  = rowLeftOff,
                RightOffset = rowRightOff,
                LeftCells   = lc,
                RightCells  = rc,
                IsContext   = rowIsCtx,
            });

            rowLeftOff  = lPos;
            rowRightOff = rPos;
            cellIdx     = 0;
            rowIsCtx    = true;
        }

        // Add one byte-pair to the current row; flush when the row is full.
        void EmitCell(byte lb, byte rb, BinaryByteKind kind)
        {
            if (cellIdx == 0)
            {
                rowLeftOff  = lPos;
                rowRightOff = rPos;
            }

            leftCells[cellIdx]  = kind == BinaryByteKind.InsertedRight
                ? BinaryHexByteCell.Padding()
                : BinaryHexByteCell.FromByte(lb, kind);

            rightCells[cellIdx] = kind == BinaryByteKind.DeletedLeft
                ? BinaryHexByteCell.Padding()
                : BinaryHexByteCell.FromByte(rb, kind);

            if (kind != BinaryByteKind.Equal) rowIsCtx = false;

            if (++cellIdx == bpr) FlushRow();
        }

        // Walk regions in order.
        long leftCursor  = 0;
        long rightCursor = 0;

        foreach (var region in bin.Regions)
        {
            if (region.Kind == BinaryRegionKind.Identical)
            {
                leftCursor  += region.Length;
                rightCursor += region.Length;
                continue;
            }

            // Emit equal bytes filling the gap before this region.
            long gapLen = region.LeftOffset - leftCursor;
            for (long g = 0; g < gapLen; g++)
            {
                long li = leftCursor  + g;
                long ri = rightCursor + g;
                if (li < leftFull.Length && ri < rightFull.Length)
                {
                    EmitCell(leftFull[li], rightFull[ri], BinaryByteKind.Equal);
                    lPos++;
                    rPos++;
                }
            }
            leftCursor  = region.LeftOffset;
            rightCursor = region.RightOffset;

            switch (region.Kind)
            {
                case BinaryRegionKind.Modified:
                    for (int k = 0; k < region.Length; k++)
                    {
                        EmitCell(region.LeftBytes[k], region.RightBytes[k], BinaryByteKind.Modified);
                        lPos++;
                        rPos++;
                    }
                    leftCursor  += region.Length;
                    rightCursor += region.Length;
                    break;

                case BinaryRegionKind.DeletedInRight:
                    for (int k = 0; k < region.Length; k++)
                    {
                        EmitCell(region.LeftBytes[k], 0x00, BinaryByteKind.DeletedLeft);
                        lPos++;
                    }
                    leftCursor += region.Length;
                    break;

                case BinaryRegionKind.InsertedInRight:
                    for (int k = 0; k < region.Length; k++)
                    {
                        EmitCell(0x00, region.RightBytes[k], BinaryByteKind.InsertedRight);
                        rPos++;
                    }
                    rightCursor += region.Length;
                    break;
            }
        }

        // Emit tail equal bytes.
        long tailLen = Math.Min(leftFull.Length - leftCursor, rightFull.Length - rightCursor);
        for (long i = 0; i < tailLen; i++)
        {
            EmitCell(leftFull[leftCursor + i], rightFull[rightCursor + i], BinaryByteKind.Equal);
            lPos++;
            rPos++;
        }

        // Left-only tail (file length mismatch — left file is longer).
        for (long i = leftCursor + tailLen; i < leftFull.Length; i++)
        {
            EmitCell(leftFull[i], 0x00, BinaryByteKind.DeletedLeft);
            lPos++;
        }

        // Right-only tail (right file is longer).
        for (long i = rightCursor + tailLen; i < rightFull.Length; i++)
        {
            EmitCell(0x00, rightFull[i], BinaryByteKind.InsertedRight);
            rPos++;
        }

        if (cellIdx > 0) FlushRow();

        return rows;
    }

    // ── Regions-only streaming fallback (no full buffers retained) ──────────

    private static List<BinaryHexDiffRow> BuildFromRegionsOnly(BinaryDiffResult bin)
    {
        const int bpr = BinaryHexDiffRow.BytesPerRow;

        var rows       = new List<BinaryHexDiffRow>();
        var leftCells  = new BinaryHexByteCell[bpr];
        var rightCells = new BinaryHexByteCell[bpr];

        int  cellIdx     = 0;
        long rowLeftOff  = 0;
        long rowRightOff = 0;
        long lPos        = 0;
        long rPos        = 0;
        bool rowIsCtx    = true;

        void FlushRow()
        {
            for (int p = cellIdx; p < bpr; p++)
            {
                leftCells[p]  = BinaryHexByteCell.Padding();
                rightCells[p] = BinaryHexByteCell.Padding();
            }

            var lc = new BinaryHexByteCell[bpr];
            var rc = new BinaryHexByteCell[bpr];
            leftCells.CopyTo(lc, 0);
            rightCells.CopyTo(rc, 0);

            rows.Add(new BinaryHexDiffRow
            {
                LeftOffset  = rowLeftOff,
                RightOffset = rowRightOff,
                LeftCells   = lc,
                RightCells  = rc,
                IsContext   = rowIsCtx,
            });

            rowLeftOff  = lPos;
            rowRightOff = rPos;
            cellIdx     = 0;
            rowIsCtx    = true;
        }

        void EmitCell(byte lb, byte rb, BinaryByteKind kind)
        {
            if (cellIdx == 0) { rowLeftOff = lPos; rowRightOff = rPos; }

            leftCells[cellIdx]  = kind == BinaryByteKind.InsertedRight
                ? BinaryHexByteCell.Padding()
                : BinaryHexByteCell.FromByte(lb, kind);

            rightCells[cellIdx] = kind == BinaryByteKind.DeletedLeft
                ? BinaryHexByteCell.Padding()
                : BinaryHexByteCell.FromByte(rb, kind);

            if (kind != BinaryByteKind.Equal) rowIsCtx = false;

            if (++cellIdx == bpr) FlushRow();
        }

        foreach (var region in bin.Regions)
        {
            if (region.Kind == BinaryRegionKind.Identical) continue;

            switch (region.Kind)
            {
                case BinaryRegionKind.Modified:
                    for (int k = 0; k < region.Length; k++)
                    {
                        EmitCell(region.LeftBytes[k], region.RightBytes[k], BinaryByteKind.Modified);
                        lPos++; rPos++;
                    }
                    break;

                case BinaryRegionKind.DeletedInRight:
                    for (int k = 0; k < region.Length; k++)
                    {
                        EmitCell(region.LeftBytes[k], 0x00, BinaryByteKind.DeletedLeft);
                        lPos++;
                    }
                    break;

                case BinaryRegionKind.InsertedInRight:
                    for (int k = 0; k < region.Length; k++)
                    {
                        EmitCell(0x00, region.RightBytes[k], BinaryByteKind.InsertedRight);
                        rPos++;
                    }
                    break;
            }
        }

        if (cellIdx > 0) FlushRow();

        return rows;
    }

    // ── Context folding ─────────────────────────────────────────────────────

    private static IReadOnlyList<BinaryHexDiffRow> ApplyContextFolding(
        List<BinaryHexDiffRow> rawRows,
        int contextLines)
    {
        if (rawRows.Count == 0) return rawRows;

        // Build keep-set: row indices that must remain visible
        var keepSet = new HashSet<int>();
        for (int i = 0; i < rawRows.Count; i++)
        {
            if (!rawRows[i].HasDiff) continue;

            int lo = Math.Max(0, i - contextLines);
            int hi = Math.Min(rawRows.Count - 1, i + contextLines);
            for (int j = lo; j <= hi; j++)
                keepSet.Add(j);
        }

        // If nothing differs, collapse everything
        if (keepSet.Count == 0)
        {
            return [new BinaryHexDiffRow
            {
                IsCollapsedContext = true,
                CollapsedRowCount  = rawRows.Count,
            }];
        }

        var result = new List<BinaryHexDiffRow>(rawRows.Count);
        int idx = 0;

        while (idx < rawRows.Count)
        {
            if (keepSet.Contains(idx))
            {
                result.Add(rawRows[idx++]);
            }
            else
            {
                int start = idx;
                while (idx < rawRows.Count && !keepSet.Contains(idx))
                    idx++;

                result.Add(new BinaryHexDiffRow
                {
                    IsCollapsedContext = true,
                    CollapsedRowCount  = idx - start,
                });
            }
        }

        return result;
    }
}
