// ==========================================================
// Project: WpfHexEditor.Core
// File: TblFileFormat.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Enumeration of supported TBL file formats used by the character table
//     system, distinguishing between standard Thingy format and extended
//     WpfHexEditor-specific format variants.
//
// Architecture Notes:
//     Pure enum — no dependencies. Used by TBLStream to determine parsing
//     strategy and file save format selection.
//
// ==========================================================

namespace WpfHexEditor.Core.CharacterTable
{
    /// <summary>
    /// Supported TBL file formats
    /// </summary>
    public enum TblFileFormat
    {
        /// <summary>
        /// Standard .tbl format (Thingy)
        /// </summary>
        Tbl,

        /// <summary>
        /// Extended .tblx format with JSON metadata
        /// </summary>
        Tblx,

        /// <summary>
        /// CSV spreadsheet format
        /// </summary>
        Csv,

        /// <summary>
        /// JSON format
        /// </summary>
        Json,

        /// <summary>
        /// Atlas assembler table format (ROM hacking — SNES/GBA).
        /// Same structure as Thingy TBL but hex keys may be prefixed with '$'.
        /// Example: $1A=A  or  $FFFE=\n
        /// </summary>
        Atlas
    }
}
