using WpfHexEditor.Core.FormatDetection;

namespace WpfHexEditor.Tests.Unit;

[TestClass]
public sealed class AssertionRunner_Tests
{
    private readonly AssertionRunner _runner = new();

    // ── Null / empty guards ─────────────────────────────────────────────────

    [TestMethod]
    public void NullDefinitions_ReturnsEmpty()
    {
        var results = _runner.Run(null!, new Dictionary<string, object>());
        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public void EmptyDefinitions_ReturnsEmpty()
    {
        var results = _runner.Run([], new Dictionary<string, object>());
        Assert.AreEqual(0, results.Count);
    }

    // ── Equality operator ───────────────────────────────────────────────────

    [TestMethod]
    public void EqualityTrue_Passes()
    {
        var defs = new List<AssertionDefinition>
        {
            new() { Name = "magic", Expression = "magic == 42", Severity = "error" }
        };
        var vars = new Dictionary<string, object> { ["magic"] = 42L };

        var results = _runner.Run(defs, vars);
        Assert.AreEqual(1, results.Count);
        Assert.IsTrue(results[0].Passed);
    }

    [TestMethod]
    public void EqualityFalse_Fails()
    {
        var defs = new List<AssertionDefinition>
        {
            new() { Name = "magic", Expression = "magic == 42" }
        };
        var vars = new Dictionary<string, object> { ["magic"] = 99L };

        var results = _runner.Run(defs, vars);
        Assert.IsFalse(results[0].Passed);
        Assert.IsNotNull(results[0].Message);
    }

    // ── Inequality operator ─────────────────────────────────────────────────

    [TestMethod]
    public void NotEqual_Passes()
    {
        var defs = new List<AssertionDefinition>
        {
            new() { Name = "neq", Expression = "value != 0" }
        };
        var vars = new Dictionary<string, object> { ["value"] = 5L };

        Assert.IsTrue(_runner.Run(defs, vars)[0].Passed);
    }

    // ── Comparison operators ────────────────────────────────────────────────

    [TestMethod]
    public void GreaterThan_Works()
    {
        var defs = new List<AssertionDefinition>
        {
            new() { Name = "gt", Expression = "size > 0" }
        };
        var vars = new Dictionary<string, object> { ["size"] = 100L };

        Assert.IsTrue(_runner.Run(defs, vars)[0].Passed);
    }

    [TestMethod]
    public void LessThan_Works()
    {
        var defs = new List<AssertionDefinition>
        {
            new() { Name = "lt", Expression = "offset < 1000" }
        };
        var vars = new Dictionary<string, object> { ["offset"] = 500L };

        Assert.IsTrue(_runner.Run(defs, vars)[0].Passed);
    }

    [TestMethod]
    public void GreaterOrEqual_Works()
    {
        var defs = new List<AssertionDefinition>
        {
            new() { Name = "gte", Expression = "width >= 1" }
        };
        var vars = new Dictionary<string, object> { ["width"] = 1L };

        Assert.IsTrue(_runner.Run(defs, vars)[0].Passed);
    }

    [TestMethod]
    public void LessOrEqual_Works()
    {
        var defs = new List<AssertionDefinition>
        {
            new() { Name = "lte", Expression = "height <= 4096" }
        };
        var vars = new Dictionary<string, object> { ["height"] = 4096L };

        Assert.IsTrue(_runner.Run(defs, vars)[0].Passed);
    }

    // ── Bare truthy expression ──────────────────────────────────────────────

    [TestMethod]
    public void BareTruthy_NonZero_Passes()
    {
        var defs = new List<AssertionDefinition>
        {
            new() { Name = "truthy", Expression = "flags" }
        };
        var vars = new Dictionary<string, object> { ["flags"] = 1L };

        Assert.IsTrue(_runner.Run(defs, vars)[0].Passed);
    }

    [TestMethod]
    public void BareTruthy_Zero_Fails()
    {
        var defs = new List<AssertionDefinition>
        {
            new() { Name = "truthy", Expression = "flags" }
        };
        var vars = new Dictionary<string, object> { ["flags"] = 0L };

        Assert.IsFalse(_runner.Run(defs, vars)[0].Passed);
    }

    // ── Empty expression ────────────────────────────────────────────────────

    [TestMethod]
    public void EmptyExpression_PassesTrivially()
    {
        var defs = new List<AssertionDefinition>
        {
            new() { Name = "empty", Expression = "" }
        };

        var results = _runner.Run(defs, new Dictionary<string, object>());
        Assert.IsTrue(results[0].Passed);
    }

    // ── Severity and message propagation ────────────────────────────────────

    [TestMethod]
    public void Severity_PropagatedFromDefinition()
    {
        var defs = new List<AssertionDefinition>
        {
            new() { Name = "sev", Expression = "x == 0", Severity = "info" }
        };
        var vars = new Dictionary<string, object> { ["x"] = 99L };

        var results = _runner.Run(defs, vars);
        Assert.AreEqual("info", results[0].Severity);
    }

    [TestMethod]
    public void CustomMessage_UsedOnFailure()
    {
        var defs = new List<AssertionDefinition>
        {
            new() { Name = "msg", Expression = "x == 0", Message = "Expected zero!" }
        };
        var vars = new Dictionary<string, object> { ["x"] = 1L };

        var results = _runner.Run(defs, vars);
        Assert.AreEqual("Expected zero!", results[0].Message);
    }

    [TestMethod]
    public void DefaultMessage_WhenNoCustomMessage()
    {
        var defs = new List<AssertionDefinition>
        {
            new() { Name = "default", Expression = "x == 0" }
        };
        var vars = new Dictionary<string, object> { ["x"] = 1L };

        var results = _runner.Run(defs, vars);
        Assert.IsTrue(results[0].Message!.Contains("Assertion failed"));
    }

    // ── Multiple assertions ─────────────────────────────────────────────────

    [TestMethod]
    public void MultipleAssertions_AllEvaluated()
    {
        var defs = new List<AssertionDefinition>
        {
            new() { Name = "a1", Expression = "x == 1" },
            new() { Name = "a2", Expression = "y > 10" },
            new() { Name = "a3", Expression = "z != 0" }
        };
        var vars = new Dictionary<string, object>
        {
            ["x"] = 1L,
            ["y"] = 5L,   // will fail (5 > 10 is false)
            ["z"] = 42L
        };

        var results = _runner.Run(defs, vars);
        Assert.AreEqual(3, results.Count);
        Assert.IsTrue(results[0].Passed);
        Assert.IsFalse(results[1].Passed);
        Assert.IsTrue(results[2].Passed);
    }

    // ── Default severity ────────────────────────────────────────────────────

    [TestMethod]
    public void DefaultSeverity_IsWarning()
    {
        var defs = new List<AssertionDefinition>
        {
            new() { Name = "def", Expression = "x == 0" } // Severity defaults to "warning" in model
        };
        var vars = new Dictionary<string, object> { ["x"] = 1L };

        var results = _runner.Run(defs, vars);
        Assert.AreEqual("warning", results[0].Severity);
    }
}
