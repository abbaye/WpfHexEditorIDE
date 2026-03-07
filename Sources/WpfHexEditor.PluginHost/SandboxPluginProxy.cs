//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Diagnostics;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.PluginHost;

/// <summary>
/// Proxy for an out-of-process plugin running inside WpfHexEditor.PluginSandbox.exe.
/// Phase 5 stub â€” IPC channel not yet implemented.
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
    public Version Version => Version.TryParse(_manifest.Version, out var v) ? v : new Version(0, 0);
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
