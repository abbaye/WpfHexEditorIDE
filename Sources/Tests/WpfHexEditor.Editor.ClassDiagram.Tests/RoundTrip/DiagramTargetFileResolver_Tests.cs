// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Tests
// File: RoundTrip/DiagramTargetFileResolver_Tests.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-11
// ==========================================================

using System.IO;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;
using WpfHexEditor.Editor.ClassDiagram.Services;

namespace WpfHexEditor.Editor.ClassDiagram.Tests.RoundTrip;

[TestClass]
public class DiagramTargetFileResolver_Tests
{
    private string _tmpDir = null!;
    private string _fileA  = null!;
    private string _fileB  = null!;
    private string _fileC  = null!;

    [TestInitialize]
    public void Setup()
    {
        _tmpDir = Path.Combine(Path.GetTempPath(), "wht-tfr-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tmpDir);
        _fileA = Path.Combine(_tmpDir, "A.cs"); File.WriteAllText(_fileA, "");
        _fileB = Path.Combine(_tmpDir, "B.cs"); File.WriteAllText(_fileB, "");
        _fileC = Path.Combine(_tmpDir, "C.cs"); File.WriteAllText(_fileC, "");
    }

    [TestCleanup]
    public void Cleanup()
    {
        try { Directory.Delete(_tmpDir, recursive: true); } catch { }
    }

    [TestMethod]
    public void Resolve_EmptyDocument_ReturnsNull()
    {
        Assert.IsNull(DiagramTargetFileResolver.Resolve(new DiagramDocument()));
        Assert.IsNull(DiagramTargetFileResolver.Resolve(null));
    }

    [TestMethod]
    public void Resolve_NoSourcePaths_ReturnsNull()
    {
        var doc = new DiagramDocument();
        doc.Classes.Add(new ClassNode { Name = "Foo" });
        doc.Classes.Add(new ClassNode { Name = "Bar" });
        Assert.IsNull(DiagramTargetFileResolver.Resolve(doc));
    }

    [TestMethod]
    public void Resolve_SelectedNodeHint_WinsWhenValid()
    {
        var doc = new DiagramDocument();
        doc.Classes.Add(new ClassNode { Name = "Foo", SourceFilePath = _fileA });
        doc.Classes.Add(new ClassNode { Name = "Bar", SourceFilePath = _fileB });
        var hint = new ClassNode { Name = "Hint", SourceFilePath = _fileC };

        Assert.AreEqual(_fileC, DiagramTargetFileResolver.Resolve(doc, hint));
    }

    [TestMethod]
    public void Resolve_HintMissingFile_FallsBackToDocStrategy()
    {
        var doc = new DiagramDocument();
        doc.Classes.Add(new ClassNode { Name = "Foo", SourceFilePath = _fileA });
        var hint = new ClassNode { Name = "Hint", SourceFilePath = Path.Combine(_tmpDir, "ghost.cs") };

        Assert.AreEqual(_fileA, DiagramTargetFileResolver.Resolve(doc, hint));
    }

    [TestMethod]
    public void Resolve_SingleDistinctPath_ReturnsThatPath()
    {
        var doc = new DiagramDocument();
        doc.Classes.Add(new ClassNode { Name = "Foo", SourceFilePath = _fileA });
        doc.Classes.Add(new ClassNode { Name = "Bar", SourceFilePath = _fileA });
        doc.Classes.Add(new ClassNode { Name = "Baz", SourceFilePath = _fileA });

        Assert.AreEqual(_fileA, DiagramTargetFileResolver.Resolve(doc));
    }

    [TestMethod]
    public void Resolve_DensestNamespaceWins()
    {
        var doc = new DiagramDocument();
        // namespace N1 has 3 classes spread over A.cs (2) and B.cs (1)
        doc.Classes.Add(new ClassNode { Name = "F1", Namespace = "N1", SourceFilePath = _fileA });
        doc.Classes.Add(new ClassNode { Name = "F2", Namespace = "N1", SourceFilePath = _fileA });
        doc.Classes.Add(new ClassNode { Name = "F3", Namespace = "N1", SourceFilePath = _fileB });
        // namespace N2 has 1 class in C.cs — fewer overall
        doc.Classes.Add(new ClassNode { Name = "F4", Namespace = "N2", SourceFilePath = _fileC });

        // N1 wins, A.cs is N1's densest file → A.cs
        Assert.AreEqual(_fileA, DiagramTargetFileResolver.Resolve(doc));
    }

    [TestMethod]
    public void Resolve_IgnoresClassesWithMissingFiles()
    {
        var doc = new DiagramDocument();
        doc.Classes.Add(new ClassNode { Name = "Ghost", SourceFilePath = Path.Combine(_tmpDir, "missing.cs") });
        doc.Classes.Add(new ClassNode { Name = "Real",  SourceFilePath = _fileB });

        Assert.AreEqual(_fileB, DiagramTargetFileResolver.Resolve(doc));
    }
}
