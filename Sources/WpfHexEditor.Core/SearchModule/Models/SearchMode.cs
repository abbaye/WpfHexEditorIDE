/*
    Apache 2.0  2026
    Author : Derek Tremblay (derektremblay666@gmail.com)
    Contributors: Claude Sonnet 4.5
*/

namespace WpfHexaEditor.SearchModule.Models
{
    /// <summary>
    /// Defines the available search modes in the hex editor.
    /// </summary>
    public enum SearchMode
    {
        /// <summary>
        /// Text search with encoding support (UTF-8, UTF-16, ASCII, etc.)
        /// </summary>
        Text,

        /// <summary>
        /// Hexadecimal byte pattern search
        /// </summary>
        Hex,

        /// <summary>
        /// Wildcard search (hex with ?? placeholder)
        /// </summary>
        Wildcard,

        /// <summary>
        /// TBL (Character Table) text search for ROM editing
        /// </summary>
        TblText,

        /// <summary>
        /// Relative search for automatic encoding discovery
        /// </summary>
        Relative
    }
}
