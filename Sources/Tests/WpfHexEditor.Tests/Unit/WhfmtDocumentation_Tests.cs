//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Project: WpfHexEditor.Tests
// File: Unit/WhfmtDocumentation_Tests.cs
// Description:
//     Coverage for the P5 IDE doc-pane extensions: GetSoftware, GetUseCases,
//     GetReferences, GetFormatRelationships, GetInspectorHeader,
//     GetNavigationOverview, GetForensicNotes — verified against real entries
//     in the embedded catalog.
//////////////////////////////////////////////

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WpfHexEditor.Core.Definitions;
using WpfHexEditor.Core.Definitions.Metadata;
using WpfHexEditor.Core.Definitions.Query;

namespace WpfHexEditor.Tests.Unit
{
    [TestClass]
    public class WhfmtDocumentation_Tests
    {
        private static readonly EmbeddedFormatCatalog Cat = EmbeddedFormatCatalog.Instance;

        // ----- Software -----------------------------------------------------

        [TestMethod]
        public void Software_StringArray_IsRead()
        {
            // ROM_GBC declares Software as a string array (e.g. "BGB", "Gambatte", ...).
            var gbc = Cat.GetByFormatId("ROM_GBC");
            Assert.IsNotNull(gbc);
            var sw = gbc.GetSoftware(Cat);
            Assert.IsTrue(sw.Count > 0, "ROM_GBC should declare at least one software");
            // All entries should be name-only (no url/role).
            Assert.IsTrue(sw.All(s => s.Url is null && s.Role is null));
        }

        [TestMethod]
        public void Software_ObjectArray_IsRead()
        {
            // A_OUT declares "software" (camelCase) as array of {name,url,role} objects.
            var aout = Cat.GetByFormatId("A_OUT");
            Assert.IsNotNull(aout);
            var sw = aout.GetSoftware(Cat);
            Assert.IsTrue(sw.Count > 0, "A_OUT should declare software entries");
            // At least one entry has url AND role populated.
            Assert.IsTrue(sw.Any(s => s.Url is not null && s.Role is not null),
                "A_OUT software entries should expose url and role");
        }

        // ----- UseCases -----------------------------------------------------

        [TestMethod]
        public void UseCases_ReadsPascalCase()
        {
            var gbc = Cat.GetByFormatId("ROM_GBC");
            Assert.IsNotNull(gbc);
            var uc = gbc.GetUseCases(Cat);
            Assert.IsTrue(uc.Count > 0, "ROM_GBC should declare use cases");
        }

        // ----- References ---------------------------------------------------

        [TestMethod]
        public void References_NamedObjectSchema_IsRead()
        {
            // ROM_GBC declares references as { specifications: [...], WebLinks: [...] }
            var gbc = Cat.GetByFormatId("ROM_GBC");
            Assert.IsNotNull(gbc);
            var refs = gbc.GetReferences(Cat);
            Assert.IsTrue(refs.Count > 0);
            // At least one is flagged as web link (URLs present in WebLinks bucket).
            Assert.IsTrue(refs.Any(r => r.IsWebLink),
                "ROM_GBC references should contain at least one web link");
        }

        [TestMethod]
        public void References_StringArraySchema_IsRead()
        {
            // A_OUT declares references as a flat string array (all entries are URLs).
            var aout = Cat.GetByFormatId("A_OUT");
            Assert.IsNotNull(aout);
            var refs = aout.GetReferences(Cat);
            Assert.IsTrue(refs.Count > 0);
            // URLs are auto-classified as web links via prefix check.
            Assert.IsTrue(refs.All(r => r.IsWebLink),
                "A_OUT references are all URLs and should be flagged as web links");
        }

        [TestMethod]
        public void References_MixedStringArray_DistinguishesUrls()
        {
            // ANALYZE.whfmt has a mix: "Analyze 7.5 Format Specification (Mayo Clinic BIR)" + URL.
            var analyze = Cat.GetByFormatId("ANALYZE");
            Assert.IsNotNull(analyze);
            var refs = analyze.GetReferences(Cat);
            Assert.IsTrue(refs.Any(r => r.IsWebLink));
            Assert.IsTrue(refs.Any(r => !r.IsWebLink));
        }

        // ----- formatRelationships -----------------------------------------

        [TestMethod]
        public void FormatRelationships_ArraySchema_IsRead()
        {
            // A_OUT uses the array-of-objects schema (format/relationship).
            var aout = Cat.GetByFormatId("A_OUT");
            Assert.IsNotNull(aout);
            var rel = aout.GetFormatRelationships(Cat);
            Assert.IsTrue(rel.Count > 0);
            Assert.IsTrue(rel.Any(r => r.Format == "ELF"));
        }

        [TestMethod]
        public void FormatRelationships_DictSchema_IsRead()
        {
            // ROM_GBC uses the dict schema { category, extensions, relatedFormats: [...] }.
            var gbc = Cat.GetByFormatId("ROM_GBC");
            Assert.IsNotNull(gbc);
            var rel = gbc.GetFormatRelationships(Cat);
            // category and extensions are skipped; relatedFormats values are emitted.
            Assert.IsTrue(rel.Any(r => r.Format == "ROM_GB"),
                "ROM_GBC should declare relation to ROM_GB");
        }

        // ----- Inspector header --------------------------------------------

        [TestMethod]
        public void InspectorHeader_BadgeAndPrimaryField_AreRead()
        {
            // ROM_GBC declares badge="title", primaryField="cgbFlag", showQualityScore=true.
            var gbc = Cat.GetByFormatId("ROM_GBC");
            Assert.IsNotNull(gbc);
            var hdr = gbc.GetInspectorHeader(Cat);
            Assert.IsNotNull(hdr);
            Assert.AreEqual("title",   hdr.Badge);
            Assert.AreEqual("cgbFlag", hdr.PrimaryField);
            Assert.IsTrue(hdr.ShowQualityScore);
        }

        // ----- Navigation overview -----------------------------------------

        [TestMethod]
        public void NavigationOverview_EntryPointAndStructure_AreRead()
        {
            // A_OUT declares { entryPoint: "Header", structure: [...], notes: "..." }.
            var aout = Cat.GetByFormatId("A_OUT");
            Assert.IsNotNull(aout);
            var nav = aout.GetNavigationOverview(Cat);
            Assert.IsNotNull(nav);
            Assert.AreEqual("Header", nav.EntryPoint);
            Assert.IsTrue(nav.Structure.Count >= 4);
            Assert.IsFalse(string.IsNullOrEmpty(nav.Notes));
        }

        // ----- Forensic notes ----------------------------------------------

        [TestMethod]
        public void ForensicNotes_AreRead()
        {
            // A_OUT declares forensic.notes describing rootkit suspicion.
            var aout = Cat.GetByFormatId("A_OUT");
            Assert.IsNotNull(aout);
            var notes = aout.GetForensicNotes(Cat);
            Assert.IsFalse(string.IsNullOrEmpty(notes));
        }
    }
}
