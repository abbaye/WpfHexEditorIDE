//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// PluginMigrationMonitor Unit Tests
// Author : Claude Sonnet 4.6
// Contributors: Derek Tremblay (derektremblay666@gmail.com)
//////////////////////////////////////////////

using System.Windows.Threading;
using WpfHexEditor.PluginHost;
using WpfHexEditor.PluginHost.Monitoring;
using WpfHexEditor.PluginHost.Services;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.Tests.Unit;

/// <summary>
/// Unit tests for PluginMigrationMonitor — crash counter state and policy update API.
/// Timer-based trigger tests (EvaluateEntry) require a running WPF message loop and
/// are validated via the integration / manual verification checklist instead.
/// </summary>
[TestClass]
public class PluginMigrationMonitor_Tests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static PluginMigrationMonitor CreateMonitor(
        PluginMigrationPolicy?                            policy            = null,
        Action<string, MigrationTriggerReason>?           onTriggered       = null)
    {
        return new PluginMigrationMonitor(
            getLoadedEntries:     () => [],
            policy:               policy ?? PluginMigrationPolicy.CreateDefault(),
            onMigrationTriggered: onTriggered ?? ((_, _) => { }),
            dispatcher:           Dispatcher.CurrentDispatcher);
    }

    private static PluginEntry MakeEntry(
        string            id,
        PluginState       state = PluginState.Loaded,
        PluginIsolationMode isolation = PluginIsolationMode.InProcess)
    {
        var manifest = new PluginManifest
        {
            Id   = id,
            Name = $"Plugin-{id}"
        };
        var entry = new PluginEntry(manifest);
        entry.SetState(state);
        entry.SetResolvedIsolationMode(isolation);
        return entry;
    }

    // ── Crash counter — basic state ───────────────────────────────────────────

    [TestMethod]
    public void GetCrashCount_ReturnsZero_ForUnknownPlugin()
    {
        using var monitor = CreateMonitor();

        Assert.AreEqual(0, monitor.GetCrashCount("unknown.plugin"));
    }

    [TestMethod]
    public void RecordCrash_IncrementsCrashCount()
    {
        using var monitor = CreateMonitor();

        monitor.RecordCrash("plugin.a");
        Assert.AreEqual(1, monitor.GetCrashCount("plugin.a"));

        monitor.RecordCrash("plugin.a");
        Assert.AreEqual(2, monitor.GetCrashCount("plugin.a"));
    }

    [TestMethod]
    public void RecordCrash_IsIndependentPerPlugin()
    {
        using var monitor = CreateMonitor();

        monitor.RecordCrash("plugin.a");
        monitor.RecordCrash("plugin.a");
        monitor.RecordCrash("plugin.b");

        Assert.AreEqual(2, monitor.GetCrashCount("plugin.a"));
        Assert.AreEqual(1, monitor.GetCrashCount("plugin.b"));
    }

    [TestMethod]
    public void RecordCrash_IgnoresNullOrEmpty()
    {
        using var monitor = CreateMonitor();

        // Must not throw.
        monitor.RecordCrash(null!);
        monitor.RecordCrash(string.Empty);

        Assert.AreEqual(0, monitor.GetCrashCount(string.Empty));
    }

    [TestMethod]
    public void ResetCrashCount_ClearsCrashCounter()
    {
        using var monitor = CreateMonitor();

        monitor.RecordCrash("plugin.a");
        monitor.RecordCrash("plugin.a");
        monitor.ResetCrashCount("plugin.a");

        Assert.AreEqual(0, monitor.GetCrashCount("plugin.a"));
    }

    [TestMethod]
    public void ResetCrashCount_OnPluginWithNoCrashes_DoesNotThrow()
    {
        using var monitor = CreateMonitor();

        // Should be a no-op, not an exception.
        monitor.ResetCrashCount("nonexistent.plugin");
        Assert.AreEqual(0, monitor.GetCrashCount("nonexistent.plugin"));
    }

    [TestMethod]
    public void CrashCounting_IsCaseInsensitive()
    {
        using var monitor = CreateMonitor();

        monitor.RecordCrash("Plugin.A");
        monitor.RecordCrash("plugin.a");

        // Both keys are the same under OrdinalIgnoreCase.
        Assert.AreEqual(2, monitor.GetCrashCount("PLUGIN.A"));
    }

    // ── Policy update ────────────────────────────────────────────────────────

    [TestMethod]
    public void UpdatePolicy_DoesNotThrow()
    {
        using var monitor = CreateMonitor();

        var newPolicy = new PluginMigrationPolicy
        {
            Mode                         = PluginMigrationMode.AutoMigrate,
            CrashCountThreshold          = 1,
            MemorySuggestThresholdMb     = 100,
            MemoryAutoMigrateThresholdMb = 200,
            CpuSuggestThresholdPercent   = 30,
            CpuAutoMigrateThresholdPercent = 60,
            CpuSustainedWindowSeconds    = 10
        };

        // Must not throw — policy is swapped atomically for the next tick.
        monitor.UpdatePolicy(newPolicy);
    }

    [TestMethod]
    public void UpdatePolicy_ThrowsArgumentNull_WhenNull()
    {
        using var monitor = CreateMonitor();

        bool threw = false;
        try { monitor.UpdatePolicy(null!); }
        catch (ArgumentNullException) { threw = true; }

        Assert.IsTrue(threw, "UpdatePolicy(null) should throw ArgumentNullException.");
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    [TestMethod]
    public void Dispose_CanBeCalledMultipleTimes_WithoutThrowing()
    {
        var monitor = CreateMonitor();
        monitor.Dispose();
        monitor.Dispose();  // idempotent — must not throw
    }

    [TestMethod]
    public void Start_AfterDispose_DoesNotThrow()
    {
        var monitor = CreateMonitor();
        monitor.Dispose();

        // Start on a disposed monitor must be a safe no-op.
        monitor.Start();
    }

    // ── Crash threshold boundary — using EvaluateEntry indirectly ────────────
    //
    // We can verify the threshold detection without starting the timer by
    // checking that RecordCrash correctly tracks state that EvaluateEntry reads.

    [TestMethod]
    public void RecordCrash_ThreeTimes_MeetsCrashThreshold_WithDefaultPolicy()
    {
        var policy  = PluginMigrationPolicy.CreateDefault();   // CrashCountThreshold = 3
        using var monitor = CreateMonitor(policy);

        monitor.RecordCrash("plugin.x");
        monitor.RecordCrash("plugin.x");
        monitor.RecordCrash("plugin.x");

        // Three crashes equals the default threshold of 3 → trigger should fire on next tick.
        // We verify the counter state here; the actual trigger is fired by OnTick (timer-based).
        Assert.AreEqual(3, monitor.GetCrashCount("plugin.x"));
        Assert.IsTrue(monitor.GetCrashCount("plugin.x") >= policy.CrashCountThreshold);
    }

    [TestMethod]
    public void RecordCrash_TwoTimes_BelowDefaultThreshold()
    {
        var policy  = PluginMigrationPolicy.CreateDefault();   // CrashCountThreshold = 3
        using var monitor = CreateMonitor(policy);

        monitor.RecordCrash("plugin.y");
        monitor.RecordCrash("plugin.y");

        Assert.IsFalse(monitor.GetCrashCount("plugin.y") >= policy.CrashCountThreshold,
            "Two crashes should not meet the default threshold of 3.");
    }
}
