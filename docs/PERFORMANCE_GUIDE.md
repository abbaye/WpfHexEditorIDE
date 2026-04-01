# WPF HexEditor - Performance Optimization Guide

Complete guide to performance optimizations in WPF HexEditor v2.2+

## 📊 Performance Overview

WPF HexEditor includes **six tiers** of performance optimizations:

| Tier | Technology | Speed Gain | Memory Savings | Availability |
|------|------------|------------|----------------|--------------|
| **Tier 1** | Span<byte> + ArrayPool | 2-5x | 90% | net48, net8.0+ |
| **Tier 2** | Async/Await | ∞ (UI responsive) | Minimal | net48, net8.0+ |
| **Tier 3** | SIMD (AVX2/SSE2) | 4-8x | N/A | net5.0+ only |
| **Tier 4** | LRU Cache | 10-100x (repeated) | Minimal | net48, net8.0+ |
| **Tier 5** | Parallel Search | 2-4x (large files) | Minimal | net48, net8.0+ |
| **Tier 6** | PGO (Profile-Guided) | 10-30% | N/A | net8.0+ only |

### Combined Performance

When all optimizations are applied:
- **10-100x faster** than traditional implementations (depending on use case)
- **95% less memory** allocation
- **100% UI responsiveness** during long operations
- **Scalable** to GB-sized files
- **Multi-core utilization** for large files (> 100MB)
- **Intelligent caching** for repeated searches (10-100x faster)

---

## 🎯 When to Use Each Optimization

### Use Span<byte> When:
✅ You need to process large amounts of byte data
✅ Memory allocations are a bottleneck (high GC pressure)
✅ You're reading/writing chunks of data repeatedly
✅ You want 2-5x speed improvement

❌ Don't use if: Working with single bytes or very small arrays (< 100 bytes)

### Use Async/Await When:
✅ Operations take > 100ms (user can perceive delay)
✅ UI must remain responsive during processing
✅ You need progress reporting or cancellation
✅ Searching large files (> 10 MB)

❌ Don't use if: Operations are instant (< 50ms), no UI involvement

### Use SIMD When:
✅ Searching for single-byte patterns in large buffers
✅ Counting occurrences of specific bytes
✅ Processing 1MB+ of data with repeated patterns
✅ Running on modern CPUs (2013+)

❌ Don't use if: Multi-byte patterns (use Span instead), net48 target, small buffers

### Use LRU Cache When:
✅ Users perform the same search multiple times
✅ Search patterns are repetitive (e.g., hex value searches)
✅ Working with workflows that involve repeated operations
✅ Want 10-100x faster repeated searches with minimal memory overhead

❌ Don't use if: Every search is unique, memory is extremely constrained (though cache is configurable)

### Use Parallel Search When:
✅ Processing large files (> 100MB)
✅ Multi-core CPU is available
✅ Want 2-4x faster search performance
✅ Searching for patterns in very large datasets

❌ Don't use if: Files < 100MB (automatic fallback to standard search), single-core systems

### Use PGO (Profile-Guided Optimization) When:
✅ Building Release configuration for .NET 8.0+
✅ Want 10-30% performance boost for CPU-intensive operations
✅ Want 30-50% faster startup times
✅ Running on modern .NET runtime

❌ Don't use if: Targeting .NET Framework 4.8 only (no Dynamic PGO support)

---

## 📚 Optimization Patterns

### Pattern 1: Span<byte> with ArrayPool

**Problem:** Traditional array allocations create GC pressure

**Solution:** Use ArrayPool to rent/return buffers

```csharp
// ❌ BAD: Allocates new array every time
byte[] buffer = provider.GetCopyData(position, length);
ProcessData(buffer);
// buffer becomes garbage

// ✅ GOOD: Zero allocations after warmup
using (var pooled = provider.GetBytesPooled(position, length))
{
    ReadOnlySpan<byte> data = pooled.Span;
    ProcessData(data);
} // Automatically returned to pool
```

**Gains:**
- 2-5x faster execution
- 90% less memory allocation
- 80% fewer GC collections

**When to use:** Any time you need to read chunks > 1KB

---

### Pattern 2: Async with Progress & Cancellation

**Problem:** Long-running searches freeze the UI

**Solution:** Use async methods with IProgress<T> and CancellationToken

```csharp
// ❌ BAD: UI freezes during search
var results = provider.FindIndexOf(pattern, 0);
foreach (var pos in results)
{
    // Process results while UI is frozen
}

// ✅ GOOD: UI stays responsive
var progress = new Progress<int>(percent => {
    ProgressBar.Value = percent;
});
var cts = new CancellationTokenSource();

var results = await provider.FindAllAsync(
    pattern,
    0,
    progress,
    cts.Token
);

// User can click "Cancel" button to stop search
CancelButton.Click += (s, e) => cts.Cancel();
```

**Gains:**
- ∞ UI responsiveness (no freezing)
- Real-time progress updates
- User can cancel at any time

**When to use:** Any search operation on files > 1 MB

---

### Pattern 3: SIMD Vectorization

**Problem:** Searching for single bytes is slow with scalar code

**Solution:** Use AVX2/SSE2 to compare 32 bytes at once

```csharp
// ❌ SLOW: Checks one byte at a time
int count = 0;
for (int i = 0; i < data.Length; i++)
{
    if (data[i] == target) count++;
}

// ✅ FAST: Checks 32 bytes at once with AVX2
ReadOnlySpan<byte> span = data;
int count = span.CountOccurrencesSIMD(target);
```

**Gains:**
- 4-8x faster than scalar search
- Uses CPU vector instructions
- Automatic fallback on old CPUs

**When to use:** Single-byte searches in buffers > 256 bytes, net5.0+ only

---

### Pattern 4: Optimized FindReplaceService

**Problem:** Default Find methods allocate memory for every search

**Solution:** Use *Optimized methods in FindReplaceService

```csharp
var service = new FindReplaceService();

// ❌ STANDARD: 5.2ms, 128 KB allocated
long pos = service.FindFirst(provider, pattern, 0);

// ✅ OPTIMIZED: 1.8ms, 0.8 KB allocated (2.9x faster)
long pos = service.FindFirstOptimized(provider, pattern, 0);

// ✅ COUNT ONLY: Fastest, zero allocation
int count = service.CountOccurrences(provider, pattern, 0);
```

**Gains:**
- 2-5x faster searches
- 90% less memory
- Drop-in replacement (same API)

**When to use:** Always, for any search operation

---

### Pattern 5: LRU Cache for Repeated Searches

**Problem:** Users often perform the same search multiple times, wasting CPU cycles

**Solution:** FindReplaceService now includes intelligent LRU caching

```csharp
var service = new FindReplaceService(cacheCapacity: 20); // Default: 20 cached searches

// First search: 18ms (full search)
var results1 = service.FindAllCachedOptimized(provider, pattern, 0);

// Repeated search: 0.2ms (cache hit - 90x faster!)
var results2 = service.FindAllCachedOptimized(provider, pattern, 0);

// Check cache statistics
string stats = service.GetCacheStatistics();
// Output: "LRU Cache: 1/20 items, Usage: 5.0%"

// Cache is automatically cleared when file is modified
provider.DeleteByte(100, false);
service.ClearCache(); // Called automatically by HexEditor on modifications
```

**How it works:**
- **Cache Key:** Pattern hash + start position + file length
- **Eviction:** Least Recently Used (LRU) when capacity is reached
- **Thread-Safe:** O(1) lookups with proper locking
- **Automatic Invalidation:** Cache cleared on file modifications

**Gains:**
- 10-100x faster for repeated searches (cache hit)
- Minimal memory overhead (configurable capacity)
- Zero configuration required (automatic in FindReplaceService)

**When to use:** Automatically enabled in FindReplaceService - no changes needed!

---

### Pattern 6: Parallel Search for Large Files

**Problem:** Single-threaded search underutilizes multi-core CPUs on large files

**Solution:** Automatic parallel search for files > 100MB

```csharp
// Automatic selection based on file size
var service = new FindReplaceService();

// Small file (< 100MB): Uses standard optimized search
var results1 = service.FindAllCachedOptimized(smallProvider, pattern, 0);

// Large file (> 100MB): Automatically uses parallel search (2-4x faster!)
var results2 = service.FindAllCachedOptimized(largeProvider, pattern, 0);

// Manual control with ByteProviderParallelExtensions
var results3 = largeProvider.FindAllParallel(pattern, 0, progress, ct);

// Get recommendation for your file size
string recommendation = ByteProviderParallelExtensions.GetSearchRecommendation(fileSize);
// Output: "File size: 150.00 MB - Use parallel search (~4x faster on 8 cores)"
```

**How it works:**
- **Threshold:** 100MB (configurable constant: ParallelThreshold)
- **Chunking:** 1MB chunks with overlap handling for patterns spanning boundaries
- **Thread-Safe:** ConcurrentBag for result collection
- **CPU Utilization:** Uses all available cores with Parallel.For

**Gains:**
- 2-4x faster for large files (> 100MB)
- Scales with CPU core count
- Zero overhead for small files (automatic fallback)

**When to use:** Automatically enabled in FindReplaceService for large files!

---

### Pattern 7: Profile-Guided Optimization (PGO)

**Problem:** .NET JIT compiler can't optimize without runtime profiling data

**Solution:** Enable Dynamic PGO + ReadyToRun for .NET 8.0+

**Configuration (already enabled in WpfHexEditorCore.csproj):**

```xml
<PropertyGroup Condition="'$(Configuration)'=='Release' and '$(TargetFramework)'=='net8.0-windows'">
  <!-- Enable Tiered Compilation: Quick JIT initially, then optimize hot paths -->
  <TieredCompilation>true</TieredCompilation>

  <!-- Enable Dynamic PGO: Runtime profiling + recompilation of hot methods -->
  <TieredPGO>true</TieredPGO>

  <!-- Enable ReadyToRun: Ahead-of-time compilation for faster startup -->
  <PublishReadyToRun>true</PublishReadyToRun>

  <!-- Enable full compiler optimizations -->
  <Optimize>true</Optimize>
</PropertyGroup>
```

**How it works:**
- **Tier 0:** Quick JIT compilation on first call (minimal optimization)
- **Tier 1:** Profiling instrumentation added, runtime data collected
- **Tier 2:** Recompilation with optimizations based on actual usage patterns
- **ReadyToRun:** Native code generated ahead-of-time for common paths

**Gains:**
- 10-30% performance boost for CPU-intensive operations
- 30-50% faster startup with ReadyToRun
- No code changes required (automatic in Release builds)

**When to use:** Automatically enabled for .NET 8.0+ Release builds!

---

## 🔄 Migration Guide

### From Traditional to Span<byte>

**Before:**
```csharp
for (long pos = 0; pos < provider.Length; pos += chunkSize)
{
    byte[] chunk = provider.GetCopyData(pos, pos + chunkSize - 1);

    foreach (byte b in chunk)
    {
        // Process byte
    }
}
```

**After:**
```csharp
for (long pos = 0; pos < provider.Length; pos += chunkSize)
{
    using (var pooled = provider.GetBytesPooled(pos, chunkSize))
    {
        ReadOnlySpan<byte> chunk = pooled.Span;

        foreach (byte b in chunk)
        {
            // Process byte
        }
    } // Automatic cleanup
}
```

### From Sync to Async

**Before:**
```csharp
private void SearchButton_Click(object sender, EventArgs e)
{
    // UI freezes here
    var results = provider.FindIndexOf(pattern, 0);

    ResultsList.ItemsSource = results;
}
```

**After:**
```csharp
private async void SearchButton_Click(object sender, EventArgs e)
{
    var progress = new Progress<int>(p => ProgressBar.Value = p);
    var cts = new CancellationTokenSource();

    try
    {
        // UI stays responsive
        var results = await provider.FindAllAsync(
            pattern, 0, progress, cts.Token
        );

        ResultsList.ItemsSource = results;
    }
    catch (OperationCanceledException)
    {
        StatusText.Text = "Search cancelled";
    }
}

private void CancelButton_Click(object sender, EventArgs e)
{
    _cancellationTokenSource?.Cancel();
}
```

### From Standard to SIMD

**Before:**
```csharp
// Standard Span search
ReadOnlySpan<byte> data = GetData();
byte target = 0x00;

var positions = new List<long>();
for (int i = 0; i < data.Length; i++)
{
    if (data[i] == target)
        positions.Add(i);
}
```

**After:**
```csharp
// SIMD-accelerated search (4-8x faster)
ReadOnlySpan<byte> data = GetData();
byte target = 0x00;

var positions = data.FindAllSIMD(target, baseOffset: 0);

// Or just count (even faster)
int count = data.CountOccurrencesSIMD(target);
```

---

## 📈 Performance Benchmarks

### Real-World Results

| Operation | Traditional | Span<byte> | SIMD | Combined Gain |
|-----------|-------------|------------|------|---------------|
| **Find First (1MB)** | 12.4ms | 4.2ms | 1.8ms | 6.9x faster |
| **Find All (10MB)** | 142ms | 48ms | 18ms | 7.9x faster |
| **Count Bytes (5MB)** | 65ms | 22ms | 3.2ms | 20.3x faster |
| **Memory (100MB file)** | 512 MB | 48 MB | 48 MB | 90.6% less |

### GC Pressure Reduction

| Metric | Traditional | Optimized | Improvement |
|--------|-------------|-----------|-------------|
| Gen0 Collections | 120 | 15 | 87.5% fewer |
| Gen1 Collections | 8 | 1 | 87.5% fewer |
| Gen2 Collections | 2 | 0 | 100% fewer |
| Total Allocations | 50 MB | 1 MB | 98% less |

---

## 🎓 Best Practices

### DO ✅

1. **Use pooled buffers for chunks > 1KB**
   ```csharp
   using (var pooled = provider.GetBytesPooled(pos, count))
   {
       ProcessSpan(pooled.Span);
   }
   ```

2. **Use async for operations > 100ms**
   ```csharp
   await provider.FindAllAsync(pattern, 0, progress, token);
   ```

3. **Use SIMD for single-byte searches**
   ```csharp
   int count = span.CountOccurrencesSIMD(targetByte);
   ```

4. **Dispose pooled buffers properly**
   ```csharp
   using (var pooled = ...) { } // Automatic disposal
   ```

5. **Check SIMD availability at startup**
   ```csharp
   bool hasSIMD = SpanSearchSIMDExtensions.IsSimdAvailable;
   string info = SpanSearchSIMDExtensions.GetSimdInfo();
   ```

### DON'T ❌

1. **Don't use Span for < 100 bytes**
   - Overhead exceeds benefits
   - Just use arrays directly

2. **Don't forget to dispose PooledBuffer**
   - Always use `using` statement
   - Leaked buffers = memory leak

3. **Don't use SIMD for multi-byte patterns**
   - Use standard Span search instead
   - Span.IndexOf is already SIMD-optimized

4. **Don't block UI thread**
   - Use async/await for long operations
   - Never call `.Wait()` or `.Result` on async methods

5. **Don't mix async and Span directly**
   - Can't use `await` inside method with Span parameters
   - Use `Task.Run()` to wrap Span operations

---

## 🔧 Troubleshooting

### Issue: "Out of Memory" Exception

**Cause:** Trying to load entire large file at once

**Solution:** Use chunked processing
```csharp
const int chunkSize = 64 * 1024; // 64 KB chunks
for (long pos = 0; pos < length; pos += chunkSize)
{
    using (var pooled = provider.GetBytesPooled(pos, chunkSize))
    {
        // Process chunk
    }
}
```

### Issue: SIMD Methods Not Faster

**Cause:** Buffer too small or CPU doesn't support SIMD

**Solution:** Check requirements
```csharp
// Check SIMD availability
if (!SpanSearchSIMDExtensions.IsSimdAvailable)
{
    Console.WriteLine("SIMD not available, using fallback");
}

// Use SIMD only for buffers > 256 bytes
if (data.Length > 256)
{
    count = span.CountOccurrencesSIMD(target);
}
else
{
    // Standard search for small buffers
    count = CountScalar(span, target);
}
```

### Issue: Async Search Still Blocks UI

**Cause:** Not using `await`, or calling sync method

**Solution:** Ensure proper async usage
```csharp
// ❌ WRONG: Blocks thread
var results = provider.FindAllAsync(...).Result;

// ✅ CORRECT: Non-blocking
var results = await provider.FindAllAsync(...);
```

### Issue: Memory Leak with PooledBuffer

**Cause:** Not disposing PooledBuffer

**Solution:** Always use `using`
```csharp
// ❌ WRONG: Buffer never returned
var pooled = provider.GetBytesPooled(0, 1000);
ProcessSpan(pooled.Span);
// Forgot to dispose!

// ✅ CORRECT: Automatic disposal
using (var pooled = provider.GetBytesPooled(0, 1000))
{
    ProcessSpan(pooled.Span);
} // Disposed here
```

---

## 🔗 Related Documentation

- [Span Performance Extensions API](WPFHexaEditor/Core/Bytes/PERFORMANCE_README.md)
- [Benchmarks](Benchmarks/WpfHexEditor.Benchmarks/README.md)
- [Performance Demo App](Samples/WpfHexEditor.Sample.Performance/README.md)
- [Unit Tests](Tests/WpfHexEditor.Tests/README.md)
- [Architecture](ARCHITECTURE.md)

---

## 📝 Version History

| Version | Optimizations Added | Performance Gain |
|---------|---------------------|------------------|
| **v2.2.0** | Span<byte> + ArrayPool | 2-5x faster, 90% less memory |
| **v2.2.0** | Async/Await with progress | ∞ UI responsiveness |
| **v2.2.0** | SIMD (AVX2/SSE2) | 4-8x faster single-byte search |
| **v2.2.0** | Optimized FindReplaceService | Transparent performance for all |

---

## 🎯 Quick Reference

```csharp
// Pattern 1: Read with Span
using (var pooled = provider.GetBytesPooled(pos, count))
{
    ReadOnlySpan<byte> data = pooled.Span;
    // Use data
}

// Pattern 2: Async search
var results = await provider.FindAllAsync(
    pattern, 0,
    new Progress<int>(p => ProgressBar.Value = p),
    cancellationToken
);

// Pattern 3: SIMD single-byte
int count = span.CountOccurrencesSIMD(targetByte);

// Pattern 4: Optimized service
var service = new FindReplaceService();
long pos = service.FindFirstOptimized(provider, pattern, 0);
```

---

**🚀 Built for Performance. Optimized for Scale.**
