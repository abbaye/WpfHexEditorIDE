// ==========================================================
// Project: WpfHexEditor.PluginDev
// File: Loading/DevPluginHandle.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Wraps a developer-loaded plugin assembly and its CollectibleAssemblyLoadContext
//     so the GC can reclaim the assembly after PluginDevLoader.UnloadPlugin() is called.
//
// Architecture Notes:
//     Pattern: Handle / WeakReference wrapper
//     Uses WeakReference<AssemblyLoadContext> so the GC can collect the ALC
//     after the strong reference (in PluginDevLoader) is set to null.
//     Callers must null out PluginDevLoader.CurrentHandle after each hot-reload.
// ==========================================================

using System.Runtime.Loader;

namespace WpfHexEditor.PluginDev.Loading;

/// <summary>
/// Holds a <see cref="WeakReference{T}"/> to a dev-mode
/// <see cref="AssemblyLoadContext"/> so the runtime can unload it once
/// all plugin objects are released.
/// </summary>
public sealed class DevPluginHandle
{
    private readonly WeakReference<AssemblyLoadContext> _alcRef;

    /// <summary>The full path of the plugin assembly that was loaded.</summary>
    public string AssemblyPath { get; }

    /// <summary>UTC timestamp when this handle was created (used for dev-reload ordering).</summary>
    public DateTime LoadedAt { get; } = DateTime.UtcNow;

    public DevPluginHandle(AssemblyLoadContext alc, string assemblyPath)
    {
        _alcRef      = new WeakReference<AssemblyLoadContext>(alc ?? throw new ArgumentNullException(nameof(alc)));
        AssemblyPath = assemblyPath ?? throw new ArgumentNullException(nameof(assemblyPath));
    }

    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns <c>true</c> once the GC has fully unloaded the assembly.
    /// Poll this after calling <see cref="AssemblyLoadContext.Unload"/> and
    /// setting all plugin references to null.
    /// </summary>
    public bool IsUnloaded => !_alcRef.TryGetTarget(out _);

    /// <summary>
    /// Attempts to retrieve the underlying <see cref="AssemblyLoadContext"/>.
    /// Returns <c>null</c> after the GC has collected it.
    /// </summary>
    public AssemblyLoadContext? TryGetAlc()
    {
        _alcRef.TryGetTarget(out var alc);
        return alc;
    }

    /// <summary>
    /// Waits (with polling) until <see cref="IsUnloaded"/> is <c>true</c> or
    /// the timeout expires. Forces GC collections to help reclaim the ALC.
    /// </summary>
    public async Task WaitForUnloadAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (!IsUnloaded && DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Delay(100, ct);
        }
    }
}
