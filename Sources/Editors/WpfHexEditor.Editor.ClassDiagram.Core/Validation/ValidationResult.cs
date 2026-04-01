// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: Validation/ValidationResult.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Immutable record describing a single diagnostic produced by
//     DiagramValidator.  Carries a severity level, a human-readable
//     message, and an optional class identifier pinpointing the source.
//
// Architecture Notes:
//     Severity enum is co-located in this file for cohesion — it is
//     small and only used in validation context.
//     Record type enables trivial deduplication and LINQ operations by
//     callers.
// ==========================================================

namespace WpfHexEditor.Editor.ClassDiagram.Core.Validation;

/// <summary>Severity level of a diagram validation diagnostic.</summary>
public enum ValidationSeverity
{
    /// <summary>A structural problem that must be fixed before export.</summary>
    Error,

    /// <summary>A potentially problematic pattern that should be reviewed.</summary>
    Warning,

    /// <summary>An informational note that does not require action.</summary>
    Info
}

/// <summary>
/// A single diagnostic entry produced by <see cref="DiagramValidator"/>.
/// </summary>
/// <param name="Severity">How severe the issue is.</param>
/// <param name="Message">Human-readable description of the issue.</param>
/// <param name="ClassId">
/// The <see cref="Model.ClassNode.Id"/> of the node that triggered this diagnostic,
/// or <see langword="null"/> for document-level issues.
/// </param>
public sealed record ValidationResult(
    ValidationSeverity Severity,
    string Message,
    string? ClassId);
