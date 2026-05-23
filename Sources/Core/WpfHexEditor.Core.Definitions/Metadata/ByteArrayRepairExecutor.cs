// ==========================================================
// Project: WpfHexEditor.Core.Definitions
// File: Metadata/ByteArrayRepairExecutor.cs
// Description: P14 — in-memory IWhfmtRepairExecutor that applies RepairActions
//              directly to a byte[] without touching the disk. Supports the
//              mechanical repair actions: patch-bytes, zero-range, add-padding.
//              Checksum recomputation is delegated to ChecksumAlgorithmHelper.
// Architecture: Immutable input — returns new byte[] rather than mutating.
//              Keeps Core.Definitions free of any UI or file-system dependency.
// ==========================================================

namespace WpfHexEditor.Core.Definitions.Metadata;

/// <summary>
/// P14 — applies <see cref="RepairAction"/> entries to an in-memory byte array.
/// Suitable for IDE preview dialogs (before committing to disk).
/// </summary>
public sealed class ByteArrayRepairExecutor
{
    /// <summary>
    /// Applies <paramref name="action"/> to a copy of <paramref name="source"/>.
    /// Returns the patched bytes + a human-readable description of changes.
    /// On failure returns the original bytes + the error message.
    /// </summary>
    public ByteRepairResult Apply(byte[] source, RepairAction action)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            return action.Action.ToLowerInvariant() switch
            {
                "patch-bytes"       => ApplyPatchBytes(source, action),
                "patch_bytes"       => ApplyPatchBytes(source, action),
                "zero-range"        => ApplyZeroRange(source, action),
                "zero_range"        => ApplyZeroRange(source, action),
                "add-padding"       => ApplyAddPadding(source, action),
                "add_padding"       => ApplyAddPadding(source, action),
                "recompute-checksum"=> new ByteRepairResult(source, false, 0,
                    "Checksum recomputation requires the full file context — use whfmt.Validate repair command."),
                _ => new ByteRepairResult(source, false, 0,
                    $"Unknown repair action '{action.Action}'.")
            };
        }
        catch (Exception ex)
        {
            return new ByteRepairResult(source, false, 0, ex.Message);
        }
    }

    // ── Repair handlers ──────────────────────────────────────────────────────

    /// <summary>Patches a hex byte sequence at the offset declared in action.Target.</summary>
    private static ByteRepairResult ApplyPatchBytes(byte[] src, RepairAction action)
    {
        if (!TryParseTarget(action.Target, out long offset, out string? hexValue))
            return new ByteRepairResult(src, false, 0, $"Cannot parse target '{action.Target}' as offset:hexValue.");

        if (string.IsNullOrEmpty(hexValue))
            return new ByteRepairResult(src, false, 0, "patch-bytes requires a hex value in target (e.g. '0:504B0304').");

        byte[] patch = HexToBytes(hexValue);
        if (offset < 0 || offset + patch.Length > src.Length)
            return new ByteRepairResult(src, false, 0,
                $"Patch offset {offset}+{patch.Length} exceeds file length {src.Length}.");

        byte[] patched = (byte[])src.Clone();
        patch.CopyTo(patched, (int)offset);
        return new ByteRepairResult(patched, true, patch.Length,
            $"Patched {patch.Length} byte(s) at offset 0x{offset:X}.");
    }

    /// <summary>Zeroes out a byte range declared in action.Target as offset:length.</summary>
    private static ByteRepairResult ApplyZeroRange(byte[] src, RepairAction action)
    {
        if (!TryParseOffsetLength(action.Target, out long offset, out int length))
            return new ByteRepairResult(src, false, 0,
                $"Cannot parse target '{action.Target}' as offset:length.");

        if (offset < 0 || offset + length > src.Length)
            return new ByteRepairResult(src, false, 0,
                $"Zero-range {offset}+{length} exceeds file length {src.Length}.");

        byte[] patched = (byte[])src.Clone();
        Array.Clear(patched, (int)offset, length);
        return new ByteRepairResult(patched, true, length,
            $"Zeroed {length} byte(s) at offset 0x{offset:X}.");
    }

    /// <summary>Appends zero-padding until the file length is a multiple of the alignment value.</summary>
    private static ByteRepairResult ApplyAddPadding(byte[] src, RepairAction action)
    {
        if (!long.TryParse(action.Target, out long alignment) || alignment <= 0)
            return new ByteRepairResult(src, false, 0,
                $"add-padding requires a positive integer alignment in target, got '{action.Target}'.");

        long remainder = src.Length % alignment;
        if (remainder == 0)
            return new ByteRepairResult(src, true, 0, "File is already aligned — no padding added.");

        int padCount   = (int)(alignment - remainder);
        byte[] patched = new byte[src.Length + padCount];
        src.CopyTo(patched, 0);
        return new ByteRepairResult(patched, true, padCount,
            $"Added {padCount} byte(s) of padding to align to {alignment}.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Parses "offset:hexValue" format used by patch-bytes actions.</summary>
    private static bool TryParseTarget(string? target, out long offset, out string? hexValue)
    {
        offset   = 0;
        hexValue = null;
        if (string.IsNullOrWhiteSpace(target)) return false;

        int colon = target.IndexOf(':');
        if (colon <= 0) return false;

        string offStr = target[..colon].Trim();
        hexValue      = target[(colon + 1)..].Trim();

        if (offStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return long.TryParse(offStr[2..], System.Globalization.NumberStyles.HexNumber, null, out offset);
        return long.TryParse(offStr, out offset);
    }

    /// <summary>Parses "offset:length" format used by zero-range actions.</summary>
    private static bool TryParseOffsetLength(string? target, out long offset, out int length)
    {
        offset = 0;
        length = 0;
        if (string.IsNullOrWhiteSpace(target)) return false;

        int colon = target.IndexOf(':');
        if (colon <= 0) return false;

        string offStr = target[..colon].Trim();
        string lenStr = target[(colon + 1)..].Trim();

        bool okOff = offStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? long.TryParse(offStr[2..], System.Globalization.NumberStyles.HexNumber, null, out offset)
            : long.TryParse(offStr, out offset);
        bool okLen = int.TryParse(lenStr, out length);
        return okOff && okLen;
    }

    private static byte[] HexToBytes(string hex)
    {
        hex = hex.Replace(" ", "").Replace("-", "");
        if (hex.Length % 2 != 0) hex = "0" + hex;
        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return bytes;
    }
}

/// <summary>Result of a <see cref="ByteArrayRepairExecutor.Apply"/> call.</summary>
public sealed record ByteRepairResult(
    byte[]  Patched,
    bool    Success,
    int     BytesChanged,
    string  Message);
