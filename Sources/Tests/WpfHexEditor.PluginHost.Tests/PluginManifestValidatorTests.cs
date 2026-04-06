// ==========================================================
// Project: WpfHexEditor.PluginHost.Tests
// File: PluginManifestValidatorTests.cs
// Contributors: Claude Sonnet 4.6
// Description:
//     Tests for PluginManifestValidator — required fields, version
//     constraints, assembly hash verification, signature validation.
// ==========================================================

using System.IO;
using System.Security.Cryptography;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.PluginHost.Tests;

[TestClass]
public sealed class PluginManifestValidatorTests
{
    private static readonly Version IdeV  = new(1, 0, 0);
    private static readonly Version SdkV  = new(1, 0, 0);

    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
        => _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private PluginManifestValidator MakeValidator(Version? ide = null, Version? sdk = null)
        => new(ide ?? IdeV, sdk ?? SdkV);

    private static PluginManifest ValidManifest() => new()
    {
        Id         = "test.plugin",
        Name       = "Test Plugin",
        Version    = "1.0.0",
        EntryPoint = "Test.Plugin.Main",
        SdkVersion = "1.0.0"
    };

    // ── Required fields ───────────────────────────────────────────────────────

    [TestMethod]
    public void Validate_ValidManifest_NoErrors()
    {
        var result = MakeValidator().Validate(ValidManifest(), _tempDir);
        Assert.IsTrue(result.IsValid, string.Join("; ", result.Errors));
    }

    [TestMethod]
    public void Validate_MissingId_ReportsError()
    {
        var m = ValidManifest(); m.Id = "";
        var result = MakeValidator().Validate(m, _tempDir);
        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("'id'")));
    }

    [TestMethod]
    public void Validate_MissingName_ReportsError()
    {
        var m = ValidManifest(); m.Name = "   ";
        var result = MakeValidator().Validate(m, _tempDir);
        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("'name'")));
    }

    [TestMethod]
    public void Validate_MissingVersion_ReportsError()
    {
        var m = ValidManifest(); m.Version = "";
        var result = MakeValidator().Validate(m, _tempDir);
        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("'version'")));
    }

    [TestMethod]
    public void Validate_MissingEntryPoint_ReportsError()
    {
        var m = ValidManifest(); m.EntryPoint = "";
        var result = MakeValidator().Validate(m, _tempDir);
        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("'entryPoint'")));
    }

    [TestMethod]
    public void Validate_MissingSdkVersion_ReportsError()
    {
        var m = ValidManifest(); m.SdkVersion = "";
        var result = MakeValidator().Validate(m, _tempDir);
        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("'sdkVersion'")));
    }

    [TestMethod]
    public void Validate_InvalidVersionString_ReportsError()
    {
        var m = ValidManifest(); m.Version = "not-semver";
        var result = MakeValidator().Validate(m, _tempDir);
        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("not a valid SemVer")));
    }

    [TestMethod]
    public void Validate_InvalidSdkVersionString_ReportsError()
    {
        var m = ValidManifest(); m.SdkVersion = "abc";
        var result = MakeValidator().Validate(m, _tempDir);
        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("sdkVersion")));
    }

    [TestMethod]
    public void Validate_PrereleaseVersionSuffix_ParsedCorrectly()
    {
        // "1.0.0-beta.1" should parse the "1.0.0" part without error
        var m = ValidManifest(); m.Version = "1.0.0-beta.1";
        var result = MakeValidator().Validate(m, _tempDir);
        Assert.IsTrue(result.IsValid, string.Join("; ", result.Errors));
    }

    // ── Version constraints ───────────────────────────────────────────────────

    [TestMethod]
    public void Validate_MinIDEVersion_SatisfiedByCurrentIDE_NoError()
    {
        var m = ValidManifest(); m.MinIDEVersion = "0.9.0";
        var result = MakeValidator(ide: new Version(1, 0, 0)).Validate(m, _tempDir);
        Assert.IsTrue(result.IsValid, string.Join("; ", result.Errors));
    }

    [TestMethod]
    public void Validate_MinIDEVersion_AboveCurrentIDE_ReportsError()
    {
        var m = ValidManifest(); m.MinIDEVersion = "2.0.0";
        var result = MakeValidator(ide: new Version(1, 0, 0)).Validate(m, _tempDir);
        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("IDE version")));
    }

    [TestMethod]
    public void Validate_MinSDKVersion_Satisfied_NoError()
    {
        var m = ValidManifest(); m.MinSDKVersion = "0.8.0";
        var result = MakeValidator(sdk: new Version(1, 0, 0)).Validate(m, _tempDir);
        Assert.IsTrue(result.IsValid, string.Join("; ", result.Errors));
    }

    [TestMethod]
    public void Validate_MinSDKVersion_NotSatisfied_ReportsError()
    {
        var m = ValidManifest(); m.MinSDKVersion = "2.0.0";
        var result = MakeValidator(sdk: new Version(1, 0, 0)).Validate(m, _tempDir);
        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("SDK version")));
    }

    [TestMethod]
    public void Validate_MaxSDKVersion_Satisfied_NoError()
    {
        var m = ValidManifest(); m.MaxSDKVersion = "2.0.0";
        var result = MakeValidator(sdk: new Version(1, 0, 0)).Validate(m, _tempDir);
        Assert.IsTrue(result.IsValid, string.Join("; ", result.Errors));
    }

    [TestMethod]
    public void Validate_MaxSDKVersion_Exceeded_ReportsError()
    {
        var m = ValidManifest(); m.MaxSDKVersion = "0.9.0";
        var result = MakeValidator(sdk: new Version(1, 0, 0)).Validate(m, _tempDir);
        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("SDK version")));
    }

    // ── Assembly hash ─────────────────────────────────────────────────────────

    [TestMethod]
    public void Validate_CorrectAssemblyHash_NoError()
    {
        var content = "plugin-dll-content"u8.ToArray();
        Directory.CreateDirectory(_tempDir);
        var dll = Path.Combine(_tempDir, "plugin.dll");
        File.WriteAllBytes(dll, content);

        var hash = Convert.ToHexString(SHA256.HashData(content));
        var m = ValidManifest();
        m.Assembly = new PluginAssemblyInfo { File = "plugin.dll", Sha256 = hash };

        var result = MakeValidator().Validate(m, _tempDir);
        Assert.IsTrue(result.IsValid, string.Join("; ", result.Errors));
    }

    [TestMethod]
    public void Validate_WrongAssemblyHash_ReportsError()
    {
        var content = "legit-content"u8.ToArray();
        Directory.CreateDirectory(_tempDir);
        var dll = Path.Combine(_tempDir, "plugin.dll");
        File.WriteAllBytes(dll, content);

        var m = ValidManifest();
        m.Assembly = new PluginAssemblyInfo { File = "plugin.dll", Sha256 = new string('a', 64) };

        var result = MakeValidator().Validate(m, _tempDir);
        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("hash mismatch")));
    }

    [TestMethod]
    public void Validate_MissingAssemblyFile_ReportsError()
    {
        var m = ValidManifest();
        m.Assembly = new PluginAssemblyInfo { File = "missing.dll", Sha256 = new string('0', 64) };

        var result = MakeValidator().Validate(m, _tempDir);
        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("not found")));
    }

    [TestMethod]
    public void Validate_NoAssemblyHash_Skipped_NoError()
    {
        // Build manifest — no sha256 → hash check skipped
        var m = ValidManifest();
        m.Assembly = new PluginAssemblyInfo { File = "plugin.dll" };

        var result = MakeValidator().Validate(m, _tempDir);
        Assert.IsTrue(result.IsValid, string.Join("; ", result.Errors));
    }

    // ── Signature ─────────────────────────────────────────────────────────────

    [TestMethod]
    public void Validate_IsSigned_False_SignatureSkipped()
    {
        var m = ValidManifest();
        m.Signature = new PluginSignatureInfo { IsSigned = false };

        var result = MakeValidator().Validate(m, _tempDir);
        Assert.IsTrue(result.IsValid, string.Join("; ", result.Errors));
    }

    [TestMethod]
    public void Validate_IsSigned_True_MissingSignatureFile_ReportsError()
    {
        var m = ValidManifest();
        m.Signature = new PluginSignatureInfo
        {
            IsSigned      = true,
            SignatureFile = "plugin.sig"
        };
        // sig file not created → file not found error

        var result = MakeValidator().Validate(m, _tempDir);
        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("Signature file not found")));
    }

    [TestMethod]
    public void Validate_IsSigned_True_ValidSignatureFile_NoError()
    {
        Directory.CreateDirectory(_tempDir);
        var sigPath = Path.Combine(_tempDir, "plugin.sig");
        // sig must be at least 8 bytes
        File.WriteAllBytes(sigPath, new byte[16]);

        var m = ValidManifest();
        m.Signature = new PluginSignatureInfo
        {
            IsSigned      = true,
            SignatureFile = "plugin.sig"
        };

        var result = MakeValidator().Validate(m, _tempDir);
        Assert.IsTrue(result.IsValid, string.Join("; ", result.Errors));
    }

    [TestMethod]
    public void Validate_MultipleErrors_AllReported()
    {
        // PluginManifest has defaults for Version("0.1.0") and SdkVersion("1.0.0"),
        // so only Id, Name, and EntryPoint produce errors → expect ≥3 errors.
        var m = new PluginManifest();
        var result = MakeValidator().Validate(m, _tempDir);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Count >= 3, $"Expected ≥3 errors, got {result.Errors.Count}");
    }
}
