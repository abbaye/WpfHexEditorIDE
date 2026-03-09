//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
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
            return hostAssembly;

        // Plugin-specific assemblies (not present in the host): load from the
        // plugin's own directory so each plugin is properly isolated.
        string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath is not null)
            return LoadFromAssemblyPath(assemblyPath);

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
