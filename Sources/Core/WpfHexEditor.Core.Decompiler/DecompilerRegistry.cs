// ==========================================================
// Project: WpfHexEditor.Core.Decompiler
// File: DecompilerRegistry.cs
// Description:
//     Global registry of available IDecompiler implementations.
//     Plugins call Register() during startup to contribute backends.
// ==========================================================

namespace WpfHexEditor.Core.Decompiler;

/// <summary>
/// Thread-safe registry of all available <see cref="IDecompiler"/> backends.
/// </summary>
public static class DecompilerRegistry
{
    private static readonly List<IDecompiler> _all = [];
    private static readonly object _lock = new();

    /// <summary>All registered decompilers (snapshot; safe to enumerate from any thread).</summary>
    public static IReadOnlyList<IDecompiler> All
    {
        get { lock (_lock) return _all.ToArray(); }
    }

    /// <summary>Registers a decompiler backend. No-op if already registered (by reference).</summary>
    public static void Register(IDecompiler decompiler)
    {
        ArgumentNullException.ThrowIfNull(decompiler);
        lock (_lock)
        {
            if (!_all.Contains(decompiler))
                _all.Add(decompiler);
        }
    }

    /// <summary>Unregisters a decompiler backend.</summary>
    public static void Unregister(IDecompiler decompiler)
    {
        lock (_lock) _all.Remove(decompiler);
    }
}
