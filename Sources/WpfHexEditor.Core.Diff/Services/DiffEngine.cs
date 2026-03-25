// Project      : WpfHexEditorControl
// File         : Services/DiffEngine.cs
// Description  : Async orchestrator that selects the right algorithm and produces DiffEngineResult.
// Architecture : Thin coordinator — no UI, no WPF.  Thread-safe; stateless.

using WpfHexEditor.Core.Diff.Algorithms;
using WpfHexEditor.Core.Diff.Models;

namespace WpfHexEditor.Core.Diff.Services;

/// <summary>
/// Central orchestrator for file comparison.
/// Selects <see cref="BinaryDiffAlgorithm"/>, <see cref="MyersDiffAlgorithm"/>, or
/// <see cref="SemanticDiffAlgorithm"/> based on the requested <see cref="DiffMode"/>.
/// </summary>
public sealed class DiffEngine
{
    private const long MaxReadBytes = 50L * 1024 * 1024; // 50 MB cap for Myers

    // Singleton algorithm instances (stateless — safe to share)
    private static readonly BinaryDiffAlgorithm   _binary   = new();
    private static readonly MyersDiffAlgorithm    _myers    = new();
    private static readonly SemanticDiffAlgorithm _semantic = new();

    /// <summary>
    /// Compares two files asynchronously and returns a <see cref="DiffEngineResult"/>.
    /// IO is performed asynchronously (frees the caller during disk reads).
    /// The CPU-bound algorithm runs on the ThreadPool via Task.Run.
    /// </summary>
    /// <param name="leftPath">Path of the left (original) file.</param>
    /// <param name="rightPath">Path of the right (modified) file.</param>
    /// <param name="mode">Requested mode; <see cref="DiffMode.Auto"/> auto-detects.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<DiffEngineResult> CompareAsync(string leftPath, string rightPath,
        DiffMode mode = DiffMode.Auto, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var effectiveMode = mode == DiffMode.Auto
            ? DiffModeDetector.DetectForPair(leftPath, rightPath)
            : mode;

        if (effectiveMode == DiffMode.Binary)
            return await Task.Run(() => CompareBinary(leftPath, rightPath, effectiveMode, ct), ct)
                             .ConfigureAwait(false);

        var leftInfo  = new FileInfo(leftPath);
        var rightInfo = new FileInfo(rightPath);

        if (leftInfo.Length > MaxReadBytes || rightInfo.Length > MaxReadBytes)
        {
            var reason = $"File exceeds {MaxReadBytes / (1024 * 1024)} MB — using binary comparison";
            return await Task.Run(() => CompareBinary(leftPath, rightPath, DiffMode.Binary, ct, reason), ct)
                             .ConfigureAwait(false);
        }

        // Async IO — reads happen without holding a ThreadPool thread (OPT-PERF-01).
        string[] leftLines, rightLines;
        try
        {
            leftLines  = await File.ReadAllLinesAsync(leftPath,  ct).ConfigureAwait(false);
            rightLines = await File.ReadAllLinesAsync(rightPath, ct).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            var reason = $"Could not read file as text ({ex.Message}) — using binary comparison";
            return await Task.Run(() => CompareBinary(leftPath, rightPath, DiffMode.Binary, ct, reason), ct)
                             .ConfigureAwait(false);
        }

        // CPU-bound algorithm on ThreadPool.
        var capturedMode = effectiveMode;
        return await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            IDiffAlgorithm algo = capturedMode == DiffMode.Semantic ? _semantic : _myers;
            TextDiffResult textResult;
            string? fallbackReason = null;
            try
            {
                textResult = algo.ComputeLines(leftLines, rightLines);
            }
            catch
            {
                fallbackReason = "Diff algorithm failed — falling back to Myers";
                textResult     = _myers.ComputeLines(leftLines, rightLines);
                capturedMode   = DiffMode.Text;
            }
            return new DiffEngineResult
            {
                EffectiveMode  = capturedMode,
                TextResult     = textResult,
                FallbackReason = fallbackReason ?? textResult.FallbackReason,
                LeftPath       = leftPath,
                RightPath      = rightPath
            };
        }, ct).ConfigureAwait(false);
    }

    private static DiffEngineResult CompareBinary(string leftPath, string rightPath,
        DiffMode mode, CancellationToken ct, string? fallbackReason = null)
    {
        byte[] leftBytes, rightBytes;
        string? truncReason = null;

        leftBytes  = ReadFileCapped(leftPath,  MaxReadBytes, out var leftTrunc);
        rightBytes = ReadFileCapped(rightPath, MaxReadBytes, out var rightTrunc);

        if (leftTrunc || rightTrunc)
            truncReason = $"File(s) truncated to {MaxReadBytes / (1024 * 1024)} MB for comparison";

        ct.ThrowIfCancellationRequested();

        var result = _binary.ComputeBytes(leftBytes, rightBytes);
        if (truncReason is not null)
            result = new BinaryDiffResult { Regions = result.Regions, Stats = result.Stats, Truncated = true, TruncatedReason = truncReason };

        return new DiffEngineResult
        {
            EffectiveMode  = mode,
            BinaryResult   = result,
            FallbackReason = fallbackReason,
            LeftPath       = leftPath,
            RightPath      = rightPath
        };
    }

    private static byte[] ReadFileCapped(string path, long cap, out bool truncated)
    {
        truncated = false;
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (fs.Length <= cap)
        {
            var buf = new byte[fs.Length];
            fs.ReadExactly(buf);
            return buf;
        }

        truncated = true;
        var capped = new byte[cap];
        fs.ReadExactly(capped);
        return capped;
    }
}
