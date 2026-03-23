// ==========================================================
// Project: WpfHexEditor.Plugins.DiagnosticTools
// File: Models/DiagnosticsSession.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Wraps a Microsoft.Diagnostics.NETCore.Client DiagnosticsClient session
//     for a specific process ID. Orchestrates ProcessMonitor (CPU/mem polling)
//     and EventCounterReader (EventPipe GC/runtime counters).
//
// Architecture Notes:
//     Pattern: Facade — single entry point for all diagnostics on one PID.
//     Disposed by DiagnosticToolsPlugin when ProcessExitedEvent arrives or on
//     plugin shutdown. Thread-safe: Start/Stop are idempotent.
// ==========================================================

using System.Diagnostics;
using Microsoft.Diagnostics.NETCore.Client;
using WpfHexEditor.Plugins.DiagnosticTools.ViewModels;

namespace WpfHexEditor.Plugins.DiagnosticTools.Models;

/// <summary>
/// Active diagnostics session attached to a single process.
/// Feeds real-time data into <see cref="DiagnosticToolsPanelViewModel"/>.
/// </summary>
internal sealed class DiagnosticsSession : IDisposable
{
    private readonly int                           _pid;
    private readonly DiagnosticToolsPanelViewModel _vm;
    private readonly CancellationTokenSource       _cts = new();

    private ProcessMonitor?    _processMonitor;
    private EventCounterReader? _counterReader;
    private bool               _started;
    private bool               _disposed;

    // -----------------------------------------------------------------------

    public DiagnosticsSession(int pid, DiagnosticToolsPanelViewModel vm)
    {
        _pid = pid;
        _vm  = vm;
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>Starts polling and EventPipe listeners for the target process.</summary>
    public void Start()
    {
        if (_started || _disposed) return;
        _started = true;

        _vm.SessionStatus = $"Attached to PID {_pid}";

        // CPU / memory polling at 500 ms.
        _processMonitor = new ProcessMonitor(_pid, _vm, _cts.Token);
        _processMonitor.Start();

        // EventPipe counters (GC, thread-pool, exceptions…).
        if (IsProcessAlive(_pid))
        {
            _counterReader = new EventCounterReader(_pid, _vm, _cts.Token);
            _counterReader.Start();
        }
    }

    /// <summary>
    /// Signals the session to stop streaming. Called when <c>ProcessExitedEvent</c>
    /// arrives or when the plugin shuts down.
    /// </summary>
    public void Stop(int? exitCode = null)
    {
        if (!_started || _disposed) return;

        _cts.Cancel();

        if (exitCode.HasValue)
            _vm.SessionStatus = $"Process exited (code {exitCode.Value})";
        else
            _vm.SessionStatus = "Session stopped";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (!_cts.IsCancellationRequested)
            _cts.Cancel();

        _counterReader?.Dispose();
        _processMonitor?.Dispose();
        _cts.Dispose();
    }

    // -----------------------------------------------------------------------

    private static bool IsProcessAlive(int pid)
    {
        try   { using var p = Process.GetProcessById(pid); return !p.HasExited; }
        catch { return false; }
    }
}
