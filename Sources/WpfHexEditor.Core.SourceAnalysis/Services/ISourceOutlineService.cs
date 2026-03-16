// ==========================================================
// Project: WpfHexEditor.Core.SourceAnalysis
// File: Services/ISourceOutlineService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-16
// Description:
//     Contract for producing a SourceOutlineModel from a source file.
//     Implementations must be thread-safe and cache results by file path.
// ==========================================================

using WpfHexEditor.Core.SourceAnalysis.Models;

namespace WpfHexEditor.Core.SourceAnalysis.Services;

/// <summary>
/// Produces a <see cref="SourceOutlineModel"/> for a source file.
/// Implementations must be thread-safe and cache results keyed by absolute file path.
/// </summary>
public interface ISourceOutlineService
{
    /// <summary>
    /// Returns true when this service can outline <paramref name="filePath"/>.
    /// Checks extension (.cs, .xaml) only — does not read the file.
    /// </summary>
    bool CanOutline(string filePath);

    /// <summary>
    /// Returns the outline for <paramref name="filePath"/>.
    /// Result is cached and returned immediately if the file has not changed since last parse.
    /// Parse runs on a thread-pool thread; always await on a background context.
    /// Returns <c>null</c> when the file cannot be read or is not a supported type.
    /// </summary>
    Task<SourceOutlineModel?> GetOutlineAsync(string filePath, CancellationToken ct = default);

    /// <summary>
    /// Removes the cached entry for <paramref name="filePath"/>, forcing a re-parse
    /// on the next <see cref="GetOutlineAsync"/> call.
    /// Safe to call from any thread.
    /// </summary>
    void Invalidate(string filePath);
}
