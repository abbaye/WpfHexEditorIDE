//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: DevTools/PluginDevLoader.cs
// Created: 2026-03-15
// Description:
//     In-IDE plugin development hot-reload engine (Phase 8).
//     Watches a plugin project output directory for DLL changes,
//     automatically unloads the old version, and reloads the new one
//     without restarting the IDE.
//
// Architecture Notes:
//     - Pattern: Observer (FileSystemWatcher) + Decorator (around WpfPluginHost)
//     - Collectible AssemblyLoadContext ensures old DLL can be unloaded by GC
//     - Debounce: 500ms after last file change before triggering reload
//       (MSBuild often writes multiple files in quick succession)
//     - Thread safety: all reload operations are marshalled to the Dispatcher
// ==========================================================

using System.IO;
using System.Windows.Threading;

namespace WpfHexEditor.PluginHost.DevTools;

/// <summary>
/// Watches a plugin development output directory and hot-reloads the plugin
/// when its DLL is rebuilt. Enables live plugin development inside the IDE
/// without restarting.
/// </summary>
public sealed class PluginDevLoader : IDisposable
{
    private const int DebounceMs = 500;

    private readonly WpfPluginHost _host;
    private readonly Dispatcher _dispatcher;
    private readonly Action<string> _log;

    // Watcher per watched plugin: pluginId → watcher
    private readonly Dictionary<string, FileSystemWatcher> _watchers = new(StringComparer.OrdinalIgnoreCase);
    // Debounce timers: pluginId → timer
    private readonly Dictionary<string, DispatcherTimer> _debouncers = new(StringComparer.OrdinalIgnoreCase);
    // Manifest cache: pluginId → manifest path (for reload)
    private readonly Dictionary<string, string> _manifestPaths = new(StringComparer.OrdinalIgnoreCase);

    private bool _disposed;

    // ── Events ────────────────────────────────────────────────────────────────
    public event EventHandler<PluginDevReloadEventArgs>? ReloadStarted;
    public event EventHandler<PluginDevReloadEventArgs>? ReloadCompleted;
    public event EventHandler<PluginDevReloadFailedEventArgs>? ReloadFailed;

    // ─────────────────────────────────────────────────────────────────────────
    public PluginDevLoader(WpfPluginHost host, Dispatcher dispatcher, Action<string>? logger = null)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _log = logger ?? (_ => { });
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Starts watching <paramref name="pluginOutputDir"/> for DLL changes.
    /// When the plugin's DLL is rebuilt, it is hot-reloaded automatically.
    /// </summary>
    /// <param name="pluginId">Plugin ID (as declared in manifest.json).</param>
    /// <param name="pluginOutputDir">Directory containing the plugin DLL and manifest.json.</param>
    public void Watch(string pluginId, string pluginOutputDir)
    {
        ArgumentNullException.ThrowIfNull(pluginId);
        ArgumentNullException.ThrowIfNull(pluginOutputDir);

        if (!Directory.Exists(pluginOutputDir))
            throw new DirectoryNotFoundException($"Plugin output directory not found: {pluginOutputDir}");

        StopWatching(pluginId); // remove any existing watcher

        var manifestPath = Path.Combine(pluginOutputDir, "manifest.json");
        _manifestPaths[pluginId] = manifestPath;

        var watcher = new FileSystemWatcher(pluginOutputDir, "*.dll")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };

        watcher.Changed += (_, _) => OnFileChanged(pluginId);
        watcher.Created += (_, _) => OnFileChanged(pluginId);

        _watchers[pluginId] = watcher;
        _log($"[PluginDevLoader] Watching '{pluginId}' → {pluginOutputDir}");
    }

    /// <summary>Stops watching a specific plugin directory.</summary>
    public void StopWatching(string pluginId)
    {
        if (_watchers.TryGetValue(pluginId, out var w))
        {
            w.EnableRaisingEvents = false;
            w.Dispose();
            _watchers.Remove(pluginId);
        }

        if (_debouncers.TryGetValue(pluginId, out var timer))
        {
            timer.Stop();
            _debouncers.Remove(pluginId);
        }

        _manifestPaths.Remove(pluginId);
    }

    /// <summary>Stops watching all plugin directories.</summary>
    public void StopAll()
    {
        foreach (var id in _watchers.Keys.ToList())
            StopWatching(id);
    }

    // ── Internal ─────────────────────────────────────────────────────────────

    private void OnFileChanged(string pluginId)
    {
        // All UI/timer operations must be on the Dispatcher thread
        _dispatcher.InvokeAsync(() => ScheduleReload(pluginId));
    }

    private void ScheduleReload(string pluginId)
    {
        // Debounce: reset timer on every change, fire once after quiet period
        if (!_debouncers.TryGetValue(pluginId, out var timer))
        {
            timer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(DebounceMs),
            };
            timer.Tick += (_, _) => TriggerReload(pluginId);
            _debouncers[pluginId] = timer;
        }

        timer.Stop();
        timer.Start();
    }

    private void TriggerReload(string pluginId)
    {
        _debouncers.TryGetValue(pluginId, out var timer);
        timer?.Stop();

        _log($"[PluginDevLoader] Change detected — reloading '{pluginId}'...");
        ReloadStarted?.Invoke(this, new PluginDevReloadEventArgs(pluginId));

        _ = ReloadAsync(pluginId);
    }

    private async Task ReloadAsync(string pluginId)
    {
        try
        {
            if (!_manifestPaths.TryGetValue(pluginId, out var manifestPath))
                return;

            // Unload old instance
            await _host.UnloadPluginAsync(pluginId).ConfigureAwait(false);

            // Give GC a chance to release the old ALC
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Wait briefly for the file system to finish writing
            await Task.Delay(200).ConfigureAwait(false);

            // Reload from fresh manifest
            var validator = new PluginManifestValidator(new Version(1, 0), new Version(1, 0));
            var manifestDir = Path.GetDirectoryName(manifestPath)!;
            var json = await File.ReadAllTextAsync(manifestPath).ConfigureAwait(false);
            var manifest = System.Text.Json.JsonSerializer.Deserialize<SDK.Models.PluginManifest>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (manifest is null)
                throw new InvalidOperationException($"Could not deserialize manifest: {manifestPath}");

            manifest.ResolvedDirectory = manifestDir;

            var result = validator.Validate(manifest, manifestDir);
            if (!result.IsValid)
                throw new InvalidOperationException(
                    $"Manifest invalid: {string.Join(", ", result.Errors)}");

            var entry = await _host.LoadPluginAsync(manifest).ConfigureAwait(false);

            _log($"[PluginDevLoader] '{pluginId}' reloaded OK (v{entry.Manifest.Version}).");
            await _dispatcher.InvokeAsync(() =>
                ReloadCompleted?.Invoke(this, new PluginDevReloadEventArgs(pluginId)));
        }
        catch (Exception ex)
        {
            _log($"[PluginDevLoader] Reload FAILED for '{pluginId}': {ex.Message}");
            await _dispatcher.InvokeAsync(() =>
                ReloadFailed?.Invoke(this, new PluginDevReloadFailedEventArgs(pluginId, ex)));
        }
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopAll();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Event args
// ─────────────────────────────────────────────────────────────────────────────

public sealed class PluginDevReloadEventArgs : EventArgs
{
    public string PluginId { get; }
    public PluginDevReloadEventArgs(string pluginId) => PluginId = pluginId;
}

public sealed class PluginDevReloadFailedEventArgs : EventArgs
{
    public string PluginId { get; }
    public Exception Exception { get; }
    public PluginDevReloadFailedEventArgs(string pluginId, Exception ex)
    {
        PluginId = pluginId;
        Exception = ex;
    }
}
