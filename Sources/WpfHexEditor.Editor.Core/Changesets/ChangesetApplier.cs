// GNU Affero General Public License v3.0 - 2026
// Contributors: Claude Sonnet 4.6

using System.IO;
using System.Linq;

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Applies a <see cref="ChangesetDto"/> to a source byte array, producing the
/// fully-patched result that should be written to the physical file.
///
/// Algorithm (one forward pass, physical positions):
///   For each physical position p in source:
///     1. Emit any bytes inserted BEFORE p (InsertedBlock at p)
///     2. If p is deleted: skip
///     3. Else: emit modified value if present, otherwise original byte
///   After last position: emit any inserted bytes at position == source.Length
/// </summary>
public static class ChangesetApplier
{
    /// <summary>
    /// Applies the changeset to <paramref name="source"/> and returns the patched bytes.
    /// The source array is not modified.
    /// </summary>
    public static byte[] Apply(byte[] source, ChangesetDto dto)
    {
        // -- Build lookup tables from the DTO -------------------------------

        // Flat map: physical position → new byte value (expanded from runs)
        var modifiedFlat = new Dictionary<long, byte>(
            dto.Edits.Modified.Sum(m => ChangesetSerializer.ParseHexBytes(m.Values).Length));

        foreach (var m in dto.Edits.Modified)
        {
            long offset = ChangesetSerializer.ParseOffset(m.Offset);
            byte[] values = ChangesetSerializer.ParseHexBytes(m.Values);
            for (int i = 0; i < values.Length; i++)
                modifiedFlat[offset + i] = values[i];
        }

        // Physical position → bytes to insert before it
        var insertedAt = new Dictionary<long, byte[]>(dto.Edits.Inserted.Count);
        foreach (var ins in dto.Edits.Inserted)
        {
            long offset = ChangesetSerializer.ParseOffset(ins.Offset);
            insertedAt[offset] = ChangesetSerializer.ParseHexBytes(ins.Bytes);
        }

        // Deleted ranges — kept as sorted (start, count) pairs; O(d) memory.
        // Using binary search (IsInDeletedRange) instead of a HashSet expansion
        // avoids O(total_deleted_bytes) memory for large contiguous deletions.
        var deletedRanges = dto.Edits.Deleted
            .Select(d => (Start: ChangesetSerializer.ParseOffset(d.Start), d.Count))
            .OrderBy(r => r.Start)
            .ToArray();

        // -- Estimate output size and write ---------------------------------
        long totalDeletedBytes = dto.Edits.Deleted.Sum(d => d.Count);
        int insertedTotal = insertedAt.Values.Sum(b => b.Length);
        int estimatedSize = (int)Math.Max(0, source.Length + insertedTotal - totalDeletedBytes);

        using var ms = new MemoryStream(estimatedSize);

        for (long p = 0; p < source.Length; p++)
        {
            // 1. Insertions before this position
            if (insertedAt.TryGetValue(p, out byte[]? insBytes))
                ms.Write(insBytes, 0, insBytes.Length);

            // 2. Skip deleted
            if (IsInDeletedRange(p, deletedRanges)) continue;

            // 3. Modified or original
            ms.WriteByte(modifiedFlat.TryGetValue(p, out byte newVal) ? newVal : source[p]);
        }

        // Insertions at the very end (position == source.Length)
        if (insertedAt.TryGetValue(source.Length, out byte[]? endBytes))
            ms.Write(endBytes, 0, endBytes.Length);

        return ms.ToArray();
    }

    /// <summary>
    /// Binary search over sorted deleted ranges to determine whether
    /// <paramref name="pos"/> falls inside any range.
    /// O(log d) per call, O(d) total memory — d = number of ranges.
    /// </summary>
    private static bool IsInDeletedRange(long pos, (long Start, long Count)[] ranges)
    {
        int lo = 0, hi = ranges.Length - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            if (pos < ranges[mid].Start)
                hi = mid - 1;
            else if (pos >= ranges[mid].Start + ranges[mid].Count)
                lo = mid + 1;
            else
                return true;
        }
        return false;
    }
}
