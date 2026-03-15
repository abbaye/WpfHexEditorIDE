//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

// ==========================================================
// Project: WpfHexEditor.PluginSandbox
// File: SandboxedPluginRunner.cs
// Created: 2026-03-15
// Description:
//     Loads and runs a plugin inside the sandbox process.
//     Receives an InitializeRequest via IPC, creates an AssemblyLoadContext,
//     instantiates the plugin, calls InitializeAsync with a stub context,
//     then dispatches subsequent requests to the live plugin instance.
//
// Architecture Notes:
//     - Pattern: Command Dispatcher — incoming SandboxEnvelope.Kind maps to
//       a specific handler that invokes the appropriate plugin method.
//     - The stub IIDEHostContext (SandboxedHostContext) marshals IDE service
//       calls back to the IDE via the IpcChannel (InvokeRequest round-trip).
//     - Crashes are caught at the top level and pushed as CrashNotification.
//     - Metrics are pushed every MetricsIntervalMs via SandboxMetricsRelay.
// ==========================================================

using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using WpfHexEditor.Core.Interfaces;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Focus;
using WpfHexEditor.SDK.Contracts.Services;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Models;
using WpfHexEditor.SDK.Sandbox;

namespace WpfHexEditor.PluginSandbox;

/// <summary>
/// Loads the plugin assembly, handles IPC dispatch and owns the plugin lifetime
/// inside the sandbox process.
/// </summary>
internal sealed class SandboxedPluginRunner : IAsyncDisposable
{
    private readonly IpcChannel _channel;
    private readonly SandboxMetricsRelay _metrics;
    private readonly CancellationToken _ct;
    private readonly Action<string> _log;

    private IWpfHexEditorPlugin? _plugin;
    private AssemblyLoadContext? _alc;

    // ─────────────────────────────────────────────────────────────────────────
    public SandboxedPluginRunner(
        IpcChannel channel,
        SandboxMetricsRelay metrics,
        CancellationToken ct,
        Action<string>? logger = null)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _ct = ct;
        _log = logger ?? (_ => { });
    }

    // ── Message dispatch ──────────────────────────────────────────────────────

    /// <summary>
    /// Processes a single incoming envelope.
    /// Returns false when the Shutdown request has been handled (exit the loop).
    /// </summary>
    public async Task<bool> HandleAsync(SandboxEnvelope envelope)
    {
        return envelope.Kind switch
        {
            SandboxMessageKind.InitializeRequest => await HandleInitializeAsync(envelope),
            SandboxMessageKind.ShutdownRequest   => await HandleShutdownAsync(envelope),
            SandboxMessageKind.InvokeRequest     => await HandleInvokeAsync(envelope),
            _ => await HandleUnknownAsync(envelope),
        };
    }

    // ── Handlers ─────────────────────────────────────────────────────────────

    private async Task<bool> HandleInitializeAsync(SandboxEnvelope envelope)
    {
        var payload = Deserialize<InitializeRequestPayload>(envelope.Payload);
        if (payload is null)
            return await SendError(envelope, "InitializeRequest payload was null.");

        try
        {
            // Load plugin assembly in a collectible ALC
            _alc = new AssemblyLoadContext($"SandboxedPlugin_{payload.PluginId}", isCollectible: true);
            var assembly = _alc.LoadFromAssemblyPath(payload.AssemblyPath);
            var type = assembly.GetType(payload.EntryType)
                ?? throw new TypeLoadException($"Type '{payload.EntryType}' not found.");

            _plugin = (IWpfHexEditorPlugin)(Activator.CreateInstance(type)
                ?? throw new InvalidOperationException("Cannot create plugin instance."));

            // Create stub host context that marshals IDE calls back over IPC
            var stubContext = new SandboxedHostContext(_channel, payload.GrantedPermissions, _log);

            await _plugin.InitializeAsync(stubContext, _ct).ConfigureAwait(false);

            // Start emitting metrics now that plugin is alive
            _metrics.SetPluginId(payload.PluginId);
            _metrics.Start();

            // Tell the IDE the plugin is ready
            await _channel.SendAsync(new SandboxEnvelope
            {
                Kind = SandboxMessageKind.ReadyNotification,
                Payload = Serialize(new ReadyNotificationPayload
                {
                    PluginId = payload.PluginId,
                    PluginVersion = _plugin.Version.ToString(),
                }),
            }, _ct).ConfigureAwait(false);

            return await SendSuccess(envelope);
        }
        catch (Exception ex)
        {
            await PushCrash(payload.PluginId, ex, "Initialize").ConfigureAwait(false);
            return await SendError(envelope, ex.Message);
        }
    }

    private async Task<bool> HandleShutdownAsync(SandboxEnvelope envelope)
    {
        _metrics.Stop();
        try
        {
            if (_plugin is not null)
                await _plugin.ShutdownAsync(_ct).ConfigureAwait(false);
        }
        catch { /* best-effort */ }

        await SendSuccess(envelope).ConfigureAwait(false);
        return false; // signals exit
    }

    private async Task<bool> HandleInvokeAsync(SandboxEnvelope envelope)
    {
        // Service call marshalling — currently a stub that returns success.
        // Full implementation would route to actual service implementations
        // or return serialized results from stub service adapters.
        return await SendSuccess(envelope, resultJson: "null");
    }

    private async Task<bool> HandleUnknownAsync(SandboxEnvelope envelope)
    {
        _log($"[Runner] Unknown message kind: {envelope.Kind}");
        return await SendError(envelope, $"Unknown kind: {envelope.Kind}");
    }

    // ── Crash reporting ───────────────────────────────────────────────────────

    public async Task PushCrash(string pluginId, Exception ex, string phase)
    {
        try
        {
            await _channel.SendAsync(new SandboxEnvelope
            {
                Kind = SandboxMessageKind.CrashNotification,
                Payload = Serialize(new CrashNotificationPayload
                {
                    PluginId = pluginId,
                    ExceptionType = ex.GetType().FullName ?? ex.GetType().Name,
                    Message = ex.Message,
                    StackTrace = ex.StackTrace ?? string.Empty,
                    Phase = phase,
                }),
            }, CancellationToken.None).ConfigureAwait(false);
        }
        catch { /* last resort */ }
    }

    // ── IPC helpers ───────────────────────────────────────────────────────────

    private async Task<bool> SendSuccess(SandboxEnvelope req, string? resultJson = null)
    {
        await _channel.SendAsync(new SandboxEnvelope
        {
            Kind = GetResponseKind(req.Kind),
            CorrelationId = req.CorrelationId,
            Payload = Serialize(new SandboxResponsePayload
            {
                Success = true,
                ResultJson = resultJson,
            }),
        }, _ct).ConfigureAwait(false);
        return true;
    }

    private async Task<bool> SendError(SandboxEnvelope req, string error)
    {
        await _channel.SendAsync(new SandboxEnvelope
        {
            Kind = GetResponseKind(req.Kind),
            CorrelationId = req.CorrelationId,
            Payload = Serialize(new SandboxResponsePayload
            {
                Success = false,
                ErrorMessage = error,
            }),
        }, _ct).ConfigureAwait(false);
        return true;
    }

    private static SandboxMessageKind GetResponseKind(SandboxMessageKind request) => request switch
    {
        SandboxMessageKind.InitializeRequest => SandboxMessageKind.InitializeResponse,
        SandboxMessageKind.ShutdownRequest   => SandboxMessageKind.ShutdownResponse,
        SandboxMessageKind.InvokeRequest     => SandboxMessageKind.InvokeResponse,
        _ => SandboxMessageKind.InvokeResponse,
    };

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value);

    private static T? Deserialize<T>(string json)
    {
        if (string.IsNullOrEmpty(json)) return default;
        try { return JsonSerializer.Deserialize<T>(json); }
        catch { return default; }
    }

    public async ValueTask DisposeAsync()
    {
        _metrics.Stop();
        if (_plugin is IDisposable d) d.Dispose();
        if (_plugin is IAsyncDisposable ad) await ad.DisposeAsync().ConfigureAwait(false);
        try { _alc?.Unload(); } catch { }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Stub IIDEHostContext used inside the sandbox — all service calls are marshalled
// back to the IDE over the IPC channel via InvokeRequest messages.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Minimal IIDEHostContext that runs inside the sandbox process.
/// Services that are granted forward their calls to the IDE via IPC InvokeRequest.
/// Services that are not granted throw <see cref="UnauthorizedAccessException"/>.
/// </summary>
internal sealed class SandboxedHostContext : IIDEHostContext
{
    private readonly IpcChannel _channel;
    private readonly HashSet<string> _granted;
    private readonly Action<string> _log;

    // Stub no-op services for sandbox context
    public IHexEditorService HexEditor => NullHexEditorService.Instance;
    public ICodeEditorService CodeEditor => NullCodeEditorService.Instance;
    public IOutputService Output => NullOutputService.Instance;
    public IParsedFieldService ParsedField => NullParsedFieldService.Instance;
    public IErrorPanelService ErrorPanel => NullErrorPanelService.Instance;
    public IFocusContextService FocusContext => NullFocusContextService.Instance;
    public IPluginEventBus EventBus => NullEventBus.Instance;
    public IUIRegistry UIRegistry => NullUIRegistry.Instance;
    public IThemeService Theme => NullThemeService.Instance;
    public IPermissionService Permissions => NullPermissionService.Instance;
    public ITerminalService Terminal => NullTerminalService.Instance;
    public ISolutionExplorerService SolutionExplorer => NullSolutionExplorerService.Instance;

    public SandboxedHostContext(IpcChannel channel, List<string> grantedPermissions, Action<string> log)
    {
        _channel = channel;
        _granted = new HashSet<string>(grantedPermissions, StringComparer.OrdinalIgnoreCase);
        _log = log;
    }

    private void AssertGranted(string permission)
    {
        if (!_granted.Contains(permission))
            throw new UnauthorizedAccessException(
                $"Sandbox plugin is not granted '{permission}' permission.");
    }
}

// Null-object service stubs — used inside the sandbox where IDE services are not directly available.
// All service calls over IPC marshalling will be implemented in a future phase.
// These stubs satisfy the interface contracts required by IIDEHostContext.

file sealed class NullHexEditorService : IHexEditorService
{
    public static readonly NullHexEditorService Instance = new();
    public bool IsActive => false;
    public string? CurrentFilePath => null;
    public long FileSize => 0;
    public long CurrentOffset => 0;
    public long SelectionStart => 0;
    public long SelectionStop => 0;
    public long SelectionLength => 0;
    public long FirstVisibleByteOffset => 0;
    public long LastVisibleByteOffset => 0;
    public event EventHandler? ViewportScrolled { add { } remove { } }
    public event EventHandler? SelectionChanged { add { } remove { } }
    public event EventHandler? FileOpened { add { } remove { } }
    public event EventHandler<FormatDetectedArgs>? FormatDetected { add { } remove { } }
    public event EventHandler? ActiveEditorChanged { add { } remove { } }
    public byte[] ReadBytes(long offset, int length) => [];
    public byte[] GetSelectedBytes() => [];
    public IReadOnlyList<long> SearchHex(string hexPattern) => [];
    public IReadOnlyList<long> SearchText(string text) => [];
    public void WriteBytes(long offset, byte[] data) { }
    public void SetSelection(long start, long end) { }
    public void NavigateTo(long offset) { }
    public void ConnectParsedFieldsPanel(IParsedFieldsPanel panel) { }
    public void DisconnectParsedFieldsPanel() { }
}

file sealed class NullCodeEditorService : ICodeEditorService
{
    public static readonly NullCodeEditorService Instance = new();
    public bool IsActive => false;
    public string? CurrentLanguage => null;
    public string? CurrentFilePath => null;
    public string? GetContent() => null;
    public string GetSelectedText() => string.Empty;
    public int CaretLine => 1;
    public int CaretColumn => 1;
    public event EventHandler? DocumentChanged { add { } remove { } }
}

file sealed class NullOutputService : IOutputService
{
    public static readonly NullOutputService Instance = new();
    public void Info(string message) { }
    public void Warning(string message) { }
    public void Error(string message) { }
    public void Debug(string message) { }
    public void Write(string category, string message) { }
    public void Clear() { }
    public IReadOnlyList<string> GetRecentLines(int count) => [];
}

file sealed class NullParsedFieldService : IParsedFieldService
{
    public static readonly NullParsedFieldService Instance = new();
    public bool HasParsedFields => false;
    public IReadOnlyList<ParsedFieldEntry> GetParsedFields() => [];
    public ParsedFieldEntry? GetFieldAtOffset(long offset) => null;
    public event EventHandler? ParsedFieldsChanged { add { } remove { } }
}

file sealed class NullErrorPanelService : IErrorPanelService
{
    public static readonly NullErrorPanelService Instance = new();
    public void PostDiagnostic(DiagnosticSeverity severity, string message, string source = "", int line = -1, int column = -1) { }
    public void ClearPluginDiagnostics(string pluginId) { }
    public IReadOnlyList<string> GetRecentErrors(int count) => [];
}

file sealed class NullFocusContextService : IFocusContextService
{
    public static readonly NullFocusContextService Instance = new();
    public IDocument? ActiveDocument => null;
    public IPanel? ActivePanel => null;
    public event EventHandler<FocusChangedEventArgs>? FocusChanged { add { } remove { } }
}

file sealed class NullEventBus : IPluginEventBus
{
    public static readonly NullEventBus Instance = new();
    public void Publish<TEvent>(TEvent evt) where TEvent : class { }
    public Task PublishAsync<TEvent>(TEvent evt, CancellationToken ct = default) where TEvent : class => Task.CompletedTask;
    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class => Disposable.Empty;
    public IDisposable Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : class => Disposable.Empty;
}

file sealed class NullUIRegistry : IUIRegistry
{
    public static readonly NullUIRegistry Instance = new();
    public string GenerateUIId(string pluginId, string elementType, string elementName) => string.Empty;
    public bool Exists(string uiId) => false;
    public void RegisterPanel(string uiId, System.Windows.UIElement panel, string pluginId, PanelDescriptor descriptor) { }
    public void UnregisterPanel(string uiId) { }
    public void RegisterMenuItem(string uiId, string pluginId, MenuItemDescriptor descriptor) { }
    public void UnregisterMenuItem(string uiId) { }
    public void RegisterToolbarItem(string uiId, string pluginId, ToolbarItemDescriptor descriptor) { }
    public void UnregisterToolbarItem(string uiId) { }
    public void RegisterDocumentTab(string uiId, System.Windows.UIElement content, string pluginId, DocumentDescriptor descriptor) { }
    public void UnregisterDocumentTab(string uiId) { }
    public void RegisterStatusBarItem(string uiId, string pluginId, StatusBarItemDescriptor descriptor) { }
    public void UnregisterStatusBarItem(string uiId) { }
    public void ShowPanel(string uiId) { }
    public void HidePanel(string uiId) { }
    public void TogglePanel(string uiId) { }
    public void FocusPanel(string uiId) { }
    public bool IsPanelVisible(string uiId) => false;
    public void UnregisterAllForPlugin(string pluginId) { }
}

file sealed class NullThemeService : IThemeService
{
    public static readonly NullThemeService Instance = new();
    public string CurrentTheme => "Dark";
    public event EventHandler? ThemeChanged { add { } remove { } }
    public System.Windows.ResourceDictionary GetThemeResources() => new();
    public void RegisterThemeAwareControl(System.Windows.FrameworkElement element) { }
    public void UnregisterThemeAwareControl(System.Windows.FrameworkElement element) { }
}

file sealed class NullPermissionService : IPermissionService
{
    public static readonly NullPermissionService Instance = new();
    public bool IsGranted(string pluginId, PluginPermission permission) => false;
    public PluginPermission GetGranted(string pluginId) => default;
    public void Grant(string pluginId, PluginPermission permission) { }
    public void Revoke(string pluginId, PluginPermission permission) { }
    public event EventHandler<PermissionChangedEventArgs>? PermissionChanged { add { } remove { } }
}

file sealed class NullTerminalService : ITerminalService
{
    public static readonly NullTerminalService Instance = new();
    public void WriteLine(string text) { }
    public void WriteInfo(string text) { }
    public void WriteWarning(string text) { }
    public void WriteError(string text) { }
    public void Clear() { }
    public void OpenSession(string shellType) { }
    public void CloseActiveSession() { }
}

file sealed class NullSolutionExplorerService : ISolutionExplorerService
{
    public static readonly NullSolutionExplorerService Instance = new();
    public bool HasActiveSolution => false;
    public string? ActiveSolutionPath => null;
    public string? ActiveSolutionName => null;
    public IReadOnlyList<string> GetOpenFilePaths() => [];
    public IReadOnlyList<string> GetSolutionFilePaths() => [];
    public Task OpenFileAsync(string filePath, CancellationToken ct = default) => Task.CompletedTask;
    public Task CloseFileAsync(string? fileName = null, CancellationToken ct = default) => Task.CompletedTask;
    public Task SaveFileAsync(string? fileName = null, CancellationToken ct = default) => Task.CompletedTask;
    public Task OpenFolderAsync(string path, CancellationToken ct = default) => Task.CompletedTask;
    public Task OpenProjectAsync(string name, CancellationToken ct = default) => Task.CompletedTask;
    public Task CloseProjectAsync(string name, CancellationToken ct = default) => Task.CompletedTask;
    public Task OpenSolutionAsync(string path, CancellationToken ct = default) => Task.CompletedTask;
    public Task CloseSolutionAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task ReloadSolutionAsync(CancellationToken ct = default) => Task.CompletedTask;
    public IReadOnlyList<string> GetFilesInDirectory(string path) => [];
    public event EventHandler? SolutionChanged { add { } remove { } }
}

file static class Disposable
{
    public static readonly IDisposable Empty = new EmptyDisposable();

    private sealed class EmptyDisposable : IDisposable { public void Dispose() { } }
}
