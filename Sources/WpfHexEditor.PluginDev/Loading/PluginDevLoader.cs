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

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace WpfHexEditor.PluginDev.Loading;

/// <summary>
/// Manages the hot-reload lifecycle for a single developer plugin session.
/// </summary>
public sealed class PluginDevLoader : IDisposable
{
    // -----------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------

    private DevPluginHandle? _currentHandle;
    private bool             _disposed;

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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
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
