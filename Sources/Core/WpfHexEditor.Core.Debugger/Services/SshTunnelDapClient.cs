// ==========================================================
// Project: WpfHexEditor.Core.Debugger
// File: Services/SshTunnelDapClient.cs
// Description: DAP client that tunnels to a remote debug adapter via SSH port forwarding.
//              Spawns an ssh process with -L (local tunnel) to forward a local port to the
//              remote adapter port, then delegates all DAP traffic to TcpDapClient.
// Architecture: IDapClient decorator over TcpDapClient — SSH tunnel is a thin process wrapper.
//              No external SSH library dependency; requires ssh.exe in PATH (standard on
//              Windows 10+, macOS, Linux).
// ==========================================================

using System.Diagnostics;
using WpfHexEditor.Core.Debugger.Protocol;

namespace WpfHexEditor.Core.Debugger.Services;

/// <summary>
/// DAP client for remote debugging over SSH.
/// Starts an SSH tunnel process (<c>ssh -N -L localPort:remoteHost:remotePort user@sshHost</c>)
/// then delegates all DAP traffic to a <see cref="TcpDapClient"/> on the tunnel endpoint.
/// </summary>
public sealed class SshTunnelDapClient : IDapClient
{
    private TcpDapClient? _inner;
    private Process?      _sshProcess;

    // ── IDapClient event delegation ───────────────────────────────────────────

    public event EventHandler<StoppedEventBody>? Stopped
    {
        add    { if (_inner is not null) _inner.Stopped += value; }
        remove { if (_inner is not null) _inner.Stopped -= value; }
    }

    public event EventHandler<OutputEventBody>? Output
    {
        add    { if (_inner is not null) _inner.Output += value; }
        remove { if (_inner is not null) _inner.Output -= value; }
    }

    public event EventHandler<ExitedEventBody>? Exited
    {
        add    { if (_inner is not null) _inner.Exited += value; }
        remove { if (_inner is not null) _inner.Exited -= value; }
    }

    public event EventHandler? Terminated
    {
        add    { if (_inner is not null) _inner.Terminated += value; }
        remove { if (_inner is not null) _inner.Terminated -= value; }
    }

    /// <summary>
    /// Establishes the SSH tunnel and connects the DAP transport.
    /// </summary>
    public async Task ConnectAsync(
        string sshUser,
        string sshHost,
        int    sshPort           = 22,
        string remoteAdapterHost = "localhost",
        int    remoteAdapterPort = 4711,
        int    localTunnelPort   = 14711,
        string? sshKeyPath       = null,
        CancellationToken ct     = default)
    {
        var keyArg  = sshKeyPath is not null ? $"-i \"{sshKeyPath}\" " : string.Empty;
        var forward = $"{localTunnelPort}:{remoteAdapterHost}:{remoteAdapterPort}";
        var args    = $"-N {keyArg}-L {forward} -p {sshPort} -o StrictHostKeyChecking=no -o BatchMode=yes {sshUser}@{sshHost}";

        _sshProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName               = "ssh",
                Arguments              = args,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            }
        };
        _sshProcess.Start();

        // Give SSH time to establish the local tunnel before connecting the TCP client.
        await Task.Delay(1200, ct);

        _inner = new TcpDapClient();
        await _inner.ConnectAsync("127.0.0.1", localTunnelPort, ct);
    }

    // ── IDapClient delegation ─────────────────────────────────────────────────

    private TcpDapClient Inner => _inner ?? throw new InvalidOperationException("Not connected — call ConnectAsync first.");

    public Task<CapabilitiesBody?> InitializeAsync(InitializeRequestArgs args, CancellationToken ct = default)
        => Inner.InitializeAsync(args, ct);

    public Task LaunchAsync(LaunchRequestArgs args, CancellationToken ct = default)
        => Inner.LaunchAsync(args, ct);

    public Task AttachAsync(AttachRequestArgs args, CancellationToken ct = default)
        => Inner.AttachAsync(args, ct);

    public Task ConfigurationDoneAsync(CancellationToken ct = default)
        => Inner.ConfigurationDoneAsync(ct);

    public Task DisconnectAsync(DisconnectArgs? args = null, CancellationToken ct = default)
        => Inner.DisconnectAsync(args, ct);

    public Task<SetBreakpointsBody?> SetBreakpointsAsync(SetBreakpointsArgs args, CancellationToken ct = default)
        => Inner.SetBreakpointsAsync(args, ct);

    public Task ContinueAsync(ContinueArgs args, CancellationToken ct = default)
        => Inner.ContinueAsync(args, ct);

    public Task NextAsync(StepArgs args, CancellationToken ct = default)
        => Inner.NextAsync(args, ct);

    public Task StepInAsync(StepArgs args, CancellationToken ct = default)
        => Inner.StepInAsync(args, ct);

    public Task StepOutAsync(StepArgs args, CancellationToken ct = default)
        => Inner.StepOutAsync(args, ct);

    public Task PauseAsync(PauseArgs args, CancellationToken ct = default)
        => Inner.PauseAsync(args, ct);

    public Task<ThreadsBody?> ThreadsAsync(CancellationToken ct = default)
        => Inner.ThreadsAsync(ct);

    public Task<StackTraceBody?> StackTraceAsync(StackTraceArgs args, CancellationToken ct = default)
        => Inner.StackTraceAsync(args, ct);

    public Task<ScopesBody?> ScopesAsync(ScopesArgs args, CancellationToken ct = default)
        => Inner.ScopesAsync(args, ct);

    public Task<VariablesBody?> VariablesAsync(VariablesArgs args, CancellationToken ct = default)
        => Inner.VariablesAsync(args, ct);

    public Task<EvaluateBody?> EvaluateAsync(EvaluateArgs args, CancellationToken ct = default)
        => Inner.EvaluateAsync(args, ct);

    public Task<SetVariableBody?> SetVariableAsync(SetVariableArgs args, CancellationToken ct = default)
        => Inner.SetVariableAsync(args, ct);

    public Task<GotoTargetsBody?> GotoTargetsAsync(GotoTargetsArgs args, CancellationToken ct = default)
        => Inner.GotoTargetsAsync(args, ct);

    public Task GotoAsync(GotoArgs args, CancellationToken ct = default)
        => Inner.GotoAsync(args, ct);

    public Task SetExceptionBreakpointsAsync(SetExceptionBreakpointsArgs args, CancellationToken ct = default)
        => Inner.SetExceptionBreakpointsAsync(args, ct);

    public Task<ModulesBody> GetModulesAsync(ModulesArgs? args = null, CancellationToken ct = default)
        => Inner.GetModulesAsync(args, ct);

    public Task<DisassembleBody> DisassembleAsync(DisassembleArgs args, CancellationToken ct = default)
        => Inner.DisassembleAsync(args, ct);

    public Task<ReadMemoryBody?> ReadMemoryAsync(ReadMemoryArgs args, CancellationToken ct = default)
        => Inner.ReadMemoryAsync(args, ct);

    public Task WriteMemoryAsync(WriteMemoryArgs args, CancellationToken ct = default)
        => Inner.WriteMemoryAsync(args, ct);

    public Task RestartFrameAsync(RestartFrameArgs args, CancellationToken ct = default)
        => Inner.RestartFrameAsync(args, ct);

    public Task<DataBreakpointInfoBody?> DataBreakpointInfoAsync(DataBreakpointInfoArgs args, CancellationToken ct = default)
        => Inner.DataBreakpointInfoAsync(args, ct);

    public Task<SetDataBreakpointsBody?> SetDataBreakpointsAsync(SetDataBreakpointsArgs args, CancellationToken ct = default)
        => Inner.SetDataBreakpointsAsync(args, ct);

    // ── Dispose ───────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (_inner is not null)
            await _inner.DisposeAsync();

        if (_sshProcess is not null && !_sshProcess.HasExited)
        {
            try { _sshProcess.Kill(); } catch { /* best effort */ }
            _sshProcess.Dispose();
        }
    }
}
