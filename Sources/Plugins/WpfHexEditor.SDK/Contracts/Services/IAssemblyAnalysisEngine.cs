// ==========================================================
// Project: WpfHexEditor.SDK
// File: Contracts/Services/IAssemblyAnalysisEngine.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     SDK-level contract for the PE / .NET assembly analysis engine.
//     Defined independently of WpfHexEditor.Core.AssemblyAnalysis to
//     avoid circular project references. Any plugin can consume this
//     interface without referencing the Core library directly.
//
// Architecture Notes:
//     Pattern: Strategy — decouples callers from the concrete engine.
//     Implemented by AssemblyAnalysisEngine in Core.
//     Exposed via IIDEHostContext.AssemblyAnalysis (future SDK addition).
// ==========================================================

namespace WpfHexEditor.SDK.Contracts.Services;

/// <summary>
/// Analyses PE files (.NET managed or native) and produces structured
/// assembly metadata suitable for display in the Assembly Explorer.
/// </summary>
/// <remarks>
/// This interface mirrors <c>WpfHexEditor.Core.AssemblyAnalysis.Services.IAssemblyAnalysisEngine</c>
/// so that plugins can consume the engine without a direct Core reference.
/// </remarks>
public interface IAssemblyAnalysisEngine
{
    /// <summary>
    /// Returns <see langword="true"/> when the file at <paramref name="filePath"/>
    /// appears to be a valid PE file (MZ magic bytes present).
    /// Does not check for .NET metadata.
    /// </summary>
    bool CanAnalyze(string filePath);

    /// <summary>
    /// Analyses the PE file at <paramref name="filePath"/> on a background thread.
    /// Throws <see cref="OperationCanceledException"/> when <paramref name="ct"/> is cancelled.
    /// Returns a generic <see cref="object"/> to avoid a hard SDK → Core type dependency.
    /// Cast to <c>AssemblyModel</c> when the Core assembly is referenced directly.
    /// </summary>
    Task<object> AnalyzeAsync(string filePath, CancellationToken ct = default);
}
