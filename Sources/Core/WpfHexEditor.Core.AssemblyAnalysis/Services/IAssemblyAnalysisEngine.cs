// ==========================================================
// Project: WpfHexEditor.Core.AssemblyAnalysis
// File: Services/IAssemblyAnalysisEngine.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Contract for the PE / .NET assembly analysis engine.
//     Replaces the plugin-local IAssemblyAnalysisService stub.
//     Implemented by AssemblyAnalysisEngine (BCL-only, no external NuGet).
//
// Architecture Notes:
//     Pattern: Strategy — allows alternative backends in the future
//     (e.g. a faster native-image-only path).
//     Exposed in WpfHexEditor.SDK so any plugin can consume the engine
//     without referencing WpfHexEditor.Core.AssemblyAnalysis directly.
// ==========================================================

using WpfHexEditor.Core.AssemblyAnalysis.Models;

namespace WpfHexEditor.Core.AssemblyAnalysis.Services;

/// <summary>
/// Analyses PE files (.NET managed or native) and produces an
/// immutable <see cref="AssemblyModel"/> suitable for marshalling to the UI thread.
/// </summary>
public interface IAssemblyAnalysisEngine
{
    /// <summary>
    /// Returns true when the file at <paramref name="filePath"/> appears to be a valid
    /// PE file (MZ magic bytes present). Does not check for .NET metadata.
    /// </summary>
    bool CanAnalyze(string filePath);

    /// <summary>
    /// Returns true when the PE file at <paramref name="filePath"/> contains a .NET CLR
    /// metadata header (i.e. it is a managed assembly, not a native PE).
    /// Uses <c>FileShare.ReadWrite</c> so the check succeeds even when the HexEditor holds
    /// the file open. Returns false on any I/O or format error.
    /// </summary>
    bool HasManagedMetadata(string filePath);

    /// <summary>
    /// Analyses the PE file at <paramref name="filePath"/> on a background thread
    /// and returns the populated <see cref="AssemblyModel"/>.
    /// Throws <see cref="OperationCanceledException"/> when <paramref name="ct"/> is cancelled.
    /// </summary>
    Task<AssemblyModel> AnalyzeAsync(string filePath, CancellationToken ct = default);
}
