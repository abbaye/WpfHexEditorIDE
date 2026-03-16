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
using WpfHexEditor.ProjectSystem.Serialization;

namespace WpfHexEditor.Plugins.SolutionLoader.WH;

/// <summary>
/// Loads a native WpfHexEditor <c>.whsln</c> file using
/// <see cref="SolutionSerializer"/> and returns an <see cref="ISolution"/>.
/// </summary>
public sealed class WHSolutionLoader : ISolutionLoader
{
    // -----------------------------------------------------------------------
    // ISolutionLoader
    // -----------------------------------------------------------------------

    public string LoaderName => "WpfHexEditor Native";

    /// <inheritdoc />
    public bool CanLoad(string filePath)
        => string.Equals(
            System.IO.Path.GetExtension(filePath),
            ".whsln",
            StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public async Task<ISolution> LoadAsync(string filePath, CancellationToken ct = default)
    {
        var (solution, _) = await SolutionSerializer.ReadAsync(filePath, ct)
                                                    .ConfigureAwait(false);
        return solution;
    }
}
