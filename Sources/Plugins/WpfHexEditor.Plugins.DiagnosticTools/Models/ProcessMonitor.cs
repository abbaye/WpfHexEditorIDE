// ==========================================================
// Project: WpfHexEditor.Plugins.DiagnosticTools
// File: Models/ProcessMonitor.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Polls a target process every 500 ms for WorkingSet64 and CPU %.
//     Supports Pause() / Resume() via ManualResetEventSlim.
//     Pushes samples to DiagnosticToolsPanelViewModel.
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
    private readonly ManualResetEventSlim          _gate = new(initialState: true);

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

    /// <summary>Suspends sample collection without stopping the background task.</summary>
    public void Pause()  => _gate.Reset();

    /// <summary>Resumes sample collection after a <see cref="Pause"/>.</summary>
    public void Resume() => _gate.Set();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _gate.Set();   // unblock if paused so the loop can exit
        _gate.Dispose();
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
            return;
        }

        try
        {
            while (!_ct.IsCancellationRequested)
            {
                await Task.Delay(PollIntervalMs, _ct).ConfigureAwait(false);

                // Block here if paused (without burning CPU).
                _gate.Wait(_ct);

                try
                {
                    proc.Refresh();
                    if (proc.HasExited) break;

                    double memMb = proc.WorkingSet64 / (1024.0 * 1024.0);

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
                catch (Exception) { /* transient */ }
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            proc?.Dispose();
        }
    }
}
