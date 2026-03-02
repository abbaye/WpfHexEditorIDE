//////////////////////////////////////////////
// Apache 2.0  - 2003-2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.CharacterTable
{
    /// <summary>
    /// Types of conflicts that can occur in TBL files
    /// </summary>
    public enum ConflictType
    {
        /// <summary>
        /// No conflict
        /// </summary>
        None = 0,

        /// <summary>
        /// One entry is a prefix of another (e.g., "88" and "8899")
        /// This can cause greedy matching issues
        /// </summary>
        PrefixConflict = 1,

        /// <summary>
        /// Exact duplicate entry (same hex key appears twice)
        /// </summary>
        Duplicate = 2,

        /// <summary>
        /// Entry can never match due to greedy matching algorithm
        /// </summary>
        Unreachable = 3,

        /// <summary>
        /// Multiple valid parses possible for a byte sequence
        /// </summary>
        Ambiguous = 4
    }
}
