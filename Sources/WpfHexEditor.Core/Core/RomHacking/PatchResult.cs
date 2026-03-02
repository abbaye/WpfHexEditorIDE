//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

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
