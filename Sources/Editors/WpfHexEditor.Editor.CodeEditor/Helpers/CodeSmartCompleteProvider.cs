//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Custom CodeEditor - SmartComplete Provider (Phase 5)
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com), Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using WpfHexEditor.Core.ProjectSystem.Languages;
using WpfHexEditor.Editor.CodeEditor.Models;

namespace WpfHexEditor.Editor.CodeEditor.Helpers
{
    /// <summary>
    /// Provides context-aware SmartComplete suggestions for format definition JSON.
    /// Phase 5: Full property coverage for all block types, field properties,
    /// detection properties, snippets, and block-type-aware context detection.
    /// </summary>
    public class CodeSmartCompleteProvider
    {
        #region Suggestion Data

        // Root level properties for format definitions
        private static readonly List<SmartCompleteSuggestion> RootProperties = new List<SmartCompleteSuggestion>
        {
            new SmartCompleteSuggestion
            {
                DisplayText = "formatName",
                InsertText = "\"formatName\": \"\"",
                CursorOffset = -1,
                Icon = "ðŸ·ï¸",
                TypeHint = "string",
                Documentation = "Unique identifier for this format definition (e.g., \"PNG Image\", \"ELF Executable\")",
                Type = SuggestionType.Property,
                SortPriority = 10
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "version",
                InsertText = "\"version\": \"1.0\"",
                CursorOffset = -1,
                Icon = "ðŸ”¢",
                TypeHint = "string",
                Documentation = "Version number of this format definition (e.g., \"1.0\", \"2.1\")",
                Type = SuggestionType.Property,
                SortPriority = 20
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "description",
                InsertText = "\"description\": \"\"",
                CursorOffset = -1,
                Icon = "ðŸ“",
                TypeHint = "string",
                Documentation = "Human-readable description of the file format",
                Type = SuggestionType.Property,
                SortPriority = 30
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "extensions",
                InsertText = "\"extensions\": [\".ext\"]",
                CursorOffset = -2,
                Icon = "ðŸ“",
                TypeHint = "string[]",
                Documentation = "Array of file extensions associated with this format (e.g., [\".png\", \".jpg\"])",
                Type = SuggestionType.Property,
                SortPriority = 40
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "author",
                InsertText = "\"author\": \"\"",
                CursorOffset = -1,
                Icon = "ðŸ‘¤",
                TypeHint = "string",
                Documentation = "Author of this format definition",
                Type = SuggestionType.Property,
                SortPriority = 50
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "website",
                InsertText = "\"website\": \"\"",
                CursorOffset = -1,
                Icon = "ðŸŒ",
                TypeHint = "string",
                Documentation = "Website URL with format specification or documentation",
                Type = SuggestionType.Property,
                SortPriority = 60
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "detection",
                InsertText = "\"detection\": {}",
                CursorOffset = -1,
                Icon = "ðŸ”",
                TypeHint = "object",
                Documentation = "File format detection rules (signatures, magic bytes)",
                Type = SuggestionType.Property,
                SortPriority = 70
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "blocks",
                InsertText = "\"blocks\": []",
                CursorOffset = -1,
                Icon = "ðŸ“¦",
                TypeHint = "array",
                Documentation = "Array of data blocks that define the file structure",
                Type = SuggestionType.Property,
                SortPriority = 80
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "category",
                InsertText = "\"category\": \"\"",
                CursorOffset = -1,
                Icon = "[cat]",
                TypeHint = "string",
                Documentation = "Format category (e.g., \"Archives\", \"Images\", \"Audio\", \"Video\")",
                Type = SuggestionType.Property,
                SortPriority = 85
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "variables",
                InsertText = "\"variables\": {}",
                CursorOffset = -1,
                Icon = "[var]",
                TypeHint = "object",
                Documentation = "Named variables shared across blocks (accessible via var: prefix)",
                Type = SuggestionType.Property,
                SortPriority = 90
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "checksums",
                InsertText = "\"checksums\": []",
                CursorOffset = -1,
                Icon = "[chk]",
                TypeHint = "array",
                Documentation = "Checksum validation rules (CRC32, SHA256, MD5, etc.)",
                Type = SuggestionType.Property,
                SortPriority = 100
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "assertions",
                InsertText = "\"assertions\": []",
                CursorOffset = -1,
                Icon = "[asr]",
                TypeHint = "array",
                Documentation = "Post-parse boolean assertions to validate format integrity",
                Type = SuggestionType.Property,
                SortPriority = 101
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "forensic",
                InsertText = "\"forensic\": {}",
                CursorOffset = -1,
                Icon = "[frn]",
                TypeHint = "object",
                Documentation = "Forensic analysis metadata: patterns, indicators, threat context",
                Type = SuggestionType.Property,
                SortPriority = 102
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "navigation",
                InsertText = "\"navigation\": {}",
                CursorOffset = -1,
                Icon = "[nav]",
                TypeHint = "object",
                Documentation = "Navigation definitions: bookmarks and pointer targets",
                Type = SuggestionType.Property,
                SortPriority = 103
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "inspector",
                InsertText = "\"inspector\": {}",
                CursorOffset = -1,
                Icon = "[ins]",
                TypeHint = "object",
                Documentation = "Parsed fields panel layout configuration",
                Type = SuggestionType.Property,
                SortPriority = 104
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "exportTemplates",
                InsertText = "\"exportTemplates\": []",
                CursorOffset = -1,
                Icon = "[exp]",
                TypeHint = "array",
                Documentation = "Export format templates (json, csv, c-struct, python-bytes, xml)",
                Type = SuggestionType.Property,
                SortPriority = 105
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "aiHints",
                InsertText = "\"aiHints\": {}",
                CursorOffset = -1,
                Icon = "[ai]",
                TypeHint = "object",
                Documentation = "AI analysis context: hints for automated format interpretation",
                Type = SuggestionType.Property,
                SortPriority = 106
            }
        };

        // Block-level properties
        private static readonly List<SmartCompleteSuggestion> BlockProperties = new List<SmartCompleteSuggestion>
        {
            new SmartCompleteSuggestion
            {
                DisplayText = "type",
                InsertText = "\"type\": \"\"",
                CursorOffset = -1,
                Icon = "ðŸ·ï¸",
                TypeHint = "string",
                Documentation = "Block type: \"signature\", \"field\", \"conditional\", \"loop\", or \"action\"",
                Type = SuggestionType.Property,
                SortPriority = 10
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "name",
                InsertText = "\"name\": \"\"",
                CursorOffset = -1,
                Icon = "ðŸ“›",
                TypeHint = "string",
                Documentation = "Descriptive name for this block",
                Type = SuggestionType.Property,
                SortPriority = 20
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "fields",
                InsertText = "\"fields\": []",
                CursorOffset = -1,
                Icon = "ðŸ“‹",
                TypeHint = "array",
                Documentation = "Array of field definitions within this block",
                Type = SuggestionType.Property,
                SortPriority = 30
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "condition",
                InsertText = "\"condition\": \"\"",
                CursorOffset = -1,
                Icon = "â“",
                TypeHint = "string",
                Documentation = "Condition expression (for conditional blocks)",
                Type = SuggestionType.Property,
                SortPriority = 40
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "count",
                InsertText = "\"count\": \"\"",
                CursorOffset = -1,
                Icon = "ðŸ”¢",
                TypeHint = "string | number",
                Documentation = "Loop count (for loop blocks) - can be number or var: reference",
                Type = SuggestionType.Property,
                SortPriority = 50
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "color",
                InsertText = "\"color\": \"#\"",
                CursorOffset = -1,
                Icon = "[clr]",
                TypeHint = "string",
                Documentation = "Hex color for this block (#RRGGBB, e.g., \"#FF6B6B\")",
                Type = SuggestionType.Property,
                SortPriority = 60
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "opacity",
                InsertText = "\"opacity\": 0.4",
                Icon = "[opa]",
                TypeHint = "number",
                Documentation = "Block highlight opacity in hex editor view (0.0-1.0)",
                Type = SuggestionType.Property,
                SortPriority = 61
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "description",
                InsertText = "\"description\": \"\"",
                CursorOffset = -1,
                Icon = "[dsc]",
                TypeHint = "string",
                Documentation = "Detailed description of this block purpose",
                Type = SuggestionType.Property,
                SortPriority = 62
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "storeAs",
                InsertText = "\"storeAs\": \"\"",
                CursorOffset = -1,
                Icon = "[>v]",
                TypeHint = "string",
                Documentation = "Variable name to store parsed value for later var: references",
                Type = SuggestionType.Property,
                SortPriority = 63
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "hidden",
                InsertText = "\"hidden\": false",
                Icon = "[hid]",
                TypeHint = "bool",
                Documentation = "Parse this block but hide it from parsed fields panel",
                Type = SuggestionType.Property,
                SortPriority = 64
            }
        };

        // Conditional block-specific properties
        private static readonly List<SmartCompleteSuggestion> ConditionalBlockProperties = new List<SmartCompleteSuggestion>
        {
            new SmartCompleteSuggestion
            {
                DisplayText = "condition",
                InsertText = "\"condition\": \"var: == \"",
                CursorOffset = -1,
                Icon = "[if]",
                TypeHint = "string",
                Documentation = "Boolean expression for conditional block (e.g., var:myFlag == 1)",
                Type = SuggestionType.Property,
                SortPriority = 10
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "then",
                InsertText = "\"then\": []",
                CursorOffset = -1,
                Icon = "[ok]",
                TypeHint = "array",
                Documentation = "Blocks/fields to parse when condition is true",
                Type = SuggestionType.Property,
                SortPriority = 20
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "else",
                InsertText = "\"else\": []",
                CursorOffset = -1,
                Icon = "[no]",
                TypeHint = "array",
                Documentation = "Blocks/fields to parse when condition is false",
                Type = SuggestionType.Property,
                SortPriority = 30
            }
        };

        // Loop/repeating block-specific properties
        private static readonly List<SmartCompleteSuggestion> LoopBlockProperties = new List<SmartCompleteSuggestion>
        {
            new SmartCompleteSuggestion
            {
                DisplayText = "maxIterations",
                InsertText = "\"maxIterations\": 1000",
                Icon = "[max]",
                TypeHint = "number",
                Documentation = "Safety cap on iterations to prevent infinite loops (default: 10000)",
                Type = SuggestionType.Property,
                SortPriority = 20
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "indexVar",
                InsertText = "\"indexVar\": \"i\"",
                CursorOffset = -1,
                Icon = "[idx]",
                TypeHint = "string",
                Documentation = "Loop index variable name (0-based), accessible via var: in nested blocks",
                Type = SuggestionType.Property,
                SortPriority = 30
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "colorCycle",
                InsertText = "\"colorCycle\": [\"#4FC3F7\", \"#81C784\"]",
                CursorOffset = -1,
                Icon = "[cyc]",
                TypeHint = "string[]",
                Documentation = "Hex colors cycled across iterations for visual alternation",
                Type = SuggestionType.Property,
                SortPriority = 40
            }
        };

        // Union block-specific properties
        private static readonly List<SmartCompleteSuggestion> UnionBlockProperties = new List<SmartCompleteSuggestion>
        {
            new SmartCompleteSuggestion
            {
                DisplayText = "unionCondition",
                InsertText = "\"unionCondition\": \"var:\"",
                CursorOffset = -1,
                Icon = "[uni]",
                TypeHint = "string",
                Documentation = "Expression selecting which union variant to parse",
                Type = SuggestionType.Property,
                SortPriority = 10
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "variants",
                InsertText = "\"variants\": []",
                CursorOffset = -1,
                Icon = "[vars]",
                TypeHint = "array",
                Documentation = "Array of variant block definitions for union parse paths",
                Type = SuggestionType.Property,
                SortPriority = 20
            }
        };

        // Field properties
        private static readonly List<SmartCompleteSuggestion> FieldProperties = new List<SmartCompleteSuggestion>
        {
            new SmartCompleteSuggestion
            {
                DisplayText = "type",
                InsertText = "\"type\": \"\"",
                CursorOffset = -1,
                Icon = "ðŸ·ï¸",
                TypeHint = "string",
                Documentation = "Data type: uint8, uint16, uint32, uint64, int8-int64, float, double, string, ascii, utf8, utf16, bytes",
                Type = SuggestionType.Property,
                SortPriority = 10
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "name",
                InsertText = "\"name\": \"\"",
                CursorOffset = -1,
                Icon = "ðŸ“›",
                TypeHint = "string",
                Documentation = "Field name (will be displayed in parsed fields panel)",
                Type = SuggestionType.Property,
                SortPriority = 20
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "length",
                InsertText = "\"length\": ",
                Icon = "ðŸ“",
                TypeHint = "number | string",
                Documentation = "Field length in bytes (for string/bytes types) - can be number or calc:/var: expression",
                Type = SuggestionType.Property,
                SortPriority = 30
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "value",
                InsertText = "\"value\": \"\"",
                CursorOffset = -1,
                Icon = "ðŸ’Ž",
                TypeHint = "any",
                Documentation = "Expected value (for signature validation)",
                Type = SuggestionType.Property,
                SortPriority = 40
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "offset",
                InsertText = "\"offset\": ",
                Icon = "ðŸ“",
                TypeHint = "number | string",
                Documentation = "Absolute or relative offset - can be number or calc:/var: expression",
                Type = SuggestionType.Property,
                SortPriority = 50
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "endianness",
                InsertText = "\"endianness\": \"little\"",
                CursorOffset = -1,
                Icon = "ðŸ”„",
                TypeHint = "string",
                Documentation = "Byte order: \"little\" or \"big\" (default: little)",
                Type = SuggestionType.Property,
                SortPriority = 60
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "varName",
                InsertText = "\"varName\": \"\"",
                CursorOffset = -1,
                Icon = "ðŸ”¤",
                TypeHint = "string",
                Documentation = "Variable name to store this field's value (for use in var: references)",
                Type = SuggestionType.Property,
                SortPriority = 70
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "storeAs",
                InsertText = "\"storeAs\": \"\"",
                CursorOffset = -1,
                Icon = "[>v]",
                TypeHint = "string",
                Documentation = "Store parsed field value under this variable name for var: references",
                Type = SuggestionType.Property,
                SortPriority = 71
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "color",
                InsertText = "\"color\": \"#\"",
                CursorOffset = -1,
                Icon = "[clr]",
                TypeHint = "string",
                Documentation = "Hex color for this field highlight (#RRGGBB)",
                Type = SuggestionType.Property,
                SortPriority = 72
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "opacity",
                InsertText = "\"opacity\": 0.4",
                Icon = "[opa]",
                TypeHint = "number",
                Documentation = "Field highlight opacity in hex editor view (0.0-1.0)",
                Type = SuggestionType.Property,
                SortPriority = 73
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "description",
                InsertText = "\"description\": \"\"",
                CursorOffset = -1,
                Icon = "[dsc]",
                TypeHint = "string",
                Documentation = "Detailed description shown in parsed fields panel tooltip",
                Type = SuggestionType.Property,
                SortPriority = 74
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "hidden",
                InsertText = "\"hidden\": false",
                Icon = "[hid]",
                TypeHint = "bool",
                Documentation = "Parse this field but hide it from the parsed fields panel",
                Type = SuggestionType.Property,
                SortPriority = 75
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "valueMap",
                InsertText = "\"valueMap\": {\n    \"0\": \"\",\n    \"1\": \"\"\n  }",
                CursorOffset = -4,
                Icon = "[map]",
                TypeHint = "object",
                Documentation = "Maps raw numeric values to human-readable labels",
                Type = SuggestionType.Property,
                SortPriority = 76
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "bitfields",
                InsertText = "\"bitfields\": [\n    { \"bit\": 0, \"name\": \"\", \"description\": \"\" }\n  ]",
                CursorOffset = -4,
                Icon = "[bit]",
                TypeHint = "array",
                Documentation = "Bit-level field definitions (each entry: bit, name, description)",
                Type = SuggestionType.Property,
                SortPriority = 77
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "offsetFrom",
                InsertText = "\"offsetFrom\": \"var:\"",
                CursorOffset = -1,
                Icon = "[off]",
                TypeHint = "string",
                Documentation = "Compute field offset relative to a stored variable (var: reference)",
                Type = SuggestionType.Property,
                SortPriority = 78
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "offsetAdd",
                InsertText = "\"offsetAdd\": 0",
                Icon = "[+of]",
                TypeHint = "number",
                Documentation = "Additional bytes added to the computed or absolute offset",
                Type = SuggestionType.Property,
                SortPriority = 79
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "mappedValueStoreAs",
                InsertText = "\"mappedValueStoreAs\": \"\"",
                CursorOffset = -1,
                Icon = "[mv>]",
                TypeHint = "string",
                Documentation = "Store the mapped (human-readable) value instead of raw numeric value",
                Type = SuggestionType.Property,
                SortPriority = 80
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "validationRules",
                InsertText = "\"validationRules\": {}",
                CursorOffset = -1,
                Icon = "[vld]",
                TypeHint = "object",
                Documentation = "Validation constraints: min, max, pattern (regex), allowedValues",
                Type = SuggestionType.Property,
                SortPriority = 81
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "until",
                InsertText = "\"until\": \"00\"",
                CursorOffset = -1,
                Icon = "[end]",
                TypeHint = "string",
                Documentation = "Sentinel pattern: read until this hex sequence (e.g. \"00\" for null terminator). When set, length is optional.",
                Type = SuggestionType.Property,
                SortPriority = 82
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "maxLength",
                InsertText = "\"maxLength\": 4096",
                Icon = "[cap]",
                TypeHint = "number",
                Documentation = "Safety cap in bytes when using 'until' and the pattern is not found. Recommended when until is present.",
                Type = SuggestionType.Property,
                SortPriority = 83
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "untilInclusive",
                InsertText = "\"untilInclusive\": true",
                Icon = "[inc]",
                TypeHint = "bool",
                Documentation = "When true, the sentinel pattern bytes are included in the field length (default: false).",
                Type = SuggestionType.Property,
                SortPriority = 84
            }
        };

        // Block types (keywords)
        private static readonly List<SmartCompleteSuggestion> BlockTypes = new List<SmartCompleteSuggestion>
        {
            new SmartCompleteSuggestion
            {
                DisplayText = "signature",
                InsertText = "\"signature\"",
                Icon = "âœï¸",
                TypeHint = "keyword",
                Documentation = "Signature block - validates magic bytes or file markers",
                Type = SuggestionType.Keyword,
                SortPriority = 10
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "field",
                InsertText = "\"field\"",
                Icon = "ðŸ“„",
                TypeHint = "keyword",
                Documentation = "Field block - reads and parses a data field",
                Type = SuggestionType.Keyword,
                SortPriority = 20
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "conditional",
                InsertText = "\"conditional\"",
                Icon = "â“",
                TypeHint = "keyword",
                Documentation = "Conditional block - executes fields based on condition",
                Type = SuggestionType.Keyword,
                SortPriority = 30
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "loop",
                InsertText = "\"loop\"",
                Icon = "ðŸ”",
                TypeHint = "keyword",
                Documentation = "Loop block - repeats fields N times",
                Type = SuggestionType.Keyword,
                SortPriority = 40
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "action",
                InsertText = "\"action\"",
                Icon = "âš¡",
                TypeHint = "keyword",
                Documentation = "Action block - performs special operations",
                Type = SuggestionType.Keyword,
                SortPriority = 50
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "group",
                InsertText = "\"group\"",
                Icon = "[grp]",
                TypeHint = "keyword",
                Documentation = "Group block - visually groups related fields",
                Type = SuggestionType.Keyword,
                SortPriority = 60
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "metadata",
                InsertText = "\"metadata\"",
                Icon = "[meta]",
                TypeHint = "keyword",
                Documentation = "Metadata block - attaches metadata annotations to a region",
                Type = SuggestionType.Keyword,
                SortPriority = 61
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "union",
                InsertText = "\"union\"",
                Icon = "[uni]",
                TypeHint = "keyword",
                Documentation = "Union block - overlapping variants parsed at the same offset",
                Type = SuggestionType.Keyword,
                SortPriority = 62
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "nested",
                InsertText = "\"nested\"",
                Icon = "[nest]",
                TypeHint = "keyword",
                Documentation = "Nested block - embeds a referenced format definition inline",
                Type = SuggestionType.Keyword,
                SortPriority = 63
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "pointer",
                InsertText = "\"pointer\"",
                Icon = "[ptr]",
                TypeHint = "keyword",
                Documentation = "Pointer block - follows a file pointer to a non-sequential offset",
                Type = SuggestionType.Keyword,
                SortPriority = 64
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "repeating",
                InsertText = "\"repeating\"",
                Icon = "[rep]",
                TypeHint = "keyword",
                Documentation = "Repeating block - parses a structure repeatedly with colored cycling",
                Type = SuggestionType.Keyword,
                SortPriority = 65
            }
        };

        // Value types (data types)
        private static readonly List<SmartCompleteSuggestion> ValueTypes = new List<SmartCompleteSuggestion>
        {
            new SmartCompleteSuggestion("\"uint8\"", "Unsigned 8-bit integer (0-255)") { Icon = "ðŸ”¢", TypeHint = "type", Type = SuggestionType.ValueType, SortPriority = 10 },
            new SmartCompleteSuggestion("\"uint16\"", "Unsigned 16-bit integer (0-65535)") { Icon = "ðŸ”¢", TypeHint = "type", Type = SuggestionType.ValueType, SortPriority = 11 },
            new SmartCompleteSuggestion("\"uint32\"", "Unsigned 32-bit integer") { Icon = "ðŸ”¢", TypeHint = "type", Type = SuggestionType.ValueType, SortPriority = 12 },
            new SmartCompleteSuggestion("\"uint64\"", "Unsigned 64-bit integer") { Icon = "ðŸ”¢", TypeHint = "type", Type = SuggestionType.ValueType, SortPriority = 13 },
            new SmartCompleteSuggestion("\"int8\"", "Signed 8-bit integer (-128 to 127)") { Icon = "ðŸ”¢", TypeHint = "type", Type = SuggestionType.ValueType, SortPriority = 20 },
            new SmartCompleteSuggestion("\"int16\"", "Signed 16-bit integer") { Icon = "ðŸ”¢", TypeHint = "type", Type = SuggestionType.ValueType, SortPriority = 21 },
            new SmartCompleteSuggestion("\"int32\"", "Signed 32-bit integer") { Icon = "ðŸ”¢", TypeHint = "type", Type = SuggestionType.ValueType, SortPriority = 22 },
            new SmartCompleteSuggestion("\"int64\"", "Signed 64-bit integer") { Icon = "ðŸ”¢", TypeHint = "type", Type = SuggestionType.ValueType, SortPriority = 23 },
            new SmartCompleteSuggestion("\"float\"", "32-bit floating point") { Icon = "ðŸ”¢", TypeHint = "type", Type = SuggestionType.ValueType, SortPriority = 30 },
            new SmartCompleteSuggestion("\"double\"", "64-bit floating point") { Icon = "ðŸ”¢", TypeHint = "type", Type = SuggestionType.ValueType, SortPriority = 31 },
            new SmartCompleteSuggestion("\"string\"", "String with specified length") { Icon = "ðŸ”¤", TypeHint = "type", Type = SuggestionType.ValueType, SortPriority = 40 },
            new SmartCompleteSuggestion("\"ascii\"", "ASCII-encoded string") { Icon = "ðŸ”¤", TypeHint = "type", Type = SuggestionType.ValueType, SortPriority = 41 },
            new SmartCompleteSuggestion("\"utf8\"", "UTF-8 encoded string") { Icon = "ðŸ”¤", TypeHint = "type", Type = SuggestionType.ValueType, SortPriority = 42 },
            new SmartCompleteSuggestion("\"utf16\"", "UTF-16 encoded string") { Icon = "ðŸ”¤", TypeHint = "type", Type = SuggestionType.ValueType, SortPriority = 43 },
            new SmartCompleteSuggestion("\"bytes\"", "Raw byte array") { Icon = "ðŸ“¦", TypeHint = "type", Type = SuggestionType.ValueType, SortPriority = 50 }
        };

        // Detection properties (Phase 16)
        private static readonly List<SmartCompleteSuggestion> DetectionProperties = new List<SmartCompleteSuggestion>
        {
            new SmartCompleteSuggestion
            {
                DisplayText = "signatures",
                InsertText = "\"signatures\": []",
                CursorOffset = -1,
                Icon = "âœï¸",
                TypeHint = "array",
                Documentation = "Array of signature objects for format detection",
                Type = SuggestionType.Property,
                SortPriority = 10
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "offset",
                InsertText = "\"offset\": 0",
                Icon = "ðŸ“",
                TypeHint = "number",
                Documentation = "Byte offset where signature is located",
                Type = SuggestionType.Property,
                SortPriority = 20
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "value",
                InsertText = "\"value\": \"\"",
                CursorOffset = -1,
                Icon = "ðŸ’Ž",
                TypeHint = "string",
                Documentation = "Expected signature bytes (hex string)",
                Type = SuggestionType.Property,
                SortPriority = 30
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "description",
                InsertText = "\"description\": \"\"",
                CursorOffset = -1,
                Icon = "ðŸ“",
                TypeHint = "string",
                Documentation = "Human-readable description of this signature",
                Type = SuggestionType.Property,
                SortPriority = 40
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "weight",
                InsertText = "\"weight\": 1.0",
                Icon = "[wgt]",
                TypeHint = "number",
                Documentation = "Detection confidence weight for this signature (0.0-1.0)",
                Type = SuggestionType.Property,
                SortPriority = 50
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "minConfidence",
                InsertText = "\"minConfidence\": 0.7",
                Icon = "[min]",
                TypeHint = "number",
                Documentation = "Minimum confidence threshold to accept this format (0.0-1.0)",
                Type = SuggestionType.Property,
                SortPriority = 60
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "maxConfidence",
                InsertText = "\"maxConfidence\": 1.0",
                Icon = "[max]",
                TypeHint = "number",
                Documentation = "Maximum confidence cap for this format detection (0.0-1.0)",
                Type = SuggestionType.Property,
                SortPriority = 61
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "minFileSize",
                InsertText = "\"minFileSize\": 8",
                Icon = "[sz]",
                TypeHint = "number",
                Documentation = "Minimum file size in bytes required to attempt detection",
                Type = SuggestionType.Property,
                SortPriority = 70
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "isTextFormat",
                InsertText = "\"isTextFormat\": false",
                Icon = "[txt]",
                TypeHint = "bool",
                Documentation = "True if format is text-based (affects encoding detection strategy)",
                Type = SuggestionType.Property,
                SortPriority = 80
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "entropyHints",
                InsertText = "\"entropyHints\": {}",
                CursorOffset = -1,
                Icon = "[ent]",
                TypeHint = "object",
                Documentation = "Entropy analysis hints: expectedMin, expectedMax for compressed/encrypted regions",
                Type = SuggestionType.Property,
                SortPriority = 90
            }
        };

        // Endianness values (Phase 16)
        private static readonly List<SmartCompleteSuggestion> EndiannessValues = new List<SmartCompleteSuggestion>
        {
            new SmartCompleteSuggestion("\"little\"", "Little-endian byte order (x86, ARM)") { Icon = "ðŸ”„", TypeHint = "value", Type = SuggestionType.Value, SortPriority = 10 },
            new SmartCompleteSuggestion("\"big\"", "Big-endian byte order (network, SPARC)") { Icon = "ðŸ”„", TypeHint = "value", Type = SuggestionType.Value, SortPriority = 20 }
        };

        // Snippets - Complete code templates (Phase 16)
        private static readonly List<SmartCompleteSuggestion> Snippets = new List<SmartCompleteSuggestion>
        {
            new SmartCompleteSuggestion
            {
                DisplayText = "snippet:signature",
                InsertText = "{\n  \"type\": \"signature\",\n  \"name\": \"Magic bytes\",\n  \"value\": \"\"\n}",
                CursorOffset = -4,
                Icon = "ðŸ“‹",
                TypeHint = "snippet",
                Documentation = "Complete signature block template",
                Type = SuggestionType.Snippet,
                SortPriority = 100
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "snippet:field",
                InsertText = "{\n  \"type\": \"field\",\n  \"name\": \"\",\n  \"valueType\": \"uint32\",\n  \"endianness\": \"little\"\n}",
                CursorOffset = -48,
                Icon = "ðŸ“‹",
                TypeHint = "snippet",
                Documentation = "Complete field block template",
                Type = SuggestionType.Snippet,
                SortPriority = 101
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "snippet:loop",
                InsertText = "{\n  \"type\": \"loop\",\n  \"name\": \"\",\n  \"count\": 1,\n  \"fields\": []\n}",
                CursorOffset = -18,
                Icon = "ðŸ“‹",
                TypeHint = "snippet",
                Documentation = "Complete loop block template",
                Type = SuggestionType.Snippet,
                SortPriority = 102
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "snippet:conditional",
                InsertText = "{\n  \"type\": \"conditional\",\n  \"name\": \"\",\n  \"condition\": \"var:someVar == 1\",\n  \"fields\": []\n}",
                CursorOffset = -18,
                Icon = "ðŸ“‹",
                TypeHint = "snippet",
                Documentation = "Complete conditional block template",
                Type = SuggestionType.Snippet,
                SortPriority = 103
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "snippet:format",
                InsertText = "{\n  \"formatName\": \"\",\n  \"version\": \"1.0\",\n  \"description\": \"\",\n  \"extensions\": [\".ext\"],\n  \"author\": \"\",\n  \"detection\": {\n    \"signatures\": []\n  },\n  \"blocks\": []\n}",
                CursorOffset = -92,
                Icon = "ðŸ“‹",
                TypeHint = "snippet",
                Documentation = "Complete format definition template",
                Type = SuggestionType.Snippet,
                SortPriority = 1
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "snippet:colored-field",
                InsertText = "{\n  \"type\": \"field\",\n  \"name\": \"\",\n  \"valueType\": \"uint32\",\n  \"offset\": 0,\n  \"color\": \"#4FC3F7\",\n  \"opacity\": 0.4,\n  \"storeAs\": \"\",\n  \"description\": \"\"\n}",
                CursorOffset = -4,
                Icon = "[clr]",
                TypeHint = "snippet",
                Documentation = "Field with visual highlight color, opacity, and variable storage",
                Type = SuggestionType.Snippet,
                SortPriority = 104
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "snippet:valuemap-field",
                InsertText = "{\n  \"type\": \"field\",\n  \"name\": \"\",\n  \"valueType\": \"uint16\",\n  \"offset\": 0,\n  \"color\": \"#CE93D8\",\n  \"opacity\": 0.4,\n  \"valueMap\": {\n    \"0\": \"\",\n    \"1\": \"\"\n  },\n  \"description\": \"\"\n}",
                CursorOffset = -4,
                Icon = "[map]",
                TypeHint = "snippet",
                Documentation = "Field with a valueMap for human-readable enum labels",
                Type = SuggestionType.Snippet,
                SortPriority = 105
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "snippet:bitfield-entry",
                InsertText = "{ \"bit\": 0, \"name\": \"\", \"description\": \"\" }",
                CursorOffset = -4,
                Icon = "[bit]",
                TypeHint = "snippet",
                Documentation = "Single bit-level field entry (used inside a bitfields array)",
                Type = SuggestionType.Snippet,
                SortPriority = 106
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "snippet:conditional-block",
                InsertText = "{\n  \"type\": \"conditional\",\n  \"name\": \"\",\n  \"condition\": \"var: == 1\",\n  \"then\": [],\n  \"else\": []\n}",
                CursorOffset = -4,
                Icon = "[if]",
                TypeHint = "snippet",
                Documentation = "Conditional block with then/else branches",
                Type = SuggestionType.Snippet,
                SortPriority = 107
            },
            new SmartCompleteSuggestion
            {
                DisplayText = "snippet:repeating-block",
                InsertText = "{\n  \"type\": \"repeating\",\n  \"name\": \"\",\n  \"count\": \"var:\",\n  \"indexVar\": \"i\",\n  \"colorCycle\": [\"#4FC3F7\", \"#81C784\"],\n  \"fields\": []\n}",
                CursorOffset = -4,
                Icon = "[rep]",
                TypeHint = "snippet",
                Documentation = "Repeating block with loop index, colored alternation, and fields",
                Type = SuggestionType.Snippet,
                SortPriority = 108
            }
        };

        #endregion

        #region Public Methods

        /// <summary>
        /// Get SmartComplete suggestions for given context
        /// </summary>
        public List<SmartCompleteSuggestion> GetSuggestions(SmartCompleteContext context, LanguageDefinition? language = null)
        {
            if (context == null)
                return new List<SmartCompleteSuggestion>();

            // Only provide whfmt/JSON format-definition snippets when in a whfmt or json file.
            // For all other languages (C#, Python, JS…) return empty so the popup stays clean
            // when LSP is unavailable rather than polluting it with format snippets.
            if (language is not null &&
                !string.Equals(language.Id, "json",  StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(language.Id, "whfmt", StringComparison.OrdinalIgnoreCase))
                return new List<SmartCompleteSuggestion>();

            try
            {
                // Determine context type
                var contextType = DetermineContextType(context);

                // Get suggestions based on context
                List<SmartCompleteSuggestion> suggestions;

                switch (contextType)
                {
                    case ContextType.Root:
                        suggestions = new List<SmartCompleteSuggestion>(RootProperties);
                        // Add snippets at root level
                        suggestions.AddRange(Snippets);
                        break;

                    case ContextType.Block:
                        suggestions = new List<SmartCompleteSuggestion>(BlockProperties);
                        // Merge block-type-specific properties based on the "type" declared in the current block
                        var blockType = GetCurrentBlockType(context);
                        if (blockType == "conditional")
                            suggestions.AddRange(ConditionalBlockProperties);
                        else if (blockType == "loop" || blockType == "repeating")
                            suggestions.AddRange(LoopBlockProperties);
                        else if (blockType == "union")
                            suggestions.AddRange(UnionBlockProperties);
                        else if (blockType == null)
                        {
                            // Type not yet declared: offer all type-specific props so author sees options
                            suggestions.AddRange(ConditionalBlockProperties);
                            suggestions.AddRange(LoopBlockProperties);
                            suggestions.AddRange(UnionBlockProperties);
                        }
                        break;

                    case ContextType.Field:
                        suggestions = new List<SmartCompleteSuggestion>(FieldProperties);
                        break;

                    case ContextType.BlockTypeValue:
                        suggestions = new List<SmartCompleteSuggestion>(BlockTypes);
                        break;

                    case ContextType.FieldTypeValue:
                        suggestions = new List<SmartCompleteSuggestion>(ValueTypes);
                        break;

                    case ContextType.Detection:
                        suggestions = new List<SmartCompleteSuggestion>(DetectionProperties);
                        break;

                    case ContextType.EndiannessValue:
                        suggestions = new List<SmartCompleteSuggestion>(EndiannessValues);
                        break;

                    case ContextType.BlocksArray:
                        // Inside blocks array - suggest block snippets
                        suggestions = new List<SmartCompleteSuggestion>
                        {
                            Snippets.First(s => s.DisplayText == "snippet:signature"),
                            Snippets.First(s => s.DisplayText == "snippet:field"),
                            Snippets.First(s => s.DisplayText == "snippet:loop"),
                            Snippets.First(s => s.DisplayText == "snippet:conditional"),
                            Snippets.First(s => s.DisplayText == "snippet:colored-field"),
                            Snippets.First(s => s.DisplayText == "snippet:valuemap-field"),
                            Snippets.First(s => s.DisplayText == "snippet:conditional-block"),
                            Snippets.First(s => s.DisplayText == "snippet:repeating-block")
                        };
                        break;

                    default:
                        suggestions = new List<SmartCompleteSuggestion>();
                        break;
                }

                // Sort by priority
                return suggestions.OrderBy(s => s.SortPriority).ThenBy(s => s.DisplayText).ToList();
            }
            catch (Exception)
            {
                // Return empty list on error
                return new List<SmartCompleteSuggestion>();
            }
        }

        #endregion

        #region Context Analysis

        /// <summary>
        /// Determine the context type based on cursor position and document structure
        /// </summary>
        private ContextType DetermineContextType(SmartCompleteContext context)
        {
            var line = context.CurrentLine?.Trim() ?? string.Empty;

            // Phase 16: Check if we're after "endianness": for endianness values
            if (line.Contains("\"endianness\":"))
            {
                return ContextType.EndiannessValue;
            }

            // Check if we're after "type": for block or field type values
            if (line.Contains("\"type\":"))
            {
                // Determine if we're in a block or field context by looking at parent structure
                if (IsInBlockContext(context))
                    return ContextType.BlockTypeValue;
                else if (IsInFieldContext(context))
                    return ContextType.FieldTypeValue;
            }

            // Phase 16: Check if we're in detection object
            if (IsInDetectionObject(context))
            {
                return ContextType.Detection;
            }

            // Check if we're in blocks array
            if (IsInBlocksArray(context))
            {
                // Phase 16: If we just opened a brace in blocks array, suggest snippets
                if (line.TrimEnd().EndsWith("{"))
                {
                    return ContextType.BlocksArray;
                }

                if (IsInFieldsArray(context))
                    return ContextType.Field;
                else
                    return ContextType.Block;
            }

            // Default to root context
            return ContextType.Root;
        }

        /// <summary>
        /// Check if cursor is inside blocks array
        /// </summary>
        private bool IsInBlocksArray(SmartCompleteContext context)
        {
            if (string.IsNullOrEmpty(context.DocumentText))
                return false;

            // Simple heuristic: check if "blocks" appears before cursor position
            var textBeforeCursor = GetTextBeforeCursor(context);
            return textBeforeCursor.Contains("\"blocks\"");
        }

        /// <summary>
        /// Check if cursor is inside fields array (within a block)
        /// </summary>
        private bool IsInFieldsArray(SmartCompleteContext context)
        {
            if (string.IsNullOrEmpty(context.DocumentText))
                return false;

            var textBeforeCursor = GetTextBeforeCursor(context);
            return textBeforeCursor.Contains("\"fields\"");
        }

        /// <summary>
        /// Check if cursor is in block context (for type value suggestions)
        /// </summary>
        private bool IsInBlockContext(SmartCompleteContext context)
        {
            var textBeforeCursor = GetTextBeforeCursor(context);

            // Count braces to determine nesting level
            int braceCount = 0;
            bool inBlocks = false;
            bool inFields = false;

            for (int i = 0; i < textBeforeCursor.Length; i++)
            {
                if (textBeforeCursor[i] == '{')
                    braceCount++;
                else if (textBeforeCursor[i] == '}')
                    braceCount--;

                // Check for "blocks" keyword
                if (i >= 7 && textBeforeCursor.Substring(i - 7, 8) == "\"blocks\"")
                    inBlocks = true;

                // Check for "fields" keyword (resets to field context)
                if (i >= 7 && textBeforeCursor.Substring(i - 7, 8) == "\"fields\"")
                    inFields = true;
            }

            // We're in block context if we're inside blocks but not inside fields
            return inBlocks && !inFields;
        }

        /// <summary>
        /// Check if cursor is in field context (for type value suggestions)
        /// </summary>
        private bool IsInFieldContext(SmartCompleteContext context)
        {
            var textBeforeCursor = GetTextBeforeCursor(context);
            return textBeforeCursor.Contains("\"fields\"");
        }

        /// <summary>
        /// Check if cursor is inside detection object (Phase 16)
        /// </summary>
        private bool IsInDetectionObject(SmartCompleteContext context)
        {
            var textBeforeCursor = GetTextBeforeCursor(context);

            // Simple heuristic: check if "detection" keyword appears and we're inside its braces
            int detectionIndex = textBeforeCursor.LastIndexOf("\"detection\"");
            if (detectionIndex == -1)
                return false;

            // Count braces after "detection" keyword
            var afterDetection = textBeforeCursor.Substring(detectionIndex);
            int openBraces = 0;
            foreach (char c in afterDetection)
            {
                if (c == '{') openBraces++;
                else if (c == '}') openBraces--;
            }

            // We're inside detection if there's at least one unclosed brace
            return openBraces > 0;
        }

        /// <summary>
        /// Extracts the "type" value of the innermost block object containing the cursor.
        /// Returns null if no type declaration is found in the enclosing block scope.
        /// </summary>
        private string? GetCurrentBlockType(SmartCompleteContext context)
        {
            var text = GetTextBeforeCursor(context);
            if (string.IsNullOrEmpty(text))
                return null;

            // Walk backward from the cursor to find the start of the enclosing block object
            int depth = 0;
            int blockStart = -1;
            for (int i = text.Length - 1; i >= 0; i--)
            {
                if (text[i] == '}') depth++;
                else if (text[i] == '{')
                {
                    if (depth == 0) { blockStart = i; break; }
                    depth--;
                }
            }

            if (blockStart < 0)
                return null;

            var blockText = text.Substring(blockStart);
            var match = Regex.Match(blockText, "\"type\"\\s*:\\s*\"([^\"]+)\"");
            return match.Success ? match.Groups[1].Value : null;
        }

        /// <summary>
        /// Get all text before cursor position
        /// </summary>
        private string GetTextBeforeCursor(SmartCompleteContext context)
        {
            if (string.IsNullOrEmpty(context.DocumentText))
                return string.Empty;

            try
            {
                var lines = context.DocumentText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                var beforeLines = lines.Take(context.CursorPosition.Line).ToList();

                if (context.CursorPosition.Line < lines.Length)
                {
                    var currentLineBeforeCursor = lines[context.CursorPosition.Line].Substring(0,
                        Math.Min(context.CursorPosition.Column, lines[context.CursorPosition.Line].Length));
                    beforeLines.Add(currentLineBeforeCursor);
                }

                return string.Join("\n", beforeLines);
            }
            catch
            {
                return string.Empty;
            }
        }

        #endregion

        #region Context Types

        private enum ContextType
        {
            Root,              // Root level of format definition
            Block,             // Inside a block object (type-aware: merges conditional/loop/union props)
            Field,             // Inside a field object
            BlockTypeValue,    // Value for "type" property in block
            FieldTypeValue,    // Value for "type" property in field
            Detection,         // Inside detection object (Phase 16)
            EndiannessValue,   // Value for "endianness" property (Phase 16)
            BlocksArray        // Inside blocks array - suggest block snippets (Phase 16)
        }

        #endregion
    }
}
