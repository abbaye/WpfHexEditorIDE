// ==========================================================
// Project: WpfHexEditor.Core.Debugger
// File: Services/TcpDapClient.cs
// Description: DAP client that connects to a debug adapter listening on a TCP socket.
//              Used for remote debugging scenarios where the adapter is already running
//              on a remote host and listening for connections (e.g. netcoredbg --server).
// Architecture: Extends DapClientBase — only supplies TCP NetworkStream as transport.
// ==========================================================

using System.Net;
using System.Net.Sockets;

namespace WpfHexEditor.Core.Debugger.Services;

/// <summary>
/// DAP client that connects to a debug adapter over TCP.
/// Typical usage: adapter started with <c>netcoredbg --server --port 4711</c>.
/// </summary>
public sealed class TcpDapClient : DapClientBase
{
    private TcpClient? _tcpClient;
    private Stream?    _stream;

    protected override Stream InputStream  => _stream ?? throw new InvalidOperationException("Not connected.");
    protected override Stream OutputStream => _stream ?? throw new InvalidOperationException("Not connected.");

    // Exposed internal for SshTunnelDapClient which wraps this instance.
    internal Stream InternalStream => _stream ?? throw new InvalidOperationException("Not connected.");

    /// <summary>
    /// Connects to a DAP server at the given host/port and starts the read loop.
    /// </summary>
    public async Task ConnectAsync(string host, int port, CancellationToken ct = default)
    {
        _tcpClient = new TcpClient();
        await _tcpClient.ConnectAsync(host, port, ct);
        _stream = _tcpClient.GetStream();
        StartReader();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        _stream?.Dispose();
        _tcpClient?.Dispose();
    }
}
