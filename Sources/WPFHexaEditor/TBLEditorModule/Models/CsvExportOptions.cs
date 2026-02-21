//////////////////////////////////////////////
// Apache 2.0  - 2016-2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System.Text;

namespace WpfHexaEditor.TBLEditorModule.Models
{
    /// <summary>
    /// Options for CSV export
    /// </summary>
    public class CsvExportOptions
    {
        /// <summary>
        /// Include Type column
        /// </summary>
        public bool IncludeType { get; set; } = true;

        /// <summary>
        /// Include ByteCount column
        /// </summary>
        public bool IncludeByteCount { get; set; } = true;

        /// <summary>
        /// Include Comment column
        /// </summary>
        public bool IncludeComment { get; set; } = true;

        /// <summary>
        /// CSV delimiter
        /// </summary>
        public string Delimiter { get; set; } = ",";

        /// <summary>
        /// Quote strings
        /// </summary>
        public bool QuoteStrings { get; set; } = true;

        /// <summary>
        /// Output encoding
        /// </summary>
        public Encoding Encoding { get; set; } = Encoding.UTF8;
    }
}
