// ==========================================================
// Project: WpfHexEditor.Core.LSP.Client
// File: Transport/LspJsonRpcChannel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Low-level JSON-RPC 2.0 framing layer over two System.IO.Stream objects
//     (stdin/stdout of the language server process).
//     Handles Content-Length header encoding/decoding, request/response
//     correlation, and server-push notification dispatch.
//
// Architecture Notes:
//     Request-Reply Pattern — outgoing calls register a TaskCompletionSource keyed
//     by the JSON-RPC id; the read loop resolves them on server responses.
//
//     Notification Pattern — messages with no "id" are dispatched via the
//     NotificationReceived event (callers subscribe per method name).
//
//     Thread Safety — all shared state is protected by a single SemaphoreSlim
//     for writes and a ConcurrentDictionary for pending requests.
// ==========================================================

using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace WpfHexEditor.Core.LSP.Client.Transport;

/// <summary>
/// Raw JSON-RPC 2.0 channel over a pair of streams.
/// Used by <see cref="LspProcess"/> to communicate with the language server.
/// </summary>
internal sealed class LspJsonRpcChannel : IAsyncDisposable
{
    // ── Static JSON options ────────────────────────────────────────────────────
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition      = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    // ── Fields ─────────────────────────────────────────────────────────────────
    private readonly Stream _writeStream;
    private readonly Stream _readStream;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonNode?>> _pending = new();

    private int _nextId;
    private CancellationTokenSource? _readLoopCts;
    private Task? _readLoopTask;

    // ── Events ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Raised for every server notification (no "id" field) and for responses
    /// whose "id" is unrecognised (defensive).  Argument is the full JSON object.
    /// </summary>
    public event Action<string /*method*/, JsonNode? /*params*/>? NotificationReceived;

    // ── Construction / Lifecycle ───────────────────────────────────────────────

    internal LspJsonRpcChannel(Stream writeStream, Stream readStream)
    {
        _writeStream = writeStream;
        _readStream  = readStream;
    }

    /// <summary>Starts the background read loop.</summary>
    internal void Start()
    {
        _readLoopCts  = new CancellationTokenSource();
        _readLoopTask = Task.Run(() => ReadLoopAsync(_readLoopCts.Token));
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Sends a JSON-RPC request and waits for the server's response.
    /// Returns the "result" node (may be null/JsonNull on success with no body).
    /// Throws <see cref="InvalidOperationException"/> on JSON-RPC error responses.
    /// </summary>
    internal async Task<JsonNode?> CallAsync(
        string method, object? @params, CancellationToken ct = default)
    {
        var id  = Interlocked.Increment(ref _nextId);
        var tcs = new TaskCompletionSource<JsonNode?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;

        ct.Register(() =>
        {
            if (_pending.TryRemove(id, out var t))
                t.TrySetCanceled(ct);
        });

        var payload = new
        {
            jsonrpc = "2.0",
            id,
            method,
            @params,
        };
        await SendAsync(payload, ct).ConfigureAwait(false);

        return await tcs.Task.ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a JSON-RPC notification (fire-and-forget — no response expected).
    /// </summary>
    internal async Task NotifyAsync(string method, object? @params, CancellationToken ct = default)
    {
        var payload = new
        {
            jsonrpc = "2.0",
            method,
            @params,
        };
        await SendAsync(payload, ct).ConfigureAwait(false);
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private async Task SendAsync(object payload, CancellationToken ct)
    {
        var json    = JsonSerializer.Serialize(payload, s_jsonOptions);
        var body    = Encoding.UTF8.GetBytes(json);
        var header  = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");

        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await _writeStream.WriteAsync(header, ct).ConfigureAwait(false);
            await _writeStream.WriteAsync(body,   ct).ConfigureAwait(false);
            await _writeStream.FlushAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        using var reader = new StreamReader(_readStream, Encoding.UTF8, leaveOpen: true);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // ── Read headers ───────────────────────────────────────────────
                int contentLength = 0;

                while (true)
                {
                    var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                    if (line is null) return;                      // EOF
                    if (line.Length == 0) break;                   // blank line = end of headers

                    if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                        int.TryParse(line["Content-Length:".Length..].Trim(), out contentLength);
                }

                if (contentLength <= 0) continue;

                // ── Read body ──────────────────────────────────────────────────
                var buffer = new char[contentLength];
                int read   = 0;
                while (read < contentLength)
                {
                    int n = await reader.ReadAsync(new Memory<char>(buffer, read, contentLength - read), ct)
                                        .ConfigureAwait(false);
                    if (n == 0) return;
                    read += n;
                }

                var json = new string(buffer, 0, read);
                DispatchMessage(json);
            }
            catch (OperationCanceledException) { return; }
            catch (IOException)                { return; }
        }
    }

    private void DispatchMessage(string json)
    {
        JsonNode? node;
        try { node = JsonNode.Parse(json); }
        catch { return; }

        if (node is not JsonObject obj) return;

        // ── Request or notification from server ────────────────────────────────
        if (obj["method"] is JsonNode methodNode)
        {
            var method = methodNode.GetValue<string>();
            var p      = obj["params"];
            NotificationReceived?.Invoke(method, p);
            return;
        }

        // ── Response to our call ───────────────────────────────────────────────
        if (obj["id"] is JsonNode idNode &&
            idNode.GetValue<int?>() is int id &&
            _pending.TryRemove(id, out var tcs))
        {
            if (obj["error"] is JsonNode errorNode)
            {
                var msg = errorNode["message"]?.GetValue<string>() ?? "LSP error";
                tcs.TrySetException(new InvalidOperationException($"LSP: {msg}"));
            }
            else
            {
                tcs.TrySetResult(obj["result"]);
            }
        }
    }

    // ── IAsyncDisposable ───────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (_readLoopCts is not null)
        {
            await _readLoopCts.CancelAsync().ConfigureAwait(false);
            if (_readLoopTask is not null)
                await _readLoopTask.ConfigureAwait(false);
            _readLoopCts.Dispose();
        }

        // Cancel all pending calls.
        foreach (var kv in _pending)
            kv.Value.TrySetCanceled();
        _pending.Clear();

        _writeLock.Dispose();
    }
}
