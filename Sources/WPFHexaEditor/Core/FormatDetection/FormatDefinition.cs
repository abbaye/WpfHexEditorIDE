//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System.Collections.Generic;

namespace WpfHexaEditor.Core.FormatDetection
{
    /// <summary>
    /// Represents a complete file format definition loaded from JSON
    /// Contains metadata, detection rules, and block definitions
    /// </summary>
    public class FormatDefinition
    {
        /// <summary>
        /// Human-readable format name (e.g., "ZIP Archive", "PNG Image")
        /// </summary>
        public string FormatName { get; set; }

        /// <summary>
        /// Version of this definition (e.g., "1.0")
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// File extensions associated with this format (e.g., [".zip", ".jar"])
        /// </summary>
        public List<string> Extensions { get; set; } = new List<string>();

        /// <summary>
        /// Description of the format
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Author of this definition
        /// </summary>
        public string Author { get; set; }

        /// <summary>
        /// Category of this format (e.g., "Archives", "Images", "Documents")
        /// Automatically set from folder structure when loading format definitions
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Detection rule (signature check)
        /// </summary>
        public DetectionRule Detection { get; set; }

        /// <summary>
        /// Initial variables and their values
        /// Used by the script interpreter for calculations
        /// </summary>
        public Dictionary<string, object> Variables { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// List of block definitions to render
        /// </summary>
        public List<BlockDefinition> Blocks { get; set; } = new List<BlockDefinition>();

        /// <summary>
        /// Optional functions documentation (for reference)
        /// </summary>
        public Dictionary<string, string> Functions { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Validate this format definition
        /// </summary>
        public bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(FormatName))
                return false;

            if (Detection == null || !Detection.IsValid())
                return false;

            if (Blocks == null || Blocks.Count == 0)
                return false;

            return true;
        }

        public override string ToString()
        {
            return $"{FormatName} v{Version} ({Extensions?.Count ?? 0} extensions, {Blocks?.Count ?? 0} blocks)";
        }
    }

    /// <summary>
    /// Detection rule for identifying file formats
    /// Uses magic bytes (signature) at a specific offset
    /// </summary>
    public class DetectionRule
    {
        /// <summary>
        /// Hex signature to match (e.g., "504B0304" for ZIP)
        /// </summary>
        public string Signature { get; set; }

        /// <summary>
        /// Offset where signature should appear (usually 0)
        /// </summary>
        public long Offset { get; set; }

        /// <summary>
        /// Whether this signature is required for detection
        /// </summary>
        public bool Required { get; set; } = true;

        /// <summary>
        /// Validate this detection rule
        /// </summary>
        public bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(Signature))
                return false;

            // Signature must be valid hex string (even number of hex digits)
            if (Signature.Length % 2 != 0)
                return false;

            // Check all characters are valid hex
            foreach (char c in Signature)
            {
                if (!System.Uri.IsHexDigit(c))
                    return false;
            }

            return Offset >= 0;
        }

        /// <summary>
        /// Convert hex signature string to byte array
        /// </summary>
        public byte[] GetSignatureBytes()
        {
            if (!IsValid())
                return null;

            var bytes = new byte[Signature.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = System.Convert.ToByte(Signature.Substring(i * 2, 2), 16);
            }

            return bytes;
        }

        public override string ToString()
        {
            return $"Signature: {Signature} at offset {Offset} (required: {Required})";
        }
    }

    /// <summary>
    /// Condition definition for conditional blocks
    /// </summary>
    public class ConditionDefinition
    {
        /// <summary>
        /// Field to check (e.g., "offset:6" to read 2 bytes at offset 6)
        /// Supported formats:
        /// - "offset:N" - Read from absolute offset N
        /// - "var:name" - Read variable value
        /// </summary>
        public string Field { get; set; }

        /// <summary>
        /// Comparison operator
        /// Supported: "equals", "notEquals", "greaterThan", "lessThan"
        /// </summary>
        public string Operator { get; set; }

        /// <summary>
        /// Value to compare against (hex string like "0x0008")
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Number of bytes to read from field (default: 1)
        /// </summary>
        public int Length { get; set; } = 1;

        public override string ToString()
        {
            return $"{Field} {Operator} {Value} (length: {Length})";
        }
    }
}
