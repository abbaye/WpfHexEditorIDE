// ==========================================================
// Project: WpfHexEditor.Benchmarks
// File: Benchmarks/FormatDetectionBenchmarks.cs
// Description:
//     Benchmarks for format detection across 8 representative file type signatures.
//     Detection is capped at 4 KB per file as per WHFMT v2 design.
//
// Baseline target: < 50 ms per detection call
// ==========================================================

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using WpfHexEditor.Core.Services;

namespace WpfHexEditor.Benchmarks.Benchmarks;

[SimpleJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
[BenchmarkCategory("FormatDetection")]
public class FormatDetectionBenchmarks
{
    private readonly Dictionary<string, byte[]> _samples = new();
    private FormatDetectionService _detector = null!;

    private static readonly byte[] PeSignature   = [0x4D, 0x5A]; // MZ
    private static readonly byte[] ElfSignature  = [0x7F, 0x45, 0x4C, 0x46]; // ELF
    private static readonly byte[] PngSignature  = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] ZipSignature  = [0x50, 0x4B, 0x03, 0x04];
    private static readonly byte[] PdfSignature  = [0x25, 0x50, 0x44, 0x46]; // %PDF
    private static readonly byte[] GzipSignature = [0x1F, 0x8B];
    private static readonly byte[] Mp4Signature  = [0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70];

    [GlobalSetup]
    public void Setup()
    {
        _detector = new FormatDetectionService();

        // Build 4 KB sample buffers with correct magic bytes
        _samples["PE"]   = MakeSample(PeSignature,   4096);
        _samples["ELF"]  = MakeSample(ElfSignature,  4096);
        _samples["PNG"]  = MakeSample(PngSignature,  4096);
        _samples["ZIP"]  = MakeSample(ZipSignature,  4096);
        _samples["PDF"]  = MakeSample(PdfSignature,  4096);
        _samples["GZIP"] = MakeSample(GzipSignature, 4096);
        _samples["MP4"]  = MakeSample(Mp4Signature,  4096);
        _samples["UNKN"] = new byte[4096]; // random — should return Unknown
        Random.Shared.NextBytes(_samples["UNKN"]);
    }

    [Benchmark]
    [Arguments("PE")]
    [Arguments("ELF")]
    [Arguments("PNG")]
    [Arguments("ZIP")]
    [Arguments("PDF")]
    [Arguments("GZIP")]
    [Arguments("MP4")]
    [Arguments("UNKN")]
    public object DetectFormat(string key)
        => _detector.DetectFormat(_samples[key]);

    private static byte[] MakeSample(byte[] magic, int totalSize)
    {
        var buf = new byte[totalSize];
        Random.Shared.NextBytes(buf);
        Array.Copy(magic, buf, Math.Min(magic.Length, buf.Length));
        return buf;
    }
}
