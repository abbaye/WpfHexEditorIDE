// Apache 2.0 - 2026
// Contributors: Claude Sonnet 4.6

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.ProjectSystem.Services;

/// <summary>
/// Manages .whchg companion files on disk for project items in Tracked save mode.
///
/// A .whchg file sits beside its source file:
///   game.smc       ← physical binary (untouched until Write-to-Disk)
///   game.smc.whchg ← JSON changeset (modify/insert/delete ops, WHChg format v1)
///
/// All methods are async and non-blocking.
/// </summary>
public sealed class ChangesetService
{
    // -- Singleton ---------------------------------------------------------

    public static readonly ChangesetService Instance = new();
    private ChangesetService() { }

    // -- Path helpers ------------------------------------------------------

    /// <summary>Returns the companion .whchg path for a project item.</summary>
    public string GetChangesetPath(IProjectItem item)
        => item.AbsolutePath + ".whchg";

    /// <summary>True when a .whchg companion file exists on disk.</summary>
    public bool HasChangeset(IProjectItem item)
        => File.Exists(GetChangesetPath(item));

    // -- Read / Write ------------------------------------------------------

    /// <summary>
    /// Serialises <paramref name="snapshot"/> to the companion .whchg file.
    /// CRC32 of the source file is computed and embedded in the DTO for integrity checks.
    /// </summary>
    public async Task WriteChangesetAsync(
        IProjectItem     item,
        ChangesetSnapshot snapshot,
        DateTimeOffset   createdAt      = default,
        CancellationToken ct            = default)
    {
        if (!snapshot.HasEdits)
            return;

        string srcPath  = item.AbsolutePath;
        string destPath = GetChangesetPath(item);

        // Compute CRC32 of source file for the sourceHash field
        string? sourceHash = null;
        if (File.Exists(srcPath))
        {
            try { sourceHash = "crc32:" + ComputeCrc32Hex(srcPath); }
            catch { /* best-effort */ }
        }

        var dto = ChangesetSerializer.ToDto(
            snapshot,
            sourceFile: Path.GetFileName(srcPath),
            sourceHash: sourceHash,
            created:    createdAt == default ? DateTimeOffset.UtcNow : createdAt);

        await using var fs = new FileStream(
            destPath, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 4096, useAsync: true);
        await ChangesetSerializer.WriteAsync(dto, fs, ct).ConfigureAwait(false);
    }

    /// <summary>Reads the .whchg companion file synchronously.
    /// Safe on the UI thread for small .whchg files (e.g. during content factory calls).
    /// Returns null if the file does not exist.</summary>
    public ChangesetDto? ReadChangeset(IProjectItem item)
    {
        string path = GetChangesetPath(item);
        if (!File.Exists(path)) return null;
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return ChangesetSerializer.Read(fs);
    }

    /// <summary>Reads the .whchg companion file, or returns null if it doesn't exist.</summary>
    public async Task<ChangesetDto?> ReadChangesetAsync(
        IProjectItem      item,
        CancellationToken ct = default)
    {
        string path = GetChangesetPath(item);
        if (!File.Exists(path)) return null;

        await using var fs = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 4096, useAsync: true);
        return await ChangesetSerializer.ReadAsync(fs, ct).ConfigureAwait(false);
    }

    // -- Write to Disk -----------------------------------------------------

    /// <summary>
    /// Applies the .whchg changeset to the physical file on disk, then removes the companion file.
    /// Throws <see cref="InvalidOperationException"/> if no changeset exists.
    /// The caller must reload the editor after this call.
    /// </summary>
    public async Task ApplyChangesetToDiskAsync(
        IProjectItem      item,
        CancellationToken ct = default)
    {
        var dto = await ReadChangesetAsync(item, ct).ConfigureAwait(false)
                  ?? throw new InvalidOperationException(
                         $"No .whchg found for '{item.Name}'.");

        string srcPath = item.AbsolutePath;
        byte[] original = await File.ReadAllBytesAsync(srcPath, ct).ConfigureAwait(false);
        byte[] patched  = ChangesetApplier.Apply(original, dto);

        // Atomic write: write to temp file then replace
        string tmpPath = srcPath + ".whchg_tmp";
        await File.WriteAllBytesAsync(tmpPath, patched, ct).ConfigureAwait(false);
        File.Move(tmpPath, srcPath, overwrite: true);

        // Remove the companion file
        await DeleteChangesetAsync(item, ct).ConfigureAwait(false);
    }

    // -- Discard -----------------------------------------------------------

    /// <summary>Deletes the .whchg companion file without applying it.</summary>
    public Task DeleteChangesetAsync(IProjectItem item, CancellationToken ct = default)
    {
        string path = GetChangesetPath(item);
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    // -- CRC32 helper ------------------------------------------------------

    private static string ComputeCrc32Hex(string filePath)
    {
        // Simple CRC32 implementation (polynomial 0xEDB88320)
        uint crc = 0xFFFFFFFF;
        using var fs = File.OpenRead(filePath);
        int b;
        while ((b = fs.ReadByte()) != -1)
        {
            crc ^= (byte)b;
            for (int i = 0; i < 8; i++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
        }
        return (~crc).ToString("X8");
    }
}
