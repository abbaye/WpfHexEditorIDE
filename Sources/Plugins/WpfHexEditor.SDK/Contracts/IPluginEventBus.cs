//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

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
