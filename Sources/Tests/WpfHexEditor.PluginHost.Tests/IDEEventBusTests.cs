// ==========================================================
// Project: WpfHexEditor.PluginHost.Tests
// File: IDEEventBusTests.cs
// Contributors: Claude Sonnet 4.6
// Description:
//     Integration tests for IDEEventBus — typed publish/subscribe,
//     unsubscribe, async dispatch, log rolling, thread safety.
// ==========================================================

using System.Collections.Concurrent;
using WpfHexEditor.Core.Events;
using WpfHexEditor.PluginHost.Services;

namespace WpfHexEditor.PluginHost.Tests;

// ── Concrete test events ──────────────────────────────────────────────────────

file sealed record PingEvent(string Message) : IDEEventBase;
file sealed record PongEvent(int Value) : IDEEventBase;
file sealed record PlainEvent(string Tag); // NOT an IDEEventBase — plain class

// ── Tests ─────────────────────────────────────────────────────────────────────

[TestClass]
public sealed class IDEEventBusTests
{
    private IDEEventBus _bus = null!;

    [TestInitialize]
    public void Setup() => _bus = new IDEEventBus();

    [TestCleanup]
    public void Teardown() => _bus.Dispose();

    // ── Basic Subscribe / Publish ─────────────────────────────────────────────

    [TestMethod]
    public void Publish_SyncSubscriber_ReceivesEvent()
    {
        PingEvent? received = null;
        _bus.Subscribe<PingEvent>(e => received = e);

        _bus.Publish(new PingEvent("hello"));

        Assert.IsNotNull(received);
        Assert.AreEqual("hello", received.Message);
    }

    [TestMethod]
    public void Publish_MultipleSubscribers_AllReceive()
    {
        var count = 0;
        _bus.Subscribe<PingEvent>(_ => count++);
        _bus.Subscribe<PingEvent>(_ => count++);
        _bus.Subscribe<PingEvent>(_ => count++);

        _bus.Publish(new PingEvent("x"));

        Assert.AreEqual(3, count);
    }

    [TestMethod]
    public void Publish_TypeIsolation_OtherTypeNotDelivered()
    {
        var pingReceived = false;
        var pongReceived = false;

        _bus.Subscribe<PingEvent>(_ => pingReceived = true);
        _bus.Subscribe<PongEvent>(_ => pongReceived = true);

        _bus.Publish(new PingEvent("only ping"));

        Assert.IsTrue(pingReceived);
        Assert.IsFalse(pongReceived);
    }

    [TestMethod]
    public void Publish_NoSubscribers_DoesNotThrow()
    {
        // Should be a no-op, not an exception
        _bus.Publish(new PingEvent("nobody listening"));
    }

    [TestMethod]
    public void Publish_NullEvent_ThrowsArgumentNullException()
    {
        try
        {
            _bus.Publish<PingEvent>(null!);
            Assert.Fail("Expected ArgumentNullException was not thrown.");
        }
        catch (ArgumentNullException) { /* expected */ }
    }

    // ── Plain class events (non-IDEEventBase) ─────────────────────────────────

    [TestMethod]
    public void Publish_PlainClassEvent_DeliveredCorrectly()
    {
        PlainEvent? received = null;
        _bus.Subscribe<PlainEvent>(e => received = e);

        _bus.Publish(new PlainEvent("plain"));

        Assert.IsNotNull(received);
        Assert.AreEqual("plain", received.Tag);
    }

    // ── Unsubscribe ───────────────────────────────────────────────────────────

    [TestMethod]
    public void Unsubscribe_AfterDispose_NoLongerReceives()
    {
        var count = 0;
        var sub = _bus.Subscribe<PingEvent>(_ => count++);

        _bus.Publish(new PingEvent("before"));
        Assert.AreEqual(1, count);

        sub.Dispose();
        _bus.Publish(new PingEvent("after"));
        Assert.AreEqual(1, count); // not incremented
    }

    [TestMethod]
    public void Unsubscribe_Idempotent_DoubleDisposeIsSafe()
    {
        var sub = _bus.Subscribe<PingEvent>(_ => { });
        sub.Dispose();
        sub.Dispose(); // must not throw
    }

    [TestMethod]
    public void Unsubscribe_OneOfMany_OthersStillReceive()
    {
        var count = 0;
        var subA = _bus.Subscribe<PingEvent>(_ => count++);
        _bus.Subscribe<PingEvent>(_ => count++);

        subA.Dispose();
        _bus.Publish(new PingEvent("x"));

        Assert.AreEqual(1, count); // only one subscriber left
    }

    // ── Context-aware subscribe ───────────────────────────────────────────────

    [TestMethod]
    public void Subscribe_ContextOverload_ContextIsProvided()
    {
        IDEEventContext? capturedCtx = null;
        _bus.Subscribe<PingEvent>((ctx, _) => capturedCtx = ctx);

        _bus.Publish(new PingEvent("ctx-test"));

        Assert.IsNotNull(capturedCtx);
    }

    // ── Async publish ─────────────────────────────────────────────────────────

    [TestMethod]
    public async Task PublishAsync_AsyncSubscriber_ReceivesEvent()
    {
        PingEvent? received = null;
        _bus.Subscribe<PingEvent>(async e =>
        {
            await Task.Delay(1);
            received = e;
        });

        await _bus.PublishAsync(new PingEvent("async-hello"));

        Assert.IsNotNull(received);
        Assert.AreEqual("async-hello", received.Message);
    }

    [TestMethod]
    public async Task PublishAsync_NullEvent_ThrowsArgumentNullException()
    {
        try
        {
            await _bus.PublishAsync<PingEvent>(null!);
            Assert.Fail("Expected ArgumentNullException was not thrown.");
        }
        catch (ArgumentNullException) { /* expected */ }
    }

    [TestMethod]
    public async Task PublishAsync_ContextOverload_CancellationTokenPropagated()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken captured = default;

        _bus.Subscribe<PingEvent>(async (ctx, _) =>
        {
            captured = ctx.CancellationToken;
            await Task.CompletedTask;
        });

        await _bus.PublishAsync(new PingEvent("ct"), cts.Token);

        Assert.AreEqual(cts.Token, captured);
    }

    // ── Log rolling ───────────────────────────────────────────────────────────

    [TestMethod]
    public void GetLog_AfterPublish_ContainsIDEEventBase()
    {
        var evt = new PingEvent("logged") { Source = "test" };
        _bus.Publish(evt);

        var log = _bus.GetLog();

        Assert.AreEqual(1, log.Count);
        Assert.IsInstanceOfType<PingEvent>(log[0]);
    }

    [TestMethod]
    public void GetLog_PlainClassEvent_NotLogged()
    {
        _bus.Publish(new PlainEvent("not-loggable"));
        Assert.AreEqual(0, _bus.GetLog().Count);
    }

    [TestMethod]
    public void GetLog_Rolling_CapsAt100()
    {
        for (int i = 0; i < 120; i++)
            _bus.Publish(new PingEvent($"msg-{i}"));

        Assert.AreEqual(100, _bus.GetLog().Count);
    }

    [TestMethod]
    public void ClearLog_RemovesAllEntries()
    {
        _bus.Publish(new PingEvent("a"));
        _bus.Publish(new PingEvent("b"));

        _bus.ClearLog();

        Assert.AreEqual(0, _bus.GetLog().Count);
    }

    [TestMethod]
    public void GetLog_ReturnsSnapshot_DoesNotMutateOnSubsequentPublish()
    {
        _bus.Publish(new PingEvent("before-snap"));
        var snapshot = _bus.GetLog();

        _bus.Publish(new PingEvent("after-snap"));

        Assert.AreEqual(1, snapshot.Count);  // snapshot is frozen
    }

    // ── EventRegistry ─────────────────────────────────────────────────────────

    [TestMethod]
    public void EventRegistry_UnregisteredType_SubscriberCountIsZero()
    {
        // Types that were never Register()-ed return 0
        Assert.AreEqual(0, _bus.EventRegistry.GetSubscriberCount(typeof(PongEvent)));
    }

    [TestMethod]
    public void EventRegistry_RegisterThenSubscribe_CountIncreases()
    {
        // Register the type first so UpdateSubscriberCount has an entry to update
        _bus.EventRegistry.Register(typeof(PingEvent), "PingEvent", "test");
        var before = _bus.EventRegistry.GetSubscriberCount(typeof(PingEvent));

        _bus.Subscribe<PingEvent>(_ => { });

        Assert.AreEqual(before + 1, _bus.EventRegistry.GetSubscriberCount(typeof(PingEvent)));
    }

    // ── Thread safety ─────────────────────────────────────────────────────────

    [TestMethod]
    public void Publish_ConcurrentPublishers_AllDelivered()
    {
        var bag = new ConcurrentBag<int>();
        _bus.Subscribe<PongEvent>(e => bag.Add(e.Value));

        const int count = 100;
        Parallel.For(0, count, i => _bus.Publish(new PongEvent(i)));

        Assert.AreEqual(count, bag.Count);
    }

    [TestMethod]
    public void Subscribe_MultipleHandlers_AllFireOnPublish()
    {
        const int n = 50;
        var subs = new IDisposable[n];
        var counter = 0;

        for (int i = 0; i < n; i++)
            subs[i] = _bus.Subscribe<PingEvent>(_ => Interlocked.Increment(ref counter));

        _bus.Subscribe<PingEvent>(_ => Interlocked.Increment(ref counter)); // n+1 total

        _bus.Publish(new PingEvent("x"));

        Assert.AreEqual(n + 1, counter);
        foreach (var s in subs) s.Dispose();
    }
}
