// Project      : WpfHexEditorControl
// File         : Algorithms/BinaryDiffAlgorithm.cs
// Description  : Byte-level comparison via contiguous region scanning.
//                Preserves the semantics of the original FileDiffService.
// Architecture : Implements IDiffAlgorithm; stateless; O(n) time, O(k) space (k = diff count).

using WpfHexEditor.Core.Diff.Models;

namespace WpfHexEditor.Core.Diff.Algorithms;

/// <summary>
/// Byte-offset scan that groups contiguous differing bytes into regions.
/// Fast and low-memory; cannot detect insertions/deletions (only Modified,
/// InsertedInRight, DeletedInRight at the length boundary).
/// Use <see cref="MyersDiffAlgorithm"/> for text files where insertion/deletion detection matters.
/// </summary>
public sealed class BinaryDiffAlgorithm : IDiffAlgorithm
{
    private const int ChunkSize = 4096;

    // -----------------------------------------------------------------------
    // IDiffAlgorithm — text path delegates to Myers
    // -----------------------------------------------------------------------

    public TextDiffResult ComputeLines(string[] leftLines, string[] rightLines)
        => new MyersDiffAlgorithm().ComputeLines(leftLines, rightLines);

    // -----------------------------------------------------------------------
    // IDiffAlgorithm — binary path
    // -----------------------------------------------------------------------

    public BinaryDiffResult ComputeBytes(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        var regions = new List<BinaryDiffRegion>();
        var commonLen = Math.Min(left.Length, right.Length);

        int offset = 0;
        while (offset < commonLen)
        {
            var chunk = Math.Min(ChunkSize, commonLen - offset);

            int i = 0;
            while (i < chunk)
            {
                if (left[offset + i] != right[offset + i])
                {
                    // Extend contiguous mismatch
                    int start = offset + i;
                    int len   = 0;
                    while (i + len < chunk && left[offset + i + len] != right[offset + i + len])
                        len++;

                    regions.Add(new BinaryDiffRegion
                    {
                        LeftOffset  = start,
                        RightOffset = start,
                        Length      = len,
                        Kind        = BinaryRegionKind.Modified,
                        LeftBytes   = left.Slice(start, len).ToArray(),
                        RightBytes  = right.Slice(start, len).ToArray()
                    });

                    i += len;
                }
                else
                {
                    i++;
                }
            }

            offset += chunk;
        }

        // Length tail
        if (left.Length > right.Length)
        {
            var extraStart = right.Length;
            var extraLen   = left.Length - right.Length;
            regions.Add(new BinaryDiffRegion
            {
                LeftOffset  = extraStart,
                RightOffset = extraStart,
                Length      = extraLen,
                Kind        = BinaryRegionKind.DeletedInRight,
                LeftBytes   = left.Slice(extraStart, extraLen).ToArray(),
                RightBytes  = []
            });
        }
        else if (right.Length > left.Length)
        {
            var extraStart = left.Length;
            var extraLen   = right.Length - left.Length;
            regions.Add(new BinaryDiffRegion
            {
                LeftOffset  = extraStart,
                RightOffset = extraStart,
                Length      = extraLen,
                Kind        = BinaryRegionKind.InsertedInRight,
                LeftBytes   = [],
                RightBytes  = right.Slice(extraStart, extraLen).ToArray()
            });
        }

        return new BinaryDiffResult
        {
            Regions = regions,
            Stats   = BuildStats(regions, left.Length, right.Length)
        };
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static BinaryDiffStats BuildStats(List<BinaryDiffRegion> regions, long leftSize, long rightSize)
    {
        int  mod = 0, ins = 0, del = 0;
        long modB = 0, insB = 0, delB = 0;
        foreach (var r in regions)
        {
            switch (r.Kind)
            {
                case BinaryRegionKind.Modified:       mod++; modB += r.Length; break;
                case BinaryRegionKind.InsertedInRight: ins++; insB += r.Length; break;
                case BinaryRegionKind.DeletedInRight:  del++; delB += r.Length; break;
            }
        }
        return new BinaryDiffStats
        {
            TotalRegions   = regions.Count,
            ModifiedCount  = mod, ModifiedBytes  = modB,
            InsertedCount  = ins, InsertedBytes  = insB,
            DeletedCount   = del, DeletedBytes   = delB,
            LeftFileSize   = leftSize,
            RightFileSize  = rightSize
        };
    }
}
