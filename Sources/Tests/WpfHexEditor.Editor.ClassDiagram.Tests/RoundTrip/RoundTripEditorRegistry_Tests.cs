// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Tests
// File: RoundTrip/RoundTripEditorRegistry_Tests.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-10
// Description:
//     Unit tests for RoundTripEditorRegistry — registration, lookup by
//     language id and file path, idempotence, and test isolation.
// ==========================================================

using WpfHexEditor.Editor.ClassDiagram.Core.RoundTrip;
using WpfHexEditor.Editor.ClassDiagram.Core.RoundTrip.Abstractions;

namespace WpfHexEditor.Editor.ClassDiagram.Tests.RoundTrip;

[TestClass]
public class RoundTripEditorRegistry_Tests
{
    [TestInitialize]
    public void Reset() => RoundTripEditorRegistry.ResetForTests();

    [TestCleanup]
    public void Cleanup() => RoundTripEditorRegistry.ResetForTests();

    [TestMethod]
    public void Register_ThenLookupByLanguageId_Returns()
    {
        var ed = new CSharpRoundTripEditor();
        RoundTripEditorRegistry.Register(ed);

        Assert.AreSame(ed, RoundTripEditorRegistry.TryGetByLanguageId("csharp"));
    }

    [TestMethod]
    public void LookupByLanguageId_IsCaseInsensitive()
    {
        RoundTripEditorRegistry.Register(new CSharpRoundTripEditor());
        Assert.IsNotNull(RoundTripEditorRegistry.TryGetByLanguageId("CSharp"));
        Assert.IsNotNull(RoundTripEditorRegistry.TryGetByLanguageId("CSHARP"));
    }

    [TestMethod]
    public void LookupByFilePath_MatchesExtension()
    {
        var ed = new CSharpRoundTripEditor();
        RoundTripEditorRegistry.Register(ed);

        Assert.AreSame(ed, RoundTripEditorRegistry.TryGetByFilePath(@"C:\proj\Foo.cs"));
        Assert.AreSame(ed, RoundTripEditorRegistry.TryGetByFilePath("rel/path/x.CS"));
    }

    [TestMethod]
    public void LookupByFilePath_UnknownExtension_ReturnsNull()
    {
        RoundTripEditorRegistry.Register(new CSharpRoundTripEditor());
        Assert.IsNull(RoundTripEditorRegistry.TryGetByFilePath("Foo.unknown"));
        Assert.IsNull(RoundTripEditorRegistry.TryGetByFilePath("Foo"));
    }

    [TestMethod]
    public void Register_IsIdempotentOnLanguageId_LastWins()
    {
        var a = new CSharpRoundTripEditor();
        var b = new CSharpRoundTripEditor();
        RoundTripEditorRegistry.Register(a);
        RoundTripEditorRegistry.Register(b);

        Assert.AreSame(b, RoundTripEditorRegistry.TryGetByLanguageId("csharp"));
    }

    [TestMethod]
    public void Register_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            RoundTripEditorRegistry.Register(null!));
    }

    [TestMethod]
    public void All_ReturnsRegisteredEditors_StableOrder()
    {
        RoundTripEditorRegistry.Register(new CSharpRoundTripEditor());
        var snapshot = RoundTripEditorRegistry.All();
        Assert.AreEqual(1, snapshot.Count);
        Assert.AreEqual("csharp", snapshot[0].LanguageId);
    }
}
