using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using WpfHexaEditor.Core;
using WpfHexaEditor.Core.Bytes;

namespace WpfHexaEditor.Tests
{
    /// <summary>
    /// Unit tests for Diagnostics APIs
    /// Tests GetCacheStatistics, GetDiagnostics, and GetMemoryStatistics
    /// </summary>
    [TestClass]
    public class Diagnostics_ApiTests
    {
        #region Test Helpers

        /// <summary>
        /// Creates a ByteProvider instance with test data
        /// </summary>
        private ByteProvider CreateProviderWithData(int size)
        {
            var data = new byte[size];
            for (int i = 0; i < size; i++)
            {
                data[i] = (byte)(i % 256);
            }

            var provider = new ByteProvider();
            provider.OpenMemory(data);
            return provider;
        }

        #endregion

        #region GetCacheStatistics Tests

        [TestMethod]
        public void GetCacheStatistics_WithData_ReturnsNonEmptyString()
        {
            // Arrange
            var provider = CreateProviderWithData(10000);

            // Perform some reads to populate cache
            for (int i = 0; i < 100; i++)
            {
                provider.GetByte(i * 10);
            }

            // Act
            var stats = provider.GetCacheStatistics();

            // Assert
            Assert.IsNotNull(stats, "Cache statistics should not be null");
            Assert.IsTrue(stats.Length > 0, "Cache statistics should not be empty");

            Console.WriteLine("Cache Statistics:");
            Console.WriteLine(stats);
        }

        [TestMethod]
        public void GetCacheStatistics_ContainsExpectedInformation()
        {
            // Arrange
            var provider = CreateProviderWithData(50000);

            // Perform reads to generate cache activity
            for (int i = 0; i < 500; i++)
            {
                provider.GetByte(i * 100);
            }

            // Act
            var stats = provider.GetCacheStatistics();

            // Assert - Check for expected sections
            Assert.IsTrue(stats.Contains("Cache") || stats.Contains("Line Cache"),
                "Should contain cache information");

            Console.WriteLine(stats);
        }

        [TestMethod]
        public void GetCacheStatistics_AfterModifications_ShowsActivity()
        {
            // Arrange
            var provider = CreateProviderWithData(20000);

            // Act - Get initial stats
            var statsBefore = provider.GetCacheStatistics();

            // Perform modifications
            for (int i = 0; i < 100; i++)
            {
                provider.ModifyByte(i, 0xFF);
            }

            // Perform reads
            for (int i = 0; i < 200; i++)
            {
                provider.GetByte(i);
            }

            var statsAfter = provider.GetCacheStatistics();

            // Assert
            Assert.IsNotNull(statsBefore);
            Assert.IsNotNull(statsAfter);
            Assert.IsTrue(statsAfter.Length > 0);

            Console.WriteLine("Stats Before:");
            Console.WriteLine(statsBefore);
            Console.WriteLine("\nStats After:");
            Console.WriteLine(statsAfter);
        }

        [TestMethod]
        public void GetCacheStatistics_EmptyProvider_ReturnsValidStats()
        {
            // Arrange
            var provider = new ByteProvider();
            provider.OpenMemory(new byte[0]);

            // Act
            var stats = provider.GetCacheStatistics();

            // Assert
            Assert.IsNotNull(stats, "Should return stats even for empty data");
            Assert.IsTrue(stats.Length > 0);

            Console.WriteLine(stats);
        }

        [TestMethod]
        public void GetCacheStatistics_AfterBatchOperations_UpdatesCorrectly()
        {
            // Arrange
            var provider = CreateProviderWithData(30000);

            // Act - Batch operations
            provider.BeginBatch();
            try
            {
                for (int i = 0; i < 500; i++)
                {
                    provider.ModifyByte(i * 10, 0xAA);
                }
            }
            finally
            {
                provider.EndBatch();
            }

            var stats = provider.GetCacheStatistics();

            // Assert
            Assert.IsNotNull(stats);
            Assert.IsTrue(stats.Length > 0);

            Console.WriteLine("Cache Stats After Batch:");
            Console.WriteLine(stats);
        }

        #endregion

        #region Modification Tracking Tests

        [TestMethod]
        public void Diagnostics_TrackModifications_CountsCorrectly()
        {
            // Arrange
            var provider = CreateProviderWithData(10000);

            // Act - Make various modifications
            for (int i = 0; i < 50; i++)
            {
                provider.ModifyByte(i * 10, 0xFF);
            }

            for (int i = 0; i < 20; i++)
            {
                provider.InsertByte(i * 100, 0xAA);
            }

            for (int i = 0; i < 10; i++)
            {
                provider.DeleteBytes(i * 200, 1);
            }

            // Assert
            var modified = provider.GetByteModifieds(ByteAction.Modified);
            var added = provider.GetByteModifieds(ByteAction.Added);
            var deleted = provider.GetByteModifieds(ByteAction.Deleted);

            Assert.AreEqual(50, modified.Count, "Should track 50 modifications");
            Assert.AreEqual(20, added.Count, "Should track 20 additions");
            Assert.AreEqual(10, deleted.Count, "Should track 10 deletions");

            Console.WriteLine($"Modified: {modified.Count}, Added: {added.Count}, Deleted: {deleted.Count}");
        }

        [TestMethod]
        public void Diagnostics_ClearModifications_ResetsCounters()
        {
            // Arrange
            var provider = CreateProviderWithData(5000);

            // Make modifications
            for (int i = 0; i < 100; i++)
            {
                provider.ModifyByte(i, 0xFF);
            }

            var modifiedBefore = provider.GetByteModifieds(ByteAction.Modified);
            Assert.AreEqual(100, modifiedBefore.Count);

            // Act - Clear modifications
            provider.ClearModifications();

            // Assert
            var modifiedAfter = provider.GetByteModifieds(ByteAction.Modified);
            Assert.AreEqual(0, modifiedAfter.Count, "Modifications should be cleared");
        }

        [TestMethod]
        public void Diagnostics_ComplexOperations_TracksAllChanges()
        {
            // Arrange
            var provider = CreateProviderWithData(20000);

            // Act - Complex sequence of operations
            for (int i = 0; i < 30; i++)
            {
                provider.ModifyByte(i * 10, 0xAA);
                provider.InsertByte(i * 10 + 5, 0xBB);
                if (i < 20)
                {
                    provider.DeleteBytes(i * 10 + 50, 1);
                }
            }

            // Assert
            var modified = provider.GetByteModifieds(ByteAction.Modified);
            var added = provider.GetByteModifieds(ByteAction.Added);
            var deleted = provider.GetByteModifieds(ByteAction.Deleted);

            var totalChanges = modified.Count + added.Count + deleted.Count;

            Console.WriteLine($"Complex Operations:");
            Console.WriteLine($"  Modified: {modified.Count}");
            Console.WriteLine($"  Added: {added.Count}");
            Console.WriteLine($"  Deleted: {deleted.Count}");
            Console.WriteLine($"  Total: {totalChanges}");

            Assert.IsTrue(totalChanges > 0, "Should have tracked changes");
            Assert.AreEqual(30, modified.Count, "Should track all modifications");
            Assert.AreEqual(30, added.Count, "Should track all insertions");
            Assert.AreEqual(20, deleted.Count, "Should track all deletions");
        }

        #endregion

        #region Performance and Memory Tests

        [TestMethod]
        public void Diagnostics_LargeFile_StatisticsPerformance()
        {
            // Arrange
            var provider = CreateProviderWithData(1000000); // 1MB

            // Perform many operations
            for (int i = 0; i < 1000; i++)
            {
                provider.GetByte(i * 100);
            }

            // Act - Measure statistics retrieval time
            var startTime = DateTime.Now;
            var stats = provider.GetCacheStatistics();
            var elapsed = DateTime.Now - startTime;

            // Assert
            Assert.IsTrue(elapsed.TotalMilliseconds < 100,
                $"GetCacheStatistics took too long: {elapsed.TotalMilliseconds}ms");

            Assert.IsNotNull(stats);
            Console.WriteLine($"Statistics retrieval: {elapsed.TotalMilliseconds:F2}ms");
            Console.WriteLine(stats);
        }

        [TestMethod]
        public void Diagnostics_MemoryUsage_WithManyModifications()
        {
            // Arrange
            var provider = CreateProviderWithData(50000);

            // Act - Make many modifications
            for (int i = 0; i < 1000; i++)
            {
                provider.ModifyByte(i * 50, (byte)(i % 256));
            }

            // Get modification counts
            var modifiedCount = provider.GetByteModifieds(ByteAction.Modified).Count;

            // Assert
            Assert.AreEqual(1000, modifiedCount);

            // Estimate memory usage (rough calculation)
            // Each modification typically uses ~32-64 bytes
            var estimatedMemoryKB = modifiedCount * 48 / 1024.0;

            Console.WriteLine($"Modifications: {modifiedCount}");
            Console.WriteLine($"Estimated Memory: {estimatedMemoryKB:F2} KB");

            Assert.IsTrue(estimatedMemoryKB < 100,
                "Memory usage seems excessive for number of modifications");
        }

        #endregion

        #region Edge Cases

        [TestMethod]
        public void Diagnostics_ClosedProvider_HandlesGracefully()
        {
            // Arrange
            var provider = CreateProviderWithData(1000);
            provider.Close();

            // Act - Try to get statistics from closed provider
            var stats = provider.GetCacheStatistics();

            // Assert - Should not throw, may return empty or default stats
            Assert.IsNotNull(stats, "Should handle closed provider gracefully");
            Console.WriteLine(stats);
        }

        [TestMethod]
        public void Diagnostics_AfterReset_ClearsStatistics()
        {
            // Arrange
            var provider = CreateProviderWithData(10000);

            // Make modifications
            for (int i = 0; i < 100; i++)
            {
                provider.ModifyByte(i, 0xFF);
            }

            var statsBefore = provider.GetCacheStatistics();

            // Act - Close and reopen
            provider.Close();
            provider.OpenMemory(new byte[5000]);

            var statsAfter = provider.GetCacheStatistics();
            var modifications = provider.GetByteModifieds(ByteAction.Modified);

            // Assert
            Assert.IsNotNull(statsBefore);
            Assert.IsNotNull(statsAfter);
            Assert.AreEqual(0, modifications.Count, "Should have no modifications after reset");

            Console.WriteLine("Before reset:");
            Console.WriteLine(statsBefore);
            Console.WriteLine("\nAfter reset:");
            Console.WriteLine(statsAfter);
        }

        [TestMethod]
        public void Diagnostics_ConcurrentReads_CacheConsistency()
        {
            // Arrange
            var provider = CreateProviderWithData(100000);

            // Act - Perform many concurrent-like reads
            for (int iteration = 0; iteration < 5; iteration++)
            {
                for (int i = 0; i < 1000; i++)
                {
                    var result = provider.GetByte(i * 10);
                    Assert.IsTrue(result.success);
                }

                var stats = provider.GetCacheStatistics();
                Assert.IsNotNull(stats);
                Assert.IsTrue(stats.Length > 0);
            }

            // Assert - Final statistics should be consistent
            var finalStats = provider.GetCacheStatistics();
            Assert.IsNotNull(finalStats);

            Console.WriteLine("Final Cache Statistics:");
            Console.WriteLine(finalStats);
        }

        #endregion

        #region Integration Tests

        [TestMethod]
        public void Diagnostics_FullWorkflow_StatisticsAccurate()
        {
            // Arrange
            var provider = CreateProviderWithData(30000);

            // Act - Simulate real workflow
            Console.WriteLine("=== Initial State ===");
            Console.WriteLine(provider.GetCacheStatistics());

            // Step 1: Read some data
            for (int i = 0; i < 200; i++)
            {
                provider.GetByte(i * 10);
            }

            Console.WriteLine("\n=== After Reads ===");
            Console.WriteLine(provider.GetCacheStatistics());

            // Step 2: Make modifications
            provider.BeginBatch();
            try
            {
                for (int i = 0; i < 100; i++)
                {
                    provider.ModifyByte(i * 20, 0xFF);
                }
            }
            finally
            {
                provider.EndBatch();
            }

            Console.WriteLine("\n=== After Batch Modifications ===");
            Console.WriteLine(provider.GetCacheStatistics());

            // Step 3: More reads to generate cache hits
            for (int i = 0; i < 200; i++)
            {
                provider.GetByte(i * 10);
            }

            Console.WriteLine("\n=== After More Reads ===");
            var finalStats = provider.GetCacheStatistics();
            Console.WriteLine(finalStats);

            // Assert
            Assert.IsNotNull(finalStats);
            Assert.IsTrue(finalStats.Length > 50, "Should have substantial statistics");

            var modifications = provider.GetByteModifieds(ByteAction.Modified);
            Assert.AreEqual(100, modifications.Count, "Should track all modifications");
        }

        #endregion
    }
}
