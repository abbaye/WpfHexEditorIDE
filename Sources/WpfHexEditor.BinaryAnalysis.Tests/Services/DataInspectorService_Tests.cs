//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using WpfHexEditor.BinaryAnalysis.Services;

namespace WpfHexEditor.BinaryAnalysis.Tests.Services
{
    [TestClass]
    public class DataInspectorService_Tests
    {
        private DataInspectorService _service;

        [TestInitialize]
        public void Setup() => _service = new DataInspectorService();

        [TestMethod]
        public void InterpretBytes_EmptyArray_ReturnsEmptyList()
        {
            var results = _service.InterpretBytes(new byte[0]);
            Assert.AreEqual(0, results.Count);
        }

        [TestMethod]
        public void InterpretBytes_OneByte_ReturnsBitsAndIntegerCategories()
        {
            var results = _service.InterpretBytes(new byte[] { 0x42 });
            Assert.IsTrue(results.Any(r => r.Category == "Integer"));
            Assert.IsTrue(results.Any(r => r.Category == "Bits"));
        }

        [TestMethod]
        public void InterpretBytes_OneByte_UInt8ValueCorrect()
        {
            var results = _service.InterpretBytes(new byte[] { 0xFF });
            var uint8 = results.FirstOrDefault(r => r.Format == "UInt8 (unsigned)");
            Assert.IsNotNull(uint8);
            Assert.AreEqual("255", uint8.Value);
        }

        [TestMethod]
        public void InterpretBytes_FourBytes_ReturnsNetworkCategory()
        {
            // 192.168.1.1
            var bytes = new byte[] { 192, 168, 1, 1 };
            var results = _service.InterpretBytes(bytes);
            Assert.IsTrue(results.Any(r => r.Category == "Network"));
        }

        [TestMethod]
        public void InterpretBytes_FourBytes_IPv4ValueCorrect()
        {
            var bytes = new byte[] { 192, 168, 1, 1 };
            var results = _service.InterpretBytes(bytes);
            var ipv4 = results.FirstOrDefault(r => r.Format == "IPv4 Address");
            Assert.IsNotNull(ipv4);
            Assert.AreEqual("192.168.1.1", ipv4.Value);
        }

        [TestMethod]
        public void InterpretBytes_FourBytes_FloatInterpretationPresent()
        {
            var results = _service.InterpretBytes(new byte[] { 0x00, 0x00, 0x80, 0x3F }); // 1.0f LE
            var floatVal = results.FirstOrDefault(r => r.Format == "Float32 LE");
            Assert.IsNotNull(floatVal);
            Assert.IsTrue(floatVal.IsValid);
        }

        [TestMethod]
        public void InterpretBytes_SixteenBytes_ReturnsGuidCategory()
        {
            var bytes = new byte[16];
            var results = _service.InterpretBytes(bytes);
            Assert.IsTrue(results.Any(r => r.Category == "GUID"));
        }

        [TestMethod]
        public void InterpretBytes_EightBytes_ReturnsInt64Interpretation()
        {
            // 1L in little-endian
            var bytes = new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 };
            var results = _service.InterpretBytes(bytes);
            var int64 = results.FirstOrDefault(r => r.Format == "Int64 LE (signed)");
            Assert.IsNotNull(int64);
            Assert.AreEqual("1", int64.Value);
        }

        [TestMethod]
        public void InterpretBytes_AllResults_HaveNonEmptyFormat()
        {
            var results = _service.InterpretBytes(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });
            foreach (var r in results)
                Assert.IsFalse(string.IsNullOrEmpty(r.Format), $"Empty format found: {r.Category}");
        }
    }
}
