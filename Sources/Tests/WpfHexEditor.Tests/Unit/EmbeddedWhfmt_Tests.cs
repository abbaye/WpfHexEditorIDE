//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Project: WpfHexEditor.Tests
// File: EmbeddedWhfmt_Tests.cs
// Description:
//     Build-gate test suite validating all embedded .whfmt format definitions.
//     Run before packaging — a failing test means a broken whfmt must be fixed
//     before it gets compiled into the DLL.
//
//     Each test collects ALL failures before asserting, so authors see the
//     complete list of broken files in a single test run.
//////////////////////////////////////////////

using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WpfHexEditor.Core.Definitions;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Core.Services;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Tests.Unit
{
    [TestClass]
    public class EmbeddedWhfmt_Tests
    {
        // Shared catalog instance — populated once for the test class.
        private static IReadOnlySet<EmbeddedFormatEntry> _entries = null!;
        private static FormatDetectionService _parser = null!;

        [ClassInitialize]
        public static void ClassInit(TestContext _)
        {
            // Filter to .whfmt entries only — .grammar files are XML (Synalysis) and not JSON-parseable.
            _entries = EmbeddedFormatCatalog.Instance.GetAll()
                .Where(e => e.ResourceKey.EndsWith(".whfmt", System.StringComparison.OrdinalIgnoreCase))
                .ToHashSet();
            _parser  = new FormatDetectionService();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Test 1 — GetAll() does not throw
        // ─────────────────────────────────────────────────────────────────────
        [TestMethod]
        public void AllEmbeddedEntries_LoadWithoutException()
        {
            // ClassInit already called GetAll() without exception.
            // Assert that it returned a non-null, non-empty list.
            Assert.IsNotNull(_entries);
            Assert.IsTrue(_entries.Count > 0, "EmbeddedFormatCatalog.GetAll() returned an empty list.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Test 2 — Count regression guard
        // ─────────────────────────────────────────────────────────────────────
        [TestMethod]
        public void EmbeddedCatalog_HasMinimumFormatCount()
        {
            const int MinExpected = 400;
            Assert.IsTrue(
                _entries.Count >= MinExpected,
                $"Expected at least {MinExpected} embedded formats, but found {_entries.Count}.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Test 3 — Every entry parses to a non-null FormatDefinition
        // Uses a direct JsonSerializer call (not ImportFromJson) so we get the
        // real exception message instead of the swallowed null return.
        // ─────────────────────────────────────────────────────────────────────
        [TestMethod]
        public void AllEmbeddedFormats_ParseSuccessfully()
        {
            var failures = new List<string>();
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            };

            foreach (var entry in _entries)
            {
                try
                {
                    var json = EmbeddedFormatCatalog.Instance.GetJson(entry.ResourceKey);
                    var fmt  = System.Text.Json.JsonSerializer.Deserialize<FormatDefinition>(json, options);
                    if (fmt is null)
                        failures.Add($"{entry.ResourceKey}: Deserialize returned null");
                }
                catch (System.Exception ex)
                {
                    failures.Add($"{entry.ResourceKey}: {ex.Message}");
                }
            }

            Assert.AreEqual(0, failures.Count,
                $"{failures.Count} format(s) failed to parse:\n{string.Join("\n", failures)}");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Test 4 — Every parsed format passes IsValid()
        // ─────────────────────────────────────────────────────────────────────
        [TestMethod]
        public void AllEmbeddedFormats_PassIsValid()
        {
            var failures = new List<string>();

            foreach (var entry in _entries)
            {
                try
                {
                    var json = EmbeddedFormatCatalog.Instance.GetJson(entry.ResourceKey);
                    var fmt  = _parser.ImportFromJson(json);
                    if (fmt is null) continue; // already caught by Test 3
                    if (!fmt.IsValid())
                        failures.Add($"{entry.ResourceKey}: IsValid() = false");
                }
                catch { /* already caught by Test 3 */ }
            }

            Assert.AreEqual(0, failures.Count,
                $"{failures.Count} format(s) failed IsValid():\n{string.Join("\n", failures)}");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Test 5 — No duplicate FormatName across the catalog
        // ─────────────────────────────────────────────────────────────────────
        [TestMethod]
        public void AllEmbeddedFormats_HaveUniqueFormatNames()
        {
            var names    = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            var failures = new List<string>();

            foreach (var entry in _entries)
            {
                try
                {
                    var json = EmbeddedFormatCatalog.Instance.GetJson(entry.ResourceKey);
                    var fmt  = _parser.ImportFromJson(json);
                    if (fmt?.FormatName is null) continue;

                    if (names.TryGetValue(fmt.FormatName, out var existing))
                        failures.Add($"Duplicate '{fmt.FormatName}': {existing} and {entry.ResourceKey}");
                    else
                        names[fmt.FormatName] = entry.ResourceKey;
                }
                catch { /* parse errors reported in Test 3 */ }
            }

            Assert.AreEqual(0, failures.Count,
                $"{failures.Count} duplicate FormatName(s):\n{string.Join("\n", failures)}");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Test 6 — Every format declares at least one extension
        // ─────────────────────────────────────────────────────────────────────
        [TestMethod]
        public void AllEmbeddedFormats_HaveAtLeastOneExtension()
        {
            var failures = new List<string>();

            foreach (var entry in _entries)
            {
                try
                {
                    var json = EmbeddedFormatCatalog.Instance.GetJson(entry.ResourceKey);
                    var fmt  = _parser.ImportFromJson(json);
                    if (fmt is null) continue;

                    if (fmt.Extensions is null || fmt.Extensions.Count == 0)
                        failures.Add($"{entry.ResourceKey} ({fmt.FormatName}): no extensions declared");
                }
                catch { /* parse errors reported in Test 3 */ }
            }

            Assert.AreEqual(0, failures.Count,
                $"{failures.Count} format(s) have no extensions:\n{string.Join("\n", failures)}");
        }
    }
}
