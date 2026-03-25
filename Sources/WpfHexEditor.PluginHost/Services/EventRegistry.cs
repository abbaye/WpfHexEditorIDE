// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: Services/EventRegistry.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-15
// Description:
//     Lock-based implementation of IEventRegistry for tracking
//     IDE event registrations and subscriber counts.
//     Used by IDEEventBusOptionsPage to display the event log.
// ==========================================================

using WpfHexEditor.Core.Events;

namespace WpfHexEditor.PluginHost.Services;

/// <summary>
/// Thread-safe implementation of <see cref="IEventRegistry"/>.
/// </summary>
internal sealed class EventRegistry : IEventRegistry
{
    private readonly object _lock = new();
    private readonly Dictionary<Type, EventRegistryEntry> _entries = [];

    /// <inheritdoc />
    public void Register(Type eventType, string displayName, string producerLabel)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        lock (_lock)
        {
            if (!_entries.ContainsKey(eventType))
                _entries[eventType] = new EventRegistryEntry(eventType, displayName, producerLabel, 0);
        }
    }

    /// <inheritdoc />
    public void UpdateSubscriberCount(Type eventType, int delta)
    {
        lock (_lock)
        {
            if (_entries.TryGetValue(eventType, out var entry))
                _entries[eventType] = entry with { SubscriberCount = Math.Max(0, entry.SubscriberCount + delta) };
        }
    }

    /// <inheritdoc />
    public int GetSubscriberCount(Type eventType)
    {
        lock (_lock)
        {
            return _entries.TryGetValue(eventType, out var e) ? e.SubscriberCount : 0;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<EventRegistryEntry> GetAllEntries()
    {
        lock (_lock) { return [.. _entries.Values]; }
    }
}
