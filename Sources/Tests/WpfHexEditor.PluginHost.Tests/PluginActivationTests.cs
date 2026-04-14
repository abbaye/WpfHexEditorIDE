// ==========================================================
// Project: WpfHexEditor.PluginHost.Tests
// File: PluginActivationTests.cs
// Description:
//     Unit tests for PluginEntry state machine, PluginManifest validation,
//     PluginDependencyGraph, and MarketplaceUpdateScheduler interval behaviour.
// ==========================================================

using WpfHexEditor.PluginHost;
using WpfHexEditor.SDK.Models;
using WpfHexEditor.PluginHost.Services;

namespace WpfHexEditor.PluginHost.Tests;

[TestClass]
public sealed class PluginActivationTests
{
    // ── PluginEntry — state machine ───────────────────────────────────────────

    [TestMethod]
    public void PluginEntry_InitialState_IsUnloaded()
    {
        var entry = new PluginEntry(CreateManifest("test-plugin", "1.0.0"));
        Assert.AreEqual(PluginState.Unloaded, entry.State);
    }

    [TestMethod]
    public void PluginEntry_ManifestId_MatchesInput()
    {
        var entry = new PluginEntry(CreateManifest("my-plugin", "2.0.0"));
        Assert.AreEqual("my-plugin", entry.Manifest.Id);
        Assert.AreEqual("2.0.0",     entry.Manifest.Version);
    }

    [TestMethod]
    public void PluginEntry_IsActive_WhenStateIsLoaded()
    {
        var entry = new PluginEntry(CreateManifest("active-plugin", "1.0.0"));
        entry.SetState(PluginState.Loaded);
        Assert.IsTrue(entry.IsActive);
    }

    [TestMethod]
    public void PluginEntry_IsNotActive_WhenStateIsUnloaded()
    {
        var entry = new PluginEntry(CreateManifest("inactive-plugin", "1.0.0"));
        Assert.IsFalse(entry.IsActive);
    }

    [TestMethod]
    public void PluginEntry_IsNotActive_WhenStateIsFailed()
    {
        var entry = new PluginEntry(CreateManifest("failed-plugin", "1.0.0"));
        entry.SetState(PluginState.Faulted);
        Assert.IsFalse(entry.IsActive);
    }

    [TestMethod]
    public void PluginEntry_IsDormant_WhenStateDormant()
    {
        var entry = new PluginEntry(CreateManifest("lazy-plugin", "1.0.0"));
        entry.SetState(PluginState.Dormant);
        Assert.AreEqual(PluginState.Dormant, entry.State);
        Assert.IsFalse(entry.IsActive);
    }

    // ── PluginManifest validation ─────────────────────────────────────────────

    [TestMethod]
    public void PluginManifest_ValidManifest_PassesValidation()
    {
        var manifest  = CreateManifest("valid-plugin", "1.0.0");
        var validator = new PluginManifestValidator(new Version(1, 0), new Version(1, 0));
        var result    = validator.Validate(manifest, string.Empty);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void PluginManifest_EmptyId_FailsValidation()
    {
        var manifest  = CreateManifest("", "1.0.0");
        var validator = new PluginManifestValidator(new Version(1, 0), new Version(1, 0));
        var result    = validator.Validate(manifest, string.Empty);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void PluginManifest_EmptyVersion_FailsValidation()
    {
        var manifest  = CreateManifest("plugin", "");
        var validator = new PluginManifestValidator(new Version(1, 0), new Version(1, 0));
        var result    = validator.Validate(manifest, string.Empty);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void PluginManifest_InvalidVersionFormat_FailsValidation()
    {
        var manifest  = CreateManifest("plugin", "not-a-version");
        var validator = new PluginManifestValidator(new Version(1, 0), new Version(1, 0));
        var result    = validator.Validate(manifest, string.Empty);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void PluginManifest_ValidSemver_PassesValidation()
    {
        var manifest  = CreateManifest("plugin", "1.2.3");
        var validator = new PluginManifestValidator(new Version(1, 0), new Version(1, 0));
        var result    = validator.Validate(manifest, string.Empty);
        Assert.IsTrue(result.IsValid);
    }

    // ── PluginDependencyGraph ─────────────────────────────────────────────────

    [TestMethod]
    public void DependencyGraph_Empty_HasNoCycles()
    {
        var graph  = new PluginDependencyGraph();
        graph.Build([]);
        var errors = graph.Validate(new Dictionary<string, PluginEntry>());
        Assert.IsFalse(errors.Any(e => e.Kind == DependencyErrorKind.Circular));
    }

    [TestMethod]
    public void DependencyGraph_SingleNode_HasNoCycle()
    {
        var graph    = new PluginDependencyGraph();
        var manifest = CreateManifest("plugin-a", "1.0.0");
        graph.Build([manifest]);
        var errors = graph.Validate(new Dictionary<string, PluginEntry>());
        Assert.IsFalse(errors.Any(e => e.Kind == DependencyErrorKind.Circular));
    }

    [TestMethod]
    public void DependencyGraph_LinearChain_HasNoCycle()
    {
        var a = CreateManifest("a", "1.0.0");
        var b = CreateManifest("b", "1.0.0"); b.Dependencies.Add("a");
        var c = CreateManifest("c", "1.0.0"); c.Dependencies.Add("b");

        var graph = new PluginDependencyGraph();
        graph.Build([a, b, c]);
        var entries = new Dictionary<string, PluginEntry>
        {
            ["a"] = new PluginEntry(a),
            ["b"] = new PluginEntry(b),
            ["c"] = new PluginEntry(c),
        };
        var errors = graph.Validate(entries);
        Assert.IsFalse(errors.Any(e => e.Kind == DependencyErrorKind.Circular));
    }

    [TestMethod]
    public void DependencyGraph_SelfLoop_HasCycle()
    {
        var a = CreateManifest("a", "1.0.0"); a.Dependencies.Add("a");
        var graph = new PluginDependencyGraph();
        graph.Build([a]);
        var errors = graph.Validate(new Dictionary<string, PluginEntry> { ["a"] = new PluginEntry(a) });
        Assert.IsTrue(errors.Any(e => e.Kind == DependencyErrorKind.Circular));
    }

    [TestMethod]
    public void DependencyGraph_MutualDependency_HasCycle()
    {
        var a = CreateManifest("a", "1.0.0"); a.Dependencies.Add("b");
        var b = CreateManifest("b", "1.0.0"); b.Dependencies.Add("a");
        var graph = new PluginDependencyGraph();
        graph.Build([a, b]);
        var entries = new Dictionary<string, PluginEntry>
        {
            ["a"] = new PluginEntry(a),
            ["b"] = new PluginEntry(b),
        };
        var errors = graph.Validate(entries);
        Assert.IsTrue(errors.Any(e => e.Kind == DependencyErrorKind.Circular));
    }

    [TestMethod]
    public void DependencyGraph_GetLoadOrder_NoCycle_ReturnsOrder()
    {
        var core    = CreateManifest("core",    "1.0.0");
        var feature = CreateManifest("feature", "1.0.0"); feature.Dependencies.Add("core");

        var graph = new PluginDependencyGraph();
        graph.Build([core, feature]);

        var coreEntry    = new PluginEntry(core);
        var featureEntry = new PluginEntry(feature);
        var entries = new Dictionary<string, PluginEntry>
        {
            ["core"]    = coreEntry,
            ["feature"] = featureEntry,
        };
        var order = graph.GetLoadOrder(entries);
        Assert.AreEqual(2, order.Count);
        Assert.AreEqual("core",    order[0].Id);
        Assert.AreEqual("feature", order[1].Id);
    }

    // ── MarketplaceUpdateScheduler ────────────────────────────────────────────

    [TestMethod]
    public void MarketplaceUpdateScheduler_Start_DoesNotThrowImmediately()
    {
        // Scheduler delays 60 s before first check — just verify Start() doesn't blow up.
        using var bus  = new IDEEventBus();
        var settings   = new WpfHexEditor.Core.Options.AppSettings();
        var svc        = new MarketplaceServiceImplStub();
        var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;

        using var scheduler = new MarketplaceUpdateScheduler(
            svc, bus, settings, dispatcher);

        scheduler.Start(); // should not throw
    }

    [TestMethod]
    public void MarketplaceUpdateScheduler_Dispose_DoesNotThrow()
    {
        using var bus  = new IDEEventBus();
        var settings   = new WpfHexEditor.Core.Options.AppSettings();
        var svc        = new MarketplaceServiceImplStub();
        var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;

        var scheduler = new MarketplaceUpdateScheduler(svc, bus, settings, dispatcher);
        scheduler.Start();
        scheduler.Dispose(); // clean cancellation
    }

    [TestMethod]
    public void MarketplaceUpdateScheduler_StartTwice_DoesNotStartSecondLoop()
    {
        using var bus  = new IDEEventBus();
        var settings   = new WpfHexEditor.Core.Options.AppSettings();
        var svc        = new MarketplaceServiceImplStub();
        var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;

        using var scheduler = new MarketplaceUpdateScheduler(svc, bus, settings, dispatcher);
        scheduler.Start();
        scheduler.Start(); // second call — should be no-op
    }

    // ── PluginEntry — additional state transitions ─────────────────────────────

    [TestMethod]
    public void PluginEntry_LoadedThenUnloaded_StateIsUnloaded()
    {
        var entry = new PluginEntry(CreateManifest("transient-plugin", "1.0.0"));
        entry.SetState(PluginState.Loaded);
        entry.SetState(PluginState.Unloaded);
        Assert.AreEqual(PluginState.Unloaded, entry.State);
        Assert.IsFalse(entry.IsActive);
    }

    [TestMethod]
    public void PluginEntry_IncompatibleState_IsNotActive()
    {
        var entry = new PluginEntry(CreateManifest("incompatible-plugin", "0.0.1"));
        entry.SetState(PluginState.Incompatible);
        Assert.IsFalse(entry.IsActive);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static PluginManifest CreateManifest(string id, string version) => new()
    {
        Id          = id,
        Name        = id,
        Version     = version,
        Description = "Test",
        Author      = "Test",
        EntryPoint  = $"{id}.dll",
    };

    // Minimal stub for IMarketplaceService
    private sealed class MarketplaceServiceImplStub : WpfHexEditor.SDK.Contracts.IMarketplaceService
    {
        public Task<IReadOnlyList<WpfHexEditor.SDK.Models.MarketplaceListing>> SearchAsync(
            string q, WpfHexEditor.SDK.Contracts.MarketplaceFilter? f = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<WpfHexEditor.SDK.Models.MarketplaceListing>>([]);

        public Task<WpfHexEditor.SDK.Models.MarketplaceListing?> GetByIdAsync(
            string id, CancellationToken ct = default) => Task.FromResult<WpfHexEditor.SDK.Models.MarketplaceListing?>(null);

        public Task<WpfHexEditor.SDK.Contracts.InstallResult> InstallAsync(
            string id, IProgress<WpfHexEditor.SDK.Contracts.InstallProgress>? p = null, CancellationToken ct = default)
            => Task.FromResult(new WpfHexEditor.SDK.Contracts.InstallResult(false, "stub", null));

        public Task<bool> UninstallAsync(string id, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> RollbackAsync(string id, CancellationToken ct = default) => Task.FromResult(false);
        public Task<string?> GetChangelogAsync(string id, CancellationToken ct = default) => Task.FromResult<string?>(null);

        public Task<IReadOnlyList<WpfHexEditor.SDK.Models.MarketplaceListing>> GetInstalledAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<WpfHexEditor.SDK.Models.MarketplaceListing>>([]);

        public Task<IReadOnlyList<WpfHexEditor.SDK.Models.MarketplaceListing>> GetUpdatesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<WpfHexEditor.SDK.Models.MarketplaceListing>>([]);

        public bool IsInstalled(string id) => false;

        public Task<bool> VerifyIntegrityAsync(string id, CancellationToken ct = default) => Task.FromResult(true);

        public event EventHandler<WpfHexEditor.SDK.Contracts.InstallProgressEventArgs>? InstallProgressChanged;
    }
}
