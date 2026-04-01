namespace WpfHexEditor.Core.Workspaces.Tests;

[TestClass]
public sealed class WorkspaceSerializer_Tests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"WsTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private string TempPath(string name = "test.whidews")
        => Path.Combine(_tempDir, name);

    // ── Round-trip tests ────────────────────────────────────────────────────

    [TestMethod]
    public async Task RoundTrip_FullState_PreservesAllFields()
    {
        var state = new WorkspaceState
        {
            Manifest = new WorkspaceManifest("TestWorkspace", "1.0", "2026-03-26", "TestUser"),
            Layout   = """{"dockLayout": "serialized"}""",
            Solution = new WorkspaceSolutionState(@"C:\Projects\Test.whsln"),
            Files =
            [
                new OpenFileEntry(@"C:\file1.cs", "code-editor", 42, 10),
                new OpenFileEntry(@"C:\file2.bin", "hex-editor", 0, 0),
            ],
            Settings = new WorkspaceSettingsOverride("Cyberpunk"),
        };

        var path = TempPath();
        await WorkspaceSerializer.WriteAsync(path, state);
        var loaded = await WorkspaceSerializer.ReadAsync(path);

        Assert.AreEqual("TestWorkspace", loaded.Manifest.Name);
        Assert.AreEqual("1.0", loaded.Manifest.Version);
        Assert.AreEqual("2026-03-26", loaded.Manifest.CreatedAt);
        Assert.AreEqual("TestUser", loaded.Manifest.Author);
        Assert.AreEqual(state.Layout, loaded.Layout);
        Assert.AreEqual(@"C:\Projects\Test.whsln", loaded.Solution.SolutionPath);
        Assert.AreEqual(2, loaded.Files.Count);
        Assert.AreEqual(@"C:\file1.cs", loaded.Files[0].Path);
        Assert.AreEqual("code-editor", loaded.Files[0].EditorId);
        Assert.AreEqual(42, loaded.Files[0].CursorLine);
        Assert.AreEqual(10, loaded.Files[0].CursorCol);
        Assert.AreEqual("Cyberpunk", loaded.Settings.ThemeName);
    }

    [TestMethod]
    public async Task RoundTrip_MinimalState_UsesDefaults()
    {
        var state = new WorkspaceState
        {
            Manifest = new WorkspaceManifest("Minimal"),
            Layout   = "",
            Solution = new WorkspaceSolutionState(null),
            Files    = [],
            Settings = new WorkspaceSettingsOverride(null),
        };

        var path = TempPath();
        await WorkspaceSerializer.WriteAsync(path, state);
        var loaded = await WorkspaceSerializer.ReadAsync(path);

        Assert.AreEqual("Minimal", loaded.Manifest.Name);
        Assert.AreEqual(string.Empty, loaded.Layout);
        Assert.IsNull(loaded.Solution.SolutionPath);
        Assert.AreEqual(0, loaded.Files.Count);
        Assert.IsNull(loaded.Settings.ThemeName);
    }

    [TestMethod]
    public async Task RoundTrip_ManyFiles_PreservesOrder()
    {
        var files = Enumerable.Range(0, 50)
            .Select(i => new OpenFileEntry($@"C:\file{i:D3}.txt", null, i, 0))
            .ToList();

        var state = new WorkspaceState
        {
            Manifest = new WorkspaceManifest("OrderTest"),
            Files    = files,
        };

        var path = TempPath();
        await WorkspaceSerializer.WriteAsync(path, state);
        var loaded = await WorkspaceSerializer.ReadAsync(path);

        Assert.AreEqual(50, loaded.Files.Count);
        for (int i = 0; i < 50; i++)
        {
            Assert.AreEqual($@"C:\file{i:D3}.txt", loaded.Files[i].Path);
            Assert.AreEqual(i, loaded.Files[i].CursorLine);
        }
    }

    // ── Edge cases ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task WriteToNonexistentSubdir_CreatesDirectory()
    {
        var path = Path.Combine(_tempDir, "sub", "deep", "test.whidews");

        var state = new WorkspaceState { Manifest = new WorkspaceManifest("SubDir") };
        await WorkspaceSerializer.WriteAsync(path, state);

        Assert.IsTrue(File.Exists(path));

        var loaded = await WorkspaceSerializer.ReadAsync(path);
        Assert.AreEqual("SubDir", loaded.Manifest.Name);
    }

    [TestMethod]
    public async Task OutputFile_IsValidZip()
    {
        var state = new WorkspaceState { Manifest = new WorkspaceManifest("ZipCheck") };
        var path = TempPath();
        await WorkspaceSerializer.WriteAsync(path, state);

        using var fs = File.OpenRead(path);
        using var zip = new System.IO.Compression.ZipArchive(fs, System.IO.Compression.ZipArchiveMode.Read);

        var entryNames = zip.Entries.Select(e => e.FullName).OrderBy(n => n).ToList();
        CollectionAssert.Contains(entryNames, "manifest.json");
        CollectionAssert.Contains(entryNames, "layout.json");
        CollectionAssert.Contains(entryNames, "solution.json");
        CollectionAssert.Contains(entryNames, "openfiles.json");
        CollectionAssert.Contains(entryNames, "settings.json");
    }

    [TestMethod]
    public async Task LayoutJson_PreservedAsRawString()
    {
        var layout = """
        {
            "root": {
                "type": "split",
                "children": [
                    {"id": "panel-1", "width": 300},
                    {"id": "panel-2", "width": 700}
                ]
            }
        }
        """;

        var state = new WorkspaceState
        {
            Manifest = new WorkspaceManifest("LayoutTest"),
            Layout   = layout,
        };

        var path = TempPath();
        await WorkspaceSerializer.WriteAsync(path, state);
        var loaded = await WorkspaceSerializer.ReadAsync(path);

        Assert.AreEqual(layout, loaded.Layout);
    }

    // ── Cancellation ────────────────────────────────────────────────────────

    [TestMethod]
    public async Task Write_CancelledToken_ThrowsOrAborts()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var state = new WorkspaceState { Manifest = new WorkspaceManifest("Cancel") };
        var path = TempPath();

        bool threw = false;
        try
        {
            await WorkspaceSerializer.WriteAsync(path, state, cts.Token);
        }
        catch (OperationCanceledException)
        {
            threw = true;
        }

        Assert.IsTrue(threw, "Expected cancellation exception");
    }
}
