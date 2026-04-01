//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Plugin Isolation Decision Engine Unit Tests
// Author : Claude Sonnet 4.6
// Contributors: Derek Tremblay (derektremblay666@gmail.com)
//////////////////////////////////////////////

using WpfHexEditor.PluginHost.Services;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.Tests.Unit;

[TestClass]
public class PluginIsolationDecisionEngine_Tests
{
    // ── Helpers ─────────────────────────────────────────────────────────────

    private static PluginManifest MakeManifest(
        bool trusted,
        bool registerMenus  = false,
        bool accessNetwork  = false,
        bool isTerminalOnly = false,
        PluginIsolationMode mode = PluginIsolationMode.Auto)
        => new()
        {
            Id              = "test.plugin",
            Name            = "Test Plugin",
            TrustedPublisher = trusted,
            IsolationMode   = mode,
            Permissions     = new PluginCapabilities
            {
                RegisterMenus  = registerMenus,
                AccessNetwork  = accessNetwork,
                IsTerminalOnly = isTerminalOnly,
            }
        };

    // ── Rule 1: Untrusted → Sandbox ──────────────────────────────────────────

    [TestMethod]
    public void Untrusted_NoCapabilities_ResolvesSandbox()
    {
        var result = PluginIsolationDecisionEngine.Resolve(MakeManifest(trusted: false));
        Assert.AreEqual(PluginIsolationMode.Sandbox, result);
    }

    [TestMethod]
    public void Untrusted_WithUI_StillResolvesSandbox()
    {
        // Rule 1 wins over Rule 2 — untrusted must always be sandboxed.
        var result = PluginIsolationDecisionEngine.Resolve(
            MakeManifest(trusted: false, registerMenus: true));
        Assert.AreEqual(PluginIsolationMode.Sandbox, result);
    }

    // ── Rule 2: RegisterMenus → InProcess ────────────────────────────────────

    [TestMethod]
    public void Trusted_RegisterMenus_ResolvesInProcess()
    {
        var result = PluginIsolationDecisionEngine.Resolve(
            MakeManifest(trusted: true, registerMenus: true));
        Assert.AreEqual(PluginIsolationMode.InProcess, result);
    }

    [TestMethod]
    public void Trusted_RegisterMenus_WithNetwork_ResolvesInProcess()
    {
        // Rule 2 fires before Rule 3 — UI always promotes to InProcess.
        var result = PluginIsolationDecisionEngine.Resolve(
            MakeManifest(trusted: true, registerMenus: true, accessNetwork: true));
        Assert.AreEqual(PluginIsolationMode.InProcess, result);
    }

    // ── Rule 3: AccessNetwork → Sandbox ──────────────────────────────────────

    [TestMethod]
    public void Trusted_NetworkOnly_ResolvesSandbox()
    {
        var result = PluginIsolationDecisionEngine.Resolve(
            MakeManifest(trusted: true, accessNetwork: true));
        Assert.AreEqual(PluginIsolationMode.Sandbox, result);
    }

    [TestMethod]
    public void Trusted_NetworkAndTerminalOnly_ResolvesSandbox()
    {
        // Rule 3 fires before Rule 4 — network always sandboxes.
        var result = PluginIsolationDecisionEngine.Resolve(
            MakeManifest(trusted: true, accessNetwork: true, isTerminalOnly: true));
        Assert.AreEqual(PluginIsolationMode.Sandbox, result);
    }

    // ── Rule 4: IsTerminalOnly → InProcess ───────────────────────────────────

    [TestMethod]
    public void Trusted_TerminalOnly_ResolvesInProcess()
    {
        var result = PluginIsolationDecisionEngine.Resolve(
            MakeManifest(trusted: true, isTerminalOnly: true));
        Assert.AreEqual(PluginIsolationMode.InProcess, result);
    }

    // ── Default: trusted, no special caps ────────────────────────────────────

    [TestMethod]
    public void Trusted_NoCapabilities_ResolvesInProcess()
    {
        var result = PluginIsolationDecisionEngine.Resolve(MakeManifest(trusted: true));
        Assert.AreEqual(PluginIsolationMode.InProcess, result);
    }

    // ── Non-Auto passthrough ─────────────────────────────────────────────────

    [TestMethod]
    public void ExplicitInProcess_IsNotReevaluated()
    {
        // Even if it would normally sandbox (untrusted), explicit InProcess must be honoured.
        var result = PluginIsolationDecisionEngine.Resolve(
            MakeManifest(trusted: false, mode: PluginIsolationMode.InProcess));
        Assert.AreEqual(PluginIsolationMode.InProcess, result);
    }

    [TestMethod]
    public void ExplicitSandbox_IsNotReevaluated()
    {
        // Trusted + RegisterMenus would normally resolve InProcess, but explicit Sandbox is honoured.
        var result = PluginIsolationDecisionEngine.Resolve(
            MakeManifest(trusted: true, registerMenus: true, mode: PluginIsolationMode.Sandbox));
        Assert.AreEqual(PluginIsolationMode.Sandbox, result);
    }
}
