//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WpfHexEditor.Core.FormatDetection
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
        /// Technical references and web links for this format
        /// </summary>
        public FormatReferences References { get; set; }

        /// <summary>
        /// MIME types associated with this format
        /// </summary>
        public List<string> MimeTypes { get; set; } = new List<string>();

        /// <summary>
        /// Quality metrics for this format definition
        /// </summary>
        public QualityMetrics QualityMetrics { get; set; }

        /// <summary>
        /// Software that can open/create this format
        /// </summary>
        public List<string> Software { get; set; } = new List<string>();

        /// <summary>
        /// Common use cases for this format
        /// </summary>
        public List<string> UseCases { get; set; } = new List<string>();

        /// <summary>
        /// Preferred editor factory ID for files matching this format.
        /// When null, the editor is derived automatically: IsTextFormat=true → "code-editor";
        /// binary with blocks → "structure-editor"; otherwise falls back to registry first-match.
        /// Valid values: "code-editor", "structure-editor", "hex-editor", "text-editor", "tbl-editor".
        /// </summary>
        public string? PreferredEditor { get; set; }

        /// <summary>
        /// Format relationships and associations
        /// </summary>
        public FormatRelationships FormatRelationships { get; set; }

        /// <summary>
        /// Technical details specific to this format type
        /// </summary>
        public TechnicalDetails TechnicalDetails { get; set; }

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
        /// Strength of the signature (for confidence scoring)
        /// Default: Medium for backward compatibility
        /// </summary>
        public SignatureStrength Strength { get; set; } = SignatureStrength.Medium;

        /// <summary>
        /// Whether this format is text-based (e.g., YAML, JSON, CSV)
        /// Text-based formats are handled differently in content analysis
        /// </summary>
        public bool IsTextFormat { get; set; } = false;

        /// <summary>
        /// Content validation patterns (regex) for text formats
        /// Used to validate content matches expected format structure
        /// </summary>
        public List<string> ContentPatterns { get; set; }

        /// <summary>
        /// Minimum confidence threshold for auto-selection (0.0 - 1.0)
        /// If confidence is below this, user selection may be prompted
        /// </summary>
        public double MinConfidenceThreshold { get; set; } = 0.7;

        /// <summary>
        /// Validate this detection rule
        /// </summary>
        public bool IsValid()
        {
            // Allow empty signature if not required (for text formats without magic bytes)
            if (string.IsNullOrWhiteSpace(Signature))
            {
                return !Required; // Valid if signature is optional
            }

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
    /// Signature strength classification for confidence scoring.
    /// Higher values indicate more unique/reliable signatures.
    /// </summary>
    public enum SignatureStrength
    {
        /// <summary>
        /// No signature or required: false (lowest confidence)
        /// </summary>
        None = 0,

        /// <summary>
        /// Weak signature - matches many files (e.g., 0x00, 0xFF, single byte)
        /// </summary>
        Weak = 20,

        /// <summary>
        /// Medium signature - somewhat specific (e.g., GIF "GIF", BMP "BM")
        /// </summary>
        Medium = 50,

        /// <summary>
        /// Strong signature - highly specific but still common (e.g., PDF, ELF)
        /// </summary>
        Strong = 80,

        /// <summary>
        /// Unique signature - highly distinctive (e.g., PNG, ZIP, JPEG)
        /// </summary>
        Unique = 100
    }

    /// <summary>
    /// JSON converter that handles ConditionDefinition as either a JSON object or a string expression.
    /// String conditions like "(generalPurposeFlags &amp; 1) == 1" are parsed into Field/Operator/Value
    /// when possible, or stored as an expression string for future evaluation.
    /// </summary>
    public class ConditionDefinitionConverter : JsonConverter<ConditionDefinition>
    {
        public override ConditionDefinition Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            if (reader.TokenType == JsonTokenType.String)
            {
                // String condition like "(generalPurposeFlags & 1) == 1"
                // Store as expression - the interpreter can evaluate it later
                var expression = reader.GetString();
                if (string.IsNullOrWhiteSpace(expression))
                    return null;

                // Try to parse simple "var op value" patterns
                return ParseStringCondition(expression);
            }

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                // Standard object format: { "field": "...", "operator": "...", "value": "..." }
                var condition = new ConditionDefinition();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        var propertyName = reader.GetString()?.ToLowerInvariant();
                        reader.Read();
                        switch (propertyName)
                        {
                            case "field":
                                condition.Field = reader.GetString();
                                break;
                            case "operator":
                                condition.Operator = reader.GetString();
                                break;
                            case "value":
                                condition.Value = reader.GetString();
                                break;
                            case "length":
                                condition.Length = reader.GetInt32();
                                break;
                        }
                    }
                }
                return condition;
            }

            // Skip unexpected token types
            reader.Skip();
            return null;
        }

        public override void Write(Utf8JsonWriter writer, ConditionDefinition value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStartObject();
            if (value.Field != null) writer.WriteString("field", value.Field);
            if (value.Operator != null) writer.WriteString("operator", value.Operator);
            if (value.Value != null) writer.WriteString("value", value.Value);
            writer.WriteNumber("length", value.Length);
            writer.WriteEndObject();
        }

        private static ConditionDefinition ParseStringCondition(string expression)
        {
            // Try to parse patterns like "(var & mask) == value" or "var == value"
            // For now, store the expression as-is in the Field property with a special prefix
            // The interpreter can handle "expr:" prefix for expression-based conditions
            return new ConditionDefinition
            {
                Field = "expr:" + expression,
                Operator = "expression",
                Value = expression,
                Length = 0
            };
        }
    }

    /// <summary>
    /// Condition definition for conditional blocks
    /// </summary>
    [JsonConverter(typeof(ConditionDefinitionConverter))]
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

    /// <summary>
    /// Technical references for a format definition
    /// </summary>
    public class FormatReferences
    {
        /// <summary>
        /// List of technical specification names
        /// </summary>
        public List<string> Specifications { get; set; } = new List<string>();

        /// <summary>
        /// List of web links to specifications and documentation
        /// </summary>
        public List<string> WebLinks { get; set; } = new List<string>();
    }

    /// <summary>
    /// Quality metrics for format definition completeness
    /// Added by enrichment automation - tracks metadata quality
    /// </summary>
    public class QualityMetrics
    {
        /// <summary>
        /// Completeness score (0-100) indicating how well-documented this format is
        /// </summary>
        public int CompletenessScore { get; set; }

        /// <summary>
        /// Documentation level: "basic", "standard", "detailed", "comprehensive"
        /// </summary>
        public string DocumentationLevel { get; set; }

        /// <summary>
        /// Number of blocks defined in this format
        /// </summary>
        public int BlocksDefined { get; set; }

        /// <summary>
        /// Number of validation rules defined
        /// </summary>
        public int ValidationRules { get; set; }

        /// <summary>
        /// Last updated date (YYYY-MM-DD format)
        /// </summary>
        public string LastUpdated { get; set; }

        /// <summary>
        /// Whether this is a priority/critical format (top 100)
        /// </summary>
        public bool PriorityFormat { get; set; }

        /// <summary>
        /// Whether this format was auto-refined by enrichment script
        /// </summary>
        public bool AutoRefined { get; set; }
    }

    /// <summary>
    /// Format relationships - how this format relates to other formats and categories
    /// </summary>
    public class FormatRelationships
    {
        /// <summary>
        /// Primary category this format belongs to
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// List of file extensions associated with this format
        /// </summary>
        public List<string> Extensions { get; set; } = new List<string>();

        /// <summary>
        /// Related formats (variants, versions, compatible formats)
        /// </summary>
        public List<string> RelatedFormats { get; set; } = new List<string>();
    }

    /// <summary>
    /// Technical details specific to the format type
    /// Properties vary by category (archives, images, executables, etc.)
    /// </summary>
    public class TechnicalDetails
    {
        /// <summary>
        /// Primary file extension (most common)
        /// </summary>
        public string PrimaryExtension { get; set; }

        /// <summary>
        /// Number of defined fields/blocks
        /// </summary>
        public int DefinedFields { get; set; }

        /// <summary>
        /// Compression method (for archives/compressed formats)
        /// </summary>
        public string CompressionMethod { get; set; }

        /// <summary>
        /// Whether this is an archive format
        /// </summary>
        public bool ArchivesFormat { get; set; }

        /// <summary>
        /// Whether this is an image format
        /// </summary>
        public bool ImagesFormat { get; set; }

        /// <summary>
        /// Whether this is an executable format
        /// </summary>
        public bool ExecutablesFormat { get; set; }

        /// <summary>
        /// Whether this is an audio format
        /// </summary>
        public bool AudioFormat { get; set; }

        /// <summary>
        /// Whether this is a video format
        /// </summary>
        public bool VideoFormat { get; set; }

        /// <summary>
        /// Whether this is a document format
        /// </summary>
        public bool DocumentFormat { get; set; }

        /// <summary>
        /// Bit depth (for images/audio)
        /// </summary>
        public int? BitDepth { get; set; }

        /// <summary>
        /// Color space (for images)
        /// </summary>
        public string ColorSpace { get; set; }

        /// <summary>
        /// Sample rate (for audio)
        /// </summary>
        public int? SampleRate { get; set; }

        /// <summary>
        /// Container format (for video/audio)
        /// </summary>
        public string Container { get; set; }

        /// <summary>
        /// Platform (for executables/ROMs)
        /// </summary>
        public string Platform { get; set; }

        /// <summary>
        /// Encryption support
        /// </summary>
        public bool SupportsEncryption { get; set; }
    }
}
