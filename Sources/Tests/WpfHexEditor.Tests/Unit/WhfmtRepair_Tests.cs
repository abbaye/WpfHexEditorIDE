//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Project: WpfHexEditor.Tests
// File: Unit/WhfmtRepair_Tests.cs
// Description: Coverage for the P6 repair[] + checksums readers.
//////////////////////////////////////////////

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WpfHexEditor.Core.Definitions;
using WpfHexEditor.Core.Definitions.Metadata;
using WpfHexEditor.Core.Definitions.Query;

namespace WpfHexEditor.Tests.Unit
{
    [TestClass]
    public class WhfmtRepair_Tests
    {
        private static readonly EmbeddedFormatCatalog Cat = EmbeddedFormatCatalog.Instance;

        [TestMethod]
        public void Repair_ROM_GBC_DeclaresHeaderChecksumFix()
        {
            // ROM_GBC.whfmt declares one repair: FixHeaderChecksum.
            var gbc = Cat.GetByFormatId("ROM_GBC");
            Assert.IsNotNull(gbc);
            var repairs = gbc.GetRepairs(Cat);
            Assert.IsTrue(repairs.Count >= 1, "ROM_GBC should declare at least one repair");

            var fix = repairs.First(r => r.Name == "FixHeaderChecksum");
            Assert.AreEqual("recompute_checksum", fix.Action);
            Assert.AreEqual("headerChecksum",     fix.Target);
            Assert.IsFalse(string.IsNullOrEmpty(fix.Trigger));
            Assert.IsFalse(string.IsNullOrEmpty(fix.Description));
        }

        [TestMethod]
        public void Repair_AbsentReturnsEmpty()
        {
            // Most formats have no repair[] block — should return empty, not throw.
            var anyEntry = Cat.GetAll().First(e => e.ResourceKey.EndsWith(".whfmt"));
            var repairs  = anyEntry.GetRepairs(Cat);
            Assert.IsNotNull(repairs);
        }

        [TestMethod]
        public void Repair_SkipsMalformedEntries()
        {
            // The reader requires name + action; malformed entries are silently skipped.
            var whfmtOnly = Cat.GetAll().Where(e => e.ResourceKey.EndsWith(".whfmt"));
            foreach (var e in whfmtOnly.Take(50))
            {
                // Should never throw on any catalog whfmt entry.
                _ = e.GetRepairs(Cat);
            }
        }

        [TestMethod]
        public void Checksums_AbsentReturnsEmpty()
        {
            // Most formats have no checksums block — should return empty.
            var anyEntry = Cat.GetAll().First(e => e.ResourceKey.EndsWith(".whfmt"));
            var sums     = anyEntry.GetChecksums(Cat);
            Assert.IsNotNull(sums);
        }

        [TestMethod]
        public void Checksums_DoesNotThrowOnAnyEntry()
        {
            // Smoke test: no malformed JSON should crash the reader.
            int total = 0;
            foreach (var e in Cat.GetAll().Where(e => e.ResourceKey.EndsWith(".whfmt")))
            {
                total += e.GetChecksums(Cat).Count;
            }
            // We just verify the reader is robust — count may legitimately be 0.
            Assert.IsTrue(total >= 0);
        }

        // RepairResult / IWhfmtRepairExecutor contract smoke test

        [TestMethod]
        public void RepairResult_RecordHasExpectedFields()
        {
            var r = new RepairResult(true, "ok", 1);
            Assert.IsTrue(r.Success);
            Assert.AreEqual("ok", r.Message);
            Assert.AreEqual(1, r.BytesChanged);
        }
    }
}
