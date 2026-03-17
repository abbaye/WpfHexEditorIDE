// ==========================================================
// Project: WpfHexEditor.LSP.Client
// File: Transport/LspProcess.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Manages the external language server executable lifecycle.
//     Starts the process with stdin/stdout redirected, constructs
//     the LspJsonRpcChannel, and performs the LSP initialize handshake.
//
// Architecture Notes:
//     Facade Pattern — hides process management and handshake details
//     behind a simple Start/Stop API consumed by LspClientImpl.
// ==========================================================

using System.Diagnostics;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using WpfHexEditor.LSP.Client.Transport;

namespace WpfHexEditor.LSP.Client.Transport;

/// <summary>
/// Starts and owns a language server process, exposing the raw JSON-RPC channel.
/// </summary>
internal sealed class LspProcess : IAsyncDisposable
{
    private Process?              _process;
    private LspJsonRpcChannel?    _channel;

    internal LspJsonRpcChannel Channel
        => _channel ?? throw new InvalidOperationException("LspProcess not started.");

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Launches the language server executable and performs the LSP
    /// initialize / initialized handshake.
    /// </summary>
    /// <param name="executablePath">Full path to the server executable.</param>
    /// <param name="arguments">Optional command-line arguments.</param>
    /// <param name="workspacePath">Root URI sent in the initialize request (may be null).</param>
    internal async Task StartAsync(
        string executablePath,
        string? arguments,
        string? workspacePath,
        CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(executablePath, arguments ?? string.Empty)
        {
            UseShellExecute        = false,
            RedirectStandardInput  = true,
            RedirectStandardOutput = true,
            RedirectStandardError  = false,   // let stderr go to the host console for diagnostics
            CreateNoWindow         = true,
        };

        _process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start LSP server: {executablePath}");

        _channel = new LspJsonRpcChannel(
            _process.StandardInput.BaseStream,
            _process.StandardOutput.BaseStream);
        _channel.Start();

        await HandshakeAsync(workspacePath, ct).ConfigureAwait(false);
    }

    private async Task HandshakeAsync(string? workspacePath, CancellationToken ct)
    {
        // Build a minimal initialize params; the server fills in capabilities.
        var rootUri = workspacePath is not null
            ? new Uri(workspacePath).AbsoluteUri
            : null;

        var initParams = new
        {
            processId   = Environment.ProcessId,
            rootUri,
            capabilities = new
            {
                textDocument = new
                {
                    completion   = new { completionItem = new { snippetSupport = false } },
                    hover        = new { contentFormat  = new[] { "plaintext", "markdown" } },
                    publishDiagnostics = new { relatedInformation = false },
                    definition   = new { },
                    references   = new { },
                    signatureHelp = new { },
                },
                workspace = new { },
            },
        };

        await Channel.CallAsync("initialize", initParams, ct).ConfigureAwait(false);
        await Channel.NotifyAsync("initialized", new { }, ct).ConfigureAwait(false);
    }

    // ── Graceful shutdown ──────────────────────────────────────────────────────

    internal async Task ShutdownAsync(CancellationToken ct = default)
    {
        if (_channel is not null)
        {
            try
            {
                await _channel.CallAsync("shutdown", null, ct).ConfigureAwait(false);
                await _channel.NotifyAsync("exit", null, ct).ConfigureAwait(false);
            }
            catch { /* ignore — process may already be dead */ }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
            await _channel.DisposeAsync().ConfigureAwait(false);

        if (_process is not null)
        {
            if (!_process.HasExited)
            {
                try { _process.Kill(entireProcessTree: true); } catch { }
            }
            _process.Dispose();
        }
    }
}
