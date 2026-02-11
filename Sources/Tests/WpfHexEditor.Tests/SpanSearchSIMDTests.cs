using System;
using System.Linq;
using Xunit;
using WpfHexaEditor.Core.MethodExtention;

namespace WpfHexEditor.Tests
{
    /// <summary>
    /// Unit tests for SIMD-accelerated search extensions
    /// </summary>
    public class SpanSearchSIMDTests
    {
        [Fact]
        public void IsSimdAvailable_ReturnsValue()
        {
            // Act
            bool available = SpanSearchSIMDExtensions.IsSimdAvailable;

            // Assert - Just verify it returns without error
            Assert.True(available || !available); // Tautology, but verifies property access works
        }

        [Fact]
        public void GetSimdInfo_ReturnsNonEmpty()
        {
            // Act
            string info = SpanSearchSIMDExtensions.GetSimdInfo();

            // Assert
            Assert.False(string.IsNullOrEmpty(info));
        }

        [Fact]
        public void FindFirstSIMD_FindsSingleByte()
        {
            // Arrange
            byte[] data = { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            byte needle = 5;
            ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(data);

            // Act
            long result = span.FindFirstSIMD(needle, baseOffset: 0);

            // Assert
            Assert.Equal(4, result);
        }

        [Fact]
        public void FindFirstSIMD_ReturnsNegativeForNoMatch()
        {
            // Arrange
            byte[] data = { 1, 2, 3, 4, 5 };
            byte needle = 99;
            ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(data);

            // Act
            long result = span.FindFirstSIMD(needle, baseOffset: 0);

            // Assert
            Assert.Equal(-1, result);
        }

        [Fact]
        public void FindFirstSIMD_AppliesBaseOffset()
        {
            // Arrange
            byte[] data = { 1, 2, 3, 4, 5 };
            byte needle = 3;
            ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(data);
            long baseOffset = 1000;

            // Act
            long result = span.FindFirstSIMD(needle, baseOffset);

            // Assert
            Assert.Equal(1002, result); // 2 + 1000
        }

        [Fact]
        public void FindAllSIMD_FindsMultipleOccurrences()
        {
            // Arrange
            byte[] data = { 1, 2, 1, 4, 1, 6, 1, 8, 1 };
            byte needle = 1;
            ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(data);

            // Act
            var results = span.FindAllSIMD(needle, baseOffset: 0);

            // Assert
            Assert.Equal(5, results.Count);
            Assert.Equal(0, results[0]);
            Assert.Equal(2, results[1]);
            Assert.Equal(4, results[2]);
            Assert.Equal(6, results[3]);
            Assert.Equal(8, results[4]);
        }

        [Fact]
        public void FindAllSIMD_HandlesEmptyData()
        {
            // Arrange
            byte[] data = Array.Empty<byte>();
            byte needle = 1;
            ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(data);

            // Act
            var results = span.FindAllSIMD(needle, baseOffset: 0);

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public void FindAllSIMD_AppliesBaseOffset()
        {
            // Arrange
            byte[] data = { 1, 2, 1, 4, 1 };
            byte needle = 1;
            ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(data);
            long baseOffset = 500;

            // Act
            var results = span.FindAllSIMD(needle, baseOffset);

            // Assert
            Assert.Equal(3, results.Count);
            Assert.Equal(500, results[0]); // 0 + 500
            Assert.Equal(502, results[1]); // 2 + 500
            Assert.Equal(504, results[2]); // 4 + 500
        }

        [Fact]
        public void FindAllSIMD_LargeBuffer()
        {
            // Arrange - Test with buffer larger than SIMD vector size (256 bytes for AVX2)
            byte[] data = new byte[512];
            byte needle = 42;

            // Insert pattern at known positions
            for (int i = 0; i < data.Length; i += 50)
            {
                data[i] = needle;
            }

            ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(data);

            // Act
            var results = span.FindAllSIMD(needle, baseOffset: 0);

            // Assert
            Assert.True(results.Count >= 10); // At least 10 occurrences (512/50 = 10.24)
            Assert.All(results, pos => Assert.Equal(needle, data[pos]));
        }

        [Fact]
        public void CountOccurrencesSIMD_CountsCorrectly()
        {
            // Arrange
            byte[] data = { 1, 2, 1, 4, 1, 6, 1, 8, 1 };
            byte needle = 1;
            ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(data);

            // Act
            int count = span.CountOccurrencesSIMD(needle);

            // Assert
            Assert.Equal(5, count);
        }

        [Fact]
        public void CountOccurrencesSIMD_ReturnsZeroForNoMatch()
        {
            // Arrange
            byte[] data = { 1, 2, 3, 4, 5 };
            byte needle = 99;
            ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(data);

            // Act
            int count = span.CountOccurrencesSIMD(needle);

            // Assert
            Assert.Equal(0, count);
        }

        [Fact]
        public void CountOccurrencesSIMD_HandlesEmptyData()
        {
            // Arrange
            byte[] data = Array.Empty<byte>();
            byte needle = 1;
            ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(data);

            // Act
            int count = span.CountOccurrencesSIMD(needle);

            // Assert
            Assert.Equal(0, count);
        }

        [Fact]
        public void CountOccurrencesSIMD_LargeBuffer()
        {
            // Arrange
            byte[] data = new byte[1000];
            byte needle = 42;
            int expectedCount = 0;

            // Insert needle at every 10th position
            for (int i = 0; i < data.Length; i += 10)
            {
                data[i] = needle;
                expectedCount++;
            }

            ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(data);

            // Act
            int count = span.CountOccurrencesSIMD(needle);

            // Assert
            Assert.Equal(expectedCount, count);
        }

        [Fact]
        public void FindAll2BytePatternSIMD_FindsPattern()
        {
            // Arrange
            byte[] data = { 1, 2, 3, 0xAA, 0xBB, 5, 6, 0xAA, 0xBB, 9 };
            byte[] pattern = { 0xAA, 0xBB };
            ReadOnlySpan<byte> dataSpan = new ReadOnlySpan<byte>(data);
            ReadOnlySpan<byte> patternSpan = new ReadOnlySpan<byte>(pattern);

            // Act
            var results = dataSpan.FindAll2BytePatternSIMD(patternSpan, baseOffset: 0);

            // Assert
            Assert.Equal(2, results.Count);
            Assert.Equal(3, results[0]);
            Assert.Equal(7, results[1]);
        }

        [Fact]
        public void FindAll2BytePatternSIMD_NoMatch()
        {
            // Arrange
            byte[] data = { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            byte[] pattern = { 0xFF, 0xFF };
            ReadOnlySpan<byte> dataSpan = new ReadOnlySpan<byte>(data);
            ReadOnlySpan<byte> patternSpan = new ReadOnlySpan<byte>(pattern);

            // Act
            var results = dataSpan.FindAll2BytePatternSIMD(patternSpan, baseOffset: 0);

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public void FindAll2BytePatternSIMD_AppliesBaseOffset()
        {
            // Arrange
            byte[] data = { 1, 2, 3, 0xAA, 0xBB, 5 };
            byte[] pattern = { 0xAA, 0xBB };
            ReadOnlySpan<byte> dataSpan = new ReadOnlySpan<byte>(data);
            ReadOnlySpan<byte> patternSpan = new ReadOnlySpan<byte>(pattern);
            long baseOffset = 2000;

            // Act
            var results = dataSpan.FindAll2BytePatternSIMD(patternSpan, baseOffset);

            // Assert
            Assert.Single(results);
            Assert.Equal(2003, results[0]); // 3 + 2000
        }

        [Fact]
        public void FindAll2BytePatternSIMD_InvalidPatternSize()
        {
            // Arrange
            byte[] data = { 1, 2, 3, 4, 5 };
            byte[] pattern = { 1, 2, 3 }; // 3 bytes, not 2
            ReadOnlySpan<byte> dataSpan = new ReadOnlySpan<byte>(data);
            ReadOnlySpan<byte> patternSpan = new ReadOnlySpan<byte>(pattern);

            // Act
            var results = dataSpan.FindAll2BytePatternSIMD(patternSpan, baseOffset: 0);

            // Assert
            Assert.Empty(results); // Should return empty for invalid pattern size
        }

        [Fact]
        public void SIMDMethods_ConsistentWithStandardMethods()
        {
            // This test verifies SIMD methods produce same results as standard Span methods

            // Arrange
            byte[] data = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
            byte needle = 128;

            ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(data);

            // Act
            long simdFirst = span.FindFirstSIMD(needle, 0);
            long standardFirst = span.IndexOf(needle);

            var simdAll = span.FindAllSIMD(needle, 0);
            int simdCount = span.CountOccurrencesSIMD(needle);

            // Assert
            Assert.Equal(standardFirst, simdFirst);
            Assert.Equal(1, simdCount); // Should find exactly 1
            Assert.Single(simdAll);
            Assert.Equal(simdFirst, simdAll[0]);
        }
    }
}
