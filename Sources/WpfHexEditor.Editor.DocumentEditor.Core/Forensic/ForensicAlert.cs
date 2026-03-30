// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor.Core
// File: Forensic/ForensicAlert.cs
// Description: A single anomaly detected during forensic analysis.
// ==========================================================

using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Editor.DocumentEditor.Core.Forensic;

/// <summary>Severity level of a <see cref="ForensicAlert"/>.</summary>
public enum ForensicSeverity
{
    /// <summary>Informational — offset debug data, no anomaly.</summary>
    Info,
    /// <summary>Warning — suspicious but not necessarily corrupt.</summary>
    Warning,
    /// <summary>Error — structural corruption, invalid encoding, or failed assertion.</summary>
    Error
}

/// <summary>Type of forensic finding.</summary>
public enum ForensicAlertKind
{
    /// <summary>Unexpected gap between consecutive block offsets.</summary>
    OffsetGap,
    /// <summary>Two blocks share overlapping byte ranges.</summary>
    OffsetOverlap,
    /// <summary>Text run contains bytes that are invalid in the declared encoding.</summary>
    InvalidEncoding,
    /// <summary>A WHFMT assertion was not satisfied.</summary>
    AssertionFailed,
    /// <summary>DOCX/DOTX file contains VBA macro storage.</summary>
    MacroPresent,
    /// <summary>Author / creation-date metadata looks anomalous.</summary>
    SuspiciousMetadata,
    /// <summary>Parser threw an exception — model is partial.</summary>
    ParseError,
    /// <summary>Generic / loader-defined alert.</summary>
    Custom
}

/// <summary>
/// A single forensic finding produced by <see cref="ForensicAnalyzer"/>.
/// </summary>
public sealed class ForensicAlert
{
    /// <summary>Alert kind.</summary>
    public required ForensicAlertKind Kind { get; init; }

    /// <summary>Severity level.</summary>
    public required ForensicSeverity Severity { get; init; }

    /// <summary>Human-readable description shown in the tooltip and panel.</summary>
    public required string Description { get; init; }

    /// <summary>
    /// The block associated with this alert, if applicable.
    /// Null for file-level alerts (e.g. <see cref="ForensicAlertKind.MacroPresent"/>).
    /// </summary>
    public DocumentBlock? Block { get; init; }

    /// <summary>Byte offset in the source file where the anomaly was detected.</summary>
    public long? Offset { get; init; }

    /// <summary>Optional suggestion shown below the description.</summary>
    public string? Suggestion { get; init; }
}
