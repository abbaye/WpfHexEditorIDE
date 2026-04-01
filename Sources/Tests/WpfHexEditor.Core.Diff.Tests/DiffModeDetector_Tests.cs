using WpfHexEditor.Core.Diff.Models;
using WpfHexEditor.Core.Diff.Services;

namespace WpfHexEditor.Core.Diff.Tests;

[TestClass]
public sealed class DiffModeDetector_Tests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"DiffTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private string CreateFile(string name, byte[] content)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    private string CreateFile(string name, string content)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    // ── Extension-based detection ───────────────────────────────────────────

    [TestMethod]
    [DataRow(".json")]
    [DataRow(".xml")]
    [DataRow(".xaml")]
    [DataRow(".cs")]
    [DataRow(".yaml")]
    public void StructuredExtension_ReturnsSemantic(string ext)
    {
        var path = CreateFile($"test{ext}", "content");
        Assert.AreEqual(DiffMode.Semantic, DiffModeDetector.Detect(path));
    }

    [TestMethod]
    [DataRow(".txt")]
    [DataRow(".log")]
    [DataRow(".md")]
    [DataRow(".py")]
    [DataRow(".js")]
    public void TextExtension_ReturnsText(string ext)
    {
        var path = CreateFile($"test{ext}", "content");
        Assert.AreEqual(DiffMode.Text, DiffModeDetector.Detect(path));
    }

    // ── Content sniff ───────────────────────────────────────────────────────

    [TestMethod]
    public void NullBytesInContent_ReturnsBinary()
    {
        var path = CreateFile("test.dat", new byte[] { 0x48, 0x65, 0x00, 0x6C });
        Assert.AreEqual(DiffMode.Binary, DiffModeDetector.Detect(path));
    }

    [TestMethod]
    public void JsonLeader_ReturnsSemantic()
    {
        var path = CreateFile("test.dat", """{"key": "value"}""");
        Assert.AreEqual(DiffMode.Semantic, DiffModeDetector.Detect(path));
    }

    [TestMethod]
    public void XmlLeader_ReturnsSemantic()
    {
        var path = CreateFile("test.dat", "<root><item/></root>");
        Assert.AreEqual(DiffMode.Semantic, DiffModeDetector.Detect(path));
    }

    [TestMethod]
    public void ArrayLeader_ReturnsSemantic()
    {
        var path = CreateFile("test.dat", "[1, 2, 3]");
        Assert.AreEqual(DiffMode.Semantic, DiffModeDetector.Detect(path));
    }

    [TestMethod]
    public void Utf8Bom_ReturnsText()
    {
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var content = bom.Concat(System.Text.Encoding.UTF8.GetBytes("hello")).ToArray();
        var path = CreateFile("test.dat", content);
        Assert.AreEqual(DiffMode.Text, DiffModeDetector.Detect(path));
    }

    [TestMethod]
    public void PlainText_ReturnsText()
    {
        var path = CreateFile("test.dat", "just plain text content here");
        Assert.AreEqual(DiffMode.Text, DiffModeDetector.Detect(path));
    }

    [TestMethod]
    public void EmptyFile_ReturnsText()
    {
        var path = CreateFile("test.dat", Array.Empty<byte>());
        Assert.AreEqual(DiffMode.Text, DiffModeDetector.Detect(path));
    }

    // ── Pair detection ──────────────────────────────────────────────────────

    [TestMethod]
    public void PairWithSameType_ReturnsThatType()
    {
        var left  = CreateFile("a.json", "{}");
        var right = CreateFile("b.json", "{}");
        Assert.AreEqual(DiffMode.Semantic, DiffModeDetector.DetectForPair(left, right));
    }

    [TestMethod]
    public void PairWithMismatchedTypes_ReturnsBinary()
    {
        var left  = CreateFile("a.json", "{}");
        var right = CreateFile("b.txt", "hello");
        Assert.AreEqual(DiffMode.Binary, DiffModeDetector.DetectForPair(left, right));
    }

    [TestMethod]
    public void NonexistentFile_ReturnsBinary()
    {
        Assert.AreEqual(DiffMode.Binary, DiffModeDetector.Detect("/nonexistent/path.txt"));
    }

    // ── Whitespace before JSON/XML leader ───────────────────────────────────

    [TestMethod]
    public void WhitespaceThenJsonLeader_ReturnsSemantic()
    {
        var path = CreateFile("test.dat", "  \n  {\"a\": 1}");
        Assert.AreEqual(DiffMode.Semantic, DiffModeDetector.Detect(path));
    }
}
