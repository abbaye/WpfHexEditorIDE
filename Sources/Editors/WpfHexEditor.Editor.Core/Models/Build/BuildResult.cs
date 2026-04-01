// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: Models/Build/BuildResult.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Immutable result returned by IBuildSystem after a build operation completes.
// ==========================================================

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Describes the outcome of a build operation.
/// </summary>
public sealed record BuildResult(
    /// <summary>Whether the build completed without errors.</summary>
    bool IsSuccess,
    /// <summary>All error-level diagnostics.</summary>
    IReadOnlyList<BuildDiagnostic> Errors,
    /// <summary>All warning-level diagnostics.</summary>
    IReadOnlyList<BuildDiagnostic> Warnings,
    /// <summary>Wall-clock duration of the build.</summary>
    TimeSpan Duration,
    /// <summary>Absolute path of the primary output assembly, or null.</summary>
    string? OutputAssembly = null)
{
    /// <summary>Total number of errors and warnings.</summary>
    public int TotalIssues => Errors.Count + Warnings.Count;
}
