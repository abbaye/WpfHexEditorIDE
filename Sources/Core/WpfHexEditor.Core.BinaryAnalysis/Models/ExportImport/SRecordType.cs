//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.BinaryAnalysis.Models.ExportImport
{
    /// <summary>
    /// Motorola S-Record types
    /// </summary>
    public enum SRecordType
    {
        /// <summary>
        /// Header record (S0)
        /// </summary>
        S0_Header = 0,

        /// <summary>
        /// Data record with 16-bit address (S1)
        /// </summary>
        S1_Data16 = 1,

        /// <summary>
        /// Data record with 24-bit address (S2)
        /// </summary>
        S2_Data24 = 2,

        /// <summary>
        /// Data record with 32-bit address (S3)
        /// </summary>
        S3_Data32 = 3,

        /// <summary>
        /// Reserved (S4)
        /// </summary>
        S4_Reserved = 4,

        /// <summary>
        /// Count record with 16-bit address (S5)
        /// </summary>
        S5_Count16 = 5,

        /// <summary>
        /// Count record with 24-bit address (S6)
        /// </summary>
        S6_Count24 = 6,

        /// <summary>
        /// Start address record with 32-bit address (S7)
        /// </summary>
        S7_Start32 = 7,

        /// <summary>
        /// Start address record with 24-bit address (S8)
        /// </summary>
        S8_Start24 = 8,

        /// <summary>
        /// Start address record with 16-bit address (S9)
        /// </summary>
        S9_Start16 = 9
    }
}
