// Project      : WpfHexEditorControl
// File         : Algorithms/MyersDiffAlgorithm.cs
// Description  : Myers O(ND) diff algorithm for text lines and byte sequences.
// Architecture : Implements IDiffAlgorithm; stateless; O(ND) time, O(N+D) space.
//                Reference: "An O(ND) Difference Algorithm" — Eugene W. Myers, 1986.

using WpfHexEditor.Core.Diff.Models;

namespace WpfHexEditor.Core.Diff.Algorithms;

/// <summary>
/// Myers O(ND) shortest-edit-script algorithm.
/// Detects true insertions and deletions (not just offsets), making it
/// far more useful for text files than the linear byte-scan approach.
/// </summary>
public sealed class MyersDiffAlgorithm : IDiffAlgorithm
{
    private const int MaxCharLevelLength = 500;

    // -----------------------------------------------------------------------
    // IDiffAlgorithm — binary path (Myers on bytes, bounded to avoid OOM)
    // -----------------------------------------------------------------------

    public BinaryDiffResult ComputeBytes(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        // Myers on raw bytes is expensive for large files; delegate to binary scan for safety.
        // The DiffEngine already enforces the 50 MB cap before calling here.
        var edits  = ShortestEdit(left, right);
        var regions = EditsToRegions(edits, left, right);
        return new BinaryDiffResult
        {
            Regions = regions,
            Stats   = BuildBinaryStats(regions, left.Length, right.Length)
        };
    }

    // -----------------------------------------------------------------------
    // IDiffAlgorithm — text path
    // -----------------------------------------------------------------------

    public TextDiffResult ComputeLines(string[] leftLines, string[] rightLines)
        => ComputeLines(leftLines, rightLines, DiffCompareOptions.Default);

    public TextDiffResult ComputeLines(string[] leftLines, string[] rightLines, DiffCompareOptions options)
    {
        var edits  = ShortestEditLines(leftLines, rightLines, options.IgnoreWhitespace);
        var lines  = BuildTextLines(edits, leftLines, rightLines);
        return new TextDiffResult
        {
            Lines = lines,
            Stats = BuildTextStats(lines)
        };
    }

    // -----------------------------------------------------------------------
    // Core Myers algorithm — generic on int index comparison
    // -----------------------------------------------------------------------

    /// <summary>
    /// Myers shortest-edit-script for two byte sequences.
    /// Returns the list of <see cref="DiffEdit"/> in left-to-right order.
    /// </summary>
    private static List<DiffEdit> ShortestEdit(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        int n = a.Length, m = b.Length;
        int max = n + m;
        if (max == 0) return [];

        // V[k] = furthest x reached on diagonal k
        // We store snapshots of V at each d-step for backtracking.
        var vSnapshots = new List<int[]>();
        var v = new int[2 * max + 1];

        for (int d = 0; d <= max; d++)
        {
            var snap = (int[])v.Clone();
            vSnapshots.Add(snap);

            for (int k = -d; k <= d; k += 2)
            {
                int idx = k + max;
                int x;
                if (k == -d || (k != d && v[idx - 1] < v[idx + 1]))
                    x = v[idx + 1];       // move down (insert from b)
                else
                    x = v[idx - 1] + 1;  // move right (delete from a)

                int y = x - k;
                while (x < n && y < m && a[x] == b[y]) { x++; y++; }

                v[idx] = x;
                if (x >= n && y >= m)
                    return Backtrack(vSnapshots, a, b, max);
            }
        }

        return Backtrack(vSnapshots, a, b, max);
    }

    private static List<DiffEdit> Backtrack(List<int[]> vSnapshots,
        ReadOnlySpan<byte> a, ReadOnlySpan<byte> b, int offset)
    {
        var edits = new List<DiffEdit>();
        int x = a.Length, y = b.Length;

        for (int d = vSnapshots.Count - 1; d > 0; d--)
        {
            var vPrev = vSnapshots[d - 1];
            int k = x - y;
            int idx = k + offset;

            int prevK;
            if (k == -d || (k != d && vPrev[idx - 1] < vPrev[idx + 1]))
                prevK = k + 1;
            else
                prevK = k - 1;

            int prevX = vPrev[prevK + offset];
            int prevY = prevX - prevK;

            // Equal snake
            while (x > prevX && y > prevY)
            {
                edits.Add(new DiffEdit(EditKind.Equal, x - 1, x, y - 1, y));
                x--; y--;
            }

            if (d > 0)
            {
                if (x == prevX)
                    edits.Add(new DiffEdit(EditKind.Insert, prevX, prevX, prevY, y));
                else
                    edits.Add(new DiffEdit(EditKind.Delete, prevX, x, prevY, prevY));
            }

            x = prevX;
            y = prevY;
        }

        edits.Reverse();
        return edits;
    }

    // -----------------------------------------------------------------------
    // Line-based variant (uses string equality, not byte equality)
    // -----------------------------------------------------------------------

    private static List<DiffEdit> ShortestEditLines(string[] a, string[] b, bool ignoreWhitespace = false)
    {
        int n = a.Length, m = b.Length;
        int max = n + m;
        if (max == 0) return [];

        // When ignoring whitespace, compare trimmed versions for equality but
        // preserve original content in the output. Build normalized arrays once.
        string[] aNorm = a, bNorm = b;
        if (ignoreWhitespace)
        {
            aNorm = new string[n];
            bNorm = new string[m];
            for (int i = 0; i < n; i++) aNorm[i] = NormalizeWhitespace(a[i]);
            for (int i = 0; i < m; i++) bNorm[i] = NormalizeWhitespace(b[i]);
        }

        // Pre-hash all lines for O(1) inequality fast-path — avoids O(line-length) string
        // equality on every Myers diagonal step (OPT-PERF-02). Full Ordinal compare is still
        // done on hash matches to handle collisions correctly.
        var aHash = new int[n];
        var bHash = new int[m];
        for (int i = 0; i < n; i++) aHash[i] = aNorm[i].GetHashCode();
        for (int i = 0; i < m; i++) bHash[i] = bNorm[i].GetHashCode();

        var vSnapshots = new List<int[]>();
        var v = new int[2 * max + 1];

        for (int d = 0; d <= max; d++)
        {
            vSnapshots.Add((int[])v.Clone());

            for (int k = -d; k <= d; k += 2)
            {
                int idx = k + max;
                int x;
                if (k == -d || (k != d && v[idx - 1] < v[idx + 1]))
                    x = v[idx + 1];
                else
                    x = v[idx - 1] + 1;

                int y = x - k;
                while (x < n && y < m
                    && aHash[x] == bHash[y]
                    && string.Equals(aNorm[x], bNorm[y], StringComparison.Ordinal))
                { x++; y++; }

                v[idx] = x;
                if (x >= n && y >= m)
                    return BacktrackLines(vSnapshots, n, m, max);
            }
        }
        return BacktrackLines(vSnapshots, n, m, max);
    }

    /// <summary>
    /// Normalizes a line for whitespace-insensitive comparison:
    /// trims leading/trailing whitespace and collapses internal runs to single spaces.
    /// </summary>
    private static string NormalizeWhitespace(string line)
    {
        var trimmed = line.AsSpan().Trim();
        if (trimmed.IsEmpty) return string.Empty;

        // Fast path: no internal whitespace runs
        bool hasRun = false;
        for (int i = 1; i < trimmed.Length; i++)
        {
            if (char.IsWhiteSpace(trimmed[i]) && char.IsWhiteSpace(trimmed[i - 1]))
            { hasRun = true; break; }
        }
        if (!hasRun) return trimmed.ToString();

        // Collapse internal whitespace runs to single space
        var sb = new System.Text.StringBuilder(trimmed.Length);
        bool prevWs = false;
        foreach (char ch in trimmed)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!prevWs) sb.Append(' ');
                prevWs = true;
            }
            else
            {
                sb.Append(ch);
                prevWs = false;
            }
        }
        return sb.ToString();
    }

    private static List<DiffEdit> BacktrackLines(List<int[]> vSnapshots, int n, int m, int offset)
    {
        var edits = new List<DiffEdit>();
        int x = n, y = m;

        for (int d = vSnapshots.Count - 1; d > 0; d--)
        {
            var vPrev = vSnapshots[d - 1];
            int k = x - y;
            int idx = k + offset;

            int prevK;
            if (k == -d || (k != d && vPrev[idx - 1] < vPrev[idx + 1]))
                prevK = k + 1;
            else
                prevK = k - 1;

            int prevX = vPrev[prevK + offset];
            int prevY = prevX - prevK;

            while (x > prevX && y > prevY)
            {
                edits.Add(new DiffEdit(EditKind.Equal, x - 1, x, y - 1, y));
                x--; y--;
            }

            if (d > 0)
            {
                if (x == prevX)
                    edits.Add(new DiffEdit(EditKind.Insert, prevX, prevX, prevY, y));
                else
                    edits.Add(new DiffEdit(EditKind.Delete, prevX, x, prevY, prevY));
            }

            x = prevX;
            y = prevY;
        }

        edits.Reverse();
        return edits;
    }

    // -----------------------------------------------------------------------
    // Convert edits → TextDiffLine list
    // -----------------------------------------------------------------------

    private static List<TextDiffLine> BuildTextLines(List<DiffEdit> edits,
        string[] leftLines, string[] rightLines)
    {
        var result      = new List<TextDiffLine>();
        var deletedBuf  = new List<TextDiffLine>();
        var insertedBuf = new List<TextDiffLine>();

        void FlushPaired()
        {
            int delCount = deletedBuf.Count;
            int insCount = insertedBuf.Count;

            if (delCount == 0 && insCount == 0) return;

            // Build similarity-based pairing when buffers are small enough.
            // Greedy: pick the best-similarity pair, emit it, repeat.
            // Unmatched lines (below threshold) stay as pure Delete/Insert.
            const int MaxSmartPairSize = 50;
            const double MinSimilarity = 0.35;

            if (delCount > 0 && insCount > 0 && delCount <= MaxSmartPairSize && insCount <= MaxSmartPairSize)
            {
                var usedDel = new bool[delCount];
                var usedIns = new bool[insCount];
                var pairs = new List<(int di, int ii, double sim)>();

                // Compute pairwise similarity scores
                for (int di = 0; di < delCount; di++)
                    for (int ii = 0; ii < insCount; ii++)
                    {
                        double sim = LineSimilarity(deletedBuf[di].Content, insertedBuf[ii].Content);
                        if (sim >= MinSimilarity)
                            pairs.Add((di, ii, sim));
                    }

                // Greedy: pick best similarity first, preserving source order
                pairs.Sort((a, b) => b.sim.CompareTo(a.sim));

                var matched = new List<(int di, int ii)>();
                foreach (var (di, ii, sim) in pairs)
                {
                    if (usedDel[di] || usedIns[ii]) continue;
                    usedDel[di] = true;
                    usedIns[ii] = true;
                    matched.Add((di, ii));
                }

                // Emit in source order (by delete index)
                matched.Sort((a, b) => a.di.CompareTo(b.di));

                int nextDel = 0, nextIns = 0;
                foreach (var (di, ii) in matched)
                {
                    // Emit unmatched deletes before this pair
                    for (; nextDel < di; nextDel++)
                        if (!usedDel[nextDel]) result.Add(deletedBuf[nextDel]);
                    // Emit unmatched inserts before this pair
                    for (; nextIns < ii; nextIns++)
                        if (!usedIns[nextIns]) result.Add(insertedBuf[nextIns]);

                    EmitModifiedPair(deletedBuf[di], insertedBuf[ii], result);
                    nextDel = di + 1;
                    nextIns = ii + 1;
                }

                // Emit remaining unmatched
                for (; nextDel < delCount; nextDel++)
                    if (!usedDel[nextDel]) result.Add(deletedBuf[nextDel]);
                for (; nextIns < insCount; nextIns++)
                    if (!usedIns[nextIns]) result.Add(insertedBuf[nextIns]);
            }
            else
            {
                // Large buffers: positional pairing (perf guard)
                int positionalPairs = Math.Min(delCount, insCount);
                for (int i = 0; i < positionalPairs; i++)
                    EmitModifiedPair(deletedBuf[i], insertedBuf[i], result);
                for (int i = positionalPairs; i < delCount;  i++) result.Add(deletedBuf[i]);
                for (int i = positionalPairs; i < insCount; i++) result.Add(insertedBuf[i]);
            }

            deletedBuf.Clear();
            insertedBuf.Clear();
        }

        int leftLine = 1, rightLine = 1;
        foreach (var edit in edits)
        {
            switch (edit.Kind)
            {
                case EditKind.Equal:
                    FlushPaired();
                    for (int i = edit.LeftStart; i < edit.LeftEnd; i++)
                    {
                        result.Add(new TextDiffLine
                        {
                            Kind            = TextLineKind.Equal,
                            Content         = leftLines[i],
                            LeftLineNumber  = leftLine++,
                            RightLineNumber = rightLine++
                        });
                    }
                    break;

                case EditKind.Delete:
                    for (int i = edit.LeftStart; i < edit.LeftEnd; i++)
                        deletedBuf.Add(new TextDiffLine
                        {
                            Kind           = TextLineKind.DeletedLeft,
                            Content        = leftLines[i],
                            LeftLineNumber = leftLine++
                        });
                    break;

                case EditKind.Insert:
                    for (int i = edit.RightStart; i < edit.RightEnd; i++)
                        insertedBuf.Add(new TextDiffLine
                        {
                            Kind            = TextLineKind.InsertedRight,
                            Content         = rightLines[i],
                            RightLineNumber = rightLine++
                        });
                    break;
            }
        }

        FlushPaired();
        return result;
    }

    // -----------------------------------------------------------------------
    // Pairing helpers
    // -----------------------------------------------------------------------

    private static void EmitModifiedPair(TextDiffLine del, TextDiffLine ins, List<TextDiffLine> result)
    {
        var wordEdits = del.Content.Length <= MaxCharLevelLength && ins.Content.Length <= MaxCharLevelLength
            ? ComputeWordEdits(del.Content, ins.Content)
            : (IReadOnlyList<DiffEdit>)[];
        var modified = new TextDiffLine
        {
            Kind            = TextLineKind.Modified,
            Content         = del.Content,
            LeftLineNumber  = del.LeftLineNumber,
            RightLineNumber = del.RightLineNumber,
            WordEdits       = wordEdits
        };
        var modifiedIns = new TextDiffLine
        {
            Kind            = TextLineKind.Modified,
            Content         = ins.Content,
            LeftLineNumber  = ins.LeftLineNumber,
            RightLineNumber = ins.RightLineNumber,
            WordEdits       = wordEdits
        };
        modified.CounterpartLine    = modifiedIns;
        modifiedIns.CounterpartLine = modified;
        result.Add(modified);
        result.Add(modifiedIns);
    }

    /// <summary>
    /// Quick similarity ratio between two lines: 2 × (common chars) / (total chars).
    /// Uses a frequency-based approximation for speed (O(n+m), no allocation beyond stackalloc).
    /// </summary>
    private static double LineSimilarity(string a, string b)
    {
        if (a.Length == 0 && b.Length == 0) return 1.0;
        if (a.Length == 0 || b.Length == 0) return 0.0;

        // Character frequency counts (ASCII fast path)
        Span<int> freqA = stackalloc int[128];
        Span<int> freqB = stackalloc int[128];
        freqA.Clear();
        freqB.Clear();

        int nonAsciiA = 0, nonAsciiB = 0;
        foreach (char c in a)
        {
            if (c < 128) freqA[c]++;
            else nonAsciiA++;
        }
        foreach (char c in b)
        {
            if (c < 128) freqB[c]++;
            else nonAsciiB++;
        }

        int common = 0;
        for (int i = 0; i < 128; i++)
            common += Math.Min(freqA[i], freqB[i]);
        common += Math.Min(nonAsciiA, nonAsciiB);

        return 2.0 * common / (a.Length + b.Length);
    }

    // -----------------------------------------------------------------------
    // Character-level diff (bounded)
    // -----------------------------------------------------------------------

    internal static IReadOnlyList<DiffEdit> ComputeWordEdits(string left, string right)
    {
        if (left.Length == 0 && right.Length == 0) return [];
        var aBytes = System.Text.Encoding.Unicode.GetBytes(left);
        var bBytes = System.Text.Encoding.Unicode.GetBytes(right);
        // Work in char pairs (UTF-16 code units)
        var aChars = left.ToCharArray();
        var bChars = right.ToCharArray();
        return ShortestEditChars(aChars, bChars);
    }

    private static List<DiffEdit> ShortestEditChars(char[] a, char[] b)
    {
        int n = a.Length, m = b.Length;
        int max = n + m;
        if (max == 0) return [];

        var vSnapshots = new List<int[]>();
        var v = new int[2 * max + 1];

        for (int d = 0; d <= max; d++)
        {
            vSnapshots.Add((int[])v.Clone());
            for (int k = -d; k <= d; k += 2)
            {
                int idx = k + max;
                int x = (k == -d || (k != d && v[idx - 1] < v[idx + 1])) ? v[idx + 1] : v[idx - 1] + 1;
                int y = x - k;
                while (x < n && y < m && a[x] == b[y]) { x++; y++; }
                v[idx] = x;
                if (x >= n && y >= m) return BacktrackLines(vSnapshots, n, m, max);
            }
        }
        return BacktrackLines(vSnapshots, n, m, max);
    }

    // -----------------------------------------------------------------------
    // Binary edits → regions
    // -----------------------------------------------------------------------

    private static List<BinaryDiffRegion> EditsToRegions(List<DiffEdit> edits,
        ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        var regions = new List<BinaryDiffRegion>();
        foreach (var edit in edits)
        {
            if (edit.Kind == EditKind.Equal) continue;

            if (edit.Kind == EditKind.Delete)
                regions.Add(new BinaryDiffRegion
                {
                    LeftOffset = edit.LeftStart, RightOffset = edit.RightStart,
                    Length = edit.LeftLength, Kind = BinaryRegionKind.DeletedInRight,
                    LeftBytes = left.Slice(edit.LeftStart, edit.LeftLength).ToArray(), RightBytes = []
                });
            else
                regions.Add(new BinaryDiffRegion
                {
                    LeftOffset = edit.LeftStart, RightOffset = edit.RightStart,
                    Length = edit.RightLength, Kind = BinaryRegionKind.InsertedInRight,
                    LeftBytes = [], RightBytes = right.Slice(edit.RightStart, edit.RightLength).ToArray()
                });
        }
        return regions;
    }

    // -----------------------------------------------------------------------
    // Stats helpers
    // -----------------------------------------------------------------------

    private static BinaryDiffStats BuildBinaryStats(List<BinaryDiffRegion> regions, int leftSize, int rightSize)
    {
        int mod = 0, ins = 0, del = 0; long modB = 0, insB = 0, delB = 0;
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
            TotalRegions = regions.Count,
            ModifiedCount = mod, ModifiedBytes = modB,
            InsertedCount = ins, InsertedBytes = insB,
            DeletedCount  = del, DeletedBytes  = delB,
            LeftFileSize  = leftSize, RightFileSize = rightSize
        };
    }

    private static TextDiffStats BuildTextStats(List<TextDiffLine> lines)
    {
        int eq = 0, mod = 0, del = 0, ins = 0;
        foreach (var l in lines)
        {
            switch (l.Kind)
            {
                case TextLineKind.Equal:        eq++;  break;
                case TextLineKind.Modified:     mod++; break;
                case TextLineKind.DeletedLeft:  del++; break;
                case TextLineKind.InsertedRight: ins++; break;
            }
        }
        int modPairs = mod / 2; // FlushPaired always emits 2 TextDiffLine rows per conceptual modified pair
        return new TextDiffStats
        {
            TotalLines    = eq + modPairs + del + ins,
            EqualLines    = eq,
            ModifiedLines = modPairs,
            DeletedLines  = del,
            InsertedLines = ins
        };
    }
}
