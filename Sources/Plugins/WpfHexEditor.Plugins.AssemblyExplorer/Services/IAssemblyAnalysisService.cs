// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: Services/IAssemblyAnalysisService.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Description:
//     Contract for the assembly analysis service.
//     Implementations use System.Reflection.Metadata + PEReader (BCL)
//     to parse PE files without any external NuGet dependencies.
//
// Architecture Notes:
//     Pattern: Strategy — concrete backend (managed BCL, future ILSpy)
//     can be swapped without touching the ViewModel layer.
// ==========================================================

using WpfHexEditor.Plugins.AssemblyExplorer.Models;

namespace WpfHexEditor.Plugins.AssemblyExplorer.Services;

/// <summary>
/// Reads a PE file (managed .NET or native) and returns a fully populated
/// <see cref="AssemblyModel"/>. Analysis runs on a background thread;
/// the returned model is immutable and safe to marshal to the UI thread.
/// </summary>
public interface IAssemblyAnalysisService
{
    /// <summary>
    /// Returns true when the file at <paramref name="filePath"/> is a PE file
    /// that this service can analyze (MZ signature present).
    /// Does not open the full file; only reads the first 2 bytes.
    /// </summary>
    bool CanAnalyze(string filePath);

    /// <summary>
    /// Parses the PE file and returns an <see cref="AssemblyModel"/>.
    /// For non-managed PE files (<c>PEReader.HasMetadata == false</c>), returns a
    /// minimal model with <c>IsManaged = false</c> and section headers only.
    /// Throws <see cref="OperationCanceledException"/> if <paramref name="ct"/> fires.
    /// </summary>
    Task<AssemblyModel> AnalyzeAsync(string filePath, CancellationToken ct = default);
}
