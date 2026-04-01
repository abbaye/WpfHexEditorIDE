// ==========================================================
// Project: WpfHexEditor.Core.Debugger
// File: Services/IDebugAdapterRegistry.cs
// Description: Registry mapping language IDs to debug adapter factories.
// ==========================================================

using WpfHexEditor.Core.Debugger.Models;

namespace WpfHexEditor.Core.Debugger.Services;

/// <summary>
/// Registry for debug adapters keyed by language ID.
/// Plugins can register custom adapters for non-.NET languages.
/// </summary>
public interface IDebugAdapterRegistry
{
    /// <summary>Register a factory for a given language ID (e.g. "csharp", "python").</summary>
    void Register(string languageId, Func<IDapClient> factory);

    /// <summary>Unregister a previously registered adapter factory.</summary>
    void Unregister(string languageId);

    /// <summary>Create an adapter for the given language. Returns null if not registered.</summary>
    IDapClient? CreateAdapter(string languageId);

    /// <summary>Returns true when an adapter is available for <paramref name="languageId"/>.</summary>
    bool HasAdapter(string languageId);
}

/// <summary>Default registry implementation — thread-safe.</summary>
public sealed class DebugAdapterRegistry : IDebugAdapterRegistry
{
    private readonly Dictionary<string, Func<IDapClient>> _factories = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string languageId, Func<IDapClient> factory)
    {
        lock (_factories) _factories[languageId] = factory;
    }

    public void Unregister(string languageId)
    {
        lock (_factories) _factories.Remove(languageId);
    }

    public IDapClient? CreateAdapter(string languageId)
    {
        lock (_factories)
            return _factories.TryGetValue(languageId, out var f) ? f() : null;
    }

    public bool HasAdapter(string languageId)
    {
        lock (_factories) return _factories.ContainsKey(languageId);
    }
}
