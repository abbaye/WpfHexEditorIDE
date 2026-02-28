//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

namespace WpfHexEditor.Core.CharacterTable
{
    /// <summary>
    /// Statistics about TBL file content with caching support
    /// </summary>
    public class TblStatistics
    {
        /// <summary>
        /// Total number of entries
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Count of ASCII entries (1 byte, single character)
        /// </summary>
        public int AsciiCount { get; set; }

        /// <summary>
        /// Count of DTE entries (Dual Title Encoding)
        /// </summary>
        public int DteCount { get; set; }

        /// <summary>
        /// Count of MTE entries (Multiple Title Encoding, 3+ bytes)
        /// </summary>
        public int MteCount { get; set; }

        /// <summary>
        /// Count of 2-byte entries
        /// </summary>
        public int Byte2Count { get; set; }

        /// <summary>
        /// Count of 3-byte entries
        /// </summary>
        public int Byte3Count { get; set; }

        /// <summary>
        /// Count of 4-byte entries
        /// </summary>
        public int Byte4Count { get; set; }

        /// <summary>
        /// Count of 5+ byte entries
        /// </summary>
        public int Byte5PlusCount { get; set; }

        /// <summary>
        /// Count of Japanese character entries
        /// </summary>
        public int JapaneseCount { get; set; }

        /// <summary>
        /// Count of EndBlock markers
        /// </summary>
        public int EndBlockCount { get; set; }

        /// <summary>
        /// Count of EndLine markers
        /// </summary>
        public int EndLineCount { get; set; }

        /// <summary>
        /// Coverage percentage (0-100) of single-byte space (0x00-0xFF)
        /// </summary>
        public double CoveragePercent { get; set; }

        /// <summary>
        /// Count of conflicts detected (duplicates, prefix conflicts, etc.)
        /// </summary>
        public int ConflictCount { get; set; }

        /// <summary>
        /// Get formatted statistics summary
        /// </summary>
        public override string ToString()
        {
            return $"Total: {TotalCount}, ASCII: {AsciiCount}, DTE: {DteCount}, MTE: {MteCount}, Coverage: {CoveragePercent:F1}%";
        }
    }
}
