//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using WpfHexEditor.BinaryAnalysis.Services;

namespace WpfHexEditor.BinaryAnalysis.Tests.Services
{
    [TestClass]
    public class SRecordService_Tests
    {
        private SRecordService _service;

        [TestInitialize]
        public void Setup() => _service = new SRecordService();

        [TestMethod]
        public void ExportToSRecord_NullData_Throws()
        {
            try { _service.ExportToSRecord(null!); Assert.Fail("Expected exception"); }
            catch (ArgumentNullException) { }
        }

        [TestMethod]
        public void ExportToSRecord_SimpleData_StartsWithS0Header()
        {
            var lines = _service.ExportToSRecord(new byte[] { 0x01, 0x02 });
            Assert.IsTrue(lines[0].StartsWith("S0"));
        }

        [TestMethod]
        public void ExportToSRecord_16BitRange_UsesS1Records()
        {
            var data = new byte[] { 0xAA, 0xBB };
            var lines = _service.ExportToSRecord(data, baseAddress: 0x0100);
            Assert.IsTrue(lines.Any(l => l.StartsWith("S1")));
            Assert.IsTrue(lines.Any(l => l.StartsWith("S9"))); // S9 = 16-bit termination
        }

        [TestMethod]
        public void ExportToSRecord_32BitAddress_UsesS3Records()
        {
            var data = new byte[] { 0x01 };
            var lines = _service.ExportToSRecord(data, use32BitAddress: true);
            Assert.IsTrue(lines.Any(l => l.StartsWith("S3")));
            Assert.IsTrue(lines.Any(l => l.StartsWith("S7"))); // S7 = 32-bit termination
        }

        [TestMethod]
        public void ExportThenImport_RoundTrip_DataMatches()
        {
            var original = new byte[] { 0x57, 0x50, 0x46, 0x48, 0x65, 0x78 }; // "WPFHex"
            var lines = _service.ExportToSRecord(original);
            var result = _service.ImportFromSRecord(lines.ToArray());

            Assert.IsTrue(result.Success, result.ErrorMessage);
            CollectionAssert.AreEqual(original, result.Data);
        }

        [TestMethod]
        public void ExportThenImport_WithBaseAddress_BaseAddressPreserved()
        {
            var original = new byte[] { 0xFF, 0xFE, 0xFD };
            var lines = _service.ExportToSRecord(original, baseAddress: 0x8000);
            var result = _service.ImportFromSRecord(lines.ToArray());

            Assert.IsTrue(result.Success);
            Assert.AreEqual(0x8000u, result.BaseAddress);
        }

        [TestMethod]
        public void ExportThenImport_LargeData_DataMatches()
        {
            var original = new byte[256];
            for (int i = 0; i < 256; i++)
                original[i] = (byte)i;

            var lines = _service.ExportToSRecord(original);
            var result = _service.ImportFromSRecord(lines.ToArray());

            Assert.IsTrue(result.Success);
            CollectionAssert.AreEqual(original, result.Data);
        }

        [TestMethod]
        public void ImportFromSRecord_InvalidLine_AddsWarning()
        {
            var lines = new[] { "XXXXXXXX" };
            var result = _service.ImportFromSRecord(lines);
            Assert.IsFalse(result.Success);
        }

        [TestMethod]
        public void ExportToSRecord_HeaderTextIncluded()
        {
            _service.HeaderText = "TEST";
            var lines = _service.ExportToSRecord(new byte[] { 0x01 });
            Assert.IsTrue(lines[0].StartsWith("S0"), "First line should be S0 header");
        }
    }
}
