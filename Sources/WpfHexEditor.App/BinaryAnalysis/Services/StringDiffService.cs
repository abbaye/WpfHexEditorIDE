// Project     : WpfHexEditor.App
// File        : StringDiffService.cs
// Description : Compares two StringRun snapshots and returns per-run diff statuses.
//               Match strategy: exact offset → Unchanged or Modified (value changed).
//               Present in B only → Added; present in A only → Removed.
// Architecture: Stateless static service; O(n) via offset-keyed dictionaries.

namespace WpfHexEditor.App.BinaryAnalysis.Services;

public enum StringDiffStatus { Unchanged, Modified, Added, Removed }

public sealed record StringDiffEntry(StringRun Run, StringDiffStatus Status, string? OldValue = null);

public static class StringDiffService
{
    public static IReadOnlyList<StringDiffEntry> Compare(
        IReadOnlyList<StringRun> snapshotA,
        IReadOnlyList<StringRun> snapshotB)
    {
        // Last-wins on duplicate offsets (degenerate input) to avoid ArgumentException.
        var mapA = snapshotA.GroupBy(r => r.Offset).ToDictionary(g => g.Key, g => g.Last());
        var mapB = snapshotB.GroupBy(r => r.Offset).ToDictionary(g => g.Key, g => g.Last());

        var result = new List<StringDiffEntry>(snapshotA.Count + snapshotB.Count);

        foreach (var b in snapshotB)
        {
            if (mapA.TryGetValue(b.Offset, out var a))
            {
                var status = string.Equals(a.Value, b.Value, StringComparison.Ordinal)
                    ? StringDiffStatus.Unchanged
                    : StringDiffStatus.Modified;
                result.Add(new StringDiffEntry(b, status, status == StringDiffStatus.Modified ? a.Value : null));
            }
            else
            {
                result.Add(new StringDiffEntry(b, StringDiffStatus.Added));
            }
        }

        foreach (var a in snapshotA)
        {
            if (!mapB.ContainsKey(a.Offset))
                result.Add(new StringDiffEntry(a, StringDiffStatus.Removed));
        }

        result.Sort((x, y) => x.Run.Offset.CompareTo(y.Run.Offset));
        return result;
    }
}
