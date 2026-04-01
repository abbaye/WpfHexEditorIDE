//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using WpfHexEditor.Core.BinaryAnalysis.Services;

namespace WpfHexEditor.BinaryAnalysis.Tests.Services
{
    [TestClass]
    public class IntelHexService_Tests
    {
        private IntelHexService _service = null!;

        [TestInitialize]
        public void Setup() => _service = new IntelHexService();

        [TestMethod]
        public void ExportToHex_NullData_Throws()
        {
            try { _service.ExportToHex(null); Assert.Fail("Expected exception"); }
            catch (ArgumentNullException) { }
        }

        [TestMethod]
        public void ExportToHex_SimpleData_StartsWithColon()
        {
            var data = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            var lines = _service.ExportToHex(data);
            Assert.IsTrue(lines.All(l => l.StartsWith(":")));
        }

        [TestMethod]
        public void ExportToHex_SimpleData_EndsWithEOF()
        {
            var data = new byte[] { 0xAA, 0xBB };
            var lines = _service.ExportToHex(data);
            Assert.AreEqual(":00000001FF", lines.Last());
        }

        [TestMethod]
        public void ExportThenImport_RoundTrip_DataMatches()
        {
            var original = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"
            var lines = _service.ExportToHex(original);
            var result = _service.ImportFromHex(lines.ToArray());

            Assert.IsTrue(result.Success, result.ErrorMessage);
            CollectionAssert.AreEqual(original, result.Data);
        }

        [TestMethod]
        public void ExportThenImport_WithBaseAddress_DataMatches()
        {
            var original = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
            var lines = _service.ExportToHex(original, baseAddress: 0x1000);
            var result = _service.ImportFromHex(lines.ToArray());

            Assert.IsTrue(result.Success);
            Assert.AreEqual(0x1000u, result.BaseAddress);
            CollectionAssert.AreEqual(original, result.Data);
        }

        [TestMethod]
        public void ExportThenImport_LargeData_DataMatches()
        {
            var original = new byte[512];
            for (int i = 0; i < original.Length; i++)
                original[i] = (byte)(i & 0xFF);

            var lines = _service.ExportToHex(original);
            var result = _service.ImportFromHex(lines.ToArray());

            Assert.IsTrue(result.Success);
            CollectionAssert.AreEqual(original, result.Data);
        }

        [TestMethod]
        public void ImportFromHex_InvalidLine_ReturnsFailure()
        {
            var lines = new[] { "NOT_A_HEX_RECORD" };
            var result = _service.ImportFromHex(lines);
            Assert.IsFalse(result.Success);
        }

        [TestMethod]
        public void ExportToHex_CustomBytesPerRecord_RespectsLimit()
        {
            _service.MaxBytesPerRecord = 8;
            var data = new byte[32];
            var lines = _service.ExportToHex(data);

            // 32 / 8 = 4 data records + EOF
            var dataLines = lines.Where(l => l.Substring(7, 2) == "00").ToList();
            Assert.AreEqual(4, dataLines.Count);
        }
    }
}
