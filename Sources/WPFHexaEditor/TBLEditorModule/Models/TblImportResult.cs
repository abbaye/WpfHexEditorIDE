//////////////////////////////////////////////
// Apache 2.0  - 2016-2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System.Collections.Generic;
using WpfHexaEditor.Core.CharacterTable;

namespace WpfHexaEditor.TBLEditorModule.Models
{
    /// <summary>
    /// Result of a TBL import operation
    /// </summary>
    public class TblImportResult
    {
        /// <summary>
        /// Whether the import succeeded
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Imported entries
        /// </summary>
        public List<Dte> Entries { get; set; } = new List<Dte>();

        /// <summary>
        /// Error messages
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// Warning messages
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>
        /// Number of entries successfully imported
        /// </summary>
        public int ImportedCount { get; set; }

        /// <summary>
        /// Number of entries skipped (invalid)
        /// </summary>
        public int SkippedCount { get; set; }

        /// <summary>
        /// Detected file format
        /// </summary>
        public TblFileFormat DetectedFormat { get; set; }
    }
}
