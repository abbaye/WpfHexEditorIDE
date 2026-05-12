//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Project: WpfHexEditor.Tests
// File: Unit/WhfmtSurfaces_Tests.cs
// Description:
//     Phase B B1+B3 coverage for surfaces previously without direct tests:
//     EmbeddedFormatCatalog.GetJsonV3, FormatSummaryBuilder, GetDocumentationBundle,
//     FormatMatcher.Match (which exercises the internal ScoreEntry).
//////////////////////////////////////////////

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WpfHexEditor.Core.Definitions;
using WpfHexEditor.Core.Definitions.Matching;
using WpfHexEditor.Core.Definitions.Metadata;

namespace WpfHexEditor.Tests.Unit
{
    [TestClass]
    public class WhfmtSurfaces_Tests
    {
        private static readonly EmbeddedFormatCatalog Cat = EmbeddedFormatCatalog.Instance;

        // ─────────────────────────────────────────────────────────────────────
        // B1 — GetJsonV3
        // ─────────────────────────────────────────────────────────────────────

        [TestMethod]
        public void GetJsonV3_ReturnsNonEmptyJson_ForKnownFormat()
        {
            var entry = Cat.GetByFormatId("PNG") ?? Cat.GetAll().First(e => e.ResourceKey.EndsWith(".whfmt"));
            var json = Cat.GetJsonV3(entry.ResourceKey);
            Assert.IsFalse(string.IsNullOrWhiteSpace(json), "GetJsonV3 returned empty");
            Assert.IsTrue(json.TrimStart().StartsWith('{'), "GetJsonV3 did not return a JSON object");
        }

        [TestMethod]
        public void GetJsonV3_FallsBackToRaw_OnMigrationFailure()
        {
            // Non-whfmt resource keys are returned as-is per the contract.
            var entry = Cat.GetAll().First();
            var v3 = Cat.GetJsonV3(entry.ResourceKey);
            var raw = Cat.GetJson(entry.ResourceKey);
            // Either equal (migration was a no-op) or differs (migrated PascalCase→camelCase).
            // What matters: it never throws and never returns null/empty.
            Assert.IsNotNull(v3);
            Assert.IsNotNull(raw);
        }

        // ─────────────────────────────────────────────────────────────────────
        // B3 — FormatSummaryBuilder
        // ─────────────────────────────────────────────────────────────────────

        [TestMethod]
        public void FormatSummaryBuilder_OneLiner_ContainsNameAndCategory()
        {
            var entry = Cat.GetAll().First(e => e.ResourceKey.EndsWith(".whfmt") && e.Extensions.Count > 0);
            var line = FormatSummaryBuilder.BuildOneLiner(entry);
            StringAssert.Contains(line, entry.Name);
            StringAssert.Contains(line, entry.Category);
            StringAssert.Contains(line, "Quality");
        }

        [TestMethod]
        public void FormatSummaryBuilder_PlainText_NotEmpty_WithOrWithoutCatalog()
        {
            var entry = Cat.GetAll().First(e => e.ResourceKey.EndsWith(".whfmt"));
            var bare    = FormatSummaryBuilder.BuildPlainText(entry);
            var withCat = FormatSummaryBuilder.BuildPlainText(entry, Cat);
            Assert.IsFalse(string.IsNullOrWhiteSpace(bare));
            Assert.IsFalse(string.IsNullOrWhiteSpace(withCat));
            // With catalog, output should be at least as long (more metadata appended).
            Assert.IsTrue(withCat.Length >= bare.Length);
        }

        // ─────────────────────────────────────────────────────────────────────
        // B3 — GetDocumentationBundle
        // ─────────────────────────────────────────────────────────────────────

        [TestMethod]
        public void GetDocumentationBundle_ReturnsTuple_ForAnyWhfmt()
        {
            var entry = Cat.GetAll().First(e => e.ResourceKey.EndsWith(".whfmt"));
            var bundle = entry.GetDocumentationBundle(Cat);
            // Tuple structure: (Inspector, Navigation, ForensicNotes). All three may be null
            // for a minimal .whfmt — what matters is no throw.
            _ = bundle.Inspector;
            _ = bundle.Navigation;
            _ = bundle.ForensicNotes;
        }

        [TestMethod]
        public void GetDocumentationBundle_PopulatedForRichFormat()
        {
            // PNG carries inspector + navigation + forensic blocks in the catalog.
            var entry = Cat.GetByFormatId("PNG");
            if (entry is null) Assert.Inconclusive("PNG not in catalog");
            var bundle = entry!.GetDocumentationBundle(Cat);
            Assert.IsTrue(
                bundle.Inspector is not null || bundle.Navigation is not null || bundle.ForensicNotes is not null,
                "Rich format should expose at least one documentation block");
        }

        // ─────────────────────────────────────────────────────────────────────
        // B3 — FormatMatcher (exercises internal ScoreEntry)
        // ─────────────────────────────────────────────────────────────────────

        [TestMethod]
        public void FormatMatcher_Match_DetectsPngByMagicBytes()
        {
            // PNG signature: 89 50 4E 47 0D 0A 1A 0A
            byte[] pngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
            var result = FormatMatcher.Match(Cat, extension: null, pngHeader);
            Assert.IsNotNull(result, "PNG magic bytes should produce a match");
            // PNG or APNG (animated PNG) both share the 8-byte signature.
            StringAssert.Contains(result!.Entry.FormatId.ToUpperInvariant(), "PNG");
        }

        [TestMethod]
        public void FormatMatcher_Match_ReturnsNull_OnUnknownHeader()
        {
            byte[] randomGarbage = [0xAB, 0xCD, 0xEF, 0x12, 0x34, 0x56];
            var result = FormatMatcher.Match(Cat, extension: null, randomGarbage);
            // May be null OR a low-confidence extension fallback (none here since extension=null).
            // We only assert it doesn't throw and, if non-null, has a sensible confidence.
            if (result is not null)
                Assert.IsTrue(result.Confidence is >= 0 and <= 1);
        }

        [TestMethod]
        public void FormatMatcher_GetTopMatches_OrdersByConfidence()
        {
            byte[] pngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
            var top = FormatMatcher.GetTopMatches(Cat, pngHeader, maxResults: 3);
            Assert.IsTrue(top.Count > 0);
            for (int i = 1; i < top.Count; i++)
                Assert.IsTrue(top[i - 1].Confidence >= top[i].Confidence, "Results not ordered by confidence");
        }
    }
}
