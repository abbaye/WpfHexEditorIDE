// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Services/DocumentDiffService.cs
// Description:
//     Block-level structural diff between two DocumentModels.
//     Flattens each model's block tree to a (Kind, Text) signature
//     stream and runs an LCS pass to label each row as Equal /
//     Added / Removed / Modified.
// Architecture notes:
//     LCS uses a byte-direction matrix (1 byte/cell instead of 4)
//     so worst-case memory is m·n bytes — 100 MB for two 10k-row
//     docs rather than 400 MB with an int matrix. The LCS-length
//     row is held as a rolling int[n+1] pair (prev/cur), not a
//     full int[m+1,n+1] table.
//     Modified rows are emitted when adjacent Removed+Added pairs
//     share the same Kind.
// ==========================================================

using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Editor.DocumentEditor.Services;

/// <summary>Computes a structural diff between two <see cref="DocumentModel"/>s.</summary>
public static class DocumentDiffService
{
    private const byte DirDiag = 0;
    private const byte DirUp   = 1;
    private const byte DirLeft = 2;

    /// <summary>Returns one row per block-level change, in display order.</summary>
    public static IReadOnlyList<DocumentDiffRow> Diff(DocumentModel left, DocumentModel right)
    {
        if (left  is null) throw new ArgumentNullException(nameof(left));
        if (right is null) throw new ArgumentNullException(nameof(right));

        var leftRows  = Flatten(left.Blocks);
        var rightRows = Flatten(right.Blocks);
        return BuildRows(leftRows, rightRows);
    }

    // ── Flattening ─────────────────────────────────────────────────────────

    private static List<DocumentDiffSignature> Flatten(IEnumerable<DocumentBlock> blocks)
    {
        var sink = new List<DocumentDiffSignature>();
        Walk(blocks, sink);
        return sink;
    }

    private static void Walk(IEnumerable<DocumentBlock> blocks, List<DocumentDiffSignature> sink)
    {
        foreach (var b in blocks)
        {
            // Sections are pure containers — skip the row, descend into children.
            if (b.Kind is DocumentBlockKinds.Section)
            {
                Walk(b.Children, sink);
                continue;
            }

            sink.Add(new DocumentDiffSignature(b.Kind, FlattenText(b).Trim(), b));

            // For tables/lists, surface child rows so per-cell/per-item edits
            // show up as their own diff rows.
            if (b.Kind is DocumentBlockKinds.Table or DocumentBlockKinds.List)
                Walk(b.Children, sink);
        }
    }

    /// <summary>Concatenates text of <paramref name="b"/>'s subtree in a single walk.</summary>
    private static string FlattenText(DocumentBlock b)
    {
        if (b.Children.Count == 0) return b.Text ?? string.Empty;
        var sb = new System.Text.StringBuilder();
        AppendText(sb, b);
        return sb.ToString();

        static void AppendText(System.Text.StringBuilder sb, DocumentBlock b)
        {
            if (b.Children.Count == 0) { sb.Append(b.Text); return; }
            foreach (var c in b.Children) AppendText(sb, c);
        }
    }

    // ── LCS (byte direction table + rolling int row) ───────────────────────

    private static IReadOnlyList<DocumentDiffRow> BuildRows(
        IReadOnlyList<DocumentDiffSignature> a,
        IReadOnlyList<DocumentDiffSignature> b)
    {
        int m = a.Count, n = b.Count;
        var dir  = new byte[m + 1, n + 1];
        var prev = new int[n + 1];
        var cur  = new int[n + 1];

        for (int i = m - 1; i >= 0; i--)
        {
            for (int j = n - 1; j >= 0; j--)
            {
                if (a[i].Equals(b[j]))
                {
                    cur[j]    = prev[j + 1] + 1;
                    dir[i, j] = DirDiag;
                }
                else if (prev[j] >= cur[j + 1])
                {
                    cur[j]    = prev[j];
                    dir[i, j] = DirUp;
                }
                else
                {
                    cur[j]    = cur[j + 1];
                    dir[i, j] = DirLeft;
                }
            }
            (prev, cur) = (cur, prev);
        }

        var rows = new List<DocumentDiffRow>(Math.Max(m, n));
        int x = 0, y = 0;
        while (x < m && y < n)
        {
            switch (dir[x, y])
            {
                case DirDiag:
                    rows.Add(new DocumentDiffRow(DocumentDiffKind.Equal, a[x].Block, b[y].Block, a[x].Text));
                    x++; y++;
                    break;
                case DirUp:
                    rows.Add(new DocumentDiffRow(DocumentDiffKind.Removed, a[x].Block, null, a[x].Text));
                    x++;
                    break;
                default:
                    rows.Add(new DocumentDiffRow(DocumentDiffKind.Added, null, b[y].Block, b[y].Text));
                    y++;
                    break;
            }
        }
        while (x < m) { rows.Add(new DocumentDiffRow(DocumentDiffKind.Removed, a[x].Block, null, a[x].Text)); x++; }
        while (y < n) { rows.Add(new DocumentDiffRow(DocumentDiffKind.Added,   null, b[y].Block, b[y].Text)); y++; }

        return MergeAdjacentModifications(rows);
    }

    /// <summary>
    /// Replaces adjacent Removed+Added pairs of the same Kind with a single
    /// Modified row — much friendlier display than "deleted P1 / added P1'".
    /// </summary>
    private static IReadOnlyList<DocumentDiffRow> MergeAdjacentModifications(List<DocumentDiffRow> input)
    {
        var output = new List<DocumentDiffRow>(input.Count);
        for (int i = 0; i < input.Count; i++)
        {
            if (TryPairModification(input, i, out var merged))
            {
                output.Add(merged);
                i++;
                continue;
            }
            output.Add(input[i]);
        }
        return output;
    }

    private static bool TryPairModification(List<DocumentDiffRow> rows, int i, out DocumentDiffRow merged)
    {
        merged = default!;
        if (i + 1 >= rows.Count) return false;
        var cur  = rows[i];
        var next = rows[i + 1];
        if (cur.Kind != DocumentDiffKind.Removed || next.Kind != DocumentDiffKind.Added) return false;
        if (cur.Left is null || next.Right is null) return false;
        if (cur.Left.Kind != next.Right.Kind) return false;

        merged = new DocumentDiffRow(
            DocumentDiffKind.Modified, cur.Left, next.Right,
            $"{cur.Text}  →  {next.Text}");
        return true;
    }
}

/// <summary>
/// Internal flattening signature. <c>Block</c> is intentionally excluded from
/// equality so identical text under different block instances still matches.
/// </summary>
internal readonly record struct DocumentDiffSignature(string Kind, string Text, DocumentBlock Block)
{
    public bool Equals(DocumentDiffSignature other) =>
        Kind == other.Kind && Text == other.Text;

    public override int GetHashCode() => HashCode.Combine(Kind, Text);
}

public enum DocumentDiffKind { Equal, Added, Removed, Modified }

/// <summary>One row in a structural diff between two documents.</summary>
public sealed record DocumentDiffRow(
    DocumentDiffKind Kind,
    DocumentBlock?   Left,
    DocumentBlock?   Right,
    string           Text)
{
    /// <summary>Effective Kind label for UI (uses Left when present, otherwise Right).</summary>
    public string BlockKind => Left?.Kind ?? Right?.Kind ?? string.Empty;

    public string KindGlyph => Kind switch
    {
        DocumentDiffKind.Added    => "+",
        DocumentDiffKind.Removed  => "−",
        DocumentDiffKind.Modified => "~",
        _                         => " "
    };
}
