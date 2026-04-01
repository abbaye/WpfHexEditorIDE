// ==========================================================
// Project: WpfHexEditor.Core
// File: ConflictType.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Enumeration classifying the types of conflicts that can occur in TBL files,
//     such as prefix ambiguity (one entry being a prefix of another) or duplicate
//     hex keys mapping to different characters.
//
// Architecture Notes:
//     Pure enum — no dependencies. Consumed by TBLStream conflict detection
//     and TblStatistics reporting. Pairs with ConflictSeverity for full context.
//
// ==========================================================

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
