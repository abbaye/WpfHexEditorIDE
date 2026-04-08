// ==========================================================
// Project: WpfHexEditor.Core.Debugger
// File: Adapters/DebugPyAdapter.cs
// Description:
//     DAP adapter for Python via debugpy.
//     Spawns "python -m debugpy --listen host:port --wait-for-client"
//     then connects the TcpDapTransport to that port.
//
// Architecture Notes:
//     Extends TcpDapTransport — inherits TCP connection + DAP framing.
//     Port chosen dynamically in ephemeral range 5678–5778.
//     Registered in DebuggerPlugin under language ID "python".
// ==========================================================

using System.Diagnostics;
using WpfHexEditor.Core.Debugger.Models;
using WpfHexEditor.Core.Debugger.Protocol;
using WpfHexEditor.Core.Debugger.Services;

namespace WpfHexEditor.Core.Debugger.Adapters;

/// <summary>
/// Debug adapter for Python programs via <c>debugpy</c>.
/// Requires <c>python -m debugpy</c> to be available on PATH.
/// </summary>
public sealed class DebugPyAdapter : TcpDapTransport
{
    private readonly Process _process;

    private DebugPyAdapter(Process process) => _process = process;

    /// <summary>
    /// Spawns <c>python -m debugpy --listen host:port --wait-for-client</c>
    /// then connects the TCP transport to the listening port.
    /// </summary>
    public static async Task<DebugPyAdapter> CreateAsync(
        DebugLaunchConfig config, CancellationToken ct = default)
    {
        int port    = FindFreePort(5678, 5778);
        string host = "127.0.0.1";

        var psi = new ProcessStartInfo("python", BuildDebugPyArgs(host, port, config))
        {
            UseShellExecute  = false,
            CreateNoWindow   = true,
        };

        if (!string.IsNullOrWhiteSpace(config.WorkDir))
            psi.WorkingDirectory = config.WorkDir;

        foreach (var (k, v) in config.Env)
            psi.EnvironmentVariables[k] = v;

        var process = Process.Start(psi)!;
        var adapter = new DebugPyAdapter(process);
        await adapter.ConnectCoreAsync(host, port, ct).ConfigureAwait(false);
        return adapter;
    }

    /// <summary>Build a <see cref="LaunchRequestArgs"/> for a Python program.</summary>
    public static LaunchRequestArgs BuildLaunchArgs(DebugLaunchConfig config) => new(
        Program:     config.ProgramPath,
        Args:        config.Args.Length > 0 ? config.Args : null,
        Cwd:         config.WorkDir,
        Env:         config.Env.Count > 0 ? config.Env : null,
        StopAtEntry: config.StopAtEntry);

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync().ConfigureAwait(false);
        try { _process.Kill(entireProcessTree: true); } catch { /* may already be dead */ }
        _process.Dispose();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string BuildDebugPyArgs(string host, int port, DebugLaunchConfig config)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"-m debugpy --listen {host}:{port} --wait-for-client");
        if (config.StopAtEntry) sb.Append(" --stop-on-entry");
        sb.Append($" \"{config.ProgramPath}\"");
        foreach (var arg in config.Args) sb.Append($" {arg}");
        return sb.ToString();
    }

    private static int FindFreePort(int start, int end)
    {
        for (int p = start; p <= end; p++)
        {
            try
            {
                using var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, p);
                l.Start(); l.Stop();
                return p;
            }
            catch { /* port in use */ }
        }
        return start;
    }
}
