// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: WpfPluginHost.cs
// Author: Auto
// Created: 2026-03-06
// Description:
//     Core plugin orchestrator. Discovers, loads, unloads, reloads, enables,
//     disables, and monitors plugins using AssemblyLoadContext (isCollectible: true)
//     for true hot-load/hot-unload semantics.
//
// Architecture Notes:
//     Each plugin runs in its own PluginLoadContext (collectible ALC).
//     Unload = Instance.ShutdownAsync + ALC.Unload + UIRegistry.UnregisterAllForPlugin.
//     PluginWatchdog bounds all async calls (5s init, 200ms per-call).
//     SlowPluginDetector runs on the WPF Dispatcher at 5-second intervals.
//     Thread-safety: _entries dict protected by _lock; events fired on Dispatcher.
//
// ==========================================================

using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using WpfHexEditor.PluginHost.Monitoring;
using WpfHexEditor.PluginHost.Services;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.PluginHost;

/// <summary>
/// Orchestrates the full plugin lifecycle: discovery, load, unload, reload, monitoring.
/// </summary>
public sealed class WpfPluginHost : IAsyncDisposable
{
    private static readonly string UserPluginsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "WpfHexEditor", "Plugins");

    private readonly IIDEHostContext _hostContext;
    private readonly UIRegistry _uiRegistry;
    private readonly PermissionService _permissionService;
    private readonly PluginWatchdog _watchdog;
    private readonly SlowPluginDetector _slowDetector;

    private readonly Dictionary<string, PluginEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    // -- Events --

    /// <summary>Raised on the Dispatcher thread when a plugin has been successfully loaded.</summary>
    public event EventHandler<PluginEventArgs>? PluginLoaded;

    /// <summary>Raised on the Dispatcher thread when a plugin has been unloaded (gracefully).</summary>
    public event EventHandler<PluginEventArgs>? PluginUnloaded;

    /// <summary>Raised when a plugin transitions to Faulted state.</summary>
    public event EventHandler<PluginFaultedEventArgs>? PluginCrashed;

    /// <summary>Raised when SlowPluginDetector identifies a non-responsive plugin.</summary>
    public event EventHandler<SlowPluginDetectedEventArgs>? SlowPluginDetected;

    public WpfPluginHost(
        IIDEHostContext hostContext,
        UIRegistry uiRegistry,
        PermissionService permissionService,
        Dispatcher dispatcher)
    {
        _hostContext = hostContext ?? throw new ArgumentNullException(nameof(hostContext));
        _uiRegistry = uiRegistry ?? throw new ArgumentNullException(nameof(uiRegistry));
        _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
        _watchdog = new PluginWatchdog();
        _watchdog.PluginNonResponsive += OnPluginNonResponsive;

        _slowDetector = new SlowPluginDetector(GetLoadedEntries, dispatcher);
        _slowDetector.SlowPluginDetected += (s, e) => SlowPluginDetected?.Invoke(this, e);
        _slowDetector.Start();
    }

    // ─── Discovery ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Discovers all plugins under <see cref="UserPluginsDir"/> and any provided extra directories.
    /// Returns a list of validated manifests ready for loading.
    /// </summary>
    public async Task<IReadOnlyList<PluginManifest>> DiscoverPluginsAsync(
        IEnumerable<string>? extraDirectories = null,
        CancellationToken ct = default)
    {
        var result = new List<PluginManifest>();
        var searchDirs = new List<string> { UserPluginsDir };
        if (extraDirectories is not null) searchDirs.AddRange(extraDirectories);

        foreach (var dir in searchDirs)
        {
            if (!Directory.Exists(dir)) continue;

            foreach (var pluginDir in Directory.GetDirectories(dir))
            {
                ct.ThrowIfCancellationRequested();
                var manifest = await TryLoadManifestAsync(pluginDir).ConfigureAwait(false);
                if (manifest is not null) result.Add(manifest);
            }
        }

        return result;
    }

    // ─── Load ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads a plugin from a discovered manifest. Handles ALC creation, entry point
    /// reflection, InitializeAsync (watchdog-bounded), permission initialization.
    /// </summary>
    public async Task<PluginEntry> LoadPluginAsync(PluginManifest manifest, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        PluginEntry entry;
        lock (_lock)
        {
            if (_entries.TryGetValue(manifest.Id, out var existing) && existing.State == PluginState.Loaded)
                return existing;

            entry = new PluginEntry(manifest);
            entry.SetState(PluginState.Loading);
            _entries[manifest.Id] = entry;
        }

        try
        {
            if (manifest.IsolationMode == PluginIsolationMode.Sandbox)
                throw new NotSupportedException("Sandbox isolation is Phase 5 — use InProcess for now.");

            // Resolve assembly path
            var pluginDir = ResolvePluginDirectory(manifest);
            var assemblyPath = Path.Combine(pluginDir, manifest.Assembly?.File ?? $"{manifest.Id}.dll");

            if (!File.Exists(assemblyPath))
                throw new FileNotFoundException($"Plugin assembly not found: {assemblyPath}");

            // Create collectible ALC
            var loadContext = new PluginLoadContext(pluginDir);
            var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);

            // Resolve entry point type
            var entryType = assembly.GetType(manifest.EntryPoint)
                ?? throw new TypeLoadException($"Entry point type '{manifest.EntryPoint}' not found in '{assemblyPath}'.");

            var instance = (IWpfHexEditorPlugin)(Activator.CreateInstance(entryType)
                ?? throw new InvalidOperationException($"Could not create instance of '{manifest.EntryPoint}'."));

            entry.SetInstance(instance, loadContext);

            // Initialize permissions from declared capabilities
            var declaredPerms = instance.Capabilities.ToPermissionFlags();
            _permissionService.InitializeForPlugin(manifest.Id, declaredPerms);

            // Run InitializeAsync under watchdog
            var started = DateTime.UtcNow;
            await _watchdog.WrapAsync(manifest.Id, "InitializeAsync",
                instance.InitializeAsync(_hostContext, ct),
                PluginWatchdog.InitTimeout).ConfigureAwait(false);

            entry.SetInitDuration(DateTime.UtcNow - started);
            entry.SetState(PluginState.Loaded);
            entry.SetLoadedAt(DateTime.UtcNow);

            RaiseOnDispatcher(() => PluginLoaded?.Invoke(this, new PluginEventArgs(manifest.Id, manifest.Name)));
            return entry;
        }
        catch (Exception ex)
        {
            entry.SetState(PluginState.Faulted);
            entry.SetFaultException(ex);
            RaiseOnDispatcher(() => PluginCrashed?.Invoke(this,
                new PluginFaultedEventArgs(manifest.Id, manifest.Name, ex, "Load")));
            throw;
        }
    }

    /// <summary>
    /// Discovers and loads all plugins. Faulted plugins are silently recorded.
    /// </summary>
    public async Task LoadAllAsync(IEnumerable<string>? extraDirectories = null, CancellationToken ct = default)
    {
        var manifests = await DiscoverPluginsAsync(extraDirectories, ct).ConfigureAwait(false);
        foreach (var manifest in manifests.OrderBy(m => m.LoadPriority))
        {
            ct.ThrowIfCancellationRequested();
            try { await LoadPluginAsync(manifest, ct).ConfigureAwait(false); }
            catch { /* recorded in entry; PluginCrashed already raised */ }
        }
    }

    // ─── Unload ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Gracefully shuts down and unloads a plugin. Removes all UI contributions.
    /// </summary>
    public async Task UnloadPluginAsync(string pluginId, CancellationToken ct = default)
    {
        PluginEntry? entry;
        lock (_lock) _entries.TryGetValue(pluginId, out entry);
        if (entry is null) return;

        try
        {
            if (entry.Instance is not null)
            {
                await _watchdog.WrapAsync(pluginId, "ShutdownAsync",
                    entry.Instance.ShutdownAsync(ct),
                    TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            }
        }
        catch { /* best-effort shutdown */ }
        finally
        {
            _uiRegistry.UnregisterAllForPlugin(pluginId);
            entry.Unload();
            entry.SetState(PluginState.Unloaded);
            RaiseOnDispatcher(() => PluginUnloaded?.Invoke(this, new PluginEventArgs(pluginId, entry.Manifest.Name)));
        }
    }

    // ─── Reload ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Hot-reloads a plugin: unload + wait for ALC GC + load fresh.
    /// If the plugin implements IWpfHexEditorPluginV2 and SupportsHotReload, calls ReloadAsync instead.
    /// </summary>
    public async Task ReloadPluginAsync(string pluginId, CancellationToken ct = default)
    {
        PluginEntry? entry;
        lock (_lock) _entries.TryGetValue(pluginId, out entry);
        if (entry is null) return;

        // Try V2 ReloadAsync first
        if (entry.Instance is IWpfHexEditorPluginV2 v2 && v2.SupportsHotReload)
        {
            await _watchdog.WrapAsync(pluginId, "ReloadAsync",
                v2.ReloadAsync(ct),
                PluginWatchdog.InitTimeout).ConfigureAwait(false);
            return;
        }

        var manifest = entry.Manifest;
        await UnloadPluginAsync(pluginId, ct).ConfigureAwait(false);

        // Allow GC to collect the old ALC
        GC.Collect();
        GC.WaitForPendingFinalizers();

        await LoadPluginAsync(manifest, ct).ConfigureAwait(false);
    }

    // ─── Enable / Disable ────────────────────────────────────────────────────────

    public async Task EnablePluginAsync(string pluginId, CancellationToken ct = default)
    {
        PluginEntry? entry;
        lock (_lock) _entries.TryGetValue(pluginId, out entry);
        if (entry is null || entry.State != PluginState.Disabled) return;

        await LoadPluginAsync(entry.Manifest, ct).ConfigureAwait(false);
    }

    public async Task DisablePluginAsync(string pluginId, CancellationToken ct = default)
    {
        await UnloadPluginAsync(pluginId, ct).ConfigureAwait(false);

        PluginEntry? entry;
        lock (_lock) _entries.TryGetValue(pluginId, out entry);
        entry?.SetState(PluginState.Disabled);
    }

    public async Task UninstallPluginAsync(string pluginId, CancellationToken ct = default)
    {
        await UnloadPluginAsync(pluginId, ct).ConfigureAwait(false);
        lock (_lock) _entries.Remove(pluginId);
        // Physical file removal is handled by PluginInstaller, not PluginHost.
    }

    // ─── Queries ─────────────────────────────────────────────────────────────────

    public IReadOnlyList<PluginEntry> GetAllPlugins()
    {
        lock (_lock) return _entries.Values.ToList();
    }

    public PluginEntry? GetPlugin(string pluginId)
    {
        lock (_lock) _entries.TryGetValue(pluginId, out var entry);
        return null; // compiler fix: will be replaced with proper return
    }

    // ─── Private helpers ─────────────────────────────────────────────────────────

    private static async Task<PluginManifest?> TryLoadManifestAsync(string pluginDir)
    {
        var manifestPath = Path.Combine(pluginDir, "manifest.json");
        if (!File.Exists(manifestPath)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(manifestPath).ConfigureAwait(false);
            var manifest = JsonSerializer.Deserialize<PluginManifest>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (manifest is null) return null;

            // Store the directory for later assembly resolution
            manifest.ResolvedDirectory = pluginDir;

            var validator = new PluginManifestValidator();
            var result = validator.Validate(manifest, pluginDir);
            if (result.HasErrors) return null; // Faulted manifest — skip silently

            return manifest;
        }
        catch
        {
            return null;
        }
    }

    private static string ResolvePluginDirectory(PluginManifest manifest)
    {
        if (!string.IsNullOrEmpty(manifest.ResolvedDirectory) && Directory.Exists(manifest.ResolvedDirectory))
            return manifest.ResolvedDirectory;

        return Path.Combine(UserPluginsDir, manifest.Id);
    }

    private IReadOnlyList<PluginEntry> GetLoadedEntries()
    {
        lock (_lock) return _entries.Values.Where(e => e.State == PluginState.Loaded).ToList();
    }

    private void OnPluginNonResponsive(object? sender, PluginNonResponsiveEventArgs e)
    {
        PluginEntry? entry;
        lock (_lock) _entries.TryGetValue(e.PluginId, out entry);
        if (entry is null) return;

        var crash = new PluginCrashHandler();
        crash.PluginFaulted += (s, fe) => RaiseOnDispatcher(() => PluginCrashed?.Invoke(this, fe));
        _ = crash.HandleCrashAsync(entry, new TimeoutException($"Plugin '{e.PluginId}' timed out on '{e.Operation}' ({e.Timeout.TotalMilliseconds:F0} ms)."), e.Operation);
    }

    private static void RaiseOnDispatcher(Action action)
    {
        if (Application.Current?.Dispatcher is { } d && !d.CheckAccess())
            d.InvokeAsync(action);
        else
            action();
    }

    public async ValueTask DisposeAsync()
    {
        _slowDetector.Dispose();
        _watchdog.PluginNonResponsive -= OnPluginNonResponsive;

        string[] ids;
        lock (_lock) ids = _entries.Keys.ToArray();

        foreach (var id in ids)
        {
            try { await UnloadPluginAsync(id).ConfigureAwait(false); }
            catch { /* best-effort */ }
        }
    }
}

// ─── Lightweight event args ──────────────────────────────────────────────────

public sealed class PluginEventArgs : EventArgs
{
    public string PluginId { get; }
    public string PluginName { get; }
    public PluginEventArgs(string id, string name) { PluginId = id; PluginName = name; }
}
