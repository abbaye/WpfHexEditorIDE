//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Project: WpfHexEditor.Tests
// File: DocumentEditor_ForensicTrio_Tests.cs
// Description:
//     Coverage for the DocumentEditor forensic trio:
//       - EmbeddedObjectsScanner.Scan (image/object/macro discovery)
//       - DocumentAnonymizer.Anonymize (metadata strip + flags)
//       - RtfSchemaEngine.EscapeText (Unicode + structural escapes)
//////////////////////////////////////////////

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;
using WpfHexEditor.Editor.DocumentEditor.Core.Schema;
using WpfHexEditor.Editor.DocumentEditor.Services;

namespace WpfHexEditor.Tests.Unit
{
    [TestClass]
    public class DocumentEditor_ForensicTrio_Tests
    {
        // ── EmbeddedObjectsScanner ────────────────────────────────────────

        [TestMethod]
        public void Scan_NullModel_ReturnsEmpty()
        {
            var result = EmbeddedObjectsScanner.Scan(null);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void Scan_FindsTopLevelImage()
        {
            var model = new DocumentModel();
            var img = new DocumentBlock { Kind = DocumentBlockKinds.Image, Text = "[image]" };
            img.Attributes[DocumentBlockAttributes.ZipEntryName] = "word/media/image1.png";
            img.Attributes[DocumentBlockAttributes.BinarySize]    = 12345;
            model.Blocks.Add(img);

            var result = EmbeddedObjectsScanner.Scan(model);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(DocumentBlockKinds.Image, result[0].Kind);
            Assert.AreEqual("image1.png", result[0].Name);
            Assert.AreEqual(12345, result[0].SizeBytes);
            Assert.AreEqual("word/media/image1.png", result[0].ZipEntryName);
        }

        [TestMethod]
        public void Scan_FindsNestedImageInsideParagraph()
        {
            var model = new DocumentModel();
            var para  = new DocumentBlock { Kind = DocumentBlockKinds.Paragraph };
            var img   = new DocumentBlock { Kind = DocumentBlockKinds.Image };
            para.Children.Add(img);
            model.Blocks.Add(para);

            var result = EmbeddedObjectsScanner.Scan(model);
            Assert.AreEqual(1, result.Count);
        }

        [TestMethod]
        public void Scan_AddsSyntheticMacroEntry_WhenHasMacros()
        {
            var model = new DocumentModel();
            model.Metadata = model.Metadata with
            {
                HasMacros = true,
                MimeType  = "application/vnd.ms-word.document.macroEnabled.12"
            };

            var result = EmbeddedObjectsScanner.Scan(model);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(DocumentBlockKinds.Macro, result[0].Kind);
            Assert.AreEqual("word/vbaProject.bin", result[0].ZipEntryName);
        }

        [TestMethod]
        public void Scan_MacroPathIsNull_ForNonOoxmlMimeType()
        {
            var model = new DocumentModel();
            model.Metadata = model.Metadata with
            {
                HasMacros = true,
                MimeType  = "application/vnd.oasis.opendocument.text"
            };

            var result = EmbeddedObjectsScanner.Scan(model);
            Assert.AreEqual(1, result.Count);
            Assert.IsNull(result[0].ZipEntryName);
        }

        [TestMethod]
        public void EmbeddedObjectEntry_SourceFallsBackToRawOffset_WhenNoZipEntryName()
        {
            var entry = new EmbeddedObjectEntry
            {
                Kind  = DocumentBlockKinds.Image,
                Block = new DocumentBlock { Kind = DocumentBlockKinds.Image, RawOffset = 0x1A2B }
            };
            Assert.AreEqual("@0x1A2B", entry.Source);
        }

        [TestMethod]
        public void EmbeddedObjectEntry_SizeText_FormatsByMagnitude()
        {
            Assert.AreEqual("—",       new EmbeddedObjectEntry { SizeBytes = -1 }.SizeText);
            Assert.AreEqual("512 B",   new EmbeddedObjectEntry { SizeBytes = 512 }.SizeText);
            Assert.AreEqual("1.5 KB",  new EmbeddedObjectEntry { SizeBytes = 1536 }.SizeText);
            Assert.AreEqual("2.00 MB", new EmbeddedObjectEntry { SizeBytes = 2 * 1024 * 1024 }.SizeText);
        }

        [TestMethod]
        public void ComputeHash_PopulatesSha256_OncePerEntry()
        {
            var entry = new EmbeddedObjectEntry { Kind = "image" };
            entry.ComputeHash(new byte[] { 0x68, 0x69 }); // "hi"
            // SHA-256 of "hi" = 8f434346648f6b96df89dda901c5176b10a6d83961dd3c1ac88b59b2dc327aa4
            Assert.AreEqual("8f434346648f6b96df89dda901c5176b10a6d83961dd3c1ac88b59b2dc327aa4", entry.Sha256);

            // Idempotent: second call doesn't overwrite.
            entry.ComputeHash(new byte[] { 0xFF });
            Assert.AreEqual("8f434346648f6b96df89dda901c5176b10a6d83961dd3c1ac88b59b2dc327aa4", entry.Sha256);
        }

        [TestMethod]
        public void ComputeHash_EmptyOrNullBytes_LeavesEmptyHash()
        {
            var entry = new EmbeddedObjectEntry();
            entry.ComputeHash(System.Array.Empty<byte>());
            Assert.AreEqual(string.Empty, entry.Sha256);
        }

        // ── DocumentAnonymizer ────────────────────────────────────────────

        [TestMethod]
        public void Anonymize_ClearsAuthorAndTimestamps()
        {
            var model = new DocumentModel
            {
                Metadata = new DocumentMetadata
                {
                    Title       = "Report",
                    Author      = "Alice",
                    CreatedUtc  = new System.DateTime(2025, 1, 1, 0, 0, 0, System.DateTimeKind.Utc),
                    ModifiedUtc = new System.DateTime(2025, 6, 1, 0, 0, 0, System.DateTimeKind.Utc),
                }
            };

            var result = DocumentAnonymizer.Anonymize(model);

            Assert.AreEqual(string.Empty, model.Metadata.Author);
            Assert.IsNull(model.Metadata.CreatedUtc);
            Assert.IsNull(model.Metadata.ModifiedUtc);
            Assert.AreEqual("Report", model.Metadata.Title, "Title should be preserved.");
            Assert.IsTrue(model.Metadata.Extra[DocumentMetadataExtraKeys.Anonymized] == "true");
            Assert.IsTrue(result.HadAuthor);
            Assert.IsTrue(result.HadCreated);
            Assert.IsTrue(result.HadModified);
        }

        [TestMethod]
        public void Anonymize_PreservesIsTemplateButRemovesCustomKeys()
        {
            var model = new DocumentModel();
            model.Metadata.Extra[DocumentMetadataExtraKeys.IsTemplate] = "true";
            model.Metadata.Extra["company"]   = "AcmeCorp";
            model.Metadata.Extra["printedBy"] = "Bob";

            var result = DocumentAnonymizer.Anonymize(model);

            Assert.IsTrue(model.Metadata.Extra.ContainsKey(DocumentMetadataExtraKeys.IsTemplate),
                "IsTemplate is format-identity; allowlisted.");
            Assert.IsFalse(model.Metadata.Extra.ContainsKey("company"));
            Assert.IsFalse(model.Metadata.Extra.ContainsKey("printedBy"));
            Assert.AreEqual(2, result.ExtraKeysRemoved);
        }

        [TestMethod]
        public void Anonymize_FlagsMacrosRemoval_WhenStripMacrosTrue()
        {
            var model = new DocumentModel();
            model.Metadata = model.Metadata with { HasMacros = true };

            var result = DocumentAnonymizer.Anonymize(model, stripMacros: true);

            Assert.IsFalse(model.Metadata.HasMacros);
            Assert.IsTrue(result.HadMacros);
            Assert.IsTrue(model.Metadata.Extra.ContainsKey(DocumentMetadataExtraKeys.MacrosRemoved));
        }

        [TestMethod]
        public void Anonymize_KeepsMacros_WhenStripMacrosFalse()
        {
            var model = new DocumentModel();
            model.Metadata = model.Metadata with { HasMacros = true };

            DocumentAnonymizer.Anonymize(model, stripMacros: false);

            Assert.IsTrue(model.Metadata.HasMacros);
            Assert.IsFalse(model.Metadata.Extra.ContainsKey(DocumentMetadataExtraKeys.MacrosRemoved));
        }

        // ── RtfSchemaEngine.EscapeText ────────────────────────────────────

        [TestMethod]
        public void EscapeText_AsciiOnly_ReturnsAsIs()
        {
            Assert.AreEqual("hello world", RtfSchemaEngine.EscapeText("hello world"));
        }

        [TestMethod]
        public void EscapeText_StructuralChars_AreEscaped()
        {
            Assert.AreEqual(@"\\ \{ \}", RtfSchemaEngine.EscapeText(@"\ { }"));
        }

        [TestMethod]
        public void EscapeText_Accent_EmitsRtfUnicodeEscape()
        {
            // é (U+00E9) → \u233?
            Assert.AreEqual(@"\u233?", RtfSchemaEngine.EscapeText("é"));
        }

        [TestMethod]
        public void EscapeText_Null_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, RtfSchemaEngine.EscapeText(null!));
        }
    }
}
