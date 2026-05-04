// ==========================================================
// Project: WpfHexEditor.Core.Debugger
// File: Services/DapClientBase.cs
// Description:
//     Abstract DAP transport over stdio.
//     "Content-Length: N\r\n\r\n{json}" framing.
//     Drives a request/response correlation table using TaskCompletionSource.
// ==========================================================

using System.Text;
using System.Text.Json;
using WpfHexEditor.Core.Debugger.Protocol;

namespace WpfHexEditor.Core.Debugger.Services;

/// <summary>
/// Base class providing DAP stdio transport.
/// Subclasses supply <see cref="InputStream"/> and <see cref="OutputStream"/>
/// (from the adapter process stdin/stdout).
/// </summary>
public abstract class DapClientBase : IDapClient
{
    private int _seq;
    private readonly Dictionary<int, TaskCompletionSource<DapResponse>> _pending = [];
    private readonly CancellationTokenSource _cts = new();
    private Task? _readerTask;

    protected abstract Stream InputStream  { get; }
    protected abstract Stream OutputStream { get; }

    // ── IDapClient events ─────────────────────────────────────────────────────

    public event EventHandler<StoppedEventBody>? Stopped;
    public event EventHandler<OutputEventBody>?  Output;
    public event EventHandler<ExitedEventBody>?  Exited;
    public event EventHandler?                   Terminated;

    // ── Transport ─────────────────────────────────────────────────────────────

    protected void StartReader() =>
        _readerTask = Task.Run(() => ReadLoopAsync(_cts.Token));

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        var reader = new StreamReader(InputStream, Encoding.UTF8, leaveOpen: true);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Read headers until blank line
                int contentLength = 0;
                while (true)
                {
                    var line = await reader.ReadLineAsync(ct);
                    if (line is null) return;
                    if (line.Length == 0) break;
                    if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                        contentLength = int.Parse(line["Content-Length:".Length..].Trim());
                }

                if (contentLength <= 0) continue;

                var buf = new char[contentLength];
                int read = 0;
                while (read < contentLength)
                    read += await reader.ReadAsync(buf, read, contentLength - read);

                var json = new string(buf);
                DispatchMessage(json);
            }
            catch (OperationCanceledException) { break; }
            catch { break; }
        }
    }

    private void DispatchMessage(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var type = root.GetProperty("type").GetString();

        if (type == "response")
        {
            var resp    = JsonSerializer.Deserialize<DapResponse>(json)!;
            var seq     = resp.RequestSeq;
            lock (_pending)
            {
                if (_pending.TryGetValue(seq, out var tcs))
                {
                    _pending.Remove(seq);
                    tcs.TrySetResult(resp);
                }
            }
        }
        else if (type == "event")
        {
            var evt = JsonSerializer.Deserialize<DapEvent>(json)!;
            DispatchEvent(evt);
        }
    }

    private void DispatchEvent(DapEvent evt)
    {
        switch (evt.Event)
        {
            case "stopped" when evt.Body.HasValue:
                var stopped = JsonSerializer.Deserialize<StoppedEventBody>(evt.Body.Value.GetRawText());
                if (stopped is not null) Stopped?.Invoke(this, stopped);
                break;

            case "output" when evt.Body.HasValue:
                var output = JsonSerializer.Deserialize<OutputEventBody>(evt.Body.Value.GetRawText());
                if (output is not null) Output?.Invoke(this, output);
                break;

            case "exited" when evt.Body.HasValue:
                var exited = JsonSerializer.Deserialize<ExitedEventBody>(evt.Body.Value.GetRawText());
                if (exited is not null) Exited?.Invoke(this, exited);
                break;

            case "terminated":
                Terminated?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    protected async Task<DapResponse> SendRequestAsync(
        string command, object? args = null, CancellationToken ct = default)
    {
        var seq = Interlocked.Increment(ref _seq);
        var tcs = new TaskCompletionSource<DapResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_pending) _pending[seq] = tcs;

        using var reg = ct.Register(() =>
        {
            lock (_pending) _pending.Remove(seq);
            tcs.TrySetCanceled(ct);
        });

        var json = JsonSerializer.Serialize(new
        {
            seq,
            type      = "request",
            command,
            arguments = args
        });

        var bytes  = Encoding.UTF8.GetBytes(json);
        var header = Encoding.ASCII.GetBytes($"Content-Length: {bytes.Length}\r\n\r\n");

        await OutputStream.WriteAsync(header, ct);
        await OutputStream.WriteAsync(bytes,  ct);
        await OutputStream.FlushAsync(ct);

        return await tcs.Task.WaitAsync(ct);
    }

    protected static T? ParseBody<T>(DapResponse resp) where T : class
    {
        if (!resp.Success) return null;
        var raw = resp.Body.GetRawText();
        return string.IsNullOrEmpty(raw) || raw == "null"
            ? null
            : JsonSerializer.Deserialize<T>(raw);
    }

    // ── IDapClient implementation ─────────────────────────────────────────────

    public virtual async Task<CapabilitiesBody?> InitializeAsync(
        InitializeRequestArgs args, CancellationToken ct = default)
    {
        var resp = await SendRequestAsync("initialize", args, ct);
        return ParseBody<CapabilitiesBody>(resp);
    }

    public virtual async Task LaunchAsync(LaunchRequestArgs args, CancellationToken ct = default) =>
        await SendRequestAsync("launch", args, ct);

    public virtual async Task AttachAsync(AttachRequestArgs args, CancellationToken ct = default) =>
        await SendRequestAsync("attach", args, ct);

    public virtual async Task ConfigurationDoneAsync(CancellationToken ct = default) =>
        await SendRequestAsync("configurationDone", null, ct);

    public virtual async Task DisconnectAsync(DisconnectArgs? args = null, CancellationToken ct = default) =>
        await SendRequestAsync("disconnect", args ?? new DisconnectArgs(), ct);

    public virtual async Task<SetBreakpointsBody?> SetBreakpointsAsync(
        SetBreakpointsArgs args, CancellationToken ct = default)
    {
        var resp = await SendRequestAsync("setBreakpoints", args, ct);
        return ParseBody<SetBreakpointsBody>(resp);
    }

    public virtual async Task ContinueAsync(ContinueArgs args, CancellationToken ct = default) =>
        await SendRequestAsync("continue", args, ct);

    public virtual async Task NextAsync(StepArgs args, CancellationToken ct = default) =>
        await SendRequestAsync("next", args, ct);

    public virtual async Task StepInAsync(StepArgs args, CancellationToken ct = default) =>
        await SendRequestAsync("stepIn", args, ct);

    public virtual async Task StepOutAsync(StepArgs args, CancellationToken ct = default) =>
        await SendRequestAsync("stepOut", args, ct);

    public virtual async Task PauseAsync(PauseArgs args, CancellationToken ct = default) =>
        await SendRequestAsync("pause", args, ct);

    public virtual async Task<ThreadsBody?> ThreadsAsync(CancellationToken ct = default)
    {
        var resp = await SendRequestAsync("threads", null, ct);
        return ParseBody<ThreadsBody>(resp);
    }

    public virtual async Task<StackTraceBody?> StackTraceAsync(
        StackTraceArgs args, CancellationToken ct = default)
    {
        var resp = await SendRequestAsync("stackTrace", args, ct);
        return ParseBody<StackTraceBody>(resp);
    }

    public virtual async Task<ScopesBody?> ScopesAsync(
        ScopesArgs args, CancellationToken ct = default)
    {
        var resp = await SendRequestAsync("scopes", args, ct);
        return ParseBody<ScopesBody>(resp);
    }

    public virtual async Task<VariablesBody?> VariablesAsync(
        VariablesArgs args, CancellationToken ct = default)
    {
        var resp = await SendRequestAsync("variables", args, ct);
        return ParseBody<VariablesBody>(resp);
    }

    public virtual async Task<EvaluateBody?> EvaluateAsync(
        EvaluateArgs args, CancellationToken ct = default)
    {
        var resp = await SendRequestAsync("evaluate", args, ct);
        return ParseBody<EvaluateBody>(resp);
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    public virtual async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        if (_readerTask is not null)
        {
            try { await _readerTask.WaitAsync(TimeSpan.FromSeconds(2)); }
            catch { /* ignore */ }
        }
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}
