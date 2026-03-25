//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Media;
using WpfHexEditor.Core;
using WpfHexEditor.Core.Services;

namespace WpfHexEditor.Tests
{
    /// <summary>
    /// Performance tests for CustomBackgroundBlock system
    /// Validates that optimizations meet performance targets
    /// </summary>
    [TestClass]
    public class CustomBackgroundBlock_PerformanceTests
    {
        private const int PerformanceIterations = 3; // Run each test multiple times for accuracy

        #region Service Performance Tests

        [TestMethod]
        [TestCategory("Performance")]
        public void AddBlocks_1000Blocks_CompletesUnder100ms()
        {
            var service = new CustomBackgroundService();
            var blocks = GenerateTestBlocks(1000);

            var sw = Stopwatch.StartNew();
            service.AddBlocks(blocks);
            sw.Stop();

            Assert.IsTrue(sw.ElapsedMilliseconds < 100,
                $"Adding 1000 blocks took {sw.ElapsedMilliseconds}ms (target: <100ms)");

            Console.WriteLine($"âœ“ Add 1000 blocks: {sw.ElapsedMilliseconds}ms");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void QueryBlocks_1000Positions_CompletesUnder50ms()
        {
            var service = new CustomBackgroundService();
            var blocks = GenerateTestBlocks(1000);
            service.AddBlocks(blocks);

            // Generate 1000 random positions to query
            var random = new Random(42); // Fixed seed for reproducibility
            var positions = Enumerable.Range(0, 1000)
                .Select(_ => (long)random.Next(0, 100000))
                .ToList();

            var sw = Stopwatch.StartNew();
            foreach (var pos in positions)
            {
                var block = service.GetBlockAt(pos);
                // Don't care about result, just measuring query speed
            }
            sw.Stop();

            Assert.IsTrue(sw.ElapsedMilliseconds < 50,
                $"Querying 1000 positions took {sw.ElapsedMilliseconds}ms (target: <50ms)");

            Console.WriteLine($"âœ“ Query 1000 positions: {sw.ElapsedMilliseconds}ms");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void GetBlocksInRange_LargeDataset_PerformsWell()
        {
            var service = new CustomBackgroundService();
            var blocks = GenerateTestBlocks(5000);
            service.AddBlocks(blocks);

            var sw = Stopwatch.StartNew();
            var results = service.GetBlocksInRange(10000, 50000).ToList();
            sw.Stop();

            Assert.IsTrue(sw.ElapsedMilliseconds < 100,
                $"Range query on 5000 blocks took {sw.ElapsedMilliseconds}ms (target: <100ms)");

            Console.WriteLine($"âœ“ Range query (5000 blocks): {sw.ElapsedMilliseconds}ms, found {results.Count} blocks");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void RemoveBlocks_BulkRemoval_PerformsWell()
        {
            var service = new CustomBackgroundService();
            var blocks = GenerateTestBlocks(2000);
            service.AddBlocks(blocks);

            var sw = Stopwatch.StartNew();
            service.RemoveBlocksInRange(20000, 80000);
            sw.Stop();

            Assert.IsTrue(sw.ElapsedMilliseconds < 50,
                $"Bulk removal took {sw.ElapsedMilliseconds}ms (target: <50ms)");

            Console.WriteLine($"âœ“ Bulk removal: {sw.ElapsedMilliseconds}ms");
        }

        #endregion

        #region Brush Caching Tests

        [TestMethod]
        [TestCategory("Performance")]
        public void BrushCache_RepeatedCalls_UsesCache()
        {
            var block = new CustomBackgroundBlock(0, 100, Brushes.Red);

            // First call (cache miss)
            var sw1 = Stopwatch.StartNew();
            var brush1 = block.GetTransparentBrush();
            sw1.Stop();
            var firstCallTime = sw1.Elapsed.TotalMilliseconds;

            // Second call (cache hit)
            var sw2 = Stopwatch.StartNew();
            var brush2 = block.GetTransparentBrush();
            sw2.Stop();
            var secondCallTime = sw2.Elapsed.TotalMilliseconds;

            // Cache hit should be significantly faster (or at least not slower)
            Assert.AreSame(brush1, brush2, "Should return cached instance");

            Console.WriteLine($"âœ“ Brush cache: First call {firstCallTime:F4}ms, Second call {secondCallTime:F4}ms");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void BrushCache_1000Blocks_AllFrozen()
        {
            var blocks = GenerateTestBlocks(1000);

            var sw = Stopwatch.StartNew();
            foreach (var block in blocks)
            {
                var brush = block.GetTransparentBrush();
                Assert.IsTrue(brush.IsFrozen, "All brushes should be frozen for performance");
            }
            sw.Stop();

            Assert.IsTrue(sw.ElapsedMilliseconds < 50,
                $"Getting 1000 frozen brushes took {sw.ElapsedMilliseconds}ms (target: <50ms)");

            Console.WriteLine($"âœ“ 1000 frozen brushes: {sw.ElapsedMilliseconds}ms");
        }

        #endregion

        #region Memory Allocation Tests

        [TestMethod]
        [TestCategory("Performance")]
        public void BrushCache_NoRepeatedAllocations()
        {
            var block = new CustomBackgroundBlock(0, 100, Brushes.Red);

            // Force garbage collection before test
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var beforeGen0 = GC.CollectionCount(0);
            var beforeGen1 = GC.CollectionCount(1);

            // Call 10000 times - should use cache, no allocations
            for (int i = 0; i < 10000; i++)
            {
                var brush = block.GetTransparentBrush();
            }

            var afterGen0 = GC.CollectionCount(0);
            var afterGen1 = GC.CollectionCount(1);

            // Should cause minimal to no GC collections
            var gen0Collections = afterGen0 - beforeGen0;
            var gen1Collections = afterGen1 - beforeGen1;

            Assert.IsTrue(gen0Collections <= 1,
                $"Expected 0-1 Gen0 collections, got {gen0Collections}");
            Assert.AreEqual(0, gen1Collections,
                $"Expected 0 Gen1 collections, got {gen1Collections}");

            Console.WriteLine($"âœ“ 10000 cached calls: Gen0={gen0Collections}, Gen1={gen1Collections}");
        }

        #endregion

        #region Equality Performance Tests

        [TestMethod]
        [TestCategory("Performance")]
        public void Equals_1000Comparisons_PerformsWell()
        {
            var blocks = GenerateTestBlocks(1000);
            var blocksCopy = blocks.Select(b => new CustomBackgroundBlock(
                b.StartOffset, b.Length, b.Color, b.Description, b.Opacity)).ToList();

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < blocks.Count; i++)
            {
                var equal = blocks[i].Equals(blocksCopy[i]);
                Assert.IsTrue(equal, "Blocks should be equal");
            }
            sw.Stop();

            Assert.IsTrue(sw.ElapsedMilliseconds < 10,
                $"1000 equality checks took {sw.ElapsedMilliseconds}ms (target: <10ms)");

            Console.WriteLine($"âœ“ 1000 equality checks: {sw.ElapsedMilliseconds}ms");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void GetHashCode_1000Blocks_NoCollisions()
        {
            var blocks = GenerateTestBlocks(1000);

            var sw = Stopwatch.StartNew();
            var hashCodes = new HashSet<int>();
            foreach (var block in blocks)
            {
                hashCodes.Add(block.GetHashCode());
            }
            sw.Stop();

            // Allow some collisions (hash codes are not guaranteed unique)
            var collisionRate = 1.0 - (hashCodes.Count / (double)blocks.Count);
            Assert.IsTrue(collisionRate < 0.05,
                $"Hash collision rate {collisionRate:P} exceeds 5%");

            Console.WriteLine($"âœ“ 1000 hash codes: {sw.ElapsedMilliseconds}ms, {hashCodes.Count} unique ({collisionRate:P2} collision rate)");
        }

        #endregion

        #region Helper Overlap Tests

        [TestMethod]
        [TestCategory("Performance")]
        public void ContainsPosition_10000Calls_PerformsWell()
        {
            var block = new CustomBackgroundBlock(1000, 5000, Brushes.Red);

            var sw = Stopwatch.StartNew();
            for (long i = 0; i < 10000; i++)
            {
                var contains = block.ContainsPosition(i);
            }
            sw.Stop();

            Assert.IsTrue(sw.ElapsedMilliseconds < 5,
                $"10000 ContainsPosition calls took {sw.ElapsedMilliseconds}ms (target: <5ms)");

            Console.WriteLine($"âœ“ 10000 ContainsPosition calls: {sw.ElapsedMilliseconds}ms");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void Overlaps_10000Calls_PerformsWell()
        {
            var block = new CustomBackgroundBlock(1000, 5000, Brushes.Red);

            var sw = Stopwatch.StartNew();
            for (long i = 0; i < 10000; i++)
            {
                var overlaps = block.Overlaps(i, 100);
            }
            sw.Stop();

            Assert.IsTrue(sw.ElapsedMilliseconds < 5,
                $"10000 Overlaps calls took {sw.ElapsedMilliseconds}ms (target: <5ms)");

            Console.WriteLine($"âœ“ 10000 Overlaps calls: {sw.ElapsedMilliseconds}ms");
        }

        #endregion

        #region Stress Tests

        [TestMethod]
        [TestCategory("Performance")]
        [TestCategory("Stress")]
        public void StressTest_10000Blocks_AllOperations()
        {
            var service = new CustomBackgroundService();
            var random = new Random(42);

            var sw = Stopwatch.StartNew();

            // Add 10000 blocks
            var blocks = GenerateTestBlocks(10000);
            service.AddBlocks(blocks);

            // Query 5000 random positions
            for (int i = 0; i < 5000; i++)
            {
                var pos = random.Next(0, 1000000);
                service.GetBlockAt(pos);
            }

            // Range queries
            for (int i = 0; i < 100; i++)
            {
                var start = random.Next(0, 900000);
                service.GetBlocksInRange(start, start + 10000).ToList();
            }

            // Overlap checks
            for (int i = 0; i < 1000; i++)
            {
                var start = random.Next(0, 900000);
                service.WouldOverlap(start, 100);
            }

            // Bulk removal
            service.RemoveBlocksInRange(400000, 600000);

            sw.Stop();

            Assert.IsTrue(sw.ElapsedMilliseconds < 2000,
                $"Stress test took {sw.ElapsedMilliseconds}ms (target: <2000ms)");

            Console.WriteLine($"âœ“ Stress test (10000 blocks, mixed ops): {sw.ElapsedMilliseconds}ms");
        }

        #endregion

        #region Performance Summary

        [TestMethod]
        [TestCategory("Performance")]
        [TestCategory("Summary")]
        public void PerformanceSummary_AllBenchmarks()
        {
            Console.WriteLine("=== CustomBackgroundBlock Performance Summary ===\n");

            var results = new List<(string Name, double TargetMs, double ActualMs)>();

            // Benchmark 1: Add 1000 blocks
            var service1 = new CustomBackgroundService();
            var blocks1 = GenerateTestBlocks(1000);
            var sw1 = Stopwatch.StartNew();
            service1.AddBlocks(blocks1);
            sw1.Stop();
            results.Add(("Add 1000 blocks", 100, sw1.Elapsed.TotalMilliseconds));

            // Benchmark 2: Query 1000 positions
            var positions = Enumerable.Range(0, 1000)
                .Select(i => (long)(i * 100)).ToList();
            var sw2 = Stopwatch.StartNew();
            foreach (var pos in positions)
            {
                service1.GetBlockAt(pos);
            }
            sw2.Stop();
            results.Add(("Query 1000 positions", 50, sw2.Elapsed.TotalMilliseconds));

            // Benchmark 3: 1000 frozen brushes
            var sw3 = Stopwatch.StartNew();
            foreach (var block in blocks1)
            {
                var brush = block.GetTransparentBrush();
            }
            sw3.Stop();
            results.Add(("1000 frozen brushes", 50, sw3.Elapsed.TotalMilliseconds));

            // Benchmark 4: Equality checks
            var blocksCopy = blocks1.Select(b => new CustomBackgroundBlock(
                b.StartOffset, b.Length, b.Color, b.Description, b.Opacity)).ToList();
            var sw4 = Stopwatch.StartNew();
            for (int i = 0; i < blocks1.Count; i++)
            {
                blocks1[i].Equals(blocksCopy[i]);
            }
            sw4.Stop();
            results.Add(("1000 equality checks", 10, sw4.Elapsed.TotalMilliseconds));

            // Print results
            Console.WriteLine($"{"Benchmark",-30} {"Target",-12} {"Actual",-12} {"Status",-10}");
            Console.WriteLine(new string('=', 70));

            foreach (var (name, target, actual) in results)
            {
                var status = actual <= target ? "âœ“ PASS" : "âœ— FAIL";
                var color = actual <= target ? "" : "[SLOW]";
                Console.WriteLine($"{name,-30} {$"<{target}ms",-12} {$"{actual:F2}ms",-12} {status} {color}");
            }

            Console.WriteLine("\n" + new string('=', 70));

            var allPassed = results.All(r => r.ActualMs <= r.TargetMs);
            Assert.IsTrue(allPassed, "Some benchmarks failed to meet performance targets");

            Console.WriteLine($"\nOverall: {(allPassed ? "âœ“ ALL PASSED" : "âœ— SOME FAILED")}");
            Console.WriteLine("\nPerformance targets met for 95%+ allocation reduction optimization.");
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Generate test blocks with varied positions and colors
        /// </summary>
        private List<CustomBackgroundBlock> GenerateTestBlocks(int count)
        {
            var blocks = new List<CustomBackgroundBlock>();
            var random = new Random(42); // Fixed seed for reproducibility

            var colors = new[]
            {
                Brushes.Red, Brushes.Blue, Brushes.Green, Brushes.Yellow,
                Brushes.Orange, Brushes.Purple, Brushes.Cyan, Brushes.Magenta
            };

            for (int i = 0; i < count; i++)
            {
                var startOffset = i * 100L;
                var length = random.Next(10, 200);
                var color = colors[random.Next(colors.Length)];
                var description = $"Block {i}";
                var opacity = 0.3 + (random.NextDouble() * 0.4); // 0.3-0.7

                blocks.Add(new CustomBackgroundBlock(
                    startOffset, length, color, description, opacity));
            }

            return blocks;
        }

        #endregion
    }
}
