//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project: WpfHexEditor.Editor.Core
// File: ValidationError.cs
// Description: Validation error model shared across all editor modules.
//              Moved from WpfHexEditor.Editor.CodeEditor.Models so that
//              StructureEditor (and future editors) can reference it without
//              taking a dependency on the CodeEditor assembly.
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.Core.Validation;

/// <summary>Severity level of a validation diagnostic.</summary>
public enum ValidationSeverity
{
    /// <summary>Informational message — no action required.</summary>
    Info,
    /// <summary>Non-critical issue that should be reviewed.</summary>
    Warning,
    /// <summary>Critical error that prevents the format from working.</summary>
    Error,
}

/// <summary>Validation layer that detected the diagnostic.</summary>
public enum ValidationLayer
{
    /// <summary>Layer 1: JSON syntax errors.</summary>
    JsonSyntax,
    /// <summary>Layer 2: Missing required properties (schema compliance).</summary>
    Schema,
    /// <summary>Layer 3: Format-specific rule violations.</summary>
    FormatRules,
    /// <summary>Layer 4: Semantic errors (invalid references, etc.).</summary>
    Semantic,
    /// <summary>Layer 5: Diagnostics from an external Language Server.</summary>
    Lsp,
}

/// <summary>
/// A single validation diagnostic with source location and severity.
/// Produced by <c>FormatSchemaValidator</c> and any LSP integration.
/// </summary>
public class ValidationError
{
    /// <summary>Line number where the error occurs (0-based).</summary>
    public int Line { get; set; }

    /// <summary>Column where the error starts (0-based).</summary>
    public int Column { get; set; }

    /// <summary>Length of the error span (used for underlining).</summary>
    public int Length { get; set; }

    /// <summary>Human-readable error message.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Severity level of this diagnostic.</summary>
    public ValidationSeverity Severity { get; set; }

    /// <summary>Optional error code for categorisation.</summary>
    public string ErrorCode { get; set; } = string.Empty;

    /// <summary>Validation layer that produced this diagnostic.</summary>
    public ValidationLayer Layer { get; set; }

    /// <summary>
    /// Identifies the sub-system that produced this error (e.g. "lsp", "schema").
    /// Used to selectively replace error sets on incremental updates.
    /// </summary>
    public string? Source { get; set; }

    /// <summary>Initialises a diagnostic with default Error severity.</summary>
    public ValidationError()
    {
        Severity = ValidationSeverity.Error;
        Length   = 1;
    }

    /// <summary>Initialises a diagnostic with explicit location and message.</summary>
    public ValidationError(int line, int column, string message,
                           ValidationSeverity severity = ValidationSeverity.Error)
    {
        Line     = line;
        Column   = column;
        Message  = message;
        Severity = severity;
        Length   = 1;
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"[{Severity}] Line {Line + 1}, Col {Column + 1}: {Message}";
}
