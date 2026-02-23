//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Linq;
using WpfHexaEditor.Interfaces;

namespace WpfHexaEditor.Formatters
{
    /// <summary>
    /// Formats field values using the most appropriate format for each type
    /// - Bytes/Arrays: Hex with preview
    /// - Integers: Decimal with hex in parentheses
    /// - Floats: Decimal notation
    /// - Strings: Text with length info
    /// </summary>
    public class MixedValueFormatter : IFieldValueFormatter
    {
        public string DisplayName => "Mixed (Smart)";

        public bool Supports(string valueType)
        {
            // Supports all types
            return true;
        }

        public string Format(object value, string valueType, int length)
        {
            if (value == null)
                return "null";

            return valueType?.ToLowerInvariant() switch
            {
                // Integers: Decimal (0xHex)
                "uint8" or "byte" => FormatInteger(Convert.ToByte(value)),
                "int8" or "sbyte" => FormatInteger(Convert.ToSByte(value)),
                "uint16" or "ushort" => FormatInteger(Convert.ToUInt16(value)),
                "int16" or "short" => FormatInteger(Convert.ToInt16(value)),
                "uint32" or "uint" => FormatInteger(Convert.ToUInt32(value)),
                "int32" or "int" => FormatInteger(Convert.ToInt32(value)),
                "uint64" or "ulong" => FormatInteger(Convert.ToUInt64(value)),
                "int64" or "long" => FormatInteger(Convert.ToInt64(value)),

                // Floats: Decimal with precision
                "float" when value is float f => FormatFloat(f),
                "double" when value is double d => FormatDouble(d),

                // Strings: Text with length
                "string" or "ascii" or "utf8" or "utf16" => FormatString(value.ToString(), length),

                // Bytes: Hex with ASCII preview
                "bytes" when value is byte[] bytes => FormatByteArray(bytes),

                // Unknown: hex representation
                _ => $"0x{value:X}"
            };
        }

        private string FormatInteger(byte value)
        {
            // Add ASCII character if printable
            if (value >= 32 && value <= 126)
            {
                var chr = (char)value;
                // Escape special display characters
                var charDisplay = chr switch
                {
                    '"' => "\\\"",
                    '\\' => "\\\\",
                    _ => chr.ToString()
                };
                return $"{value} (0x{value:X2}, '{charDisplay}')";
            }

            return $"{value} (0x{value:X2})";
        }

        private string FormatInteger(sbyte value)
        {
            return value >= 0
                ? $"{value} (0x{value:X2})"
                : $"{value} (0x{(byte)value:X2})";
        }

        private string FormatInteger(ushort value)
        {
            return $"{value} (0x{value:X4})";
        }

        private string FormatInteger(short value)
        {
            return value >= 0
                ? $"{value} (0x{value:X4})"
                : $"{value} (0x{(ushort)value:X4})";
        }

        private string FormatInteger(uint value)
        {
            return $"{value:N0} (0x{value:X8})";
        }

        private string FormatInteger(int value)
        {
            return value >= 0
                ? $"{value:N0} (0x{value:X8})"
                : $"{value:N0} (0x{(uint)value:X8})";
        }

        private string FormatInteger(ulong value)
        {
            return $"{value:N0} (0x{value:X16})";
        }

        private string FormatInteger(long value)
        {
            return value >= 0
                ? $"{value:N0} (0x{value:X16})"
                : $"{value:N0} (0x{(ulong)value:X16})";
        }

        private string FormatFloat(float value)
        {
            // Use G9 for 9 significant digits (max precision for float)
            if (float.IsNaN(value))
                return "NaN";
            if (float.IsInfinity(value))
                return value > 0 ? "+Infinity" : "-Infinity";

            return value.ToString("G9");
        }

        private string FormatDouble(double value)
        {
            // Use G17 for 17 significant digits (max precision for double)
            if (double.IsNaN(value))
                return "NaN";
            if (double.IsInfinity(value))
                return value > 0 ? "+Infinity" : "-Infinity";

            return value.ToString("G17");
        }

        private string FormatString(string text, int length)
        {
            if (string.IsNullOrEmpty(text))
                return "<empty>";

            // Show string with length indicator
            // Escape special characters for display
            var displayText = EscapeSpecialChars(text);

            // Truncate if too long
            if (displayText.Length > 60)
            {
                displayText = displayText.Substring(0, 57) + "...";
            }

            return $"\"{displayText}\" ({length} bytes)";
        }

        private string FormatByteArray(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return "[]";

            // For short arrays (≤ 8 bytes), show all bytes in hex
            if (bytes.Length <= 8)
            {
                var hex = string.Join(" ", bytes.Select(b => $"{b:X2}"));
                var ascii = GetAsciiPreview(bytes);

                // If all bytes are printable ASCII, emphasize the string interpretation
                if (IsAllPrintableAscii(bytes))
                {
                    return $"{ascii} [{hex}]";
                }

                return $"[{hex}] {ascii}";
            }

            // For longer arrays, show first 4 and last 4 bytes
            var firstFour = string.Join(" ", bytes.Take(4).Select(b => $"{b:X2}"));
            var lastFour = string.Join(" ", bytes.Skip(bytes.Length - 4).Select(b => $"{b:X2}"));
            var preview = GetAsciiPreview(bytes.Take(8).ToArray());

            return $"[{firstFour} ... {lastFour}] {preview} ({bytes.Length} bytes)";
        }

        private bool IsAllPrintableAscii(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return false;

            // Check if at least 75% of bytes are printable ASCII
            int printableCount = bytes.Count(b => b >= 32 && b <= 126);
            return printableCount >= bytes.Length * 0.75;
        }

        private string GetAsciiPreview(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return "";

            var preview = new System.Text.StringBuilder("\"");
            foreach (var b in bytes)
            {
                // Show printable ASCII, replace non-printable with '.'
                preview.Append(b >= 32 && b <= 126 ? (char)b : '.');
            }
            preview.Append('"');

            return preview.ToString();
        }

        private string EscapeSpecialChars(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            return text
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t")
                .Replace("\0", "\\0");
        }
    }
}
