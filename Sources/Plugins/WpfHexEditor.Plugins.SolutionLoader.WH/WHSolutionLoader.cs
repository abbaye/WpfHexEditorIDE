// ==========================================================
// Project: WpfHexEditor.Plugins.SolutionLoader.WH
// File: WHSolutionLoader.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     ISolutionLoader adapter for the native WpfHexEditor solution
//     format (.whsln / .whproj). Delegates all I/O to the existing
//     SolutionSerializer, so the WH format is always authoritative.
//
// Architecture Notes:
//     - Pattern: Adapter — wraps SolutionSerializer behind ISolutionLoader
//     - No custom model needed: Solution (from ProjectSystem) already
//       implements ISolution; returned as-is to the IDE.
// ==========================================================

using WpfHexEditor.Editor.Core;
using WpfHexEditor.Core.ProjectSystem.Services;

namespace WpfHexEditor.Plugins.SolutionLoader.WH;

/// <summary>
/// Loads a native WpfHexEditor <c>.whsln</c> file.
/// Delegates to <see cref="SolutionManager"/> so that format migration,
/// MRU tracking, and model ownership are handled consistently.
/// </summary>
public sealed class WHSolutionLoader : ISolutionLoader
{
    // -----------------------------------------------------------------------
    // ISolutionLoader
    // -----------------------------------------------------------------------

    public string LoaderName => "WpfHexEditor Native";

    /// <inheritdoc />
    public IReadOnlyList<string> SupportedExtensions { get; } = ["whsln", "whproj"];

    /// <inheritdoc />
    public bool CanLoad(string filePath)
    {
        var ext = System.IO.Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();
        return SupportedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public Task<ISolution> LoadAsync(string filePath, CancellationToken ct = default)
        => SolutionManager.Instance.OpenSolutionAsync(filePath, ct);
}
