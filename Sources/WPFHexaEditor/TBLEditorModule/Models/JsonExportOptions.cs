//////////////////////////////////////////////
// Apache 2.0  - 2016-2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

namespace WpfHexaEditor.TBLEditorModule.Models
{
    /// <summary>
    /// Options for JSON export
    /// </summary>
    public class JsonExportOptions
    {
        /// <summary>
        /// Include Type property in JSON output
        /// </summary>
        public bool IncludeType { get; set; } = true;

        /// <summary>
        /// Include ByteCount property in JSON output
        /// </summary>
        public bool IncludeByteCount { get; set; } = true;

        /// <summary>
        /// Include Comment property in JSON output (if available)
        /// </summary>
        public bool IncludeComment { get; set; } = true;

        /// <summary>
        /// Use indented formatting (pretty print)
        /// </summary>
        public bool Indented { get; set; } = true;

        /// <summary>
        /// Property name for hex value
        /// </summary>
        public string HexPropertyName { get; set; } = "hex";

        /// <summary>
        /// Property name for character value
        /// </summary>
        public string ValuePropertyName { get; set; } = "value";

        /// <summary>
        /// Wrap entries in an object with metadata
        /// </summary>
        public bool IncludeMetadata { get; set; } = false;

        /// <summary>
        /// Metadata to include if IncludeMetadata is true
        /// </summary>
        public JsonMetadata Metadata { get; set; }
    }

    /// <summary>
    /// Metadata for JSON export
    /// </summary>
    public class JsonMetadata
    {
        /// <summary>
        /// Format version (e.g., "1.0")
        /// </summary>
        public string Version { get; set; } = "1.0";

        /// <summary>
        /// Description of the table
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Author name
        /// </summary>
        public string Author { get; set; }

        /// <summary>
        /// Creation/export date
        /// </summary>
        public string CreatedDate { get; set; }
    }
}
