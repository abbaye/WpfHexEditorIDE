//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using WpfHexEditor.HexEditor;
using WpfHexEditor.Core;

namespace WpfHexEditor.Tests
{
    /// <summary>
    /// Phase 1: Unit tests for Legacy API data retrieval methods
    /// Tests the 6 methods for Legacy compatibility
    ///
    /// Note: These tests use ByteProvider directly instead of the full HexEditor control
    /// to avoid WPF UI threading complexities in unit tests.
    /// </summary>
    [TestClass]
    public class Phase1_DataRetrievalTests
    {
        #region Test Helpers

        /// <summary>
        /// Creates a ByteProvider instance with test data
        /// </summary>
        private WpfHexEditor.Core.Bytes.ByteProvider CreateProviderWithData(byte[] data)
        {
            var provider = new WpfHexEditor.Core.Bytes.ByteProvider();
            provider.OpenMemory(data);
            return provider;
        }

        /// <summary>
        /// Creates test data with known pattern
        /// </summary>
        private byte[] CreateTestData(int length)
        {
            var data = new byte[length];
            for (int i = 0; i < length; i++)
            {
                data[i] = (byte)(i % 256);
            }
            return data;
        }

        #endregion

        #region GetByte Tests (ByteProvider.GetByte)

        [TestMethod]
        public void GetByte_ValidPosition_ReturnsCorrectByte()
        {
            // Arrange
            var testData = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55 };
            var provider = CreateProviderWithData(testData);

            // Act
            var result = provider.GetByte(2);

            // Assert
            Assert.IsTrue(result.success, "GetByte should succeed");
            Assert.AreEqual((byte)0x22, result.value, "Byte value should be 0x22");
        }

        [TestMethod]
        public void GetByte_FirstPosition_ReturnsFirstByte()
        {
            // Arrange
            var testData = new byte[] { 0xAA, 0xBB, 0xCC };
            var provider = CreateProviderWithData(testData);

            // Act
            var result = provider.GetByte(0);

            // Assert
            Assert.IsTrue(result.success);
            Assert.AreEqual((byte)0xAA, result.value);
        }

        [TestMethod]
        public void GetByte_LastPosition_ReturnsLastByte()
        {
            // Arrange
            var testData = new byte[] { 0xAA, 0xBB, 0xCC };
            var provider = CreateProviderWithData(testData);

            // Act
            var result = provider.GetByte(2);

            // Assert
            Assert.IsTrue(result.success);
            Assert.AreEqual((byte)0xCC, result.value);
        }

        [TestMethod]
        public void GetByte_InvalidPosition_ReturnsFalse()
        {
            // Arrange
            var testData = new byte[] { 0xAA, 0xBB, 0xCC };
            var provider = CreateProviderWithData(testData);

            // Act
            var result = provider.GetByte(100);

            // Assert
            Assert.IsFalse(result.success, "GetByte should fail for out-of-bounds position");
        }

        [TestMethod]
        public void GetByte_NegativePosition_ReturnsFalse()
        {
            // Arrange
            var testData = new byte[] { 0xAA, 0xBB, 0xCC };
            var provider = CreateProviderWithData(testData);

            // Act
            var result = provider.GetByte(-1);

            // Assert
            Assert.IsFalse(result.success);
        }

        #endregion

        #region GetByteModifieds Tests (ByteProvider.GetByteModifieds)

        [TestMethod]
        public void GetByteModifieds_NoModifications_ReturnsEmptyDictionary()
        {
            // Arrange
            var testData = CreateTestData(100);
            var provider = CreateProviderWithData(testData);

            // Act
            var modified = provider.GetByteModifieds(ByteAction.Modified);

            // Assert
            Assert.IsNotNull(modified);
            Assert.AreEqual(0, modified.Count);
        }

        [TestMethod]
        public void GetByteModifieds_AfterModification_ReturnsModifiedBytes()
        {
            // Arrange
            var testData = CreateTestData(100);
            var provider = CreateProviderWithData(testData);

            // Modify a byte
            provider.ModifyByte(50, 0xFF);

            // Act
            var modified = provider.GetByteModifieds(ByteAction.Modified);

            // Assert
            Assert.IsNotNull(modified);
            Assert.AreEqual(1, modified.Count);
            Assert.IsTrue(modified.ContainsKey(50));
        }

        #endregion

        #region GetBytes Tests (ByteProvider.GetBytes - equivalent to GetCopyData)

        [TestMethod]
        public void GetBytes_ValidRange_ReturnsCorrectBytes()
        {
            // Arrange
            var testData = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66 };
            var provider = CreateProviderWithData(testData);

            // Act
            var result = provider.GetBytes(2, 3); // 3 bytes starting at position 2

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.Length);
            Assert.AreEqual(0x22, result[0]);
            Assert.AreEqual(0x33, result[1]);
            Assert.AreEqual(0x44, result[2]);
        }

        [TestMethod]
        public void GetBytes_SingleByte_ReturnsSingleByte()
        {
            // Arrange
            var testData = new byte[] { 0xAA, 0xBB, 0xCC };
            var provider = CreateProviderWithData(testData);

            // Act
            var result = provider.GetBytes(1, 1);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual(0xBB, result[0]);
        }

        [TestMethod]
        public void GetBytes_EntireFile_ReturnsAllBytes()
        {
            // Arrange
            var testData = new byte[] { 0x00, 0x11, 0x22, 0x33 };
            var provider = CreateProviderWithData(testData);

            // Act
            var result = provider.GetBytes(0, testData.Length);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(4, result.Length);
            CollectionAssert.AreEqual(testData, result);
        }

        [TestMethod]
        public void GetBytes_OutOfBounds_ReturnsClampedRange()
        {
            // Arrange
            var testData = new byte[] { 0xAA, 0xBB, 0xCC };
            var provider = CreateProviderWithData(testData);

            // Act - request beyond file length (should be clamped)
            var result = provider.GetBytes(0, 100);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.Length);
            CollectionAssert.AreEqual(testData, result);
        }

        #endregion

        #region Integration Tests

        [TestMethod]
        public void Integration_GetByteAndGetRange_ConsistentResults()
        {
            // Arrange
            var testData = CreateTestData(100);
            var provider = CreateProviderWithData(testData);

            // Act - Compare GetByte vs GetBytes for same position
            var resultGetByte = provider.GetByte(50);
            var arrayFromGetBytes = provider.GetBytes(50, 1);

            // Assert
            Assert.IsTrue(resultGetByte.success);
            Assert.AreEqual(1, arrayFromGetBytes.Length);
            Assert.AreEqual(resultGetByte.value, arrayFromGetBytes[0]);
        }

        [TestMethod]
        public void Integration_ModifyAndRetrieve_CorrectlyReflectsChanges()
        {
            // Arrange
            var testData = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44 };
            var provider = CreateProviderWithData(testData);

            // Act - Modify a byte
            provider.ModifyByte(2, 0xFF);

            // Read it back
            var result = provider.GetByte(2);

            // Assert
            Assert.IsTrue(result.success);
            Assert.AreEqual((byte)0xFF, result.value);

            // Check modified bytes dictionary
            var modified = provider.GetByteModifieds(ByteAction.Modified);
            Assert.AreEqual(1, modified.Count);
            Assert.IsTrue(modified.ContainsKey(2));
        }

        #endregion

        #region Performance Tests

        [TestMethod]
        public void Performance_GetByte_MultipleReads()
        {
            // Arrange
            var testData = CreateTestData(100000); // 100KB
            var provider = CreateProviderWithData(testData);

            // Act & Assert - Should complete in reasonable time
            var startTime = DateTime.Now;

            for (int i = 0; i < 1000; i++)
            {
                var result = provider.GetByte(i * 10);
                Assert.IsTrue(result.success);
            }

            var elapsed = DateTime.Now - startTime;
            Assert.IsTrue(elapsed.TotalSeconds < 1.0,
                $"GetByte took too long: {elapsed.TotalSeconds}s");
        }

        [TestMethod]
        public void Performance_GetRange_LargeRange()
        {
            // Arrange
            var testData = CreateTestData(100000); // 100KB
            var provider = CreateProviderWithData(testData);

            // Act
            var startTime = DateTime.Now;
            var result = provider.GetBytes(0, 100000);
            var elapsed = DateTime.Now - startTime;

            // Assert
            Assert.AreEqual(100000, result.Length);
            Assert.IsTrue(elapsed.TotalSeconds < 1.0,
                $"GetRange took too long: {elapsed.TotalSeconds}s");
        }

        #endregion
    }
}
