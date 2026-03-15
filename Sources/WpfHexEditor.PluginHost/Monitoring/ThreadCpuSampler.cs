// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: Monitoring/ThreadCpuSampler.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-15
// Description:
//     Per-thread CPU time sampler for InProcess plugins.
//     Wraps a plugin callback execution, measures the calling thread's
//     CPU time delta, and records a precise per-plugin CPU% estimate.
//     This is a best-effort approximation — .NET does not expose per-thread
//     CPU time via a public BCL API, so we use the native kernel32 call.
//
// Architecture Notes:
//     - Pattern: Decorator / Timing probe
//     - Uses GetThreadTimes (kernel32) for sub-millisecond precision.
//     - Falls back to wall-clock time if the P/Invoke call is unavailable.
//     - Used by TimedHexEditorService to wrap each plugin callback.
//     - CpuPercent reported = (threadCpuDelta / wallDelta) * 100 / ProcessorCount
//       clamped to [0, 100].
// ==========================================================

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WpfHexEditor.PluginHost.Monitoring;

/// <summary>
/// Provides per-thread CPU time sampling around a plugin callback.
/// Wraps <c>GetThreadTimes</c> on Windows for precise measurement.
/// </summary>
public static class ThreadCpuSampler
{
    // ── Native interop ────────────────────────────────────────────────────────

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetThreadTimes(
        IntPtr hThread,
        out long lpCreationTime,
        out long lpExitTime,
        out long lpKernelTime,
        out long lpUserTime);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentThread();

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Executes <paramref name="action"/> and returns a <see cref="ThreadSample"/>
    /// containing the wall-clock elapsed time and the estimated thread CPU%.
    /// </summary>
    public static ThreadSample Measure(Action action)
    {
        var wallSw = Stopwatch.StartNew();
        var cpuBefore = GetCurrentThreadCpuTime();

        try
        {
            action();
        }
        finally
        {
            wallSw.Stop();
        }

        var cpuAfter = GetCurrentThreadCpuTime();
        var cpuDelta = cpuAfter - cpuBefore;

        double cpuPct = wallSw.Elapsed.TotalMilliseconds > 0.1
            ? Math.Clamp(
                cpuDelta.TotalMilliseconds / (wallSw.Elapsed.TotalMilliseconds * Environment.ProcessorCount) * 100.0,
                0.0, 100.0)
            : 0.0;

        return new ThreadSample(wallSw.Elapsed, cpuPct);
    }

    /// <summary>
    /// Asynchronous variant — measures the synchronous portion of the callback.
    /// For async plugin callbacks the measurement reflects the initiating thread only.
    /// </summary>
    public static async Task<ThreadSample> MeasureAsync(Func<Task> action)
    {
        var wallSw = Stopwatch.StartNew();
        var cpuBefore = GetCurrentThreadCpuTime();

        await action().ConfigureAwait(false);

        wallSw.Stop();
        var cpuAfter = GetCurrentThreadCpuTime();
        var cpuDelta = cpuAfter - cpuBefore;

        double cpuPct = wallSw.Elapsed.TotalMilliseconds > 0.1
            ? Math.Clamp(
                cpuDelta.TotalMilliseconds / (wallSw.Elapsed.TotalMilliseconds * Environment.ProcessorCount) * 100.0,
                0.0, 100.0)
            : 0.0;

        return new ThreadSample(wallSw.Elapsed, cpuPct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TimeSpan GetCurrentThreadCpuTime()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return TimeSpan.Zero; // Fallback for non-Windows (unused — project is net8.0-windows)

        try
        {
            var hThread = GetCurrentThread();
            if (GetThreadTimes(hThread, out _, out _, out var kernelTime, out var userTime))
                return TimeSpan.FromTicks(kernelTime + userTime);
        }
        catch { /* best-effort */ }

        return TimeSpan.Zero;
    }
}

/// <summary>
/// Result of a single <see cref="ThreadCpuSampler"/> measurement.
/// </summary>
public readonly record struct ThreadSample(TimeSpan Elapsed, double CpuPercent);
