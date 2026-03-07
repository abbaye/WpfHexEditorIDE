// Apache 2.0 - 2026
// Contributors: Claude Sonnet 4.6

using System.IO;

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

        // Deleted positions (flattened from ranges)
        var deleted = new HashSet<long>(
            dto.Edits.Deleted.Sum(d => (int)Math.Min(d.Count, int.MaxValue)));
        foreach (var del in dto.Edits.Deleted)
        {
            long start = ChangesetSerializer.ParseOffset(del.Start);
            for (long i = 0; i < del.Count; i++)
                deleted.Add(start + i);
        }

        // -- Estimate output size and write ---------------------------------
        int insertedTotal = insertedAt.Values.Sum(b => b.Length);
        int estimatedSize = source.Length + insertedTotal - deleted.Count;
        if (estimatedSize < 0) estimatedSize = 0;

        using var ms = new MemoryStream(estimatedSize);

        for (long p = 0; p < source.Length; p++)
        {
            // 1. Insertions before this position
            if (insertedAt.TryGetValue(p, out byte[]? insBytes))
                ms.Write(insBytes, 0, insBytes.Length);

            // 2. Skip deleted
            if (deleted.Contains(p)) continue;

            // 3. Modified or original
            ms.WriteByte(modifiedFlat.TryGetValue(p, out byte newVal) ? newVal : source[p]);
        }

        // Insertions at the very end (position == source.Length)
        if (insertedAt.TryGetValue(source.Length, out byte[]? endBytes))
            ms.Write(endBytes, 0, endBytes.Length);

        return ms.ToArray();
    }
}
