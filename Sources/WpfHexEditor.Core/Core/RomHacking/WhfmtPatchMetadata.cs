//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WpfHexEditor.Core.RomHacking
{
    /// <summary>
    /// Metadata companion file for a binary patch (.ips / .bps / .xdelta).
    /// Saved alongside the patch as a <c>.whfmt</c> file with the same base name.
    ///
    /// Example:
    ///   myrom.bps       ← binary patch
    ///   myrom.whfmt     ← this metadata file
    /// </summary>
    public class WhfmtPatchMetadata
    {
        private static readonly JsonSerializerOptions _options = new()
        {
            WriteIndented          = true,
            PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        // ------------------------------------------------------------------
        // Properties
        // ------------------------------------------------------------------

        /// <summary>
        /// Patch format: "IPS", "BPS" or "xdelta".
        /// </summary>
        public string Format      { get; set; }

        /// <summary>
        /// Tool that created the patch (e.g. "WpfHexEditor 2.8.0").
        /// </summary>
        public string ToolVersion { get; set; }

        /// <summary>
        /// ISO-8601 UTC creation date/time.
        /// </summary>
        public string Created     { get; set; }

        /// <summary>
        /// Optional author name.
        /// </summary>
        public string Author      { get; set; }

        /// <summary>
        /// Optional human-readable description of the patch.
        /// </summary>
        public string Description { get; set; }

        public PatchFileInfo SourceFile { get; set; }
        public PatchFileInfo TargetFile { get; set; }

        // ------------------------------------------------------------------
        // Factory
        // ------------------------------------------------------------------

        /// <summary>
        /// Generates a <see cref="WhfmtPatchMetadata"/> from the original and modified byte arrays.
        /// CRC32 is computed over the full buffers.
        /// </summary>
        public static WhfmtPatchMetadata Generate(
            PatchFormat format,
            byte[]      original,
            byte[]      modified,
            string      sourceName = null,
            string      targetName = null,
            string      author     = null,
            string      description = null)
        {
            uint crcSrc = CRC32.Compute(original);
            uint crcTgt = CRC32.Compute(modified);

            return new WhfmtPatchMetadata
            {
                Format      = format.ToString(),
                ToolVersion = $"WpfHexEditor 2.8.0",
                Created     = DateTime.UtcNow.ToString("O"),
                Author      = author,
                Description = description,
                SourceFile  = new PatchFileInfo
                {
                    Name  = sourceName ?? "source",
                    Size  = original.Length,
                    Crc32 = crcSrc.ToString("X8")
                },
                TargetFile  = new PatchFileInfo
                {
                    Name  = targetName ?? "target",
                    Size  = modified.Length,
                    Crc32 = crcTgt.ToString("X8")
                }
            };
        }

        // ------------------------------------------------------------------
        // Serialization
        // ------------------------------------------------------------------

        /// <summary>
        /// Saves this metadata to <paramref name="whfmtPath"/> as a formatted JSON file.
        /// </summary>
        public void Save(string whfmtPath)
        {
            var json = JsonSerializer.Serialize(this, _options);
            File.WriteAllText(whfmtPath, json, System.Text.Encoding.UTF8);
        }

        /// <summary>
        /// Loads metadata from an existing <c>.whfmt</c> file.
        /// Returns <see langword="null"/> on error.
        /// </summary>
        public static WhfmtPatchMetadata Load(string whfmtPath)
        {
            try
            {
                if (!File.Exists(whfmtPath)) return null;
                var json = File.ReadAllText(whfmtPath, System.Text.Encoding.UTF8);
                return JsonSerializer.Deserialize<WhfmtPatchMetadata>(json, _options);
            }
            catch { return null; }
        }

        /// <summary>
        /// Returns the companion <c>.whfmt</c> path for a given patch file path.
        /// E.g. "C:\patches\myrom.bps" → "C:\patches\myrom.whfmt"
        /// </summary>
        public static string MetadataPathFor(string patchFilePath)
            => Path.ChangeExtension(patchFilePath, ".whfmt");

        /// <summary>
        /// Validates the source and target CRC32 against the provided byte arrays.
        /// Returns <see langword="true"/> when both checksums match.
        /// </summary>
        public bool Validate(byte[] original, byte[] modified)
        {
            if (SourceFile?.Crc32 == null || TargetFile?.Crc32 == null) return false;
            uint expectedSrc = Convert.ToUInt32(SourceFile.Crc32, 16);
            uint expectedTgt = Convert.ToUInt32(TargetFile.Crc32, 16);
            return CRC32.Compute(original) == expectedSrc
                && CRC32.Compute(modified) == expectedTgt;
        }
    }

    /// <summary>
    /// Source or target file information stored in <see cref="WhfmtPatchMetadata"/>.
    /// </summary>
    public class PatchFileInfo
    {
        /// <summary>
        /// Original file name (without path).
        /// </summary>
        public string Name  { get; set; }

        /// <summary>
        /// File size in bytes.
        /// </summary>
        public long   Size  { get; set; }

        /// <summary>
        /// CRC-32 checksum as 8-character uppercase hex string (e.g. "A1B2C3D4").
        /// </summary>
        public string Crc32 { get; set; }
    }
}
