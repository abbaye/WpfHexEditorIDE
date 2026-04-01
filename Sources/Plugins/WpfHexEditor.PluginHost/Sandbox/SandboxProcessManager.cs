//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: Sandbox/SandboxProcessManager.cs
// Created: 2026-03-15
// Description:
//     Manages the lifecycle of WpfHexEditor.PluginSandbox.exe child processes.
//     One process per sandboxed plugin. Handles spawn, named-pipe handshake,
//     IPC dispatch and graceful / forceful termination.
//
// Architecture Notes:
//     - Pattern: Proxy + Process-per-plugin isolation
//     - Named pipe name: "WpfHexEditorSandbox_{pluginId}_{Guid}"
//     - The IDE is the pipe SERVER; the sandbox exe is the pipe CLIENT.
//     - Correlation IDs (Guid) match pending requests to incoming responses.
//     - MetricsPush / CrashNotification are routed to registered callbacks
//       without a pending correlation entry.
// ==========================================================

using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using WpfHexEditor.SDK.Sandbox;

namespace WpfHexEditor.PluginHost.Sandbox;

/// <summary>
/// Spawns and manages one WpfHexEditor.PluginSandbox.exe process per sandboxed plugin.
/// Provides async request/response IPC over a Named Pipe.
/// </summary>
internal sealed class SandboxProcessManager : IAsyncDisposable
{
    // ── Configuration ───────────────────────────────────────────────────────
    private static readonly string SandboxExePath = ResolveSandboxExePath();
    private const int ConnectTimeoutMs = 10_000;
    private const int RequestTimeoutMs = 15_000;

    // ── State ────────────────────────────────────────────────────────────────
    private readonly string _pluginId;
    private readonly string _pipeName;
    private readonly Action<string> _log;

    private Process? _process;
    private NamedPipeServerStream? _pipe;
    private IpcChannel? _channel;
    private Task? _receiveLoop;
    private CancellationTokenSource _cts = new();

    // Pending request → TaskCompletionSource for awaiter
    private readonly ConcurrentDictionary<string, TaskCompletionSource<SandboxEnvelope>>
        _pending = new(StringComparer.Ordinal);

    // ── Events (routed to proxy) ─────────────────────────────────────────────
    public event EventHandler<MetricsPushPayload>? MetricsPushed;
    public event EventHandler<CrashNotificationPayload>? CrashReceived;
    public event EventHandler? PluginReady;

    // Phase 9 — panel HWND bridge events
    public event EventHandler<RegisterPanelNotificationPayload>? PanelRegistered;
    public event EventHandler<RegisterDocumentTabNotificationPayload>? DocumentTabRegistered;
    public event EventHandler<UnregisterPanelNotificationPayload>? PanelUnregistered;

    // Phase 10 — menu / toolbar / status-bar bridge events
    public event EventHandler<RegisterMenuItemNotificationPayload>? MenuItemRegistered;
    public event EventHandler<UnregisterMenuItemNotificationPayload>? MenuItemUnregistered;
    public event EventHandler<RegisterToolbarItemNotificationPayload>? ToolbarItemRegistered;
    public event EventHandler<UnregisterToolbarItemNotificationPayload>? ToolbarItemUnregistered;
    public event EventHandler<RegisterStatusBarItemNotificationPayload>? StatusBarItemRegistered;
    public event EventHandler<UnregisterStatusBarItemNotificationPayload>? StatusBarItemUnregistered;

    // Phase 10 — panel visibility forwarding (Sandbox → IDE)
    public event EventHandler<PanelActionNotificationPayload>? PanelActionReceived;

    // Phase 11 — options page (Sandbox → IDE)
    public event EventHandler<RegisterOptionsPageNotificationPayload>? OptionsPageDeclared;

    // ──────────────────────────────────────────────────────────────────────────
    public SandboxProcessManager(string pluginId, Action<string>? logger = null)
    {
        _pluginId = pluginId ?? throw new ArgumentNullException(nameof(pluginId));
        _pipeName = $"WpfHexEditorSandbox_{pluginId.Replace('.', '_')}_{Guid.NewGuid():N}";
        _log = logger ?? (_ => { });
    }

    // ── Startup ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates the named pipe server, spawns the sandbox process and waits
    /// for it to connect within <see cref="ConnectTimeoutMs"/> milliseconds.
    /// </summary>
    public async Task StartAsync(CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        _pipe = new NamedPipeServerStream(
            _pipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        _process = SpawnSandboxProcess();
        _log($"[SandboxProcMgr:{_pluginId}] Spawned PID={_process.Id}, pipe={_pipeName}");

        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
        connectCts.CancelAfter(ConnectTimeoutMs);
        await _pipe.WaitForConnectionAsync(connectCts.Token).ConfigureAwait(false);

        _channel = new IpcChannel(_pipe);
        _receiveLoop = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);
        _log($"[SandboxProcMgr:{_pluginId}] Pipe connected.");
    }

    // ── Send / Receive ────────────────────────────────────────────────────────

    /// <summary>
    /// Sends a request envelope and waits for the matching response.
    /// Throws <see cref="TimeoutException"/> if no response arrives within the timeout.
    /// </summary>
    public async Task<SandboxEnvelope> SendRequestAsync(
        SandboxEnvelope request,
        int timeoutMs = RequestTimeoutMs,
        CancellationToken ct = default)
    {
        if (_channel is null) throw new InvalidOperationException("Sandbox not started.");

        var tcs = new TaskCompletionSource<SandboxEnvelope>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[request.CorrelationId] = tcs;

        try
        {
            await _channel.SendAsync(request, ct).ConfigureAwait(false);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeoutMs);

            await using (timeoutCts.Token.Register(() =>
                tcs.TrySetException(new TimeoutException(
                    $"Sandbox '{_pluginId}' did not respond to '{request.Kind}' within {timeoutMs}ms."))))
            {
                return await tcs.Task.ConfigureAwait(false);
            }
        }
        finally
        {
            _pending.TryRemove(request.CorrelationId, out _);
        }
    }

    /// <summary>Fire-and-forget send (for metrics push acks, etc.).</summary>
    public Task SendAsync(SandboxEnvelope envelope, CancellationToken ct = default)
        => _channel?.SendAsync(envelope, ct) ?? Task.CompletedTask;

    // ── Receive loop ──────────────────────────────────────────────────────────

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var envelope = await _channel!.ReceiveAsync<SandboxEnvelope>(ct).ConfigureAwait(false);
                if (envelope is null) break;

                DispatchIncoming(envelope);
            }
        }
        catch (OperationCanceledException) { /* normal shutdown */ }
        catch (Exception ex)
        {
            _log($"[SandboxProcMgr:{_pluginId}] Receive loop error: {ex.Message}");
            // Fail all pending requests
            foreach (var (_, tcs) in _pending)
                tcs.TrySetException(ex);
            _pending.Clear();
        }
    }

    private void DispatchIncoming(SandboxEnvelope envelope)
    {
        // Fire-and-forget push messages
        switch (envelope.Kind)
        {
            case SandboxMessageKind.MetricsPush:
                var metrics = Deserialize<MetricsPushPayload>(envelope.Payload);
                if (metrics is not null) MetricsPushed?.Invoke(this, metrics);
                return;

            case SandboxMessageKind.CrashNotification:
                var crash = Deserialize<CrashNotificationPayload>(envelope.Payload);
                if (crash is not null) CrashReceived?.Invoke(this, crash);
                return;

            case SandboxMessageKind.ReadyNotification:
                PluginReady?.Invoke(this, EventArgs.Empty);
                return;

            // Phase 9 — UI bridge notifications
            case SandboxMessageKind.RegisterPanelNotification:
                var panel = Deserialize<RegisterPanelNotificationPayload>(envelope.Payload);
                if (panel is not null) PanelRegistered?.Invoke(this, panel);
                return;

            case SandboxMessageKind.RegisterDocumentTabNotification:
                var tab = Deserialize<RegisterDocumentTabNotificationPayload>(envelope.Payload);
                if (tab is not null) DocumentTabRegistered?.Invoke(this, tab);
                return;

            case SandboxMessageKind.UnregisterPanelNotification:
                var unregister = Deserialize<UnregisterPanelNotificationPayload>(envelope.Payload);
                if (unregister is not null) PanelUnregistered?.Invoke(this, unregister);
                return;

            // Phase 10 — menu / toolbar / status-bar notifications
            case SandboxMessageKind.RegisterMenuItemNotification:
                var menuItem = Deserialize<RegisterMenuItemNotificationPayload>(envelope.Payload);
                if (menuItem is not null) MenuItemRegistered?.Invoke(this, menuItem);
                return;

            case SandboxMessageKind.UnregisterMenuItemNotification:
                var unregMenuItem = Deserialize<UnregisterMenuItemNotificationPayload>(envelope.Payload);
                if (unregMenuItem is not null) MenuItemUnregistered?.Invoke(this, unregMenuItem);
                return;

            case SandboxMessageKind.RegisterToolbarItemNotification:
                var toolbarItem = Deserialize<RegisterToolbarItemNotificationPayload>(envelope.Payload);
                if (toolbarItem is not null) ToolbarItemRegistered?.Invoke(this, toolbarItem);
                return;

            case SandboxMessageKind.UnregisterToolbarItemNotification:
                var unregToolbar = Deserialize<UnregisterToolbarItemNotificationPayload>(envelope.Payload);
                if (unregToolbar is not null) ToolbarItemUnregistered?.Invoke(this, unregToolbar);
                return;

            case SandboxMessageKind.RegisterStatusBarItemNotification:
                var statusItem = Deserialize<RegisterStatusBarItemNotificationPayload>(envelope.Payload);
                if (statusItem is not null) StatusBarItemRegistered?.Invoke(this, statusItem);
                return;

            case SandboxMessageKind.UnregisterStatusBarItemNotification:
                var unregStatus = Deserialize<UnregisterStatusBarItemNotificationPayload>(envelope.Payload);
                if (unregStatus is not null) StatusBarItemUnregistered?.Invoke(this, unregStatus);
                return;

            case SandboxMessageKind.PanelActionNotification:
                var panelAction = Deserialize<PanelActionNotificationPayload>(envelope.Payload);
                if (panelAction is not null) PanelActionReceived?.Invoke(this, panelAction);
                return;

            case SandboxMessageKind.RegisterOptionsPageNotification:
                var optionsPage = Deserialize<RegisterOptionsPageNotificationPayload>(envelope.Payload);
                if (optionsPage is not null) OptionsPageDeclared?.Invoke(this, optionsPage);
                return;
        }

        // Request/response pairing
        if (!string.IsNullOrEmpty(envelope.CorrelationId)
            && _pending.TryGetValue(envelope.CorrelationId, out var tcs))
        {
            tcs.TrySetResult(envelope);
        }
        else
        {
            _log($"[SandboxProcMgr:{_pluginId}] Unmatched envelope kind={envelope.Kind}");
        }
    }

    // ── Shutdown ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Gracefully terminates the sandbox: sends ShutdownRequest (1.5 s grace),
    /// then force-kills regardless of response.
    /// The short IPC timeout ensures ForceKill runs well within the 3 s watchdog
    /// window used by <c>WpfPluginHost.UnloadPluginAsync</c>.
    /// </summary>
    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_channel is not null)
        {
            try
            {
                var req = BuildRequest(SandboxMessageKind.ShutdownRequest,
                    new ShutdownRequestPayload { Reason = "HostShutdown" });
                // 1.5 s: enough time for a healthy sandbox to ack; short enough that
                // ForceKill always fires before the 3 s watchdog timeout in the host.
                await SendRequestAsync(req, timeoutMs: 1_500, ct: ct).ConfigureAwait(false);
            }
            catch { /* best-effort — ForceKill below is the real termination */ }
        }

        await ForceKillAsync().ConfigureAwait(false);
    }

    private async Task ForceKillAsync()
    {
        _cts.Cancel();

        if (_receiveLoop is not null)
            try { await _receiveLoop.ConfigureAwait(false); } catch { }

        if (_process is { HasExited: false })
        {
            try { _process.Kill(entireProcessTree: true); }
            catch { /* best-effort */ }

            try
            {
                await _process.WaitForExitAsync(CancellationToken.None)
                    .WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            }
            catch { }
        }

        _log($"[SandboxProcMgr:{_pluginId}] Terminated.");
    }

    public async ValueTask DisposeAsync()
    {
        await ForceKillAsync().ConfigureAwait(false);

        if (_channel is not null) await _channel.DisposeAsync().ConfigureAwait(false);
        _pipe?.Dispose();
        _process?.Dispose();
        _cts.Dispose();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Process SpawnSandboxProcess()
    {
        var psi = new ProcessStartInfo
        {
            FileName = SandboxExePath,
            Arguments = $"\"{_pipeName}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
        };

        var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start sandbox process: {SandboxExePath}");

        // Assign to the host-lifetime job object so the sandbox is killed automatically
        // when the host process exits for any reason (crash, debugger stop, kill).
        SandboxJobObject.Assign(process);

        process.Exited += (_, _) =>
            _log($"[SandboxProcMgr:{_pluginId}] Process exited (code={SafeExitCode(process)}).");
        process.EnableRaisingEvents = true;

        return process;
    }

    internal static SandboxEnvelope BuildRequest<T>(SandboxMessageKind kind, T payload)
        where T : class
        => new()
        {
            Kind = kind,
            CorrelationId = Guid.NewGuid().ToString("N"),
            Payload = JsonSerializer.Serialize(payload),
        };

    private static T? Deserialize<T>(string json)
    {
        if (string.IsNullOrEmpty(json)) return default;
        try { return JsonSerializer.Deserialize<T>(json); }
        catch { return default; }
    }

    private static int SafeExitCode(Process p)
    {
        try { return p.ExitCode; } catch { return -1; }
    }

    private static string ResolveSandboxExePath()
    {
        var exeDir = AppContext.BaseDirectory;
        var candidate = Path.Combine(exeDir, "WpfHexEditor.PluginSandbox.exe");
        return File.Exists(candidate) ? candidate : "WpfHexEditor.PluginSandbox.exe";
    }
}
