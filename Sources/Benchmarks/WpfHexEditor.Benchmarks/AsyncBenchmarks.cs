using BenchmarkDotNet.Attributes;
using WpfHexaEditor.Core.Bytes;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WpfHexEditor.Benchmarks
{
    /// <summary>
    /// Benchmarks comparing synchronous vs asynchronous search operations
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(warmupCount: 2, iterationCount: 5)]
    public class AsyncBenchmarks
    {
        private ByteProvider? _provider;
        private string _testFile = string.Empty;
        private byte[] _pattern = new byte[] { 0x42, 0x00 }; // Pattern that appears multiple times

        [Params(1048576, 10485760)] // 1 MB, 10 MB
        public int FileSize { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            // Create test file with known pattern
            _testFile = Path.GetTempFileName();
            byte[] testData = new byte[FileSize];

            Random rnd = new Random(42);
            rnd.NextBytes(testData);

            // Insert pattern at known positions (every 10KB)
            for (int i = 0; i < testData.Length - _pattern.Length; i += 10240)
            {
                Array.Copy(_pattern, 0, testData, i, _pattern.Length);
            }

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
        /// ❌ BLOCKING: Synchronous FindIndexOf (blocks thread)
        /// </summary>
        [Benchmark(Baseline = true)]
        public long SynchronousFindAll()
        {
            var results = _provider!.FindIndexOf(_pattern, 0);
            return results.Sum();
        }

        /// <summary>
        /// ✅ NON-BLOCKING: Asynchronous FindAll with progress
        /// </summary>
        [Benchmark]
        public async Task<long> AsynchronousFindAll()
        {
            var progress = new Progress<int>(); // No-op progress for benchmarking
            var results = await _provider!.FindAllAsync(_pattern, 0, progress, CancellationToken.None);
            return results.Sum();
        }

        /// <summary>
        /// ✅ CANCELLABLE: Async with early cancellation support
        /// </summary>
        [Benchmark]
        public async Task<long> AsynchronousWithCancellation()
        {
            using var cts = new CancellationTokenSource();
            var progress = new Progress<int>();

            var results = await _provider!.FindAllAsync(_pattern, 0, progress, cts.Token);
            return results.Sum();
        }
    }
}
