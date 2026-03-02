//////////////////////////////////////////////
// Apache 2.0  - 2016-2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

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
