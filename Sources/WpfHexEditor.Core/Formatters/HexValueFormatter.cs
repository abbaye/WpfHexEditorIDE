//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Linq;
using WpfHexEditor.Core.Interfaces;

namespace WpfHexEditor.Core.Formatters
{
    /// <summary>
    /// Formats field values as hexadecimal strings
    /// </summary>
    public class HexValueFormatter : IFieldValueFormatter
    {
        public string DisplayName => "Hexadecimal";

        public bool Supports(string valueType)
        {
            // Supports all numeric types
            return valueType == "uint8" || valueType == "uint16" || valueType == "uint32" ||
                   valueType == "int8" || valueType == "int16" || valueType == "int32" ||
                   valueType == "uint64" || valueType == "int64" || valueType == "bytes" ||
                   valueType == "float" || valueType == "double";
        }

        public string Format(object value, string valueType, int length)
        {
            if (value == null)
                return "null";

            return valueType switch
            {
                "uint8" or "int8" => $"0x{Convert.ToByte(value):X2}",
                "uint16" or "int16" => $"0x{Convert.ToUInt16(value):X4}",
                "uint32" or "int32" => $"0x{Convert.ToUInt32(value):X8}",
                "uint64" or "int64" => $"0x{Convert.ToUInt64(value):X16}",
                "float" when value is float f => $"0x{BitConverter.ToUInt32(BitConverter.GetBytes(f), 0):X8} ({f})",
                "double" when value is double d => $"0x{BitConverter.ToUInt64(BitConverter.GetBytes(d), 0):X16} ({d})",
                "bytes" when value is byte[] bytes => FormatByteArray(bytes),
                _ => $"0x{value:X}"
            };
        }

        private string FormatByteArray(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return "[]";

            if (bytes.Length <= 8)
                return string.Join(" ", bytes.Select(b => $"{b:X2}"));

            // For longer arrays, show first 4 and last 4 bytes
            var first = string.Join(" ", bytes.Take(4).Select(b => $"{b:X2}"));
            var last = string.Join(" ", bytes.Skip(bytes.Length - 4).Select(b => $"{b:X2}"));
            return $"{first} ... {last} ({bytes.Length} bytes)";
        }
    }
}
