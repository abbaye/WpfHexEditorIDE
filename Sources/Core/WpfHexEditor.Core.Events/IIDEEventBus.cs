// ==========================================================
// Project: WpfHexEditor.Core.Events
// File: IIDEEventBus.cs
// Created: 2026-03-15
// Description:
//     IDE-level event bus interface. Supersedes IPluginEventBus with:
//       - IDE-wide typed events (FileOpened, SelectionChanged, etc.)
//       - Context-aware subscription overloads (IDEEventContext injected into handler)
//       - EventRegistry for diagnostics tooling
//       - Async publish with CancellationToken
//
// Architecture Notes:
//     Defined in WpfHexEditor.Core.Events (no WPF, no SDK dependency).
//     SDK references Events; plugins receive IIDEEventBus via IIDEHostContext.IDEEvents.
//     IPluginEventBus (plugin-to-plugin) is separate and unaffected.
// ==========================================================

namespace WpfHexEditor.Core.Events;

/// <summary>
/// IDE-level event bus for publishing and subscribing to typed IDE events.
/// Accessible to plugins via <c>IIDEHostContext.IDEEvents</c>.
/// </summary>
public interface IIDEEventBus
{
    // -- Core publish/subscribe (same surface as IPluginEventBus for familiarity) --

    /// <summary>Publishes an event synchronously to all current subscribers.</summary>
    void Publish<TEvent>(TEvent evt) where TEvent : class;

    /// <summary>Publishes an event asynchronously, awaiting all async subscribers in parallel.</summary>
    Task PublishAsync<TEvent>(TEvent evt, CancellationToken ct = default) where TEvent : class;

    /// <summary>Subscribes a synchronous handler. Returns a token — dispose to unsubscribe.</summary>
    IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class;

    /// <summary>Subscribes an asynchronous handler. Returns a token — dispose to unsubscribe.</summary>
    IDisposable Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : class;

    // -- Context-aware overloads (carries publisher identity and sandbox flag) --

    /// <summary>
    /// Subscribes a context-aware synchronous handler.
    /// The <see cref="IDEEventContext"/> carries publisher plugin ID and sandbox origin flag.
    /// </summary>
    IDisposable Subscribe<TEvent>(Action<IDEEventContext, TEvent> handler) where TEvent : class;

    /// <summary>
    /// Subscribes a context-aware asynchronous handler.
    /// </summary>
    IDisposable Subscribe<TEvent>(Func<IDEEventContext, TEvent, Task> handler) where TEvent : class;

    // -- Registry --

    /// <summary>Event catalog with subscriber counts and producer labels for diagnostics.</summary>
    IEventRegistry EventRegistry { get; }
}
