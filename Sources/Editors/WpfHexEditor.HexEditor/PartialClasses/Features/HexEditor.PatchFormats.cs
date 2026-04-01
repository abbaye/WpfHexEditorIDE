// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: HexEditor.PatchFormats.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Partial class providing the unified multi-format patch API for the HexEditor.
//     Supports IPS, BPS, and xdelta patch formats through a single Apply/Create
//     interface, delegating to WpfHexEditor.Core.RomHacking format handlers.
//
// Architecture Notes:
//     Unified facade over format-specific handlers in Core.RomHacking.
//     UI dialog methods for IPS remain in HexEditor.IPSPatcher.cs.
//
// ==========================================================

using System;
using System.IO;
using WpfHexEditor.Core.RomHacking;

namespace WpfHexEditor.HexEditor
{
    /// <summary>
    /// Unified patch API for IPS, BPS and xdelta formats.
    /// The IPS-specific dialog methods in HexEditor.IPSPatcher.cs remain unchanged.
    /// This file adds format-agnostic programmatic methods.
    /// </summary>
    public partial class HexEditor
    {
        // ------------------------------------------------------------------
        // Auto-detection
        // ------------------------------------------------------------------

        /// <summary>
        /// Detects the patch format of <paramref name="patchFilePath"/> from its magic bytes.
        /// Returns <see langword="null"/> when the format is not recognised.
        /// </summary>
        public static PatchFormat? DetectPatchFormat(string patchFilePath)
        {
            if (!File.Exists(patchFilePath)) return null;

            if (IPSPatcher.IsValidIPSFile(patchFilePath))  return PatchFormat.IPS;
            if (BPSPatcher.IsValidBPSFile(patchFilePath))  return PatchFormat.BPS;
            if (XDeltaPatcher.IsValidXDeltaFile(patchFilePath)) return PatchFormat.XDelta;

            return null;
        }

        // ------------------------------------------------------------------
        // Apply
        // ------------------------------------------------------------------

        /// <summary>
        /// Applies a patch file to the currently loaded data.
        /// The format is auto-detected from the file's magic bytes unless
        /// <paramref name="format"/> is specified explicitly.
        /// </summary>
        /// <param name="patchFilePath">Path to the patch file (.ips / .bps / .xdelta).</param>
        /// <param name="format">
        /// Optional explicit format. When <see langword="null"/> the format is auto-detected.
        /// </param>
        /// <returns>
        /// A <see cref="PatchResult"/> describing success or failure.
        /// On success the editor content is replaced with the patched data.
        /// </returns>
        public PatchResult ApplyPatch(string patchFilePath, PatchFormat? format = null)
        {
            if (!IsFileOrStreamLoaded)
                return PatchResult.CreateFailure(format ?? PatchFormat.IPS, "No file loaded.");

            if (!File.Exists(patchFilePath))
                return PatchResult.CreateFailure(format ?? PatchFormat.IPS, $"Patch file not found: {patchFilePath}");

            var detected = format ?? DetectPatchFormat(patchFilePath);
            if (detected == null)
                return PatchResult.CreateFailure(PatchFormat.IPS, "Unknown patch format (unrecognised magic bytes).");

            var source = GetAllBytes();
            var patch  = File.ReadAllBytes(patchFilePath);

            PatchResult result;
            switch (detected.Value)
            {
                case PatchFormat.IPS:
                {
                    var ipsResult = IPSPatcher.ApplyPatchFromBytes(ref source, patch);
                    result = ipsResult.Success
                        ? PatchResult.CreateSuccess(PatchFormat.IPS, ipsResult.OriginalFileSize, ipsResult.PatchedFileSize, ipsResult.Duration)
                        : PatchResult.CreateFailure(PatchFormat.IPS, ipsResult.ErrorMessage);
                    break;
                }

                case PatchFormat.BPS:
                    result = BPSPatcher.ApplyPatch(source, patch, out source);
                    break;

                case PatchFormat.XDelta:
                    result = XDeltaPatcher.ApplyPatch(source, patch, out source);
                    break;

                default:
                    return PatchResult.CreateFailure(detected.Value, $"Unsupported format: {detected.Value}");
            }

            if (result.Success)
                OpenMemory(source);

            return result;
        }

        // ------------------------------------------------------------------
        // Create from unsaved changes
        // ------------------------------------------------------------------

        /// <summary>
        /// Creates a patch from the current unsaved modifications and writes it to
        /// <paramref name="outputPath"/>. Equivalent to saving the file and comparing
        /// with the original on disk.
        /// </summary>
        /// <param name="format">Patch format to generate.</param>
        /// <param name="outputPath">Destination file path for the patch.</param>
        /// <param name="writeMetadata">
        /// When <see langword="true"/>, a companion <c>.whfmt</c> metadata file is written
        /// next to the patch (same base name, <c>.whfmt</c> extension).
        /// </param>
        /// <returns>
        /// <see langword="true"/> on success; <see langword="false"/> when there is nothing
        /// to patch (no file loaded or no unsaved changes).
        /// </returns>
        public bool CreatePatchFromUnsavedChanges(PatchFormat format, string outputPath, bool writeMetadata = false)
        {
            if (!IsFileOrStreamLoaded || !IsModified)
                return false;

            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("Output path must not be empty.", nameof(outputPath));

            // Original = file on disk; modified = current in-memory state
            byte[] original = !string.IsNullOrEmpty(FileName) && File.Exists(FileName)
                ? File.ReadAllBytes(FileName)
                : GetAllBytes(copyChange: false);

            byte[] modified = GetAllBytes(copyChange: true);

            byte[] patchBytes;
            switch (format)
            {
                case PatchFormat.IPS:
                    patchBytes = IPSPatcher.CreatePatch(original, modified);
                    break;

                case PatchFormat.BPS:
                    patchBytes = BPSPatcher.CreatePatch(original, modified,
                        metadata: !string.IsNullOrEmpty(FileName) ? Path.GetFileName(FileName) : string.Empty);
                    break;

                case PatchFormat.XDelta:
                    patchBytes = XDeltaPatcher.CreatePatch(original, modified);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }

            File.WriteAllBytes(outputPath, patchBytes);

            if (writeMetadata)
            {
                var meta = WhfmtPatchMetadata.Generate(
                    format,
                    original,
                    modified,
                    sourceName: !string.IsNullOrEmpty(FileName) ? Path.GetFileName(FileName) : "source",
                    targetName: Path.GetFileNameWithoutExtension(outputPath) + "_patched" +
                                (!string.IsNullOrEmpty(FileName) ? Path.GetExtension(FileName) : ".bin"));

                meta.Save(WhfmtPatchMetadata.MetadataPathFor(outputPath));
            }

            return true;
        }
    }
}
