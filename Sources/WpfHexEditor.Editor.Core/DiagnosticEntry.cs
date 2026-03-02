//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Severity level of a diagnostic entry.
/// </summary>
public enum DiagnosticSeverity
{
    Error,
    Warning,
    Message
}

/// <summary>
/// Scope filter used by the error panel.
/// </summary>
public enum ErrorPanelScope
{
    Solution,
    CurrentProject,
    CurrentDocument
}

/// <summary>
/// Immutable diagnostic entry reported by an <see cref="IDiagnosticSource"/>.
/// </summary>
/// <param name="Severity">Error, Warning or Message.</param>
/// <param name="Code">Short diagnostic code, e.g. "PARSE001", "PROJ002".</param>
/// <param name="Description">Human-readable description of the issue.</param>
/// <param name="ProjectName">Name of the project (display only), or null.</param>
/// <param name="FileName">Short file name for display, or null.</param>
/// <param name="FilePath">Absolute file path for navigation, or null.</param>
/// <param name="Offset">Byte offset within the file (hex context), or null.</param>
/// <param name="Line">Line number (text/TBL/JSON context), or null.</param>
/// <param name="Column">Column number, or null.</param>
/// <param name="Tag">Arbitrary source object for advanced filtering (future use).</param>
public sealed record DiagnosticEntry(
    DiagnosticSeverity Severity,
    string             Code,
    string             Description,
    string?            ProjectName = null,
    string?            FileName    = null,
    string?            FilePath    = null,
    long?              Offset      = null,
    int?               Line        = null,
    int?               Column      = null,
    object?            Tag         = null
);
