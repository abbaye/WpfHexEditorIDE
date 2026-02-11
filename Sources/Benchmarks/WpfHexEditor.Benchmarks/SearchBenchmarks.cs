using BenchmarkDotNet.Attributes;
using WpfHexaEditor.Core.Bytes;
using System;
using System.IO;
using System.Linq;

namespace WpfHexEditor.Benchmarks
{
    /// <summary>
    /// Benchmarks for different search algorithms and optimizations
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(warmupCount: 3, iterationCount: 10)]
    public class SearchBenchmarks
    {
        private ByteProvider? _provider;
        private string _testFile = string.Empty;

        [Params(2, 4, 8, 16)] // Pattern sizes
        public int PatternSize { get; set; }

        private byte[] _pattern = Array.Empty<byte>();

        [GlobalSetup]
        public void Setup()
        {
            // Create 5 MB test file
            _testFile = Path.GetTempFileName();
            byte[] testData = new byte[5 * 1024 * 1024];
            new Random(42).NextBytes(testData);

            // Create pattern to search for
            _pattern = new byte[PatternSize];
            Array.Copy(testData, 1000, _pattern, 0, PatternSize); // Pattern exists in file

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
        /// Find first occurrence using FindIndexOf
        /// </summary>
        [Benchmark]
        public long FindFirst()
        {
            return _provider!.FindIndexOf(_pattern, 0).FirstOrDefault(-1);
        }

        /// <summary>
        /// Find all occurrences
        /// </summary>
        [Benchmark]
        public int FindAllCount()
        {
            var results = _provider!.FindIndexOf(_pattern, 0);
            return results.Count();
        }

        /// <summary>
        /// Find from middle position
        /// </summary>
        [Benchmark]
        public long FindFromMiddle()
        {
            long middlePos = _provider!.Length / 2;
            return _provider.FindIndexOf(_pattern, middlePos).FirstOrDefault(-1);
        }

        /// <summary>
        /// Enumerate all occurrences
        /// </summary>
        [Benchmark]
        public long EnumerateAll()
        {
            long sum = 0;
            foreach (long pos in _provider!.FindIndexOf(_pattern, 0))
            {
                sum += pos;
            }
            return sum;
        }
    }
}
