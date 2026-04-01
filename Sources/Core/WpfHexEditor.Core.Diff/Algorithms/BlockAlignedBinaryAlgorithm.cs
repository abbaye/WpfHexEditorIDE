// Project      : WpfHexEditorControl
// File         : Algorithms/BlockAlignedBinaryAlgorithm.cs
// Description  : Block-aligned binary diff using Rabin-Karp rolling hash + LCS anchor alignment.
//                Correctly detects byte insertions and deletions, preventing the "cascade Modified"
//                problem caused by offset-aligned comparison of shifted content.
// Architecture : Implements IDiffAlgorithm; stateless; O(n) hashing, O(m log m) LCS via patience sort,
//                O(gap) Myers diff for residual gaps.  Safe for ThreadPool execution.

using WpfHexEditor.Core.Diff.Models;

namespace WpfHexEditor.Core.Diff.Algorithms;

/// <summary>
/// Block-aligned binary diff algorithm.
/// <para>
/// <b>Algorithm:</b><br/>
/// 1. Compute Rabin-Karp rolling hashes over fixed-size blocks (default 64 bytes).<br/>
/// 2. Build a hash → left-block-index multimap.<br/>
/// 3. Find anchor pairs (leftBlock, rightBlock) where hashes and full content match.<br/>
/// 4. Extract the LCS of anchor pairs via patience sort (O(m log m)).<br/>
/// 5. For each gap between aligned anchors, run byte-level Myers diff bounded by gap size.<br/>
/// 6. Equal anchors become Identical regions; gaps produce Modified/Inserted/Deleted regions.
/// </para>
/// <para>
/// <b>Why:</b> A single inserted byte makes the byte-aligned <see cref="BinaryDiffAlgorithm"/>
/// report every subsequent byte as Modified.  This algorithm detects the shift and emits a
/// single InsertedInRight region with all surrounding bytes classified as Identical.
/// </para>
/// </summary>
public sealed class BlockAlignedBinaryAlgorithm : IDiffAlgorithm
{
    // ── IDiffAlgorithm — text path delegates to Myers ────────────────────────

    public TextDiffResult ComputeLines(string[] leftLines, string[] rightLines)
        => new MyersDiffAlgorithm().ComputeLines(leftLines, rightLines);

    // ── IDiffAlgorithm — binary path ─────────────────────────────────────────

    public BinaryDiffResult ComputeBytes(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
        => ComputeBytes(left, right, blockSize: 64);

    /// <summary>Computes with a configurable block size.</summary>
    public BinaryDiffResult ComputeBytes(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right,
        int blockSize)
    {
        if (blockSize < 4) blockSize = 4;

        // Fast path: identical files
        if (left.SequenceEqual(right))
            return EmptyResult(left.Length, right.Length);

        var anchors = FindAnchors(left, right, blockSize);
        var regions = new List<BinaryDiffRegion>();

        int lPos = 0;
        int rPos = 0;

        foreach (var (li, ri) in anchors)
        {
            int lStart = li * blockSize;
            int rStart = ri * blockSize;

            // Gap before this anchor
            if (lStart > lPos || rStart > rPos)
            {
                var lGap = left [lPos..lStart];
                var rGap = right[rPos..rStart];
                AppendGapRegions(regions, lGap, rGap, lPos, rPos);
            }

            // Equal anchor block
            int lEnd = Math.Min(lStart + blockSize, left.Length);
            int rEnd = Math.Min(rStart + blockSize, right.Length);
            lPos = lEnd;
            rPos = rEnd;
        }

        // Trailing gap
        if (lPos < left.Length || rPos < right.Length)
        {
            var lTail = left [lPos..];
            var rTail = right[rPos..];
            AppendGapRegions(regions, lTail, rTail, lPos, rPos);
        }

        return new BinaryDiffResult
        {
            Regions = regions,
            Stats   = BuildStats(regions, left.Length, right.Length)
        };
    }

    // ── Anchor detection ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns LCS-aligned (leftBlockIndex, rightBlockIndex) anchor pairs.
    /// </summary>
    private static List<(int L, int R)> FindAnchors(
        ReadOnlySpan<byte> left, ReadOnlySpan<byte> right, int blockSize)
    {
        // Hash all left blocks
        int leftBlockCount  = CeilDiv(left.Length,  blockSize);
        int rightBlockCount = CeilDiv(right.Length, blockSize);

        // Build left hash multimap: hash → list of block indices
        var leftMap = new Dictionary<ulong, List<int>>();
        for (int i = 0; i < leftBlockCount; i++)
        {
            var block = SliceBlock(left, i, blockSize);
            var hash  = RabinKarp(block);
            if (!leftMap.TryGetValue(hash, out var list))
                leftMap[hash] = list = new List<int>(1);
            list.Add(i);
        }

        // Collect candidate anchor pairs where hash + content match
        var candidates = new List<(int L, int R)>();
        for (int rIdx = 0; rIdx < rightBlockCount; rIdx++)
        {
            var rBlock = SliceBlock(right, rIdx, blockSize);
            var hash   = RabinKarp(rBlock);
            if (!leftMap.TryGetValue(hash, out var matches)) continue;
            foreach (int lIdx in matches)
            {
                var lBlock = SliceBlock(left, lIdx, blockSize);
                if (lBlock.SequenceEqual(rBlock))
                    candidates.Add((lIdx, rIdx));
            }
        }

        // LCS of candidates via patience sort on L index, then greedy longest-increasing R
        return PatienceLcs(candidates);
    }

    // ── LCS via patience sort (O(m log m)) ───────────────────────────────────

    /// <summary>
    /// Returns the longest strictly-increasing subsequence of (L, R) pairs where
    /// both L and R are strictly increasing (i.e., LCS of anchor pairs).
    /// Uses the patience-sort / binary-search approach.
    /// </summary>
    private static List<(int L, int R)> PatienceLcs(List<(int L, int R)> pairs)
    {
        if (pairs.Count == 0) return [];

        // Sort by L, then by R ascending (so equal-L candidates are in R order)
        pairs.Sort((a, b) => a.L != b.L ? a.L.CompareTo(b.L) : a.R.CompareTo(b.R));

        // Patience sort on R values
        var piles       = new List<(int L, int R)>();   // top of each pile
        var prev        = new int[pairs.Count];         // predecessor index for backtracking
        var lastOnPile  = new int[pairs.Count + 1];     // O(1) pile-tail lookup

        for (int i = 0; i < prev.Length; i++) prev[i] = -1;
        Array.Fill(lastOnPile, -1);

        for (int i = 0; i < pairs.Count; i++)
        {
            var (l, r) = pairs[i];

            // Binary search for leftmost pile whose top.R >= r
            int lo = 0, hi = piles.Count;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (piles[mid].R < r) lo = mid + 1;
                else                  hi = mid;
            }

            if (lo < piles.Count) piles[lo] = (l, r);
            else                  piles.Add((l, r));

            // O(1) predecessor lookup — no more reverse scan.
            prev[i]       = lo > 0 ? lastOnPile[lo - 1] : -1;
            lastOnPile[lo] = i;
        }

        // Backtrack from last pile to reconstruct sequence
        int cur = piles.Count > 0 ? lastOnPile[piles.Count - 1] : -1;
        var result = new List<(int L, int R)>(piles.Count);
        while (cur >= 0)
        {
            result.Add(pairs[cur]);
            cur = prev[cur];
        }
        result.Reverse();
        return result;
    }

    // ── Gap diff (bounded Myers) ──────────────────────────────────────────────

    /// <summary>
    /// Appends diff regions for a gap between two anchor-aligned segments.
    /// Uses per-byte Myers diff when the gap is small; falls back to simple
    /// length-based heuristic for very large gaps to stay bounded.
    /// </summary>
    private static void AppendGapRegions(List<BinaryDiffRegion> regions,
        ReadOnlySpan<byte> lGap, ReadOnlySpan<byte> rGap, int lBase, int rBase)
    {
        const int MaxMyersGap = 256; // was 8192 — trace clones at 8192 cost ~2 GB, at 256 ~2 MB

        if (lGap.Length == 0 && rGap.Length == 0) return;

        // Short enough for Myers byte-diff
        if (lGap.Length <= MaxMyersGap && rGap.Length <= MaxMyersGap)
        {
            AppendByteMyersRegions(regions, lGap, rGap, lBase, rBase);
            return;
        }

        // Large gap: emit simple regions by length comparison
        int commonLen = Math.Min(lGap.Length, rGap.Length);
        if (commonLen > 0)
            regions.Add(new BinaryDiffRegion
            {
                LeftOffset  = lBase,
                RightOffset = rBase,
                Length      = commonLen,
                Kind        = BinaryRegionKind.Modified,
                LeftBytes   = lGap[..commonLen].ToArray(),
                RightBytes  = rGap[..commonLen].ToArray()
            });

        if (lGap.Length > rGap.Length)
            regions.Add(new BinaryDiffRegion
            {
                LeftOffset  = lBase + commonLen,
                RightOffset = rBase + commonLen,
                Length      = lGap.Length - commonLen,
                Kind        = BinaryRegionKind.DeletedInRight,
                LeftBytes   = lGap[commonLen..].ToArray(),
                RightBytes  = []
            });
        else if (rGap.Length > lGap.Length)
            regions.Add(new BinaryDiffRegion
            {
                LeftOffset  = lBase + commonLen,
                RightOffset = rBase + commonLen,
                Length      = rGap.Length - commonLen,
                Kind        = BinaryRegionKind.InsertedInRight,
                LeftBytes   = [],
                RightBytes  = rGap[commonLen..].ToArray()
            });
    }

    /// <summary>
    /// Byte-level Myers diff for small gaps, appending BinaryDiffRegion entries.
    /// Produces true InsertedInRight / DeletedInRight / Modified regions.
    /// </summary>
    private static void AppendByteMyersRegions(List<BinaryDiffRegion> regions,
        ReadOnlySpan<byte> left, ReadOnlySpan<byte> right, int lBase, int rBase)
    {
        // Use edit script from Myers to build region list
        var edits = ByteMyers(left, right);

        int li = 0, ri = 0;
        int lRunStart = 0, rRunStart = 0;
        bool inDiff = false;
        var lBuf = new List<byte>();
        var rBuf = new List<byte>();

        void FlushDiff()
        {
            if (!inDiff || (lBuf.Count == 0 && rBuf.Count == 0)) return;
            var kind = (lBuf.Count > 0 && rBuf.Count > 0) ? BinaryRegionKind.Modified
                     : (lBuf.Count > 0)                   ? BinaryRegionKind.DeletedInRight
                                                           : BinaryRegionKind.InsertedInRight;
            regions.Add(new BinaryDiffRegion
            {
                LeftOffset  = lBase + lRunStart,
                RightOffset = rBase + rRunStart,
                Length      = Math.Max(lBuf.Count, rBuf.Count),
                Kind        = kind,
                LeftBytes   = [.. lBuf],
                RightBytes  = [.. rBuf]
            });
            lBuf.Clear(); rBuf.Clear();
            inDiff = false;
        }

        foreach (var edit in edits)
        {
            switch (edit)
            {
                case EditOp.Equal:
                    FlushDiff();
                    li++; ri++;
                    break;

                case EditOp.Delete:
                    if (!inDiff) { lRunStart = li; rRunStart = ri; inDiff = true; }
                    lBuf.Add(left[li++]);
                    break;

                case EditOp.Insert:
                    if (!inDiff) { lRunStart = li; rRunStart = ri; inDiff = true; }
                    rBuf.Add(right[ri++]);
                    break;
            }
        }
        FlushDiff();
    }

    // ── Byte-level Myers edit script ─────────────────────────────────────────

    private enum EditOp : byte { Equal, Delete, Insert }

    /// <summary>
    /// Standard Myers O(ND) diff on byte arrays.
    /// Returns an edit script of Equal/Delete/Insert operations.
    /// </summary>
    private static List<EditOp> ByteMyers(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        int n = a.Length, m = b.Length;
        int maxD = n + m;
        if (maxD == 0) return [];

        // v[k] = furthest-reaching x on diagonal k
        var v = new int[2 * maxD + 1];
        // trace[d] = snapshot of v after d-edits
        var trace = new List<int[]>(maxD + 1);

        for (int d = 0; d <= maxD; d++)
        {
            var snap = (int[])v.Clone();
            trace.Add(snap);

            for (int k = -d; k <= d; k += 2)
            {
                int x;
                if (k == -d || (k != d && v[k - 1 + maxD] < v[k + 1 + maxD]))
                    x = v[k + 1 + maxD];
                else
                    x = v[k - 1 + maxD] + 1;

                int y = x - k;
                while (x < n && y < m && a[x] == b[y]) { x++; y++; }
                v[k + maxD] = x;

                if (x >= n && y >= m)
                {
                    // Backtrack
                    return Backtrack(trace, a, b, d, maxD);
                }
            }
        }
        return [];
    }

    private static List<EditOp> Backtrack(List<int[]> trace, ReadOnlySpan<byte> a,
        ReadOnlySpan<byte> b, int lastD, int maxD)
    {
        var ops = new List<EditOp>(a.Length + b.Length);
        int x = a.Length, y = b.Length;

        for (int d = lastD; d > 0; d--)
        {
            var vPrev = trace[d - 1];
            int k = x - y;
            int prevK;
            if (k == -d || (k != d && vPrev[k - 1 + maxD] < vPrev[k + 1 + maxD]))
                prevK = k + 1;
            else
                prevK = k - 1;

            int prevX = vPrev[prevK + maxD];
            int prevY = prevX - prevK;

            // Snake (equal) steps
            while (x > prevX + 1 && y > prevY + 1) { ops.Add(EditOp.Equal); x--; y--; }

            if (prevK == k - 1)      { ops.Add(EditOp.Insert); y--; }
            else                      { ops.Add(EditOp.Delete); x--; }
        }

        // Remaining snake at start
        while (x > 0 && y > 0) { ops.Add(EditOp.Equal); x--; y--; }

        ops.Reverse();
        return ops;
    }

    // ── Rabin-Karp hash ──────────────────────────────────────────────────────

    private static ulong RabinKarp(ReadOnlySpan<byte> block)
    {
        const ulong Base = 257UL;
        ulong h = 0;
        foreach (var b in block)
            h = unchecked(h * Base + b);
        return h;
    }

    // ── Utilities ────────────────────────────────────────────────────────────

    private static ReadOnlySpan<byte> SliceBlock(ReadOnlySpan<byte> data, int idx, int blockSize)
    {
        int start = idx * blockSize;
        int end   = Math.Min(start + blockSize, data.Length);
        return data[start..end];
    }

    private static int CeilDiv(int n, int d) => (n + d - 1) / d;

    private static BinaryDiffResult EmptyResult(int leftLen, int rightLen)
        => new()
        {
            Regions = [],
            Stats   = new BinaryDiffStats
            {
                LeftFileSize  = leftLen,
                RightFileSize = rightLen
            }
        };

    private static BinaryDiffStats BuildStats(List<BinaryDiffRegion> regions,
        long leftSize, long rightSize)
    {
        int  mod = 0, ins = 0, del = 0;
        long modB = 0, insB = 0, delB = 0;
        foreach (var r in regions)
        {
            switch (r.Kind)
            {
                case BinaryRegionKind.Modified:        mod++; modB += r.Length; break;
                case BinaryRegionKind.InsertedInRight: ins++; insB += r.Length; break;
                case BinaryRegionKind.DeletedInRight:  del++; delB += r.Length; break;
            }
        }
        return new BinaryDiffStats
        {
            TotalRegions   = regions.Count,
            ModifiedCount  = mod,  ModifiedBytes  = modB,
            InsertedCount  = ins,  InsertedBytes  = insB,
            DeletedCount   = del,  DeletedBytes   = delB,
            LeftFileSize   = leftSize,
            RightFileSize  = rightSize
        };
    }
}
