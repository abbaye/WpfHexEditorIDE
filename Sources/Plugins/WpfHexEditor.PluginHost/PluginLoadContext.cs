// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: PluginLoadContext.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-15
// Description:
//     Isolated AssemblyLoadContext for a single InProcess plugin.
//     Enhanced with ALC diagnostics: loaded-assembly tracking,
//     version-conflict detection, and weak-reference for GC verification.
//
// Architecture Notes:
//     Host ALC always wins on version conflict (shared assemblies are
//     returned from Default context). Conflicts are surfaced via event
//     DependencyConflictDetected for UI display.
// ==========================================================

using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.PluginHost;

/// <summary>
/// Isolated <see cref="AssemblyLoadContext"/> for a single InProcess plugin.
/// Supports hot-unload via <see cref="AssemblyLoadContext.Unload"/>.
/// Tracks loaded assemblies and version conflicts for diagnostics.
/// </summary>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly List<Assembly> _loadedAssemblies = [];
    private readonly object _lock = new();

    /// <summary>Fired when a dependency version conflict is detected (host version wins).</summary>
    public event Action<PluginAssemblyConflictInfo>? DependencyConflictDetected;

    /// <summary>All assemblies loaded into this context (snapshot-safe).</summary>
    public IReadOnlyList<Assembly> LoadedAssemblies
    {
        get { lock (_lock) { return [.. _loadedAssemblies]; } }
    }

    /// <param name="pluginAssemblyPath">Full path to the plugin's main DLL.</param>
    public PluginLoadContext(string pluginAssemblyPath)
        : base(name: Path.GetFileNameWithoutExtension(pluginAssemblyPath), isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginAssemblyPath);
    }

    /// <summary>
    /// Creates a <see cref="WeakReference{T}"/> to this context for post-unload GC verification.
    /// Call after <see cref="AssemblyLoadContext.Unload"/> to verify the ALC is collected.
    /// </summary>
    public WeakReference<PluginLoadContext> CreateWeakReference()
        => new(this);

    /// <inheritdoc/>
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // If the host (default context) already has this assembly loaded, always use
        // the host's version.  This is the enforcement point for shared assemblies
        // (WpfHexEditor.SDK, WpfHexEditor.Core, WpfHexEditor.HexEditor, …):
        //   - Prevents loading a stale copy from the plugin's output directory.
        //   - Prevents type-identity mismatches ("is IWpfHexEditorPlugin" fails
        //     when the interface is loaded from two different ALCs).
        //   - Guarantees plugins always see the latest SDK surface area even if
        //     their output directory was not updated since the last SDK build.
        var hostAssembly = Default.Assemblies
            .FirstOrDefault(a => a.GetName().Name == assemblyName.Name);
        if (hostAssembly is not null)
        {
            // Detect version conflicts: plugin requested a different version than the host has.
            var hostVersion = hostAssembly.GetName().Version;
            var requestedVersion = assemblyName.Version;
            if (requestedVersion is not null && hostVersion is not null
                && requestedVersion != hostVersion)
            {
                DependencyConflictDetected?.Invoke(new PluginAssemblyConflictInfo(
                    assemblyName.Name ?? assemblyName.FullName,
                    hostVersion,
                    requestedVersion,
                    DateTime.UtcNow));
            }
            return hostAssembly;
        }

        // Plugin-specific assemblies (not present in the host): load from the
        // plugin's own directory so each plugin is properly isolated.
        string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath is not null)
        {
            var asm = LoadFromAssemblyPath(assemblyPath);
            lock (_lock) { _loadedAssemblies.Add(asm); }
            return asm;
        }

        return null;
    }

    /// <inheritdoc/>
    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath is not null)
            return LoadUnmanagedDllFromPath(libraryPath);

        return IntPtr.Zero;
    }
}
