//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

// ==========================================================
// Project: WpfHexEditor.SDK
// File: Sandbox/IpcChannel.cs
// Created: 2026-03-15
// Description:
//     Bidirectional Named Pipe IPC channel using length-prefixed JSON framing.
//     Shared between WpfHexEditor.PluginHost (pipe server side, via SandboxProcessManager)
//     and WpfHexEditor.PluginSandbox.exe (pipe client side, via Program.cs).
//     Moved to SDK so both projects can reference it without a cross-project dependency.
//
// Architecture Notes:
//     - Pattern: Channel / Transport abstraction
//     - Framing: 4-byte little-endian length prefix + UTF-8 JSON body
//     - Write lock (SemaphoreSlim) prevents interleaved writes from concurrent callers
//     - 1 MB max message size guard prevents malformed-length DoS
// ==========================================================

using System.IO.Pipes;
using System.Text.Json;

namespace WpfHexEditor.SDK.Sandbox;

/// <summary>
/// Bidirectional Named Pipe IPC channel with length-prefixed JSON framing.
/// </summary>
public sealed class IpcChannel : IAsyncDisposable
{
    private readonly PipeStream _pipe;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public IpcChannel(PipeStream pipe) => _pipe = pipe;

    /// <summary>Serialises <paramref name="message"/> and sends it over the pipe.</summary>
    public async Task SendAsync<T>(T message, CancellationToken ct = default)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(message);
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var prefix = BitConverter.GetBytes(json.Length);
            await _pipe.WriteAsync(prefix, ct).ConfigureAwait(false);
            await _pipe.WriteAsync(json, ct).ConfigureAwait(false);
            await _pipe.FlushAsync(ct).ConfigureAwait(false);
        }
        finally { _writeLock.Release(); }
    }

    /// <summary>Reads one message from the pipe and deserialises it to <typeparamref name="T"/>.</summary>
    public async Task<T?> ReceiveAsync<T>(CancellationToken ct = default)
    {
        var prefix = new byte[4];
        var read = await _pipe.ReadAsync(prefix.AsMemory(), ct).ConfigureAwait(false);
        if (read < 4) return default;

        var length = BitConverter.ToInt32(prefix);
        if (length <= 0 || length > 1024 * 1024) return default; // 1 MB safety guard

        var buffer = new byte[length];
        read = await _pipe.ReadAsync(buffer.AsMemory(), ct).ConfigureAwait(false);
        if (read < length) return default;

        return JsonSerializer.Deserialize<T>(buffer);
    }

    public ValueTask DisposeAsync()
    {
        _writeLock.Dispose();
        _pipe.Dispose();
        return ValueTask.CompletedTask;
    }
}
