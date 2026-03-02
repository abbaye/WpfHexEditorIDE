//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.BinaryAnalysis.Models.ExportImport
{
    /// <summary>
    /// Intel HEX record types
    /// </summary>
    public enum IntelHexRecordType : byte
    {
        /// <summary>
        /// Data record (00)
        /// </summary>
        Data = 0x00,

        /// <summary>
        /// End of file record (01)
        /// </summary>
        EndOfFile = 0x01,

        /// <summary>
        /// Extended segment address record (02)
        /// </summary>
        ExtendedSegmentAddress = 0x02,

        /// <summary>
        /// Start segment address record (03)
        /// </summary>
        StartSegmentAddress = 0x03,

        /// <summary>
        /// Extended linear address record (04)
        /// </summary>
        ExtendedLinearAddress = 0x04,

        /// <summary>
        /// Start linear address record (05)
        /// </summary>
        StartLinearAddress = 0x05
    }
}
