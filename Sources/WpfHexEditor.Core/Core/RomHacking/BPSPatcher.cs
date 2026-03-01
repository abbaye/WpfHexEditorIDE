using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace WpfHexEditor.Core.RomHacking
{
    /// <summary>
    /// BPS (Beat Patch System) patcher — format spec by Near/byuu.
    /// Supports creating and applying BPS1 patches including CRC-32 verification.
    ///
    /// BPS format overview:
    ///   Header  : "BPS1" (4 bytes ASCII)
    ///   VLQ     : sourceSize, targetSize, metadataLength
    ///   Metadata: UTF-8 string (metadataLength bytes)
    ///   Actions : VLQ-encoded   (action &lt;&lt; 2) | (length - 1)
    ///             0 = SourceRead   — copy from source at current position
    ///             1 = TargetRead   — literal bytes follow
    ///             2 = SourceCopy   — copy from source at signed relative offset
    ///             3 = TargetCopy   — copy from already-written target bytes
    ///   Footer  : CRC32-source (4 LE), CRC32-target (4 LE), CRC32-patch (4 LE)
    /// </summary>
    public static class BPSPatcher
    {
        private const string BPS_HEADER = "BPS1";

        // ------------------------------------------------------------------
        // Validation
        // ------------------------------------------------------------------

        public static bool IsValidBPSFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return false;
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                if (fs.Length < 4) return false;
                var buf = new byte[4];
                fs.Read(buf, 0, 4);
                return Encoding.ASCII.GetString(buf) == BPS_HEADER;
            }
            catch { return false; }
        }

        public static bool IsValidBPSBytes(byte[] data)
        {
            if (data == null || data.Length < 4) return false;
            return Encoding.ASCII.GetString(data, 0, 4) == BPS_HEADER;
        }

        // ------------------------------------------------------------------
        // Apply (decode)
        // ------------------------------------------------------------------

        public static PatchResult ApplyPatch(string sourceFilePath, string patchFilePath, string outputFilePath)
        {
            if (!File.Exists(sourceFilePath))
                return PatchResult.CreateFailure(PatchFormat.BPS, $"Source not found: {sourceFilePath}");
            if (!File.Exists(patchFilePath))
                return PatchResult.CreateFailure(PatchFormat.BPS, $"Patch not found: {patchFilePath}");

            var source = File.ReadAllBytes(sourceFilePath);
            var patch  = File.ReadAllBytes(patchFilePath);

            var result = ApplyPatch(source, patch);
            if (result.Success && outputFilePath != null)
                File.WriteAllBytes(outputFilePath, _lastTarget);

            return result;
        }

        public static PatchResult ApplyPatch(byte[] source, byte[] patch, out byte[] target)
        {
            var result = ApplyPatch(source, patch);
            target = result.Success ? _lastTarget : null;
            return result;
        }

        // Keeps the last successfully decoded target for file-based overload above.
        [ThreadStatic] private static byte[] _lastTarget;

        public static PatchResult ApplyPatch(byte[] source, byte[] patch)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                if (source == null || source.Length == 0)
                    return PatchResult.CreateFailure(PatchFormat.BPS, "Source is empty");
                if (!IsValidBPSBytes(patch))
                    return PatchResult.CreateFailure(PatchFormat.BPS, "Not a valid BPS1 patch");

                // --- verify patch CRC32 (last 4 bytes) ---
                uint patchCrc = ReadUInt32LE(patch, patch.Length - 4);
                uint computedPatchCrc = CRC32.Compute(patch, 0, patch.Length - 4);
                if (patchCrc != computedPatchCrc)
                    return PatchResult.CreateFailure(PatchFormat.BPS, $"Patch CRC32 mismatch (expected 0x{patchCrc:X8}, got 0x{computedPatchCrc:X8})");

                // --- verify source CRC32 ---
                uint sourceCrc = ReadUInt32LE(patch, patch.Length - 12);
                uint computedSourceCrc = CRC32.Compute(source);
                if (sourceCrc != computedSourceCrc)
                    return PatchResult.CreateFailure(PatchFormat.BPS, $"Source CRC32 mismatch (expected 0x{sourceCrc:X8}, got 0x{computedSourceCrc:X8})");

                int pos = 4; // skip "BPS1"

                ulong sourceSize = ReadVLQ(patch, ref pos);
                ulong targetSize = ReadVLQ(patch, ref pos);
                ulong metaLen   = ReadVLQ(patch, ref pos);
                pos += (int)metaLen; // skip metadata string

                var target = new byte[targetSize];
                int outputOffset = 0;
                int sourceRelOffset = 0;
                int targetRelOffset = 0;

                int actionEnd = patch.Length - 12; // last 12 bytes = 3× CRC32

                while (pos < actionEnd)
                {
                    ulong data   = ReadVLQ(patch, ref pos);
                    int   action = (int)(data & 3);
                    int   length = (int)(data >> 2) + 1;

                    switch (action)
                    {
                        case 0: // SourceRead
                            for (int i = 0; i < length; i++)
                                target[outputOffset + i] = source[outputOffset + i];
                            break;

                        case 1: // TargetRead
                            Array.Copy(patch, pos, target, outputOffset, length);
                            pos += length;
                            break;

                        case 2: // SourceCopy
                        {
                            ulong offsetData = ReadVLQ(patch, ref pos);
                            int   delta      = (offsetData & 1) != 0 ? -(int)(offsetData >> 1) : (int)(offsetData >> 1);
                            sourceRelOffset += delta;
                            for (int i = 0; i < length; i++)
                                target[outputOffset + i] = source[sourceRelOffset + i];
                            sourceRelOffset += length;
                            break;
                        }

                        case 3: // TargetCopy
                        {
                            ulong offsetData = ReadVLQ(patch, ref pos);
                            int   delta      = (offsetData & 1) != 0 ? -(int)(offsetData >> 1) : (int)(offsetData >> 1);
                            targetRelOffset += delta;
                            for (int i = 0; i < length; i++)
                                target[outputOffset + i] = target[targetRelOffset + i];
                            targetRelOffset += length;
                            break;
                        }
                    }

                    outputOffset += length;
                }

                // --- verify target CRC32 ---
                uint targetCrc = ReadUInt32LE(patch, patch.Length - 8);
                uint computedTargetCrc = CRC32.Compute(target);
                if (targetCrc != computedTargetCrc)
                    return PatchResult.CreateFailure(PatchFormat.BPS, $"Target CRC32 mismatch (expected 0x{targetCrc:X8}, got 0x{computedTargetCrc:X8})");

                _lastTarget = target;
                sw.Stop();
                return PatchResult.CreateSuccess(PatchFormat.BPS, source.Length, target.Length, sw.Elapsed);
            }
            catch (Exception ex)
            {
                sw.Stop();
                return PatchResult.CreateFailure(PatchFormat.BPS, $"Unexpected error: {ex.Message}");
            }
        }

        // ------------------------------------------------------------------
        // Create (encode)
        // ------------------------------------------------------------------

        /// <summary>
        /// Creates a BPS1 patch by comparing <paramref name="original"/> with <paramref name="modified"/>.
        /// Uses a greedy hash-table strategy: SourceRead where source matches at the same position,
        /// SourceCopy for matches elsewhere in source, TargetRead for new bytes.
        /// </summary>
        public static byte[] CreatePatch(byte[] original, byte[] modified, string metadata = "")
        {
            if (original == null) throw new ArgumentNullException(nameof(original));
            if (modified == null) throw new ArgumentNullException(nameof(modified));

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);

            // Header
            bw.Write(Encoding.ASCII.GetBytes(BPS_HEADER));

            // VLQ fields
            WriteVLQ(bw, (ulong)original.Length);
            WriteVLQ(bw, (ulong)modified.Length);

            var metaBytes = string.IsNullOrEmpty(metadata) ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(metadata);
            WriteVLQ(bw, (ulong)metaBytes.Length);
            if (metaBytes.Length > 0) bw.Write(metaBytes);

            // Build source hash table: 4-byte key → list of positions
            var sourceHash = BuildHashTable(original);

            const int MIN_COPY = 6;
            int outputOffset = 0;
            int sourceRelPos = 0;

            while (outputOffset < modified.Length)
            {
                // Try SourceRead first (same position, same bytes)
                if (outputOffset < original.Length && original[outputOffset] == modified[outputOffset])
                {
                    int run = 0;
                    while (outputOffset + run < modified.Length
                           && outputOffset + run < original.Length
                           && original[outputOffset + run] == modified[outputOffset + run])
                        run++;

                    WriteAction(bw, 0, run);
                    outputOffset += run;
                    sourceRelPos  = outputOffset;
                    continue;
                }

                // Try SourceCopy (best match in source)
                int copyLen   = 0;
                int copyStart = -1;
                if (outputOffset + MIN_COPY <= modified.Length)
                {
                    uint key = HashKey(modified, outputOffset);
                    if (sourceHash.TryGetValue(key, out var candidates))
                    {
                        foreach (int srcPos in candidates)
                        {
                            int len = 0;
                            while (srcPos   + len < original.Length
                                   && outputOffset + len < modified.Length
                                   && original[srcPos + len] == modified[outputOffset + len])
                                len++;
                            if (len > copyLen) { copyLen = len; copyStart = srcPos; }
                        }
                    }
                }

                if (copyLen >= MIN_COPY)
                {
                    int delta = copyStart - sourceRelPos;
                    ulong encoded = delta >= 0 ? (ulong)(delta << 1) : (ulong)((-delta << 1) | 1);
                    WriteAction(bw, 2, copyLen);
                    WriteVLQ(bw, encoded);
                    sourceRelPos   = copyStart + copyLen;
                    outputOffset  += copyLen;
                    continue;
                }

                // TargetRead — emit one byte as literal
                // Collect as many non-matchable bytes as possible in a single TargetRead record
                int litStart = outputOffset;
                int litLen   = 0;
                while (outputOffset + litLen < modified.Length)
                {
                    // Check if a good source match exists starting here
                    if (outputOffset + litLen + MIN_COPY <= modified.Length)
                    {
                        uint key2 = HashKey(modified, outputOffset + litLen);
                        if (sourceHash.TryGetValue(key2, out var cands2))
                        {
                            bool found = false;
                            foreach (int p in cands2)
                            {
                                int l = 0;
                                while (p + l < original.Length
                                       && outputOffset + litLen + l < modified.Length
                                       && original[p + l] == modified[outputOffset + litLen + l])
                                    l++;
                                if (l >= MIN_COPY) { found = true; break; }
                            }
                            if (found) break;
                        }
                    }
                    // Also break if SourceRead would kick in
                    if (outputOffset + litLen < original.Length
                        && original[outputOffset + litLen] == modified[outputOffset + litLen])
                    {
                        int srun = 0;
                        while (outputOffset + litLen + srun < original.Length
                               && outputOffset + litLen + srun < modified.Length
                               && original[outputOffset + litLen + srun] == modified[outputOffset + litLen + srun])
                            srun++;
                        if (srun >= MIN_COPY) break;
                    }
                    litLen++;
                    if (litLen >= 65535) break;
                }
                if (litLen == 0) litLen = 1;

                WriteAction(bw, 1, litLen);
                bw.Write(modified, litStart, litLen);
                outputOffset += litLen;
            }

            bw.Flush();
            byte[] patchWithoutCrc = ms.ToArray();

            // Footer: CRC32 source, CRC32 target, CRC32 patch-so-far
            uint crcSource = CRC32.Compute(original);
            uint crcTarget = CRC32.Compute(modified);
            uint crcPatch  = CRC32.Compute(patchWithoutCrc);

            using var full = new MemoryStream(patchWithoutCrc.Length + 12);
            full.Write(patchWithoutCrc, 0, patchWithoutCrc.Length);
            WriteUInt32LE(full, crcSource);
            WriteUInt32LE(full, crcTarget);

            byte[] patchBeforeSelfCrc = full.ToArray();
            uint crcFinal = CRC32.Compute(patchBeforeSelfCrc);

            full.Write(BitConverter.GetBytes(crcFinal), 0, 4);
            return full.ToArray();
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static ulong ReadVLQ(byte[] data, ref int pos)
        {
            ulong result = 0;
            int   shift  = 1;
            while (true)
            {
                byte b = data[pos++];
                result += (ulong)(b & 0x7F) * (ulong)shift;
                if ((b & 0x80) != 0) break;
                shift <<= 7;
                result += (ulong)shift;
            }
            return result;
        }

        private static void WriteVLQ(BinaryWriter bw, ulong value)
        {
            while (true)
            {
                byte b = (byte)(value & 0x7F);
                value >>= 7;
                if (value == 0) { bw.Write((byte)(b | 0x80)); break; }
                bw.Write(b);
                value--;
            }
        }

        private static void WriteAction(BinaryWriter bw, int action, int length)
            => WriteVLQ(bw, (ulong)(((length - 1) << 2) | action));

        private static uint ReadUInt32LE(byte[] data, int offset)
            => (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));

        private static void WriteUInt32LE(Stream s, uint value)
        {
            s.WriteByte((byte)(value & 0xFF));
            s.WriteByte((byte)((value >> 8) & 0xFF));
            s.WriteByte((byte)((value >> 16) & 0xFF));
            s.WriteByte((byte)((value >> 24) & 0xFF));
        }

        private static uint HashKey(byte[] data, int offset)
        {
            if (offset + 4 > data.Length) return 0;
            return (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
        }

        private static Dictionary<uint, List<int>> BuildHashTable(byte[] data)
        {
            var table = new Dictionary<uint, List<int>>();
            for (int i = 0; i <= data.Length - 4; i++)
            {
                uint key = HashKey(data, i);
                if (!table.TryGetValue(key, out var list))
                    table[key] = list = new List<int>(2);
                list.Add(i);
            }
            return table;
        }
    }
}
