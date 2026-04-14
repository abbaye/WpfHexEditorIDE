// ==========================================================
// Project: WpfHexEditor.Core
// File: Enum.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Defines the DteType enumeration classifying the type of each DTE entry
//     in a TBL character table (ASCII, Japonais, DualTitleEncoding,
//     MultipleTitleEncoding, EndLine, EndBlock, Invalid).
//
// Architecture Notes:
//     Pure enum — no dependencies. Used by Dte class to classify entries and
//     by TBLStream for lookup and conflict detection logic.
//
// ==========================================================

namespace WpfHexEditor.Core.CharacterTable
{
    /// <summary>
    /// Type of DTE used in TBL
    /// </summary>
    public enum DteType
    {
        Invalid = -1,
        Ascii = 0,
        Japonais,
        DualTitleEncoding,
        MultipleTitleEncoding,
        EndLine,
        EndBlock
    }

    public enum DefaultCharacterTableType
    {
        Ascii,
        EbcdicWithSpecialChar,
        EbcdicNoSpecialChar
        //MACINTOSH
        //DOS/IBM-ASCII
    }
}
