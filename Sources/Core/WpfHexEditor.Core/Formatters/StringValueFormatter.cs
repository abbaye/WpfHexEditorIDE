//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Linq;
using System.Text;
using WpfHexEditor.Core.Interfaces;

namespace WpfHexEditor.Core.Formatters
{
    /// <summary>
    /// Formats field values as strings with proper encoding
    /// </summary>
    public class StringValueFormatter : IFieldValueFormatter
    {
        public string DisplayName => "String";

        public bool Supports(string valueType)
        {
            return valueType == "string" || valueType == "ascii" || valueType == "utf8" || valueType == "utf16";
        }

        public string Format(object value, string valueType, int length)
        {
            if (value == null)
                return "null";

            if (value is string str)
                return EscapeString(str);

            if (value is byte[] bytes)
            {
                // Try to decode as string based on type
                var encoding = GetEncoding(valueType);
                try
                {
                    var decoded = encoding.GetString(bytes);
                    // Remove null terminators
                    decoded = decoded.TrimEnd('\0');
                    return EscapeString(decoded);
                }
                catch
                {
                    // If decoding fails, show as hex
                    return $"<invalid {valueType}>";
                }
            }

            return value.ToString();
        }

        private Encoding GetEncoding(string valueType)
        {
            return valueType switch
            {
                "ascii" => Encoding.ASCII,
                "utf8" or "string" => Encoding.UTF8,
                "utf16" => Encoding.Unicode,
                _ => Encoding.UTF8
            };
        }

        private string EscapeString(string str)
        {
            if (string.IsNullOrEmpty(str))
                return "\"\"";

            // Escape special characters
            var escaped = str
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");

            // Replace non-printable characters with escapes
            var sb = new StringBuilder();
            foreach (var c in escaped)
            {
                if (char.IsControl(c) && c != '\0')
                    sb.Append($"\\x{(int)c:X2}");
                else if (c >= 32 && c < 127)
                    sb.Append(c);
                else if (c != '\0')
                    sb.Append($"\\u{(int)c:X4}");
            }

            return $"\"{sb}\"";
        }
    }
}
