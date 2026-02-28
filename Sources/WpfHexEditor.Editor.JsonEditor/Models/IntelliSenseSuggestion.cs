//////////////////////////////////////////////
// Apache 2.0  - 2026
// Custom JsonEditor - IntelliSense Suggestion Model (Phase 4)
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com)
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.JsonEditor.Models
{
    /// <summary>
    /// Represents a single IntelliSense suggestion.
    /// Contains display text, insert text, documentation, and metadata.
    /// </summary>
    public class IntelliSenseSuggestion
    {
        /// <summary>
        /// Text displayed in suggestion list
        /// </summary>
        public string DisplayText { get; set; }

        /// <summary>
        /// Text to insert when suggestion is committed (if different from DisplayText)
        /// </summary>
        public string InsertText { get; set; }

        /// <summary>
        /// Icon to display (emoji or symbol)
        /// </summary>
        public string Icon { get; set; }

        /// <summary>
        /// Type hint displayed after display text (e.g., "property", "string", "uint8")
        /// </summary>
        public string TypeHint { get; set; }

        /// <summary>
        /// Documentation text shown in preview pane
        /// </summary>
        public string Documentation { get; set; }

        /// <summary>
        /// Cursor offset after insertion (0 = end of text, negative = relative to end)
        /// </summary>
        public int CursorOffset { get; set; }

        /// <summary>
        /// Sort priority (lower = higher priority)
        /// </summary>
        public int SortPriority { get; set; }

        /// <summary>
        /// Suggestion type (for filtering and grouping)
        /// </summary>
        public SuggestionType Type { get; set; }

        public IntelliSenseSuggestion()
        {
            Icon = "📄"; // Default icon
            CursorOffset = 0;
            SortPriority = 100;
            Type = SuggestionType.Property;
        }

        public IntelliSenseSuggestion(string displayText, string documentation = null)
        {
            DisplayText = displayText;
            InsertText = displayText;
            Documentation = documentation;
            Icon = "📄";
            CursorOffset = 0;
            SortPriority = 100;
            Type = SuggestionType.Property;
        }

        public override string ToString()
        {
            return $"{DisplayText} ({TypeHint})";
        }
    }

    /// <summary>
    /// Type of IntelliSense suggestion
    /// </summary>
    public enum SuggestionType
    {
        Property,       // JSON property name
        Value,          // JSON value
        Keyword,        // Format definition keyword (signature, field, conditional, etc.)
        ValueType,      // Data type (uint8, uint16, string, etc.)
        Snippet,        // Code snippet template
        Function        // Function or expression (calc:, var:)
    }
}
