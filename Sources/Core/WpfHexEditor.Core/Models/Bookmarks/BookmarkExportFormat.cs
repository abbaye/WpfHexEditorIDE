//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Models.Bookmarks
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
