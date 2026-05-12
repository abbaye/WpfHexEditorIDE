// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Tests
// File: RoundTrip/RoundTripResult_Tests.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-10
// ==========================================================

using WpfHexEditor.Editor.ClassDiagram.Core.RoundTrip.Abstractions;

namespace WpfHexEditor.Editor.ClassDiagram.Tests.RoundTrip;

[TestClass]
public class RoundTripResult_Tests
{
    [TestMethod]
    public void Fail_Factory_PopulatesErrorAndEmptyContent()
    {
        var r = RoundTripResult.Fail("x.cs", "boom");
        Assert.IsFalse(r.Success);
        Assert.AreEqual("x.cs",         r.FilePath);
        Assert.AreEqual("boom",          r.ErrorMessage);
        Assert.AreEqual(string.Empty,    r.ContentBefore);
        Assert.AreEqual(string.Empty,    r.ContentAfter);
    }

    [TestMethod]
    public void Success_RecordEquality_ByValue()
    {
        var a = new RoundTripResult(true, "f.cs", "X", "Y");
        var b = new RoundTripResult(true, "f.cs", "X", "Y");
        Assert.AreEqual(a, b);
    }
}
