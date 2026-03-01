//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using Microsoft.VisualStudio.TestTools.UnitTesting;
using WpfHexEditor.Core;

namespace WpfHexEditor.Tests
{
    [TestClass]
    public class FormattedValueCache_Tests
    {
        [TestMethod]
        public void TryGet_EmptyCache_ReturnsFalse()
        {
            // Arrange
            var cache = new FormattedValueCache();

            // Act
            bool result = cache.TryGet(0, 4, "uint32", "hex", 42, out string value);

            // Assert
            Assert.IsFalse(result);
            Assert.IsNull(value);
        }

        [TestMethod]
        public void Set_ThenGet_ReturnsValue()
        {
            // Arrange
            var cache = new FormattedValueCache();
            cache.Set(0, 4, "uint32", "hex", 42, "0x0000002A");

            // Act
            bool result = cache.TryGet(0, 4, "uint32", "hex", 42, out string value);

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual("0x0000002A", value);
        }

        [TestMethod]
        public void Get_DifferentOffset_CacheMiss()
        {
            // Arrange
            var cache = new FormattedValueCache();
            cache.Set(0, 4, "uint32", "hex", 42, "0x0000002A");

            // Act
            bool result = cache.TryGet(4, 4, "uint32", "hex", 42, out string value);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void Get_DifferentFormatter_CacheMiss()
        {
            // Arrange
            var cache = new FormattedValueCache();
            cache.Set(0, 4, "uint32", "hex", 42, "0x0000002A");

            // Act
            bool result = cache.TryGet(0, 4, "uint32", "decimal", 42, out string value);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void Get_DifferentValue_CacheMiss()
        {
            // Arrange
            var cache = new FormattedValueCache();
            cache.Set(0, 4, "uint32", "hex", 42, "0x0000002A");

            // Act
            bool result = cache.TryGet(0, 4, "uint32", "hex", 100, out string value);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void Clear_RemovesAllEntries()
        {
            // Arrange
            var cache = new FormattedValueCache();
            cache.Set(0, 4, "uint32", "hex", 42, "0x0000002A");
            cache.Set(4, 2, "uint16", "hex", 100, "0x0064");

            // Act
            cache.Clear();
            bool result1 = cache.TryGet(0, 4, "uint32", "hex", 42, out _);
            bool result2 = cache.TryGet(4, 2, "uint16", "hex", 100, out _);

            // Assert
            Assert.IsFalse(result1);
            Assert.IsFalse(result2);
        }

        [TestMethod]
        public void GetStatistics_TracksHitsAndMisses()
        {
            // Arrange
            var cache = new FormattedValueCache();
            cache.Set(0, 4, "uint32", "hex", 42, "0x0000002A");

            // Act
            cache.TryGet(0, 4, "uint32", "hex", 42, out _); // Hit
            cache.TryGet(0, 4, "uint32", "hex", 42, out _); // Hit
            cache.TryGet(4, 4, "uint32", "hex", 50, out _); // Miss
            var stats = cache.GetStatistics();

            // Assert
            Assert.AreEqual(1, stats.CachedItems);
            Assert.AreEqual(2, stats.Hits);
            Assert.AreEqual(1, stats.Misses);
            Assert.AreEqual(66.67, stats.HitRate, 0.01); // 2 / 3 * 100 = 66.67%
        }

        [TestMethod]
        public void ResetStatistics_ClearsCounters()
        {
            // Arrange
            var cache = new FormattedValueCache();
            cache.Set(0, 4, "uint32", "hex", 42, "0x0000002A");
            cache.TryGet(0, 4, "uint32", "hex", 42, out _); // Hit
            cache.TryGet(4, 4, "uint32", "hex", 50, out _); // Miss

            // Act
            cache.ResetStatistics();
            var stats = cache.GetStatistics();

            // Assert
            Assert.AreEqual(1, stats.CachedItems); // Cache items remain
            Assert.AreEqual(0, stats.Hits);
            Assert.AreEqual(0, stats.Misses);
            Assert.AreEqual(0, stats.HitRate);
        }

        [TestMethod]
        public void InvalidateFormatter_RemovesMatchingEntries()
        {
            // Arrange
            var cache = new FormattedValueCache();
            cache.Set(0, 4, "uint32", "hex", 42, "0x0000002A");
            cache.Set(4, 4, "uint32", "decimal", 42, "42");
            cache.Set(8, 2, "uint16", "hex", 100, "0x0064");

            // Act
            cache.InvalidateFormatter("hex");
            bool hex1 = cache.TryGet(0, 4, "uint32", "hex", 42, out _);
            bool decimal1 = cache.TryGet(4, 4, "uint32", "decimal", 42, out _);
            bool hex2 = cache.TryGet(8, 2, "uint16", "hex", 100, out _);

            // Assert
            Assert.IsFalse(hex1, "Hex entry should be invalidated");
            Assert.IsTrue(decimal1, "Decimal entry should remain");
            Assert.IsFalse(hex2, "Hex entry should be invalidated");
        }

        [TestMethod]
        public void Cache_EvictsOldestEntries_WhenFull()
        {
            // Arrange
            var cache = new FormattedValueCache(maxCacheSize: 100);

            // Fill cache beyond capacity
            for (int i = 0; i < 150; i++)
            {
                cache.Set(i, 4, "uint32", "hex", i, $"0x{i:X8}");
            }

            // Act
            var stats = cache.GetStatistics();

            // Assert
            Assert.IsTrue(stats.CachedItems <= 100, "Cache should evict old entries");
            Assert.IsTrue(stats.CachedItems >= 75, "Should keep at least 75% after eviction");

            // Verify that newer entries are retained
            bool hasNew = cache.TryGet(149, 4, "uint32", "hex", 149, out _);
            Assert.IsTrue(hasNew, "Newest entry should be retained");
        }

        [TestMethod]
        public void Cache_HandlesNullValues()
        {
            // Arrange
            var cache = new FormattedValueCache();

            // Act
            cache.Set(0, 4, "uint32", "hex", null, "null");
            bool result = cache.TryGet(0, 4, "uint32", "hex", null, out string value);

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual("null", value);
        }

        [TestMethod]
        public void Cache_MultipleFormatters_IndependentCaching()
        {
            // Arrange
            var cache = new FormattedValueCache();

            // Act - Same offset/value, different formatters
            cache.Set(0, 4, "uint32", "hex", 42, "0x0000002A");
            cache.Set(0, 4, "uint32", "decimal", 42, "42");
            cache.Set(0, 4, "uint32", "mixed", 42, "42 (0x2A)");

            bool hex = cache.TryGet(0, 4, "uint32", "hex", 42, out string hexValue);
            bool dec = cache.TryGet(0, 4, "uint32", "decimal", 42, out string decValue);
            bool mix = cache.TryGet(0, 4, "uint32", "mixed", 42, out string mixValue);

            // Assert
            Assert.IsTrue(hex && dec && mix, "All formatters should be cached independently");
            Assert.AreEqual("0x0000002A", hexValue);
            Assert.AreEqual("42", decValue);
            Assert.AreEqual("42 (0x2A)", mixValue);
        }
    }
}
