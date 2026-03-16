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
using WpfHexEditor.Events.IDEEvents;
using WpfHexEditor.PluginHost.Monitoring;
using WpfHexEditor.PluginHost.Sandbox;
using WpfHexEditor.PluginHost.Services;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.ExtensionPoints;
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

    // -- Feature 5 & 6 --
    private readonly PluginDependencyGraph _dependencyGraph = new();
    private PluginActivationService? _activationService;

    /// <summary>
    /// Live capability registry backed by <see cref="_entries"/>.
    /// Exposed so the App layer can call <see cref="PluginCapabilityRegistryAdapter.SetInner"/>.
    /// </summary>
    public IPluginCapabilityRegistry CapabilityRegistry { get; }

    // --- Isolation mode overrides (user preference, persisted) ------------------
    private readonly Dictionary<string, PluginIsolationMode> _isolationOverrides =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly string OverridesFilePath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "WpfHexEditor", "plugin-isolation-overrides.json");

    // PHASE 1-4: New MetricsEngine for advanced monitoring
    private readonly PluginMetricsEngine _metricsEngine;

    // --- Dynamic migration monitor -----------------------------------------------
    private readonly PluginMigrationMonitor _migrationMonitor;

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

    /// <summary>
    /// Raised on the Dispatcher thread when the migration monitor detects that an InProcess
    /// plugin has exceeded a configured threshold.
    /// Only raised in <see cref="PluginMigrationMode.SuggestOnly"/> mode —
    /// in <see cref="PluginMigrationMode.AutoMigrate"/> mode the host migrates automatically.
    /// </summary>
    public event EventHandler<PluginMigrationSuggestedEventArgs>? MigrationSuggested;

    /// <summary>The active migration policy. Live-updated via <see cref="UpdateMigrationPolicy"/>.</summary>
    public PluginMigrationPolicy MigrationPolicy { get; private set; }

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

        CapabilityRegistry = new PluginCapabilityRegistry(_entries);

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

        // PerformInitialSample() removed — MetricsEngine handles startup initialization
        // and avoids the race condition with PluginMonitoringViewModel more reliably.

        _samplingTimer.Start();

        LoadIsolationOverrides();

        // Dynamic migration monitor — loaded policy persisted per-user.
        MigrationPolicy = PluginMigrationPolicy.Load();
        _migrationMonitor = new PluginMigrationMonitor(
            getLoadedEntries: GetLoadedEntries,
            policy:           MigrationPolicy,
            onMigrationTriggered: OnMigrationTriggered,
            dispatcher:       dispatcher);
        _migrationMonitor.Start();

        // Forward crash events to migration monitor so it can track crash counts.
        PluginCrashed += (_, e) => _migrationMonitor.RecordCrash(e.PluginId);
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

        // Use user override if present, else resolve (Auto → InProcess/Sandbox via decision engine).
        var effectiveMode = GetEffectiveIsolationMode(manifest);

        if (manifest.IsolationMode == PluginIsolationMode.Auto)
            _log($"[PluginSystem] Auto-resolved '{manifest.Id}' → {effectiveMode}");

        PluginEntry entry;
        lock (_lock)
        {
            if (_entries.TryGetValue(manifest.Id, out var existing) && existing.State == PluginState.Loaded)
                return existing;

            entry = new PluginEntry(manifest);
            entry.SetState(PluginState.Loading);
            entry.SetResolvedIsolationMode(effectiveMode);
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

                // Create collectible ALC off the UI thread — no WPF objects created yet.
                // Wire conflict detection before any assemblies are loaded.
                loadContext = new PluginLoadContext(assemblyPath);
                loadContext.DependencyConflictDetected += conflict =>
                {
                    lock (_lock)
                    {
                        if (_entries.TryGetValue(manifest.Id, out var e))
                        {
                            e.AssemblyConflicts.Add(conflict);
                            _log($"[ALC] '{manifest.Id}' conflict: {conflict.AssemblyName} " +
                                 $"host={conflict.HostVersion} requested={conflict.RequestedVersion}");
                        }
                    }
                };

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
            // because plugins create WPF controls in-process.
            // Sandbox plugins also start on the thread pool — their SandboxPluginProxy explicitly
            // captures Application.Current.Dispatcher (not Dispatcher.CurrentDispatcher) so the
            // SandboxUIRegistryProxy always marshals panel/menu registration to the UI thread.
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

            // Populate ALC diagnostics (InProcess only).
            if (loadContext is not null)
            {
                entry.Diagnostics.AlcAssemblyCount = loadContext.LoadedAssemblies.Count;
                entry.Diagnostics.AlcConflictCount = entry.AssemblyConflicts.Count;
            }

            // Register extension-point contributions declared in the manifest.
            RegisterExtensionContributions(entry);

            // Auto-register options page (InProcess plugins only — sandbox UI is remote)
            if (instance is IPluginWithOptions optionsPlugin)
                OptionsRegistry.RegisterPluginPage(manifest.Id, manifest.Name, optionsPlugin);

            entry.SetLoadedAt(DateTime.UtcNow);

            // Publish PluginLoadedEvent to IDE EventBus.
            _hostContext.IDEEvents.Publish(new PluginLoadedEvent
            {
                Source = "WpfPluginHost",
                PluginId = manifest.Id,
                PluginName = manifest.Name,
                IsolationMode = effectiveMode.ToString(),
            });

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
    /// Discovers and loads all plugins.
    /// Builds the dependency graph, validates constraints, registers startup plugins,
    /// registers dormant plugins (awaiting activation triggers), and inits the activation service.
    /// Faulted plugins are silently recorded.
    /// </summary>
    public async Task LoadAllAsync(IEnumerable<string>? extraDirectories = null, CancellationToken ct = default)
    {
        var manifests = await DiscoverPluginsAsync(extraDirectories, ct).ConfigureAwait(false);

        // Build the dependency graph and validate constraints.
        _dependencyGraph.Build(manifests);

        // Pre-register all entries so Validate() can look them up.
        lock (_lock)
        {
            foreach (var m in manifests)
            {
                if (!_entries.ContainsKey(m.Id))
                    _entries[m.Id] = new PluginEntry(m);
            }
        }

        var validationErrors = _dependencyGraph.Validate(_entries);
        foreach (var err in validationErrors)
        {
            _logError($"[DependencyGraph] {err.DependentPluginId} → {err.Kind}: {err.RequiredPluginId}");
            lock (_lock)
            {
                if (_entries.TryGetValue(err.DependentPluginId, out var e))
                {
                    e.UnresolvedDependencies.Add(err);
                    if (err.Kind != DependencyErrorKind.VersionMismatch || e.State == PluginState.Unloaded)
                        e.SetState(PluginState.Incompatible);
                }
            }
        }

        var sorted = _dependencyGraph.GetLoadOrder(_entries);

        foreach (var manifest in sorted)
        {
            ct.ThrowIfCancellationRequested();

            // Skip incompatible plugins.
            lock (_lock)
            {
                if (_entries.TryGetValue(manifest.Id, out var existing)
                    && existing.State == PluginState.Incompatible)
                {
                    _log($"[PluginSystem] Skipping '{manifest.Name}' — incompatible dependencies.");
                    continue;
                }
            }

            // Determine if plugin should load eagerly (startup) or be dormant.
            var activation = manifest.Activation;
            bool isDormant = activation is not null && !activation.IsStartupLoad;

            if (isDormant)
            {
                lock (_lock)
                {
                    if (_entries.TryGetValue(manifest.Id, out var entry))
                        entry.SetState(PluginState.Dormant);
                }
                _log($"[PluginSystem] '{manifest.Name}' registered as Dormant (lazy).");
                continue;
            }

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

        // Initialize activation service (after all dormant plugins are registered).
        _activationService = new PluginActivationService(
            _hostContext.IDEEvents, _entries, id => ActivateDormantPluginAsync(id));

        // PHASE 1: Initialize MetricsEngine after all plugins loaded
        await _metricsEngine.InitializeAsync(delayMs: 150).ConfigureAwait(false);
    }

    /// <summary>
    /// Activates a dormant plugin by ID — called by <see cref="PluginActivationService"/>
    /// or by the "Load Now" command in the Plugin Manager UI.
    /// </summary>
    public async Task ActivateDormantPluginAsync(string pluginId, CancellationToken ct = default)
    {
        PluginEntry? entry;
        lock (_lock) _entries.TryGetValue(pluginId, out entry);
        if (entry is null || entry.State != PluginState.Dormant) return;

        // Ensure all dependencies are activated first (if they are dormant too).
        var deps = _dependencyGraph.GetDirectDependencies(pluginId);
        foreach (var dep in deps)
        {
            PluginEntry? depEntry;
            lock (_lock) _entries.TryGetValue(dep.PluginId, out depEntry);
            if (depEntry?.State == PluginState.Dormant)
                await ActivateDormantPluginAsync(dep.PluginId, ct).ConfigureAwait(false);
        }

        _log($"[PluginSystem] Activating dormant plugin '{entry.Manifest.Name}'...");
        await LoadPluginAsync(entry.Manifest, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Cascading unload: unloads all dependents first, then the target plugin.
    /// </summary>
    public async Task CascadingUnloadAsync(string pluginId, CancellationToken ct = default)
    {
        var order = _dependencyGraph.GetCascadedUnloadOrder(pluginId);
        foreach (var id in order)
        {
            ct.ThrowIfCancellationRequested();
            _log($"[PluginSystem] Cascade unload '{id}'...");
            await UnloadPluginAsync(id, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Cascading reload: reloads the target plugin first, then its dependents in order.
    /// </summary>
    public async Task CascadingReloadAsync(string pluginId, CancellationToken ct = default)
    {
        var order = _dependencyGraph.GetCascadedReloadOrder(pluginId);
        foreach (var id in order)
        {
            ct.ThrowIfCancellationRequested();
            _log($"[PluginSystem] Cascade reload '{id}'...");
            await ReloadPluginAsync(id, ct).ConfigureAwait(false);
        }
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
            // UI unregistration (menus, panels, toolbar items) must run on the UI thread.
            // UnregisterAllForPlugin calls MenuAdapter.RemoveMenuItem which touches ItemsControl.Items.
            if (_dispatcher.CheckAccess())
                _uiRegistry.UnregisterAllForPlugin(pluginId);
            else
                await _dispatcher.InvokeAsync(() => _uiRegistry.UnregisterAllForPlugin(pluginId));

            OptionsRegistry.UnregisterPluginPage(pluginId);
            _migrationMonitor.ResetCrashCount(pluginId);

            // Unregister all extension-point contributions from this plugin.
            _hostContext.ExtensionRegistry.UnregisterAll(pluginId);

            entry.Unload();
            entry.SetState(PluginState.Unloaded);

            // Publish PluginUnloadedEvent to IDE EventBus.
            _hostContext.IDEEvents.Publish(new PluginUnloadedEvent
            {
                Source = "WpfPluginHost",
                PluginId = pluginId,
                PluginName = entry.Manifest.Name,
            });

            RaiseOnDispatcher(() => PluginUnloaded?.Invoke(this, new PluginEventArgs(pluginId, entry.Manifest.Name)));
        }
    }

    // --- Isolation Mode Override -------------------------------------------------

    /// <summary>
    /// Returns the user override if set, otherwise the raw manifest declaration
    /// (preserving <see cref="PluginIsolationMode.Auto"/> without resolving it).
    /// Use this to seed the Plugin Manager ComboBox so that Auto plugins show "Auto".
    /// </summary>
    public PluginIsolationMode GetDeclaredIsolationMode(PluginManifest manifest)
        => _isolationOverrides.TryGetValue(manifest.Id, out var mode) ? mode : manifest.IsolationMode;

    /// <summary>
    /// Returns the effective isolation mode for a plugin:
    /// user override if set, otherwise the manifest declaration with Auto resolved
    /// to a concrete InProcess or Sandbox decision via <see cref="PluginIsolationDecisionEngine"/>.
    /// </summary>
    public PluginIsolationMode GetEffectiveIsolationMode(PluginManifest manifest)
    {
        if (_isolationOverrides.TryGetValue(manifest.Id, out var overrideMode))
            return overrideMode;

        return Services.PluginIsolationDecisionEngine.Resolve(manifest);
    }

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

    // --- Dynamic migration -------------------------------------------------------

    /// <summary>
    /// Replaces the active migration policy and persists it.
    /// The monitor picks up the change on its next tick.
    /// </summary>
    public void UpdateMigrationPolicy(PluginMigrationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        MigrationPolicy = policy;
        _migrationMonitor.UpdatePolicy(policy);
        policy.Save();
    }

    /// <summary>
    /// Invoked by <see cref="PluginMigrationMonitor"/> on the Dispatcher thread when a
    /// threshold is exceeded.
    /// </summary>
    private void OnMigrationTriggered(string pluginId, MigrationTriggerReason reason)
    {
        PluginEntry? entry;
        lock (_lock) _entries.TryGetValue(pluginId, out entry);
        if (entry is null) return;

        if (MigrationPolicy.Mode == PluginMigrationMode.AutoMigrate)
        {
            _log($"[PluginSystem] Auto-migrating '{pluginId}' to Sandbox (reason: {reason}).");
            _ = SetIsolationOverrideAsync(pluginId, PluginIsolationMode.Sandbox);
        }
        else
        {
            // SuggestOnly — raise the event for the UI to handle.
            RaiseOnDispatcher(() => MigrationSuggested?.Invoke(this,
                BuildMigrationSuggestedArgs(entry, reason)));
        }
    }

    private PluginMigrationSuggestedEventArgs BuildMigrationSuggestedArgs(
        PluginEntry entry, MigrationTriggerReason reason)
    {
        var snap  = entry.Diagnostics.GetLatest();
        var memMb = snap is not null ? snap.MemoryBytes / (1024 * 1024) : 0;
        var cpu   = snap?.CpuPercent ?? 0.0;

        var message = reason switch
        {
            MigrationTriggerReason.Crashes =>
                $"Plugin '{entry.Manifest.Name}' has crashed {_migrationMonitor.GetCrashCount(entry.Manifest.Id)} time(s). Consider moving it to Sandbox.",
            MigrationTriggerReason.Memory =>
                $"Plugin '{entry.Manifest.Name}' is using {memMb} MB — consider moving it to Sandbox.",
            MigrationTriggerReason.Cpu =>
                $"Plugin '{entry.Manifest.Name}' has sustained high CPU ({cpu:F1}%) — consider moving it to Sandbox.",
            _ => $"Plugin '{entry.Manifest.Name}' may benefit from Sandbox isolation."
        };

        return new PluginMigrationSuggestedEventArgs(
            entry.Manifest.Id, entry.Manifest.Name, reason, memMb, cpu, message);
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

    // --- Sandbox Options Pages (Phase 11) ----------------------------------------

    /// <summary>
    /// Returns options page registrations declared by sandbox plugins.
    /// Each entry carries the plugin ID, display name, and the Win32 HWND of the
    /// options page HwndSource created inside the sandbox process.
    /// The caller should wrap the HWND in an HwndPanelHost and register it with
    /// OptionsPageRegistry. Call this after <see cref="LoadAllAsync"/> completes.
    /// </summary>
    public IReadOnlyList<(string PluginId, string PluginName, long Hwnd)> GetSandboxOptionsPages()
    {
        lock (_lock)
        {
            var result = new List<(string PluginId, string PluginName, long Hwnd)>();
            foreach (var entry in _entries.Values)
            {
                if (entry.State != PluginState.Loaded) continue;
                if (entry.Instance is not SandboxPluginProxy proxy) continue;
                var info = proxy.GetOptionsPageInfo();
                if (info.HasValue)
                    result.Add((info.Value.PluginId, info.Value.PluginName, info.Value.Hwnd));
            }
            return result;
        }
    }

    // --- Theme forwarding (Phase 9) -----------------------------------------------

    /// <summary>
    /// Notifies all running sandbox plugins of a theme change.
    /// Call this from the IDE theme switch handler so sandbox-hosted panels
    /// can re-apply the new brush/color tokens.
    /// </summary>
    public Task NotifyThemeChangedAsync(string themeXaml, CancellationToken ct = default)
    {
        List<SandboxPluginProxy> proxies;
        lock (_lock)
        {
            proxies = _entries.Values
                .Where(e => e.State == PluginState.Loaded && e.Instance is SandboxPluginProxy)
                .Select(e => (SandboxPluginProxy)e.Instance!)
                .ToList();
        }

        if (proxies.Count == 0) return Task.CompletedTask;

        return Task.WhenAll(proxies.Select(p => p.ForwardThemeChangeAsync(themeXaml, ct)));
    }

    // --- Extension Point Registration -------------------------------------------

    /// <summary>
    /// After InitializeAsync completes, iterates the manifest "extensions" dict and registers
    /// each declared implementation against its contract type in the IDE ExtensionRegistry.
    /// </summary>
    private void RegisterExtensionContributions(PluginEntry entry)
    {
        if (entry.Manifest.Extensions.Count == 0) return;

        // Collect all assemblies from the plugin's ALC (or AppDomain for sandbox proxies).
        var assemblies = entry.LoadContext?.LoadedAssemblies.ToList()
            ?? AppDomain.CurrentDomain.GetAssemblies().ToList();

        foreach (var (pointName, className) in entry.Manifest.Extensions)
        {
            var contractType = ExtensionPointCatalog.TryResolve(pointName);
            if (contractType is null)
            {
                _log($"[Extensions] Unknown extension point '{pointName}' in '{entry.Manifest.Id}' — skipping.");
                continue;
            }

            object? impl = null;
            foreach (var asm in assemblies)
            {
                var type = asm.GetType(className);
                if (type is null) continue;
                try { impl = Activator.CreateInstance(type); }
                catch (Exception ex)
                {
                    _logError($"[Extensions] Could not create '{className}': {ex.Message}");
                    break;
                }
                break;
            }

            if (impl is null)
            {
                _logError($"[Extensions] Type '{className}' not found for point '{pointName}' in '{entry.Manifest.Id}'.");
                continue;
            }

            // Inject IDE context if the implementation requests it.
            if (impl is IExtensionWithContext ctxImpl)
            {
                try { ctxImpl.Initialize(new PluginScopedContext(
                    _hostContext, new TimedHexEditorService(_hostContext.HexEditor, entry.Diagnostics, _metricsEngine))); }
                catch (Exception ex)
                {
                    _logError($"[Extensions] Initialize() failed for '{className}': {ex.Message}");
                }
            }

            _hostContext.ExtensionRegistry.Register(entry.Manifest.Id, contractType, impl);
            _log($"[Extensions] Registered '{pointName}' → '{className}' for plugin '{entry.Manifest.Id}'.");
        }
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
        _activationService?.Dispose();

        // Dispose MetricsEngine first
        _metricsEngine?.Dispose();
        _migrationMonitor.Dispose();

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

/// <summary>
/// Event args raised when the migration monitor suggests moving an InProcess plugin to Sandbox.
/// </summary>
public sealed class PluginMigrationSuggestedEventArgs : EventArgs
{
    public string                PluginId      { get; }
    public string                PluginName    { get; }
    public MigrationTriggerReason Reason       { get; }
    public long                  CurrentMemoryMb { get; }
    public double                CurrentCpu    { get; }
    public string                Message       { get; }

    public PluginMigrationSuggestedEventArgs(
        string pluginId, string pluginName,
        MigrationTriggerReason reason,
        long currentMemoryMb, double currentCpu,
        string message)
    {
        PluginId       = pluginId;
        PluginName     = pluginName;
        Reason         = reason;
        CurrentMemoryMb = currentMemoryMb;
        CurrentCpu     = currentCpu;
        Message        = message;
    }
}
