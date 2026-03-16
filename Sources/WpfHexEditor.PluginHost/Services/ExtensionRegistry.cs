// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: Services/ExtensionRegistry.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-15
// Description:
//     Thread-safe implementation of IExtensionRegistry backed by a
//     type-keyed dictionary of plugin contributions.
//     GetExtensions<T>() returns a snapshot — safe for iteration while
//     plugins are concurrently loaded or unloaded.
//
// Architecture Notes:
//     Pattern: Registry (Fowler PEAA).
//     Uses ReaderWriterLockSlim for low-contention reads.
// ==========================================================

using System.Threading;
using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.PluginHost.Services;

/// <summary>
/// Thread-safe registry of plugin extension-point contributions.
/// </summary>
public sealed class ExtensionRegistry : IExtensionRegistry, IDisposable
{
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);

    // Type → list of (pluginId, implementation) pairs.
    private readonly Dictionary<Type, List<(string PluginId, object Impl)>> _buckets = [];

    // Flat list of entries for GetAllEntries().
    private readonly List<ExtensionRegistryEntry> _entries = [];

    /// <inheritdoc />
    public IReadOnlyList<T> GetExtensions<T>() where T : class
    {
        _lock.EnterReadLock();
        try
        {
            if (!_buckets.TryGetValue(typeof(T), out var bucket))
                return [];
            return bucket.Select(x => (T)x.Impl).ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <inheritdoc />
    public void Register<T>(string pluginId, T implementation) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(implementation);

        _lock.EnterWriteLock();
        try
        {
            if (!_buckets.TryGetValue(typeof(T), out var bucket))
            {
                bucket = [];
                _buckets[typeof(T)] = bucket;
            }
            bucket.Add((pluginId, implementation));
            _entries.Add(new ExtensionRegistryEntry(pluginId, typeof(T).Name, typeof(T)));
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <inheritdoc />
    public void Register(string pluginId, Type contractType, object implementation)
        => RegisterByType(pluginId, contractType, implementation);

    /// <summary>
    /// Registers an extension by contract type and implementation (internal helper).
    /// </summary>
    private void RegisterByType(string pluginId, Type contractType, object implementation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(contractType);
        ArgumentNullException.ThrowIfNull(implementation);

        _lock.EnterWriteLock();
        try
        {
            if (!_buckets.TryGetValue(contractType, out var bucket))
            {
                bucket = [];
                _buckets[contractType] = bucket;
            }
            bucket.Add((pluginId, implementation));
            _entries.Add(new ExtensionRegistryEntry(pluginId, contractType.Name, contractType));
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <inheritdoc />
    public void UnregisterAll(string pluginId)
    {
        _lock.EnterWriteLock();
        try
        {
            foreach (var bucket in _buckets.Values)
                bucket.RemoveAll(x => string.Equals(x.PluginId, pluginId, StringComparison.OrdinalIgnoreCase));

            _entries.RemoveAll(e => string.Equals(e.PluginId, pluginId, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ExtensionRegistryEntry> GetAllEntries()
    {
        _lock.EnterReadLock();
        try { return [.. _entries]; }
        finally { _lock.ExitReadLock(); }
    }

    public void Dispose() => _lock.Dispose();
}
