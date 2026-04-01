// ==========================================================
// Project: WpfHexEditor.PluginHost.Tests
// File: PluginIntegrityService_Tests.cs
// Description:
//     Tests for PluginIntegrityService SHA-256 hash computation and verification.
// ==========================================================

using System.IO;
using System.Security.Cryptography;
using WpfHexEditor.PluginHost.Services;

namespace WpfHexEditor.PluginHost.Tests;

[TestClass]
public sealed class PluginIntegrityService_Tests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup() => _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    private string CreateTempFile(byte[] content, string name = "test.bin")
    {
        Directory.CreateDirectory(_tempDir);
        var path = Path.Combine(_tempDir, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    // ── ComputeAsync ─────────────────────────────────────────────────────────

    [TestMethod]
    public async Task ComputeAsync_EmptyFile_ReturnsKnownHash()
    {
        var path = CreateTempFile([]);
        var hash = await PluginIntegrityService.ComputeAsync(path);

        // SHA-256 of empty input is well-known
        Assert.AreEqual("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", hash);
    }

    [TestMethod]
    public async Task ComputeAsync_KnownContent_ReturnsCorrectHash()
    {
        var content = "Hello, WpfHexEditor!"u8.ToArray();
        var path    = CreateTempFile(content);

        var actual   = await PluginIntegrityService.ComputeAsync(path);
        var expected = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public async Task ComputeAsync_ReturnsLowercaseHex()
    {
        var path = CreateTempFile("abc"u8.ToArray());
        var hash = await PluginIntegrityService.ComputeAsync(path);

        Assert.IsTrue(hash == hash.ToLowerInvariant(), "Hash must be lowercase hex.");
    }

    [TestMethod]
    public async Task ComputeAsync_Returns64Characters()
    {
        var path = CreateTempFile([0x01, 0x02, 0x03]);
        var hash = await PluginIntegrityService.ComputeAsync(path);

        Assert.AreEqual(64, hash.Length);
    }

    [TestMethod]
    public async Task ComputeAsync_DifferentContent_ReturnsDifferentHash()
    {
        var path1 = CreateTempFile("content-A"u8.ToArray(), "a.bin");
        var path2 = CreateTempFile("content-B"u8.ToArray(), "b.bin");

        var hash1 = await PluginIntegrityService.ComputeAsync(path1);
        var hash2 = await PluginIntegrityService.ComputeAsync(path2);

        Assert.AreNotEqual(hash1, hash2);
    }

    // ── VerifyAsync ───────────────────────────────────────────────────────────

    [TestMethod]
    public async Task VerifyAsync_MatchingHash_ReturnsTrue()
    {
        var content  = "plugin-payload"u8.ToArray();
        var path     = CreateTempFile(content);
        var expected = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

        Assert.IsTrue(await PluginIntegrityService.VerifyAsync(path, expected));
    }

    [TestMethod]
    public async Task VerifyAsync_MismatchedHash_ReturnsFalse()
    {
        var path = CreateTempFile("legitimate-content"u8.ToArray());

        Assert.IsFalse(await PluginIntegrityService.VerifyAsync(path, "deadbeef" + new string('0', 56)));
    }

    [TestMethod]
    public async Task VerifyAsync_EmptyExpectedHash_ReturnsTrue()
    {
        // Empty Sha256 means no checksum provided → skip verification
        var path = CreateTempFile([0xFF, 0xFE]);
        Assert.IsTrue(await PluginIntegrityService.VerifyAsync(path, string.Empty));
    }

    [TestMethod]
    public async Task VerifyAsync_WhitespaceExpectedHash_ReturnsTrue()
    {
        var path = CreateTempFile([0xAA]);
        Assert.IsTrue(await PluginIntegrityService.VerifyAsync(path, "   "));
    }

    [TestMethod]
    public async Task VerifyAsync_HashCaseInsensitive_Matches()
    {
        var content  = "case-test"u8.ToArray();
        var path     = CreateTempFile(content);
        var upper    = Convert.ToHexString(SHA256.HashData(content)); // uppercase

        Assert.IsTrue(await PluginIntegrityService.VerifyAsync(path, upper));
    }

    [TestMethod]
    public async Task VerifyAsync_FileNotFound_ThrowsIOException()
    {
        try
        {
            await PluginIntegrityService.VerifyAsync(
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "nonexistent.bin"),
                "abc123");
            Assert.Fail("Expected IOException was not thrown.");
        }
        catch (FileNotFoundException) { /* expected */ }
        catch (DirectoryNotFoundException) { /* Windows throws this when parent dir missing */ }
    }
}
