// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: CodeLensSymbolKinds.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-18
// Description:
//     Flags enum for filtering which symbol kinds display
//     inline CodeLens reference-count hints in the CodeEditor.
//     Shared between WpfHexEditor.Editor.CodeEditor (rendering)
//     and WpfHexEditor.Options (settings page).
//
// Architecture Notes:
//     Pattern: Value Object / Flags Enum.
//     Stored as int in AppSettings (JSON-safe, no cross-project enum dep).
//     Cast to this type only in layers that reference Editor.Core.
//     All = 4095 = (1 << 12) - 1 — covers all 12 defined kinds.
// ==========================================================

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Bitmask controlling which symbol kinds show CodeLens reference-count hints.
/// All kinds are enabled by default (<see cref="All"/> = 4095).
/// </summary>
[Flags]
public enum CodeLensSymbolKinds
{
    /// <summary>No hints displayed.</summary>
    None        = 0,

    // ── Type kinds ─────────────────────────────────────────────────────────

    /// <summary>Class declarations.</summary>
    Class       = 1 << 0,   //    1

    /// <summary>Interface declarations.</summary>
    Interface   = 1 << 1,   //    2

    /// <summary>Struct declarations.</summary>
    Struct      = 1 << 2,   //    4

    /// <summary>Enum declarations.</summary>
    Enum        = 1 << 3,   //    8

    /// <summary>Record declarations.</summary>
    Record      = 1 << 4,   //   16

    /// <summary>Delegate declarations.</summary>
    Delegate    = 1 << 5,   //   32

    // ── Member kinds ───────────────────────────────────────────────────────

    /// <summary>Method declarations.</summary>
    Method      = 1 << 6,   //   64

    /// <summary>Constructor declarations.</summary>
    Constructor = 1 << 7,   //  128

    /// <summary>Property declarations.</summary>
    Property    = 1 << 8,   //  256

    /// <summary>Indexer declarations.</summary>
    Indexer     = 1 << 9,   //  512

    /// <summary>Field declarations.</summary>
    Field       = 1 << 10,  // 1024

    /// <summary>Event declarations.</summary>
    Event       = 1 << 11,  // 2048

    // ── Aggregate ──────────────────────────────────────────────────────────

    /// <summary>All 12 symbol kinds — default value when no filter is applied.</summary>
    All         = (1 << 12) - 1,   // 4095
}
