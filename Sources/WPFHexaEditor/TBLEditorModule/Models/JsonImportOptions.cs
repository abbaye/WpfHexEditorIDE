//////////////////////////////////////////////
// Apache 2.0  - 2016-2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

namespace WpfHexaEditor.TBLEditorModule.Models
{
    /// <summary>
    /// Options for JSON import
    /// </summary>
    public class JsonImportOptions
    {
        /// <summary>
        /// Auto-detect DteType from hex length
        /// </summary>
        public bool AutoDetectType { get; set; } = true;

        /// <summary>
        /// Skip invalid entries instead of failing
        /// </summary>
        public bool SkipInvalidEntries { get; set; } = true;

        /// <summary>
        /// Property name for hex value (default: "hex" or "entry")
        /// </summary>
        public string HexPropertyName { get; set; } = "hex";

        /// <summary>
        /// Property name for character value (default: "value")
        /// </summary>
        public string ValuePropertyName { get; set; } = "value";
    }
}
