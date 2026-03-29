// ==========================================================
// Project: WpfHexEditor.Core
// File: ConflictSeverity.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Enumeration defining severity levels for TBL file entry conflicts.
//     Used by the TBL validation system to classify issues ranging from
//     informational notices to critical errors.
//
// Architecture Notes:
//     Pure enum — no dependencies. Consumed by TBLStream conflict detection
//     and TblStatistics reporting. Pairs with ConflictType for full context.
//
// ==========================================================

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
