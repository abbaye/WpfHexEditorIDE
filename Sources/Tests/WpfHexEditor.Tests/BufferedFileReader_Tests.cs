//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using WpfHexEditor.Core;

namespace WpfHexEditor.Tests
{
    [TestClass]
    public class BufferedFileReader_Tests
    {
        private MemoryStream _stream;
        private byte[] _testData;

        [TestInitialize]
        public void Setup()
        {
            // Create test data: 1000 bytes (0, 1, 2, ... 255, 0, 1, 2, ...)
            _testData = new byte[1000];
            for (int i = 0; i < _testData.Length; i++)
            {
                _testData[i] = (byte)(i % 256);
            }
            _stream = new MemoryStream(_testData);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _stream?.Dispose();
        }

        [TestMethod]
        public void ReadBytes_FirstRead_ReturnsCorrectData()
        {
            // Arrange
            using var reader = new BufferedFileReader(_stream, blockSize: 256);

            // Act
            byte[] result = reader.ReadBytes(0, 10);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(10, result.Length);
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, result);
        }

        [TestMethod]
        public void ReadBytes_SequentialReads_UsesCache()
        {
            // Arrange
            using var reader = new BufferedFileReader(_stream, blockSize: 256);

            // Act - Read multiple times within same block
            byte[] read1 = reader.ReadBytes(0, 10);
            long pos1 = _stream.Position; // Should be at 256 after reading block
            byte[] read2 = reader.ReadBytes(10, 10);
            long pos2 = _stream.Position; // Should still be at 256 (cache hit)
            byte[] read3 = reader.ReadBytes(20, 10);
            long pos3 = _stream.Position; // Should still be at 256 (cache hit)

            // Assert
            Assert.IsNotNull(read1);
            Assert.IsNotNull(read2);
            Assert.IsNotNull(read3);
            Assert.AreEqual(256, pos1, "First read should load block");
            Assert.AreEqual(256, pos2, "Second read should use cache");
            Assert.AreEqual(256, pos3, "Third read should use cache");
        }

        [TestMethod]
        public void ReadBytes_AcrossBlockBoundary_LoadsNewBlock()
        {
            // Arrange
            using var reader = new BufferedFileReader(_stream, blockSize: 100);

            // Act
            byte[] read1 = reader.ReadBytes(0, 10); // Load block 0-99
            byte[] read2 = reader.ReadBytes(150, 10); // Load new block 150-249

            // Assert
            Assert.IsNotNull(read1);
            Assert.IsNotNull(read2);
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual((byte)i, read1[i]);
                Assert.AreEqual((byte)((150 + i) % 256), read2[i]);
            }
        }

        [TestMethod]
        public void ReadBytes_LargerThanBlock_ReadsDirect()
        {
            // Arrange
            using var reader = new BufferedFileReader(_stream, blockSize: 64);

            // Act - Read more than block size
            byte[] result = reader.ReadBytes(0, 100);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(100, result.Length);
            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual((byte)i, result[i]);
            }
        }

        [TestMethod]
        public void ReadBytes_InvalidOffset_ReturnsNull()
        {
            // Arrange
            using var reader = new BufferedFileReader(_stream, blockSize: 256);

            // Act
            byte[] result = reader.ReadBytes(-1, 10);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void ReadBytes_OffsetBeyondStream_ReturnsNull()
        {
            // Arrange
            using var reader = new BufferedFileReader(_stream, blockSize: 256);

            // Act
            byte[] result = reader.ReadBytes(2000, 10);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void ReadBytes_LengthExceedsStream_ClampsToStreamEnd()
        {
            // Arrange
            using var reader = new BufferedFileReader(_stream, blockSize: 256);

            // Act
            byte[] result = reader.ReadBytes(990, 20); // Only 10 bytes available

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(10, result.Length);
        }

        [TestMethod]
        public void ReadBytes_ZeroLength_ReturnsEmptyArray()
        {
            // Arrange
            using var reader = new BufferedFileReader(_stream, blockSize: 256);

            // Act
            byte[] result = reader.ReadBytes(0, 0);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Length);
        }

        [TestMethod]
        public void InvalidateCache_ForcesReload()
        {
            // Arrange
            using var reader = new BufferedFileReader(_stream, blockSize: 256);
            reader.ReadBytes(0, 10); // Load block
            var (start1, length1, _) = reader.GetCacheInfo();

            // Act
            reader.InvalidateCache();
            var (start2, length2, _) = reader.GetCacheInfo();
            reader.ReadBytes(0, 10); // Should reload
            var (start3, length3, _) = reader.GetCacheInfo();

            // Assert
            Assert.IsTrue(start2 < 0, "Cache should be invalidated");
            Assert.AreEqual(0, start3, "Cache should be reloaded after read");
            Assert.IsTrue(length3 > 0, "Reloaded cache should have valid length");
        }

        [TestMethod]
        public void GetCacheInfo_ReturnsCorrectInfo()
        {
            // Arrange
            using var reader = new BufferedFileReader(_stream, blockSize: 512);

            // Act
            reader.ReadBytes(100, 10);
            var (start, length, blockSize) = reader.GetCacheInfo();

            // Assert
            Assert.AreEqual(100, start, "Buffer should start at read offset");
            Assert.IsTrue(length > 0, "Buffer should have valid length");
            Assert.AreEqual(512, blockSize, "Block size should match constructor");
        }

        [TestMethod]
        public void ReadBytes_AfterDispose_ThrowsException()
        {
            // Arrange
            var reader = new BufferedFileReader(_stream, blockSize: 256);
            reader.Dispose();

            // Act & Assert
            try
            {
                reader.ReadBytes(0, 10);
                Assert.Fail("Expected ObjectDisposedException");
            }
            catch (ObjectDisposedException)
            {
                // Expected
            }
        }

        [TestMethod]
        public void Constructor_NullStream_ThrowsException()
        {
            // Act & Assert
            try
            {
                var reader = new BufferedFileReader(null);
                Assert.Fail("Expected ArgumentNullException");
            }
            catch (ArgumentNullException)
            {
                // Expected
            }
        }

        [TestMethod]
        public void Constructor_InvalidBlockSize_ThrowsException()
        {
            // Act & Assert
            try
            {
                var reader = new BufferedFileReader(_stream, blockSize: 0);
                Assert.Fail("Expected ArgumentException");
            }
            catch (ArgumentException)
            {
                // Expected
            }
        }

        [TestMethod]
        public void ReadBytes_RandomAccess_WorksCorrectly()
        {
            // Arrange
            using var reader = new BufferedFileReader(_stream, blockSize: 128);

            // Act - Random access pattern
            byte[] read1 = reader.ReadBytes(500, 5);
            byte[] read2 = reader.ReadBytes(10, 5);
            byte[] read3 = reader.ReadBytes(900, 5);
            byte[] read4 = reader.ReadBytes(15, 5); // Should cache read2's block

            // Assert
            Assert.IsNotNull(read1);
            Assert.IsNotNull(read2);
            Assert.IsNotNull(read3);
            Assert.IsNotNull(read4);

            // Verify data correctness
            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual((byte)((500 + i) % 256), read1[i]);
                Assert.AreEqual((byte)(10 + i), read2[i]);
                Assert.AreEqual((byte)((900 + i) % 256), read3[i]);
                Assert.AreEqual((byte)(15 + i), read4[i]);
            }
        }

        [TestMethod]
        public void ReadBytes_PerformanceTest_Sequential()
        {
            // Arrange
            using var reader = new BufferedFileReader(_stream, blockSize: 1024);
            int iterations = 100;

            // Act
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                reader.ReadBytes(i * 4, 4);
            }
            watch.Stop();

            // Assert
            Assert.IsTrue(watch.ElapsedMilliseconds < 50, $"Sequential reads should be fast, took {watch.ElapsedMilliseconds}ms");
        }
    }
}
