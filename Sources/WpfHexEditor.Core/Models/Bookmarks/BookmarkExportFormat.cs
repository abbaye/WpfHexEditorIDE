//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

namespace WpfHexaEditor.Models.Bookmarks
{
    /// <summary>
    /// Export format options for bookmarks
    /// </summary>
    public enum BookmarkExportFormat
    {
        /// <summary>
        /// JSON format with full metadata
        /// </summary>
        Json,

        /// <summary>
        /// XML format compatible with StateService
        /// </summary>
        Xml,

        /// <summary>
        /// CSV format for spreadsheet import
        /// </summary>
        Csv
    }
}
