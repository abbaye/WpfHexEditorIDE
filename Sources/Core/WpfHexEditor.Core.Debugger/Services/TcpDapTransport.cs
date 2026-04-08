// ==========================================================
// Project: WpfHexEditor.Core.Debugger
// File: Services/TcpDapTransport.cs
// Description:
//     DapClientBase subclass backed by a TCP socket instead of stdio.
//     Used by debugpy (Python) and node --inspect-brk (JavaScript/TypeScript)
//     adapters which listen on a TCP port rather than reading from stdin/stdout.
//
// Architecture Notes:
//     Same JSON-RPC Content-Length framing as the stdio transport.
//     ConnectAsync retries up to MaxRetries times with a 200 ms gap to tolerate
//     a slow adapter startup (debugpy needs ~500 ms to bind the port).
// ==========================================================

using System.Net;
using System.Net.Sockets;

namespace WpfHexEditor.Core.Debugger.Services;

/// <summary>
/// DAP transport over TCP. Connects to an adapter that is already listening
/// on <paramref name="host"/>:<paramref name="port"/>.
/// </summary>
public class TcpDapTransport : DapClientBase
{
    private const int MaxRetries = 15;
    private const int RetryDelayMs = 200;

    private readonly TcpClient _tcp = new();
    private Stream? _stream;

    protected override Stream InputStream  => _stream!;
    protected override Stream OutputStream => _stream!;

    protected TcpDapTransport() { }

    /// <summary>
    /// Connects to the adapter at <paramref name="host"/>:<paramref name="port"/>,
    /// retrying up to <see cref="MaxRetries"/> times to allow the adapter to start.
    /// </summary>
    public static async Task<TcpDapTransport> ConnectAsync(
        string host, int port, CancellationToken ct = default)
    {
        var transport = new TcpDapTransport();
        await transport.ConnectCoreAsync(host, port, ct).ConfigureAwait(false);
        return transport;
    }

    /// <summary>
    /// Performs TCP connection with retry logic. Call from subclass <c>CreateAsync</c>
    /// after constructing the instance (but before returning it to callers).
    /// </summary>
    protected async Task ConnectCoreAsync(string host, int port, CancellationToken ct)
    {
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                await _tcp.ConnectAsync(host, port, ct).ConfigureAwait(false);
                break;
            }
            catch (SocketException) when (attempt < MaxRetries - 1)
            {
                await Task.Delay(RetryDelayMs, ct).ConfigureAwait(false);
            }
        }

        _stream = _tcp.GetStream();
        StartReader();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync().ConfigureAwait(false);
        _tcp.Dispose();
    }
}
