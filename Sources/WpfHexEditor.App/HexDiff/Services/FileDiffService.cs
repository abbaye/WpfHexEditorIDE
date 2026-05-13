// Project      : WpfHexEditor.App
// File         : HexDiff/Services/FileDiffService.cs
// Description  : Linear byte-level diff between two byte arrays.
//                O(n) single pass — no LCS (semantically meaningless for binary).
// Architecture : Pure static service; no WPF dependency; unit-testable.

using WpfHexEditor.App.HexDiff.Models;

namespace WpfHexEditor.App.HexDiff.Services;

public static class FileDiffService
{
    public const long MaxFileSizeBytes = 256L * 1024 * 1024; // 256 MB guard

    public static IReadOnlyList<DiffRecord> Diff(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        var results = new List<DiffRecord>();
        int common  = Math.Min(a.Length, b.Length);

        for (int i = 0; i < common; i++)
            if (a[i] != b[i])
                results.Add(new DiffRecord(i, a[i], b[i], DiffKind.Substitution));

        for (int i = common; i < a.Length; i++)
            results.Add(new DiffRecord(i, a[i], 0, DiffKind.Deletion));

        for (int i = common; i < b.Length; i++)
            results.Add(new DiffRecord(i, 0, b[i], DiffKind.Insertion));

        return results;
    }
}
