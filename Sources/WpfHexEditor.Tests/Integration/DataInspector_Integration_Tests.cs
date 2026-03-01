//////////////////////////////////////////////
// Apache 2.0  - 2026
// Data Inspector Integration Tests
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com)
//////////////////////////////////////////////

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using WpfHexEditor.BinaryAnalysis.Services;

namespace WpfHexEditor.Tests.Integration
{
    [TestClass]
    public class DataInspector_Integration_Tests
    {
        private DataInspectorService _service;

        [TestInitialize]
        public void Setup()
        {
            _service = new DataInspectorService();
        }

        [TestMethod]
        public void InterpretBytes_EmptyArray_ReturnsEmptyList()
        {
            // Arrange
            var bytes = new byte[0];

            // Act
            var results = _service.InterpretBytes(bytes);

            // Assert
            Assert.IsNotNull(results);
            Assert.AreEqual(0, results.Count);
        }

        [TestMethod]
        public void InterpretBytes_SingleByte_ReturnsIntegerAndBitInterpretations()
        {
            // Arrange
            var bytes = new byte[] { 0x42 };

            // Act
            var results = _service.InterpretBytes(bytes);

            // Assert
            Assert.IsTrue(results.Count > 0);
            Assert.IsTrue(results.Any(r => r.Category == "Integer"));
            Assert.IsTrue(results.Any(r => r.Category == "Bits"));
        }

        [TestMethod]
        public void InterpretBytes_FourBytes_ReturnsIPv4Interpretation()
        {
            // Arrange
            var bytes = new byte[] { 192, 168, 1, 1 }; // 192.168.1.1

            // Act
            var results = _service.InterpretBytes(bytes);

            // Assert
            var ipv4 = results.FirstOrDefault(r => r.Format == "IPv4 Address");
            Assert.IsNotNull(ipv4);
            Assert.AreEqual("192.168.1.1", ipv4.Value);
        }

        [TestMethod]
        public void InterpretBytes_SixteenBytes_ReturnsGUIDInterpretation()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var bytes = guid.ToByteArray();

            // Act
            var results = _service.InterpretBytes(bytes);

            // Assert
            var guidResult = results.FirstOrDefault(r => r.Format == "GUID/UUID");
            Assert.IsNotNull(guidResult);
            Assert.AreEqual(guid.ToString(), guidResult.Value);
        }

        [TestMethod]
        public void InterpretBytes_FloatBytes_ReturnsFloatInterpretation()
        {
            // Arrange
            var floatValue = 3.14159f;
            var bytes = BitConverter.GetBytes(floatValue);

            // Act
            var results = _service.InterpretBytes(bytes);

            // Assert
            var floatResult = results.FirstOrDefault(r => r.Format == "Float32 LE");
            Assert.IsNotNull(floatResult);
            Assert.IsTrue(floatResult.Value.StartsWith("3.14"));
        }

        [TestMethod]
        public void InterpretBytes_UnixTimestamp_ReturnsDateInterpretation()
        {
            // Arrange
            var unixTimestamp = 1609459200u; // 2021-01-01 00:00:00 UTC
            var bytes = BitConverter.GetBytes(unixTimestamp);

            // Act
            var results = _service.InterpretBytes(bytes);

            // Assert
            var dateResult = results.FirstOrDefault(r => r.Format == "Unix Timestamp (32-bit)");
            Assert.IsNotNull(dateResult);
            Assert.IsTrue(dateResult.Value.Contains("2021-01-01"));
        }

        [TestMethod]
        public void InterpretBytes_RGBBytes_ReturnsColorInterpretation()
        {
            // Arrange
            var bytes = new byte[] { 255, 0, 0 }; // Red

            // Act
            var results = _service.InterpretBytes(bytes);

            // Assert
            var colorResult = results.FirstOrDefault(r => r.Format == "RGB");
            Assert.IsNotNull(colorResult);
            Assert.IsTrue(colorResult.Value.Contains("R=255"));
            Assert.IsTrue(colorResult.HexValue == "#FF0000");
        }

        [TestMethod]
        public void InterpretBytes_LargeArray_ReturnsMultipleInterpretations()
        {
            // Arrange
            var bytes = new byte[16];
            new Random().NextBytes(bytes);

            // Act
            var results = _service.InterpretBytes(bytes);

            // Assert
            Assert.IsTrue(results.Count > 20); // Should have many interpretations
            Assert.IsTrue(results.Any(r => r.Category == "Integer"));
            Assert.IsTrue(results.Any(r => r.Category == "Float"));
            Assert.IsTrue(results.Any(r => r.Category == "Basic"));
        }
    }
}
