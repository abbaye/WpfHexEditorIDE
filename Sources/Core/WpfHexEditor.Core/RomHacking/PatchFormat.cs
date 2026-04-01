// ==========================================================
// Project: WpfHexEditor.Core
// File: PatchFormat.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     Enumeration of supported binary patch formats: IPS, BPS, and XDelta (VCDIFF).
//     Used across the ROM hacking module to identify the format of a patch file
//     and to select the appropriate patcher implementation.
//
// Architecture Notes:
//     Pure enum — no WPF dependencies. Carried as a discriminator in PatchResult.
//
// ==========================================================

namespace WpfHexEditor.Core.RomHacking
{
    /// <summary>
    /// Supported binary patch formats.
    /// </summary>
    public enum PatchFormat
    {
        /// <summary>
        /// IPS — International Patching System (offset + data records, 16 MB limit)
        /// </summary>
        IPS,

        /// <summary>
        /// BPS — Beat Patch System by Near/byuu (VLQ actions, CRC32 verification)
        /// </summary>
        BPS,

        /// <summary>
        /// xdelta — VCDIFF RFC 3284 delta encoding (no size limit, better compression)
        /// </summary>
        XDelta
    }
}
