// ==========================================================
// Project: WpfHexEditor.PluginHost.Tests
// File: PluginLoadContextTests.cs
// Description:
//     Verifies that PluginLoadContext (collectible ALC) is garbage-collected
//     after Unload(), using the WeakReference pattern from .NET docs.
// ==========================================================

namespace WpfHexEditor.PluginHost.Tests;

[TestClass]
public sealed class PluginLoadContextTests
{
    // The GC needs several cycles to collect a collectible ALC.
    // Three forced GC cycles with compaction is the .NET-recommended minimum.
    private const int GcCycles = 3;

    /// <summary>
    /// Verifies that a PluginLoadContext becomes eligible for collection after
    /// Unload() is called and all strong references to it are dropped.
    /// This is the ADR-081 hot-reload invariant: no ALC leak after plugin unload.
    /// </summary>
    [TestMethod]
    public void Unload_CollectsALC_AfterThreeGcCycles()
    {
        var weakRef = CreateAndUnload();

        for (int i = 0; i < GcCycles; i++)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            GC.WaitForPendingFinalizers();
        }

        Assert.IsFalse(weakRef.IsAlive,
            "PluginLoadContext was NOT collected after Unload() + 3 GC cycles — ALC leak.");
    }

    [TestMethod]
    public void Unload_WeakReference_IsAliveBeforeGc()
    {
        // Sanity: the WeakRef must be alive before GC.
        var ctx = new PluginLoadContext(GetDummyAssemblyPath());
        var wr  = ctx.CreateWeakReference();
        Assert.IsTrue(wr.TryGetTarget(out _));
        ctx.Unload();
    }

    [TestMethod]
    public void PluginLoadContext_Name_MatchesAssemblyFileName()
    {
        var path = GetDummyAssemblyPath();
        var ctx  = new PluginLoadContext(path);
        Assert.AreEqual(Path.GetFileNameWithoutExtension(path), ctx.Name);
        ctx.Unload();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Isolated to a no-inline method so the local ctx variable is not kept
    // alive by the JIT in the caller's stack frame (critical for GC test).
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference CreateAndUnload()
    {
        var ctx = new PluginLoadContext(GetDummyAssemblyPath());
        var wr  = new WeakReference(ctx);
        ctx.Unload();
        return wr;
    }

    private static string GetDummyAssemblyPath()
    {
        // Use this test assembly itself as the "plugin" path — we only need
        // a valid path for the AssemblyDependencyResolver constructor; the
        // context is unloaded before any actual loading occurs.
        return typeof(PluginLoadContextTests).Assembly.Location;
    }
}
