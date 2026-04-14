// ==========================================================
// Project: WpfHexEditor.Core
// File: FormatDefinition.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Represents a complete file format definition loaded from a .whfmt JSON file.
//     Contains metadata (name, extension, MIME), detection rules (magic bytes,
//     signatures), and block definitions for structured field parsing.
//
// Architecture Notes:
//     Deserialized from JSON via System.Text.Json. Includes PreferredEditor
//     and IsTextFormat properties for editor selection routing. Consumed by
//     FormatDetectionService and FormatScriptInterpreter. No WPF dependencies.
//
// ==========================================================

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

        // ── whfmt v2.0 top-level fields ─────────────────────────────────────────

        /// <summary>
        /// Preferred diff algorithm: "text", "semantic", or "binary".
        /// Used by DiffViewer to select the appropriate comparison engine.
        /// </summary>
        public string DiffMode { get; set; }

        /// <summary>
        /// Describes how to detect the format version from file content,
        /// enabling dispatch to version-specific block sets (VersionedBlocks).
        /// </summary>
        public FormatVersionDetection VersionDetection { get; set; }

        /// <summary>
        /// Version-specific block sets, keyed by the version string returned by VersionDetection.
        /// Example: { "PE32": [...], "PE32+": [...] }
        /// </summary>
        public Dictionary<string, List<BlockDefinition>> VersionedBlocks { get; set; }

        /// <summary>
        /// Checksum validation rules evaluated after block interpretation.
        /// </summary>
        public List<ChecksumDefinition> Checksums { get; set; }

        /// <summary>
        /// Assertion rules that must hold for a well-formed file.
        /// Failures populate the ForensicAlerts panel.
        /// </summary>
        public List<AssertionDefinition> Assertions { get; set; }

        /// <summary>
        /// Forensic / security metadata: suspicious patterns, risk level, known attack vectors.
        /// </summary>
        public ForensicDefinition Forensic { get; set; }

        /// <summary>
        /// Named navigation bookmarks derived from parsed variables (entry points, header offsets…).
        /// </summary>
        public NavigationDefinition Navigation { get; set; }

        /// <summary>
        /// Declares how parsed fields are grouped and displayed in the Format Inspector panel.
        /// </summary>
        public InspectorDefinition Inspector { get; set; }

        /// <summary>
        /// Export templates that let users extract structured data from the active file.
        /// </summary>
        public List<ExportTemplate> ExportTemplates { get; set; }

        /// <summary>
        /// Contextual hints for AI-assisted analysis.
        /// </summary>
        public AiHints AiHints { get; set; }

        // ── end v2.0 fields ──────────────────────────────────────────────────────

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
    /// Detection rule for identifying file formats.
    /// Supports single-signature (legacy) and multi-signature (v2.0) detection.
    /// </summary>
    public class DetectionRule
    {
        /// <summary>
        /// Legacy single hex signature (e.g., "504B0304" for ZIP).
        /// If Signatures array is provided, this field is ignored.
        /// </summary>
        public string Signature { get; set; }

        /// <summary>
        /// v2.0: Multiple alternative signatures (OR logic by default).
        /// Any one match is sufficient to trigger detection (matchMode = "any").
        /// </summary>
        public List<SignatureEntry> Signatures { get; set; }

        /// <summary>
        /// v2.0: How to combine multi-signature matches: "any" (default) or "all".
        /// </summary>
        public string MatchMode { get; set; } = "any";

        /// <summary>
        /// v2.0: Entropy hint — expected Shannon entropy range for valid files.
        /// Used as a secondary signal when signature matching is ambiguous.
        /// </summary>
        public EntropyHint EntropyHint { get; set; }

        /// <summary>
        /// v2.0: Minimum weighted confidence score (0.0–1.0) across all signals.
        /// Default: 0.70 (70%).
        /// </summary>
        public double MinimumScore { get; set; } = 0.70;

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
        [JsonConverter(typeof(SignatureStrengthConverter))]
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
        /// Validate this detection rule.
        /// v2.0: accepts rules with only a Signatures array and no legacy Signature.
        /// </summary>
        public bool IsValid()
        {
            // v2.0 multi-signature array takes precedence
            if (Signatures != null && Signatures.Count > 0)
                return true;

            // Legacy single-signature path
            if (string.IsNullOrWhiteSpace(Signature))
                return !Required; // valid when optional

            if (Signature.Length % 2 != 0)
                return false;

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
    /// Case-insensitive JSON converter for <see cref="SignatureStrength"/>.
    /// Accepts string names ("strong", "Strong", "STRONG") and integer values (80).
    /// Falls back to <see cref="SignatureStrength.Medium"/> for unknown values.
    /// </summary>
    internal sealed class SignatureStrengthConverter : JsonConverter<SignatureStrength>
    {
        public override SignatureStrength Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
            {
                if (reader.TryGetInt32(out int intVal) && Enum.IsDefined(typeof(SignatureStrength), intVal))
                    return (SignatureStrength)intVal;
                return SignatureStrength.Medium;
            }
            if (reader.TokenType == JsonTokenType.String)
            {
                var s = reader.GetString();
                if (Enum.TryParse<SignatureStrength>(s, ignoreCase: true, out var result))
                    return result;
                return SignatureStrength.Medium; // unknown value → safe default
            }
            return SignatureStrength.Medium;
        }

        public override void Write(Utf8JsonWriter writer, SignatureStrength value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString());
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
                                if (reader.TokenType == JsonTokenType.Number)
                                {
                                    if (reader.TryGetInt64(out long lv))
                                        condition.Value = lv.ToString(System.Globalization.CultureInfo.InvariantCulture);
                                    else if (reader.TryGetDouble(out double dv))
                                        condition.Value = dv.ToString(System.Globalization.CultureInfo.InvariantCulture);
                                    else
                                        condition.Value = reader.GetDecimal().ToString(System.Globalization.CultureInfo.InvariantCulture);
                                }
                                else
                                {
                                    condition.Value = reader.GetString();
                                }
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

    // ── whfmt v2.0 Supporting Classes ────────────────────────────────────────

    /// <summary>A single signature entry in a multi-signature detection rule.</summary>
    public class SignatureEntry
    {
        /// <summary>Hex bytes to match (e.g., "FF FB").</summary>
        public string Value { get; set; }
        /// <summary>File offset where the signature must appear. Default 0.</summary>
        public long Offset { get; set; }
        /// <summary>Human-readable label for this variant (e.g., "MP3 no ID3").</summary>
        public string Label { get; set; }
        /// <summary>Confidence weight contribution (0.0–1.0). Default 0.9.</summary>
        public double Weight { get; set; } = 0.9;
    }

    /// <summary>Expected Shannon entropy range for format detection hints.</summary>
    public class EntropyHint
    {
        /// <summary>Minimum expected entropy (0.0–8.0).</summary>
        public double Min { get; set; }
        /// <summary>Maximum expected entropy (0.0–8.0). Default 8.0.</summary>
        public double Max { get; set; } = 8.0;
    }

    /// <summary>Version detection config — reads a field value and maps it to a version string.</summary>
    public class FormatVersionDetection
    {
        /// <summary>Variable name set by a prior field block whose value drives version selection.</summary>
        public string Field { get; set; }
        /// <summary>Maps raw field values (as strings) to version labels.</summary>
        public Dictionary<string, string> Map { get; set; }
    }

    /// <summary>Checksum validation rule applied after block interpretation.</summary>
    public class ChecksumDefinition
    {
        /// <summary>Display name for this checksum rule.</summary>
        public string Name { get; set; }
        /// <summary>Algorithm: "crc32", "crc16", "md5", "sha1", "sha256", "adler32".</summary>
        public string Algorithm { get; set; }
        /// <summary>Byte range to compute checksum over.</summary>
        public ChecksumRange DataRange { get; set; }
        /// <summary>Location where the expected checksum is stored in the file.</summary>
        public ChecksumStoredAt StoredAt { get; set; }
        /// <summary>Data type of the stored checksum field ("uint32", "uint32be", etc.).</summary>
        public string StoredType { get; set; }
        /// <summary>Expected checksum hex string (alternative to StoredAt — known fixed value).</summary>
        public string ExpectedValue { get; set; }
        /// <summary>Severity if checksum fails: "error", "warning", "info". Default "warning".</summary>
        public string Severity { get; set; } = "warning";
    }

    /// <summary>Byte range specification using variable names.</summary>
    public class ChecksumRange
    {
        /// <summary>Variable name holding the start offset.</summary>
        public string OffsetVar { get; set; }
        /// <summary>Variable name holding the length.</summary>
        public string LengthVar { get; set; }
        /// <summary>Fixed start offset (alternative to OffsetVar).</summary>
        public long? FixedOffset { get; set; }
        /// <summary>Fixed length (alternative to LengthVar).</summary>
        public long? FixedLength { get; set; }
    }

    /// <summary>Location of the stored checksum value in the file.</summary>
    public class ChecksumStoredAt
    {
        /// <summary>Variable name holding the offset of the stored checksum.</summary>
        public string OffsetVar { get; set; }
        /// <summary>Fixed offset of the stored checksum.</summary>
        public long? FixedOffset { get; set; }
        /// <summary>Byte length of the stored checksum field (e.g. 4 for CRC32).</summary>
        public int Length { get; set; }
        /// <summary>Endianness of the stored value: "little" | "big". Default "little".</summary>
        public string Endianness { get; set; }
    }

    /// <summary>An assertion that must hold for a well-formed file.</summary>
    public class AssertionDefinition
    {
        /// <summary>Display name for this assertion.</summary>
        public string Name { get; set; }
        /// <summary>Boolean expression evaluated against parsed variables.</summary>
        public string Expression { get; set; }
        /// <summary>Severity if assertion fails: "error", "warning", "info". Default "warning".</summary>
        public string Severity { get; set; } = "warning";
        /// <summary>Human-readable failure message shown in Forensic Alerts panel.</summary>
        public string Message { get; set; }
    }

    /// <summary>Forensic / security metadata for the format.</summary>
    public class ForensicDefinition
    {
        /// <summary>Format category for forensic purposes (e.g., "executable", "document").</summary>
        public string Category { get; set; }
        /// <summary>Inherent risk level: "low", "medium", "high". Default "low".</summary>
        public string RiskLevel { get; set; } = "low";
        /// <summary>Patterns that warrant forensic attention when detected.</summary>
        public List<ForensicPattern> SuspiciousPatterns { get; set; }
        /// <summary>Patterns that are definitively malicious / corrupted.</summary>
        public List<ForensicPattern> KnownMaliciousPatterns { get; set; }
    }

    /// <summary>A single forensic detection pattern.</summary>
    public class ForensicPattern
    {
        /// <summary>Display name for this pattern.</summary>
        public string Name { get; set; }
        /// <summary>Boolean expression to detect this pattern (uses parsed variables).</summary>
        public string Condition { get; set; }
        /// <summary>Optional entropy threshold that must also be exceeded (7.0–8.0).</summary>
        public double? EntropyThreshold { get; set; }
        /// <summary>Severity: "error", "warning", "info". Default "warning".</summary>
        public string Severity { get; set; } = "warning";
        /// <summary>Human-readable description shown in Forensic Alerts panel.</summary>
        public string Description { get; set; }
    }

    /// <summary>Named navigation bookmarks derived from parsed variables.</summary>
    public class NavigationDefinition
    {
        /// <summary>Named jump targets (e.g., Entry Point, PE Header).</summary>
        public List<NavigationBookmark> Bookmarks { get; set; }
        /// <summary>Follow-pointer targets (jump to the value stored in a variable).</summary>
        public List<NavigationPointer> Pointers { get; set; }
    }

    /// <summary>A named bookmark at an offset stored in a variable.</summary>
    public class NavigationBookmark
    {
        /// <summary>Display name (e.g., "Entry Point").</summary>
        public string Name { get; set; }
        /// <summary>Variable whose value is the target offset.</summary>
        public string OffsetVar { get; set; }
        /// <summary>Segoe MDL2 glyph character for the icon (e.g., "\uE768").</summary>
        public string Icon { get; set; }
        /// <summary>Hex highlight color for this bookmark (#RRGGBB).</summary>
        public string Color { get; set; }
    }

    /// <summary>A "follow pointer" jump annotation in the structure view.</summary>
    public class NavigationPointer
    {
        /// <summary>Display label (e.g., "Follow e_lfanew").</summary>
        public string Name { get; set; }
        /// <summary>Variable holding the pointer value (target offset).</summary>
        public string OffsetVar { get; set; }
    }

    /// <summary>Inspector panel layout — how parsed fields are grouped and displayed.</summary>
    public class InspectorDefinition
    {
        /// <summary>Field groups shown as collapsible sections in the inspector.</summary>
        public List<InspectorGroup> Groups { get; set; }
        /// <summary>Variable name whose value is shown as a badge chip (e.g., "peMachineName").</summary>
        public string Badge { get; set; }
        /// <summary>Variable name of the primary / most important field.</summary>
        public string PrimaryField { get; set; }
        /// <summary>Whether to display the QualityMetrics.CompletenessScore. Default false.</summary>
        public bool ShowQualityScore { get; set; }
    }

    /// <summary>A collapsible group within the inspector panel.</summary>
    public class InspectorGroup
    {
        /// <summary>Group header title.</summary>
        public string Title { get; set; }
        /// <summary>Segoe MDL2 glyph for the group icon.</summary>
        public string Icon { get; set; }
        /// <summary>Whether the group starts collapsed. Default false.</summary>
        public bool Collapsed { get; set; }
        /// <summary>Whether this group is highlighted (accent background). Default false.</summary>
        public bool Highlight { get; set; }
        /// <summary>Variable names of fields to display in this group.</summary>
        public List<string> Fields { get; set; }
    }

    /// <summary>Export template for extracting structured data from the active file.</summary>
    public class ExportTemplate
    {
        /// <summary>Display name for this template.</summary>
        public string Name { get; set; }
        /// <summary>Output format: "json", "csv", "c-struct", "python-bytes", "xml".</summary>
        public string Format { get; set; }
        /// <summary>Variable names to include (for json/c-struct templates).</summary>
        public List<string> Fields { get; set; }
        /// <summary>Source repeating block name (for csv templates). Prefix: "repeating:".</summary>
        public string Source { get; set; }
        /// <summary>Column names to include from the repeating block (for csv).</summary>
        public List<string> Columns { get; set; }
        /// <summary>Struct name override for c-struct output.</summary>
        public string StructName { get; set; }
    }

    /// <summary>Contextual hints for AI-assisted analysis.</summary>
    public class AiHints
    {
        /// <summary>Brief analysis context for LLM prompts.</summary>
        public string AnalysisContext { get; set; }
        /// <summary>Known vulnerability patterns relevant to this format.</summary>
        public List<string> KnownVulnerabilities { get; set; }
        /// <summary>Suggested manual inspections for analysts.</summary>
        public List<string> SuggestedInspections { get; set; }
        /// <summary>Forensic context (e.g., "common in malware analysis").</summary>
        public string ForensicContext { get; set; }
    }

    // ── end v2.0 supporting classes ───────────────────────────────────────────

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
