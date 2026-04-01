//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Models
{
    /// <summary>
    /// Edit mode for HexEditor (V2 architecture)
    /// </summary>
    public enum EditMode
    {
        /// <summary>
        /// Overwrite existing bytes (default mode)
        /// </summary>
        Overwrite,

        /// <summary>
        /// Insert new bytes, shifting existing content
        /// </summary>
        Insert
    }
}
