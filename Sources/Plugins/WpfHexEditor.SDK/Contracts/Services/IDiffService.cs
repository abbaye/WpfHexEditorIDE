// ==========================================================
// Project: WpfHexEditor.SDK
// File: Contracts/Services/IDiffService.cs
// Description:
//     Public SDK contract for file comparison (diff) operations.
//     Exposed via IDEHostContext.DiffService (nullable — absent if the
//     FileComparison plugin is not loaded).
//     Plugins and terminal commands use this to compare files or open the
//     DiffHub viewer programmatically.
// Architecture Notes:
//     Implemented by DiffServiceAdapter inside WpfHexEditor.Plugins.FileComparison.
//     The adapter is registered on plugin init; the App layer holds the reference.
// ==========================================================

namespace WpfHexEditor.SDK.Contracts.Services;

/// <summary>
/// Service for comparing files inside the IDE.
/// Accessible to plugins and terminal commands via <c>context.DiffService</c>.
/// </summary>
public interface IDiffService
{
    /// <summary>
    /// Compares <paramref name="leftPath"/> against <paramref name="rightPath"/> and returns
    /// a summary of differences. Does NOT open the viewer — use
    /// <see cref="OpenInViewerAsync"/> for that.
    /// </summary>
    Task<DiffSummary> CompareAsync(string leftPath, string rightPath, CancellationToken ct = default);

    /// <summary>
    /// Opens the DiffHub panel with <paramref name="leftPath"/> and <paramref name="rightPath"/>
    /// pre-loaded and starts the comparison automatically.
    /// </summary>
    Task OpenInViewerAsync(string leftPath, string rightPath, CancellationToken ct = default);
}

/// <summary>Summary result of a file comparison.</summary>
/// <param name="Modified">Lines present in both files but changed.</param>
/// <param name="Added">Lines present only in the right file.</param>
/// <param name="Removed">Lines present only in the left file.</param>
/// <param name="SimilarityPercent">0–100 similarity percentage between the two files.</param>
public record DiffSummary(int Modified, int Added, int Removed, int SimilarityPercent)
{
    /// <summary>Total number of changed lines.</summary>
    public int TotalChanges => Modified + Added + Removed;
}
