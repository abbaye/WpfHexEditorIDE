//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace WpfHexaEditor.Core.Bytes
{
    /// <summary>
    /// Test suite for ByteProvider V2 - validates architecture and performance.
    /// Run this to verify V2 improvements over ByteProviderLegacy.
    /// </summary>
    public static class ByteProviderV2Test
    {
        /// <summary>
        /// Run all tests and output results to console.
        /// </summary>
        public static void RunAllTests()
        {
            Console.WriteLine("========================================");
            Console.WriteLine("ByteProvider V2 Test Suite");
            Console.WriteLine("========================================\n");

            try
            {
                Console.WriteLine("Starting Test 1...");
                TestBasicOperations();

                Console.WriteLine("Starting Test 2...");
                TestMultipleInsertions();

                Console.WriteLine("Starting Test 3...");
                TestVirtualPhysicalMapping();

                Console.WriteLine("Starting Test 4...");
                TestCachingPerformance();

                Console.WriteLine("Starting Test 5...");
                TestMemorySource();

                Console.WriteLine("Starting Test 6...");
                TestOptimizedPositionMapper();

                Console.WriteLine("Starting Test 7...");
                TestBatchOperations();

                Console.WriteLine("Starting Test 8...");
                TestSearchAlgorithms();

                Console.WriteLine("Starting Test 9...");
                TestComparisonService();

                Console.WriteLine("\n========================================");
                Console.WriteLine("All tests completed!");
                Console.WriteLine("========================================");
            }
            catch (Exception ex)
            {
                Console.WriteLine("\n!!! TEST FAILED !!!");
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Test 1: Basic read/write operations
        /// </summary>
        private static void TestBasicOperations()
        {
            Console.WriteLine("Test 1: Basic Operations");
            Console.WriteLine("------------------------");

            var provider = new ByteProvider();
            var testData = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 };
            provider.OpenMemory(testData);

            // Test read
            var (value, success) = provider.GetByte(3);
            Console.WriteLine($"  Read byte at position 3: 0x{value:X2} (expected: 0x03) - {(value == 0x03 ? "✓ PASS" : "✗ FAIL")}");

            // Test modify
            provider.ModifyByte(3, 0xFF);
            (value, success) = provider.GetByte(3);
            Console.WriteLine($"  Modified byte at position 3: 0x{value:X2} (expected: 0xFF) - {(value == 0xFF ? "✓ PASS" : "✗ FAIL")}");

            // Test insert
            provider.InsertByte(4, 0xAA);
            var newLength = provider.VirtualLength;
            Console.WriteLine($"  After insert: VirtualLength = {newLength} (expected: 9) - {(newLength == 9 ? "✓ PASS" : "✗ FAIL")}");

            // Test delete
            provider.DeleteByte(0);
            newLength = provider.VirtualLength;
            Console.WriteLine($"  After delete: VirtualLength = {newLength} (expected: 8) - {(newLength == 8 ? "✓ PASS" : "✗ FAIL")}");

            provider.Dispose();
            Console.WriteLine();
        }

        /// <summary>
        /// Test 2: Multiple insertions at same physical position (V1 bug fix)
        /// </summary>
        private static void TestMultipleInsertions()
        {
            Console.WriteLine("Test 2: Multiple Insertions (V1 Bug Fix)");
            Console.WriteLine("-----------------------------------------");

            var provider = new ByteProvider();
            var testData = new byte[] { 0x00, 0x01, 0x02 };
            provider.OpenMemory(testData);

            // Insert multiple bytes at same position (this failed in V1)
            provider.InsertByte(1, 0xAA);
            provider.InsertByte(1, 0xBB);
            provider.InsertByte(1, 0xCC);

            // Virtual length should be 3 + 3 = 6
            var length = provider.VirtualLength;
            Console.WriteLine($"  VirtualLength after 3 insertions: {length} (expected: 6) - {(length == 6 ? "✓ PASS" : "✗ FAIL")}");

            // Verify byte order: 0x00, 0xCC, 0xBB, 0xAA, 0x01, 0x02
            var bytes = provider.GetBytes(0, (int)provider.VirtualLength);
            var expected = new byte[] { 0x00, 0xCC, 0xBB, 0xAA, 0x01, 0x02 };
            bool orderCorrect = bytes.SequenceEqual(expected);
            Console.WriteLine($"  Byte order: {string.Join(", ", bytes.Select(b => $"0x{b:X2}"))}");
            Console.WriteLine($"  Expected:   {string.Join(", ", expected.Select(b => $"0x{b:X2}"))}");
            Console.WriteLine($"  Order correct: {(orderCorrect ? "✓ PASS" : "✗ FAIL")}");

            provider.Dispose();
            Console.WriteLine();
        }

        /// <summary>
        /// Test 3: Virtual ↔ Physical position mapping
        /// </summary>
        private static void TestVirtualPhysicalMapping()
        {
            Console.WriteLine("Test 3: Virtual ↔ Physical Mapping");
            Console.WriteLine("-----------------------------------");

            var provider = new ByteProvider();
            var testData = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04 };
            provider.OpenMemory(testData);

            // Insert at position 2
            provider.InsertByte(2, 0xAA);

            // Virtual position 2 should map to inserted byte (no physical position)
            // Virtual position 3 should map to physical position 2
            var (value2, _) = provider.GetByte(2);
            var (value3, _) = provider.GetByte(3);

            Console.WriteLine($"  Virtual[2] = 0x{value2:X2} (inserted, expected: 0xAA) - {(value2 == 0xAA ? "✓ PASS" : "✗ FAIL")}");
            Console.WriteLine($"  Virtual[3] = 0x{value3:X2} (physical[2], expected: 0x02) - {(value3 == 0x02 ? "✓ PASS" : "✗ FAIL")}");

            // Delete position 0
            provider.DeleteByte(0);

            // Virtual position 0 should now map to physical position 1
            var (value0, _) = provider.GetByte(0);
            Console.WriteLine($"  After delete, Virtual[0] = 0x{value0:X2} (physical[1], expected: 0x01) - {(value0 == 0x01 ? "✓ PASS" : "✗ FAIL")}");

            provider.Dispose();
            Console.WriteLine();
        }

        /// <summary>
        /// Test 4: Caching performance
        /// </summary>
        private static void TestCachingPerformance()
        {
            Console.WriteLine("Test 4: Caching Performance");
            Console.WriteLine("---------------------------");

            var provider = new ByteProvider();

            // Create 10KB test file (reduced from 1MB for faster testing)
            var testData = new byte[10 * 1024]; // 10KB
            for (int i = 0; i < testData.Length; i++)
                testData[i] = (byte)(i % 256);

            provider.OpenMemory(testData);

            // Add some modifications
            for (int i = 0; i < 1000; i += 100)
                provider.ModifyByte(i, 0xFF);

            // Benchmark: Read 1000 random bytes (reduced for faster testing)
            var random = new Random(42);
            var stopwatch = Stopwatch.StartNew();

            for (int i = 0; i < 1000; i++)
            {
                long pos = random.Next(0, (int)provider.VirtualLength);
                provider.GetByte(pos);
            }

            stopwatch.Stop();
            Console.WriteLine($"  1000 random reads: {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"  Average: {stopwatch.ElapsedMilliseconds / 1000.0:F3}ms per read");

            // Show cache statistics
            var stats = provider.GetCacheStatistics();
            Console.WriteLine("\n  Cache Statistics:");
            foreach (var line in stats.Split('\n'))
                Console.WriteLine($"    {line}");

            provider.Dispose();
            Console.WriteLine();
        }

        /// <summary>
        /// Test 5: Memory source operations
        /// </summary>
        private static void TestMemorySource()
        {
            Console.WriteLine("Test 5: Memory Source");
            Console.WriteLine("---------------------");

            var provider = new ByteProvider();
            var testData = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"
            provider.OpenMemory(testData, readOnly: false);

            Console.WriteLine($"  Opened memory source: {provider.VirtualLength} bytes");
            Console.WriteLine($"  IsOpen: {provider.IsOpen} - {(provider.IsOpen ? "✓ PASS" : "✗ FAIL")}");
            Console.WriteLine($"  IsReadOnly: {provider.IsReadOnly} - {(!provider.IsReadOnly ? "✓ PASS" : "✗ FAIL")}");

            // Modify in memory
            provider.ModifyByte(0, 0x68); // 'H' -> 'h'
            var (firstByte, _) = provider.GetByte(0);
            Console.WriteLine($"  Modified first byte: 0x{firstByte:X2} (expected: 0x68) - {(firstByte == 0x68 ? "✓ PASS" : "✗ FAIL")}");

            provider.Dispose();
            Console.WriteLine();
        }

        /// <summary>
        /// Test 6: Optimized PositionMapper performance (segment-based approach)
        /// </summary>
        private static void TestOptimizedPositionMapper()
        {
            Console.WriteLine("Test 6: Optimized PositionMapper Performance");
            Console.WriteLine("--------------------------------------------");

            var provider = new ByteProvider();

            // Create 100KB test file with scattered edits
            var testData = new byte[100 * 1024];
            for (int i = 0; i < testData.Length; i++)
                testData[i] = (byte)(i % 256);

            provider.OpenMemory(testData);

            // Add scattered modifications and insertions (every 1000 bytes)
            for (int i = 0; i < testData.Length; i += 1000)
            {
                provider.ModifyByte(i, 0xFF);
                if (i % 2000 == 0)
                    provider.InsertByte(i, 0xAA);
            }

            Console.WriteLine($"  File size: {testData.Length / 1024}KB");
            Console.WriteLine($"  Edits: {provider.ModificationStats.modified} modified, {provider.ModificationStats.inserted} inserted");

            // Benchmark: Access positions near the end of file (worst case for O(n) approach)
            var stopwatch = Stopwatch.StartNew();
            long testPosition = provider.VirtualLength - 1000;

            for (int i = 0; i < 100; i++)
            {
                provider.GetByte(testPosition + i);
            }

            stopwatch.Stop();
            Console.WriteLine($"  100 reads near end of file: {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"  Average: {stopwatch.ElapsedMilliseconds / 100.0:F3}ms per read");
            Console.WriteLine($"  {(stopwatch.ElapsedMilliseconds < 50 ? "✓ PASS (Fast!)" : "✗ SLOW (check optimization)")}");

            provider.Dispose();
            Console.WriteLine();
        }

        /// <summary>
        /// Test 7: Batch operations performance
        /// </summary>
        private static void TestBatchOperations()
        {
            Console.WriteLine("Test 7: Batch Operations Performance");
            Console.WriteLine("------------------------------------");

            var provider = new ByteProvider();
            var testData = new byte[10000];
            for (int i = 0; i < testData.Length; i++)
                testData[i] = (byte)(i % 256);

            provider.OpenMemory(testData);

            // Test 7a: Non-batched modifications (slow)
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++)
            {
                provider.ModifyByte(i, 0xFF);
            }
            stopwatch.Stop();
            long nonBatchedTime = stopwatch.ElapsedMilliseconds;
            Console.WriteLine($"  Non-batched: 1000 modifications in {nonBatchedTime}ms");

            // Reset
            provider.ClearAllEdits();

            // Test 7b: Batched modifications (fast)
            stopwatch.Restart();
            provider.BeginBatch();
            for (int i = 0; i < 1000; i++)
            {
                provider.ModifyByte(i, 0xFF);
            }
            provider.EndBatch();
            stopwatch.Stop();
            long batchedTime = stopwatch.ElapsedMilliseconds;
            Console.WriteLine($"  Batched: 1000 modifications in {batchedTime}ms");

            double speedup = nonBatchedTime > 0 ? (double)nonBatchedTime / Math.Max(1, batchedTime) : 0;
            Console.WriteLine($"  Speedup: {speedup:F1}x faster");
            Console.WriteLine($"  {(speedup > 1.5 ? "✓ PASS (Batching is faster!)" : "✗ WARNING (Batching should be faster)")}");

            // Test 7c: ModifyBytes batch method
            provider.ClearAllEdits();
            stopwatch.Restart();
            byte[] values = new byte[1000];
            for (int i = 0; i < values.Length; i++)
                values[i] = 0xFF;
            provider.ModifyBytes(0, values);
            stopwatch.Stop();
            long modifyBytesTime = stopwatch.ElapsedMilliseconds;
            Console.WriteLine($"  ModifyBytes: 1000 modifications in {modifyBytesTime}ms");
            Console.WriteLine($"  {(modifyBytesTime <= batchedTime + 5 ? "✓ PASS" : "✗ WARNING")}");

            provider.Dispose();
            Console.WriteLine();
        }

        /// <summary>
        /// Test 8: Search algorithms (Boyer-Moore-Horspool optimization)
        /// </summary>
        private static void TestSearchAlgorithms()
        {
            Console.WriteLine("Test 8: Search Algorithms (Boyer-Moore-Horspool)");
            Console.WriteLine("------------------------------------------------");

            var provider = new ByteProvider();

            // Create test data with known pattern
            var testData = new byte[10000];
            for (int i = 0; i < testData.Length; i++)
                testData[i] = (byte)(i % 256);

            // Insert known pattern at specific positions
            byte[] pattern = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
            Array.Copy(pattern, 0, testData, 1000, pattern.Length);
            Array.Copy(pattern, 0, testData, 5000, pattern.Length);
            Array.Copy(pattern, 0, testData, 9000, pattern.Length);

            provider.OpenMemory(testData);

            // Create ViewModel for search methods
            var viewModel = new ViewModels.HexEditorViewModel(provider);

            // Test FindFirst
            long firstPos = viewModel.FindFirst(pattern, 0);
            Console.WriteLine($"  FindFirst: Found at {firstPos} (expected: 1000) - {(firstPos == 1000 ? "✓ PASS" : "✗ FAIL")}");

            // Test FindNext
            long secondPos = viewModel.FindNext(pattern, firstPos);
            Console.WriteLine($"  FindNext: Found at {secondPos} (expected: 5000) - {(secondPos == 5000 ? "✓ PASS" : "✗ FAIL")}");

            // Test FindLast
            long lastPos = viewModel.FindLast(pattern, 0);
            Console.WriteLine($"  FindLast: Found at {lastPos} (expected: 9000) - {(lastPos == 9000 ? "✓ PASS" : "✗ FAIL")}");

            // Test FindAll
            var allPositions = viewModel.FindAll(pattern, 0).ToList();
            Console.WriteLine($"  FindAll: Found {allPositions.Count} occurrences (expected: 3) - {(allPositions.Count == 3 ? "✓ PASS" : "✗ FAIL")}");

            // Test single byte search (optimized path)
            byte[] singleByte = new byte[] { 0xDE };
            long singlePos = viewModel.FindFirst(singleByte, 0);
            Console.WriteLine($"  Single byte search: Found at {singlePos} (expected: 1000) - {(singlePos == 1000 ? "✓ PASS" : "✗ FAIL")}");

            // Test not found case
            byte[] notFoundPattern = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };
            long notFoundPos = viewModel.FindFirst(notFoundPattern, 0);
            Console.WriteLine($"  Pattern not found: {notFoundPos} (expected: -1) - {(notFoundPos == -1 ? "✓ PASS" : "✗ FAIL")}");

            // Performance test: Search large pattern near end (Boyer-Moore should be fast)
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            long perfTestPos = viewModel.FindLast(pattern, 0);
            stopwatch.Stop();
            Console.WriteLine($"\n  Performance (FindLast): {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"  {(stopwatch.ElapsedMilliseconds < 10 ? "✓ PASS (Fast!)" : "⚠ SLOW (but might be okay)")}");

            provider.Dispose();
            Console.WriteLine();
        }

        /// <summary>
        /// Test 9: ComparisonService V2 methods
        /// </summary>
        private static void TestComparisonService()
        {
            Console.WriteLine("Test 9: ComparisonService V2");
            Console.WriteLine("----------------------------");

            var service = new Services.ComparisonService();

            // Create two providers with known differences
            var provider1 = new ByteProvider();
            var provider2 = new ByteProvider();

            var data1 = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05 };
            var data2 = new byte[] { 0x00, 0xFF, 0x02, 0xFF, 0x04, 0x05 }; // 2 differences

            provider1.OpenMemory(data1);
            provider2.OpenMemory(data2);

            // Test Compare
            var differences = service.Compare(provider1, provider2, 0).ToList();
            Console.WriteLine($"  Compare: Found {differences.Count} differences (expected: 2) - {(differences.Count == 2 ? "✓ PASS" : "✗ FAIL")}");

            if (differences.Count >= 2)
            {
                var diff1 = differences[0];
                var diff2 = differences[1];
                bool diff1Correct = diff1.BytePositionInStream == 1 && diff1.Origine == 0x01 && diff1.Destination == 0xFF;
                bool diff2Correct = diff2.BytePositionInStream == 3 && diff2.Origine == 0x03 && diff2.Destination == 0xFF;
                Console.WriteLine($"  Difference 1: Pos={diff1.BytePositionInStream}, Orig=0x{diff1.Origine:X2}, Dst=0x{diff1.Destination:X2} - {(diff1Correct ? "✓ PASS" : "✗ FAIL")}");
                Console.WriteLine($"  Difference 2: Pos={diff2.BytePositionInStream}, Orig=0x{diff2.Origine:X2}, Dst=0x{diff2.Destination:X2} - {(diff2Correct ? "✓ PASS" : "✗ FAIL")}");
            }

            // Test CountDifferences
            long diffCount = service.CountDifferences(provider1, provider2);
            Console.WriteLine($"  CountDifferences: {diffCount} (expected: 2) - {(diffCount == 2 ? "✓ PASS" : "✗ FAIL")}");

            // Test CalculateSimilarity
            double similarity = service.CalculateSimilarity(provider1, provider2);
            double expectedSimilarity = (4.0 / 6.0) * 100.0; // 4 matches out of 6 bytes
            Console.WriteLine($"  CalculateSimilarity: {similarity:F2}% (expected: {expectedSimilarity:F2}%) - {(Math.Abs(similarity - expectedSimilarity) < 0.01 ? "✓ PASS" : "✗ FAIL")}");

            // Test length difference
            var provider3 = new ByteProvider();
            var data3 = new byte[] { 0x00, 0x01, 0x02 }; // Shorter
            provider3.OpenMemory(data3);

            long diffCountLength = service.CountDifferences(provider1, provider3);
            Console.WriteLine($"  Length difference: {diffCountLength} differences (expected: 3) - {(diffCountLength == 3 ? "✓ PASS" : "✗ FAIL")}");

            // Test identical files
            var provider4 = new ByteProvider();
            provider4.OpenMemory(data1); // Same as provider1

            long identicalCount = service.CountDifferences(provider1, provider4);
            double identicalSimilarity = service.CalculateSimilarity(provider1, provider4);
            Console.WriteLine($"  Identical files: {identicalCount} differences, {identicalSimilarity:F2}% similarity - {(identicalCount == 0 && identicalSimilarity == 100.0 ? "✓ PASS" : "✗ FAIL")}");

            provider1.Dispose();
            provider2.Dispose();
            provider3.Dispose();
            provider4.Dispose();
            Console.WriteLine();
        }

        /// <summary>
        /// Quick test method for debugging (callable from outside)
        /// </summary>
        public static void QuickTest()
        {
            Console.WriteLine("ByteProvider V2 Quick Test\n");

            var provider = new ByteProvider();
            provider.OpenMemory(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04 });

            Console.WriteLine($"Initial: Length={provider.VirtualLength}");

            provider.InsertByte(2, 0xAA);
            Console.WriteLine($"After insert at 2: Length={provider.VirtualLength}");

            provider.DeleteByte(0);
            Console.WriteLine($"After delete at 0: Length={provider.VirtualLength}");

            provider.ModifyByte(1, 0xFF);
            var (value, _) = provider.GetByte(1);
            Console.WriteLine($"After modify at 1: Value=0x{value:X2}");

            Console.WriteLine($"\nHasChanges: {provider.HasChanges}");
            var (mod, ins, del) = provider.ModificationStats;
            Console.WriteLine($"Stats: {mod} modified, {ins} inserted, {del} deleted");

            provider.Dispose();
        }
    }
}
