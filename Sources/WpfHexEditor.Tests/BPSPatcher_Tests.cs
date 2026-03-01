//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Text;
using WpfHexEditor.Core.RomHacking;

namespace WpfHexEditor.Tests
{
    [TestClass]
    public class BPSPatcher_Tests
    {
        // ------------------------------------------------------------------
        // Helper
        // ------------------------------------------------------------------

        private static byte[] Range(int start, int count)
        {
            var b = new byte[count];
            for (int i = 0; i < count; i++) b[i] = (byte)((start + i) & 0xFF);
            return b;
        }

        // ------------------------------------------------------------------
        // CRC32
        // ------------------------------------------------------------------

        [TestMethod]
        public void CRC32_KnownValue()
        {
            // CRC32 of "123456789" = 0xCBF43926
            var input = Encoding.ASCII.GetBytes("123456789");
            uint crc = CRC32.Compute(input);
            Assert.AreEqual(0xCBF43926u, crc, "CRC32 of '123456789' must equal 0xCBF43926");
        }

        // ------------------------------------------------------------------
        // Validation
        // ------------------------------------------------------------------

        [TestMethod]
        public void IsValidBPSBytes_ValidHeader_ReturnsTrue()
        {
            var data = Encoding.ASCII.GetBytes("BPS1XXXX");
            Assert.IsTrue(BPSPatcher.IsValidBPSBytes(data));
        }

        [TestMethod]
        public void IsValidBPSBytes_WrongHeader_ReturnsFalse()
        {
            var data = Encoding.ASCII.GetBytes("PATCH123");
            Assert.IsFalse(BPSPatcher.IsValidBPSBytes(data));
        }

        [TestMethod]
        public void IsValidBPSBytes_Null_ReturnsFalse()
            => Assert.IsFalse(BPSPatcher.IsValidBPSBytes(null));

        // ------------------------------------------------------------------
        // Round-trip: CreatePatch → ApplyPatch
        // ------------------------------------------------------------------

        [TestMethod]
        public void RoundTrip_SimpleModification_ProducesCorrectTarget()
        {
            var original = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 };
            var modified = new byte[] { 0x00, 0x01, 0xFF, 0x03, 0x04, 0x05, 0x06, 0x07 };

            byte[] patch  = BPSPatcher.CreatePatch(original, modified);
            var result = BPSPatcher.ApplyPatch(original, patch, out byte[] target);

            Assert.IsTrue(result.Success, result.ErrorMessage);
            CollectionAssert.AreEqual(modified, target, "Round-trip target must equal modified");
        }

        [TestMethod]
        public void RoundTrip_Identical_ProducesCorrectTarget()
        {
            var original = Range(0, 64);
            byte[] patch  = BPSPatcher.CreatePatch(original, original);
            var result = BPSPatcher.ApplyPatch(original, patch, out byte[] target);

            Assert.IsTrue(result.Success, result.ErrorMessage);
            CollectionAssert.AreEqual(original, target);
        }

        [TestMethod]
        public void RoundTrip_LargeRepeatingData_RLELike()
        {
            var original = new byte[256];
            var modified = new byte[256];
            Array.Fill(modified, (byte)0xAA);

            byte[] patch  = BPSPatcher.CreatePatch(original, modified);
            var result = BPSPatcher.ApplyPatch(original, patch, out byte[] target);

            Assert.IsTrue(result.Success, result.ErrorMessage);
            CollectionAssert.AreEqual(modified, target);
        }

        [TestMethod]
        public void RoundTrip_TargetLonger_Insertion()
        {
            var original = Range(0, 8);
            var modified = new byte[] { 0x00, 0x01, 0x02, 0xAA, 0xBB, 0x03, 0x04, 0x05, 0x06, 0x07 };

            byte[] patch  = BPSPatcher.CreatePatch(original, modified);
            var result = BPSPatcher.ApplyPatch(original, patch, out byte[] target);

            Assert.IsTrue(result.Success, result.ErrorMessage);
            CollectionAssert.AreEqual(modified, target);
        }

        [TestMethod]
        public void RoundTrip_TargetShorter_Deletion()
        {
            var original = Range(0, 10);
            var modified = new byte[] { 0x00, 0x01, 0x02, 0x05, 0x06, 0x07, 0x08, 0x09 };

            byte[] patch  = BPSPatcher.CreatePatch(original, modified);
            var result = BPSPatcher.ApplyPatch(original, patch, out byte[] target);

            Assert.IsTrue(result.Success, result.ErrorMessage);
            CollectionAssert.AreEqual(modified, target);
        }

        [TestMethod]
        public void RoundTrip_LargeFile_SequentialBytes()
        {
            var original = Range(0, 2048);
            var modified  = (byte[])original.Clone();
            modified[512] = 0xFF;
            modified[1024] = 0xAB;

            byte[] patch  = BPSPatcher.CreatePatch(original, modified);
            var result = BPSPatcher.ApplyPatch(original, patch, out byte[] target);

            Assert.IsTrue(result.Success, result.ErrorMessage);
            CollectionAssert.AreEqual(modified, target);
        }

        // ------------------------------------------------------------------
        // Header / CRC validation
        // ------------------------------------------------------------------

        [TestMethod]
        public void ApplyPatch_CorruptedCRC_Fails()
        {
            var original = Range(0, 16);
            var modified  = Range(1, 16);

            byte[] patch = BPSPatcher.CreatePatch(original, modified);
            // Corrupt the patch CRC (last 4 bytes)
            patch[patch.Length - 1] ^= 0xFF;

            var result = BPSPatcher.ApplyPatch(original, patch, out _);
            Assert.IsFalse(result.Success, "Corrupted patch CRC should cause failure");
        }

        [TestMethod]
        public void ApplyPatch_WrongSource_SourceCRCFails()
        {
            var original = Range(0, 16);
            var modified  = Range(1, 16);
            byte[] patch  = BPSPatcher.CreatePatch(original, modified);

            // Apply with a different source
            var wrongSource = Range(10, 16);
            var result = BPSPatcher.ApplyPatch(wrongSource, patch, out _);
            Assert.IsFalse(result.Success, "Wrong source should fail source CRC check");
        }

        // ------------------------------------------------------------------
        // WhjsonPatchMetadata
        // ------------------------------------------------------------------

        [TestMethod]
        public void WhjsonMetadata_GenerateAndValidate_Succeeds()
        {
            var original = Range(0, 32);
            var modified  = Range(1, 32);

            var meta = WhjsonPatchMetadata.Generate(PatchFormat.BPS, original, modified, "source.rom", "target.rom");

            Assert.AreEqual("BPS", meta.Format);
            Assert.AreEqual(32, meta.SourceFile.Size);
            Assert.AreEqual(32, meta.TargetFile.Size);
            Assert.IsTrue(meta.Validate(original, modified), "CRC32 validation must pass");
            Assert.IsFalse(meta.Validate(modified, original), "Swapped arrays must fail validation");
        }

        [TestMethod]
        public void WhjsonMetadata_SerializeDeserialize_RoundTrip()
        {
            var original = Range(0, 32);
            var modified  = Range(1, 32);
            var meta = WhjsonPatchMetadata.Generate(PatchFormat.BPS, original, modified, "a.rom", "b.rom", "Author", "Desc");

            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".whjson");
            try
            {
                meta.Save(tempPath);
                var loaded = WhjsonPatchMetadata.Load(tempPath);

                Assert.IsNotNull(loaded);
                Assert.AreEqual("BPS",    loaded.Format);
                Assert.AreEqual("Author", loaded.Author);
                Assert.AreEqual("Desc",   loaded.Description);
                Assert.AreEqual(meta.SourceFile.Crc32, loaded.SourceFile.Crc32);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        [TestMethod]
        public void WhjsonMetadata_MetadataPathFor_CorrectExtension()
        {
            var path = WhjsonPatchMetadata.MetadataPathFor(@"C:\patches\myrom.bps");
            Assert.AreEqual(@"C:\patches\myrom.whjson", path);
        }
    }
}
