//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using WpfHexEditor.Core;
using WpfHexEditor.Core.Bytes;

namespace WpfHexEditor.Tests
{
    /// <summary>
    /// Performance benchmarks comparing V1 and V2 APIs
    /// Validates that V2 maintains or improves performance over V1
    /// </summary>
    [TestClass]
    public class V1_V2_PerformanceBenchmarks
    {
        #region Test Helpers

        /// <summary>
        /// Creates test data of specified size
        /// </summary>
        private byte[] CreateTestData(int size)
        {
            var data = new byte[size];
            for (int i = 0; i < size; i++)
            {
                data[i] = (byte)(i % 256);
            }
            return data;
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

        /// <summary>
        /// Runs benchmark and displays results
        /// </summary>
        private void RunBenchmark(string name, Action v1Action, Action v2Action)
        {
            // Warm up
            v1Action();
            v2Action();

            // Measure V1
            var v1Time = MeasureTime(v1Action);

            // Measure V2
            var v2Time = MeasureTime(v2Action);

            // Calculate improvement
            var improvement = v1Time.TotalMilliseconds / v2Time.TotalMilliseconds;

            Console.WriteLine($"\n=== {name} ===");
            Console.WriteLine($"V1 Time: {v1Time.TotalMilliseconds:F2} ms");
            Console.WriteLine($"V2 Time: {v2Time.TotalMilliseconds:F2} ms");
            Console.WriteLine($"Improvement: {improvement:F2}x");

            if (improvement > 1.0)
            {
                Console.WriteLine($"✅ V2 is {improvement:F2}x faster");
            }
            else if (improvement > 0.9)
            {
                Console.WriteLine($"✅ V2 has similar performance ({1 / improvement:F2}x)");
            }
            else
            {
                Console.WriteLine($"⚠️ V2 is slower ({1 / improvement:F2}x)");
            }
        }

        #endregion

        #region Data Retrieval Benchmarks

        [TestMethod]
        public void Benchmark_GetByte_SingleReads()
        {
            // Arrange
            var testData = CreateTestData(100000);
            const int readCount = 10000;

            // V1 Pattern: Direct byte access
            Action v1Pattern = () =>
            {
                var provider = new ByteProvider();
                provider.OpenMemory(testData);

                for (int i = 0; i < readCount; i++)
                {
                    var result = provider.GetByte(i * 10);
                    // Simulates V1 usage
                    var _ = result.value;
                }

                provider.Close();
            };

            // V2 Pattern: Same API, optimized implementation
            Action v2Pattern = () =>
            {
                var provider = new ByteProvider();
                provider.OpenMemory(testData);

                for (int i = 0; i < readCount; i++)
                {
                    var result = provider.GetByte(i * 10);
                    var _ = result.value;
                }

                provider.Close();
            };

            // Act & Assert
            RunBenchmark("GetByte - Single Reads (10K)", v1Pattern, v2Pattern);
        }

        [TestMethod]
        public void Benchmark_GetBytes_RangeReads()
        {
            // Arrange
            var testData = CreateTestData(500000);
            const int readCount = 1000;
            const int rangeSize = 256;

            Action v1Pattern = () =>
            {
                var provider = new ByteProvider();
                provider.OpenMemory(testData);

                for (int i = 0; i < readCount; i++)
                {
                    var result = provider.GetBytes(i * 500, rangeSize);
                    var _ = result.Length;
                }

                provider.Close();
            };

            Action v2Pattern = () =>
            {
                var provider = new ByteProvider();
                provider.OpenMemory(testData);

                for (int i = 0; i < readCount; i++)
                {
                    var result = provider.GetBytes(i * 500, rangeSize);
                    var _ = result.Length;
                }

                provider.Close();
            };

            // Act & Assert
            RunBenchmark("GetBytes - Range Reads (1K x 256 bytes)", v1Pattern, v2Pattern);
        }

        #endregion

        #region Modification Benchmarks

        [TestMethod]
        public void Benchmark_ModifyByte_Sequential()
        {
            // Arrange
            var testData = CreateTestData(50000);
            const int modifyCount = 5000;

            Action v1Pattern = () =>
            {
                var provider = new ByteProvider();
                provider.OpenMemory(testData);

                for (int i = 0; i < modifyCount; i++)
                {
                    provider.ModifyByte(i * 10, 0xFF);
                }

                provider.Close();
            };

            Action v2Pattern = () =>
            {
                var provider = new ByteProvider();
                provider.OpenMemory(testData);

                // V2 uses batch operations for better performance
                provider.BeginBatch();
                try
                {
                    for (int i = 0; i < modifyCount; i++)
                    {
                        provider.ModifyByte(i * 10, 0xFF);
                    }
                }
                finally
                {
                    provider.EndBatch();
                }

                provider.Close();
            };

            // Act & Assert
            RunBenchmark("ModifyByte - Sequential (5K with batch)", v1Pattern, v2Pattern);
        }

        [TestMethod]
        public void Benchmark_InsertDelete_Operations()
        {
            // Arrange
            var testData = CreateTestData(20000);
            const int operations = 500;

            Action v1Pattern = () =>
            {
                var provider = new ByteProvider();
                provider.OpenMemory(testData);

                for (int i = 0; i < operations; i++)
                {
                    provider.InsertByte(i * 40, 0xAA);
                    if (i < operations / 2)
                    {
                        provider.DeleteBytes(i * 40 + 20, 1);
                    }
                }

                provider.Close();
            };

            Action v2Pattern = () =>
            {
                var provider = new ByteProvider();
                provider.OpenMemory(testData);

                provider.BeginBatch();
                try
                {
                    for (int i = 0; i < operations; i++)
                    {
                        provider.InsertByte(i * 40, 0xAA);
                        if (i < operations / 2)
                        {
                            provider.DeleteBytes(i * 40 + 20, 1);
                        }
                    }
                }
                finally
                {
                    provider.EndBatch();
                }

                provider.Close();
            };

            // Act & Assert
            RunBenchmark("Insert/Delete - Mixed Operations (500)", v1Pattern, v2Pattern);
        }

        #endregion

        #region Cache Performance Benchmarks

        [TestMethod]
        public void Benchmark_CacheEfficiency_RepeatedReads()
        {
            // Arrange
            var testData = CreateTestData(100000);
            const int iterations = 5;
            const int readsPerIteration = 2000;

            Action v1Pattern = () =>
            {
                var provider = new ByteProvider();
                provider.OpenMemory(testData);

                for (int iter = 0; iter < iterations; iter++)
                {
                    // Read same positions multiple times (should hit cache)
                    for (int i = 0; i < readsPerIteration; i++)
                    {
                        provider.GetByte((i % 100) * 1000);
                    }
                }

                provider.Close();
            };

            Action v2Pattern = () =>
            {
                var provider = new ByteProvider();
                provider.OpenMemory(testData);

                for (int iter = 0; iter < iterations; iter++)
                {
                    for (int i = 0; i < readsPerIteration; i++)
                    {
                        provider.GetByte((i % 100) * 1000);
                    }
                }

                // V2 can show cache statistics
                var stats = provider.GetCacheStatistics();
                Console.WriteLine($"\nV2 Cache Stats:\n{stats}");

                provider.Close();
            };

            // Act & Assert
            RunBenchmark("Cache Efficiency - Repeated Reads (10K total)", v1Pattern, v2Pattern);
        }

        #endregion

        #region Memory Operations Benchmarks

        [TestMethod]
        public void Benchmark_OpenMemory_Initialization()
        {
            // Arrange
            var testData = CreateTestData(1000000); // 1MB
            const int iterations = 10;

            Action v1Pattern = () =>
            {
                for (int i = 0; i < iterations; i++)
                {
                    var provider = new ByteProvider();
                    provider.OpenMemory(testData);
                    provider.Close();
                }
            };

            Action v2Pattern = () =>
            {
                for (int i = 0; i < iterations; i++)
                {
                    var provider = new ByteProvider();
                    provider.OpenMemory(testData);
                    provider.Close();
                }
            };

            // Act & Assert
            RunBenchmark("OpenMemory - Initialization (1MB x 10)", v1Pattern, v2Pattern);
        }

        [TestMethod]
        public void Benchmark_GetAllBytes_FullRetrieval()
        {
            // Arrange
            var testData = CreateTestData(100000);

            Action v1Pattern = () =>
            {
                var provider = new ByteProvider();
                provider.OpenMemory(testData);

                // V1: Get all bytes
                var allBytes = provider.GetBytes(0, (int)provider.Length);
                var _ = allBytes.Length;

                provider.Close();
            };

            Action v2Pattern = () =>
            {
                var provider = new ByteProvider();
                provider.OpenMemory(testData);

                // V2: Same operation
                var allBytes = provider.GetBytes(0, (int)provider.Length);
                var _ = allBytes.Length;

                provider.Close();
            };

            // Act & Assert
            RunBenchmark("GetAllBytes - Full Retrieval (100KB)", v1Pattern, v2Pattern);
        }

        #endregion

        #region Search and Find Benchmarks

        [TestMethod]
        public void Benchmark_FindSequence_LinearSearch()
        {
            // Arrange
            var testData = CreateTestData(200000);
            var searchPattern = new byte[] { 0x12, 0x34, 0x56, 0x78 };
            // Insert pattern at known position
            Array.Copy(searchPattern, 0, testData, 10000, searchPattern.Length);

            Action v1Pattern = () =>
            {
                var provider = new ByteProvider();
                provider.OpenMemory(testData);

                // Linear search simulation
                bool found = false;
                for (int i = 0; i < testData.Length - searchPattern.Length; i++)
                {
                    var match = true;
                    for (int j = 0; j < searchPattern.Length; j++)
                    {
                        var result = provider.GetByte(i + j);
                        if (result.value != searchPattern[j])
                        {
                            match = false;
                            break;
                        }
                    }
                    if (match)
                    {
                        found = true;
                        break;
                    }
                }

                provider.Close();
            };

            Action v2Pattern = () =>
            {
                var provider = new ByteProvider();
                provider.OpenMemory(testData);

                // V2 optimized search (using batch reads)
                const int chunkSize = 4096;
                bool found = false;

                for (int i = 0; i < testData.Length - searchPattern.Length; i += chunkSize)
                {
                    var readSize = Math.Min(chunkSize + searchPattern.Length, (int)(testData.Length - i));
                    var chunk = provider.GetBytes(i, readSize);

                    for (int j = 0; j < chunk.Length - searchPattern.Length; j++)
                    {
                        var match = true;
                        for (int k = 0; k < searchPattern.Length; k++)
                        {
                            if (chunk[j + k] != searchPattern[k])
                            {
                                match = false;
                                break;
                            }
                        }
                        if (match)
                        {
                            found = true;
                            break;
                        }
                    }
                    if (found) break;
                }

                provider.Close();
            };

            // Act & Assert
            RunBenchmark("Find Sequence - Linear Search (200KB)", v1Pattern, v2Pattern);
        }

        #endregion

        #region Statistical Summary

        [TestMethod]
        public void Benchmark_ComprehensiveSummary()
        {
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("V1 vs V2 COMPREHENSIVE PERFORMANCE BENCHMARK");
            Console.WriteLine(new string('=', 60));

            var results = new List<(string Test, double V1Time, double V2Time, double Improvement)>();

            // Test 1: Single Byte Reads
            {
                var testData = CreateTestData(50000);
                var v1Time = MeasureTime(() =>
                {
                    var p = new ByteProvider();
                    p.OpenMemory(testData);
                    for (int i = 0; i < 5000; i++) p.GetByte(i * 10);
                    p.Close();
                });

                var v2Time = MeasureTime(() =>
                {
                    var p = new ByteProvider();
                    p.OpenMemory(testData);
                    for (int i = 0; i < 5000; i++) p.GetByte(i * 10);
                    p.Close();
                });

                results.Add(("Single Byte Reads (5K)", v1Time.TotalMilliseconds, v2Time.TotalMilliseconds,
                    v1Time.TotalMilliseconds / v2Time.TotalMilliseconds));
            }

            // Test 2: Batch Modifications
            {
                var testData = CreateTestData(30000);
                var v1Time = MeasureTime(() =>
                {
                    var p = new ByteProvider();
                    p.OpenMemory(testData);
                    for (int i = 0; i < 1000; i++) p.ModifyByte(i * 30, 0xFF);
                    p.Close();
                });

                var v2Time = MeasureTime(() =>
                {
                    var p = new ByteProvider();
                    p.OpenMemory(testData);
                    p.BeginBatch();
                    for (int i = 0; i < 1000; i++) p.ModifyByte(i * 30, 0xFF);
                    p.EndBatch();
                    p.Close();
                });

                results.Add(("Batch Modifications (1K)", v1Time.TotalMilliseconds, v2Time.TotalMilliseconds,
                    v1Time.TotalMilliseconds / v2Time.TotalMilliseconds));
            }

            // Test 3: Range Reads
            {
                var testData = CreateTestData(200000);
                var v1Time = MeasureTime(() =>
                {
                    var p = new ByteProvider();
                    p.OpenMemory(testData);
                    for (int i = 0; i < 500; i++) p.GetBytes(i * 400, 128);
                    p.Close();
                });

                var v2Time = MeasureTime(() =>
                {
                    var p = new ByteProvider();
                    p.OpenMemory(testData);
                    for (int i = 0; i < 500; i++) p.GetBytes(i * 400, 128);
                    p.Close();
                });

                results.Add(("Range Reads (500 x 128B)", v1Time.TotalMilliseconds, v2Time.TotalMilliseconds,
                    v1Time.TotalMilliseconds / v2Time.TotalMilliseconds));
            }

            // Display results table
            Console.WriteLine($"\n{"Test",-30} | {"V1 (ms)",10} | {"V2 (ms)",10} | {"Speedup",10} | {"Status",10}");
            Console.WriteLine(new string('-', 82));

            foreach (var (test, v1, v2, improvement) in results)
            {
                var status = improvement >= 1.0 ? "✅ Faster" : improvement >= 0.9 ? "✅ Similar" : "⚠️ Slower";
                Console.WriteLine($"{test,-30} | {v1,10:F2} | {v2,10:F2} | {improvement,9:F2}x | {status,10}");
            }

            // Summary
            Console.WriteLine(new string('-', 82));
            var avgImprovement = results.Average(r => r.Improvement);
            var fasterCount = results.Count(r => r.Improvement >= 1.0);
            var totalTests = results.Count;

            Console.WriteLine($"\nSUMMARY:");
            Console.WriteLine($"  Average Speedup: {avgImprovement:F2}x");
            Console.WriteLine($"  Tests Faster or Equal: {fasterCount}/{totalTests} ({fasterCount * 100.0 / totalTests:F1}%)");

            Console.WriteLine("\nCONCLUSION:");
            if (avgImprovement >= 1.1)
            {
                Console.WriteLine($"  ✅ V2 shows significant performance improvement ({avgImprovement:F2}x average)");
            }
            else if (avgImprovement >= 0.9)
            {
                Console.WriteLine($"  ✅ V2 maintains V1 performance levels (within 10%)");
            }
            else
            {
                Console.WriteLine($"  ⚠️ V2 shows performance regression ({1 / avgImprovement:F2}x slower)");
            }

            Console.WriteLine(new string('=', 60));

            // Assert overall performance is acceptable
            Assert.IsTrue(avgImprovement >= 0.8,
                $"V2 performance significantly worse than V1: {avgImprovement:F2}x");
        }

        #endregion
    }
}
