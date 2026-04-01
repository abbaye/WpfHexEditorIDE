using WpfHexEditor.Core.FormatDetection;

namespace WpfHexEditor.Tests.Unit;

[TestClass]
public sealed class ChecksumEngine_Tests
{
    private readonly ChecksumEngine _engine = new();

    // ── Null / empty guards ─────────────────────────────────────────────────

    [TestMethod]
    public void NullDefinitions_ReturnsEmpty()
    {
        var results = _engine.Execute(null!, new byte[] { 0x01 }, new Dictionary<string, object>());
        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public void EmptyDefinitions_ReturnsEmpty()
    {
        var results = _engine.Execute([], new byte[] { 0x01 }, new Dictionary<string, object>());
        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public void NullData_ReturnsEmpty()
    {
        var defs = new List<ChecksumDefinition>
        {
            new() { Algorithm = "crc32", DataRange = new ChecksumRange { FixedOffset = 0, FixedLength = 4 } }
        };
        var results = _engine.Execute(defs, null!, new Dictionary<string, object>());
        Assert.AreEqual(0, results.Count);
    }

    // ── CRC32 with literal expected value ───────────────────────────────────

    [TestMethod]
    public void Crc32_MatchingExpected_IsValid()
    {
        byte[] data = [0x49, 0x48, 0x44, 0x52]; // "IHDR"

        // First: compute the actual CRC32 to learn the expected value
        var validator = new ChecksumValidator();
        string computed = validator.Calculate(data, "crc32");
        Assert.IsNotNull(computed);

        // Now run the engine with that expected value
        var defs = new List<ChecksumDefinition>
        {
            new()
            {
                Name = "IHDR CRC",
                Algorithm = "crc32",
                DataRange = new ChecksumRange { FixedOffset = 0, FixedLength = 4 },
                ExpectedValue = computed,
                Severity = "error"
            }
        };

        var results = _engine.Execute(defs, data, new Dictionary<string, object>());
        Assert.AreEqual(1, results.Count);
        Assert.IsTrue(results[0].IsValid);
        Assert.AreEqual(computed, results[0].Computed);
        Assert.AreEqual("IHDR CRC", results[0].Name);
    }

    [TestMethod]
    public void Crc32_WrongExpected_Fails()
    {
        byte[] data = [0x49, 0x48, 0x44, 0x52];
        var defs = new List<ChecksumDefinition>
        {
            new()
            {
                Name = "wrong",
                Algorithm = "crc32",
                DataRange = new ChecksumRange { FixedOffset = 0, FixedLength = 4 },
                ExpectedValue = "DEADBEEF",
                Severity = "error"
            }
        };

        var results = _engine.Execute(defs, data, new Dictionary<string, object>());
        Assert.IsFalse(results[0].IsValid);
        Assert.IsTrue(results[0].ErrorMessage!.Contains("mismatch"));
    }

    // ── Missing algorithm ───────────────────────────────────────────────────

    [TestMethod]
    public void MissingAlgorithm_ReturnsFailure()
    {
        byte[] data = [0x01, 0x02, 0x03, 0x04];
        var defs = new List<ChecksumDefinition>
        {
            new()
            {
                Name = "test",
                Algorithm = null!,
                DataRange = new ChecksumRange { FixedOffset = 0, FixedLength = 4 },
                ExpectedValue = "00000000"
            }
        };

        var results = _engine.Execute(defs, data, new Dictionary<string, object>());
        Assert.AreEqual(1, results.Count);
        Assert.IsFalse(results[0].IsValid);
        Assert.IsTrue(results[0].ErrorMessage!.Contains("Algorithm not specified"));
    }

    // ── Out-of-bounds data range ────────────────────────────────────────────

    [TestMethod]
    public void OutOfBoundsRange_ReturnsFailure()
    {
        byte[] data = [0x01, 0x02];
        var defs = new List<ChecksumDefinition>
        {
            new()
            {
                Name = "oob",
                Algorithm = "crc32",
                DataRange = new ChecksumRange { FixedOffset = 0, FixedLength = 100 },
                ExpectedValue = "00000000"
            }
        };

        var results = _engine.Execute(defs, data, new Dictionary<string, object>());
        Assert.IsFalse(results[0].IsValid);
        Assert.IsTrue(results[0].ErrorMessage!.Contains("Invalid data range"));
    }

    // ── Variable-resolved offset ────────────────────────────────────────────

    [TestMethod]
    public void VariableResolvedRange_Works()
    {
        byte[] data = [0x00, 0x00, 0x49, 0x48, 0x44, 0x52, 0x00, 0x00];
        var vars = new Dictionary<string, object>
        {
            ["dataStart"] = 2L,
            ["dataLen"]   = 4L
        };

        // Compute expected CRC32 of the slice [0x49, 0x48, 0x44, 0x52]
        var validator = new ChecksumValidator();
        string expected = validator.Calculate([0x49, 0x48, 0x44, 0x52], "crc32");

        var defs = new List<ChecksumDefinition>
        {
            new()
            {
                Name = "var-test",
                Algorithm = "crc32",
                DataRange = new ChecksumRange { OffsetVar = "dataStart", LengthVar = "dataLen" },
                ExpectedValue = expected
            }
        };

        var results = _engine.Execute(defs, data, vars);
        Assert.AreEqual(1, results.Count);
        Assert.IsTrue(results[0].IsValid);
        Assert.AreEqual(expected, results[0].Computed);
    }

    // ── Severity propagation ────────────────────────────────────────────────

    [TestMethod]
    public void Severity_PropagatedFromDefinition()
    {
        byte[] data = [0x01, 0x02, 0x03, 0x04];
        var defs = new List<ChecksumDefinition>
        {
            new()
            {
                Name = "warn-test",
                Algorithm = "crc32",
                DataRange = new ChecksumRange { FixedOffset = 0, FixedLength = 4 },
                ExpectedValue = "FFFFFFFF",
                Severity = "warning"
            }
        };

        var results = _engine.Execute(defs, data, new Dictionary<string, object>());
        Assert.AreEqual("warning", results[0].Severity);
    }

    // ── Multiple definitions ────────────────────────────────────────────────

    [TestMethod]
    public void MultipleDefinitions_AllExecuted()
    {
        byte[] data = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08];
        var defs = new List<ChecksumDefinition>
        {
            new()
            {
                Name = "first",
                Algorithm = "crc32",
                DataRange = new ChecksumRange { FixedOffset = 0, FixedLength = 4 },
                ExpectedValue = "00000000"
            },
            new()
            {
                Name = "second",
                Algorithm = "md5",
                DataRange = new ChecksumRange { FixedOffset = 0, FixedLength = 8 },
                ExpectedValue = "00000000"
            }
        };

        var results = _engine.Execute(defs, data, new Dictionary<string, object>());
        Assert.AreEqual(2, results.Count);
        Assert.AreEqual("first", results[0].Name);
        Assert.AreEqual("second", results[1].Name);
    }
}
