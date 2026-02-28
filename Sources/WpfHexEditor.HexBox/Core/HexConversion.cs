//////////////////////////////////////////////
// Apache 2.0  - 2016-2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
//////////////////////////////////////////////

using System.Globalization;

namespace WpfHexEditor.HexBox.Core
{
    /// <summary>
    /// Minimal hex conversion utilities for the HexBox control.
    /// Extracted subset of WpfHexaEditor.Core.Bytes.ByteConverters.
    /// </summary>
    internal static class HexConversion
    {
        /// <summary>
        /// Convert a long value to its hexadecimal string representation.
        /// </summary>
        public static string LongToHex(long val) =>
            val.ToString("X", CultureInfo.InvariantCulture);

        /// <summary>
        /// Parse a hex string (with or without "0x" prefix) to a long value.
        /// Returns (true, value) on success, (false, -1) on error.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0054:Utiliser une attribution composée", Justification = "<En attente>")]
        public static (bool success, long position) HexLiteralToLong(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return (false, -1);

            var i = hex.Length > 1 && hex[0] == '0' && (hex[1] == 'x' || hex[1] == 'X')
                ? 2
                : 0;

            long value = 0;

            while (i < hex.Length)
            {
                int x = hex[i++];

                if (x >= '0' && x <= '9') x = x - '0';
                else if (x >= 'A' && x <= 'F') x = x - 'A' + 10;
                else if (x >= 'a' && x <= 'f') x = x - 'a' + 10;
                else return (false, -1);

                value = 16 * value + x;
            }

            return (true, value);
        }
    }
}
