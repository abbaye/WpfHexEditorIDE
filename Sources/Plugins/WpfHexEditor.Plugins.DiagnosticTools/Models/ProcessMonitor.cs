// ==========================================================
// Project: WpfHexEditor.Plugins.DiagnosticTools
// File: Models/ProcessMonitor.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Polls a target process every 500 ms for WorkingSet64 and CPU %.
//     Pushes samples to DiagnosticToolsPanelViewModel's ring-buffer queues.
//
// Architecture Notes:
//     Polling runs on a Task.Run background thread — never blocks the UI.
//     Calculates CPU delta against a previous TotalProcessorTime snapshot.
//     Stops automatically when the process exits or the CT is cancelled.
// ==========================================================

using System.Diagnostics;
using WpfHexEditor.Plugins.DiagnosticTools.ViewModels;

namespace WpfHexEditor.Plugins.DiagnosticTools.Models;

/// <summary>
/// Polls <see cref="Process"/> metrics every 500 ms and pushes samples
/// into <see cref="DiagnosticToolsPanelViewModel"/>.
/// </summary>
internal sealed class ProcessMonitor : IDisposable
{
    private const int PollIntervalMs = 500;

    private readonly int                           _pid;
    private readonly DiagnosticToolsPanelViewModel _vm;
    private readonly CancellationToken             _ct;

    private Task? _pollTask;
    private bool  _disposed;

    // -----------------------------------------------------------------------

    public ProcessMonitor(int pid, DiagnosticToolsPanelViewModel vm, CancellationToken ct)
    {
        _pid = pid;
        _vm  = vm;
        _ct  = ct;
    }

    // -----------------------------------------------------------------------

    public void Start()
    {
        if (_pollTask != null || _disposed) return;
        _pollTask = Task.Run(PollLoopAsync, CancellationToken.None);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // _ct cancellation signals the loop to exit; Task finishes on its own.
    }

    // -----------------------------------------------------------------------

    private async Task PollLoopAsync()
    {
        Process? proc = null;
        TimeSpan prevCpuTime = TimeSpan.Zero;
        DateTime prevSampleAt = DateTime.UtcNow;

        try
        {
            proc = Process.GetProcessById(_pid);
            prevCpuTime  = proc.TotalProcessorTime;
            prevSampleAt = DateTime.UtcNow;
        }
        catch
        {
            // Process already gone before the first poll.
            return;
        }

        try
        {
            while (!_ct.IsCancellationRequested)
            {
                await Task.Delay(PollIntervalMs, _ct).ConfigureAwait(false);

                try
                {
                    proc.Refresh();
                    if (proc.HasExited) break;

                    // Working Set (MB)
                    double memMb = proc.WorkingSet64 / (1024.0 * 1024.0);

                    // CPU % = delta CPU time / (wall time × core count)
                    var now        = DateTime.UtcNow;
                    var cpuTime    = proc.TotalProcessorTime;
                    var wallMs     = (now - prevSampleAt).TotalMilliseconds;
                    var cpuDeltaMs = (cpuTime - prevCpuTime).TotalMilliseconds;
                    var coreCount  = Math.Max(1, Environment.ProcessorCount);
                    double cpuPct  = wallMs > 0
                        ? Math.Min(100.0, cpuDeltaMs / (wallMs * coreCount) * 100.0)
                        : 0.0;

                    prevCpuTime  = cpuTime;
                    prevSampleAt = now;

                    _vm.PushMemorySample(memMb);
                    _vm.PushCpuSample(cpuPct);
                }
                catch (InvalidOperationException) { break; }
                catch (Exception) { /* transient — continue */ }
            }
        }
        catch (OperationCanceledException) { /* normal exit */ }
        finally
        {
            proc?.Dispose();
        }
    }
}
