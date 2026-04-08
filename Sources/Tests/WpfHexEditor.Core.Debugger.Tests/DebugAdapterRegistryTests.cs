// ==========================================================
// Project: WpfHexEditor.Core.Debugger.Tests
// File: DebugAdapterRegistryTests.cs
// Description:
//     Unit tests for DebugAdapterRegistry, TcpDapTransport,
//     DebugLaunchConfig, and model types.
// ==========================================================

using WpfHexEditor.Core.Debugger.Services;
using WpfHexEditor.Core.Debugger.Models;

namespace WpfHexEditor.Core.Debugger.Tests;

[TestClass]
public sealed class DebugAdapterRegistryTests
{
    // ── DebugAdapterRegistry ──────────────────────────────────────────────────

    [TestMethod]
    public void HasAdapter_EmptyRegistry_ReturnsFalse()
    {
        var registry = new DebugAdapterRegistry();
        Assert.IsFalse(registry.HasAdapter("python"));
    }

    [TestMethod]
    public void Register_ThenHasAdapter_ReturnsTrue()
    {
        var registry = new DebugAdapterRegistry();
        registry.Register("python", () => null!);
        Assert.IsTrue(registry.HasAdapter("python"));
    }

    [TestMethod]
    public void Register_CaseInsensitive_ReturnsTrue()
    {
        var registry = new DebugAdapterRegistry();
        registry.Register("Python", () => null!);
        Assert.IsTrue(registry.HasAdapter("python"));
        Assert.IsTrue(registry.HasAdapter("PYTHON"));
    }

    [TestMethod]
    public void Unregister_AfterRegister_RemovesAdapter()
    {
        var registry = new DebugAdapterRegistry();
        registry.Register("ruby", () => null!);
        registry.Unregister("ruby");
        Assert.IsFalse(registry.HasAdapter("ruby"));
    }

    [TestMethod]
    public void Unregister_NonExistentKey_DoesNotThrow()
    {
        var registry = new DebugAdapterRegistry();
        registry.Unregister("nonexistent");
    }

    [TestMethod]
    public void CreateAdapter_NoRegistration_ReturnsNull()
    {
        var registry = new DebugAdapterRegistry();
        Assert.IsNull(registry.CreateAdapter("go"));
    }

    [TestMethod]
    public void CreateAdapter_WithRegistration_InvokesFactory()
    {
        var called   = false;
        var registry = new DebugAdapterRegistry();
        registry.Register("test-lang", () => { called = true; return null!; });

        registry.CreateAdapter("test-lang");
        Assert.IsTrue(called);
    }

    [TestMethod]
    public void Register_SameKey_ReplacesFactory()
    {
        int callCount = 0;
        var registry  = new DebugAdapterRegistry();
        registry.Register("lang", () => { callCount++; return null!; });
        registry.Register("lang", () => { callCount += 10; return null!; }); // replace

        registry.CreateAdapter("lang");
        Assert.AreEqual(10, callCount, "Should call the second (replacement) factory.");
    }

    [TestMethod]
    public void MultipleLanguages_RegisteredIndependently()
    {
        var registry = new DebugAdapterRegistry();
        registry.Register("java",   () => null!);
        registry.Register("kotlin", () => null!);
        Assert.IsTrue(registry.HasAdapter("java"));
        Assert.IsTrue(registry.HasAdapter("kotlin"));
        Assert.IsFalse(registry.HasAdapter("scala"));
    }

    // ── DebugLaunchConfig ─────────────────────────────────────────────────────

    [TestMethod]
    public void DebugLaunchConfig_DefaultValues()
    {
        var config = new DebugLaunchConfig();
        Assert.AreEqual(string.Empty, config.ProgramPath);
        Assert.AreEqual(string.Empty, config.ProjectPath);
        Assert.AreEqual("csharp",     config.LanguageId);
        Assert.AreEqual(0,            config.Args.Length);
        Assert.IsNull(config.WorkDir);
        Assert.IsFalse(config.StopAtEntry);
    }

    [TestMethod]
    public void DebugLaunchConfig_WithValues_RoundTrips()
    {
        var config = new DebugLaunchConfig
        {
            LanguageId  = "python",
            ProgramPath = "/app/script.py",
            ProjectPath = "/app/project.json",
            Args        = ["--verbose", "--port", "8080"],
            WorkDir     = "/app",
            StopAtEntry = true,
            Env         = new() { ["PYTHONPATH"] = "/app/lib" },
        };

        Assert.AreEqual("python",          config.LanguageId);
        Assert.AreEqual("/app/script.py",  config.ProgramPath);
        Assert.AreEqual(3,                 config.Args.Length);
        Assert.AreEqual("--verbose",       config.Args[0]);
        Assert.AreEqual("/app",            config.WorkDir);
        Assert.IsTrue(config.StopAtEntry);
        Assert.AreEqual("/app/lib",        config.Env["PYTHONPATH"]);
    }

    // ── TcpDapTransport ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task TcpDapTransport_ConnectAsync_TimeoutOnNoListener_Throws()
    {
        // Connect to a port with no listener — should throw after retries
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            await TcpDapTransport.ConnectAsync("127.0.0.1", 1, cts.Token);
            Assert.Fail("Expected an exception connecting to a closed port.");
        }
        catch (OperationCanceledException) { /* expected — test CT fired */ }
        catch (System.Net.Sockets.SocketException) { /* expected — connection refused */ }
    }

    [TestMethod]
    public async Task TcpDapTransport_ConnectAndDispose_DoesNotLeakSockets()
    {
        // Use a local listener to get a real connection
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;

        // Accept connection in background
        var acceptTask = listener.AcceptTcpClientAsync();

        TcpDapTransport transport;
        try
        {
            transport = await TcpDapTransport.ConnectAsync("127.0.0.1", port);
        }
        finally
        {
            listener.Stop();
        }

        await transport.DisposeAsync();
        var accepted = await acceptTask;
        accepted.Dispose();
    }

    // ── DebugSession model ────────────────────────────────────────────────────

    [TestMethod]
    public void DebugSession_Empty_IsNotActive()
    {
        Assert.IsFalse(DebugSession.Empty.IsActive);
        Assert.IsFalse(DebugSession.Empty.IsPaused);
    }

    [TestMethod]
    public void DebugSession_DefaultState_IsIdle()
    {
        var session = DebugSession.Empty;
        Assert.AreEqual(DebugSessionState.Idle, session.State);
    }
}
