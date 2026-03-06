// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: SandboxPluginProxy.cs
// Author: Auto
// Created: 2026-03-06
// Description:
//     In-process proxy for sandboxed plugins running in WpfHexEditor.PluginSandbox.exe.
//     Marshals IWpfHexEditorPlugin method calls over a Named Pipe IPC channel.
//     Full implementation is Phase 5; this stub enables Phase 1 compilation and wiring.
//
// Architecture Notes:
//     Proxy pattern — implements IWpfHexEditorPlugin, delegates to out-of-process plugin.
//     Named Pipe connection lifecycle tied to Process lifetime.
//     If the sandbox process exits unexpectedly, proxy raises PluginCrashed.
//
// ==========================================================

using System.Diagnostics;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.PluginHost;

/// <summary>
/// Proxy for an out-of-process plugin running inside WpfHexEditor.PluginSandbox.exe.
/// Phase 5 stub — IPC channel not yet implemented.
/// </summary>
internal sealed class SandboxPluginProxy : IWpfHexEditorPlugin, IAsyncDisposable
{
    private readonly PluginManifest _manifest;
    private Process? _sandboxProcess;

    public SandboxPluginProxy(PluginManifest manifest)
    {
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
    }

    public string Id => _manifest.Id;
    public string Name => _manifest.Name;
    public string Version => _manifest.Version;
    public PluginCapabilities Capabilities => new();

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct)
    {
        // Phase 5: spawn PluginSandbox.exe, establish IPC, call remote InitializeAsync.
        throw new NotSupportedException(
            "Sandbox isolation mode is not yet implemented. Use InProcess isolation mode for Phase 1-4.");
    }

    public Task ShutdownAsync(CancellationToken ct)
    {
        return TerminateAsync(ct);
    }

    private async Task TerminateAsync(CancellationToken ct)
    {
        if (_sandboxProcess is { HasExited: false })
        {
            try
            {
                _sandboxProcess.Kill(entireProcessTree: true);
                await _sandboxProcess.WaitForExitAsync(ct).ConfigureAwait(false);
            }
            catch { /* best-effort */ }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await TerminateAsync(CancellationToken.None).ConfigureAwait(false);
        _sandboxProcess?.Dispose();
    }
}
