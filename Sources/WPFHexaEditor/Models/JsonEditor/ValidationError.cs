//////////////////////////////////////////////
// Apache 2.0  - 2026
// Custom JsonEditor - Validation Error Model (Phase 5)
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com)
//////////////////////////////////////////////

namespace WpfHexaEditor.Models.JsonEditor
{
    /// <summary>
    /// Represents a validation error with location and severity.
    /// Used by FormatSchemaValidator to report issues in format definitions.
    /// </summary>
    public class ValidationError
    {
        /// <summary>
        /// Line number where error occurs (0-based)
        /// </summary>
        public int Line { get; set; }

        /// <summary>
        /// Column where error starts (0-based)
        /// </summary>
        public int Column { get; set; }

        /// <summary>
        /// Length of error span (for underlining)
        /// </summary>
        public int Length { get; set; }

        /// <summary>
        /// Error message
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Error severity level
        /// </summary>
        public ValidationSeverity Severity { get; set; }

        /// <summary>
        /// Error code (for categorization)
        /// </summary>
        public string ErrorCode { get; set; }

        /// <summary>
        /// Validation layer that detected this error
        /// </summary>
        public ValidationLayer Layer { get; set; }

        public ValidationError()
        {
            Severity = ValidationSeverity.Error;
            Length = 1;
        }

        public ValidationError(int line, int column, string message, ValidationSeverity severity = ValidationSeverity.Error)
        {
            Line = line;
            Column = column;
            Message = message;
            Severity = severity;
            Length = 1;
        }

        public override string ToString()
        {
            return $"[{Severity}] Line {Line + 1}, Col {Column + 1}: {Message}";
        }
    }

    /// <summary>
    /// Severity level of validation error
    /// </summary>
    public enum ValidationSeverity
    {
        Info,       // Informational message
        Warning,    // Non-critical issue
        Error       // Critical error that prevents format from working
    }

    /// <summary>
    /// Validation layer that detected the error
    /// </summary>
    public enum ValidationLayer
    {
        JsonSyntax,     // Layer 1: JSON syntax errors
        Schema,         // Layer 2: Missing required properties
        FormatRules,    // Layer 3: Format-specific rule violations
        Semantic        // Layer 4: Semantic errors (invalid references, etc.)
    }
}
