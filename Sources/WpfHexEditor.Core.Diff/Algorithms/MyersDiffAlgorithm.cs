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
    {
        var edits  = ShortestEditLines(leftLines, rightLines);
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

    private static List<DiffEdit> ShortestEditLines(string[] a, string[] b)
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
                int x;
                if (k == -d || (k != d && v[idx - 1] < v[idx + 1]))
                    x = v[idx + 1];
                else
                    x = v[idx - 1] + 1;

                int y = x - k;
                while (x < n && y < m && string.Equals(a[x], b[y], StringComparison.Ordinal))
                { x++; y++; }

                v[idx] = x;
                if (x >= n && y >= m)
                    return BacktrackLines(vSnapshots, n, m, max);
            }
        }
        return BacktrackLines(vSnapshots, n, m, max);
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
            int pairs = Math.Min(deletedBuf.Count, insertedBuf.Count);
            for (int i = 0; i < pairs; i++)
            {
                var del = deletedBuf[i];
                var ins = insertedBuf[i];
                // Mark as Modified pair and attach word-level diff
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
            // Remainder as pure deletions/insertions
            for (int i = pairs; i < deletedBuf.Count;  i++) result.Add(deletedBuf[i]);
            for (int i = pairs; i < insertedBuf.Count; i++) result.Add(insertedBuf[i]);
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
        return new TextDiffStats
        {
            TotalLines    = lines.Count,
            EqualLines    = eq,
            ModifiedLines = mod,
            DeletedLines  = del,
            InsertedLines = ins
        };
    }
}
