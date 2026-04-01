// ==========================================================
// Project: WpfHexEditor.Benchmarks
// File: Benchmarks/ChecksumBenchmarks.cs
// Description:
//     Benchmarks for MD5, SHA256, and CRC32 checksum operations
//     on 1 MB and 100 MB byte buffers.
//
// Baseline targets:
//     MD5  / SHA256 / CRC32 on 1 MB   : < 10 ms
//     MD5  / SHA256 / CRC32 on 100 MB : < 500 ms
// ==========================================================

using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace WpfHexEditor.Benchmarks.Benchmarks;

[SimpleJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
[BenchmarkCategory("Checksum")]
public class ChecksumBenchmarks
{
    private byte[] _data1Mb  = [];
    private byte[] _data100Mb = [];

    [GlobalSetup]
    public void Setup()
    {
        _data1Mb   = new byte[1   * 1024 * 1024];
        _data100Mb = new byte[100 * 1024 * 1024];
        Random.Shared.NextBytes(_data1Mb);
        Random.Shared.NextBytes(_data100Mb);
    }

    [Benchmark] public byte[] Md5_1Mb()    => MD5.HashData(_data1Mb);
    [Benchmark] public byte[] Md5_100Mb()  => MD5.HashData(_data100Mb);
    [Benchmark] public byte[] Sha256_1Mb() => SHA256.HashData(_data1Mb);
    [Benchmark] public byte[] Sha256_100Mb() => SHA256.HashData(_data100Mb);
    [Benchmark] public uint   Crc32_1Mb()  => ComputeCrc32(_data1Mb);
    [Benchmark] public uint   Crc32_100Mb() => ComputeCrc32(_data100Mb);

    private static uint ComputeCrc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
        }
        return ~crc;
    }
}
