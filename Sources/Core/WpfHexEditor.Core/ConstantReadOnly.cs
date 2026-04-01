// ==========================================================
// Project: WpfHexEditor.Core
// File: ConstantReadOnly.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Centralizes read-only string format constants and default value strings
//     used throughout the hex editor for hex display, offset formatting,
//     and default placeholder values.
//
// Architecture Notes:
//     Static class with readonly string fields — no instantiation, no WPF
//     dependencies. Single source of truth for all hex format strings across
//     Core, HexEditor rendering, and UI layers.
//
// ==========================================================

namespace WpfHexEditor.Core
{
    public static class ConstantReadOnly
    {
        public static readonly string HexLineInfoStringFormat = "x8";
        public static readonly string Hex2StringFormat = "x2";
        public static readonly string HexStringFormat = "x";
        public static readonly string DefaultHex8String = "0x00000000";
        public static readonly string DefaultHex2String = "0x00";

        public const long Largefilelength = 52_428_800L; //50 MB
        public const int Copyblocksize = 131_072; //128 KB
        public const int Findblocksize = 1_048_576; //1 MB
    }
}
