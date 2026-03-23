// ==========================================================
// Project: WpfHexEditor.Plugins.DiagnosticTools
// File: Models/EventCounterReader.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Attaches to the target process via EventPipe and subscribes to the
//     System.Runtime provider (GC collections, thread-pool, exceptions).
//     Parses EventCounterPayload entries and pushes them to the VM.
//
// Architecture Notes:
//     Uses Microsoft.Diagnostics.NETCore.Client.DiagnosticsClient.
//     EventPipe session runs on a background thread; parsing is minimal.
//     Graceful teardown on CancellationToken: stops the EventPipe session,
//     which unblocks ReadEvent() on the background thread.
// ==========================================================

using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using WpfHexEditor.Plugins.DiagnosticTools.ViewModels;

namespace WpfHexEditor.Plugins.DiagnosticTools.Models;

/// <summary>
/// Reads EventPipe counters from the <c>System.Runtime</c> provider and
/// pushes them into <see cref="DiagnosticToolsPanelViewModel"/>.
/// </summary>
internal sealed class EventCounterReader : IDisposable
{
    private readonly int                           _pid;
    private readonly DiagnosticToolsPanelViewModel _vm;
    private readonly CancellationToken             _ct;

    private EventPipeSession? _session;
    private Task?             _readTask;
    private bool              _disposed;

    // -----------------------------------------------------------------------

    public EventCounterReader(int pid, DiagnosticToolsPanelViewModel vm, CancellationToken ct)
    {
        _pid = pid;
        _vm  = vm;
        _ct  = ct;
    }

    // -----------------------------------------------------------------------

    public void Start()
    {
        if (_readTask != null || _disposed) return;
        _readTask = Task.Run(ReadLoopAsync, CancellationToken.None);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try { _session?.Stop(); } catch { /* ignore */ }
        _session?.Dispose();
    }

    // -----------------------------------------------------------------------

    private async Task ReadLoopAsync()
    {
        try
        {
            var client = new DiagnosticsClient(_pid);

            var providers = new[]
            {
                new EventPipeProvider(
                    "System.Runtime",
                    System.Diagnostics.Tracing.EventLevel.Verbose,
                    arguments: new Dictionary<string, string>
                    {
                        ["EventCounterIntervalSec"] = "1"
                    }),

                // GC keyword (0x1) captures GCStart (ID=1) and GCStop (ID=2) events,
                // giving real-time per-collection entries in the Events tab.
                new EventPipeProvider(
                    "Microsoft-Windows-DotNETRuntime",
                    System.Diagnostics.Tracing.EventLevel.Informational,
                    keywords: 0x1L),
            };

            _session = client.StartEventPipeSession(providers, requestRundown: false);

            // Register CT callback to stop the session — unblocks ReadEvent().
            await using var reg = _ct.Register(() =>
            {
                try { _session.Stop(); } catch { }
            });

            using var source = new EventPipeEventSource(_session.EventStream);

            source.Dynamic.All += OnEvent;

            // Process() blocks until the session is stopped or stream ends.
            await Task.Run(() => source.Process(), CancellationToken.None).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { /* normal */ }
        catch (DiagnosticsClientException) { /* process gone */ }
        catch (Exception ex)
        {
            _vm.AddEvent($"[EventPipe error] {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string GcReasonText(int reason) => reason switch
    {
        0 => "alloc-small",
        1 => "induced",
        4 => "alloc-large",
        _ => $"r={reason}",
    };

    private void OnEvent(TraceEvent evt)
    {
        // Real-time GC Start events from the CLR runtime provider (GC keyword = 0x1).
        // Event ID 1 = GCStart. Payload: Depth (gen), Count (collection #), Reason.
        if (evt.ProviderName == "Microsoft-Windows-DotNETRuntime" && (int)evt.ID == 1)
        {
            var gen    = Convert.ToInt32(evt.PayloadByName("Depth")  ?? 0);
            var count  = Convert.ToInt32(evt.PayloadByName("Count")  ?? 0);
            var reason = Convert.ToInt32(evt.PayloadByName("Reason") ?? 0);
            _vm.AddEvent($"[GC] Gen{gen} #{count} — {GcReasonText(reason)}");
            return;
        }

        if (evt.EventName != "EventCounters") return;

        // EventCounterPayload arrives as a IDictionary<string, object> in Payload[0].
        if (evt.PayloadByName("Payload") is not IDictionary<string, object> payload) return;

        if (!payload.TryGetValue("Name",         out var nameObj))  return;
        if (!payload.TryGetValue("CounterType",  out var typeObj))  return;
        if (!payload.TryGetValue("Mean",         out var meanObj) &&
            !payload.TryGetValue("Increment",    out meanObj))     return;

        string name     = nameObj?.ToString()  ?? string.Empty;
        string ctype    = typeObj?.ToString()  ?? string.Empty;
        double value    = Convert.ToDouble(meanObj);

        switch (name)
        {
            case "gc-heap-size":
                _vm.GcHeapMb = value;
                break;

            case "gen-0-gc-count" when ctype == "Sum" && value > 0:
            case "gen-1-gc-count" when ctype == "Sum" && value > 0:
            case "gen-2-gc-count" when ctype == "Sum" && value > 0:
                _vm.AddGcEvent(name, value);
                break;

            case "exception-count" when value > 0:
                _vm.AddEvent($"[exception] count={value:F0}/s");
                break;

            case "threadpool-queue-length":
                _vm.ThreadPoolQueue = (int)value;
                break;

            case "threadpool-thread-count":
                _vm.ThreadPoolThreads = (int)value;
                break;
        }
    }
}
