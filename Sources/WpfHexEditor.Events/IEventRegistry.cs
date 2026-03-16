// ==========================================================
// Project: WpfHexEditor.Events
// File: IEventRegistry.cs
// Created: 2026-03-15
// Description:
//     Catalog of event types registered on the IDE event bus.
//     Provides subscriber counts and producer labels for diagnostics tooling.
// ==========================================================

namespace WpfHexEditor.Events;

/// <summary>
/// Catalog of IDE event types with subscriber counts and producer labels.
/// Exposed via <see cref="IIDEEventBus.EventRegistry"/> for diagnostics UI.
/// </summary>
public interface IEventRegistry
{
    /// <summary>Returns all registered event type entries with current subscriber counts.</summary>
    IReadOnlyList<EventRegistryEntry> GetAllEntries();

    /// <summary>Returns the current number of live subscribers for the given event type.</summary>
    int GetSubscriberCount(Type eventType);

    /// <summary>
    /// Registers an event type in the catalog.
    /// Called at IDE startup for well-known events; plugins may call it for their own types.
    /// </summary>
    void Register(Type eventType, string displayName, string producerLabel);

    /// <summary>Increments or decrements subscriber count when handlers are added/removed.</summary>
    void UpdateSubscriberCount(Type eventType, int delta);
}

/// <summary>A snapshot entry from the event registry.</summary>
public sealed record EventRegistryEntry(
    Type EventType,
    string DisplayName,
    string ProducerLabel,
    int SubscriberCount);
