//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

namespace WpfHexaEditor.Interfaces
{
    /// <summary>
    /// Interface for formatting field values for display
    /// Implementations can format values as hex, decimal, string, binary, etc.
    /// </summary>
    public interface IFieldValueFormatter
    {
        /// <summary>
        /// Format a raw value into a display string
        /// </summary>
        /// <param name="value">Raw value (byte, ushort, uint, int, string, etc.)</param>
        /// <param name="valueType">Type of the value (uint8, uint16, uint32, string, etc.)</param>
        /// <param name="length">Length in bytes of the original data</param>
        /// <returns>Formatted string for display</returns>
        string Format(object value, string valueType, int length);

        /// <summary>
        /// Get the display name of this formatter
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Whether this formatter supports the given value type
        /// </summary>
        bool Supports(string valueType);
    }
}
