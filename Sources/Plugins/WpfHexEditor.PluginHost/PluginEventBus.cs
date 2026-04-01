//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows;
using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.PluginHost;

/// <summary>
/// Thread-safe publish/subscribe event bus for plugin-to-plugin communication.
/// </summary>
public sealed class PluginEventBus : IPluginEventBus
{
    private readonly Dictionary<Type, List<WeakHandlerWrapper>> _handlers = new();
    private readonly object _lock = new();

    // -- IPluginEventBus -------------------------------------------------------

    public void Publish<TEvent>(TEvent evt) where TEvent : class
    {
        List<WeakHandlerWrapper> snapshot;
        lock (_lock)
        {
            if (!_handlers.TryGetValue(typeof(TEvent), out var list)) return;
            snapshot = [.. list];
        }

        foreach (var wrapper in snapshot)
            wrapper.Invoke(evt);

        PurgeDeadHandlers(typeof(TEvent));
    }

    public async Task PublishAsync<TEvent>(TEvent evt, CancellationToken ct = default) where TEvent : class
    {
        List<WeakHandlerWrapper> snapshot;
        lock (_lock)
        {
            if (!_handlers.TryGetValue(typeof(TEvent), out var list)) return;
            snapshot = [.. list];
        }

        foreach (var wrapper in snapshot)
            await wrapper.InvokeAsync(evt, ct).ConfigureAwait(false);

        PurgeDeadHandlers(typeof(TEvent));
    }

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class
    {
        var wrapper = new WeakHandlerWrapper<TEvent>(handler, isAsync: false);
        AddHandler(typeof(TEvent), wrapper);
        return new Subscription(() => RemoveHandler(typeof(TEvent), wrapper));
    }

    public IDisposable Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : class
    {
        var wrapper = new WeakHandlerWrapper<TEvent>(handler);
        AddHandler(typeof(TEvent), wrapper);
        return new Subscription(() => RemoveHandler(typeof(TEvent), wrapper));
    }

    // -- Internal helpers ------------------------------------------------------

    private void AddHandler(Type eventType, WeakHandlerWrapper wrapper)
    {
        lock (_lock)
        {
            if (!_handlers.TryGetValue(eventType, out var list))
                _handlers[eventType] = list = [];
            list.Add(wrapper);
        }
    }

    private void RemoveHandler(Type eventType, WeakHandlerWrapper wrapper)
    {
        lock (_lock)
        {
            if (_handlers.TryGetValue(eventType, out var list))
                list.Remove(wrapper);
        }
    }

    private void PurgeDeadHandlers(Type eventType)
    {
        lock (_lock)
        {
            if (_handlers.TryGetValue(eventType, out var list))
                list.RemoveAll(w => !w.IsAlive);
        }
    }

    // -- Nested types ----------------------------------------------------------

    private abstract class WeakHandlerWrapper
    {
        public abstract bool IsAlive { get; }
        public abstract void Invoke(object evt);
        public abstract Task InvokeAsync(object evt, CancellationToken ct);
    }

    private sealed class WeakHandlerWrapper<TEvent> : WeakHandlerWrapper where TEvent : class
    {
        // Strong reference to the delegate — prevents GC from collecting method group
        // delegates allocated by Subscribe() before Publish() is called.
        // Cleanup is handled by IDisposable.Dispose() on the Subscription token.
        private Delegate? _strongRef;
        private readonly bool _isAsync;

        public WeakHandlerWrapper(Action<TEvent> handler, bool isAsync)
        {
            _strongRef = handler;
            _isAsync = false;
        }

        public WeakHandlerWrapper(Func<TEvent, Task> handler)
        {
            _strongRef = handler;
            _isAsync = true;
        }

        public override bool IsAlive => _strongRef is not null;

        public override void Invoke(object evt)
        {
            if (!_isAsync && _strongRef is Action<TEvent> action)
                action((TEvent)evt);
        }

        public override async Task InvokeAsync(object evt, CancellationToken ct)
        {
            if (_isAsync && _strongRef is Func<TEvent, Task> func)
                await func((TEvent)evt).ConfigureAwait(false);
            else
                Invoke(evt);
        }

        /// <summary>Release the strong reference (called when Subscription is disposed).</summary>
        public void Release() => _strongRef = null;
    }

    private sealed class Subscription(Action onDispose) : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            onDispose();
        }
    }
}
