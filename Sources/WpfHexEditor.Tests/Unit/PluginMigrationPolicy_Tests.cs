//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// PluginMigrationPolicy Unit Tests
// Author : Claude Sonnet 4.6
// Contributors: Derek Tremblay (derektremblay666@gmail.com)
//////////////////////////////////////////////

using System.IO;
using System.Text.Json;
using WpfHexEditor.PluginHost.Services;

namespace WpfHexEditor.Tests.Unit;

[TestClass]
public class PluginMigrationPolicy_Tests
{
    // ── CreateDefault ─────────────────────────────────────────────────────────

    [TestMethod]
    public void CreateDefault_ReturnsValidPolicy()
    {
        var policy = PluginMigrationPolicy.CreateDefault();

        Assert.IsTrue(policy.IsValid(), "Default policy should be valid.");
    }

    [TestMethod]
    public void CreateDefault_HasExpectedDefaults()
    {
        var policy = PluginMigrationPolicy.CreateDefault();

        Assert.AreEqual(300,  policy.MemorySuggestThresholdMb);
        Assert.AreEqual(600,  policy.MemoryAutoMigrateThresholdMb);
        Assert.AreEqual(50.0, policy.CpuSuggestThresholdPercent);
        Assert.AreEqual(80.0, policy.CpuAutoMigrateThresholdPercent);
        Assert.AreEqual(30,   policy.CpuSustainedWindowSeconds);
        Assert.AreEqual(3,    policy.CrashCountThreshold);
        Assert.AreEqual(PluginMigrationMode.SuggestOnly, policy.Mode);
    }

    // ── IsValid ───────────────────────────────────────────────────────────────

    [TestMethod]
    public void IsValid_ReturnsFalse_WhenMemorySuggestIsZero()
    {
        var policy = PluginMigrationPolicy.CreateDefault();
        policy.MemorySuggestThresholdMb = 0;

        Assert.IsFalse(policy.IsValid());
    }

    [TestMethod]
    public void IsValid_ReturnsFalse_WhenAutoMigrateLessThanSuggest()
    {
        var policy = PluginMigrationPolicy.CreateDefault();
        policy.MemoryAutoMigrateThresholdMb = policy.MemorySuggestThresholdMb - 1;

        Assert.IsFalse(policy.IsValid());
    }

    [TestMethod]
    public void IsValid_ReturnsFalse_WhenCpuAutoLessThanSuggest()
    {
        var policy = PluginMigrationPolicy.CreateDefault();
        policy.CpuAutoMigrateThresholdPercent = policy.CpuSuggestThresholdPercent - 1;

        Assert.IsFalse(policy.IsValid());
    }

    [TestMethod]
    public void IsValid_ReturnsFalse_WhenCrashCountIsZero()
    {
        var policy = PluginMigrationPolicy.CreateDefault();
        policy.CrashCountThreshold = 0;

        Assert.IsFalse(policy.IsValid());
    }

    [TestMethod]
    public void IsValid_ReturnsTrue_WhenSuggestEqualsAuto()
    {
        // Edge case: suggest == auto is acceptable.
        var policy = PluginMigrationPolicy.CreateDefault();
        policy.MemoryAutoMigrateThresholdMb = policy.MemorySuggestThresholdMb;

        Assert.IsTrue(policy.IsValid());
    }

    // ── Clone ─────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Clone_ProducesIndependentCopy()
    {
        var original = PluginMigrationPolicy.CreateDefault();
        var clone    = original.Clone();

        clone.CrashCountThreshold = 99;

        Assert.AreNotEqual(original.CrashCountThreshold, clone.CrashCountThreshold,
            "Mutating clone must not affect the original.");
    }

    // ── Save / Load round-trip ────────────────────────────────────────────────

    [TestMethod]
    public void SaveLoad_RoundTrip_PreservesAllValues()
    {
        var path   = Path.GetTempFileName();
        try
        {
            var original = new PluginMigrationPolicy
            {
                MemorySuggestThresholdMb       = 128,
                MemoryAutoMigrateThresholdMb   = 512,
                CpuSuggestThresholdPercent     = 40.0,
                CpuAutoMigrateThresholdPercent = 75.0,
                CpuSustainedWindowSeconds      = 20,
                CrashCountThreshold            = 5,
                Mode                           = PluginMigrationMode.AutoMigrate
            };

            original.Save(path);
            var loaded = PluginMigrationPolicy.Load(path);

            Assert.AreEqual(original.MemorySuggestThresholdMb,       loaded.MemorySuggestThresholdMb);
            Assert.AreEqual(original.MemoryAutoMigrateThresholdMb,   loaded.MemoryAutoMigrateThresholdMb);
            Assert.AreEqual(original.CpuSuggestThresholdPercent,     loaded.CpuSuggestThresholdPercent);
            Assert.AreEqual(original.CpuAutoMigrateThresholdPercent, loaded.CpuAutoMigrateThresholdPercent);
            Assert.AreEqual(original.CpuSustainedWindowSeconds,      loaded.CpuSustainedWindowSeconds);
            Assert.AreEqual(original.CrashCountThreshold,            loaded.CrashCountThreshold);
            Assert.AreEqual(original.Mode,                           loaded.Mode);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Load_ReturnsDefault_WhenFileDoesNotExist()
    {
        var path   = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        var loaded = PluginMigrationPolicy.Load(path);

        Assert.IsNotNull(loaded);
        Assert.IsTrue(loaded.IsValid(), "Fallback policy must be valid.");
        Assert.AreEqual(PluginMigrationPolicy.CreateDefault().CrashCountThreshold,
                        loaded.CrashCountThreshold);
    }

    [TestMethod]
    public void Load_ReturnsDefault_WhenFileIsCorrupt()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "{ not valid json {{ ]]");
            var loaded = PluginMigrationPolicy.Load(path);

            Assert.IsNotNull(loaded);
            Assert.IsTrue(loaded.IsValid(), "Fallback policy from corrupt file must be valid.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Load_ReturnsDefault_WhenJsonHasInvalidValues()
    {
        // Produce a JSON with AutoMigrate < Suggest — IsValid() returns false → fallback.
        var path = Path.GetTempFileName();
        try
        {
            var bad = new PluginMigrationPolicy
            {
                MemorySuggestThresholdMb     = 600,
                MemoryAutoMigrateThresholdMb = 100   // invalid: auto < suggest
            };
            File.WriteAllText(path,
                JsonSerializer.Serialize(bad, new JsonSerializerOptions { WriteIndented = true }));

            var loaded = PluginMigrationPolicy.Load(path);

            // Load detects IsValid() == false and returns CreateDefault().
            Assert.AreEqual(PluginMigrationPolicy.CreateDefault().MemorySuggestThresholdMb,
                            loaded.MemorySuggestThresholdMb);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Save_DoesNotThrow_WhenDirectoryDoesNotExist()
    {
        // Save() creates the directory; should not throw.
        var dir  = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var path = Path.Combine(dir, "policy.json");
        try
        {
            var policy = PluginMigrationPolicy.CreateDefault();
            policy.Save(path);   // must not throw

            Assert.IsTrue(File.Exists(path));
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }
}
