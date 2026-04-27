// ==========================================================
// Project: WpfHexEditor.Core
// File: BlockDefinition.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Updated: 2026-03-25 — whfmt v2.0: repeating/union/nested/pointer blocks,
//     offsetFrom+offsetAdd pattern, colorCycle, trueLabel/falseLabel,
//     all new block types added to IsValid().
// Description:
//     Represents a single block definition within a .whfmt format file.
//     Supports: signature, field, metadata, conditional, loop, action,
//     computeFromVariables, repeating, union, nested, pointer, group (v2.3),
//     header, data (v2.4).
//
// Architecture Notes:
//     Data model parsed from JSON format definitions. Used by FormatScriptInterpreter
//     to drive format detection and field parsing. No WPF dependencies.
//
// ==========================================================

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
        /// Offset where this block starts.
        /// Can be: int | "var:name" | "calc:expression"
        /// If OffsetFrom is set, this field is ignored — use OffsetFrom + OffsetAdd instead.
        /// </summary>
        public object Offset { get; set; }

        /// <summary>
        /// Variable name whose value is used as the base offset.
        /// Used with OffsetAdd: effectiveOffset = variables[OffsetFrom] + OffsetAdd.
        /// Example: "peHeaderOffset" → reads offset from that variable.
        /// </summary>
        public string OffsetFrom { get; set; }

        /// <summary>
        /// Additional bytes added to the OffsetFrom base.
        /// Can be: int | "calc:expression". Defaults to 0.
        /// </summary>
        public object OffsetAdd { get; set; }

        /// <summary>
        /// Length of this block in bytes
        /// Can be:
        /// - int: Fixed length (e.g., 4, 16, 256)
        /// - "var:name": Variable reference
        /// - "calc:expression": Calculated expression
        /// </summary>
        public object Length { get; set; }

        /// <summary>
        /// Sentinel pattern — stop reading when this hex byte sequence is encountered.
        /// Examples: "00" (null terminator), "0D0A" (CRLF), "504B0102" (EOCD magic).
        /// Supports "var:name" references. When set, Length becomes optional.
        /// </summary>
        public string Until { get; set; }

        /// <summary>
        /// Safety cap in bytes when Until is used and the pattern is never found.
        /// Defaults to 65536 when Until is set and MaxLength is not specified.
        /// </summary>
        public int MaxLength { get; set; }

        /// <summary>
        /// When true, the Until pattern bytes are included in the field length.
        /// Defaults to false (pattern bytes excluded from the field).
        /// </summary>
        public bool UntilInclusive { get; set; }

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

        /// <summary>Condition for conditional and loop blocks.</summary>
        public ConditionDefinition Condition { get; set; }

        /// <summary>Blocks to execute if condition is true.</summary>
        public List<BlockDefinition> Then { get; set; }

        /// <summary>Blocks to execute if condition is false (optional).</summary>
        public List<BlockDefinition> Else { get; set; }

        /// <summary>
        /// Label shown when condition is true (for display in parsed fields panel).
        /// Example: "This file is a DLL"
        /// </summary>
        public string TrueLabel { get; set; }

        /// <summary>
        /// Label shown when condition is false.
        /// Example: "This file is an EXE"
        /// </summary>
        public string FalseLabel { get; set; }

        #endregion

        #region Repeating Block Properties (v2.0)

        /// <summary>
        /// Sub-field definitions for repeating and union blocks.
        /// Each entry describes one field within a single repeated entry.
        /// </summary>
        public List<BlockDefinition> Fields { get; set; }

        /// <summary>
        /// Fixed byte size of each repeated entry. Used to advance the base offset per iteration.
        /// Can be: int | "var:name" | "calc:expression"
        /// </summary>
        public object EntrySize { get; set; }

        /// <summary>
        /// Name of the variable to store the current iteration index (0-based).
        /// Example: "sectionIndex" → accessible in field offset expressions.
        /// </summary>
        public string IndexVar { get; set; }

        /// <summary>
        /// Array of hex color strings to cycle through for successive repeated entries.
        /// Example: ["#FF6B6B", "#4ECDC4", "#FFE66D"]
        /// </summary>
        public List<string> ColorCycle { get; set; }

        #endregion

        #region Union Block Properties (v2.0)

        /// <summary>
        /// Condition variable name used to select the active union variant.
        /// The string value of variables[Condition] is matched against Variants keys.
        /// </summary>
        public string UnionCondition { get; set; }

        /// <summary>
        /// Variants for union blocks. Key = condition value (string).
        /// Each variant can specify a different length, valueType, and color.
        /// </summary>
        public Dictionary<string, UnionVariant> Variants { get; set; }

        #endregion

        #region Nested / Pointer Block Properties (v2.0)

        /// <summary>
        /// Reference to an external struct definition (without file extension).
        /// Example: "structs/PE_OptionalHeader" → loaded from embedded catalog.
        /// </summary>
        public string StructRef { get; set; }

        /// <summary>
        /// Variable name containing the target offset for pointer blocks.
        /// The pointer block creates a jump annotation in the structure view.
        /// </summary>
        public string TargetVar { get; set; }

        /// <summary>
        /// Display label for pointer blocks shown in the structure tree.
        /// Example: "→ PE Header"
        /// </summary>
        public string Label { get; set; }

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
        /// Validate this block definition.
        /// New v2 block types (repeating/union/nested/pointer) are always accepted
        /// with minimal validation so old engines silently skip unknown types.
        /// </summary>
        public bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(Type))
                return false;

            switch (Type.ToLowerInvariant())
            {
                case "signature":
                case "field":
                    // Must have (offset OR offsetFrom) + length + color
                    bool hasOffset = Offset != null || !string.IsNullOrWhiteSpace(OffsetFrom);
                    return hasOffset && Length != null && !string.IsNullOrWhiteSpace(Color);

                case "metadata":
                    return !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(Variable);

                case "conditional":
                    return Condition != null && Then != null && Then.Count > 0;

                case "loop":
                    return Condition != null && Body != null && Body.Count > 0
                           && MaxIterations > 0 && MaxIterations <= 100000;

                case "action":
                    return !string.IsNullOrWhiteSpace(Action) && !string.IsNullOrWhiteSpace(Variable);

                case "computefromvariables":
                    return !string.IsNullOrWhiteSpace(Expression) && !string.IsNullOrWhiteSpace(StoreAs);

                // v2.0 new block types — accept with minimal requirements
                case "repeating":
                    return !string.IsNullOrWhiteSpace(Name) && Count != null;

                case "union":
                    return !string.IsNullOrWhiteSpace(Name) && Variants != null && Variants.Count > 0;

                case "nested":
                    return !string.IsNullOrWhiteSpace(StructRef);

                case "pointer":
                    return !string.IsNullOrWhiteSpace(TargetVar);

                // v2.3 group block — logical grouping of sub-fields, no own offset/length required
                case "group":
                    return true;

                // v2.4 header block — labeled section overlay; requires offset + length + color
                case "header":
                    bool hasHeaderOffset = Offset != null || !string.IsNullOrWhiteSpace(OffsetFrom);
                    return hasHeaderOffset && Length != null && !string.IsNullOrWhiteSpace(Color);

                // v2.4 data block — raw byte region overlay; requires offset + length
                case "data":
                    bool hasDataOffset = Offset != null || !string.IsNullOrWhiteSpace(OffsetFrom);
                    return hasDataOffset && Length != null;

                default:
                    // Unknown block types silently pass — forward-compatibility
                    return true;
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
    /// A single variant within a union block.
    /// Describes the layout when the union's condition matches this variant's key.
    /// </summary>
    public class UnionVariant
    {
        /// <summary>Length of this variant in bytes. Can be int | "var:name" | "calc:expression".</summary>
        public object Length { get; set; }

        /// <summary>Value type for decoding this variant (e.g., "ipv4", "uint32", "utf8").</summary>
        public string ValueType { get; set; }

        /// <summary>Background color for this variant's highlight (#RRGGBB).</summary>
        public string Color { get; set; }

        /// <summary>Opacity 0.0–1.0. Default 0.3.</summary>
        public double Opacity { get; set; } = 0.3;

        /// <summary>Optional nested sub-fields for this variant.</summary>
        public List<BlockDefinition> Fields { get; set; }

        /// <summary>Human-readable description of this variant.</summary>
        public string Description { get; set; }
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
