//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.PluginHost;

/// <summary>
/// Internal record of a single plugin managed by <see cref="PluginHost"/>.
/// </summary>
public sealed class PluginEntry
{
    // -- Identity -------------------------------------------------------------

    /// <summary>Parsed plugin manifest.</summary>
    public PluginManifest Manifest { get; }

    // -- Live Instance --------------------------------------------------------

    /// <summary>Plugin instance (null until successfully loaded).</summary>
    public IWpfHexEditorPlugin? Instance { get; set; }

    /// <summary>Isolated AssemblyLoadContext for InProcess plugins (null for Sandbox).</summary>
    internal PluginLoadContext? LoadContext { get; set; }

    // -- State ----------------------------------------------------------------

    private volatile PluginState _state = PluginState.Unloaded;

    /// <summary>Current lifecycle state of the plugin.</summary>
    public PluginState State
    {
        get => _state;
        set => _state = value;
    }

    /// <summary>
    /// The concrete isolation mode this plugin was actually loaded with.
    /// For Auto-mode plugins this reflects the resolved InProcess or Sandbox decision.
    /// For explicitly declared plugins this mirrors <see cref="PluginManifest.IsolationMode"/>.
    /// Populated by <c>WpfPluginHost</c> before the load branch executes.
    /// </summary>
    public SDK.Models.PluginIsolationMode ResolvedIsolationMode { get; private set; }

    /// <summary>Exception captured during a Faulted transition (null otherwise).</summary>
    public Exception? FaultException { get; set; }

    // -- Timing ---------------------------------------------------------------

    /// <summary>UTC timestamp when the plugin was successfully initialized.</summary>
    public DateTime? LoadedAt { get; set; }

    /// <summary>Time taken by the plugin's InitializeAsync call.</summary>
    public TimeSpan InitDuration { get; set; }

    // -- Diagnostics ----------------------------------------------------------

    /// <summary>Rolling performance diagnostics collector for this plugin.</summary>
    public PluginDiagnosticsCollector Diagnostics { get; } = new();

    // PHASE 3: Memory baseline tracking
    /// <summary>Memory footprint (bytes) measured before InitializeAsync.</summary>
    public long BaselineMemoryBytes { get; set; }

    /// <summary>Memory footprint (bytes) measured after InitializeAsync.</summary>
    public long PostInitMemoryBytes { get; set; }

    /// <summary>
    /// Estimated memory footprint attributable to this plugin.
    /// Calculated as (PostInitMemoryBytes - BaselineMemoryBytes).
    /// </summary>
    public long EstimatedMemoryFootprint => Math.Max(0, PostInitMemoryBytes - BaselineMemoryBytes);

    // -- Constructor ----------------------------------------------------------

    public PluginEntry(PluginManifest manifest)
    {
        Manifest = manifest;
    }

    // -- Mutation helpers (called by WpfPluginHost) ----------------------------

    internal void SetState(PluginState state) => _state = state;

    internal void SetResolvedIsolationMode(SDK.Models.PluginIsolationMode mode) => ResolvedIsolationMode = mode;

    internal void SetInstance(IWpfHexEditorPlugin instance, PluginLoadContext? context)
    {
        Instance = instance;
        LoadContext = context;
    }

    internal void SetInitDuration(TimeSpan duration) => InitDuration = duration;

    internal void SetLoadedAt(DateTime timestamp)
    {
        LoadedAt = timestamp;
        Diagnostics.SetLoadTime(timestamp);
    }

    internal void SetFaultException(Exception exception) => FaultException = exception;

    /// <summary>
    /// Releases the plugin instance and unloads the AssemblyLoadContext (if collectible).
    /// Supports both <see cref="IDisposable"/> (in-process) and
    /// <see cref="IAsyncDisposable"/> (sandbox proxy) plugin instances.
    /// Synchronous disposal of <see cref="IAsyncDisposable"/> is safe here:
    /// the sandbox process has already been killed by ShutdownAsync/ForceKillAsync
    /// before Unload is called, so this only cleans up managed resources.
    /// </summary>
    internal void Unload()
    {
        if (Instance is IDisposable disposable)
            disposable.Dispose();
        else if (Instance is IAsyncDisposable asyncDisposable)
            asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();

        Instance = null;
        try { LoadContext?.Unload(); }
        catch { /* ALC may have been collected already */ }
        LoadContext = null;
    }
}
