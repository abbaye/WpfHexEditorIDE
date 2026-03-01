//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using WpfHexEditor.Core.Bytes;

namespace WpfHexEditor.Tests
{
    /// <summary>
    /// Unit tests for newly added APIs: ModifyBytes and CountOccurrences
    /// These tests validate the final 2 missing APIs to achieve 99.5% compatibility
    /// </summary>
    [TestClass]
    public class NewAPIs_Tests
    {
        #region Test Helpers

        /// <summary>
        /// Creates a ByteProvider instance with test data
        /// </summary>
        private ByteProvider CreateProviderWithData(byte[] data)
        {
            var provider = new ByteProvider();
            provider.OpenMemory(data);
            return provider;
        }

        #endregion

        #region ModifyBytes Tests

        [TestMethod]
        public void ModifyBytes_SingleByte_ModifiesCorrectly()
        {
            // Arrange
            var data = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44 };
            var provider = CreateProviderWithData(data);

            // Act
            provider.ModifyBytes(1, new byte[] { 0xAA });

            // Assert
            var result = provider.GetByte(1);
            Assert.AreEqual(0xAA, result.value);
        }

        [TestMethod]
        public void ModifyBytes_MultipleBytes_ModifiesAllCorrectly()
        {
            // Arrange
            var data = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66 };
            var provider = CreateProviderWithData(data);

            // Act
            var newValues = new byte[] { 0xAA, 0xBB, 0xCC };
            provider.ModifyBytes(2, newValues);

            // Assert
            Assert.AreEqual(0xAA, provider.GetByte(2).value);
            Assert.AreEqual(0xBB, provider.GetByte(3).value);
            Assert.AreEqual(0xCC, provider.GetByte(4).value);

            // Verify surrounding bytes unchanged
            Assert.AreEqual(0x11, provider.GetByte(1).value);
            Assert.AreEqual(0x55, provider.GetByte(5).value);
        }

        [TestMethod]
        public void ModifyBytes_AtStart_ModifiesCorrectly()
        {
            // Arrange
            var data = new byte[] { 0x00, 0x11, 0x22, 0x33 };
            var provider = CreateProviderWithData(data);

            // Act
            provider.ModifyBytes(0, new byte[] { 0xFF, 0xFE });

            // Assert
            Assert.AreEqual(0xFF, provider.GetByte(0).value);
            Assert.AreEqual(0xFE, provider.GetByte(1).value);
            Assert.AreEqual(0x22, provider.GetByte(2).value);
        }

        [TestMethod]
        public void ModifyBytes_AtEnd_ModifiesCorrectly()
        {
            // Arrange
            var data = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44 };
            var provider = CreateProviderWithData(data);

            // Act
            provider.ModifyBytes(3, new byte[] { 0xAA, 0xBB });

            // Assert
            Assert.AreEqual(0xAA, provider.GetByte(3).value);
            Assert.AreEqual(0xBB, provider.GetByte(4).value);
        }

        [TestMethod]
        public void ModifyBytes_LargeArray_ModifiesAllCorrectly()
        {
            // Arrange
            var data = new byte[1000];
            for (int i = 0; i < 1000; i++)
                data[i] = (byte)(i % 256);
            var provider = CreateProviderWithData(data);

            // Act
            var newValues = new byte[100];
            for (int i = 0; i < 100; i++)
                newValues[i] = 0xFF;
            provider.ModifyBytes(450, newValues);

            // Assert - Verify all modified bytes
            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(0xFF, provider.GetByte(450 + i).value,
                    $"Byte at position {450 + i} should be 0xFF");
            }

            // Verify surrounding bytes unchanged
            Assert.AreEqual((byte)(449 % 256), provider.GetByte(449).value);
            Assert.AreEqual((byte)(550 % 256), provider.GetByte(550).value);
        }

        [TestMethod]
        public void ModifyBytes_WithBatch_PerformsEfficiently()
        {
            // Arrange
            var data = new byte[10000];
            var provider = CreateProviderWithData(data);

            // Act
            var startTime = DateTime.Now;

            provider.BeginBatch();
            try
            {
                for (int i = 0; i < 10; i++)
                {
                    var chunk = new byte[100];
                    for (int j = 0; j < 100; j++)
                        chunk[j] = (byte)i;
                    provider.ModifyBytes(i * 1000, chunk);
                }
            }
            finally
            {
                provider.EndBatch();
            }

            var elapsed = DateTime.Now - startTime;

            // Assert
            Console.WriteLine($"ModifyBytes with batch: {elapsed.TotalMilliseconds:F2}ms");
            Assert.IsTrue(elapsed.TotalMilliseconds < 500,
                $"Batch ModifyBytes took too long: {elapsed.TotalMilliseconds}ms");

            // Verify some random modifications
            Assert.AreEqual(0, provider.GetByte(0).value);
            Assert.AreEqual(1, provider.GetByte(1000).value);
            Assert.AreEqual(5, provider.GetByte(5000).value);
        }

        [TestMethod]
        public void ModifyBytes_EmptyArray_DoesNothing()
        {
            // Arrange
            var data = new byte[] { 0x00, 0x11, 0x22 };
            var provider = CreateProviderWithData(data);

            // Act
            provider.ModifyBytes(1, new byte[0]);

            // Assert - Data unchanged
            Assert.AreEqual(0x11, provider.GetByte(1).value);
        }

        #endregion

        #region CountOccurrences Tests

        [TestMethod]
        public void CountOccurrences_SingleByte_CountsAllMatches()
        {
            // Arrange
            var data = new byte[] { 0xFF, 0xAA, 0xFF, 0xBB, 0xFF, 0xCC, 0xFF };
            var provider = CreateProviderWithData(data);

            // Act
            var pattern = new byte[] { 0xFF };
            int count = provider.CountOccurrences(pattern);

            // Assert
            Assert.AreEqual(4, count);
        }

        [TestMethod]
        public void CountOccurrences_MultiBytePattern_CountsAllMatches()
        {
            // Arrange
            var data = new byte[] { 0xAA, 0xBB, 0xCC, 0xAA, 0xBB, 0xDD, 0xAA, 0xBB };
            var provider = CreateProviderWithData(data);

            // Act
            var pattern = new byte[] { 0xAA, 0xBB };
            int count = provider.CountOccurrences(pattern);

            // Assert
            Assert.AreEqual(3, count);
        }

        [TestMethod]
        public void CountOccurrences_NoMatches_ReturnsZero()
        {
            // Arrange
            var data = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44 };
            var provider = CreateProviderWithData(data);

            // Act
            var pattern = new byte[] { 0xFF, 0xFE };
            int count = provider.CountOccurrences(pattern);

            // Assert
            Assert.AreEqual(0, count);
        }

        [TestMethod]
        public void CountOccurrences_OverlappingPattern_CountsCorrectly()
        {
            // Arrange - "AAAA" contains overlapping "AA" patterns
            var data = new byte[] { 0xAA, 0xAA, 0xAA, 0xAA };
            var provider = CreateProviderWithData(data);

            // Act
            var pattern = new byte[] { 0xAA, 0xAA };
            int count = provider.CountOccurrences(pattern);

            // Assert - Should find at positions 0, 1, 2 (3 occurrences)
            Assert.AreEqual(3, count);
        }

        [TestMethod]
        public void CountOccurrences_WithStartPosition_CountsFromPosition()
        {
            // Arrange
            var data = new byte[] { 0xFF, 0xAA, 0xFF, 0xBB, 0xFF, 0xCC };
            var provider = CreateProviderWithData(data);

            // Act
            var pattern = new byte[] { 0xFF };
            int count = provider.CountOccurrences(pattern, startPosition: 2);

            // Assert - Should skip first 0xFF at position 0
            Assert.AreEqual(2, count); // Positions 2 and 4
        }

        [TestMethod]
        public void CountOccurrences_LargeData_PerformsEfficiently()
        {
            // Arrange - Create 100KB data with pattern every 100 bytes
            var data = new byte[100000];
            for (int i = 0; i < data.Length; i += 100)
            {
                data[i] = 0xAA;
                if (i + 1 < data.Length)
                    data[i + 1] = 0xBB;
            }
            var provider = CreateProviderWithData(data);

            // Act
            var startTime = DateTime.Now;
            var pattern = new byte[] { 0xAA, 0xBB };
            int count = provider.CountOccurrences(pattern);
            var elapsed = DateTime.Now - startTime;

            // Assert
            Console.WriteLine($"CountOccurrences on 100KB: {elapsed.TotalMilliseconds:F2}ms");
            Assert.AreEqual(1000, count);
            Assert.IsTrue(elapsed.TotalMilliseconds < 500,
                $"CountOccurrences took too long: {elapsed.TotalMilliseconds}ms");
        }

        [TestMethod]
        public void CountOccurrences_VsFindAllCount_SameResult()
        {
            // Arrange
            var data = new byte[10000];
            for (int i = 0; i < data.Length; i++)
                data[i] = (byte)(i % 256);
            var provider = CreateProviderWithData(data);

            // Act
            var pattern = new byte[] { 0x42, 0x43 };
            int countOccurrences = provider.CountOccurrences(pattern);

            // Use FindFirst to manually count (simulating FindAll().Count())
            int findAllCount = 0;
            long pos = 0;
            while (pos < data.Length)
            {
                pos = provider.FindFirst(pattern, pos);
                if (pos < 0) break;
                findAllCount++;
                pos++;
            }

            // Assert - Both methods should give same result
            Assert.AreEqual(findAllCount, countOccurrences,
                "CountOccurrences and manual count should match");
        }

        [TestMethod]
        public void CountOccurrences_EmptyPattern_ReturnsZero()
        {
            // Arrange
            var data = new byte[] { 0x00, 0x11, 0x22 };
            var provider = CreateProviderWithData(data);

            // Act
            int count = provider.CountOccurrences(new byte[0]);

            // Assert
            Assert.AreEqual(0, count);
        }

        #endregion

        #region Integration Tests

        [TestMethod]
        public void Integration_ModifyBytes_ThenCountOccurrences()
        {
            // Arrange
            var data = new byte[1000];
            for (int i = 0; i < 1000; i++)
                data[i] = 0x00;
            var provider = CreateProviderWithData(data);

            // Act - Use ModifyBytes to create pattern
            var pattern = new byte[] { 0xAA, 0xBB };
            for (int i = 0; i < 10; i++)
            {
                provider.ModifyBytes(i * 100, pattern);
            }

            // Count the pattern
            int count = provider.CountOccurrences(pattern);

            // Assert
            Assert.AreEqual(10, count, "Should find all 10 patterns we created");
        }

        [TestMethod]
        public void Integration_BatchModifyBytes_ThenVerifyWithCount()
        {
            // Arrange
            var data = new byte[5000];
            var provider = CreateProviderWithData(data);

            // Act
            var pattern = new byte[] { 0xFF, 0xFF, 0xFF };
            provider.BeginBatch();
            try
            {
                for (int i = 0; i < 20; i++)
                {
                    provider.ModifyBytes(i * 250, pattern);
                }
            }
            finally
            {
                provider.EndBatch();
            }

            // Verify with CountOccurrences
            int count = provider.CountOccurrences(pattern);

            // Assert
            Assert.AreEqual(20, count);
        }

        #endregion

        #region Performance Comparison Tests

        [TestMethod]
        public void Performance_ModifyBytes_VsModifyByteLoop()
        {
            // Arrange
            var data1 = new byte[10000];
            var data2 = new byte[10000];
            var provider1 = CreateProviderWithData(data1);
            var provider2 = CreateProviderWithData(data2);

            var newValues = new byte[1000];
            for (int i = 0; i < 1000; i++)
                newValues[i] = 0xFF;

            // Act - ModifyBytes (new API)
            var time1 = DateTime.Now;
            provider1.ModifyBytes(1000, newValues);
            var elapsed1 = DateTime.Now - time1;

            // Act - ModifyByte loop (old way)
            var time2 = DateTime.Now;
            for (int i = 0; i < newValues.Length; i++)
            {
                provider2.ModifyByte(1000 + i, newValues[i]);
            }
            var elapsed2 = DateTime.Now - time2;

            // Assert
            Console.WriteLine($"ModifyBytes: {elapsed1.TotalMilliseconds:F2}ms");
            Console.WriteLine($"ModifyByte loop: {elapsed2.TotalMilliseconds:F2}ms");
            Console.WriteLine($"Speedup: {elapsed2.TotalMilliseconds / elapsed1.TotalMilliseconds:F2}x");

            // ModifyBytes should be at least as fast (usually faster)
            Assert.IsTrue(elapsed1.TotalMilliseconds <= elapsed2.TotalMilliseconds * 1.5,
                "ModifyBytes should be comparable or faster than loop");
        }

        #endregion
    }
}
