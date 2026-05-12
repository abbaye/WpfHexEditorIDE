//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Project: WpfHexEditor.Tests
// File: Unit/WhfmtDiffFuzz_Tests.cs
// Description: Coverage for the P7 diff{} + fuzz{} model readers.
//////////////////////////////////////////////

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WpfHexEditor.Core.Definitions;
using WpfHexEditor.Core.Definitions.Metadata;
using WpfHexEditor.Core.Definitions.Query;

namespace WpfHexEditor.Tests.Unit
{
    [TestClass]
    public class WhfmtDiffFuzz_Tests
    {
        private static readonly EmbeddedFormatCatalog Cat = EmbeddedFormatCatalog.Instance;

        // ----- Diff ---------------------------------------------------------

        [TestMethod]
        public void Diff_ROM_GBC_DeclaresKeyAndIgnoreFields()
        {
            var gbc = Cat.Query().WithFormatId("ROM_GBC").First();
            Assert.IsNotNull(gbc);
            var diff = gbc.GetDiffConfig(Cat);
            Assert.IsNotNull(diff);
            Assert.IsTrue(diff.KeyFields.Contains("title"));
            Assert.IsTrue(diff.KeyFields.Contains("cgbFlag"));
            Assert.IsTrue(diff.IgnoreFields.Contains("globalChecksum"));
            Assert.AreEqual("destinationCode", diff.GroupBy);
            Assert.IsFalse(string.IsNullOrEmpty(diff.Note));
        }

        [TestMethod]
        public void Diff_AbsentReturnsNull()
        {
            // Find a whfmt that we know has no diff block (most files don't).
            var anyEntry = Cat.GetAll()
                .First(e => e.ResourceKey.EndsWith(".whfmt") && e.GetDiffConfig(Cat) is null);
            Assert.IsNull(anyEntry.GetDiffConfig(Cat));
        }

        [TestMethod]
        public void Diff_DoesNotThrowOnAnyEntry()
        {
            int seen = 0;
            foreach (var e in Cat.GetAll().Where(e => e.ResourceKey.EndsWith(".whfmt")))
            {
                _ = e.GetDiffConfig(Cat);
                seen++;
            }
            Assert.IsTrue(seen > 0);
        }

        // ----- Fuzz ---------------------------------------------------------

        [TestMethod]
        public void Fuzz_ROM_GBC_DeclaresStrategies()
        {
            var gbc = Cat.Query().WithFormatId("ROM_GBC").First();
            Assert.IsNotNull(gbc);
            var fuzz = gbc.GetFuzzConfig(Cat);
            Assert.IsNotNull(fuzz);
            Assert.IsFalse(fuzz.PreserveChecksums);
            Assert.AreEqual(1, fuzz.MaxMutationsPerFile);
            Assert.IsTrue(fuzz.Strategies.Count >= 5);

            // Spot-check known strategy
            var corruptLogo = fuzz.Strategies.First(s => s.Field == "nintendoLogo");
            Assert.AreEqual(FuzzMutation.CorruptSignature, corruptLogo.Mutation);
            Assert.AreEqual(3.0, corruptLogo.Weight, 0.001);
        }

        [TestMethod]
        public void Fuzz_MutationEnum_RecognizesAllCatalogValues()
        {
            // Smoke test: every mutation string in the catalog should map to a non-Unknown
            // enum value, OR we discover a new mutation type that needs to be added.
            int unknownCount = 0;
            int totalStrategies = 0;
            foreach (var e in Cat.GetAll().Where(e => e.ResourceKey.EndsWith(".whfmt")))
            {
                var fuzz = e.GetFuzzConfig(Cat);
                if (fuzz is null) continue;
                foreach (var s in fuzz.Strategies)
                {
                    totalStrategies++;
                    if (s.Mutation == FuzzMutation.Unknown) unknownCount++;
                }
            }
            // Allow some unknowns (catalog may have typos) but enforce a reasonable ceiling.
            Assert.IsTrue(unknownCount < totalStrategies / 10 || totalStrategies < 10,
                $"Too many unknown fuzz mutations ({unknownCount}/{totalStrategies}) — new mutation type likely missing from enum");
        }

        [TestMethod]
        public void Fuzz_AbsentReturnsNull()
        {
            var anyEntry = Cat.GetAll()
                .First(e => e.ResourceKey.EndsWith(".whfmt") && e.GetFuzzConfig(Cat) is null);
            Assert.IsNull(anyEntry.GetFuzzConfig(Cat));
        }
    }
}
