using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using WpfHexaEditor.Core.Bytes;
using System;
using System.Buffers;
using System.IO;

namespace WpfHexEditor.Benchmarks
{
    /// <summary>
    /// Benchmarks comparing traditional array-based operations vs Span&lt;byte&gt; with ArrayPool
    /// </summary>
    [MemoryDiagnoser]
    [ThreadingDiagnoser]
    [SimpleJob(warmupCount: 3, iterationCount: 10)]
    public class SpanBenchmarks
    {
        private ByteProvider? _provider;
        private string _testFile = string.Empty;

        [Params(1024, 8192, 65536)] // 1 KB, 8 KB, 64 KB
        public int ChunkSize { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            // Create temporary test file (1 MB)
            _testFile = Path.GetTempFileName();
            byte[] testData = new byte[1024 * 1024];
            new Random(42).NextBytes(testData);
            File.WriteAllBytes(_testFile, testData);

            _provider = new ByteProvider(_testFile);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _provider?.Dispose();
            if (File.Exists(_testFile))
            {
                try { File.Delete(_testFile); } catch { }
            }
        }

        /// <summary>
        /// ❌ BAD: Traditional array allocation (allocates memory for every read)
        /// </summary>
        [Benchmark(Baseline = true)]
        public long TraditionalArrayAllocation()
        {
            long checksum = 0;
            for (long pos = 0; pos < _provider!.Length; pos += ChunkSize)
            {
                int bytesToRead = (int)Math.Min(ChunkSize, _provider.Length - pos);
                long endPos = pos + bytesToRead - 1;

                // ❌ Allocates new array every iteration
                byte[] buffer = _provider.GetCopyData(pos, endPos, false);

                foreach (byte b in buffer)
                {
                    checksum += b;
                }
            }
            return checksum;
        }

        /// <summary>
        /// ✅ GOOD: Span&lt;byte&gt; with ArrayPool (zero allocations after warmup)
        /// </summary>
        [Benchmark]
        public long SpanWithArrayPool()
        {
            long checksum = 0;
            for (long pos = 0; pos < _provider!.Length; pos += ChunkSize)
            {
                int bytesToRead = (int)Math.Min(ChunkSize, _provider.Length - pos);

                // ✅ Gets buffer from pool, returns automatically
                using (var pooled = _provider.GetBytesPooled(pos, bytesToRead))
                {
                    ReadOnlySpan<byte> span = pooled.Span;
                    foreach (byte b in span)
                    {
                        checksum += b;
                    }
                }
            }
            return checksum;
        }

        /// <summary>
        /// ✅ GOOD: ReadOnlySpan&lt;byte&gt; extension method
        /// </summary>
        [Benchmark]
        public long SpanExtensionMethod()
        {
            long checksum = 0;
            for (long pos = 0; pos < _provider!.Length; pos += ChunkSize)
            {
                int bytesToRead = (int)Math.Min(ChunkSize, _provider.Length - pos);

                // ✅ Direct Span access
                ReadOnlySpan<byte> span = _provider.GetBytesSpan(pos, bytesToRead, out byte[] buffer);
                foreach (byte b in span)
                {
                    checksum += b;
                }
            }
            return checksum;
        }
    }
}
