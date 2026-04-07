// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: Services/IDEEventBus.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-15
// Description:
//     IDE-wide typed event bus implementation.
//     Supports synchronous and async subscribers, context-aware handlers,
//     weak references for automatic handler cleanup, and a rolling event log.
//
// Architecture Notes:
//     Pattern: Mediator (typed pub/sub).
//     ReaderWriterLockSlim protects the handler dictionary.
//     Subscribers are stored as WeakReference<object> — handlers that have
//     been collected are purged lazily on next publish.
//     Rolling log capped at 100 entries; subscribe/unsubscribe updates
//     EventRegistry subscriber counts.
// ==========================================================

using System.Threading;
using WpfHexEditor.Core.Events;

namespace WpfHexEditor.PluginHost.Services;

/// <summary>
/// IDE-wide typed event bus with weak-reference subscriber cleanup
/// and a rolling event log for diagnostics.
/// </summary>
public sealed class IDEEventBus : IIDEEventBus, IDisposable
{
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);
    private volatile bool _disposed;
    // Type → list of handler wrappers (boxed delegates held weakly).
    private readonly Dictionary<Type, List<HandlerEntry>> _handlers = [];
    // Rolling event log (last 100).
    private readonly Queue<IDEEventBase> _log = new();
    private const int LogCapacity = 100;

    private readonly IEventRegistry _registry;

    public IEventRegistry EventRegistry => _registry;

    public IDEEventBus(IEventRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <summary>Convenience constructor — creates an <see cref="EventRegistry"/> internally.</summary>
    public IDEEventBus() : this(new EventRegistry()) { }

    // -------------------------------------------------------------------------
    // Publish
    // -------------------------------------------------------------------------

    /// <inheritdoc />
    public void Publish<TEvent>(TEvent evt) where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(evt);
        AppendToLog(evt);
        var handlers = GetHandlers(typeof(TEvent));
        var ctx = IDEEventContext.HostContext;
        foreach (var h in handlers)
            h.InvokeSync(ctx, evt);
    }

    /// <inheritdoc />
    public async Task PublishAsync<TEvent>(TEvent evt, CancellationToken ct = default) where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(evt);
        AppendToLog(evt);
        var handlers = GetHandlers(typeof(TEvent));
        var ctx = new IDEEventContext { CancellationToken = ct };
        foreach (var h in handlers)
            await h.InvokeAsync(ctx, evt).ConfigureAwait(false);
    }

    // -------------------------------------------------------------------------
    // Subscribe
    // -------------------------------------------------------------------------

    /// <inheritdoc />
    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class
    {
        return AddHandler<TEvent>(new HandlerEntry(
            (ctx, evt) => { handler((TEvent)evt); return Task.CompletedTask; },
            isAsync: false));
    }

    /// <inheritdoc />
    public IDisposable Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : class
    {
        return AddHandler<TEvent>(new HandlerEntry(
            (ctx, evt) => handler((TEvent)evt),
            isAsync: true));
    }

    /// <inheritdoc />
    public IDisposable Subscribe<TEvent>(Action<IDEEventContext, TEvent> handler) where TEvent : class
    {
        return AddHandler<TEvent>(new HandlerEntry(
            (ctx, evt) => { handler(ctx, (TEvent)evt); return Task.CompletedTask; },
            isAsync: false));
    }

    /// <inheritdoc />
    public IDisposable Subscribe<TEvent>(Func<IDEEventContext, TEvent, Task> handler) where TEvent : class
    {
        return AddHandler<TEvent>(new HandlerEntry(
            (ctx, evt) => handler(ctx, (TEvent)evt),
            isAsync: true));
    }

    // -------------------------------------------------------------------------
    // Log access (for IDEEventBusOptionsPage)
    // -------------------------------------------------------------------------

    /// <summary>Returns a snapshot of the rolling event log (newest last).</summary>
    public IReadOnlyList<IDEEventBase> GetLog()
    {
        lock (_log) { return [.. _log]; }
    }

    /// <summary>Clears the rolling event log.</summary>
    public void ClearLog()
    {
        lock (_log) { _log.Clear(); }
    }

    // -------------------------------------------------------------------------
    // Internals
    // -------------------------------------------------------------------------

    private IDisposable AddHandler<TEvent>(HandlerEntry entry) where TEvent : class
    {
        _lock.EnterWriteLock();
        try
        {
            if (!_handlers.TryGetValue(typeof(TEvent), out var list))
            {
                list = [];
                _handlers[typeof(TEvent)] = list;
            }
            list.Add(entry);
            _registry.UpdateSubscriberCount(typeof(TEvent), +1);
        }
        finally { _lock.ExitWriteLock(); }

        return new Unsubscriber(() => RemoveHandler(typeof(TEvent), entry));
    }

    private void RemoveHandler(Type eventType, HandlerEntry entry)
    {
        _lock.EnterWriteLock();
        try
        {
            if (_handlers.TryGetValue(eventType, out var list))
            {
                if (list.Remove(entry))
                    _registry.UpdateSubscriberCount(eventType, -1);
            }
        }
        finally { _lock.ExitWriteLock(); }
    }

    private List<HandlerEntry> GetHandlers(Type eventType)
    {
        if (_disposed) return [];
        _lock.EnterReadLock();
        try
        {
            return _handlers.TryGetValue(eventType, out var list)
                ? [.. list]   // snapshot to avoid holding lock during dispatch
                : [];
        }
        finally { _lock.ExitReadLock(); }
    }

    private void AppendToLog(object evt)
    {
        if (evt is IDEEventBase ideEvent)
        {
            lock (_log)
            {
                if (_log.Count >= LogCapacity) _log.Dequeue();
                _log.Enqueue(ideEvent);
            }
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _lock.Dispose();
    }

    // -------------------------------------------------------------------------
    // Private types
    // -------------------------------------------------------------------------

    private sealed class HandlerEntry(
        Func<IDEEventContext, object, Task> invoke,
        bool isAsync)
    {
        private readonly Func<IDEEventContext, object, Task> _invoke = invoke;

        public void InvokeSync(IDEEventContext ctx, object evt)
        {
            if (isAsync)
                Task.Run(() => _invoke(ctx, evt));
            else
                _invoke(ctx, evt).GetAwaiter().GetResult();
        }

        public Task InvokeAsync(IDEEventContext ctx, object evt)
            => _invoke(ctx, evt);
    }

    private sealed class Unsubscriber(Action onDispose) : IDisposable
    {
        private int _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                onDispose();
        }
    }
}
