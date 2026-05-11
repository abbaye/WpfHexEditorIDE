// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Tests
// File: RoundTrip/RoundTripScope_Tests.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-11
// Description:
//     Phase 1B-6 integration tests — exercise the editor-side
//     RoundTripScope gateway and verify that round-trip edits
//     reach the actual source file on disk through the same
//     CSharpRoundTripEditor pipeline the production hook uses.
// ==========================================================

using System.IO;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;
using WpfHexEditor.Editor.ClassDiagram.Core.RoundTrip;
using WpfHexEditor.Editor.ClassDiagram.Core.RoundTrip.Abstractions;
using WpfHexEditor.Editor.ClassDiagram.Services;

namespace WpfHexEditor.Editor.ClassDiagram.Tests.RoundTrip;

[TestClass]
public class RoundTripScope_Tests
{
    private string _tmpDir = null!;
    private string _tmpCs  = null!;
    private ClassDiagramUndoManager _undo = null!;
    private readonly CSharpRoundTripEditor _editor = new();

    [TestInitialize]
    public void Setup()
    {
        _tmpDir = Path.Combine(Path.GetTempPath(), "wht-rts-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tmpDir);
        _tmpCs = Path.Combine(_tmpDir, "Sample.cs");
        File.WriteAllText(_tmpCs, "namespace N { public class Foo { public int X { get; set; } } }");

        _undo = new ClassDiagramUndoManager();

        // Install the applier (production wires the same delegate from the plugin).
        RoundTripScope.Applier = async (path, edit, ct) =>
        {
            string src = await File.ReadAllTextAsync(path, ct);
            var res = await _editor.ApplyAsync(path, src, edit, ct);
            if (res.Success) await File.WriteAllTextAsync(path, res.ContentAfter, ct);
            return res;
        };
    }

    [TestCleanup]
    public void Cleanup()
    {
        RoundTripScope.ResetForTests();
        try { Directory.Delete(_tmpDir, recursive: true); } catch { /* best effort */ }
    }

    [TestMethod]
    public void HasSource_NoPath_ReturnsFalse()
    {
        var node = new ClassNode { Name = "Foo", Id = "id1" };
        Assert.IsFalse(RoundTripScope.HasSource(node));
    }

    [TestMethod]
    public void HasSource_ValidPath_ReturnsTrue()
    {
        var node = new ClassNode { Name = "Foo", Id = "id1", SourceFilePath = _tmpCs };
        Assert.IsTrue(RoundTripScope.HasSource(node));
    }

    [TestMethod]
    public void TryApply_NoApplier_ReturnsFalse()
    {
        RoundTripScope.Applier = null;
        var node = new ClassNode { Name = "Foo", SourceFilePath = _tmpCs };
        bool result = RoundTripScope.TryApply(
            node,
            new RenameType("Bar") { TargetTypeFullName = "Foo" },
            _undo,
            "test");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task TryApply_RenameType_WritesSource()
    {
        var node = new ClassNode { Name = "Foo", SourceFilePath = _tmpCs };

        bool queued = RoundTripScope.TryApply(
            node,
            new RenameType("Bar") { TargetTypeFullName = "Foo" },
            _undo,
            "rename");
        Assert.IsTrue(queued);

        // Fire-and-forget — give it a moment to complete.
        await Task.Delay(300);

        string after = await File.ReadAllTextAsync(_tmpCs);
        StringAssert.Contains(after, "class Bar");
    }

    [TestMethod]
    public async Task TryApply_AddMember_AppendsToSource()
    {
        var node = new ClassNode { Name = "Foo", SourceFilePath = _tmpCs };

        bool queued = RoundTripScope.TryApply(
            node,
            new AddMember("public string Name { get; set; }") { TargetTypeFullName = "Foo" },
            _undo,
            "add");
        Assert.IsTrue(queued);

        await Task.Delay(300);

        string after = await File.ReadAllTextAsync(_tmpCs);
        StringAssert.Contains(after, "Name");
    }

    [TestMethod]
    public async Task TryApply_PushesSourceFileBackupUndo()
    {
        var node = new ClassNode { Name = "Foo", SourceFilePath = _tmpCs };
        int beforeCount = _undo.UndoCount;

        bool queued = RoundTripScope.TryApply(
            node,
            new RenameType("Bar") { TargetTypeFullName = "Foo" },
            _undo,
            "rename");
        Assert.IsTrue(queued);
        await Task.Delay(300);

        Assert.IsTrue(_undo.UndoCount > beforeCount, "expected an undo entry to be pushed");
    }

    [TestMethod]
    public async Task TryApply_UndoRestoresSourceBytes()
    {
        var node = new ClassNode { Name = "Foo", SourceFilePath = _tmpCs };
        string original = await File.ReadAllTextAsync(_tmpCs);

        RoundTripScope.TryApply(
            node,
            new RenameType("Bar") { TargetTypeFullName = "Foo" },
            _undo,
            "rename");
        await Task.Delay(300);

        _undo.Undo();

        string restored = await File.ReadAllTextAsync(_tmpCs);
        Assert.AreEqual(original, restored);
    }

    [TestMethod]
    public async Task TryApply_Member_UsesMemberPathWhenSet()
    {
        var node = new ClassNode { Name = "Foo", SourceFilePath = _tmpCs };
        var member = new ClassMember
        {
            Name = "X",
            TypeName = "int",
            Kind = MemberKind.Property,
            Visibility = MemberVisibility.Public,
            SourceFilePath = _tmpCs
        };

        bool queued = RoundTripScope.TryApply(
            node, member,
            new RenameMember("X", "Y") { TargetTypeFullName = "Foo" },
            _undo,
            "rename member");
        Assert.IsTrue(queued);
        await Task.Delay(300);

        string after = await File.ReadAllTextAsync(_tmpCs);
        StringAssert.Contains(after, "int Y");
    }
}
