//////////////////////////////////////////////
// Apache 2.0  - 2003-2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

namespace WpfHexEditor.Core.CharacterTable
{
    /// <summary>
    /// Severity levels for TBL conflicts
    /// </summary>
    public enum ConflictSeverity
    {
        /// <summary>
        /// Informational only (e.g., single-byte followed by multi-byte is normal)
        /// </summary>
        Info = 0,

        /// <summary>
        /// Warning - may cause issues but not critical
        /// </summary>
        Warning = 1,

        /// <summary>
        /// Error - critical issue that should be resolved
        /// </summary>
        Error = 2
    }
}
