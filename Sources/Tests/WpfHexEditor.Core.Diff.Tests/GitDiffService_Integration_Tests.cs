// ==========================================================
// Project: WpfHexEditor.Core.Diff.Tests
// File: GitDiffService_Integration_Tests.cs
// Description:
//     Integration tests for GitDiffService using a temporary git repo.
//     Tests are skipped automatically when git is not on PATH.
// ==========================================================

using System.Diagnostics;
using WpfHexEditor.Core.Diff.Services;

namespace WpfHexEditor.Core.Diff.Tests;

[TestClass]
public sealed class GitDiffService_Integration_Tests
{
    private static string? _repoDir;
    private static bool    _gitAvailable;
    private static string? _testFilePath;

    private readonly GitDiffService _svc = new();

    // ── Setup / Teardown ──────────────────────────────────────────────────────

    [ClassInitialize]
    public static void ClassInit(TestContext _)
    {
        _gitAvailable = IsGitOnPath();
        if (!_gitAvailable) return;

        _repoDir = Path.Combine(Path.GetTempPath(), $"WpfHexEditorGitTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_repoDir);

        Git("init");
        Git("config user.email test@test.com");
        Git("config user.name TestUser");

        _testFilePath = Path.Combine(_repoDir, "sample.txt");
        File.WriteAllText(_testFilePath, "line1\nline2\nline3\n");
        Git("add sample.txt");
        Git("commit -m \"initial\"");
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        if (_repoDir is null) return;
        try { Directory.Delete(_repoDir, recursive: true); } catch { }
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [TestMethod]
    public void GetRepoRoot_ReturnsRepoDir_WhenFileInsideRepo()
    {
        Skip();
        var root = _svc.GetRepoRoot(_testFilePath!);
        Assert.IsNotNull(root);
        Assert.AreEqual(
            Path.GetFullPath(_repoDir!).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar),
            StringComparer.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void IsGitRepository_ReturnsTrue_ForFileInsideRepo()
    {
        Skip();
        Assert.IsTrue(_svc.IsGitRepository(_testFilePath!));
    }

    [TestMethod]
    public void IsGitRepository_ReturnsFalse_ForTempDir()
    {
        Skip();
        var isolated = Path.Combine(Path.GetTempPath(), $"NoGit_{Guid.NewGuid():N}");
        Directory.CreateDirectory(isolated);
        try   { Assert.IsFalse(_svc.IsGitRepository(isolated)); }
        finally { Directory.Delete(isolated); }
    }

    [TestMethod]
    public async Task GetRecentCommitsAsync_ReturnsAtLeastOneCommit()
    {
        Skip();
        var commits = await _svc.GetRecentCommitsAsync(_repoDir!);
        Assert.IsTrue(commits.Count >= 1, "Expected at least one commit.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(commits[0].Hash));
        Assert.AreEqual("initial", commits[0].Message);
    }

    [TestMethod]
    public async Task ExtractRefVersionAsync_ReturnsFileContent_ForHead()
    {
        Skip();
        var tempFile = await _svc.ExtractRefVersionAsync(_repoDir!, "HEAD", _testFilePath!);
        try
        {
            Assert.IsNotNull(tempFile, "ExtractRefVersionAsync returned null for HEAD.");
            var content = await File.ReadAllTextAsync(tempFile);
            StringAssert.Contains(content, "line1");
        }
        finally
        {
            if (tempFile is not null && File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [TestMethod]
    public async Task GetBranchesAsync_ContainsDefaultBranch()
    {
        Skip();
        var branches = await _svc.GetBranchesAsync(_repoDir!);
        Assert.IsTrue(branches.Count >= 1);
        // Modern git defaults to 'main' or 'master' depending on config.
        bool hasDefault = branches.Any(b =>
            b.Equals("main",   StringComparison.OrdinalIgnoreCase) ||
            b.Equals("master", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(hasDefault, $"No main/master branch. Found: {string.Join(", ", branches)}");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void Skip()
    {
        if (!_gitAvailable)
            Assert.Inconclusive("git not found on PATH — skipping integration test.");
        if (_repoDir is null)
            Assert.Inconclusive("Test repo setup failed.");
    }

    private static void Git(string args)
    {
        using var p = Process.Start(new ProcessStartInfo("git", args)
        {
            WorkingDirectory      = _repoDir!,
            UseShellExecute       = false,
            CreateNoWindow        = true,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
        })!;
        p.WaitForExit(10_000);
    }

    private static bool IsGitOnPath()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("git", "--version")
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
            })!;
            p.WaitForExit(5_000);
            return p.ExitCode == 0;
        }
        catch { return false; }
    }
}
