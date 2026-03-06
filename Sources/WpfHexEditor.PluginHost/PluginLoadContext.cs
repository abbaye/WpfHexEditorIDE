// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: PluginLoadContext.cs
// Author: Auto
// Created: 2026-03-06
// Description:
//     Isolated AssemblyLoadContext for each InProcess plugin.
//     Enables hot-unload by unloading the context when the plugin is removed.
//
// Architecture Notes:
//     isCollectible: true — required for GC-based unload.
//     Probe order: plugin's own directory first, then default resolver.
//     No shared assemblies (WpfHexEditor.SDK, WpfHexEditor.Core) are copied
//     to the plugin directory — they resolve via the default (host) context.
//
// ==========================================================

using System.Reflection;
using System.Runtime.Loader;

namespace WpfHexEditor.PluginHost;

/// <summary>
/// Isolated <see cref="AssemblyLoadContext"/> for a single InProcess plugin.
/// Supports hot-unload via <see cref="AssemblyLoadContext.Unload"/>.
/// </summary>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    /// <param name="pluginAssemblyPath">Full path to the plugin's main DLL.</param>
    public PluginLoadContext(string pluginAssemblyPath)
        : base(name: Path.GetFileNameWithoutExtension(pluginAssemblyPath), isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginAssemblyPath);
    }

    /// <inheritdoc/>
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Try to resolve from the plugin's own directory first.
        string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath is not null)
            return LoadFromAssemblyPath(assemblyPath);

        // Fall back to the host (default) context for shared assemblies.
        // This ensures WpfHexEditor.SDK, WpfHexEditor.Core etc. are shared,
        // preventing type identity mismatches across contexts.
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
