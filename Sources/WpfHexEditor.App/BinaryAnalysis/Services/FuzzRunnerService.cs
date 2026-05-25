// ==========================================================
// Project: WpfHexEditor.App
// File: BinaryAnalysis/Services/FuzzRunnerService.cs
// Description: P15 — orchestrates whfmt.Fuzz FuzzSession for the IDE FuzzPanel.
//              Runs fuzzing generations asynchronously, emitting FuzzVariant items
//              via IProgress<FuzzVariant> so the UI can stream results in real-time.
// Architecture: Stateless orchestration; caller owns lifecycle and cancellation.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WhfmtFuzz;
using WpfHexEditor.Core.Contracts;

namespace WpfHexEditor.App.BinaryAnalysis.Services;

/// <summary>
/// P15 — runs a <see cref="FuzzSession"/> in the background and emits each
/// <see cref="FuzzVariant"/> via <see cref="IProgress{T}"/> as it's generated.
/// </summary>
public sealed class FuzzRunnerService
{
    /// <summary>Default batch size (variants per generation call).</summary>
    public const int DefaultBatchSize = 10;

    /// <summary>Maximum total variants per run (hard cap to avoid OOM).</summary>
    public const int MaxTotalVariants = 500;

    /// <summary>
    /// Runs <paramref name="totalIterations"/> fuzz iterations in batches.
    /// Each <see cref="FuzzVariant"/> is reported via <paramref name="progress"/> as it's produced.
    /// Returns the aggregated corpus when complete.
    /// </summary>
    /// <param name="catalog">The embedded format catalog used for format detection.</param>
    /// <param name="fileBytes">Binary content of the file to fuzz.</param>
    /// <param name="fileName">Original file name (used for format detection).</param>
    /// <param name="totalIterations">Total number of variants to generate (capped at <see cref="MaxTotalVariants"/>).</param>
    /// <param name="seed">Random seed for reproducibility; null = random.</param>
    /// <param name="forcedFormat">Force a specific format name (skips auto-detection).</param>
    /// <param name="compoundMutations">Number of simultaneous mutations per variant.</param>
    /// <param name="progress">Receives each variant as it's generated.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<IReadOnlyList<FuzzVariant>> RunAsync(
        IEmbeddedFormatCatalog    catalog,
        byte[]                    fileBytes,
        string                    fileName,
        int                       totalIterations  = DefaultBatchSize,
        int?                      seed             = null,
        string?                   forcedFormat     = null,
        int                       compoundMutations = 1,
        IProgress<FuzzVariant>?   progress         = null,
        CancellationToken         ct               = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(fileBytes);

        int iterations = Math.Min(totalIterations, MaxTotalVariants);
        var session    = new FuzzSession(catalog, seed);
        int batchSize  = Math.Min(iterations, DefaultBatchSize);
        int remaining  = iterations;

        while (remaining > 0)
        {
            ct.ThrowIfCancellationRequested();

            int count = Math.Min(remaining, batchSize);
            var batch = await Task.Run(() =>
                session.NextGeneration(fileBytes, fileName, count, forcedFormat, compoundMutations), ct);

            foreach (var variant in batch)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report(variant);
            }

            remaining -= count;
        }

        return session.Corpus;
    }
}
