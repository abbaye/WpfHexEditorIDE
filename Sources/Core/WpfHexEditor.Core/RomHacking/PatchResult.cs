// ==========================================================
// Project: WpfHexEditor.Core
// File: PatchResult.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     Generic result value object returned by all patch operations (IPS, BPS, XDelta).
//     Carries success flag, error message, patch format, file sizes, and elapsed duration.
//     Created via factory methods CreateSuccess and CreateFailure.
//
// Architecture Notes:
//     Value object pattern — no WPF dependencies, no mutable state after construction.
//     Shared across BPSPatcher, IPSPatcher, and XDeltaPatcher as a uniform result type.
//
// ==========================================================

using System;

namespace WpfHexEditor.Core.RomHacking
{
    /// <summary>
    /// Generic result returned by all patch operations (IPS, BPS, xdelta).
    /// </summary>
    public class PatchResult
    {
        public bool        Success          { get; set; }
        public string      ErrorMessage     { get; set; }
        public PatchFormat Format           { get; set; }
        public long        OriginalFileSize { get; set; }
        public long        PatchedFileSize  { get; set; }
        public TimeSpan    Duration         { get; set; }

        public static PatchResult CreateSuccess(PatchFormat format, long originalSize, long patchedSize, TimeSpan duration)
            => new() { Success = true, Format = format, OriginalFileSize = originalSize, PatchedFileSize = patchedSize, Duration = duration };

        public static PatchResult CreateFailure(PatchFormat format, string errorMessage)
            => new() { Success = false, Format = format, ErrorMessage = errorMessage };

        public override string ToString() => Success
            ? $"{Format} patch applied: {OriginalFileSize} → {PatchedFileSize} bytes ({Duration.TotalMilliseconds:F2} ms)"
            : $"{Format} patch failed: {ErrorMessage}";
    }
}
