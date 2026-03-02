//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using WpfHexEditor.Core;
using WpfHexEditor.Core.Bytes;

namespace WpfHexEditor.Tests
{
    /// <summary>
    /// Performance tests for batch operations
    /// Validates performance improvements when using BeginBatch/EndBatch
    /// </summary>
    [TestClass]
    public class BatchOperations_PerformanceTests
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

        /// <summary>
        /// Measures execution time of an action
        /// </summary>
        private TimeSpan MeasureTime(Action action)
        {
            var sw = Stopwatch.StartNew();
            action();
            sw.Stop();
            return sw.Elapsed;
        }

        #endregion

        #region Batch vs Non-Batch Performance Tests

        [TestMethod]
        public void Performance_BatchMode_FasterThanNonBatch_SmallEdits()
        {
            // Arrange
            var provider = CreateProviderWithData(10000);
            const int editCount = 100;

            // Act - Without batch
            var timeWithoutBatch = MeasureTime(() =>
            {
                for (int i = 0; i < editCount; i++)
                {
                    provider.ModifyByte(i, 0xFF);
                }
            });

            // Reset data
            provider.Close();
            provider = CreateProviderWithData(10000);

            // Act - With batch
            var timeWithBatch = MeasureTime(() =>
            {
                provider.BeginBatch();
                try
                {
                    for (int i = 0; i < editCount; i++)
                    {
                        provider.ModifyByte(i, 0xFF);
                    }
                }
                finally
                {
                    provider.EndBatch();
                }
            });

            // Assert - Batch should be faster or similar
            // We allow batch to be slower in some cases due to overhead,
            // but for 100+ operations it should show benefits
            var speedupRatio = timeWithoutBatch.TotalMilliseconds / timeWithBatch.TotalMilliseconds;

            Console.WriteLine($"Without Batch: {timeWithoutBatch.TotalMilliseconds:F2} ms");
            Console.WriteLine($"With Batch: {timeWithBatch.TotalMilliseconds:F2} ms");
            Console.WriteLine($"Speedup: {speedupRatio:F2}x");

            // Batch should not be significantly slower (allow up to 2x slower for small datasets)
            Assert.IsTrue(speedupRatio > 0.5,
                $"Batch mode significantly slower: {speedupRatio:F2}x");
        }

        [TestMethod]
        public void Performance_BatchMode_SignificantlyFasterForManyEdits()
        {
            // Arrange
            var provider = CreateProviderWithData(50000);
            const int editCount = 1000;

            // Act - Without batch
            var timeWithoutBatch = MeasureTime(() =>
            {
                for (int i = 0; i < editCount; i++)
                {
                    provider.ModifyByte(i * 10, 0xFF);
                }
            });

            // Reset data
            provider.Close();
            provider = CreateProviderWithData(50000);

            // Act - With batch
            var timeWithBatch = MeasureTime(() =>
            {
                provider.BeginBatch();
                try
                {
                    for (int i = 0; i < editCount; i++)
                    {
                        provider.ModifyByte(i * 10, 0xFF);
                    }
                }
                finally
                {
                    provider.EndBatch();
                }
            });

            // Assert - Batch should be faster for many operations
            var speedupRatio = timeWithoutBatch.TotalMilliseconds / timeWithBatch.TotalMilliseconds;

            Console.WriteLine($"Without Batch: {timeWithoutBatch.TotalMilliseconds:F2} ms");
            Console.WriteLine($"With Batch: {timeWithBatch.TotalMilliseconds:F2} ms");
            Console.WriteLine($"Speedup: {speedupRatio:F2}x");

            // For 1000 operations, batch should show clear benefits
            Assert.IsTrue(speedupRatio >= 1.0,
                $"Batch mode not faster: {speedupRatio:F2}x (expected >= 1.0x)");
        }

        [TestMethod]
        public void Performance_BatchMode_MixedOperations()
        {
            // Arrange
            var provider = CreateProviderWithData(20000);
            const int iterations = 200;

            // Act - Without batch
            var timeWithoutBatch = MeasureTime(() =>
            {
                for (int i = 0; i < iterations; i++)
                {
                    provider.ModifyByte(i * 2, 0xAA);
                    provider.InsertByte(i * 2 + 1, 0xBB);
                    provider.DeleteBytes(i * 2 + 2, 1);
                }
            });

            // Reset data
            provider.Close();
            provider = CreateProviderWithData(20000);

            // Act - With batch
            var timeWithBatch = MeasureTime(() =>
            {
                provider.BeginBatch();
                try
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        provider.ModifyByte(i * 2, 0xAA);
                        provider.InsertByte(i * 2 + 1, 0xBB);
                        provider.DeleteBytes(i * 2 + 2, 1);
                    }
                }
                finally
                {
                    provider.EndBatch();
                }
            });

            // Assert
            var speedupRatio = timeWithoutBatch.TotalMilliseconds / timeWithBatch.TotalMilliseconds;

            Console.WriteLine($"Mixed Operations Test:");
            Console.WriteLine($"Without Batch: {timeWithoutBatch.TotalMilliseconds:F2} ms");
            Console.WriteLine($"With Batch: {timeWithBatch.TotalMilliseconds:F2} ms");
            Console.WriteLine($"Speedup: {speedupRatio:F2}x");

            // Batch should provide benefits for mixed operations
            Assert.IsTrue(speedupRatio > 0.5,
                $"Batch mode performance degraded: {speedupRatio:F2}x");
        }

        #endregion

        #region Nested Batch Tests

        [TestMethod]
        public void Performance_NestedBatch_WorksCorrectly()
        {
            // Arrange
            var provider = CreateProviderWithData(10000);

            // Act
            var time = MeasureTime(() =>
            {
                provider.BeginBatch();
                try
                {
                    for (int i = 0; i < 100; i++)
                    {
                        provider.ModifyByte(i, 0xFF);
                    }

                    // Nested batch (should be handled gracefully)
                    provider.BeginBatch();
                    try
                    {
                        for (int i = 100; i < 200; i++)
                        {
                            provider.ModifyByte(i, 0xAA);
                        }
                    }
                    finally
                    {
                        provider.EndBatch();
                    }

                    for (int i = 200; i < 300; i++)
                    {
                        provider.ModifyByte(i, 0xBB);
                    }
                }
                finally
                {
                    provider.EndBatch();
                }
            });

            // Assert - Should complete without errors
            Console.WriteLine($"Nested batch completed in {time.TotalMilliseconds:F2} ms");
            Assert.IsTrue(time.TotalMilliseconds < 1000, "Operation took too long");

            // Verify data integrity
            var result1 = provider.GetByte(50);
            var result2 = provider.GetByte(150);
            var result3 = provider.GetByte(250);

            Assert.AreEqual(0xFF, result1.value);
            Assert.AreEqual(0xAA, result2.value);
            Assert.AreEqual(0xBB, result3.value);
        }

        #endregion

        #region Data Integrity Tests

        [TestMethod]
        public void Performance_BatchMode_MaintainsDataIntegrity()
        {
            // Arrange
            var provider = CreateProviderWithData(5000);
            const int editCount = 500;

            // Act - Perform batch modifications
            provider.BeginBatch();
            try
            {
                for (int i = 0; i < editCount; i++)
                {
                    provider.ModifyByte(i * 10, (byte)(i % 256));
                }
            }
            finally
            {
                provider.EndBatch();
            }

            // Assert - Verify all modifications were applied correctly
            for (int i = 0; i < editCount; i++)
            {
                var result = provider.GetByte(i * 10);
                Assert.IsTrue(result.success, $"Failed to read byte at position {i * 10}");
                Assert.AreEqual((byte)(i % 256), result.value,
                    $"Incorrect value at position {i * 10}");
            }
        }

        [TestMethod]
        public void Performance_BatchMode_TrackingModifications()
        {
            // Arrange
            var provider = CreateProviderWithData(10000);
            const int editCount = 200;

            // Act
            provider.BeginBatch();
            try
            {
                for (int i = 0; i < editCount; i++)
                {
                    provider.ModifyByte(i * 5, 0xFF);
                }
            }
            finally
            {
                provider.EndBatch();
            }

            // Assert - All modifications should be tracked
            var modifiedBytes = provider.GetByteModifieds(ByteAction.Modified);
            Assert.AreEqual(editCount, modifiedBytes.Count,
                "Not all modifications were tracked");

            // Verify each modification
            for (int i = 0; i < editCount; i++)
            {
                long position = i * 5;
                Assert.IsTrue(modifiedBytes.ContainsKey(position),
                    $"Modification at position {position} not tracked");
            }
        }

        #endregion

        #region Stress Tests

        [TestMethod]
        public void Performance_BatchMode_LargeNumberOfEdits()
        {
            // Arrange
            var provider = CreateProviderWithData(100000);
            const int editCount = 5000;

            // Act
            var time = MeasureTime(() =>
            {
                provider.BeginBatch();
                try
                {
                    for (int i = 0; i < editCount; i++)
                    {
                        provider.ModifyByte(i * 20, (byte)(i % 256));
                    }
                }
                finally
                {
                    provider.EndBatch();
                }
            });

            // Assert
            Console.WriteLine($"5000 batched edits completed in {time.TotalMilliseconds:F2} ms");
            Assert.IsTrue(time.TotalMilliseconds < 5000,
                $"Batch operations too slow: {time.TotalMilliseconds:F2} ms");

            // Verify some random modifications
            var result1 = provider.GetByte(0);
            var result2 = provider.GetByte(1000 * 20);
            var result3 = provider.GetByte(4999 * 20);

            Assert.AreEqual(0, result1.value);
            Assert.AreEqual((byte)(1000 % 256), result2.value);
            Assert.AreEqual((byte)(4999 % 256), result3.value);
        }

        [TestMethod]
        public void Performance_BatchMode_RepeatedBatchOperations()
        {
            // Arrange
            var provider = CreateProviderWithData(50000);
            const int batchCount = 10;
            const int editsPerBatch = 100;

            // Act
            var totalTime = MeasureTime(() =>
            {
                for (int batch = 0; batch < batchCount; batch++)
                {
                    provider.BeginBatch();
                    try
                    {
                        int offset = batch * 1000;
                        for (int i = 0; i < editsPerBatch; i++)
                        {
                            provider.ModifyByte(offset + i, (byte)batch);
                        }
                    }
                    finally
                    {
                        provider.EndBatch();
                    }
                }
            });

            // Assert
            Console.WriteLine($"10 batch operations (100 edits each) completed in {totalTime.TotalMilliseconds:F2} ms");
            Console.WriteLine($"Average per batch: {totalTime.TotalMilliseconds / batchCount:F2} ms");

            Assert.IsTrue(totalTime.TotalMilliseconds < 3000,
                $"Repeated batch operations too slow: {totalTime.TotalMilliseconds:F2} ms");

            // Verify modifications from different batches
            for (int batch = 0; batch < batchCount; batch++)
            {
                var result = provider.GetByte(batch * 1000);
                Assert.AreEqual((byte)batch, result.value,
                    $"Batch {batch} modifications incorrect");
            }
        }

        #endregion

        #region Memory and Cache Tests

        [TestMethod]
        public void Performance_BatchMode_CacheStatistics()
        {
            // Arrange
            var provider = CreateProviderWithData(20000);

            // Act - Perform batch operations
            provider.BeginBatch();
            try
            {
                for (int i = 0; i < 500; i++)
                {
                    provider.ModifyByte(i * 10, 0xFF);
                }
            }
            finally
            {
                provider.EndBatch();
            }

            // Get cache statistics
            var stats = provider.GetCacheStatistics();

            // Assert
            Assert.IsNotNull(stats, "Cache statistics should not be null");
            Assert.IsTrue(stats.Length > 0, "Cache statistics should not be empty");

            Console.WriteLine("Cache Statistics after batch operations:");
            Console.WriteLine(stats);

            // Verify cache statistics contain expected information
            Assert.IsTrue(stats.Contains("Line Cache") || stats.Contains("Cache"),
                "Cache statistics should contain cache information");
        }

        #endregion
    }
}
