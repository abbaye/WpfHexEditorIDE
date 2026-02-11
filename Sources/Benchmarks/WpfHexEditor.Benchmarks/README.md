# WPF HexEditor - Performance Benchmarks

Scientific performance measurements using **BenchmarkDotNet** - the industry-standard .NET benchmarking library.

## 🎯 Purpose

Provides **precise, reproducible** performance measurements for:

1. **Span&lt;byte&gt; Optimizations** - Memory allocation comparisons
2. **Async/Await Operations** - Throughput and responsiveness
3. **Search Algorithms** - Pattern matching performance

## 🚀 Quick Start

### Run All Benchmarks

```bash
cd Sources/Benchmarks/WpfHexEditor.Benchmarks
dotnet run -c Release
```

### Run Specific Benchmark

```bash
# Only Span benchmarks
dotnet run -c Release --filter "*SpanBenchmarks*"

# Only Async benchmarks
dotnet run -c Release --filter "*AsyncBenchmarks*"

# Only Search benchmarks
dotnet run -c Release --filter "*SearchBenchmarks*"
```

### Interactive Menu

```bash
dotnet run -c Release
# Then select benchmark by number
```

## 📊 Benchmark Suites

### 1. SpanBenchmarks

**Compares:**
- ❌ `TraditionalArrayAllocation()` - Allocates arrays every iteration
- ✅ `SpanWithArrayPool()` - Uses Span&lt;byte&gt; with pooling
- ✅ `SpanExtensionMethod()` - Direct Span&lt;byte&gt; extension

**Parameters:**
- Chunk sizes: 1 KB, 8 KB, 64 KB
- Test file: 1 MB

**Metrics:**
- Execution time (Mean, Median, StdDev)
- Memory allocated per operation
- Gen 0/1/2 GC collections
- Threading overhead

**Expected Results:**
```
| Method                      | ChunkSize | Mean      | Allocated |
|---------------------------- |---------- |----------:|----------:|
| TraditionalArrayAllocation  | 8192      | 5.214 ms  | 128.5 KB  |
| SpanWithArrayPool          | 8192      | 1.856 ms  |   0.8 KB  |
| SpanExtensionMethod        | 8192      | 1.782 ms  |   0.5 KB  |
```

### 2. AsyncBenchmarks

**Compares:**
- ❌ `SynchronousFindAll()` - Blocks calling thread
- ✅ `AsynchronousFindAll()` - Non-blocking with progress
- ✅ `AsynchronousWithCancellation()` - Supports cancellation

**Parameters:**
- File sizes: 1 MB, 10 MB
- Pattern: `0x42 0x00` (appears ~100 times per MB)

**Metrics:**
- Execution time
- Memory usage
- Threading efficiency
- Async overhead

**Expected Results:**
```
| Method                          | FileSize  | Mean      | Allocated |
|-------------------------------- |---------- |----------:|----------:|
| SynchronousFindAll             | 10485760  | 142.3 ms  |  4.2 MB   |
| AsynchronousFindAll            | 10485760  | 145.8 ms  |  4.5 MB   |
| AsynchronousWithCancellation   | 10485760  | 146.1 ms  |  4.6 MB   |
```

**Note:** Async methods have slightly higher overhead (~3%) but provide UI responsiveness.

### 3. SearchBenchmarks

**Measures:**
- `FindFirst()` - First occurrence
- `FindAllCount()` - All occurrences
- `FindNext()` - Next after position
- `FindLast()` - Last occurrence

**Parameters:**
- Pattern sizes: 2, 4, 8, 16 bytes
- Test file: 5 MB

**Metrics:**
- Execution time per pattern size
- Memory allocation
- Algorithm complexity validation

**Expected Results:**
```
| Method         | PatternSize | Mean      | Allocated |
|--------------- |------------ |----------:|----------:|
| FindFirst      | 2           |  2.145 ms |   1.2 KB  |
| FindFirst      | 16          |  2.089 ms |   1.2 KB  |
| FindAllCount   | 2           | 42.378 ms |  12.5 KB  |
| FindLast       | 2           |  2.198 ms |   1.2 KB  |
```

## 🔬 Understanding Results

### Metrics Explained

**Mean:** Average execution time across all iterations

**Median:** Middle value (less affected by outliers)

**StdDev:** Standard deviation (consistency measure)

**Allocated:** Total memory allocated during operation

**Gen 0/1/2:** Number of garbage collections triggered

**Ratio:** Performance relative to baseline (lower is better)

### Reading the Output

```
| Method               | Mean     | Error    | StdDev   | Ratio | Allocated |
|--------------------- |---------:|---------:|---------:|------:|----------:|
| Baseline_Old         | 5.214 ms | 0.102 ms | 0.095 ms |  1.00 | 128.50 KB |
| Optimized_New        | 1.856 ms | 0.035 ms | 0.033 ms |  0.36 |   0.80 KB |
```

**Interpretation:**
- New method is **2.8x faster** (ratio 0.36 = 36% of baseline time)
- **160x less memory** allocated (0.8 KB vs 128.5 KB)
- **Consistent performance** (low StdDev)

## 📈 Advanced Usage

### Export Results

```bash
# Export to CSV
dotnet run -c Release --exporters csv

# Export to HTML
dotnet run -c Release --exporters html

# Export to Markdown
dotnet run -c Release --exporters markdown

# All formats
dotnet run -c Release --exporters csv html markdown
```

Results saved to: `BenchmarkDotNet.Artifacts/results/`

### Memory Profiling

```bash
# Enable detailed memory diagnostics
dotnet run -c Release --memory

# Enable ETW profiling (Windows only)
dotnet run -c Release --profiler ETW
```

### Compare Configurations

```bash
# Compare Debug vs Release
dotnet run -c Release --runtimes net8.0 --job Default

# Compare with baseline commit
git checkout baseline-commit
dotnet run -c Release > baseline.txt
git checkout current-branch
dotnet run -c Release > current.txt
# Use BenchmarkDotNet.ResultsComparer to diff
```

## 🛠️ Requirements

- **.NET 8.0 SDK** for Windows
- **Windows 10/11** (WPF dependency)
- **Administrator privileges** (optional, for ETW profiling)
- **Release mode** (Debug mode disables optimizations)

**Note:** This benchmark project requires `net8.0-windows` and `UseWPF=true` because ByteProvider depends on WPF (PresentationFramework). BenchmarkDotNet will run in a WPF-enabled console context.

## 📝 Adding Custom Benchmarks

### 1. Create Benchmark Class

```csharp
using BenchmarkDotNet.Attributes;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class MyBenchmarks
{
    [Params(100, 1000, 10000)]
    public int Size { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        // Initialize resources
    }

    [Benchmark(Baseline = true)]
    public void OldMethod()
    {
        // Original implementation
    }

    [Benchmark]
    public void NewMethod()
    {
        // Optimized implementation
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        // Dispose resources
    }
}
```

### 2. Run New Benchmark

```bash
dotnet run -c Release --filter "*MyBenchmarks*"
```

## 🎓 Best Practices

### DO:
✅ Always run in **Release mode** (`-c Release`)
✅ Close other applications to reduce noise
✅ Run multiple iterations for statistical significance
✅ Use `[MemoryDiagnoser]` to track allocations
✅ Use `[Params]` to test different input sizes
✅ Set `Baseline = true` on the original method

### DON'T:
❌ Run in Debug mode (skews results)
❌ Compare results from different machines
❌ Use Console.WriteLine in benchmarks (adds overhead)
❌ Modify code between baseline and comparison runs
❌ Run with heavy background processes active

## 📊 Sample Report

After running benchmarks, you'll see:

```
BenchmarkDotNet v0.13.12, Windows 11 (10.0.26200.0)
12th Gen Intel Core i7-12700K, 1 CPU, 12 logical and 12 physical cores
.NET SDK 8.0.100
  [Host]     : .NET 8.0.0, X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.0, X64 RyuJIT AVX2

| Method               | ChunkSize | Mean      | Error    | Ratio | Gen0   | Allocated |
|--------------------- |---------- |----------:|---------:|------:|-------:|----------:|
| TraditionalArray     | 8192      | 5.214 ms  | 0.102 ms |  1.00 | 15.625 | 128.50 KB |
| SpanWithArrayPool    | 8192      | 1.856 ms  | 0.035 ms |  0.36 |  0.000 |   0.80 KB |

// * Legends *
  Mean      : Arithmetic mean of all measurements
  Error     : Half of 99.9% confidence interval
  Ratio     : Mean of the ratio distribution ([Current]/[Baseline])
  Gen0      : GC Generation 0 collects per 1000 operations
  Allocated : Allocated memory per single operation (managed only)
  1 ms      : 1 Millisecond (0.001 sec)
```

## 🔗 Related Documentation

- [BenchmarkDotNet Official Docs](https://benchmarkdotnet.org/)
- [Performance Sample](../../Samples/WpfHexEditor.Sample.Performance/README.md) - Interactive demos
- [Performance Extensions](../../WPFHexaEditor/Core/Bytes/PERFORMANCE_README.md) - API guide

## 🤝 Contributing

Want to add more benchmarks?

1. Create new benchmark class in this project
2. Add `[MemoryDiagnoser]` and `[SimpleJob]` attributes
3. Mark baseline method with `[Benchmark(Baseline = true)]`
4. Update this README with expected results
5. Submit pull request!

Ideas for new benchmarks:
- SIMD vectorization performance
- Memory-mapped file operations
- Parallel processing scalability
- Custom encoding conversions

## 📜 License

Apache 2.0 - Same as WPF HexEditor parent project

---

**🔬 Measure twice, optimize once!**
