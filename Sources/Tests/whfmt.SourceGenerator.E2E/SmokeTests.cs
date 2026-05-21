// Project      : whfmt.SourceGenerator.E2E
// File         : SmokeTests.cs
// Description  : End-to-end smoke tests that verify the whfmt.SourceGenerator NuGet package
//                produces a compilable, usable type from a real AdditionalFiles .whfmt.
//                These tests run against the locally packed .nupkg — not a ProjectReference.

using WhfmtE2E;

namespace whfmt.SourceGenerator.E2E;

[TestClass]
public class SmokeTests
{
    // If this file compiles, the Source Generator ran successfully and produced SimpleE2EParser.
    // Any compilation failure here means the NuGet package is broken.

    [TestMethod]
    public void GeneratedType_IsAccessible()
    {
        // The generator must have produced the WhfmtE2E.SimpleE2EParser class.
        // If this line compiles, generation succeeded.
        var type = typeof(SimpleE2EParser);
        Assert.IsNotNull(type, "SimpleE2EParser must be a real type generated from SimpleE2E.whfmt.");
    }

    [TestMethod]
    public void GeneratedType_HasExpectedFields()
    {
        var props = typeof(SimpleE2EParser).GetProperties(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        var names = props.Select(p => p.Name).ToArray();

        CollectionAssert.Contains(names, "Magic",    "SimpleE2EParser should have a Magic property.");
        CollectionAssert.Contains(names, "Version",  "SimpleE2EParser should have a Version property.");
        CollectionAssert.Contains(names, "DataSize", "SimpleE2EParser should have a DataSize property.");
    }

    [TestMethod]
    public void GeneratedType_HasParseFileMethod()
    {
        var method = typeof(SimpleE2EParser).GetMethod("ParseFile",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        Assert.IsNotNull(method, "SimpleE2EParser must have a static ParseFile(string) method.");
    }

    [TestMethod]
    public void GeneratedType_ParseFromBytes_Works()
    {
        // Build a minimal 10-byte buffer to parse (enough for the 3 fields).
        var buffer = new byte[10];
        BitConverter.GetBytes((uint)0xCAFEBABE).CopyTo(buffer, 0); // Magic
        BitConverter.GetBytes((ushort)1).CopyTo(buffer, 4);         // Version
        BitConverter.GetBytes((uint)42).CopyTo(buffer, 6);          // DataSize

        var result = SimpleE2EParser.Parse(buffer);

        Assert.AreEqual((uint)0xCAFEBABE, result.Magic,    "Magic field should round-trip.");
        Assert.AreEqual((ushort)1,        result.Version,  "Version field should round-trip.");
        Assert.AreEqual((uint)42,         result.DataSize, "DataSize field should round-trip.");
    }
}
