//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7 (1M context)
//////////////////////////////////////////////
// Project: WpfHexEditor.Core.Definitions
// File: Models/Validation/WhfmtValidationIssue.cs
// Description: Result type for whfmt static validation. One issue per detected
//              problem — collected by validators, surfaced by tools / IDE /
//              whfmt-guard skill.
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Definitions.Models.Validation;

/// <summary>Severity of a whfmt validation issue.</summary>
public enum WhfmtIssueSeverity { Error, Warning, Info }

/// <summary>
/// A single issue detected by a whfmt static validator.
/// </summary>
public sealed record WhfmtValidationIssue(
    /// <summary>Stable rule identifier (e.g. "R10-001").</summary>
    string RuleId,
    /// <summary>One-line human description.</summary>
    string Message,
    WhfmtIssueSeverity Severity,
    /// <summary>JSONPath-like location inside the .whfmt (e.g. "assertions[2].expression").</summary>
    string? Path,
    /// <summary>Source string of the offending expression / value, when applicable.</summary>
    string? Source,
    /// <summary>0-based position inside <see cref="Source"/>, or -1.</summary>
    int Position);
