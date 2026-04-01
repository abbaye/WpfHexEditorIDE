// ==========================================================
// Project: WpfHexEditor.PluginDev
// File: Loading/PluginDevLoader.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Hot-reload loader for in-IDE plugin development.
//     Uses a CollectibleAssemblyLoadContext per session so that
//     the loaded assembly can be GC-collected after unload,
//     enabling iterative build-and-reload without restart.
//
// Architecture Notes:
//     Pattern: Disposable ALC wrapper.
//     Each Load() creates a fresh CollectibleLoadContext.
//     Unload() disposes the context and nulls the WeakReference,
//     GC.Collect() is called twice to force finalization.
// ==========================================================

using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Windows.Threading;
using WpfHexEditor.PluginDev.Build;

namespace WpfHexEditor.PluginDev.Loading;

/// <summary>
/// Manages the hot-reload lifecycle for a single developer plugin session.
/// </summary>
public sealed class PluginDevLoader : IDisposable
{
    // -----------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------

    private const int WatchDebounceMs = 800;

    private DevPluginHandle?    _currentHandle;
    private bool                _disposed;

    // File-watch state (set by StartWatchingProject)
    private FileSystemWatcher?  _watcher;
    private DispatcherTimer?    _debounceTimer;
    private string?             _watchedProjectFile;
    private string?             _buildConfiguration;
    private IProgress<string>?  _buildProgress;

    // -----------------------------------------------------------------------
    // Events
    // -----------------------------------------------------------------------

    /// <summary>Raised after a plugin assembly has been successfully loaded.</summary>
    public event EventHandler<PluginLoadedEventArgs>? Loaded;

    /// <summary>Raised after the assembly has been unloaded.</summary>
    public event EventHandler? Unloaded;

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Loads the assembly at <paramref name="assemblyPath"/> into an isolated
    /// CollectibleAssemblyLoadContext and raises <see cref="Loaded"/>.
    /// </summary>
    public void LoadPlugin(string assemblyPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Unload any previously loaded instance first.
        UnloadCurrent();

        var context  = new CollectibleLoadContext(assemblyPath);
        var assembly = context.LoadFromAssemblyPath(assemblyPath);

        _currentHandle = new DevPluginHandle(context, assembly);
        Loaded?.Invoke(this, new PluginLoadedEventArgs(assembly, assemblyPath));
    }

    /// <summary>
    /// Unloads the current plugin assembly and triggers GC collection.
    /// </summary>
    public void UnloadPlugin()
    {
        UnloadCurrent();
        Unloaded?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Shorthand for Unload + Load in one step.
    /// </summary>
    public void ReloadPlugin(string newAssemblyPath)
    {
        UnloadCurrent();
        LoadPlugin(newAssemblyPath);
    }

    /// <summary>Returns the currently loaded assembly, or null if nothing is loaded.</summary>
    public Assembly? CurrentAssembly => _currentHandle?.Assembly;

    /// <summary>True if a plugin is currently loaded.</summary>
    public bool IsLoaded => _currentHandle is not null;

    /// <summary>
    /// Starts watching <paramref name="projectFilePath"/> for source-file changes.
    /// When a change is detected and <c>AutoRebuildOnSave</c> is enabled in settings,
    /// triggers a background build and reloads the resulting assembly automatically.
    /// Uses an 800 ms debounce to absorb multi-file save bursts from IDEs and editors.
    /// </summary>
    /// <param name="projectFilePath">Absolute path to the .whproj or .csproj file.</param>
    /// <param name="configuration">MSBuild configuration name (default: "Debug").</param>
    /// <param name="progress">Optional sink for build log lines.</param>
    public void StartWatchingProject(
        string             projectFilePath,
        string             configuration = "Debug",
        IProgress<string>? progress      = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        StopWatchingProject();

        _watchedProjectFile = projectFilePath;
        _buildConfiguration = configuration;
        _buildProgress      = progress;

        var projectDir = Path.GetDirectoryName(projectFilePath) ?? ".";

        _watcher = new FileSystemWatcher(projectDir)
        {
            NotifyFilter        = NotifyFilters.LastWrite | NotifyFilters.FileName,
            Filter              = "*.*",
            IncludeSubdirectories = true,
            EnableRaisingEvents = true,
        };

        _watcher.Changed += OnProjectFileChanged;
        _watcher.Created += OnProjectFileChanged;
        _watcher.Renamed += (s, e) => OnProjectFileChanged(s, e);
    }

    /// <summary>Stops watching the project directory.</summary>
    public void StopWatchingProject()
    {
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnProjectFileChanged;
            _watcher.Created -= OnProjectFileChanged;
            _watcher.Dispose();
            _watcher = null;
        }

        _debounceTimer?.Stop();
        _debounceTimer = null;
        _watchedProjectFile = null;
        _buildConfiguration = null;
        _buildProgress      = null;
    }

    // -----------------------------------------------------------------------
    // File-watch internals
    // -----------------------------------------------------------------------

    private void OnProjectFileChanged(object sender, FileSystemEventArgs e)
    {
        // Ignore non-source-code files.
        var ext = Path.GetExtension(e.FullPath).ToLowerInvariant();
        if (ext is not (".cs" or ".xaml" or ".whproj" or ".csproj" or ".json"))
            return;

        // All timer operations must be on the Dispatcher thread.
        var dispatcher = System.Windows.Application.Current?.Dispatcher
                      ?? Dispatcher.CurrentDispatcher;
        dispatcher.InvokeAsync(ScheduleAutoRebuild);
    }

    private void ScheduleAutoRebuild()
    {
        // Check settings before scheduling — avoids creating timers when feature is off.
        if (WpfHexEditor.Core.Options.AppSettingsService.Instance.Current.PluginDev.AutoRebuildOnSave == false)
            return;

        if (_debounceTimer is null)
        {
            _debounceTimer = new DispatcherTimer(DispatcherPriority.Background,
                System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(WatchDebounceMs),
            };
            _debounceTimer.Tick += (_, _) =>
            {
                _debounceTimer.Stop();
                _ = AutoRebuildAndReloadAsync();
            };
        }

        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private async Task AutoRebuildAndReloadAsync()
    {
        if (_watchedProjectFile is null) return;

        _buildProgress?.Report("[DevWatch] Auto-rebuild triggered…");

        try
        {
            var orchestrator = new PluginBuildOrchestrator();
            var result = await orchestrator.BuildAsync(
                _watchedProjectFile,
                _buildConfiguration ?? "Debug",
                _buildProgress).ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                _buildProgress?.Report(
                    $"[DevWatch] Build FAILED — {result.Errors.Count} error(s).");
                BuildFailed?.Invoke(this, new PluginAutoRebuildFailedEventArgs(result.Errors));
                return;
            }

            _buildProgress?.Report(
                $"[DevWatch] Build succeeded: {result.OutputAssembly}");

            if (!string.IsNullOrEmpty(result.OutputAssembly))
                ReloadPlugin(result.OutputAssembly);

            BuildSucceeded?.Invoke(this, new PluginAutoRebuildSucceededEventArgs(result.OutputAssembly));
        }
        catch (Exception ex)
        {
            _buildProgress?.Report($"[DevWatch] Auto-rebuild exception: {ex.Message}");
        }
    }

    // -----------------------------------------------------------------------
    // Auto-rebuild events
    // -----------------------------------------------------------------------

    /// <summary>Raised after a successful auto-rebuild and reload.</summary>
    public event EventHandler<PluginAutoRebuildSucceededEventArgs>? BuildSucceeded;

    /// <summary>Raised after a failed auto-rebuild.</summary>
    public event EventHandler<PluginAutoRebuildFailedEventArgs>? BuildFailed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopWatchingProject();
        UnloadCurrent();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void UnloadCurrent()
    {
        if (_currentHandle is null) return;
        _currentHandle.Dispose();
        _currentHandle = null;

        // Two GC passes to ensure finalizers run and the ALC is collected.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}

// -----------------------------------------------------------------------
// DevPluginHandle
// -----------------------------------------------------------------------

/// <summary>
/// Wraps a weak reference to the ALC and the loaded assembly.
/// Disposed when the plugin is unloaded.
/// </summary>
internal sealed class DevPluginHandle : IDisposable
{
    private readonly WeakReference<CollectibleLoadContext> _contextRef;

    internal Assembly Assembly { get; }

    internal DevPluginHandle(CollectibleLoadContext context, Assembly assembly)
    {
        _contextRef = new WeakReference<CollectibleLoadContext>(context);
        Assembly    = assembly;
    }

    public void Dispose()
    {
        if (_contextRef.TryGetTarget(out var ctx))
            ctx.Unload();
    }
}

// -----------------------------------------------------------------------
// CollectibleLoadContext
// -----------------------------------------------------------------------

/// <summary>
/// Isolated, collectible assembly load context for developer plugins.
/// </summary>
internal sealed class CollectibleLoadContext(string pluginPath)
    : AssemblyLoadContext(name: System.IO.Path.GetFileNameWithoutExtension(pluginPath), isCollectible: true)
{
    private readonly string _pluginDir = System.IO.Path.GetDirectoryName(pluginPath)!;

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Attempt to resolve from the plugin's own directory first.
        var local = System.IO.Path.Combine(_pluginDir, $"{assemblyName.Name}.dll");
        if (System.IO.File.Exists(local))
            return LoadFromAssemblyPath(local);

        // Fall back to the default context.
        return null;
    }
}

// -----------------------------------------------------------------------
// Event args
// -----------------------------------------------------------------------

/// <summary>Event arguments for <see cref="PluginDevLoader.Loaded"/>.</summary>
public sealed class PluginLoadedEventArgs(Assembly assembly, string assemblyPath) : EventArgs
{
    public Assembly Assembly     { get; } = assembly;
    public string   AssemblyPath { get; } = assemblyPath;
}

/// <summary>Event arguments for <see cref="PluginDevLoader.BuildSucceeded"/>.</summary>
public sealed class PluginAutoRebuildSucceededEventArgs(string outputAssembly) : EventArgs
{
    public string OutputAssembly { get; } = outputAssembly;
}

/// <summary>Event arguments for <see cref="PluginDevLoader.BuildFailed"/>.</summary>
public sealed class PluginAutoRebuildFailedEventArgs(IReadOnlyList<string> errors) : EventArgs
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
