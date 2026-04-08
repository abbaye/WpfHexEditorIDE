// ==========================================================
// Project: WpfHexEditor.PluginHost.HotReload.Tests
// File: PluginDevLoaderTests.cs
// Description:
//     Unit tests for PluginDevLoader hot-reload engine and
//     WpfPluginHost Watch Mode public API.
// ==========================================================

using WpfHexEditor.PluginHost.DevTools;
using WpfHexEditor.Core.Events.IDEEvents;

namespace WpfHexEditor.PluginHost.HotReload.Tests;

[TestClass]
public sealed class PluginDevLoaderTests
{
    // ── PluginDevLoader — API surface ─────────────────────────────────────────

    [TestMethod]
    public void Watch_ValidDirectory_DoesNotThrow()
    {
        var dir = CreateTempDir();
        try
        {
            using var loader = new PluginDevLoader(null!, null!, null!);
            loader.Watch("test-plugin", dir);
            loader.StopWatching("test-plugin");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [TestMethod]
    public void StopWatching_UnknownPlugin_DoesNotThrow()
    {
        using var loader = new PluginDevLoader(null!, null!, null!);
        loader.StopWatching("nonexistent-plugin");
    }

    [TestMethod]
    public void Watch_SamePluginTwice_ReplacesWatcher()
    {
        var dir = CreateTempDir();
        try
        {
            using var loader = new PluginDevLoader(null!, null!, null!);
            loader.Watch("dup-plugin", dir);
            loader.Watch("dup-plugin", dir); // should replace
            loader.StopWatching("dup-plugin");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [TestMethod]
    public void Watch_MultiplePlugins_AllTracked()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        try
        {
            using var loader = new PluginDevLoader(null!, null!, null!);
            loader.Watch("plugin-a", dir1);
            loader.Watch("plugin-b", dir2);
            loader.StopWatching("plugin-a");
            loader.StopWatching("plugin-b");
        }
        finally
        {
            Directory.Delete(dir1, recursive: true);
            Directory.Delete(dir2, recursive: true);
        }
    }

    [TestMethod]
    public void StopAll_WithNoWatchers_DoesNotThrow()
    {
        using var loader = new PluginDevLoader(null!, null!, null!);
        loader.StopAll();
    }

    [TestMethod]
    public void StopAll_WithWatchers_StopsAll()
    {
        var dir = CreateTempDir();
        try
        {
            using var loader = new PluginDevLoader(null!, null!, null!);
            loader.Watch("p1", dir);
            loader.Watch("p2", dir);
            loader.StopAll(); // should not throw
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [TestMethod]
    public void ReloadCompleted_CanSubscribeAndUnsubscribe()
    {
        using var loader = new PluginDevLoader(null!, null!, null!);
        EventHandler<PluginDevReloadEventArgs>? h = (_, _) => { };
        loader.ReloadCompleted += h;
        loader.ReloadCompleted -= h;
    }

    [TestMethod]
    public void ReloadFailed_CanSubscribeAndUnsubscribe()
    {
        using var loader = new PluginDevLoader(null!, null!, null!);
        EventHandler<PluginDevReloadFailedEventArgs>? h = (_, _) => { };
        loader.ReloadFailed += h;
        loader.ReloadFailed -= h;
    }

    [TestMethod]
    public async Task Watch_FileChange_DoesNotFireBeforeDebounce()
    {
        var dir = CreateTempDir();
        int fired = 0;
        try
        {
            using var loader = new PluginDevLoader(null!, null!, null!);
            loader.ReloadCompleted += (_, _) => Interlocked.Increment(ref fired);
            loader.Watch("debounce-test", dir);

            File.WriteAllText(Path.Combine(dir, "plugin.dll"), "dummy");

            // Wait 100ms — debounce is 500ms, so reload should not have run yet
            await Task.Delay(100);
            Assert.AreEqual(0, fired, "Reload fired before debounce window.");

            loader.StopWatching("debounce-test");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [TestMethod]
    public void Dispose_WithActiveWatchers_DoesNotThrow()
    {
        var dir = CreateTempDir();
        try
        {
            var loader = new PluginDevLoader(null!, null!, null!);
            loader.Watch("p", dir);
            loader.Dispose();
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ── PluginHotReloadedEvent record ─────────────────────────────────────────

    [TestMethod]
    public void PluginHotReloadedEvent_DefaultValues()
    {
        var e = new PluginHotReloadedEvent();
        Assert.AreEqual(string.Empty, e.PluginId);
        Assert.AreEqual(string.Empty, e.OldVersion);
        Assert.AreEqual(string.Empty, e.NewVersion);
    }

    [TestMethod]
    public void PluginHotReloadedEvent_WithValues_RoundTrips()
    {
        var e = new PluginHotReloadedEvent
        {
            PluginId   = "pid",
            PluginName = "My Plugin",
            OldVersion = "1.0.0",
            NewVersion = "1.0.1",
        };
        Assert.AreEqual("pid",   e.PluginId);
        Assert.AreEqual("1.0.1", e.NewVersion);
    }

    [TestMethod]
    public void PluginHotReloadFailedEvent_DefaultValues()
    {
        var e = new PluginHotReloadFailedEvent();
        Assert.AreEqual(string.Empty, e.PluginId);
        Assert.AreEqual(string.Empty, e.Error);
    }

    [TestMethod]
    public void PluginHotReloadFailedEvent_WithValues_RoundTrips()
    {
        var e = new PluginHotReloadFailedEvent
        {
            PluginId = "fail-plugin",
            Error    = "File locked.",
        };
        Assert.AreEqual("fail-plugin", e.PluginId);
        Assert.AreEqual("File locked.", e.Error);
    }

    // ── PluginDevReloadEventArgs ───────────────────────────────────────────────

    [TestMethod]
    public void PluginDevReloadEventArgs_StoresPluginId()
    {
        var args = new PluginDevReloadEventArgs("my-plugin");
        Assert.AreEqual("my-plugin", args.PluginId);
    }

    [TestMethod]
    public void PluginDevReloadFailedEventArgs_StoresPluginIdAndException()
    {
        var ex   = new InvalidOperationException("test error");
        var args = new PluginDevReloadFailedEventArgs("fail-plugin", ex);
        Assert.AreEqual("fail-plugin", args.PluginId);
        Assert.AreSame(ex, args.Exception);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        return dir;
    }

}
