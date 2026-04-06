// ==========================================================
// Project: WpfHexEditor.PluginHost.Tests
// File: PluginDependencyGraphTests.cs
// Contributors: Claude Sonnet 4.6
// Description:
//     Tests for PluginDependencyGraph — load ordering, cascaded unload/reload,
//     cycle detection, missing dependency validation.
// ==========================================================

using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.PluginHost.Tests;

[TestClass]
public sealed class PluginDependencyGraphTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static PluginManifest Manifest(string id, string version = "1.0.0", List<string>? deps = null)
        => new()
        {
            Id           = id,
            Name         = id,
            Version      = version,
            EntryPoint   = $"{id}.Plugin",
            SdkVersion   = "1.0.0",
            Dependencies = deps ?? []
        };

    private static IReadOnlyDictionary<string, PluginEntry> Entries(params PluginManifest[] manifests)
        => manifests.ToDictionary(m => m.Id, m => new PluginEntry(m), StringComparer.OrdinalIgnoreCase);

    private static PluginDependencyGraph Build(params PluginManifest[] manifests)
    {
        var g = new PluginDependencyGraph();
        g.Build(manifests);
        return g;
    }

    // Helper: index in IReadOnlyList<string>
    private static int Idx(IReadOnlyList<string> list, string value)
        => list.ToList().IndexOf(value);

    // ── GetLoadOrder ──────────────────────────────────────────────────────────

    [TestMethod]
    public void GetLoadOrder_Empty_ReturnsEmpty()
    {
        var g = Build();
        Assert.AreEqual(0, g.GetLoadOrder(new Dictionary<string, PluginEntry>()).Count);
    }

    [TestMethod]
    public void GetLoadOrder_SinglePlugin_NoDeps_InResult()
    {
        var a = Manifest("A");
        var g = Build(a);
        var order = g.GetLoadOrder(Entries(a));
        Assert.AreEqual(1, order.Count);
        Assert.AreEqual("A", order[0].Id);
    }

    [TestMethod]
    public void GetLoadOrder_ADependsOnB_BLoadsFirst()
    {
        var b = Manifest("B");
        var a = Manifest("A", deps: ["B"]);
        var g = Build(a, b);

        var ids = g.GetLoadOrder(Entries(a, b)).Select(m => m.Id).ToList();

        Assert.IsTrue(ids.IndexOf("B") < ids.IndexOf("A"),
            $"Expected B before A. Order: {string.Join(",", ids)}");
    }

    [TestMethod]
    public void GetLoadOrder_Transitive_CLoadsFirst()
    {
        // A → B → C  ⟹  C, B, A
        var c = Manifest("C");
        var b = Manifest("B", deps: ["C"]);
        var a = Manifest("A", deps: ["B"]);
        var g = Build(a, b, c);

        var ids = g.GetLoadOrder(Entries(a, b, c)).Select(m => m.Id).ToList();

        Assert.IsTrue(ids.IndexOf("C") < ids.IndexOf("B"), "C before B");
        Assert.IsTrue(ids.IndexOf("B") < ids.IndexOf("A"), "B before A");
    }

    [TestMethod]
    public void GetLoadOrder_DiamondDependency_AllPresent()
    {
        // A → B, A → C, B → D, C → D
        var d = Manifest("D");
        var b = Manifest("B", deps: ["D"]);
        var c = Manifest("C", deps: ["D"]);
        var a = Manifest("A", deps: ["B", "C"]);
        var g = Build(a, b, c, d);

        var ids = g.GetLoadOrder(Entries(a, b, c, d)).Select(m => m.Id).ToList();

        Assert.AreEqual(4, ids.Count);
        Assert.IsTrue(ids.IndexOf("D") < ids.IndexOf("B"), "D before B");
        Assert.IsTrue(ids.IndexOf("D") < ids.IndexOf("C"), "D before C");
        Assert.IsTrue(ids.IndexOf("B") < ids.IndexOf("A") || ids.IndexOf("C") < ids.IndexOf("A"),
            $"B or C must load before A. Order: {string.Join(",", ids)}");
    }

    // ── GetDependents ─────────────────────────────────────────────────────────

    [TestMethod]
    public void GetDependents_NoReverseDeps_ReturnsEmpty()
    {
        var a = Manifest("A");
        var g = Build(a);
        Assert.AreEqual(0, g.GetDependents("A").Count);
    }

    [TestMethod]
    public void GetDependents_DirectDependents_ReturnsCorrectly()
    {
        var b = Manifest("B");
        var a = Manifest("A", deps: ["B"]);
        var g = Build(a, b);

        var deps = g.GetDependents("B");

        Assert.AreEqual(1, deps.Count);
        Assert.AreEqual("A", deps[0], StringComparer.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void GetDependents_Transitive_IncludesAll()
    {
        // A → B → C  ⟹  C's dependents are B (direct) and A (transitive)
        var c = Manifest("C");
        var b = Manifest("B", deps: ["C"]);
        var a = Manifest("A", deps: ["B"]);
        var g = Build(a, b, c);

        var deps = g.GetDependents("C");

        Assert.IsTrue(deps.Any(d => d.Equals("B", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(deps.Any(d => d.Equals("A", StringComparison.OrdinalIgnoreCase)));
    }

    // ── GetCascadedUnloadOrder ────────────────────────────────────────────────

    [TestMethod]
    public void GetCascadedUnloadOrder_NoDependents_OnlyTarget()
    {
        var a = Manifest("A");
        var g = Build(a);

        var order = g.GetCascadedUnloadOrder("A");

        Assert.AreEqual(1, order.Count);
        Assert.AreEqual("A", order[0], StringComparer.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void GetCascadedUnloadOrder_DependentFirst_TargetLast()
    {
        var b = Manifest("B");
        var a = Manifest("A", deps: ["B"]);
        var g = Build(a, b);

        var order = g.GetCascadedUnloadOrder("B");

        Assert.IsTrue(Idx(order, "A") < Idx(order, "B"),
            $"A should unload before B. Got: {string.Join(",", order)}");
    }

    // ── GetCascadedReloadOrder ────────────────────────────────────────────────

    [TestMethod]
    public void GetCascadedReloadOrder_TargetFirst_DependentsAfter()
    {
        var b = Manifest("B");
        var a = Manifest("A", deps: ["B"]);
        var g = Build(a, b);

        var order = g.GetCascadedReloadOrder("B");

        Assert.AreEqual("B", order[0], StringComparer.OrdinalIgnoreCase);
        Assert.IsTrue(order.Any(x => x.Equals("A", StringComparison.OrdinalIgnoreCase)));
    }

    // ── Validate — missing deps ───────────────────────────────────────────────

    [TestMethod]
    public void Validate_AllPresent_NoDependencyErrors()
    {
        var b = Manifest("B");
        var a = Manifest("A", deps: ["B"]);
        var g = Build(a, b);

        var errors = g.Validate(Entries(a, b));

        Assert.IsFalse(errors.Any(e => e.Kind == DependencyErrorKind.Missing));
    }

    [TestMethod]
    public void Validate_MissingDependency_ReportsMissingError()
    {
        var a = Manifest("A", deps: ["B"]); // B not in graph
        var g = Build(a);

        var errors = g.Validate(Entries(a));

        Assert.IsTrue(errors.Any(e => e.Kind == DependencyErrorKind.Missing
                                   && e.RequiredPluginId == "B"),
            "Expected Missing error for B");
    }

    // ── Validate — cycle detection ────────────────────────────────────────────

    [TestMethod]
    public void Validate_DirectCycle_ReportsCircularError()
    {
        // A → B, B → A
        var a = Manifest("A", deps: ["B"]);
        var b = Manifest("B", deps: ["A"]);
        var g = Build(a, b);

        var errors = g.Validate(Entries(a, b));

        Assert.IsTrue(errors.Any(e => e.Kind == DependencyErrorKind.Circular),
            "Expected Circular error");
    }

    // ── GetDirectDependencies / GetDirectDependents ───────────────────────────

    [TestMethod]
    public void GetDirectDependencies_ReturnsOnlyDirect()
    {
        var c = Manifest("C");
        var b = Manifest("B", deps: ["C"]);
        var a = Manifest("A", deps: ["B"]);
        var g = Build(a, b, c);

        var deps = g.GetDirectDependencies("A");

        Assert.AreEqual(1, deps.Count);
        Assert.AreEqual("B", deps[0].PluginId, StringComparer.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void GetDirectDependents_ReturnsOnlyDirect()
    {
        var b = Manifest("B");
        var a = Manifest("A", deps: ["B"]);
        var g = Build(a, b);

        var dependents = g.GetDirectDependents("B");

        Assert.AreEqual(1, dependents.Count);
        Assert.AreEqual("A", dependents[0], StringComparer.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void GetDirectDependencies_UnknownPlugin_ReturnsEmpty()
    {
        var g = Build(Manifest("A"));
        Assert.AreEqual(0, g.GetDirectDependencies("UNKNOWN").Count);
    }
}
