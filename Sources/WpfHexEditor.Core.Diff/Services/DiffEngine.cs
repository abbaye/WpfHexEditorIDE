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
    /// </summary>
    /// <param name="leftPath">Path of the left (original) file.</param>
    /// <param name="rightPath">Path of the right (modified) file.</param>
    /// <param name="mode">Requested mode; <see cref="DiffMode.Auto"/> auto-detects.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<DiffEngineResult> CompareAsync(string leftPath, string rightPath,
        DiffMode mode = DiffMode.Auto, CancellationToken ct = default)
        => Task.Run(() => Compare(leftPath, rightPath, mode, ct), ct);

    // -----------------------------------------------------------------------
    // Private synchronous core
    // -----------------------------------------------------------------------

    private static DiffEngineResult Compare(string leftPath, string rightPath,
        DiffMode requestedMode, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var effectiveMode = requestedMode == DiffMode.Auto
            ? DiffModeDetector.DetectForPair(leftPath, rightPath)
            : requestedMode;

        string? fallbackReason = null;

        if (effectiveMode == DiffMode.Binary)
            return CompareBinary(leftPath, rightPath, effectiveMode, ct);

        // Text or Semantic — read as lines
        ct.ThrowIfCancellationRequested();
        var leftInfo  = new FileInfo(leftPath);
        var rightInfo = new FileInfo(rightPath);

        // Files too large for Myers → force binary
        if (leftInfo.Length > MaxReadBytes || rightInfo.Length > MaxReadBytes)
        {
            fallbackReason = $"File exceeds {MaxReadBytes / (1024 * 1024)} MB — using binary comparison";
            return CompareBinary(leftPath, rightPath, DiffMode.Binary, ct, fallbackReason);
        }

        string[] leftLines, rightLines;
        try
        {
            leftLines  = File.ReadAllLines(leftPath);
            rightLines = File.ReadAllLines(rightPath);
        }
        catch (IOException ex)
        {
            fallbackReason = $"Could not read file as text ({ex.Message}) — using binary comparison";
            return CompareBinary(leftPath, rightPath, DiffMode.Binary, ct, fallbackReason);
        }

        ct.ThrowIfCancellationRequested();

        IDiffAlgorithm algo = effectiveMode == DiffMode.Semantic ? _semantic : _myers;
        TextDiffResult textResult;
        try
        {
            textResult = algo.ComputeLines(leftLines, rightLines);
        }
        catch
        {
            fallbackReason = "Diff algorithm failed — falling back to Myers";
            textResult = _myers.ComputeLines(leftLines, rightLines);
            effectiveMode = DiffMode.Text;
        }

        return new DiffEngineResult
        {
            EffectiveMode  = effectiveMode,
            TextResult     = textResult,
            FallbackReason = fallbackReason ?? textResult.FallbackReason,
            LeftPath       = leftPath,
            RightPath      = rightPath
        };
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
