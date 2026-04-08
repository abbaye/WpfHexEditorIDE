// ==========================================================
// Project: WpfHexEditor.Core.Debugger
// File: Adapters/NetCoreDapAdapter.cs
// Description:
//     DAP client for netcoredbg / vsdbg.
//     Spawns the adapter process and wires its stdin/stdout
//     to DapClientBase for JSON-RPC framing.
// ==========================================================

using System.Diagnostics;
using WpfHexEditor.Core.Debugger.Models;
using WpfHexEditor.Core.Debugger.Protocol;
using WpfHexEditor.Core.Debugger.Services;

namespace WpfHexEditor.Core.Debugger.Adapters;

/// <summary>
/// .NET debug adapter that wraps a netcoredbg or vsdbg process.
/// Launched on demand; disposed when the session ends.
/// </summary>
public sealed class NetCoreDapAdapter : DapClientBase
{
    private readonly Process _process;

    protected override Stream InputStream  => _process.StandardOutput.BaseStream;
    protected override Stream OutputStream => _process.StandardInput.BaseStream;

    private NetCoreDapAdapter(Process process) => _process = process;

    /// <summary>
    /// Spawns the adapter process and returns a ready-to-use <see cref="NetCoreDapAdapter"/>.
    /// </summary>
    /// <param name="adapterPath">Full path to netcoredbg or vsdbg.</param>
    /// <param name="ct">Cancellation token.</param>
    public static Task<NetCoreDapAdapter> CreateAsync(string adapterPath, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(adapterPath, "--interpreter=vscode")
        {
            UseShellExecute        = false,
            RedirectStandardInput  = true,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true,
            StandardInputEncoding  = System.Text.Encoding.UTF8,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
        };

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.Start();

        var adapter = new NetCoreDapAdapter(process);
        adapter.StartReader();

        return Task.FromResult(adapter);
    }

    /// <summary>Build a <see cref="LaunchRequestArgs"/> from a <see cref="DebugLaunchConfig"/>.</summary>
    public static LaunchRequestArgs BuildLaunchArgs(DebugLaunchConfig config) => new(
        Program:    config.ProgramPath,
        Args:       config.Args.Length > 0 ? config.Args : null,
        Cwd:        config.WorkDir ?? Path.GetDirectoryName(config.ProgramPath),
        Env:        config.Env.Count > 0 ? config.Env : null,
        StopAtEntry: config.StopAtEntry,
        JustMyCode: config.JustMyCode
    );

    public override async ValueTask DisposeAsync()
    {
        try
        {
            if (!_process.HasExited)
            {
                await DisconnectAsync(new DisconnectArgs(TerminateDebuggee: true))
                    .WaitAsync(TimeSpan.FromSeconds(3));
            }
        }
        catch { /* ignore timeout/errors on dispose */ }

        await base.DisposeAsync();

        try
        {
            if (!_process.HasExited) _process.Kill();
        }
        catch { /* ignore */ }

        _process.Dispose();
    }
}
