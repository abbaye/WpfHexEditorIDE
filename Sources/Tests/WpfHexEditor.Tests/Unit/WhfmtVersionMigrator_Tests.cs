//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Project: WpfHexEditor.Tests
// File: Unit/WhfmtVersionMigrator_Tests.cs
// Description:
//     Coverage for the P11 in-memory v2 → v3 PascalCase → camelCase normalizer.
//     Verifies that renames apply, that existing camelCase values are NOT
//     overwritten, that the fast-path returns the input unchanged when no
//     legacy fields are present, and that DryRun produces a report without
//     mutating the input.
//////////////////////////////////////////////

using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WpfHexEditor.Core.Definitions.Models;

namespace WpfHexEditor.Tests.Unit
{
    [TestClass]
    public class WhfmtVersionMigrator_Tests
    {
        [TestMethod]
        public void Migrate_RenamesRootPascalCaseKeys()
        {
            const string json = """
            {
              "formatName": "X",
              "QualityMetrics": { "CompletenessScore": 80 },
              "MimeTypes":      [ "text/plain" ],
              "Software":       [ "X" ],
              "UseCases":       [ "Y" ],
              "TechnicalDetails": { "Platform": "Win" }
            }
            """;
            var migrated = WhfmtVersionMigrator.Migrate(json);
            using var doc = JsonDocument.Parse(migrated);
            var root = doc.RootElement;

            Assert.IsTrue(root.TryGetProperty("qualityMetrics",   out _));
            Assert.IsTrue(root.TryGetProperty("mimeTypes",        out _));
            Assert.IsTrue(root.TryGetProperty("software",         out _));
            Assert.IsTrue(root.TryGetProperty("useCases",         out _));
            Assert.IsTrue(root.TryGetProperty("technicalDetails", out _));

            // Legacy keys are removed.
            Assert.IsFalse(root.TryGetProperty("QualityMetrics",   out _));
            Assert.IsFalse(root.TryGetProperty("MimeTypes",        out _));
            Assert.IsFalse(root.TryGetProperty("Software",         out _));
        }

        [TestMethod]
        public void Migrate_RenamesDetectionSubFields()
        {
            const string json = """
            {
              "detection": {
                "Strength":     "Medium",
                "EntropyHint":  { "min": 4.0 },
                "MinimumScore": 80
              }
            }
            """;
            var migrated = WhfmtVersionMigrator.Migrate(json);
            using var doc = JsonDocument.Parse(migrated);
            var det = doc.RootElement.GetProperty("detection");

            Assert.IsTrue(det.TryGetProperty("strength",     out _));
            Assert.IsTrue(det.TryGetProperty("entropyHint",  out _));
            Assert.IsTrue(det.TryGetProperty("minimumScore", out _));
            Assert.IsFalse(det.TryGetProperty("Strength",     out _));
        }

        [TestMethod]
        public void Migrate_DoesNotOverwriteExistingCamelCase()
        {
            // Both PascalCase and camelCase present → camelCase wins, PascalCase is dropped.
            const string json = """
            {
              "MimeTypes": ["text/plain"],
              "mimeTypes": ["application/json"]
            }
            """;
            var migrated = WhfmtVersionMigrator.Migrate(json);
            using var doc = JsonDocument.Parse(migrated);
            var arr = doc.RootElement.GetProperty("mimeTypes");
            Assert.AreEqual("application/json", arr[0].GetString());
            Assert.IsFalse(doc.RootElement.TryGetProperty("MimeTypes", out _));
        }

        [TestMethod]
        public void Migrate_FastPath_ReturnsInputUnchangedWhenNoLegacy()
        {
            const string json = """
            { "formatName": "X", "mimeTypes": ["text/plain"] }
            """;
            var migrated = WhfmtVersionMigrator.Migrate(json);
            Assert.AreSame(json, migrated);
        }

        [TestMethod]
        public void Migrate_AcceptsJsonc()
        {
            const string json = """
            // header
            { "MimeTypes": [ "text/plain", ] }
            """;
            // Just verify it doesn't throw on a JSONC input.
            var migrated = WhfmtVersionMigrator.Migrate(json);
            using var doc = JsonDocument.Parse(migrated);
            Assert.IsTrue(doc.RootElement.TryGetProperty("mimeTypes", out _));
        }

        [TestMethod]
        public void DryRun_ListsRenamesWithoutMutating()
        {
            const string json = """
            {
              "MimeTypes": ["text/plain"],
              "detection": { "Strength": "Medium" }
            }
            """;
            var report = WhfmtVersionMigrator.DryRun(json);
            Assert.AreEqual(2, report.Count);
            Assert.IsTrue(report.Any(s => s.Contains("MimeTypes")));
            Assert.IsTrue(report.Any(s => s.Contains("Strength")));
        }

        [TestMethod]
        public void DryRun_ReportsDropWhenBothCasingsPresent()
        {
            // Honors the same conflict policy as Migrate: when both forms exist,
            // the legacy key is dropped, not renamed.
            const string json = """
            { "MimeTypes": ["a"], "mimeTypes": ["b"] }
            """;
            var report = WhfmtVersionMigrator.DryRun(json);
            Assert.AreEqual(1, report.Count);
            Assert.IsTrue(report[0].Contains("dropped"));
        }

        [TestMethod]
        public void DryRun_EmptyReportWhenAlreadyV3()
        {
            const string json = """{ "mimeTypes": [], "detection": { "strength": "Medium" } }""";
            var report = WhfmtVersionMigrator.DryRun(json);
            Assert.AreEqual(0, report.Count);
        }

        [TestMethod]
        public void Migrate_PreservesNestedValues()
        {
            const string json = """
            {
              "QualityMetrics": {
                "CompletenessScore": 80,
                "DocumentationLevel": "detailed"
              }
            }
            """;
            var migrated = WhfmtVersionMigrator.Migrate(json);
            using var doc = JsonDocument.Parse(migrated);
            var qm = doc.RootElement.GetProperty("qualityMetrics");
            // Nested children are intentionally NOT renamed — only top-level
            // keys per ADR-038 D1. v4 will deepen this if needed.
            Assert.AreEqual(80, qm.GetProperty("CompletenessScore").GetInt32());
        }
    }
}
