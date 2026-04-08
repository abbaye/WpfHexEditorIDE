// ==========================================================
// Project: WpfHexEditor.Core.Debugger
// File: Adapters/NodeInspectorAdapter.cs
// Description:
//     DAP adapter for JavaScript / TypeScript via node --inspect-brk.
//     Spawns node with the CDP inspect port, then wraps it with the
//     standard DAP TCP framing via TcpDapTransport.
//
// Architecture Notes:
//     Requires node v14+ on PATH (--inspect-brk opens a V8 inspector endpoint).
//     Port chosen dynamically in range 9229–9329.
//     Registered in DebuggerPlugin under language IDs "javascript" and "typescript".
// ==========================================================

using System.Diagnostics;
using WpfHexEditor.Core.Debugger.Models;
using WpfHexEditor.Core.Debugger.Protocol;
using WpfHexEditor.Core.Debugger.Services;

namespace WpfHexEditor.Core.Debugger.Adapters;

/// <summary>
/// Debug adapter for JavaScript/TypeScript via <c>node --inspect-brk</c>.
/// Requires Node.js v14+ on PATH.
/// </summary>
public sealed class NodeInspectorAdapter : TcpDapTransport
{
    private readonly Process _process;

    private NodeInspectorAdapter(Process process) => _process = process;

    /// <summary>
    /// Spawns <c>node --inspect-brk=host:port script.js</c> and connects
    /// the TCP transport to the V8 inspector port.
    /// </summary>
    public static async Task<NodeInspectorAdapter> CreateAsync(
        DebugLaunchConfig config, CancellationToken ct = default)
    {
        int port    = FindFreePort(9229, 9329);
        string host = "127.0.0.1";

        var psi = new ProcessStartInfo("node", BuildNodeArgs(host, port, config))
        {
            UseShellExecute = false,
            CreateNoWindow  = true,
        };

        if (!string.IsNullOrWhiteSpace(config.WorkDir))
            psi.WorkingDirectory = config.WorkDir;

        foreach (var (k, v) in config.Env)
            psi.EnvironmentVariables[k] = v;

        var process = Process.Start(psi)!;
        var adapter = new NodeInspectorAdapter(process);
        await adapter.ConnectCoreAsync(host, port, ct).ConfigureAwait(false);
        return adapter;
    }

    /// <summary>Build a <see cref="LaunchRequestArgs"/> for a Node.js program.</summary>
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

    private static string BuildNodeArgs(string host, int port, DebugLaunchConfig config)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"--inspect-brk={host}:{port}");
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
