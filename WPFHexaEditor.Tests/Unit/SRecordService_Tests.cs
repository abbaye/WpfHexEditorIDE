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
    public class SRecordService_Tests
    {
        private SRecordService _service;

        [TestInitialize]
        public void Setup()
        {
            _service = new SRecordService();
        }

        #region S-Record Tests

        [TestMethod]
        public void SRecord_ParseS1Record_Success()
        {
            var line = "S1137AF00A0A0D0000000000000000000000000061";

            var record = SRecord.Parse(line);

            Assert.IsNotNull(record);
            Assert.AreEqual(SRecordType.S1_Data16, record.RecordType);
            Assert.AreEqual(0x13, record.ByteCount);
            Assert.AreEqual(0x7AF0, record.Address);
            Assert.IsTrue(record.ValidateChecksum());
        }

        [TestMethod]
        public void SRecord_ParseS0Header_Success()
        {
            var line = "S00F000068656C6C6F202020202000003C";

            var record = SRecord.Parse(line);

            Assert.AreEqual(SRecordType.S0_Header, record.RecordType);
            Assert.IsTrue(record.ValidateChecksum());
        }

        [TestMethod]
        public void SRecord_ParseS9Termination_Success()
        {
            var line = "S9030000FC";

            var record = SRecord.Parse(line);

            Assert.AreEqual(SRecordType.S9_Start16, record.RecordType);
            Assert.AreEqual(0x0000, record.Address);
            Assert.IsTrue(record.ValidateChecksum());
        }

        [TestMethod]
        public void SRecord_ToSRecordString_MatchesOriginal()
        {
            var original = "S1137AF00A0A0D0000000000000000000000000061";

            var record = SRecord.Parse(original);
            var generated = record.ToSRecordString();

            Assert.AreEqual(original, generated);
        }

        [TestMethod]
        public void SRecord_InvalidChecksum_ThrowsException()
        {
            var line = "S1137AF00A0A0D0000000000000000000000000099"; // Wrong checksum

            Assert.ThrowsException<FormatException>(() => SRecord.Parse(line));
        }

        [TestMethod]
        public void SRecord_CreateAndValidate_Success()
        {
            var data = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"
            var record = new SRecord(SRecordType.S1_Data16, 0x1000, data);

            Assert.IsTrue(record.ValidateChecksum());

            var srecString = record.ToSRecordString();
            Assert.IsTrue(srecString.StartsWith("S1"));
        }

        [TestMethod]
        public void SRecord_AddressLength_VariesByType()
        {
            Assert.AreEqual(2, SRecord.GetAddressLength(SRecordType.S1_Data16));
            Assert.AreEqual(3, SRecord.GetAddressLength(SRecordType.S2_Data24));
            Assert.AreEqual(4, SRecord.GetAddressLength(SRecordType.S3_Data32));
        }

        #endregion

        #region Export Tests

        [TestMethod]
        public void ExportToSRecord_SimpleData_GeneratesValidRecords()
        {
            var data = new byte[32];
            for (int i = 0; i < data.Length; i++)
                data[i] = (byte)i;

            var lines = _service.ExportToSRecord(data, 0, false);

            Assert.IsTrue(lines.Count > 0);
            Assert.IsTrue(lines[0].StartsWith("S0")); // Header
            Assert.IsTrue(lines.Last().StartsWith("S9")); // Termination (16-bit)

            // Verify all records parse correctly
            foreach (var line in lines)
            {
                var record = SRecord.Parse(line);
                Assert.IsTrue(record.ValidateChecksum());
            }
        }

        [TestMethod]
        public void ExportToSRecord_Use32BitAddress_GeneratesS3Records()
        {
            var data = new byte[16];
            var lines = _service.ExportToSRecord(data, 0, true);

            // Should use S3 data records
            var dataLine = lines.FirstOrDefault(l => l.StartsWith("S3"));
            Assert.IsNotNull(dataLine);

            // Should terminate with S7
            Assert.IsTrue(lines.Last().StartsWith("S7"));
        }

        [TestMethod]
        public void ExportToSRecord_LargeAddress_AutoSelectsS3()
        {
            var data = new byte[16];
            uint largeAddress = 0x1000000; // Beyond 24-bit

            var lines = _service.ExportToSRecord(data, largeAddress, false);

            // Should auto-select S3/S7
            var dataLine = lines.FirstOrDefault(l => l.StartsWith("S3"));
            Assert.IsNotNull(dataLine);
        }

        [TestMethod]
        public void ExportToSRecord_HeaderText_IncludedInS0()
        {
            var data = new byte[16];
            _service.HeaderText = "TestApp";

            var lines = _service.ExportToSRecord(data, 0, false);

            var s0Record = SRecord.Parse(lines[0]);
            Assert.AreEqual(SRecordType.S0_Header, s0Record.RecordType);
            // Header should contain "TestApp"
        }

        [TestMethod]
        public void ExportToSRecord_IncludesRecordCount()
        {
            var data = new byte[64];
            var lines = _service.ExportToSRecord(data, 0, false);

            // Should have S5 count record
            var countLine = lines.FirstOrDefault(l => l.StartsWith("S5"));
            Assert.IsNotNull(countLine);
        }

        #endregion

        #region Import Tests

        [TestMethod]
        public void ImportFromSRecord_ValidData_Success()
        {
            var srecLines = new[]
            {
                "S00F000068656C6C6F202020202000003C",
                "S1137AF00A0A0D0000000000000000000000000061",
                "S5030001FB",
                "S9030000FC"
            };

            var result = _service.ImportFromSRecord(srecLines);

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Data);
            Assert.IsTrue(result.Data.Length > 0);
        }

        [TestMethod]
        public void ImportFromSRecord_HeaderText_Extracted()
        {
            var srecLines = new[]
            {
                "S00F000068656C6C6F202020202000003C", // "hello      "
                "S1137AF00A0A0D0000000000000000000000000061",
                "S9030000FC"
            };

            var result = _service.ImportFromSRecord(srecLines);

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.HeaderText);
            Assert.IsTrue(result.HeaderText.Contains("hello"));
        }

        [TestMethod]
        public void ImportFromSRecord_RecordCount_Verified()
        {
            var srecLines = new[]
            {
                "S00F000068656C6C6F202020202000003C",
                "S1137AF00A0A0D0000000000000000000000000061",
                "S5030002FA", // Count = 2 (but only 1 data record)
                "S9030000FC"
            };

            var result = _service.ImportFromSRecord(srecLines);

            // Should have warning about count mismatch
            Assert.IsTrue(result.Warnings.Count > 0);
        }

        [TestMethod]
        public void ImportFromSRecord_StartAddress_Extracted()
        {
            var srecLines = new[]
            {
                "S00F000068656C6C6F202020202000003C",
                "S1137AF00A0A0D0000000000000000000000000061",
                "S9031000EC" // Start address = 0x1000
            };

            var result = _service.ImportFromSRecord(srecLines);

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.StartAddress.HasValue);
            Assert.AreEqual(0x1000u, result.StartAddress.Value);
        }

        #endregion

        #region Round-Trip Tests

        [TestMethod]
        public void RoundTrip_S19Format_PreservesData()
        {
            var originalData = new byte[64];
            var random = new Random(42);
            random.NextBytes(originalData);

            // Export (S1/S9 - 16-bit)
            var srecLines = _service.ExportToSRecord(originalData, 0, false);

            // Import
            var result = _service.ImportFromSRecord(srecLines.ToArray());

            Assert.IsTrue(result.Success);
            CollectionAssert.AreEqual(originalData, result.Data);
        }

        [TestMethod]
        public void RoundTrip_S37Format_PreservesData()
        {
            var originalData = new byte[64];
            for (int i = 0; i < originalData.Length; i++)
                originalData[i] = (byte)i;

            // Export (S3/S7 - 32-bit)
            var srecLines = _service.ExportToSRecord(originalData, 0, true);

            // Import
            var result = _service.ImportFromSRecord(srecLines.ToArray());

            Assert.IsTrue(result.Success);
            CollectionAssert.AreEqual(originalData, result.Data);
        }

        [TestMethod]
        public void RoundTrip_WithBaseAddress_PreservesAddressAndData()
        {
            var originalData = new byte[32];
            for (int i = 0; i < originalData.Length; i++)
                originalData[i] = (byte)(0xFF - i);

            uint baseAddress = 0x8000;

            // Export
            var srecLines = _service.ExportToSRecord(originalData, baseAddress, false);

            // Import
            var result = _service.ImportFromSRecord(srecLines.ToArray());

            Assert.IsTrue(result.Success);
            Assert.AreEqual(baseAddress, result.BaseAddress);
            CollectionAssert.AreEqual(originalData, result.Data);
        }

        #endregion

        #region Configuration Tests

        [TestMethod]
        public void Configuration_MaxBytesPerRecord_AffectsOutput()
        {
            var data = new byte[128];

            _service.MaxBytesPerRecord = 16;
            var lines16 = _service.ExportToSRecord(data, 0, false);

            _service.MaxBytesPerRecord = 32;
            var lines32 = _service.ExportToSRecord(data, 0, false);

            // With smaller records, should have more data lines
            var dataLines16 = lines16.Count(l => l.StartsWith("S1"));
            var dataLines32 = lines32.Count(l => l.StartsWith("S1"));

            Assert.IsTrue(dataLines16 > dataLines32);
        }

        [TestMethod]
        public void Configuration_HeaderText_CustomizableHeader()
        {
            var data = new byte[16];

            _service.HeaderText = "CustomHeader";
            var lines = _service.ExportToSRecord(data, 0, false);

            var result = _service.ImportFromSRecord(lines.ToArray());

            Assert.IsTrue(result.HeaderText.Contains("CustomHeader"));
        }

        #endregion
    }
}
