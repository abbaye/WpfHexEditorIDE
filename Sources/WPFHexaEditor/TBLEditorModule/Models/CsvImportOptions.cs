//////////////////////////////////////////////
// Apache 2.0  - 2016-2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System.Text;

namespace WpfHexaEditor.TBLEditorModule.Models
{
    /// <summary>
    /// Options for CSV import
    /// </summary>
    public class CsvImportOptions
    {
        /// <summary>
        /// CSV delimiter character
        /// </summary>
        public string Delimiter { get; set; } = ",";

        /// <summary>
        /// Whether the CSV has a header row
        /// </summary>
        public bool HasHeader { get; set; } = true;

        /// <summary>
        /// Auto-detect DteType from hex length
        /// </summary>
        public bool AutoDetectType { get; set; } = true;

        /// <summary>
        /// Skip invalid rows instead of failing
        /// </summary>
        public bool SkipInvalidRows { get; set; } = true;

        /// <summary>
        /// Encoding for reading the CSV file
        /// </summary>
        public Encoding Encoding { get; set; } = Encoding.UTF8;
    }
}
