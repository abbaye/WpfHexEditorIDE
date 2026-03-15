//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using WpfHexEditor.PluginHost.Monitoring;
using WpfHexEditor.PluginHost.Sandbox;
using WpfHexEditor.PluginHost.Services;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Models;
using WpfHexEditor.SDK.Sandbox;

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
    private readonly Action<string> _log;
    private readonly Action<string> _logError;
    private readonly Dispatcher _dispatcher;

    private readonly Dictionary<string, PluginEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    // --- Isolation mode overrides (user preference, persisted) ------------------
    private readonly Dictionary<string, PluginIsolationMode> _isolationOverrides =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly string OverridesFilePath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "WpfHexEditor", "plugin-isolation-overrides.json");

    // PHASE 1-4: New MetricsEngine for advanced monitoring
    private readonly PluginMetricsEngine _metricsEngine;

    // Legacy timer (deprecated - kept for backward compatibility)
    [Obsolete("Use MetricsEngine instead")]
    private readonly DispatcherTimer _samplingTimer;
    private TimeSpan _lastCpuTime;
    private DateTime _lastCpuCheck;
    private double _lastSampledCpuPercent;

    /// <summary>Interval between diagnostics samples. Default: 5 seconds.</summary>
    public TimeSpan DiagnosticSamplingInterval
    {
        get => _metricsEngine.PassiveSamplingInterval;
        set => _metricsEngine.PassiveSamplingInterval = value;
    }

    /// <summary>
    /// Process-level CPU% measured at the most recent periodic sampling tick.
    /// Now delegated to MetricsEngine for improved accuracy.
    /// </summary>
    public double LastSampledCpuPercent => _metricsEngine.LastSampledCpuPercent;

    /// <summary>Access to the metrics engine for advanced diagnostics.</summary>
    public PluginMetricsEngine MetricsEngine => _metricsEngine;

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
        Dispatcher dispatcher,
        Action<string>? logger = null,
        Action<string>? errorLogger = null)
    {
        _hostContext = hostContext ?? throw new ArgumentNullException(nameof(hostContext));
        _uiRegistry = uiRegistry ?? throw new ArgumentNullException(nameof(uiRegistry));
        _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
        _log = logger ?? (_ => { });
        _logError = errorLogger ?? _log;
        _dispatcher = dispatcher;

        // PHASE 1-4: Initialize new MetricsEngine
        _metricsEngine = new PluginMetricsEngine(GetLoadedEntries, dispatcher, _log);
        _metricsEngine.Start();

        // Legacy compatibility (deprecated)
        _lastCpuTime  = Process.GetCurrentProcess().TotalProcessorTime;
        _lastCpuCheck = DateTime.UtcNow;

        _watchdog = new PluginWatchdog();
        _watchdog.PluginNonResponsive += OnPluginNonResponsive;

        _slowDetector = new SlowPluginDetector(GetLoadedEntries, dispatcher);
        _slowDetector.SlowPluginDetected += (s, e) => SlowPluginDetected?.Invoke(this, e);
        _slowDetector.Start();

        // Legacy timer (deprecated - MetricsEngine handles sampling now)
        _samplingTimer = new System.Windows.Threading.DispatcherTimer(
            System.Windows.Threading.DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _samplingTimer.Tick += OnSamplingTick;

        // REMOVED: PerformInitialSample() - MetricsEngine handles initialization
        // The old approach caused race conditions and inaccurate startup metrics

        _samplingTimer.Start();

        LoadIsolationOverrides();
    }

    // --- Discovery --------------------------------------------------------------

    /// <summary>
    /// Discovers all plugins under <see cref="UserPluginsDir"/> and any provided extra directories.
    /// Manifest parsing runs in parallel (Phase 6c) for fast startup with many plugins.
    /// Returns validated manifests sorted by dependency order.
    /// </summary>
    public async Task<IReadOnlyList<PluginManifest>> DiscoverPluginsAsync(
        IEnumerable<string>? extraDirectories = null,
        CancellationToken ct = default)
    {
        var searchDirs = new List<string> { UserPluginsDir };
        if (extraDirectories is not null) searchDirs.AddRange(extraDirectories);

        // Collect all candidate plugin directories first
        var pluginDirs = new List<string>();
        foreach (var dir in searchDirs)
        {
            _log($"[PluginSystem] Scanning: {dir} (exists: {Directory.Exists(dir)})");
            if (Directory.Exists(dir))
                pluginDirs.AddRange(Directory.GetDirectories(dir));
        }

        // Phase 6c: Parse all manifests in parallel
        var tasks = pluginDirs.Select(d => TryLoadManifestAsync(d)).ToArray();
        var manifests = await Task.WhenAll(tasks).ConfigureAwait(false);

        var result = manifests.Where(m => m is not null).Cast<PluginManifest>().ToList();
        _log($"[PluginSystem] Discovered {result.Count} plugin(s).");
        return result;
    }

    // --- Load --------------------------------------------------------------------

    /// <summary>
    /// Loads a plugin from a discovered manifest.
    /// Supports both InProcess (AssemblyLoadContext) and Sandbox (out-of-process) isolation modes.
    /// Assembly loading and InitializeAsync run off the UI thread; only WPF control registration
    /// is dispatched back to the STA Dispatcher.
    /// </summary>
    public async Task<PluginEntry> LoadPluginAsync(PluginManifest manifest, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        // Use user override if present, else fall back to manifest declaration.
        var effectiveMode = GetEffectiveIsolationMode(manifest);

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
            IWpfHexEditorPlugin instance;
            PluginLoadContext? loadContext = null;

            if (effectiveMode == PluginIsolationMode.Sandbox)
            {
                // ── Phase 5: Out-of-process sandbox ─────────────────────────────
                var proxy = new SandboxPluginProxy(manifest, _log);
                proxy.SetMetricsEngine(_metricsEngine);

                // Forward sandbox crash events to the IDE crash handler
                proxy.CrashReceived += (_, crash) =>
                {
                    var ex = new Exception($"[Sandbox crash] {crash.ExceptionType}: {crash.Message}");
                    entry.SetState(PluginState.Faulted);
                    entry.SetFaultException(ex);
                    RaiseOnDispatcher(() => PluginCrashed?.Invoke(this,
                        new PluginFaultedEventArgs
                        {
                            PluginId   = manifest.Id,
                            PluginName = manifest.Name,
                            Exception  = ex,
                            Phase      = crash.Phase,
                        }));
                };

                instance = proxy;
                // Sandbox plugins declare permissions via manifest — no ALC needed
                _permissionService.InitializeForPlugin(
                    manifest.Id, manifest.Permissions?.ToPermissionFlags() ?? SDK.Models.PluginPermission.None);
            }
            else
            {
                // ── InProcess: AssemblyLoadContext per plugin ────────────────────
                // Phase 6b: Assembly loading runs off the UI thread (no Dispatcher.InvokeAsync here)
                var pluginDir    = ResolvePluginDirectory(manifest);
                var assemblyPath = Path.Combine(pluginDir, manifest.Assembly?.File ?? $"{manifest.Id}.dll");

                if (!File.Exists(assemblyPath))
                    throw new FileNotFoundException($"Plugin assembly not found: {assemblyPath}");

                // Create collectible ALC off the UI thread — no WPF objects created yet
                loadContext = new PluginLoadContext(assemblyPath);
                var assembly = await Task.Run(() =>
                    loadContext.LoadFromAssemblyPath(assemblyPath), ct).ConfigureAwait(false);

                var entryType = assembly.GetType(manifest.EntryPoint)
                    ?? throw new TypeLoadException(
                        $"Entry point type '{manifest.EntryPoint}' not found in '{assemblyPath}'.");

                instance = (IWpfHexEditorPlugin)(Activator.CreateInstance(entryType)
                    ?? throw new InvalidOperationException(
                        $"Could not create instance of '{manifest.EntryPoint}'."));

                var declaredPerms = instance.Capabilities.ToPermissionFlags();
                _permissionService.InitializeForPlugin(manifest.Id, declaredPerms);
            }

            entry.SetInstance(instance, loadContext);

            // Build per-plugin scoped context (timed hex service wraps callbacks for metrics)
            var timedHex     = new TimedHexEditorService(_hostContext.HexEditor, entry.Diagnostics, _metricsEngine);
            timedHex.SetPluginId(manifest.Id);
            var pluginContext = new PluginScopedContext(_hostContext, timedHex);

            // Phase 3: Capture baseline memory BEFORE InitializeAsync
            entry.BaselineMemoryBytes = GC.GetTotalMemory(forceFullCollection: false);
            entry.Diagnostics.BaselineMemoryBytes = entry.BaselineMemoryBytes;

            var cpuBefore = Process.GetCurrentProcess().TotalProcessorTime;
            var sw = Stopwatch.StartNew();

            // Phase 6b: For InProcess plugins, InitializeAsync MUST run on the STA Dispatcher
            // because plugins create WPF controls. For Sandbox plugins it runs on the thread pool
            // (only IPC messages go over the pipe — no WPF created in-process).
            Task initTask;
            if (effectiveMode == PluginIsolationMode.Sandbox)
            {
                initTask = instance.InitializeAsync(pluginContext, ct);
            }
            else
            {
                initTask = await _dispatcher.InvokeAsync(
                    () => instance.InitializeAsync(pluginContext, ct));
            }

            var elapsed = await _watchdog.WrapAsync(
                manifest.Id, "InitializeAsync", initTask, _watchdog.InitTimeout)
                .ConfigureAwait(false);

            sw.Stop();
            var cpuAfter = Process.GetCurrentProcess().TotalProcessorTime;
            var cpuDelta = cpuAfter - cpuBefore;

            // Phase 3: Capture post-init memory AFTER InitializeAsync
            entry.PostInitMemoryBytes = GC.GetTotalMemory(forceFullCollection: false);
            entry.Diagnostics.PostInitMemoryBytes = entry.PostInitMemoryBytes;

            var cpuPct = elapsed.TotalMilliseconds > 0
                ? Math.Clamp(
                    cpuDelta.TotalMilliseconds / (elapsed.TotalMilliseconds * Environment.ProcessorCount) * 100.0,
                    0.0, 100.0)
                : 0.0;

            entry.Diagnostics.Record(cpuPct, entry.PostInitMemoryBytes, elapsed);
            entry.SetInitDuration(elapsed);
            entry.SetState(PluginState.Loaded);

            // Auto-register options page (InProcess plugins only — sandbox UI is remote)
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
                new PluginFaultedEventArgs
                {
                    PluginId   = manifest.Id,
                    PluginName = manifest.Name,
                    Exception  = ex,
                    Phase      = "Load",
                }));
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
            _log($"[PluginSystem] Loading '{manifest.Name}' ({manifest.Id})...");
            try
            {
                await LoadPluginAsync(manifest, ct).ConfigureAwait(false);
                _log($"[PluginSystem] '{manifest.Name}' loaded OK.");
            }
            catch (Exception ex)
            {
                _logError($"[PluginSystem] ERROR loading '{manifest.Name}': {ex.Message}");
                /* entry already marked Faulted; PluginCrashed already raised */
            }
        }

        // PHASE 1: Initialize MetricsEngine after all plugins loaded
        // This ensures accurate baseline and prevents race conditions
        await _metricsEngine.InitializeAsync(delayMs: 150).ConfigureAwait(false);
    }

    /// <summary>
    /// Sorts manifests so that dependencies are loaded before dependents using Kahn's algorithm.
    /// Phase 6c: Detects dependency cycles and logs them instead of silently infinite-looping.
    /// Within the same dependency level, lower LoadPriority values load first.
    /// </summary>
    private IEnumerable<PluginManifest> TopologicalSort(IReadOnlyList<PluginManifest> manifests)
    {
        var byId = manifests.ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);

        // Build in-degree map and adjacency list (dep → dependents)
        var inDegree   = manifests.ToDictionary(m => m.Id, _ => 0, StringComparer.OrdinalIgnoreCase);
        var dependents = manifests.ToDictionary(m => m.Id,
            _ => new List<string>(), StringComparer.OrdinalIgnoreCase);

        foreach (var m in manifests)
        {
            foreach (var depId in m.Dependencies)
            {
                if (!byId.ContainsKey(depId)) continue; // unknown dep — skip
                inDegree[m.Id]++;
                dependents[depId].Add(m.Id);
            }
        }

        // Kahn's BFS: start with nodes that have no dependencies
        // Use a priority queue keyed by LoadPriority so lower-priority values load first
        var queue = new SortedSet<(int Priority, string Id)>(
            manifests
                .Where(m => inDegree[m.Id] == 0)
                .Select(m => (m.LoadPriority, m.Id)));

        var result = new List<PluginManifest>(manifests.Count);

        while (queue.Count > 0)
        {
            var (_, id) = queue.Min;
            queue.Remove(queue.Min);

            result.Add(byId[id]);

            foreach (var dependentId in dependents[id])
            {
                inDegree[dependentId]--;
                if (inDegree[dependentId] == 0)
                    queue.Add((byId[dependentId].LoadPriority, dependentId));
            }
        }

        // Phase 6c: Cycle detection — any remaining non-zero in-degree = cycle
        var cycleIds = inDegree.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToList();
        if (cycleIds.Count > 0)
        {
            _logError($"[PluginSystem] Circular dependency detected. Affected plugins will be skipped: " +
                      string.Join(", ", cycleIds));
        }

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

    // --- Isolation Mode Override -------------------------------------------------

    /// <summary>
    /// Returns the effective isolation mode for a plugin:
    /// user override if set, otherwise the manifest declaration.
    /// </summary>
    public PluginIsolationMode GetEffectiveIsolationMode(PluginManifest manifest)
        => _isolationOverrides.TryGetValue(manifest.Id, out var mode) ? mode : manifest.IsolationMode;

    /// <summary>
    /// Changes the isolation mode for a plugin at runtime and hot-reloads it immediately.
    /// The override is persisted to AppData and survives IDE restarts.
    /// </summary>
    public async Task SetIsolationOverrideAsync(
        string pluginId, PluginIsolationMode mode, CancellationToken ct = default)
    {
        PluginEntry? entry;
        lock (_lock) _entries.TryGetValue(pluginId, out entry);
        if (entry is null) return;

        _isolationOverrides[pluginId] = mode;
        SaveIsolationOverrides();

        var manifest = entry.Manifest;
        _log($"[PluginSystem] '{pluginId}' isolation → {mode}. Hot-reloading…");

        await UnloadPluginAsync(pluginId, ct).ConfigureAwait(false);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        await Task.Delay(200, ct).ConfigureAwait(false);
        await LoadPluginAsync(manifest, ct).ConfigureAwait(false);
    }

    private void LoadIsolationOverrides()
    {
        try
        {
            if (!File.Exists(OverridesFilePath)) return;
            var json = File.ReadAllText(OverridesFilePath);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (dict is null) return;
            foreach (var (k, v) in dict)
                if (Enum.TryParse<PluginIsolationMode>(v, out var parsed))
                    _isolationOverrides[k] = parsed;
        }
        catch { /* best-effort: corrupt/missing file is not fatal */ }
    }

    private void SaveIsolationOverrides()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(OverridesFilePath)!);
            var dict = _isolationOverrides.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());
            File.WriteAllText(OverridesFilePath,
                JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* best-effort */ }
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

    private async Task<PluginManifest?> TryLoadManifestAsync(string pluginDir)
    {
        var manifestPath = Path.Combine(pluginDir, "manifest.json");
        if (!File.Exists(manifestPath)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(manifestPath).ConfigureAwait(false);
            var manifest = JsonSerializer.Deserialize<PluginManifest>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (manifest is null)
            {
                _logError($"[PluginSystem] manifest.json is null after deserialization in '{pluginDir}'.");
                return null;
            }

            // Store the directory for later assembly resolution
            manifest.ResolvedDirectory = pluginDir;

            var validator = new PluginManifestValidator(new Version(1, 0), new Version(1, 0));
            var result = validator.Validate(manifest, pluginDir);
            if (!result.IsValid)
            {
                _logError($"[PluginSystem] Manifest invalid in '{pluginDir}': {string.Join(", ", result.Errors)}");
                return null;
            }

            return manifest;
        }
        catch (Exception ex)
        {
            _logError($"[PluginSystem] Failed to read manifest in '{pluginDir}': {ex.Message}");
            return null;
        }
    }

    private static string ResolvePluginDirectory(PluginManifest manifest)
    {
        if (!string.IsNullOrEmpty(manifest.ResolvedDirectory) && Directory.Exists(manifest.ResolvedDirectory))
            return manifest.ResolvedDirectory;

        return Path.Combine(UserPluginsDir, manifest.Id);
    }

    // --- Diagnostics sampling (continuous background monitoring) -------------------

    /// <summary>
    /// [DEPRECATED] Legacy sampling tick - kept for backward compatibility.
    /// MetricsEngine now handles all sampling operations.
    /// </summary>
    [Obsolete("Use MetricsEngine instead")]
    private void OnSamplingTick(object? sender, EventArgs e)
    {
        // Legacy compatibility - delegate to MetricsEngine
        // This method is kept to avoid breaking existing code that might depend on the timer
        var now = DateTime.UtcNow;
        var wallElapsed = now - _lastCpuCheck;
        if (wallElapsed.TotalMilliseconds < 1) return;

        var process = Process.GetCurrentProcess();
        var cpuNow = process.TotalProcessorTime;
        var cpuDelta = cpuNow - _lastCpuTime;

        double cpuPct = cpuDelta.TotalMilliseconds
            / (wallElapsed.TotalMilliseconds * Environment.ProcessorCount) * 100.0;
        cpuPct = Math.Clamp(cpuPct, 0.0, 100.0);
        _lastSampledCpuPercent = cpuPct;

        _lastCpuTime = cpuNow;
        _lastCpuCheck = now;
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
        // Dispose MetricsEngine first
        _metricsEngine?.Dispose();

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
