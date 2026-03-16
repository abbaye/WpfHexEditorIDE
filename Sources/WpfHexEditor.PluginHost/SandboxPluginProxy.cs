// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: SandboxPluginProxy.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-15
// Description:
//     Full implementation of the out-of-process sandbox proxy.
//     Implements IWpfHexEditorPlugin so WpfPluginHost can treat a sandboxed
//     plugin identically to an in-process plugin from the outside.
//
// Architecture Notes:
//     - Pattern: Proxy (GoF) — marshals every plugin lifecycle call over Named Pipe IPC.
//     - SandboxProcessManager owns process lifecycle and the IpcChannel.
//     - MetricsPush events from the sandbox are forwarded to PluginMetricsEngine
//       so the Plugin Monitor shows real per-process CPU/RAM instead of estimates.
//     - CrashNotification transitions the proxy to Faulted and raises PluginCrashed.
//     - Theme: N/A (this is infrastructure, no WPF controls created here).
// ==========================================================

using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using WpfHexEditor.PluginHost.Monitoring;
using WpfHexEditor.PluginHost.Sandbox;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Services;
using WpfHexEditor.SDK.Events;
using WpfHexEditor.SDK.Models;
using WpfHexEditor.SDK.Sandbox;

namespace WpfHexEditor.PluginHost;

/// <summary>
/// In-process proxy for a plugin running inside WpfHexEditor.PluginSandbox.exe.
/// Translates all IWpfHexEditorPlugin calls into IPC messages and forwards
/// real-time metrics/crash events back to the IDE.
/// </summary>
internal sealed class SandboxPluginProxy : IWpfHexEditorPlugin, IAsyncDisposable
{
    private readonly PluginManifest _manifest;
    private readonly SandboxProcessManager _procManager;
    private readonly Action<string> _log;

    // Injected after construction so the proxy can push real metrics into the engine.
    private PluginMetricsEngine? _metricsEngine;

    // Phase 9 — UI bridge proxy (created in InitializeAsync once registry + dispatcher are available)
    private SandboxUIRegistryProxy? _uiProxy;

    // Phase 12 — IDE event forwarding (subscribed after sandbox init, unsubscribed on dispose)
    private IHexEditorService?    _ideHexEditor;
    private IParsedFieldService?  _ideParsedField;
    private IDisposable?          _ideTemplateApplySub;

    // ── State ─────────────────────────────────────────────────────────────────
    private volatile bool _isReady;
    private readonly TaskCompletionSource<bool> _readyTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    // ── Events (raised to WpfPluginHost) ─────────────────────────────────────
    public event EventHandler<CrashNotificationPayload>? CrashReceived;
    public event EventHandler<MetricsPushPayload>? MetricsPushed;

    // ── IWpfHexEditorPlugin ───────────────────────────────────────────────────
    public string Id => _manifest.Id;
    public string Name => _manifest.Name;
    public Version Version => Version.TryParse(_manifest.Version, out var v) ? v : new Version(0, 0);
    public PluginCapabilities Capabilities => _manifest.Permissions ?? new PluginCapabilities();

    // ─────────────────────────────────────────────────────────────────────────
    public SandboxPluginProxy(
        PluginManifest manifest,
        Action<string>? logger = null)
    {
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        _log = logger ?? (_ => { });

        _procManager = new SandboxProcessManager(manifest.Id, _log);
        _procManager.PluginReady += OnPluginReady;
        _procManager.MetricsPushed += OnMetricsPushed;
        _procManager.CrashReceived += OnCrashReceived;
    }

    /// <summary>Wires a <see cref="PluginMetricsEngine"/> so sandbox metrics are forwarded.</summary>
    public void SetMetricsEngine(PluginMetricsEngine engine) => _metricsEngine = engine;

    /// <summary>
    /// Notifies the sandbox of a theme change so it can re-apply theme resources.
    /// Call this from the IDE's theme-change handler.
    /// </summary>
    public Task ForwardThemeChangeAsync(string themeXaml, CancellationToken ct = default)
        => _uiProxy?.ForwardThemeChangeAsync(themeXaml, ct) ?? Task.CompletedTask;

    /// <summary>
    /// Returns the options page registration declared by the sandbox plugin, or null if the
    /// plugin does not implement IPluginWithOptions.
    /// Only valid after <see cref="InitializeAsync"/> has completed.
    /// </summary>
    public (string PluginId, string PluginName, long Hwnd)? GetOptionsPageInfo()
    {
        var info = _uiProxy?.OptionsPageInfo;
        if (info is null) return null;
        return (info.PluginId, info.PluginName, info.Hwnd);
    }

    // ── IWpfHexEditorPlugin.InitializeAsync ───────────────────────────────────

    public async Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        var pluginDir = _manifest.ResolvedDirectory
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WpfHexEditor", "Plugins", _manifest.Id);

        var assemblyPath = Path.Combine(
            pluginDir,
            _manifest.Assembly?.File ?? $"{_manifest.Id}.dll");

        // 1. Spawn sandbox process and wait for pipe connection
        await _procManager.StartAsync(ct).ConfigureAwait(false);

        // 2. Create the UI bridge proxy so panel registrations are handled
        //    before the plugin's InitializeAsync fires.
        //    IMPORTANT: Always use the WPF UI dispatcher (Application.Current.Dispatcher),
        //    NOT Dispatcher.CurrentDispatcher — this method is called from the thread pool
        //    (LoadPluginAsync does not marshal sandbox init to the Dispatcher), so
        //    Dispatcher.CurrentDispatcher would create a new, never-pumped Dispatcher
        //    for the thread-pool thread, causing all InvokeAsync UI callbacks (menu/panel
        //    registration) to post work that is never executed.
        var dispatcher = Application.Current?.Dispatcher
            ?? throw new InvalidOperationException(
                "WPF Application has not been initialized. Cannot create sandbox UI proxy.");
        _uiProxy = new SandboxUIRegistryProxy(
            context.UIRegistry, _procManager, _manifest.Id, dispatcher, _log);

        // 3. Send InitializeRequest with granted permissions + serialized theme
        var themeResources = context.Theme.GetThemeResources();
        var themeXaml    = ThemeResourceSerializer.Serialize(themeResources);
        var themeUris    = ThemeResourceSerializer.CollectSourceUris(themeResources);
        var initPayload = new InitializeRequestPayload
        {
            PluginId = _manifest.Id,
            PluginName = _manifest.Name,
            AssemblyPath = assemblyPath,
            EntryType = _manifest.EntryPoint,
            GrantedPermissions = BuildGrantedPermissions(),
            ThemeResourcesXaml = themeXaml,
            ThemeDictionaryUris = new List<string>(themeUris),
        };

        var request = SandboxProcessManager.BuildRequest(
            SandboxMessageKind.InitializeRequest, initPayload);

        var response = await _procManager.SendRequestAsync(request, ct: ct).ConfigureAwait(false);
        var result = Deserialize<SandboxResponsePayload>(response.Payload);

        if (result is null || !result.Success)
            throw new InvalidOperationException(
                $"Sandbox plugin '{_manifest.Id}' failed to initialize: {result?.ErrorMessage ?? "no response"}");

        // 4. Wait for ReadyNotification (sandbox pushes this after successful init)
        await _readyTcs.Task.WaitAsync(TimeSpan.FromSeconds(10), ct).ConfigureAwait(false);
        _log($"[SandboxProxy:{_manifest.Id}] Ready.");

        // 5. Phase 12 — subscribe to IDE HexEditor events and forward them to the sandbox.
        //    Subscriptions are established AFTER ReadyNotification so the plugin's
        //    InitializeAsync has already run and its event handlers are wired up.
        _ideHexEditor   = context.HexEditor;
        _ideParsedField = context.ParsedField;

        _ideHexEditor.SelectionChanged    += OnIdeSelectionChanged;
        _ideHexEditor.FileOpened          += OnIdeFileOpened;
        _ideHexEditor.ActiveEditorChanged += OnIdeActiveEditorChanged;
        _ideParsedField.ParsedFieldsChanged += OnIdeParsedFieldsChanged;
        _ideTemplateApplySub = context.EventBus.Subscribe<TemplateApplyRequestedEvent>(OnIdeTemplateApplyRequested);

        // Push initial state immediately if a file is already open.
        if (_ideHexEditor.IsActive)
        {
            _ = Task.Run(async () =>
            {
                await PushHexEditorStateAsync("ActiveEditorChanged").ConfigureAwait(false);
                if (_ideParsedField.HasParsedFields)
                    await PushParsedFieldsSnapshotAsync().ConfigureAwait(false);
            });
        }
    }

    // ── IWpfHexEditorPlugin.ShutdownAsync ────────────────────────────────────

    public async Task ShutdownAsync(CancellationToken ct = default)
    {
        try
        {
            await _procManager.StopAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log($"[SandboxProxy:{_manifest.Id}] Shutdown error: {ex.Message}");
        }
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnPluginReady(object? sender, EventArgs e)
    {
        _isReady = true;
        _readyTcs.TrySetResult(true);
    }

    private void OnMetricsPushed(object? sender, MetricsPushPayload e)
    {
        MetricsPushed?.Invoke(this, e);

        // Forward real metrics into the engine (replaces estimated values)
        if (_metricsEngine is not null)
        {
            var execTime = TimeSpan.FromMilliseconds(e.AvgExecMs);
            _ = _metricsEngine.EnqueueActiveSampleAsync(_manifest.Id, execTime);
        }
    }

    private void OnCrashReceived(object? sender, CrashNotificationPayload e)
    {
        _log($"[SandboxProxy:{_manifest.Id}] CRASH: {e.ExceptionType} — {e.Message}");
        CrashReceived?.Invoke(this, e);
    }

    // ── Phase 12 — IDE event handlers (forward IDE events to sandbox) ─────────

    private void OnIdeSelectionChanged(object? sender, EventArgs e)
        => _ = PushHexEditorStateAsync("SelectionChanged");

    private void OnIdeFileOpened(object? sender, EventArgs e)
        => _ = Task.Run(async () =>
        {
            await PushHexEditorStateAsync("FileOpened").ConfigureAwait(false);
            await PushParsedFieldsSnapshotAsync().ConfigureAwait(false);
        });

    private void OnIdeActiveEditorChanged(object? sender, EventArgs e)
        => _ = Task.Run(async () =>
        {
            await PushHexEditorStateAsync("ActiveEditorChanged").ConfigureAwait(false);
            if (_ideHexEditor?.IsActive == true && _ideParsedField?.HasParsedFields == true)
                await PushParsedFieldsSnapshotAsync().ConfigureAwait(false);
        });

    private void OnIdeParsedFieldsChanged(object? sender, EventArgs e)
        => _ = PushParsedFieldsSnapshotAsync();

    private void OnIdeTemplateApplyRequested(TemplateApplyRequestedEvent evt)
        => _ = PushTemplateApplyBroadcastAsync(evt);

    // ── Phase 12 — IPC push helpers ───────────────────────────────────────────

    private Task PushHexEditorStateAsync(string eventKind)
    {
        var svc = _ideHexEditor;
        if (svc is null) return Task.CompletedTask;

        var payload = new HexEditorStateNotificationPayload
        {
            EventKind       = eventKind,
            IsActive        = svc.IsActive,
            CurrentFilePath = svc.CurrentFilePath,
            FileSize        = svc.FileSize,
            SelectionStart  = svc.SelectionStart,
            SelectionStop   = svc.SelectionStop,
            CurrentOffset   = svc.CurrentOffset,
        };

        var envelope = SandboxProcessManager.BuildRequest(
            SandboxMessageKind.HexEditorStateNotification, payload);
        return _procManager.SendAsync(envelope);
    }

    private Task PushParsedFieldsSnapshotAsync()
    {
        var svc = _ideParsedField;
        if (svc is null) return Task.CompletedTask;

        var fields = svc.GetParsedFields()
            .Select(f => new SandboxParsedFieldEntryDto
            {
                Name         = f.Name,
                DataType     = f.DataType,
                Offset       = f.Offset,
                Length       = f.Length,
                ValueDisplay = f.ValueDisplay,
            })
            .ToList();

        var payload = new ParsedFieldsSnapshotNotificationPayload
        {
            FileSize = _ideHexEditor?.FileSize ?? 0,
            Fields   = fields,
        };

        var envelope = SandboxProcessManager.BuildRequest(
            SandboxMessageKind.ParsedFieldsSnapshotNotification, payload);
        return _procManager.SendAsync(envelope);
    }

    private Task PushTemplateApplyBroadcastAsync(TemplateApplyRequestedEvent evt)
    {
        var payload = new TemplateApplyBroadcastNotificationPayload
        {
            TemplateName = evt.TemplateName,
            Blocks       = evt.Blocks.Select(b => new SandboxTemplateBlockDto
            {
                Name         = b.Name,
                Offset       = b.Offset,
                Length       = b.Length,
                TypeHint     = b.TypeHint,
                DisplayValue = b.DisplayValue,
            }).ToList(),
        };

        var envelope = SandboxProcessManager.BuildRequest(
            SandboxMessageKind.TemplateApplyBroadcastNotification, payload);
        return _procManager.SendAsync(envelope);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private List<string> BuildGrantedPermissions()
    {
        var caps = _manifest.Permissions ?? new PluginCapabilities();
        var list = new List<string>();
        if (caps.AccessFileSystem) list.Add("AccessFileSystem");
        if (caps.AccessNetwork) list.Add("AccessNetwork");
        if (caps.AccessHexEditor) list.Add("AccessHexEditor");
        if (caps.AccessCodeEditor) list.Add("AccessCodeEditor");
        if (caps.RegisterMenus) list.Add("RegisterMenus");
        if (caps.WriteOutput) list.Add("WriteOutput");
        if (caps.WriteErrorPanel) list.Add("WriteErrorPanel");
        if (caps.AccessSettings) list.Add("AccessSettings");
        if (caps.WriteTerminal) list.Add("WriteTerminal");
        return list;
    }

    private static T? Deserialize<T>(string json)
    {
        if (string.IsNullOrEmpty(json)) return default;
        try { return JsonSerializer.Deserialize<T>(json); }
        catch { return default; }
    }

    public async ValueTask DisposeAsync()
    {
        _procManager.PluginReady -= OnPluginReady;
        _procManager.MetricsPushed -= OnMetricsPushed;
        _procManager.CrashReceived -= OnCrashReceived;

        // Phase 12 — unsubscribe IDE event forwarding
        if (_ideHexEditor is not null)
        {
            _ideHexEditor.SelectionChanged    -= OnIdeSelectionChanged;
            _ideHexEditor.FileOpened          -= OnIdeFileOpened;
            _ideHexEditor.ActiveEditorChanged -= OnIdeActiveEditorChanged;
        }
        if (_ideParsedField is not null)
            _ideParsedField.ParsedFieldsChanged -= OnIdeParsedFieldsChanged;
        _ideTemplateApplySub?.Dispose();

        _uiProxy?.Dispose();
        _uiProxy = null;

        await _procManager.DisposeAsync().ConfigureAwait(false);
    }
}
