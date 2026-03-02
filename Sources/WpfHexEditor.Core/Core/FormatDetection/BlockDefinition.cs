//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.Generic;

namespace WpfHexEditor.Core.FormatDetection
{
    /// <summary>
    /// Represents a single block definition in a format
    /// Can be a simple field, signature, conditional, loop, or action
    /// </summary>
    public class BlockDefinition
    {
        /// <summary>
        /// Type of block
        /// Supported: "signature", "field", "conditional", "loop", "action"
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Name/description of this block (displayed to user)
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Offset where this block starts
        /// Can be:
        /// - int: Absolute offset (e.g., 0, 4, 16)
        /// - "var:name": Variable reference (e.g., "var:currentOffset")
        /// - "calc:expression": Calculated expression (e.g., "calc:currentOffset + 46")
        /// </summary>
        public object Offset { get; set; }

        /// <summary>
        /// Length of this block in bytes
        /// Can be:
        /// - int: Fixed length (e.g., 4, 16, 256)
        /// - "var:name": Variable reference
        /// - "calc:expression": Calculated expression
        /// </summary>
        public object Length { get; set; }

        /// <summary>
        /// Background color for this block (hex format: "#RRGGBB")
        /// </summary>
        public string Color { get; set; }

        /// <summary>
        /// Opacity of the background (0.0 to 1.0, default: 0.3)
        /// </summary>
        public double Opacity { get; set; } = 0.3;

        /// <summary>
        /// Description of this block (shown in UI)
        /// Supports placeholders like $iteration$ for loop blocks
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Value type for reading data from this field
        /// Supported: "uint8", "uint16", "uint32", "int16", "int32", "string"
        /// </summary>
        public string ValueType { get; set; }

        /// <summary>
        /// Variable name to store this field's value as (e.g., "width", "height", "fileCount")
        /// Allows referencing the value in later calculations
        /// </summary>
        public string StoreAs { get; set; }

        /// <summary>
        /// If true, this field is parsed but not shown in the UI
        /// Useful for metadata fields used only for calculations
        /// </summary>
        public bool? Hidden { get; set; }

        /// <summary>
        /// Validation rules for this field
        /// </summary>
        public FieldValidationRules ValidationRules { get; set; }

        /// <summary>
        /// Endianness for this specific field ("little" or "big")
        /// If null, uses the format-level default endianness
        /// </summary>
        public string Endianness { get; set; }

        /// <summary>
        /// Bitfield definitions for extracting sub-values from packed bytes.
        /// Each bitfield extracts a range of bits from the field's raw value.
        /// Example: fscod (bits 7-6) + frmsizecod (bits 5-0) from a single byte.
        /// </summary>
        public List<BitfieldDefinition> Bitfields { get; set; }

        /// <summary>
        /// Lookup table mapping raw integer values to human-readable names.
        /// Keys are string representations of integer values.
        /// Example: { "0": "Grayscale", "2": "RGB", "6": "RGBA" }
        /// </summary>
        public Dictionary<string, string> ValueMap { get; set; }

        /// <summary>
        /// Variable name to store the mapped/translated value from ValueMap.
        /// Example: "colorTypeName" stores "RGB" when colorType=2
        /// </summary>
        public string MappedValueStoreAs { get; set; }

        #region Conditional Block Properties

        /// <summary>
        /// Condition for conditional blocks
        /// </summary>
        public ConditionDefinition Condition { get; set; }

        /// <summary>
        /// Blocks to execute if condition is true (conditional blocks)
        /// </summary>
        public List<BlockDefinition> Then { get; set; }

        /// <summary>
        /// Blocks to execute if condition is false (optional)
        /// </summary>
        public List<BlockDefinition> Else { get; set; }

        #endregion

        #region Loop Block Properties

        /// <summary>
        /// Number of iterations for loop blocks
        /// Can be an integer or "var:name" or "calc:expression"
        /// </summary>
        public object Count { get; set; }

        /// <summary>
        /// Maximum iterations for loop blocks (safety limit)
        /// </summary>
        public int MaxIterations { get; set; } = 1000;

        /// <summary>
        /// Body of the loop (blocks to execute each iteration)
        /// </summary>
        public List<BlockDefinition> Body { get; set; }

        #endregion

        #region ComputeFromVariables Block Properties

        /// <summary>
        /// Expression to evaluate for computeFromVariables blocks
        /// Example: "uncompressedSize > 0 ? ((1 - compressedSize / uncompressedSize) * 100) : 0"
        /// </summary>
        public string Expression { get; set; }

        #endregion

        #region Action Block Properties

        /// <summary>
        /// Action to perform (for action blocks)
        /// Supported: "increment", "decrement", "setVariable"
        /// </summary>
        public string Action { get; set; }

        /// <summary>
        /// Variable name for action (e.g., "fileCount", "currentOffset")
        /// </summary>
        public string Variable { get; set; }

        /// <summary>
        /// Value for setVariable action
        /// Can be a literal or "calc:expression"
        /// </summary>
        public object Value { get; set; }

        #endregion

        /// <summary>
        /// Validate this block definition
        /// </summary>
        public bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(Type))
                return false;

            switch (Type.ToLowerInvariant())
            {
                case "signature":
                case "field":
                    // Must have offset, length, and color
                    return Offset != null && Length != null && !string.IsNullOrWhiteSpace(Color);

                case "metadata":
                    // Metadata blocks just need a name and variable
                    return !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(Variable);

                case "conditional":
                    // Must have condition and then blocks
                    return Condition != null && Then != null && Then.Count > 0;

                case "loop":
                    // Must have condition, body, and reasonable max iterations
                    return Condition != null && Body != null && Body.Count > 0 && MaxIterations > 0 && MaxIterations <= 100000;

                case "action":
                    // Must have action and variable
                    return !string.IsNullOrWhiteSpace(Action) && !string.IsNullOrWhiteSpace(Variable);

                case "computefromvariables":
                    // Must have expression and storeAs
                    return !string.IsNullOrWhiteSpace(Expression) && !string.IsNullOrWhiteSpace(StoreAs);

                default:
                    return false;
            }
        }

        /// <summary>
        /// Get offset as long value (resolves variables if needed)
        /// </summary>
        public long? GetOffsetValue(Dictionary<string, object> variables)
        {
            if (Offset == null)
                return null;

            // Direct integer
            if (Offset is int intOffset)
                return intOffset;

            if (Offset is long longOffset)
                return longOffset;

            // String-based (variable or expression)
            if (Offset is string strOffset)
            {
                if (strOffset.StartsWith("var:"))
                {
                    var varName = strOffset.Substring(4);
                    if (variables.TryGetValue(varName, out var value))
                    {
                        return System.Convert.ToInt64(value);
                    }
                }
                else if (strOffset.StartsWith("calc:"))
                {
                    // Expression evaluation handled by interpreter
                    return null; // Defer to interpreter
                }
                else
                {
                    // Try parse as number
                    if (long.TryParse(strOffset, out var parsed))
                        return parsed;
                }
            }

            return null;
        }

        /// <summary>
        /// Get length as integer value (resolves variables if needed)
        /// </summary>
        public int? GetLengthValue(Dictionary<string, object> variables)
        {
            if (Length == null)
                return null;

            // Direct integer
            if (Length is int intLength)
                return intLength;

            if (Length is long longLength)
                return (int)longLength;

            // String-based (variable or expression)
            if (Length is string strLength)
            {
                if (strLength.StartsWith("var:"))
                {
                    var varName = strLength.Substring(4);
                    if (variables.TryGetValue(varName, out var value))
                    {
                        return System.Convert.ToInt32(value);
                    }
                }
                else if (strLength.StartsWith("calc:"))
                {
                    // Expression evaluation handled by interpreter
                    return null; // Defer to interpreter
                }
                else
                {
                    // Try parse as number
                    if (int.TryParse(strLength, out var parsed))
                        return parsed;
                }
            }

            return null;
        }

        public override string ToString()
        {
            return $"{Type}: {Name} @ {Offset} (len: {Length})";
        }
    }

    /// <summary>
    /// Defines a bitfield extraction from a packed byte field.
    /// Extracts a range of bits and stores the result as a separate variable.
    /// </summary>
    public class BitfieldDefinition
    {
        /// <summary>
        /// Display name for this bitfield (e.g., "Sample Rate Code")
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Bit range specification: "7-6" for bits 7 down to 6, or "3" for a single bit.
        /// Bit 0 is the least significant bit.
        /// </summary>
        public string Bits { get; set; }

        /// <summary>
        /// Variable name to store the extracted value
        /// </summary>
        public string StoreAs { get; set; }

        /// <summary>
        /// Human-readable description of this bitfield
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Optional lookup table mapping extracted values to human-readable names.
        /// Example: { "0": "48 kHz", "1": "44.1 kHz", "2": "32 kHz" }
        /// </summary>
        public Dictionary<string, string> ValueMap { get; set; }

        /// <summary>
        /// Parse the Bits string into (highBit, lowBit) tuple.
        /// "7-6" → (7, 6), "3" → (3, 3)
        /// </summary>
        public (int high, int low) ParseBitRange()
        {
            if (string.IsNullOrWhiteSpace(Bits))
                return (0, 0);

            if (Bits.Contains("-"))
            {
                var parts = Bits.Split('-');
                if (parts.Length == 2 && int.TryParse(parts[0], out int h) && int.TryParse(parts[1], out int l))
                    return (h, l);
            }

            if (int.TryParse(Bits, out int single))
                return (single, single);

            return (0, 0);
        }

        /// <summary>
        /// Extract the bitfield value from a raw integer.
        /// Shifts right by lowBit and masks to the field width.
        /// </summary>
        public long ExtractValue(long rawValue)
        {
            var (high, low) = ParseBitRange();
            int width = high - low + 1;
            if (width <= 0 || width > 63)
                return 0;
            long mask = (1L << width) - 1;
            return (rawValue >> low) & mask;
        }
    }
}
