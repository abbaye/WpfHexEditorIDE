// ==========================================================
// Project: WpfHexEditor.SDK
// File: IPluginEventBus.cs
// Author: Auto
// Created: 2026-03-06
// Description:
//     Central event bus for decoupled communication between plugins.
//     Plugins publish and subscribe to typed events without direct references.
//
// Architecture Notes:
//     Pattern: Publish/Subscribe with weak references to prevent memory leaks
//     when plugins are unloaded. IDisposable subscriptions auto-clean on unload.
//     Async dispatch supported for non-blocking UI plugins.
//     Example: DataInspectorPlugin publishes ByteMetricsEvent; FileStatisticsPlugin subscribes.
//
// ==========================================================

namespace WpfHexEditor.SDK.Contracts;

/// <summary>
/// Central event bus for decoupled plugin-to-plugin communication.
/// </summary>
public interface IPluginEventBus
{
    /// <summary>
    /// Publishes an event to all current subscribers of type <typeparamref name="TEvent"/>.
    /// Dispatches synchronously on the calling thread.
    /// </summary>
    /// <typeparam name="TEvent">Event payload type.</typeparam>
    /// <param name="evt">The event instance to broadcast.</param>
    void Publish<TEvent>(TEvent evt) where TEvent : class;

    /// <summary>
    /// Publishes an event asynchronously, awaiting all async subscriber handlers.
    /// </summary>
    /// <typeparam name="TEvent">Event payload type.</typeparam>
    /// <param name="evt">The event instance to broadcast.</param>
    /// <param name="ct">Cancellation token.</param>
    Task PublishAsync<TEvent>(TEvent evt, CancellationToken ct = default) where TEvent : class;

    /// <summary>
    /// Subscribes to events of type <typeparamref name="TEvent"/>.
    /// Dispose the returned <see cref="IDisposable"/> to unsubscribe (automatic on plugin unload).
    /// </summary>
    /// <typeparam name="TEvent">Event payload type.</typeparam>
    /// <param name="handler">Handler invoked when an event of this type is published.</param>
    /// <returns>A disposable token that unsubscribes when disposed.</returns>
    IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class;

    /// <summary>
    /// Subscribes to events of type <typeparamref name="TEvent"/> with an async handler.
    /// </summary>
    IDisposable Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : class;
}
