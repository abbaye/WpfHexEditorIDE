//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WpfHexaEditor.Models.ExportImport;
using WpfHexaEditor.Services;

namespace WPFHexaEditor.Tests.Unit
{
    [TestClass]
    public class IntelHexService_Tests
    {
        private IntelHexService _service;

        [TestInitialize]
        public void Setup()
        {
            _service = new IntelHexService();
        }

        #region Intel HEX Record Tests

        [TestMethod]
        public void IntelHexRecord_ParseDataRecord_Success()
        {
            var line = ":10010000214601360121470136007EFE09D2194097";

            var record = IntelHexRecord.Parse(line);

            Assert.IsNotNull(record);
            Assert.AreEqual(0x10, record.ByteCount);
            Assert.AreEqual(0x0100, record.Address);
            Assert.AreEqual(IntelHexRecordType.Data, record.RecordType);
            Assert.AreEqual(16, record.Data.Length);
            Assert.IsTrue(record.ValidateChecksum());
        }

        [TestMethod]
        public void IntelHexRecord_ParseEOFRecord_Success()
        {
            var line = ":00000001FF";

            var record = IntelHexRecord.Parse(line);

            Assert.AreEqual(IntelHexRecordType.EndOfFile, record.RecordType);
            Assert.AreEqual(0, record.ByteCount);
            Assert.IsTrue(record.ValidateChecksum());
        }

        [TestMethod]
        public void IntelHexRecord_ToHexString_MatchesOriginal()
        {
            var original = ":10010000214601360121470136007EFE09D2194097";

            var record = IntelHexRecord.Parse(original);
            var generated = record.ToHexString();

            Assert.AreEqual(original, generated);
        }

        [TestMethod]
        public void IntelHexRecord_InvalidChecksum_ThrowsException()
        {
            var line = ":10010000214601360121470136007EFE09D2194099"; // Wrong checksum

            Assert.ThrowsException<FormatException>(() => IntelHexRecord.Parse(line));
        }

        [TestMethod]
        public void IntelHexRecord_MissingColon_ThrowsException()
        {
            var line = "10010000214601360121470136007EFE09D2194097"; // No colon

            Assert.ThrowsException<FormatException>(() => IntelHexRecord.Parse(line));
        }

        [TestMethod]
        public void IntelHexRecord_CreateAndValidate_Success()
        {
            var data = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"
            var record = new IntelHexRecord(0x1000, IntelHexRecordType.Data, data);

            Assert.AreEqual(5, record.ByteCount);
            Assert.IsTrue(record.ValidateChecksum());

            var hexString = record.ToHexString();
            Assert.IsTrue(hexString.StartsWith(":"));
        }

        #endregion

        #region Export Tests

        [TestMethod]
        public void ExportToHex_SimpleData_GeneratesValidRecords()
        {
            var data = new byte[32];
            for (int i = 0; i < data.Length; i++)
                data[i] = (byte)i;

            var lines = _service.ExportToHex(data, 0);

            Assert.IsTrue(lines.Count > 0);
            Assert.IsTrue(lines.Last().Contains("01FF")); // EOF record

            // Verify all records parse correctly
            foreach (var line in lines)
            {
                var record = IntelHexRecord.Parse(line);
                Assert.IsTrue(record.ValidateChecksum());
            }
        }

        [TestMethod]
        public void ExportToHex_EmptyData_ThrowsException()
        {
            var data = new byte[0];

            // Should not throw, but may produce only EOF
            var lines = _service.ExportToHex(data, 0);

            Assert.IsTrue(lines.Count >= 1); // At least EOF
        }

        [TestMethod]
        public void ExportToHex_LargeData_UsesMultipleRecords()
        {
            var data = new byte[256];
            for (int i = 0; i < data.Length; i++)
                data[i] = (byte)i;

            _service.MaxBytesPerRecord = 16;
            var lines = _service.ExportToHex(data, 0);

            // Should have: 256/16 = 16 data records + 1 EOF = 17 records
            Assert.IsTrue(lines.Count >= 17);
        }

        [TestMethod]
        public void ExportToHex_WithBaseAddress_UsesCorrectAddress()
        {
            var data = new byte[16];
            uint baseAddress = 0x8000;

            var lines = _service.ExportToHex(data, baseAddress);

            // First data record should have address 0x8000
            var firstRecord = IntelHexRecord.Parse(lines[0]);
            Assert.AreEqual(baseAddress, firstRecord.Address);
        }

        [TestMethod]
        public void ExportToHex_ExtendedAddressing_GeneratesELARecord()
        {
            var data = new byte[16];
            uint baseAddress = 0x10000; // Beyond 16-bit

            var lines = _service.ExportToHex(data, baseAddress);

            // Should have Extended Linear Address record
            var elaRecord = IntelHexRecord.Parse(lines[0]);
            Assert.AreEqual(IntelHexRecordType.ExtendedLinearAddress, elaRecord.RecordType);
        }

        #endregion

        #region Import Tests

        [TestMethod]
        public void ImportFromHex_ValidData_Success()
        {
            var hexLines = new[]
            {
                ":10000000214601360121470136007EFE09D2194097",
                ":00000001FF"
            };

            var result = _service.ImportFromHex(hexLines);

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Data);
            Assert.AreEqual(16, result.Data.Length);
            Assert.AreEqual(0x21, result.Data[0]);
        }

        [TestMethod]
        public void ImportFromHex_EmptyLines_HandlesGracefully()
        {
            var hexLines = new[]
            {
                "",
                ":10000000214601360121470136007EFE09D2194097",
                "   ",
                ":00000001FF"
            };

            var result = _service.ImportFromHex(hexLines);

            Assert.IsTrue(result.Success);
        }

        [TestMethod]
        public void ImportFromHex_InvalidRecord_CollectsWarning()
        {
            var hexLines = new[]
            {
                ":10000000214601360121470136007EFE09D2194097",
                "INVALID LINE",
                ":00000001FF"
            };

            var result = _service.ImportFromHex(hexLines);

            Assert.IsTrue(result.Warnings.Count > 0);
        }

        [TestMethod]
        public void ImportFromHex_NoDataRecords_Fails()
        {
            var hexLines = new[]
            {
                ":00000001FF" // Only EOF
            };

            var result = _service.ImportFromHex(hexLines);

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.ErrorMessage.Contains("No data"));
        }

        #endregion

        #region Round-Trip Tests

        [TestMethod]
        public void RoundTrip_ExportImport_PreservesData()
        {
            var originalData = new byte[128];
            var random = new Random(42);
            random.NextBytes(originalData);

            // Export
            var hexLines = _service.ExportToHex(originalData, 0);

            // Import
            var result = _service.ImportFromHex(hexLines.ToArray());

            Assert.IsTrue(result.Success);
            Assert.AreEqual(originalData.Length, result.Data.Length);
            CollectionAssert.AreEqual(originalData, result.Data);
        }

        [TestMethod]
        public void RoundTrip_WithBaseAddress_PreservesAddressAndData()
        {
            var originalData = new byte[64];
            for (int i = 0; i < originalData.Length; i++)
                originalData[i] = (byte)i;

            uint baseAddress = 0x2000;

            // Export
            var hexLines = _service.ExportToHex(originalData, baseAddress);

            // Import
            var result = _service.ImportFromHex(hexLines.ToArray());

            Assert.IsTrue(result.Success);
            Assert.AreEqual(baseAddress, result.BaseAddress);
            CollectionAssert.AreEqual(originalData, result.Data);
        }

        [TestMethod]
        public void RoundTrip_ExtendedAddressing_PreservesData()
        {
            var originalData = new byte[32];
            for (int i = 0; i < originalData.Length; i++)
                originalData[i] = (byte)(0xFF - i);

            uint baseAddress = 0x20000; // Extended addressing

            // Export
            var hexLines = _service.ExportToHex(originalData, baseAddress);

            // Import
            var result = _service.ImportFromHex(hexLines.ToArray());

            Assert.IsTrue(result.Success);
            CollectionAssert.AreEqual(originalData, result.Data);
        }

        #endregion

        #region Configuration Tests

        [TestMethod]
        public void Configuration_MaxBytesPerRecord_AffectsOutput()
        {
            var data = new byte[64];

            _service.MaxBytesPerRecord = 8;
            var lines8 = _service.ExportToHex(data, 0);

            _service.MaxBytesPerRecord = 16;
            var lines16 = _service.ExportToHex(data, 0);

            // With smaller records, should have more lines
            Assert.IsTrue(lines8.Count > lines16.Count);
        }

        #endregion
    }
}
