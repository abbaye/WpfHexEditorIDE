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

using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Windows.Interop;
using WpfHexEditor.Core.Interfaces;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.Events;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Focus;
using WpfHexEditor.SDK.Contracts.Services;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Events;
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
    private IpcUIRegistry? _uiRegistry;
    private SandboxedHostContext? _stubContext;
    private readonly ThemeBootstrapper _themeBootstrapper = new();

    // Phase 11 — Options page HwndSource (kept alive for the lifetime of the sandbox)
    private HwndSource? _optionsPageSource;

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
            SandboxMessageKind.InitializeRequest          => await HandleInitializeAsync(envelope),
            SandboxMessageKind.ShutdownRequest            => await HandleShutdownAsync(envelope),
            SandboxMessageKind.InvokeRequest              => await HandleInvokeAsync(envelope),
            SandboxMessageKind.ResizePanelRequest         => await HandleResizePanelAsync(envelope),
            SandboxMessageKind.ThemeChangedNotification   => await HandleThemeChangedAsync(envelope),
            SandboxMessageKind.ExecuteCommandRequest      => await HandleExecuteCommandAsync(envelope),

            // Phase 12 — HexEditor event bridge
            SandboxMessageKind.HexEditorStateNotification        => HandleHexEditorStateNotification(envelope),
            SandboxMessageKind.ParsedFieldsSnapshotNotification  => HandleParsedFieldsSnapshotNotification(envelope),
            SandboxMessageKind.TemplateApplyBroadcastNotification => HandleTemplateApplyBroadcast(envelope),

            // IDE EventBus bridge — host forwards typed IDE events
            SandboxMessageKind.IDEEventNotification => HandleIDEEventNotification(envelope),

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
            // 1. Apply host theme Source URIs first (Style / ControlTemplate resources).
            //    These must exist in Application.Resources BEFORE InitializeComponent()
            //    is called so {StaticResource PanelToolbarStyle} resolves at parse time.
            if (payload.ThemeDictionaryUris.Count > 0)
                _themeBootstrapper.ApplyUris(payload.ThemeDictionaryUris);

            // 2. Apply serialised primitive resources (SolidColorBrush, Color…).
            //    Added AFTER the URI dicts so inline values override embedded ones.
            if (!string.IsNullOrEmpty(payload.ThemeResourcesXaml))
                _themeBootstrapper.Apply(payload.ThemeResourcesXaml);

            // 3. Load plugin assembly in a collectible ALC.
            //    The Resolving event probes the plugin's own directory so private
            //    dependencies (e.g. WpfHexEditor.Core.AssemblyAnalysis.dll) are found
            //    even if they are not present in the sandbox base directory.
            var pluginDir = Path.GetDirectoryName(payload.AssemblyPath) ?? string.Empty;
            _alc = new AssemblyLoadContext($"SandboxedPlugin_{payload.PluginId}", isCollectible: true);
            _alc.Resolving += (ctx, name) =>
            {
                if (string.IsNullOrEmpty(pluginDir) || string.IsNullOrEmpty(name.Name))
                    return null;
                var probe = Path.Combine(pluginDir, name.Name + ".dll");
                return File.Exists(probe) ? ctx.LoadFromAssemblyPath(probe) : null;
            };
            var assembly = _alc.LoadFromAssemblyPath(payload.AssemblyPath);
            var type = assembly.GetType(payload.EntryType)
                ?? throw new TypeLoadException($"Type '{payload.EntryType}' not found.");

            _plugin = (IWpfHexEditorPlugin)(Activator.CreateInstance(type)
                ?? throw new InvalidOperationException("Cannot create plugin instance."));

            // Create IPC-backed UIRegistry so plugin panels are forwarded to the host
            _uiRegistry = new IpcUIRegistry(_channel, _ct);

            // Create stub host context that marshals IDE calls back over IPC
            var stubContext = new SandboxedHostContext(_channel, payload.GrantedPermissions,
                _uiRegistry, _log);
            _stubContext = stubContext; // kept for Phase 12 event delivery

            await _plugin.InitializeAsync(stubContext, _ct).ConfigureAwait(false);

            // Phase 11 — If the plugin has an options page, create its HwndSource eagerly
            // and notify the host with the HWND so it can embed the page in the Options dialog.
            // Sent BEFORE ReadyNotification so the host has the data when it unblocks.
            if (_plugin is IPluginWithOptions opts)
            {
                try
                {
                    opts.LoadOptions();
                    var optionsContent = opts.CreateOptionsPage();
                    if (optionsContent is not null)
                    {
                        _optionsPageSource = new HwndSource(
                            new HwndSourceParameters($"OptionsPage_{payload.PluginId}")
                            {
                                WindowStyle = unchecked((int)0x80000000), // WS_POPUP
                                Width  = 640,
                                Height = 400,
                            })
                        { RootVisual = optionsContent };

                        await _channel.SendAsync(new SandboxEnvelope
                        {
                            Kind = SandboxMessageKind.RegisterOptionsPageNotification,
                            Payload = Serialize(new RegisterOptionsPageNotificationPayload
                            {
                                PluginId   = payload.PluginId,
                                PluginName = payload.PluginName,
                                Hwnd       = _optionsPageSource.Handle.ToInt64(),
                            }),
                        }, _ct).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    _log($"[Runner:{payload.PluginId}] Options page creation failed: {ex.Message}");
                }
            }

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

    private Task<bool> HandleResizePanelAsync(SandboxEnvelope envelope)
    {
        var payload = Deserialize<ResizePanelRequestPayload>(envelope.Payload);
        if (payload is not null)
            _uiRegistry?.HandleResize(payload.ContentId, payload.Width, payload.Height);
        return Task.FromResult(true); // fire-and-forget, no response needed
    }

    private Task<bool> HandleThemeChangedAsync(SandboxEnvelope envelope)
    {
        var payload = Deserialize<ThemeChangedNotificationPayload>(envelope.Payload);
        if (payload is not null && !string.IsNullOrEmpty(payload.ThemeResourcesXaml))
            _themeBootstrapper.Apply(payload.ThemeResourcesXaml);
        return Task.FromResult(true); // fire-and-forget, no response needed
    }

    private Task<bool> HandleExecuteCommandAsync(SandboxEnvelope envelope)
    {
        // ExecuteCommandRequest is fire-and-forget: no response envelope expected.
        // Must be dispatched on the STA Dispatcher thread since the plugin may
        // update WPF controls in response (e.g. show a panel).
        var payload = Deserialize<ExecuteCommandRequestPayload>(envelope.Payload);
        if (payload is not null)
        {
            var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
            if (dispatcher.CheckAccess())
                _uiRegistry?.ExecuteCommand(payload.CommandId);
            else
                dispatcher.InvokeAsync(() => _uiRegistry?.ExecuteCommand(payload.CommandId));
        }
        return Task.FromResult(true);
    }

    // ── Phase 12 — HexEditor event bridge ─────────────────────────────────────
    // All three handlers run on the WPF STA thread (see Program.cs InvokeAsync),
    // so panel and event-bus operations are UI-thread safe without extra dispatch.

    private bool HandleHexEditorStateNotification(SandboxEnvelope envelope)
    {
        var payload = Deserialize<HexEditorStateNotificationPayload>(envelope.Payload);
        if (payload is not null)
            _stubContext?.HexEditorIpc.ApplyState(payload);
        return true;
    }

    private bool HandleParsedFieldsSnapshotNotification(SandboxEnvelope envelope)
    {
        var payload = Deserialize<ParsedFieldsSnapshotNotificationPayload>(envelope.Payload);
        if (payload is null) return true;

        var panel = _stubContext?.HexEditorIpc.ConnectedPanel;
        if (panel is null) return true;

        panel.Clear();
        panel.TotalFileSize = payload.FileSize;
        foreach (var f in payload.Fields)
        {
            panel.ParsedFields.Add(new ParsedFieldViewModel
            {
                Name           = f.Name,
                Offset         = f.Offset,
                Length         = f.Length,
                ValueType      = f.DataType,
                FormattedValue = f.ValueDisplay,
            });
        }
        panel.RefreshView();
        return true;
    }

    private bool HandleTemplateApplyBroadcast(SandboxEnvelope envelope)
    {
        var payload = Deserialize<TemplateApplyBroadcastNotificationPayload>(envelope.Payload);
        if (payload is null) return true;

        var evt = new TemplateApplyRequestedEvent
        {
            TemplateName = payload.TemplateName,
            Blocks       = payload.Blocks.Select(b => new ParsedBlockInfo
            {
                Name         = b.Name,
                Offset       = b.Offset,
                Length       = b.Length,
                TypeHint     = b.TypeHint,
                DisplayValue = b.DisplayValue,
            }).ToList(),
        };

        _stubContext?.EventBusIpc.RaiseTemplateApply(evt);
        return true;
    }

    // ── IDE EventBus bridge ───────────────────────────────────────────────────

    /// <summary>
    /// Deserializes an <see cref="IDEEventNotificationPayload"/> forwarded from the host
    /// and publishes the concrete event on the sandbox-local <see cref="SandboxLocalEventBus"/>
    /// so that sandbox plugins receive IDE events identically to in-process plugins.
    /// Must be called on the WPF STA thread (guaranteed by Program.cs InvokeAsync).
    /// </summary>
    private bool HandleIDEEventNotification(SandboxEnvelope envelope)
    {
        var payload = Deserialize<IDEEventNotificationPayload>(envelope.Payload);
        if (payload is null || _stubContext is null) return true;

        // Resolve the concrete event type from the Events assembly.
        var eventType = Type.GetType(
            $"WpfHexEditor.Events.IDEEvents.{payload.EventTypeName}, WpfHexEditor.Events");
        if (eventType is null)
        {
            _log($"[Runner] Unknown IDE event type: {payload.EventTypeName}");
            return true;
        }

        object? evtObj;
        try { evtObj = JsonSerializer.Deserialize(payload.EventJson, eventType); }
        catch (Exception ex)
        {
            _log($"[Runner] IDE event deserialize failed ({payload.EventTypeName}): {ex.Message}");
            return true;
        }
        if (evtObj is null) return true;

        // Invoke SandboxLocalEventBus.Publish<TEvent>(evt) via reflection.
        var publishMethod = typeof(SandboxLocalEventBus)
            .GetMethod(nameof(SandboxLocalEventBus.Publish))!
            .MakeGenericMethod(eventType);
        try { publishMethod.Invoke(_stubContext.SandboxBus, [evtObj]); }
        catch (Exception ex) { _log($"[Runner] IDE event publish failed: {ex.Message}"); }
        return true;
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
        _uiRegistry?.DisposeAll();
        _themeBootstrapper.Remove();
        _optionsPageSource?.Dispose();
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

    // Phase 12 — IPC-backed services (instance, not singleton, so state is per-plugin)
    internal readonly IpcHexEditorService HexEditorIpc = new();
    internal readonly IpcEventBus         EventBusIpc  = new();

    // IIDEHostContext services
    public IHexEditorService    HexEditor       => HexEditorIpc;
    public IPluginEventBus      EventBus        => EventBusIpc;
    public ICodeEditorService   CodeEditor      => NullCodeEditorService.Instance;
    public IOutputService       Output          => NullOutputService.Instance;
    public IParsedFieldService  ParsedField     => NullParsedFieldService.Instance;
    public IErrorPanelService   ErrorPanel      => NullErrorPanelService.Instance;
    public IFocusContextService FocusContext    => NullFocusContextService.Instance;
    public IUIRegistry          UIRegistry      { get; }
    public IThemeService        Theme           => NullThemeService.Instance;
    public IPermissionService   Permissions     => NullPermissionService.Instance;
    public ITerminalService     Terminal        => NullTerminalService.Instance;
    public ISolutionExplorerService SolutionExplorer => NullSolutionExplorerService.Instance;

    // IDE EventBus — sandbox plugins subscribe to IDE events forwarded via IPC bridge.
    // A lightweight in-process bus is created per sandbox; the host delivers events via the
    // IDEEventNotification IPC message which calls SandboxBus.Publish() on the receive path.
    internal readonly SandboxLocalEventBus SandboxBus = new();
    public IIDEEventBus IDEEvents => SandboxBus;

    // Capability and extension registries are not available inside the sandbox process.
    // Stub implementations return empty results — sandbox plugins cannot query cross-plugin state.
    public IPluginCapabilityRegistry CapabilityRegistry => NullPluginCapabilityRegistry.Instance;
    public IExtensionRegistry ExtensionRegistry => NullExtensionRegistry.Instance;

    public SandboxedHostContext(
        IpcChannel channel,
        List<string> grantedPermissions,
        IpcUIRegistry uiRegistry,
        Action<string> log)
    {
        _channel = channel;
        _granted = new HashSet<string>(grantedPermissions, StringComparer.OrdinalIgnoreCase);
        UIRegistry = uiRegistry;
        _log = log;
    }

    private void AssertGranted(string permission)
    {
        if (!_granted.Contains(permission))
            throw new UnauthorizedAccessException(
                $"Sandbox plugin is not granted '{permission}' permission.");
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Phase 12 — IPC-backed IHexEditorService
// Maintains cached IDE state and raises events when the IDE pushes
// HexEditorStateNotification messages over the pipe.
// ─────────────────────────────────────────────────────────────────────────────

internal sealed class IpcHexEditorService : IHexEditorService
{
    // Cached state (updated by ApplyState)
    private bool    _isActive;
    private string? _currentFilePath;
    private long    _fileSize;
    private long    _selectionStart;
    private long    _selectionStop;
    private long    _currentOffset;

    /// <summary>Panel connected by the plugin via ConnectParsedFieldsPanel.</summary>
    internal IParsedFieldsPanel? ConnectedPanel { get; private set; }

    // ── IHexEditorService properties ──────────────────────────────────────────
    public bool    IsActive               => _isActive;
    public string? CurrentFilePath        => _currentFilePath;
    public long    FileSize               => _fileSize;
    public long    CurrentOffset          => _currentOffset;
    public long    SelectionStart         => _selectionStart;
    public long    SelectionStop          => _selectionStop;
    public long    SelectionLength        => Math.Max(0L, _selectionStop - _selectionStart + 1);
    public long    FirstVisibleByteOffset => 0;  // not forwarded in Phase 12
    public long    LastVisibleByteOffset  => 0;  // not forwarded in Phase 12

    // ── Events ────────────────────────────────────────────────────────────────
    public event EventHandler? SelectionChanged;
    public event EventHandler? FileOpened;
    public event EventHandler? ActiveEditorChanged;
    // ViewportScrolled and FormatDetected are not forwarded in Phase 12
#pragma warning disable 67
    public event EventHandler? ViewportScrolled { add { } remove { } }
    public event EventHandler<FormatDetectedArgs>? FormatDetected { add { } remove { } }
#pragma warning restore 67

    // ── Panel connection ──────────────────────────────────────────────────────
    public void ConnectParsedFieldsPanel(IParsedFieldsPanel panel)  => ConnectedPanel = panel;
    public void DisconnectParsedFieldsPanel()                        => ConnectedPanel = null;

    // ── Methods (read/write deferred to future phase) ─────────────────────────
    public byte[] ReadBytes(long offset, int length)         => [];
    public byte[] GetSelectedBytes()                         => [];
    public IReadOnlyList<long> SearchHex(string hexPattern)  => [];
    public IReadOnlyList<long> SearchText(string text)       => [];
    public void WriteBytes(long offset, byte[] data)         { }
    public void SetSelection(long start, long end)           { }
    public void NavigateTo(long offset)                      { }

    // ── Called by SandboxedPluginRunner on HexEditorStateNotification ─────────
    /// <summary>
    /// Updates the cached state snapshot and raises the appropriate event so
    /// sandbox plugins respond exactly as they would for in-process IDE events.
    /// Must be called on the WPF STA thread (guaranteed by Program.cs InvokeAsync).
    /// </summary>
    internal void ApplyState(HexEditorStateNotificationPayload p)
    {
        _isActive        = p.IsActive;
        _currentFilePath = p.CurrentFilePath;
        _fileSize        = p.FileSize;
        _selectionStart  = p.SelectionStart;
        _selectionStop   = p.SelectionStop;
        _currentOffset   = p.CurrentOffset;

        switch (p.EventKind)
        {
            case "SelectionChanged":    SelectionChanged?.Invoke(this, EventArgs.Empty);    break;
            case "FileOpened":          FileOpened?.Invoke(this, EventArgs.Empty);          break;
            case "ActiveEditorChanged": ActiveEditorChanged?.Invoke(this, EventArgs.Empty); break;
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Phase 12 — IPC-backed IPluginEventBus
// Subscriptions are stored locally. The IDE pushes TemplateApplyBroadcastNotification
// and the runner calls RaiseTemplateApply to dispatch to all subscribers.
// ─────────────────────────────────────────────────────────────────────────────

internal sealed class IpcEventBus : IPluginEventBus
{
    private readonly List<(Type EventType, object Handler)> _handlers = [];

    public void Publish<TEvent>(TEvent evt)                                          where TEvent : class { /* sandbox→IDE publish deferred to future phase */ }
    public Task PublishAsync<TEvent>(TEvent evt, CancellationToken ct = default)     where TEvent : class => Task.CompletedTask;

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class
    {
        var entry = (typeof(TEvent), (object)handler);
        _handlers.Add(entry);
        return new ActionDisposable(() => _handlers.Remove(entry));
    }

    public IDisposable Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : class
        => Disposable.Empty; // async subscriptions deferred to future phase

    /// <summary>
    /// Called by SandboxedPluginRunner on TemplateApplyBroadcastNotification.
    /// Dispatches the event to all local Action-based subscribers.
    /// Must be called on the WPF STA thread.
    /// </summary>
    internal void RaiseTemplateApply(TemplateApplyRequestedEvent evt)
    {
        foreach (var (type, handler) in _handlers.ToList())
        {
            if (type == typeof(TemplateApplyRequestedEvent))
                ((Action<TemplateApplyRequestedEvent>)handler)(evt);
        }
    }

    private sealed class ActionDisposable : IDisposable
    {
        private readonly Action _action;
        internal ActionDisposable(Action action) => _action = action;
        public void Dispose() => _action();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Null-object service stubs — used inside the sandbox where IDE services are
// not directly available. IHexEditorService and IPluginEventBus replaced by
// IPC-backed implementations above (Phase 12).
// ─────────────────────────────────────────────────────────────────────────────

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

// NullEventBus removed — replaced by IpcEventBus (Phase 12)


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

// Capability and extension registries are not available in the sandbox process.
// Sandbox plugins cannot query cross-plugin state — these stubs return empty results.

file sealed class NullPluginCapabilityRegistry : IPluginCapabilityRegistry
{
    public static readonly NullPluginCapabilityRegistry Instance = new();
    public IReadOnlyList<string> FindPluginsWithFeature(string feature) => [];
    public bool PluginHasFeature(string pluginId, string feature) => false;
    public IReadOnlyList<string> GetFeaturesForPlugin(string pluginId) => [];
    public IReadOnlyList<string> GetAllRegisteredFeatures() => [];
}

file sealed class NullExtensionRegistry : IExtensionRegistry
{
    public static readonly NullExtensionRegistry Instance = new();
    public IReadOnlyList<T> GetExtensions<T>() where T : class => [];
    public void Register<T>(string pluginId, T implementation) where T : class { }
    public void Register(string pluginId, Type contractType, object implementation) { }
    public void UnregisterAll(string pluginId) { }
    public IReadOnlyList<ExtensionRegistryEntry> GetAllEntries() => [];
}

// Lightweight in-process EventBus for sandbox plugins.
// Plugins subscribe here; the host delivers events via IDEEventNotification IPC messages
// which call Publish<T>() on this instance from the sandbox's receive loop.
// No EventRegistry diagnostics needed inside the sandbox process.

internal sealed class SandboxLocalEventBus : IIDEEventBus
{
    private readonly Dictionary<Type, List<Action<object>>> _handlers = [];
    private readonly object _lock = new();

    public IEventRegistry EventRegistry { get; } = new SandboxNullEventRegistry();

    public void Publish<TEvent>(TEvent evt) where TEvent : class
    {
        List<Action<object>> snapshot;
        lock (_lock)
        {
            if (!_handlers.TryGetValue(typeof(TEvent), out var list)) return;
            snapshot = [.. list];
        }
        foreach (var h in snapshot) h(evt);
    }

    public Task PublishAsync<TEvent>(TEvent evt, CancellationToken ct = default) where TEvent : class
    {
        Publish(evt);
        return Task.CompletedTask;
    }

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class
    {
        Action<object> wrapper = obj => handler((TEvent)obj);
        Add(typeof(TEvent), wrapper);
        return new Token(() => Remove(typeof(TEvent), wrapper));
    }

    public IDisposable Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : class
        => Subscribe<TEvent>(evt => { _ = handler(evt); });

    public IDisposable Subscribe<TEvent>(Action<IDEEventContext, TEvent> handler) where TEvent : class
        => Subscribe<TEvent>(evt => handler(IDEEventContext.HostContext, evt));

    public IDisposable Subscribe<TEvent>(Func<IDEEventContext, TEvent, Task> handler) where TEvent : class
        => Subscribe<TEvent>(evt => { _ = handler(IDEEventContext.HostContext, evt); });

    private void Add(Type t, Action<object> h) { lock (_lock) { if (!_handlers.TryGetValue(t, out var l)) _handlers[t] = l = []; l.Add(h); } }
    private void Remove(Type t, Action<object> h) { lock (_lock) { if (_handlers.TryGetValue(t, out var l)) l.Remove(h); } }

    private sealed class Token(Action remove) : IDisposable { public void Dispose() => remove(); }

    // No-op registry — sandbox doesn't expose diagnostics to the IDE EventBus options page.
    private sealed class SandboxNullEventRegistry : IEventRegistry
    {
        public IReadOnlyList<EventRegistryEntry> GetAllEntries() => [];
        public int GetSubscriberCount(Type eventType) => 0;
        public void Register(Type eventType, string displayName, string producerLabel) { }
        public void UpdateSubscriberCount(Type eventType, int delta) { }
    }
}

file static class Disposable
{
    public static readonly IDisposable Empty = new EmptyDisposable();

    private sealed class EmptyDisposable : IDisposable { public void Dispose() { } }
}
