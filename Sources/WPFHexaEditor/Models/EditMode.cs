//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

namespace WpfHexaEditor.Models
{
    /// <summary>
    /// Edit mode for HexEditorV2
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
