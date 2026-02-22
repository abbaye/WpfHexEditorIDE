//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using WpfHexaEditor.Interfaces;

namespace WpfHexaEditor.Formatters
{
    /// <summary>
    /// Formats field values as decimal numbers
    /// </summary>
    public class DecimalValueFormatter : IFieldValueFormatter
    {
        public string DisplayName => "Decimal";

        public bool Supports(string valueType)
        {
            // Supports all numeric types
            return valueType == "uint8" || valueType == "uint16" || valueType == "uint32" ||
                   valueType == "int8" || valueType == "int16" || valueType == "int32" ||
                   valueType == "uint64" || valueType == "int64" ||
                   valueType == "float" || valueType == "double";
        }

        public string Format(object value, string valueType, int length)
        {
            if (value == null)
                return "null";

            return valueType switch
            {
                "uint8" => Convert.ToByte(value).ToString(),
                "int8" => Convert.ToSByte(value).ToString(),
                "uint16" => Convert.ToUInt16(value).ToString(),
                "int16" => Convert.ToInt16(value).ToString(),
                "uint32" => Convert.ToUInt32(value).ToString(),
                "int32" => Convert.ToInt32(value).ToString(),
                "uint64" => Convert.ToUInt64(value).ToString(),
                "int64" => Convert.ToInt64(value).ToString(),
                "float" when value is float f => f.ToString("G9"),  // 9 significant digits for float
                "double" when value is double d => d.ToString("G17"), // 17 significant digits for double
                _ => value.ToString()
            };
        }
    }
}
