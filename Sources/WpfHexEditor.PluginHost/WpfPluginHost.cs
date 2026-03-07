//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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

    // Continuous diagnostics sampling (default: every 5 seconds).
    private readonly DispatcherTimer _samplingTimer;
    private TimeSpan _lastCpuTime = TimeSpan.Zero;
    private DateTime _lastCpuCheck = DateTime.UtcNow;

    /// <summary>Interval between diagnostics samples. Default: 5 seconds.</summary>
    public TimeSpan DiagnosticSamplingInterval
    {
        get => _samplingTimer.Interval;
        set => _samplingTimer.Interval = value;
    }

    /// <summary>Registry of per-plugin options pages (populated automatically on load).</summary>
    public PluginOptionsRegistry OptionsRegistry { get; } = new();

    /// <summary>Exposes the runtime permission service so the Plugin Manager UI can show permission toggles.</summary>
    public PermissionService Permissions => _permissionService;

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

        // Sample CPU/memory for all loaded plugins on a background timer tick.
        _samplingTimer = new System.Windows.Threading.DispatcherTimer(
            System.Windows.Threading.DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _samplingTimer.Tick += OnSamplingTick;
        _samplingTimer.Start();
    }

    // --- Discovery --------------------------------------------------------------

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

    // --- Load --------------------------------------------------------------------

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
                throw new NotSupportedException("Sandbox isolation is Phase 5 - use InProcess for now.");

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

            // Run InitializeAsync under watchdog; measure execution time for diagnostics.
            var cpuBefore = System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime;
            var elapsed   = await _watchdog.WrapAsync(manifest.Id, "InitializeAsync",
                instance.InitializeAsync(_hostContext, ct),
                _watchdog.InitTimeout).ConfigureAwait(false);

            var cpuAfter  = System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime;
            var cpuDelta  = cpuAfter - cpuBefore;
            var cpuPct    = elapsed.TotalMilliseconds > 0
                ? cpuDelta.TotalMilliseconds / (elapsed.TotalMilliseconds * Environment.ProcessorCount) * 100.0
                : 0.0;
            var memBytes  = GC.GetTotalMemory(forceFullCollection: false);
            entry.Diagnostics.Record(cpuPct, memBytes, elapsed);
            entry.SetInitDuration(elapsed);
            entry.SetState(PluginState.Loaded);

            // Auto-register options page if the plugin supports it.
            if (instance is IPluginWithOptions optionsPlugin)
                OptionsRegistry.RegisterPluginPage(manifest.Id, manifest.Name, optionsPlugin);
            entry.SetLoadedAt(DateTime.UtcNow);

            RaiseOnDispatcher(() => PluginLoaded?.Invoke(this, new PluginEventArgs(manifest.Id, manifest.Name)));
            return entry;
        }
        catch (Exception ex)
        {
            entry.SetState(PluginState.Faulted);
            entry.SetFaultException(ex);
            RaiseOnDispatcher(() => PluginCrashed?.Invoke(this,
                new PluginFaultedEventArgs { PluginId = manifest.Id, PluginName = manifest.Name, Exception = ex, Phase = "Load" }));
            throw;
        }
    }

    /// <summary>
    /// Discovers and loads all plugins. Faulted plugins are silently recorded.
    /// </summary>
    public async Task LoadAllAsync(IEnumerable<string>? extraDirectories = null, CancellationToken ct = default)
    {
        var manifests = await DiscoverPluginsAsync(extraDirectories, ct).ConfigureAwait(false);
        var sorted    = TopologicalSort(manifests);
        foreach (var manifest in sorted)
        {
            ct.ThrowIfCancellationRequested();
            try { await LoadPluginAsync(manifest, ct).ConfigureAwait(false); }
            catch { /* recorded in entry; PluginCrashed already raised */ }
        }
    }

    /// <summary>
    /// Sorts manifests so that dependencies are loaded before dependents.
    /// Within the same dependency level, lower LoadPriority values load first.
    /// Cycles and unknown dependencies are silently skipped.
    /// </summary>
    private static IEnumerable<PluginManifest> TopologicalSort(IReadOnlyList<PluginManifest> manifests)
    {
        var byId   = manifests.ToDictionary(m => m.Id);
        var result = new List<PluginManifest>(manifests.Count);
        var visited = new HashSet<string>();

        void Visit(PluginManifest m)
        {
            if (!visited.Add(m.Id)) return;
            foreach (var depId in m.Dependencies)
            {
                if (byId.TryGetValue(depId, out var dep))
                    Visit(dep);
            }
            result.Add(m);
        }

        foreach (var m in manifests.OrderBy(x => x.LoadPriority))
            Visit(m);

        return result;
    }

    // --- Unload ------------------------------------------------------------------

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
                var elapsed = await _watchdog.WrapAsync(pluginId, "ShutdownAsync",
                    entry.Instance.ShutdownAsync(ct),
                    TimeSpan.FromSeconds(3)).ConfigureAwait(false);
                entry.Diagnostics.Record(0.0, GC.GetTotalMemory(false), elapsed);
            }
        }
        catch { /* best-effort shutdown */ }
        finally
        {
            _uiRegistry.UnregisterAllForPlugin(pluginId);
            OptionsRegistry.UnregisterPluginPage(pluginId);
            entry.Unload();
            entry.SetState(PluginState.Unloaded);
            RaiseOnDispatcher(() => PluginUnloaded?.Invoke(this, new PluginEventArgs(pluginId, entry.Manifest.Name)));
        }
    }

    // --- Reload ------------------------------------------------------------------

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
            var reloadElapsed = await _watchdog.WrapAsync(pluginId, "ReloadAsync",
                v2.ReloadAsync(ct),
                _watchdog.InitTimeout).ConfigureAwait(false);
            entry.Diagnostics.Record(0.0, GC.GetTotalMemory(false), reloadElapsed);
            return;
        }

        var manifest = entry.Manifest;
        await UnloadPluginAsync(pluginId, ct).ConfigureAwait(false);

        // Allow GC to collect the old ALC
        GC.Collect();
        GC.WaitForPendingFinalizers();

        await LoadPluginAsync(manifest, ct).ConfigureAwait(false);
    }

    // --- Enable / Disable --------------------------------------------------------

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

    // --- Install from package ----------------------------------------------------

    /// <summary>
    /// Extracts a .whxplugin package (ZIP) into the user plugins directory,
    /// validates the manifest, then immediately loads the plugin.
    /// </summary>
    public async Task<PluginEntry> InstallFromFileAsync(string packagePath, CancellationToken ct = default)
    {
        if (!File.Exists(packagePath))
            throw new FileNotFoundException("Plugin package not found.", packagePath);

        // Read the manifest from the ZIP to get the plugin ID for the target directory.
        string pluginId;
        using (var archive = ZipFile.OpenRead(packagePath))
        {
            var manifestEntry = archive.GetEntry("manifest.json")
                ?? throw new InvalidOperationException("Invalid plugin package: manifest.json not found.");

            using var stream = manifestEntry.Open();
            var manifest = await JsonSerializer.DeserializeAsync<PluginManifest>(stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct).ConfigureAwait(false)
                ?? throw new InvalidOperationException("manifest.json could not be deserialized.");

            pluginId = manifest.Id;
        }

        // Extract into UserPlugins/<pluginId>/
        var targetDir = Path.Combine(UserPluginsDir, pluginId);
        if (Directory.Exists(targetDir))
            Directory.Delete(targetDir, recursive: true);

        ZipFile.ExtractToDirectory(packagePath, targetDir);

        // Discover + load the freshly installed plugin.
        var manifest2 = await TryLoadManifestAsync(targetDir).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Manifest validation failed after extraction to '{targetDir}'.");

        return await LoadPluginAsync(manifest2, ct).ConfigureAwait(false);
    }

    // --- Queries -----------------------------------------------------------------

    public IReadOnlyList<PluginEntry> GetAllPlugins()
    {
        lock (_lock) return _entries.Values.ToList();
    }

    public PluginEntry? GetPlugin(string pluginId)
    {
        lock (_lock) return _entries.TryGetValue(pluginId, out var entry) ? entry : null;
    }

    // --- Private helpers ---------------------------------------------------------

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

            var validator = new PluginManifestValidator(new Version(1, 0), new Version(1, 0));
            var result = validator.Validate(manifest, pluginDir);
            if (!result.IsValid) return null; // Faulted manifest — skip silently

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

    /// <summary>
    /// Called every DiagnosticSamplingInterval. Records a process-level CPU % and GC memory
    /// snapshot into each loaded plugin's diagnostics ring buffer.
    /// CPU is computed as delta TotalProcessorTime / elapsed wall time / processor count.
    /// </summary>
    private void OnSamplingTick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        var wallElapsed = now - _lastCpuCheck;
        if (wallElapsed.TotalMilliseconds < 1) return;

        var process = Process.GetCurrentProcess();
        var cpuNow = process.TotalProcessorTime;
        var cpuDelta = cpuNow - _lastCpuTime;

        double cpuPct = cpuDelta.TotalMilliseconds
            / (wallElapsed.TotalMilliseconds * Environment.ProcessorCount) * 100.0;
        cpuPct = Math.Clamp(cpuPct, 0.0, 100.0);

        _lastCpuTime = cpuNow;
        _lastCpuCheck = now;

        long memBytes = process.WorkingSet64;

        IReadOnlyList<PluginEntry> loaded;
        lock (_lock) loaded = _entries.Values.Where(e2 => e2.State == PluginState.Loaded).ToList();

        // Distribute a single process-level sample to every loaded plugin.
        // Per-plugin isolation is not possible in InProcess mode; the process metrics
        // serve as an upper-bound indicator for the monitoring UI.
        foreach (var entry in loaded)
            entry.Diagnostics.Record(cpuPct, memBytes, wallElapsed);
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
        _samplingTimer.Stop();
        _samplingTimer.Tick -= OnSamplingTick;
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

// --- Lightweight event args --------------------------------------------------

public sealed class PluginEventArgs : EventArgs
{
    public string PluginId { get; }
    public string PluginName { get; }
    public PluginEventArgs(string id, string name) { PluginId = id; PluginName = name; }
}
