// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Tests
// File: RoundTrip/VisualBasicRoundTripEditor_Tests.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-11
// Description:
//     Phase A (ADR-037) — VB parity unit tests, mirroring the C#
//     CSharpRoundTripEditor_Tests structure 1:1 where the semantics
//     map cleanly across languages.
// ==========================================================

using WpfHexEditor.Editor.ClassDiagram.Core.RoundTrip;
using WpfHexEditor.Editor.ClassDiagram.Core.RoundTrip.Abstractions;

namespace WpfHexEditor.Editor.ClassDiagram.Tests.RoundTrip;

[TestClass]
public class VisualBasicRoundTripEditor_Tests
{
    private VisualBasicRoundTripEditor _editor = null!;

    [TestInitialize]
    public void Setup() => _editor = new VisualBasicRoundTripEditor();

    private const string SimpleClass = @"
Namespace N
    Public Class Foo
        Public Property Bar As Integer
    End Class
End Namespace
";

    // ── Metadata ────────────────────────────────────────────────────────────

    [TestMethod]
    public void LanguageMetadata_IsVB()
    {
        Assert.AreEqual("vb", _editor.LanguageId);
        Assert.AreEqual("Visual Basic", _editor.DisplayName);
        CollectionAssert.AreEqual(new[] { ".vb" }, _editor.FileExtensions.ToArray());
    }

    // ── AddMember ───────────────────────────────────────────────────────────

    [TestMethod]
    public async Task AddMember_AppendsToClass()
    {
        var edit = new AddMember("Public Property Name As String") { TargetTypeFullName = "Foo" };
        var res  = await _editor.ApplyAsync("Foo.vb", SimpleClass, edit);

        Assert.IsTrue(res.Success, res.ErrorMessage);
        StringAssert.Contains(res.ContentAfter, "Name");
        StringAssert.Contains(res.ContentAfter, "Bar");
    }

    [TestMethod]
    public async Task AddMember_TargetTypeMissing_Fails()
    {
        var edit = new AddMember("Public Property X As Integer") { TargetTypeFullName = "DoesNotExist" };
        var res  = await _editor.ApplyAsync("Foo.vb", SimpleClass, edit);

        Assert.IsFalse(res.Success);
    }

    // ── RemoveMember ────────────────────────────────────────────────────────

    [TestMethod]
    public async Task RemoveMember_DropsTheMember()
    {
        var edit = new RemoveMember("Bar") { TargetTypeFullName = "Foo" };
        var res  = await _editor.ApplyAsync("Foo.vb", SimpleClass, edit);

        Assert.IsTrue(res.Success, res.ErrorMessage);
        Assert.IsFalse(res.ContentAfter.Contains("Bar"));
    }

    // ── RenameMember ────────────────────────────────────────────────────────

    [TestMethod]
    public async Task RenameMember_RenamesIdentifier()
    {
        var edit = new RenameMember("Bar", "Baz") { TargetTypeFullName = "Foo" };
        var res  = await _editor.ApplyAsync("Foo.vb", SimpleClass, edit);

        Assert.IsTrue(res.Success, res.ErrorMessage);
        StringAssert.Contains(res.ContentAfter, "Baz");
        Assert.IsFalse(res.ContentAfter.Contains("Bar"));
    }

    // ── ChangeVisibility ────────────────────────────────────────────────────

    [TestMethod]
    public async Task ChangeVisibility_PublicToPrivate()
    {
        var edit = new ChangeVisibility("Bar", MemberVisibilityKind.Private) { TargetTypeFullName = "Foo" };
        var res  = await _editor.ApplyAsync("Foo.vb", SimpleClass, edit);

        Assert.IsTrue(res.Success, res.ErrorMessage);
        StringAssert.Contains(res.ContentAfter, "Private");
    }

    [TestMethod]
    public async Task ChangeVisibility_ToInternal_UsesFriendKeyword()
    {
        var edit = new ChangeVisibility("Bar", MemberVisibilityKind.Internal) { TargetTypeFullName = "Foo" };
        var res  = await _editor.ApplyAsync("Foo.vb", SimpleClass, edit);

        Assert.IsTrue(res.Success, res.ErrorMessage);
        StringAssert.Contains(res.ContentAfter, "Friend");
    }

    // ── RenameType ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task RenameType_RenamesClassDeclaration()
    {
        var edit = new RenameType("Foo2") { TargetTypeFullName = "Foo" };
        var res  = await _editor.ApplyAsync("Foo.vb", SimpleClass, edit);

        Assert.IsTrue(res.Success, res.ErrorMessage);
        StringAssert.Contains(res.ContentAfter, "Class Foo2");
    }

    // ── AddType ─────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task AddType_AppendsClassDeclaration()
    {
        var edit = new AddType("Public Class NewOne\nEnd Class") { TargetTypeFullName = "NewOne" };
        var res  = await _editor.ApplyAsync("Foo.vb", SimpleClass, edit);

        Assert.IsTrue(res.Success, res.ErrorMessage);
        StringAssert.Contains(res.ContentAfter, "Class NewOne");
        StringAssert.Contains(res.ContentAfter, "Class Foo");
    }

    [TestMethod]
    public async Task RemoveType_DropsClassDeclaration()
    {
        var edit = new RemoveType() { TargetTypeFullName = "Foo" };
        var res  = await _editor.ApplyAsync("Foo.vb", SimpleClass, edit);

        Assert.IsTrue(res.Success, res.ErrorMessage);
        Assert.IsFalse(res.ContentAfter.Contains("Class Foo"));
    }

    // ── Interface implements ───────────────────────────────────────────────

    [TestMethod]
    public async Task AddInterface_AppendsImplementsClause()
    {
        var edit = new AddInterface("IDisposable") { TargetTypeFullName = "Foo" };
        var res  = await _editor.ApplyAsync("Foo.vb", SimpleClass, edit);

        Assert.IsTrue(res.Success, res.ErrorMessage);
        StringAssert.Contains(res.ContentAfter, "IDisposable");
    }

    // ── Formatting preservation ────────────────────────────────────────────

    [TestMethod]
    public async Task Formatter_ProducesValidVB()
    {
        var edit = new AddMember("Public Property Name As String") { TargetTypeFullName = "Foo" };
        var res  = await _editor.ApplyAsync("Foo.vb", SimpleClass, edit);

        Assert.IsTrue(res.Success, res.ErrorMessage);
        var tree = Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxTree.ParseText(res.ContentAfter);
        var diags = tree.GetDiagnostics().Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ToList();
        Assert.AreEqual(0, diags.Count, string.Join("\n", diags.Select(d => d.GetMessage())));
    }

    // ── Registry routing ───────────────────────────────────────────────────

    [TestMethod]
    public void Registry_RoutesVbExtension_ToVisualBasicEditor()
    {
        RoundTripEditorRegistry.ResetForTests();
        RoundTripEditorRegistry.Register(new VisualBasicRoundTripEditor());
        var resolved = RoundTripEditorRegistry.TryGetByFilePath(@"C:\path\Foo.vb");
        Assert.IsNotNull(resolved);
        Assert.AreEqual("vb", resolved!.LanguageId);
        RoundTripEditorRegistry.ResetForTests();
    }
}
