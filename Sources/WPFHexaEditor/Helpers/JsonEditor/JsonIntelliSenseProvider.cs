//////////////////////////////////////////////
// Apache 2.0  - 2026
// Custom JsonEditor - IntelliSense Provider (Phase 4)
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com)
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using WpfHexaEditor.Models.JsonEditor;

namespace WpfHexaEditor.Helpers.JsonEditor
{
    /// <summary>
    /// Provides context-aware IntelliSense suggestions for format definition JSON.
    /// Phase 4: Root level + blocks contexts.
    /// Phase 7 will add all contexts, snippets, and tooltips.
    /// </summary>
    public class JsonIntelliSenseProvider
    {
        #region Suggestion Data

        // Root level properties for format definitions
        private static readonly List<IntelliSenseSuggestion> RootProperties = new List<IntelliSenseSuggestion>
        {
            new IntelliSenseSuggestion
            {
                DisplayText = "formatName",
                InsertText = "\"formatName\": \"\"",
                CursorOffset = -1,
                Icon = "🏷️",
                TypeHint = "string",
                Documentation = "Unique identifier for this format definition (e.g., \"PNG Image\", \"ELF Executable\")",
                Type = SuggestionType.Property,
                SortPriority = 10
            },
            new IntelliSenseSuggestion
            {
                DisplayText = "version",
                InsertText = "\"version\": \"1.0\"",
                CursorOffset = -1,
                Icon = "🔢",
                TypeHint = "string",
                Documentation = "Version number of this format definition (e.g., \"1.0\", \"2.1\")",
                Type = SuggestionType.Property,
                SortPriority = 20
            },
            new IntelliSenseSuggestion
            {
                DisplayText = "description",
                InsertText = "\"description\": \"\"",
                CursorOffset = -1,
                Icon = "📝",
                TypeHint = "string",
                Documentation = "Human-readable description of the file format",
                Type = SuggestionType.Property,
                SortPriority = 30
            },
            new IntelliSenseSuggestion
            {
                DisplayText = "extensions",
                InsertText = "\"extensions\": [\".ext\"]",
                CursorOffset = -2,
                Icon = "📁",
                TypeHint = "string[]",
                Documentation = "Array of file extensions associated with this format (e.g., [\".png\", \".jpg\"])",
                Type = SuggestionType.Property,
                SortPriority = 40
            },
            new IntelliSenseSuggestion
            {
                DisplayText = "author",
                InsertText = "\"author\": \"\"",
                CursorOffset = -1,
                Icon = "👤",
                TypeHint = "string",
                Documentation = "Author of this format definition",
                Type = SuggestionType.Property,
                SortPriority = 50
            },
            new IntelliSenseSuggestion
            {
                DisplayText = "website",
                InsertText = "\"website\": \"\"",
                CursorOffset = -1,
                Icon = "🌐",
                TypeHint = "string",
                Documentation = "Website URL with format specification or documentation",
                Type = SuggestionType.Property,
                SortPriority = 60
            },
            new IntelliSenseSuggestion
            {
                DisplayText = "detection",
                InsertText = "\"detection\": {}",
                CursorOffset = -1,
                Icon = "🔍",
                TypeHint = "object",
                Documentation = "File format detection rules (signatures, magic bytes)",
                Type = SuggestionType.Property,
                SortPriority = 70
            },
            new IntelliSenseSuggestion
            {
                DisplayText = "blocks",
                InsertText = "\"blocks\": []",
                CursorOffset = -1,
                Icon = "📦",
                TypeHint = "array",
                Documentation = "Array of data blocks that define the file structure",
                Type = SuggestionType.Property,
                SortPriority = 80
            }
        };

        // Block-level properties
        private static readonly List<IntelliSenseSuggestion> BlockProperties = new List<IntelliSenseSuggestion>
        {
            new IntelliSenseSuggestion
            {
                DisplayText = "type",
                InsertText = "\"type\": \"\"",
                CursorOffset = -1,
                Icon = "🏷️",
                TypeHint = "string",
                Documentation = "Block type: \"signature\", \"field\", \"conditional\", \"loop\", or \"action\"",
                Type = SuggestionType.Property,
                SortPriority = 10
            },
            new IntelliSenseSuggestion
            {
                DisplayText = "name",
                InsertText = "\"name\": \"\"",
                CursorOffset = -1,
                Icon = "📛",
                TypeHint = "string",
                Documentation = "Descriptive name for this block",
                Type = SuggestionType.Property,
                SortPriority = 20
            },
            new IntelliSenseSuggestion
            {
                DisplayText = "fields",
                InsertText = "\"fields\": []",
                CursorOffset = -1,
                Icon = "📋",
                TypeHint = "array",
                Documentation = "Array of field definitions within this block",
                Type = SuggestionType.Property,
                SortPriority = 30
            },
            new IntelliSenseSuggestion
            {
                DisplayText = "condition",
                InsertText = "\"condition\": \"\"",
                CursorOffset = -1,
                Icon = "❓",
                TypeHint = "string",
                Documentation = "Condition expression (for conditional blocks)",
                Type = SuggestionType.Property,
                SortPriority = 40
            },
            new IntelliSenseSuggestion
            {
                DisplayText = "count",
                InsertText = "\"count\": \"\"",
                CursorOffset = -1,
                Icon = "🔢",
                TypeHint = "string | number",
                Documentation = "Loop count (for loop blocks) - can be number or var: reference",
                Type = SuggestionType.Property,
                SortPriority = 50
            }
        };

        // Field properties
        private static readonly List<IntelliSenseSuggestion> FieldProperties = new List<IntelliSenseSuggestion>
        {
            new IntelliSenseSuggestion
            {
                DisplayText = "type",
                InsertText = "\"type\": \"\"",
                CursorOffset = -1,
                Icon = "🏷️",
                TypeHint = "string",
                Documentation = "Data type: uint8, uint16, uint32, uint64, int8-int64, float, double, string, ascii, utf8, utf16, bytes",
                Type = SuggestionType.Property,
                SortPriority = 10
            },
            new IntelliSenseSuggestion
            {
                DisplayText = "name",
                InsertText = "\"name\": \"\"",
                CursorOffset = -1,
                Icon = "📛",
                TypeHint = "string",
                Documentation = "Field name (will be displayed in parsed fields panel)",
                Type = SuggestionType.Property,
                SortPriority = 20
            },
            new IntelliSenseSuggestion
            {
                DisplayText = "length",
                InsertText = "\"length\": ",
                Icon = "📏",
                TypeHint = "number | string",
                Documentation = "Field length in bytes (for string/bytes types) - can be number or calc:/var: expression",
                Type = SuggestionType.Property,
                SortPriority = 30
            },
            new IntelliSenseSuggestion
            {
                DisplayText = "value",
                InsertText = "\"value\": \"\"",
                CursorOffset = -1,
                Icon = "💎",
                TypeHint = "any",
                Documentation = "Expected value (for signature validation)",
                Type = SuggestionType.Property,
                SortPriority = 40
            },
            new IntelliSenseSuggestion
            {
                DisplayText = "offset",
                InsertText = "\"offset\": ",
                Icon = "📍",
                TypeHint = "number | string",
                Documentation = "Absolute or relative offset - can be number or calc:/var: expression",
                Type = SuggestionType.Property,
                SortPriority = 50
            },
            new IntelliSenseSuggestion
            {
                DisplayText = "endianness",
                InsertText = "\"endianness\": \"little\"",
                CursorOffset = -1,
                Icon = "🔄",
                TypeHint = "string",
                Documentation = "Byte order: \"little\" or \"big\" (default: little)",
                Type = SuggestionType.Property,
                SortPriority = 60
            },
            new IntelliSenseSuggestion
            {
                DisplayText = "varName",
                InsertText = "\"varName\": \"\"",
                CursorOffset = -1,
                Icon = "🔤",
                TypeHint = "string",
                Documentation = "Variable name to store this field's value (for use in var: references)",
                Type = SuggestionType.Property,
                SortPriority = 70
            }
        };

        // Block types (keywords)
        private static readonly List<IntelliSenseSuggestion> BlockTypes = new List<IntelliSenseSuggestion>
        {
            new IntelliSenseSuggestion
            {
                DisplayText = "signature",
                InsertText = "\"signature\"",
                Icon = "✍️",
                TypeHint = "keyword",
                Documentation = "Signature block - validates magic bytes or file markers",
                Type = SuggestionType.Keyword,
                SortPriority = 10
            },
            new IntelliSenseSuggestion
            {
                DisplayText = "field",
                InsertText = "\"field\"",
                Icon = "📄",
                TypeHint = "keyword",
                Documentation = "Field block - reads and parses a data field",
                Type = SuggestionType.Keyword,
                SortPriority = 20
            },
            new IntelliSenseSuggestion
            {
                DisplayText = "conditional",
                InsertText = "\"conditional\"",
                Icon = "❓",
                TypeHint = "keyword",
                Documentation = "Conditional block - executes fields based on condition",
                Type = SuggestionType.Keyword,
                SortPriority = 30
            },
            new IntelliSenseSuggestion
            {
                DisplayText = "loop",
                InsertText = "\"loop\"",
                Icon = "🔁",
                TypeHint = "keyword",
                Documentation = "Loop block - repeats fields N times",
                Type = SuggestionType.Keyword,
                SortPriority = 40
            },
            new IntelliSenseSuggestion
            {
                DisplayText = "action",
                InsertText = "\"action\"",
                Icon = "⚡",
                TypeHint = "keyword",
                Documentation = "Action block - performs special operations",
                Type = SuggestionType.Keyword,
                SortPriority = 50
            }
        };

        // Value types (data types)
        private static readonly List<IntelliSenseSuggestion> ValueTypes = new List<IntelliSenseSuggestion>
        {
            new IntelliSenseSuggestion("\"uint8\"", "Unsigned 8-bit integer (0-255)") { Icon = "🔢", TypeHint = "type", Type = SuggestionType.ValueType, SortPriority = 10 },
            new IntelliSenseSuggestion("\"uint16\"", "Unsigned 16-bit integer (0-65535)") { Icon = "🔢", TypeHint = "type", Type = SuggestionType.ValueType, SortPriority = 11 },
            new IntelliSenseSuggestion("\"uint32\"", "Unsigned 32-bit integer") { Icon = "🔢", TypeHint = "type", Type = SuggestionType.ValueType, SortPriority = 12 },
            new IntelliSenseSuggestion("\"uint64\"", "Unsigned 64-bit integer") { Icon = "🔢", TypeHint = "type", Type = SuggestionType.ValueType, SortPriority = 13 },
            new IntelliSenseSuggestion("\"int8\"", "Signed 8-bit integer (-128 to 127)") { Icon = "🔢", TypeHint = "type", Type = SuggestionType.ValueType, SortPriority = 20 },
            new IntelliSenseSuggestion("\"int16\"", "Signed 16-bit integer") { Icon = "🔢", TypeHint = "type", Type = SuggestionType.ValueType, SortPriority = 21 },
            new IntelliSenseSuggestion("\"int32\"", "Signed 32-bit integer") { Icon = "🔢", TypeHint = "type", Type = SuggestionType.ValueType, SortPriority = 22 },
            new IntelliSenseSuggestion("\"int64\"", "Signed 64-bit integer") { Icon = "🔢", TypeHint = "type", Type = SuggestionType.ValueType, SortPriority = 23 },
            new IntelliSenseSuggestion("\"float\"", "32-bit floating point") { Icon = "🔢", TypeHint = "type", Type = SuggestionType.ValueType, SortPriority = 30 },
            new IntelliSenseSuggestion("\"double\"", "64-bit floating point") { Icon = "🔢", TypeHint = "type", Type = SuggestionType.ValueType, SortPriority = 31 },
            new IntelliSenseSuggestion("\"string\"", "String with specified length") { Icon = "🔤", TypeHint = "type", Type = SuggestionType.ValueType, SortPriority = 40 },
            new IntelliSenseSuggestion("\"ascii\"", "ASCII-encoded string") { Icon = "🔤", TypeHint = "type", Type = SuggestionType.ValueType, SortPriority = 41 },
            new IntelliSenseSuggestion("\"utf8\"", "UTF-8 encoded string") { Icon = "🔤", TypeHint = "type", Type = SuggestionType.ValueType, SortPriority = 42 },
            new IntelliSenseSuggestion("\"utf16\"", "UTF-16 encoded string") { Icon = "🔤", TypeHint = "type", Type = SuggestionType.ValueType, SortPriority = 43 },
            new IntelliSenseSuggestion("\"bytes\"", "Raw byte array") { Icon = "📦", TypeHint = "type", Type = SuggestionType.ValueType, SortPriority = 50 }
        };

        #endregion

        #region Public Methods

        /// <summary>
        /// Get IntelliSense suggestions for given context
        /// </summary>
        public List<IntelliSenseSuggestion> GetSuggestions(IntelliSenseContext context)
        {
            if (context == null)
                return new List<IntelliSenseSuggestion>();

            try
            {
                // Determine context type
                var contextType = DetermineContextType(context);

                // Get suggestions based on context
                List<IntelliSenseSuggestion> suggestions;

                switch (contextType)
                {
                    case ContextType.Root:
                        suggestions = new List<IntelliSenseSuggestion>(RootProperties);
                        break;

                    case ContextType.Block:
                        suggestions = new List<IntelliSenseSuggestion>(BlockProperties);
                        break;

                    case ContextType.Field:
                        suggestions = new List<IntelliSenseSuggestion>(FieldProperties);
                        break;

                    case ContextType.BlockTypeValue:
                        suggestions = new List<IntelliSenseSuggestion>(BlockTypes);
                        break;

                    case ContextType.FieldTypeValue:
                        suggestions = new List<IntelliSenseSuggestion>(ValueTypes);
                        break;

                    default:
                        suggestions = new List<IntelliSenseSuggestion>();
                        break;
                }

                // Sort by priority
                return suggestions.OrderBy(s => s.SortPriority).ThenBy(s => s.DisplayText).ToList();
            }
            catch (Exception)
            {
                // Return empty list on error
                return new List<IntelliSenseSuggestion>();
            }
        }

        #endregion

        #region Context Analysis

        /// <summary>
        /// Determine the context type based on cursor position and document structure
        /// </summary>
        private ContextType DetermineContextType(IntelliSenseContext context)
        {
            var line = context.CurrentLine?.Trim() ?? string.Empty;

            // Check if we're after "type": for block or field type values
            if (line.Contains("\"type\":"))
            {
                // Determine if we're in a block or field context by looking at parent structure
                if (IsInBlockContext(context))
                    return ContextType.BlockTypeValue;
                else if (IsInFieldContext(context))
                    return ContextType.FieldTypeValue;
            }

            // Check if we're in blocks array
            if (IsInBlocksArray(context))
            {
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
        private bool IsInBlocksArray(IntelliSenseContext context)
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
        private bool IsInFieldsArray(IntelliSenseContext context)
        {
            if (string.IsNullOrEmpty(context.DocumentText))
                return false;

            var textBeforeCursor = GetTextBeforeCursor(context);
            return textBeforeCursor.Contains("\"fields\"");
        }

        /// <summary>
        /// Check if cursor is in block context (for type value suggestions)
        /// </summary>
        private bool IsInBlockContext(IntelliSenseContext context)
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
        private bool IsInFieldContext(IntelliSenseContext context)
        {
            var textBeforeCursor = GetTextBeforeCursor(context);
            return textBeforeCursor.Contains("\"fields\"");
        }

        /// <summary>
        /// Get all text before cursor position
        /// </summary>
        private string GetTextBeforeCursor(IntelliSenseContext context)
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
            Block,             // Inside a block object
            Field,             // Inside a field object
            BlockTypeValue,    // Value for "type" property in block
            FieldTypeValue     // Value for "type" property in field
        }

        #endregion
    }
}
